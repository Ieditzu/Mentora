using System;
using System.Collections;
using System.Collections.Generic;
using Meta.WitAi;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Requests;
using Meta.WitAi.TTS.Integrations;
using Meta.WitAi.TTS.Utilities;
using UnityEngine;
using UnityEngine.Networking;

public sealed class RobotVoiceBridge : MonoBehaviour
{
    private const string WitConfigurationResourcePath = "Voice/RobotWitConfiguration";
    private const float SpeechStartLevel = 0.018f;
    private const float SpeechContinueLevel = 0.009f;
    private const float EndSilenceSeconds = 0.72f;
    private const float MinUtteranceSeconds = 0.35f;
    private const float MaxUtteranceSeconds = 7f;
    private const float PreRollSeconds = 0.35f;
    private const int MinVoicedFrames = 6;
    private const float MinPeakLevel = 0.028f;
    private const float TargetSttPeak = 0.82f;
    private const float MaxSttGain = 8f;

    public event Action<string> FullTranscriptionReceived;
    public float MicLevel { get; private set; }
    public bool HasSpeechRecognition => witConfigured;
    public bool IsListening => listeningRequested && microphoneManager != null && microphoneManager.IsMicrophoneCapturing;
    public bool IsSpeaking => (speaker != null && speaker.IsActive) || (ttsAudioSource != null && ttsAudioSource.isPlaying);

    private TTSSpeaker speaker;
    private TTSWit ttsService;
    private AudioSource ttsAudioSource;
    private MultiplayerSessionManager microphoneManager;
    private string witClientAccessToken;
    private string dictationEndpointUrl;
    private string speechEndpointUrl;
    private bool witConfigured;
    private bool warnedMissingConfig;
    private bool listeningRequested;
    private bool microphoneAcquired;
    private bool utteranceActive;
    private bool transcriptionRequestActive;
    private float utteranceStartTime;
    private float lastSpeechTime;
    private float utterancePeakLevel;
    private int voicedFrameCount;
    private int utteranceSampleRate = 16000;
    private readonly List<byte> utterancePcm = new List<byte>(16000 * 2 * 8);
    private readonly Queue<byte[]> preRollFrames = new Queue<byte[]>();
    private int preRollByteCount;

    public void Initialize()
    {
        if (witConfigured || speaker != null)
        {
            return;
        }

        WitConfiguration config = Resources.Load<WitConfiguration>(WitConfigurationResourcePath);
        witClientAccessToken = config == null ? string.Empty : config.GetClientAccessToken();
        witConfigured = config != null && !string.IsNullOrWhiteSpace(witClientAccessToken);
        if (!witConfigured)
        {
            WarnMissingConfig();
            return;
        }

        dictationEndpointUrl = BuildEndpointUrl(config, useDictation: true);
        speechEndpointUrl = BuildEndpointUrl(config, useDictation: false);

        GameObject ttsObject = new GameObject("RudolfApiTTS");
        ttsObject.transform.SetParent(transform, false);
        ttsObject.SetActive(false);

        ttsService = ttsObject.AddComponent<TTSWit>();
        ttsService.RequestSettings = new TTSWitRequestSettings
        {
            configuration = config,
            audioType = TTSWitAudioType.PCM,
            audioStream = true
        };

        ttsAudioSource = ttsObject.AddComponent<AudioSource>();
        ttsAudioSource.playOnAwake = false;
        ttsAudioSource.spatialBlend = 1f;
        ttsAudioSource.minDistance = 1.2f;
        ttsAudioSource.maxDistance = 12f;
        ttsAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        ttsAudioSource.dopplerLevel = 0f;
        ttsAudioSource.spread = 35f;

        speaker = ttsObject.AddComponent<TTSSpeaker>();
        speaker.presetVoiceID = string.Empty;
        speaker.customWitVoiceSettings = new TTSWitVoiceSettings { voice = "Charlie", style = "default" };
        ttsObject.SetActive(true);
    }

    public void SetListening(bool shouldListen)
    {
        if (!witConfigured)
        {
            if (shouldListen)
            {
                WarnMissingConfig();
            }
            return;
        }

        if (shouldListen)
        {
            BeginSharedMicrophoneListening();
        }
        else
        {
            EndSharedMicrophoneListening();
        }
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (speaker == null || !witConfigured)
        {
            WarnMissingConfig();
            return;
        }

        speaker.Speak(text);
    }

    private void OnDestroy()
    {
        EndSharedMicrophoneListening();
    }

    private void RaiseFullTranscription(string text)
    {
        string trimmed = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        FullTranscriptionReceived?.Invoke(trimmed);
    }

    private void BeginSharedMicrophoneListening()
    {
        if (listeningRequested)
        {
            return;
        }

        listeningRequested = true;
        microphoneManager = MultiplayerSessionManager.Instance;
        microphoneManager.LocalVoiceFrameCaptured += OnLocalVoiceFrameCaptured;
        microphoneManager.AcquireExternalVoiceCapture();
        microphoneAcquired = true;
    }

    private void EndSharedMicrophoneListening()
    {
        if (!listeningRequested && !microphoneAcquired)
        {
            return;
        }

        listeningRequested = false;
        if (microphoneManager != null)
        {
            microphoneManager.LocalVoiceFrameCaptured -= OnLocalVoiceFrameCaptured;
        }
        if (microphoneAcquired)
        {
            if (microphoneManager != null)
            {
                microphoneManager.ReleaseExternalVoiceCapture();
            }
            microphoneAcquired = false;
        }

        microphoneManager = null;
        ResetUtterance();
        MicLevel = 0f;
    }

    private void OnLocalVoiceFrameCaptured(byte[] pcm16, int sampleRate, float level)
    {
        MicLevel = Mathf.Clamp01(level);
        if (!listeningRequested ||
            pcm16 == null ||
            pcm16.Length == 0 ||
            IsSpeaking ||
            transcriptionRequestActive)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!utteranceActive)
        {
            AddPreRollFrame(pcm16, sampleRate);
        }

        bool hasSpeech = level >= (utteranceActive ? SpeechContinueLevel : SpeechStartLevel);
        bool startedThisFrame = false;
        if (hasSpeech)
        {
            if (!utteranceActive)
            {
                utteranceActive = true;
                utteranceStartTime = now;
                utterancePcm.Clear();
                utteranceSampleRate = sampleRate;
                utterancePeakLevel = 0f;
                voicedFrameCount = 0;
                CopyPreRollToUtterance();
                startedThisFrame = true;
            }

            voicedFrameCount++;
            utterancePeakLevel = Mathf.Max(utterancePeakLevel, level);
            lastSpeechTime = now;
        }

        if (!utteranceActive)
        {
            return;
        }

        if (!startedThisFrame)
        {
            utterancePcm.AddRange(pcm16);
        }

        if ((now - lastSpeechTime >= EndSilenceSeconds) || (now - utteranceStartTime >= MaxUtteranceSeconds))
        {
            SubmitUtterance();
        }
    }

    private void SubmitUtterance()
    {
        float utteranceSeconds = Time.unscaledTime - utteranceStartTime;
        if (utteranceSeconds < MinUtteranceSeconds ||
            utterancePcm.Count < utteranceSampleRate ||
            voicedFrameCount < MinVoicedFrames ||
            utterancePeakLevel < MinPeakLevel)
        {
            ResetUtterance();
            return;
        }

        byte[] pcm16 = utterancePcm.ToArray();
        int sampleRate = utteranceSampleRate;
        float peakLevel = utterancePeakLevel;
        ResetUtterance();

        if (!witConfigured || transcriptionRequestActive)
        {
            return;
        }

        StartCoroutine(TranscribePcm16(pcm16, sampleRate, peakLevel));
    }

    private void ResetUtterance()
    {
        utteranceActive = false;
        utteranceStartTime = 0f;
        lastSpeechTime = 0f;
        utterancePeakLevel = 0f;
        voicedFrameCount = 0;
        utterancePcm.Clear();
        preRollFrames.Clear();
        preRollByteCount = 0;
    }

    private void AddPreRollFrame(byte[] pcm16, int sampleRate)
    {
        byte[] copy = new byte[pcm16.Length];
        Buffer.BlockCopy(pcm16, 0, copy, 0, pcm16.Length);
        preRollFrames.Enqueue(copy);
        preRollByteCount += copy.Length;

        int maxBytes = Mathf.CeilToInt(sampleRate * 2f * PreRollSeconds);
        while (preRollByteCount > maxBytes && preRollFrames.Count > 0)
        {
            preRollByteCount -= preRollFrames.Dequeue().Length;
        }
    }

    private void CopyPreRollToUtterance()
    {
        foreach (byte[] frame in preRollFrames)
        {
            utterancePcm.AddRange(frame);
        }
    }

    private IEnumerator TranscribePcm16(byte[] pcm16, int sampleRate, float peakLevel)
    {
        transcriptionRequestActive = true;

        string dictationResponse = string.Empty;
        string dictationError = string.Empty;
        yield return SendWitAudioRequest(dictationEndpointUrl, pcm16, sampleRate, (response, error) =>
        {
            dictationResponse = response;
            dictationError = error;
        });

        string transcript = ExtractTranscript(dictationResponse);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            string speechResponse = string.Empty;
            string speechError = string.Empty;
            yield return SendWitAudioRequest(speechEndpointUrl, pcm16, sampleRate, (response, error) =>
            {
                speechResponse = response;
                speechError = error;
            });

            transcript = ExtractTranscript(speechResponse);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                byte[] normalizedPcm = NormalizePcm16ForStt(pcm16, out float appliedGain, out float audioPeak);
                if (appliedGain > 1.05f)
                {
                    string normalizedDictationResponse = string.Empty;
                    string normalizedDictationError = string.Empty;
                    yield return SendWitAudioRequest(dictationEndpointUrl, normalizedPcm, sampleRate, (response, error) =>
                    {
                        normalizedDictationResponse = response;
                        normalizedDictationError = error;
                    });

                    transcript = ExtractTranscript(normalizedDictationResponse);
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        string normalizedSpeechResponse = string.Empty;
                        string normalizedSpeechError = string.Empty;
                        yield return SendWitAudioRequest(speechEndpointUrl, normalizedPcm, sampleRate, (response, error) =>
                        {
                            normalizedSpeechResponse = response;
                            normalizedSpeechError = error;
                        });

                        transcript = ExtractTranscript(normalizedSpeechResponse);
                        if (string.IsNullOrWhiteSpace(transcript))
                        {
                            string wavSpeechResponse = string.Empty;
                            string wavSpeechError = string.Empty;
                            yield return SendWitWavRequest(speechEndpointUrl, normalizedPcm, sampleRate, (response, error) =>
                            {
                                wavSpeechResponse = response;
                                wavSpeechError = error;
                            });

                            transcript = ExtractTranscript(wavSpeechResponse);
                            if (string.IsNullOrWhiteSpace(transcript))
                            {
                                LogNoTranscript(
                                    pcm16.Length,
                                    peakLevel,
                                    audioPeak,
                                    appliedGain,
                                    dictationError,
                                    dictationResponse,
                                    speechError,
                                    speechResponse,
                                    normalizedDictationError,
                                    normalizedDictationResponse,
                                    normalizedSpeechError,
                                    normalizedSpeechResponse + " wavSpeechError=" + wavSpeechError + " wavSpeechResponse=" + wavSpeechResponse);
                                transcriptionRequestActive = false;
                                yield break;
                            }
                        }
                    }
                }
                else
                {
                    string wavSpeechResponse = string.Empty;
                    string wavSpeechError = string.Empty;
                    yield return SendWitWavRequest(speechEndpointUrl, pcm16, sampleRate, (response, error) =>
                    {
                        wavSpeechResponse = response;
                        wavSpeechError = error;
                    });

                    transcript = ExtractTranscript(wavSpeechResponse);
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        transcriptionRequestActive = false;
                        RaiseFullTranscription(transcript);
                        yield break;
                    }

                    LogNoTranscript(
                        pcm16.Length,
                        peakLevel,
                        audioPeak,
                        appliedGain,
                        dictationError,
                        dictationResponse,
                        speechError,
                        speechResponse,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        "wavSpeechError=" + wavSpeechError + " wavSpeechResponse=" + wavSpeechResponse);
                    transcriptionRequestActive = false;
                    yield break;
                }
            }
        }

        transcriptionRequestActive = false;
        RaiseFullTranscription(transcript);
    }

    private static byte[] NormalizePcm16ForStt(byte[] pcm16, out float appliedGain, out float audioPeak)
    {
        appliedGain = 1f;
        audioPeak = 0f;
        if (pcm16 == null || pcm16.Length < 2)
        {
            return pcm16;
        }

        int maxAbs = 0;
        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            int abs = Mathf.Abs(sample);
            if (abs > maxAbs)
            {
                maxAbs = abs;
            }
        }

        audioPeak = maxAbs / 32768f;
        if (maxAbs <= 0)
        {
            return pcm16;
        }

        appliedGain = Mathf.Clamp((TargetSttPeak * 32767f) / maxAbs, 1f, MaxSttGain);
        if (appliedGain <= 1.05f)
        {
            return pcm16;
        }

        byte[] normalized = new byte[pcm16.Length];
        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            int amplified = Mathf.Clamp(Mathf.RoundToInt(sample * appliedGain), short.MinValue, short.MaxValue);
            normalized[i] = (byte)(amplified & 0xff);
            normalized[i + 1] = (byte)((amplified >> 8) & 0xff);
        }

        return normalized;
    }

    private static void LogNoTranscript(
        int byteCount,
        float vadPeak,
        float audioPeak,
        float appliedGain,
        string dictationError,
        string dictationResponse,
        string speechError,
        string speechResponse,
        string normalizedDictationError,
        string normalizedDictationResponse,
        string normalizedSpeechError,
        string normalizedSpeechResponse)
    {
        UnityEngine.Debug.LogWarning(
            "[RobotVoice] Wit had no transcript. bytes=" + byteCount +
            " vadPeak=" + vadPeak.ToString("0.000") +
            " audioPeak=" + audioPeak.ToString("0.000") +
            " gain=" + appliedGain.ToString("0.00") +
            " dictationError=" + dictationError +
            " dictationResponse=" + dictationResponse +
            " speechError=" + speechError +
            " speechResponse=" + speechResponse +
            " normalizedDictationError=" + normalizedDictationError +
            " normalizedDictationResponse=" + normalizedDictationResponse +
            " normalizedSpeechError=" + normalizedSpeechError +
            " normalizedSpeechResponse=" + normalizedSpeechResponse);
    }

    private IEnumerator SendWitAudioRequest(string endpointUrl, byte[] pcm16, int sampleRate, Action<string, string> complete)
    {
        using (UnityWebRequest request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.chunkedTransfer = true;
            string contentType = "audio/raw;bits=16;rate=" + (sampleRate / 1000) + "k;encoding=signed-integer;endian=little";
            request.uploadHandler = new UploadHandlerRaw(pcm16) { contentType = contentType };
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + witClientAccessToken);
            request.SetRequestHeader("Content-Type", contentType);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            string response = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            string error = request.result == UnityWebRequest.Result.Success ? string.Empty : request.error;
            complete?.Invoke(response, error);
        }
    }

    private IEnumerator SendWitWavRequest(string endpointUrl, byte[] pcm16, int sampleRate, Action<string, string> complete)
    {
        byte[] wavBytes = BuildWavBytes(pcm16, sampleRate);
        using (UnityWebRequest request = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(wavBytes) { contentType = "audio/wav" };
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + witClientAccessToken);
            request.SetRequestHeader("Content-Type", "audio/wav");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            string response = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            string error = request.result == UnityWebRequest.Result.Success ? string.Empty : request.error;
            complete?.Invoke(response, error);
        }
    }

    private static string BuildEndpointUrl(WitConfiguration config, bool useDictation)
    {
        IWitRequestEndpointInfo endpoint = config.GetEndpointInfo();
        string url = endpoint.UriScheme + "://" + endpoint.Authority;
        if (endpoint.Port > 0 && endpoint.Port != 443)
        {
            url += ":" + endpoint.Port;
        }

        string command = useDictation ? endpoint.Dictation : endpoint.Speech;
        return url + "/" + command + "?v=" + UnityWebRequest.EscapeURL(endpoint.WitApiVersion);
    }

    private static string ExtractTranscript(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        string transcript = string.Empty;
        string[] lines = responseText.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line[0] != '{')
            {
                continue;
            }

            try
            {
                WitSpeechResponse response = JsonUtility.FromJson<WitSpeechResponse>(line);
                string lineTranscript = response == null
                    ? string.Empty
                    : (!string.IsNullOrWhiteSpace(response.text) ? response.text : response._text);
                if (!string.IsNullOrWhiteSpace(lineTranscript))
                {
                    transcript = lineTranscript;
                }
            }
            catch (ArgumentException)
            {
                string lineTranscript = ExtractJsonStringValue(line, "text");
                if (string.IsNullOrWhiteSpace(lineTranscript))
                {
                    lineTranscript = ExtractJsonStringValue(line, "_text");
                }
                if (!string.IsNullOrWhiteSpace(lineTranscript))
                {
                    transcript = lineTranscript;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(transcript))
        {
            return transcript;
        }

        transcript = ExtractJsonStringValue(responseText, "text");
        return !string.IsNullOrWhiteSpace(transcript)
            ? transcript
            : ExtractJsonStringValue(responseText, "_text");
    }

    private static string ExtractJsonStringValue(string json, string key)
    {
        string marker = "\"" + key + "\"";
        int searchIndex = 0;
        string value = string.Empty;
        while (searchIndex < json.Length)
        {
            int keyIndex = json.IndexOf(marker, searchIndex, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                break;
            }

            int colonIndex = json.IndexOf(':', keyIndex + marker.Length);
            if (colonIndex < 0)
            {
                break;
            }

            int quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0)
            {
                break;
            }

            string parsed = ReadJsonString(json, quoteStart);
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                value = parsed;
            }

            searchIndex = quoteStart + 1;
        }

        return value;
    }

    private static string ReadJsonString(string json, int quoteStart)
    {
        var builder = new System.Text.StringBuilder();
        bool escape = false;
        for (int i = quoteStart + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (escape)
            {
                builder.Append(c switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    '/' => '/',
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => c
                });
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                break;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static byte[] BuildWavBytes(List<byte> pcm16, int sampleRate)
    {
        return BuildWavBytes(pcm16.ToArray(), sampleRate);
    }

    private static byte[] BuildWavBytes(byte[] pcm16, int sampleRate)
    {
        int dataLength = pcm16.Length;
        byte[] wav = new byte[44 + dataLength];
        WriteAscii(wav, 0, "RIFF");
        WriteInt32(wav, 4, 36 + dataLength);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt32(wav, 16, 16);
        WriteInt16(wav, 20, 1);
        WriteInt16(wav, 22, 1);
        WriteInt32(wav, 24, sampleRate);
        WriteInt32(wav, 28, sampleRate * 2);
        WriteInt16(wav, 32, 2);
        WriteInt16(wav, 34, 16);
        WriteAscii(wav, 36, "data");
        WriteInt32(wav, 40, dataLength);
        Buffer.BlockCopy(pcm16, 0, wav, 44, dataLength);
        return wav;
    }

    private static void WriteAscii(byte[] target, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            target[offset + i] = (byte)value[i];
        }
    }

    private static void WriteInt16(byte[] target, int offset, short value)
    {
        target[offset] = (byte)(value & 0xff);
        target[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static void WriteInt32(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value & 0xff);
        target[offset + 1] = (byte)((value >> 8) & 0xff);
        target[offset + 2] = (byte)((value >> 16) & 0xff);
        target[offset + 3] = (byte)((value >> 24) & 0xff);
    }

    private void WarnMissingConfig()
    {
        if (warnedMissingConfig)
        {
            return;
        }

        warnedMissingConfig = true;
        UnityEngine.Debug.LogWarning("[RobotVoice] Missing Resources/Voice/RobotWitConfiguration with a client access token. Rudolf API voice input/output is disabled.");
    }

    [Serializable]
    private sealed class WitSpeechResponse
    {
        public string text;
        public string _text;
    }
}
