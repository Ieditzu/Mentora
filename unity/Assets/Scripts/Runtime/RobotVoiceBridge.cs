using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    public const string OpenAiApiKeyPrefKey = "RudolfOpenAIApiKey";

    private const string WitConfigurationResourcePath = "Voice/RobotWitConfiguration";
    private static readonly bool ServerSpeechTranscriptionEnabled = true;
    private const string OpenAiTranscriptionUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string OpenAiSpeechUrl = "https://api.openai.com/v1/audio/speech";
    private const string OpenAiTranscriptionModel = "gpt-4o-transcribe";
    private const string OpenAiTtsModel = "gpt-4o-mini-tts";
    private const string OpenAiTtsVoice = "cedar";
    private const float SpeechStartLevel = 0.012f;
    private const float SpeechContinueLevel = 0.0065f;
    private const float EndSilenceSeconds = 0.9f;
    private const float MinUtteranceSeconds = 0.25f;
    private const float MaxUtteranceSeconds = 8f;
    private const float PreRollSeconds = 0.55f;
    private const int MinVoicedFrames = 4;
    private const float MinPeakLevel = 0.014f;
    private const float TargetSttPeak = 0.9f;
    private const float MaxSttGain = 18f;

    public event Action<string> FullTranscriptionReceived;
    public event Action<byte[], int, float> VoiceUtteranceCapturedForServer;
    public event Action<int, float, float, float> TranscriptionFailedWithoutText;
    public float MicLevel { get; private set; }
    public bool HasSpeechRecognition => ServerSpeechTranscriptionEnabled || openAiConfigured || witConfigured;
    public bool IsListening => listeningRequested && microphoneManager != null && microphoneManager.IsMicrophoneCapturing;
    public bool IsSpeaking => openAiTtsRequestActive || (speaker != null && speaker.IsActive) || (ttsAudioSource != null && ttsAudioSource.isPlaying);
    public bool HasPendingUtterance => utteranceActive && utterancePcm.Count > 0;
    public string VoiceProviderLabel => ServerSpeechTranscriptionEnabled ? "Server Groq Whisper" : (openAiConfigured ? "OpenAI Audio" : (witConfigured ? "Wit fallback" : "Not configured"));

    private TTSSpeaker speaker;
    private TTSWit ttsService;
    private AudioSource ttsAudioSource;
    private MultiplayerSessionManager microphoneManager;
    private string openAiApiKey;
    private string witClientAccessToken;
    private string dictationEndpointUrl;
    private string speechEndpointUrl;
    private bool openAiConfigured;
    private bool witConfigured;
    private bool warnedMissingConfig;
    private bool listeningRequested;
    private bool microphoneAcquired;
    private bool utteranceActive;
    private bool transcriptionRequestActive;
    private bool openAiTtsRequestActive;
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
        if (ttsAudioSource != null)
        {
            return;
        }

        RefreshOpenAiConfiguration();

        WitConfiguration config = Resources.Load<WitConfiguration>(WitConfigurationResourcePath);
        witClientAccessToken = config == null ? string.Empty : config.GetClientAccessToken();
        witConfigured = config != null && !string.IsNullOrWhiteSpace(witClientAccessToken);

        GameObject ttsObject = new GameObject("RudolfApiTTS");
        ttsObject.transform.SetParent(transform, false);
        ttsObject.SetActive(false);

        if (witConfigured)
        {
            dictationEndpointUrl = BuildEndpointUrl(config, useDictation: true);
            speechEndpointUrl = BuildEndpointUrl(config, useDictation: false);

            ttsService = ttsObject.AddComponent<TTSWit>();
            ttsService.RequestSettings = new TTSWitRequestSettings
            {
                configuration = config,
                audioType = TTSWitAudioType.PCM,
                audioStream = true
            };
        }

        ttsAudioSource = ttsObject.AddComponent<AudioSource>();
        ttsAudioSource.playOnAwake = false;
        ttsAudioSource.spatialBlend = 0.2f;
        ttsAudioSource.minDistance = 15f;
        ttsAudioSource.maxDistance = 80f;
        ttsAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        ttsAudioSource.dopplerLevel = 0f;
        ttsAudioSource.spread = 160f;
        ttsAudioSource.volume = 1f;

        if (witConfigured)
        {
            speaker = ttsObject.AddComponent<TTSSpeaker>();
            speaker.presetVoiceID = string.Empty;
            speaker.customWitVoiceSettings = new TTSWitVoiceSettings { voice = "Charlie", style = "default" };
        }

        ttsObject.SetActive(true);

        if (!ServerSpeechTranscriptionEnabled && !openAiConfigured && !witConfigured)
        {
            WarnMissingConfig();
        }
    }

    public void SetListening(bool shouldListen)
    {
        RefreshOpenAiConfiguration();
        if (!HasSpeechRecognition)
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
            EndSharedMicrophoneListening(false);
        }
    }

    public void FinishListening(bool submitPendingUtterance)
    {
        EndSharedMicrophoneListening(submitPendingUtterance);
    }

    public void Speak(string text)
    {
        Speak(text, "encouraging");
    }

    public void Speak(string text, string emotion)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        RefreshOpenAiConfiguration();
        if (openAiConfigured)
        {
            StartCoroutine(SpeakOpenAi(text, emotion));
            return;
        }

        if (speaker == null || !witConfigured)
        {
            WarnMissingConfig();
            return;
        }

        ApplyFallbackVoiceEmotion(emotion);
        speaker.Speak(text);
    }

    public void RefreshOpenAiConfiguration()
    {
        string savedKey = PlayerPrefs.GetString(OpenAiApiKeyPrefKey, string.Empty);
        string environmentKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        openAiApiKey = !string.IsNullOrWhiteSpace(savedKey) ? savedKey.Trim() : (environmentKey ?? string.Empty).Trim();
        openAiConfigured = !string.IsNullOrWhiteSpace(openAiApiKey);
    }

    private void OnDestroy()
    {
        EndSharedMicrophoneListening(false);
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

    private void EndSharedMicrophoneListening(bool submitPendingUtterance)
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
        if (submitPendingUtterance && utteranceActive && !transcriptionRequestActive)
        {
            SubmitUtterance();
        }
        else
        {
            ResetUtterance();
        }
        MicLevel = 0f;
    }

    private void OnLocalVoiceFrameCaptured(byte[] pcm16, int sampleRate, float level)
    {
        if (!listeningRequested ||
            pcm16 == null ||
            pcm16.Length == 0 ||
            IsSpeaking ||
            transcriptionRequestActive)
        {
            return;
        }

        if (RobotCompanion.CurrentRudolfVoiceMode == RobotCompanion.RudolfVoiceMode.Disabled)
        {
            MicLevel = 0f;
            ResetUtterance();
            return;
        }

        MicLevel = Mathf.Clamp01(level);
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
        int minByteCount = Mathf.RoundToInt(utteranceSampleRate * 2f * MinUtteranceSeconds);
        if (utteranceSeconds < MinUtteranceSeconds ||
            utterancePcm.Count < minByteCount ||
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

        if (ServerSpeechTranscriptionEnabled)
        {
            VoiceUtteranceCapturedForServer?.Invoke(pcm16, sampleRate, peakLevel);
            return;
        }

        RefreshOpenAiConfiguration();
        if (!HasSpeechRecognition || transcriptionRequestActive)
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

        RefreshOpenAiConfiguration();
        if (openAiConfigured)
        {
            yield return TranscribeWithOpenAi(pcm16, sampleRate, peakLevel);
            transcriptionRequestActive = false;
            yield break;
        }

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
                                RaiseNoTranscript(pcm16.Length, peakLevel, audioPeak, appliedGain);
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
                    RaiseNoTranscript(pcm16.Length, peakLevel, audioPeak, appliedGain);
                    transcriptionRequestActive = false;
                    yield break;
                }
            }
        }

        transcriptionRequestActive = false;
        RaiseFullTranscription(transcript);
    }

    private IEnumerator TranscribeWithOpenAi(byte[] pcm16, int sampleRate, float peakLevel)
    {
        byte[] normalizedPcm = NormalizePcm16ForStt(pcm16, out float appliedGain, out float audioPeak);
        byte[] wavBytes = BuildWavBytes(normalizedPcm, sampleRate);

        WWWForm form = new WWWForm();
        form.AddField("model", OpenAiTranscriptionModel);
        form.AddField("response_format", "json");
        form.AddField("language", "en");
        form.AddField("prompt", "Mentora game voice. Important words: Rudolf, Python Island, C++ Island, Logic Island, Community Island, code, run, execute, debug.");
        form.AddBinaryData("file", wavBytes, "rudolf_voice.wav", "audio/wav");

        using (UnityWebRequest request = UnityWebRequest.Post(OpenAiTranscriptionUrl, form))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            string response = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[RobotVoice] OpenAI STT failed: " + request.responseCode + " " + request.error + " " + response);
                RaiseNoTranscript(pcm16.Length, peakLevel, audioPeak, appliedGain);
                yield break;
            }

            string transcript = ExtractJsonStringValue(response, "text");
            if (string.IsNullOrWhiteSpace(transcript))
            {
                Debug.LogWarning("[RobotVoice] OpenAI STT had no transcript. bytes=" + pcm16.Length +
                    " vadPeak=" + peakLevel.ToString("0.000") +
                    " audioPeak=" + audioPeak.ToString("0.000") +
                    " gain=" + appliedGain.ToString("0.00") +
                    " response=" + response);
                RaiseNoTranscript(pcm16.Length, peakLevel, audioPeak, appliedGain);
                yield break;
            }

            RaiseFullTranscription(transcript);
        }
    }

    private IEnumerator SpeakOpenAi(string text, string emotion)
    {
        if (openAiTtsRequestActive || string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        openAiTtsRequestActive = true;
        string body =
            "{" +
            "\"model\":\"" + OpenAiTtsModel + "\"," +
            "\"voice\":\"" + OpenAiTtsVoice + "\"," +
            "\"response_format\":\"wav\"," +
            "\"instructions\":\"" + EscapeJson(BuildTtsInstructions(emotion)) + "\"," +
            "\"input\":\"" + EscapeJson(text) + "\"" +
            "}";

        using (UnityWebRequest request = new UnityWebRequest(OpenAiSpeechUrl, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes) { contentType = "application/json" };
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/wav");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
                Debug.LogWarning("[RobotVoice] OpenAI TTS failed: " + request.responseCode + " " + request.error + " " + response);
                openAiTtsRequestActive = false;
                yield break;
            }

            byte[] audioBytes = request.downloadHandler == null ? null : request.downloadHandler.data;
            if (!TryCreateAudioClipFromWav(audioBytes, "RudolfOpenAITTS", out AudioClip clip))
            {
                Debug.LogWarning("[RobotVoice] OpenAI TTS returned audio that Unity could not decode as WAV.");
                openAiTtsRequestActive = false;
                yield break;
            }

            if (ttsAudioSource != null)
            {
                ttsAudioSource.Stop();
                ttsAudioSource.clip = clip;
                ttsAudioSource.Play();
            }
        }

        openAiTtsRequestActive = false;
    }

    private void ApplyFallbackVoiceEmotion(string emotion)
    {
        if (ttsAudioSource == null)
        {
            return;
        }

        string normalized = NormalizeEmotion(emotion);
        ttsAudioSource.pitch = normalized switch
        {
            "excited" => 1.08f,
            "happy" => 1.04f,
            "concerned" => 0.94f,
            "thinking" => 0.97f,
            _ => 1.0f
        };
        ttsAudioSource.volume = normalized == "concerned" ? 0.92f : 1f;
    }

    private static string BuildTtsInstructions(string emotion)
    {
        string normalized = NormalizeEmotion(emotion);
        string baseLine = "Sound like Rudolf, a friendly, clear robot mentor in a coding game. Keep it natural, concise, and never read labels like emotion or action aloud. ";
        return normalized switch
        {
            "excited" => baseLine + "Use a brighter, more energetic tone with a slightly faster pace, but do not shout.",
            "happy" => baseLine + "Use a warm, upbeat tone with a small smile in the voice.",
            "concerned" => baseLine + "Use a softer, calmer tone that reassures the student.",
            "thinking" => baseLine + "Use a thoughtful, curious tone with measured pacing.",
            _ => baseLine + "Use an encouraging mentor tone with medium energy."
        };
    }

    private static string NormalizeEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion))
        {
            return "encouraging";
        }

        string normalized = emotion.Trim().ToLowerInvariant();
        return normalized == "happy" ||
               normalized == "encouraging" ||
               normalized == "concerned" ||
               normalized == "excited" ||
               normalized == "thinking"
            ? normalized
            : "encouraging";
    }

    private void RaiseNoTranscript(int byteCount, float vadPeak, float audioPeak, float appliedGain)
    {
        TranscriptionFailedWithoutText?.Invoke(byteCount, vadPeak, audioPeak, appliedGain);
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
            string contentType = "audio/raw;encoding=signed-integer;bits=16;rate=" + sampleRate + ";endian=little";
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

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 16);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 32)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
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

    private static bool TryCreateAudioClipFromWav(byte[] wavBytes, string clipName, out AudioClip clip)
    {
        clip = null;
        if (wavBytes == null || wavBytes.Length < 44)
        {
            return false;
        }

        if (ReadAscii(wavBytes, 0, 4) != "RIFF" || ReadAscii(wavBytes, 8, 4) != "WAVE")
        {
            return false;
        }

        int offset = 12;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataSize = 0;

        while (offset + 8 <= wavBytes.Length)
        {
            string chunkId = ReadAscii(wavBytes, offset, 4);
            int chunkSize = ReadInt32LittleEndian(wavBytes, offset + 4);
            int chunkData = offset + 8;
            if (chunkSize < 0 || chunkData + chunkSize > wavBytes.Length)
            {
                break;
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                short format = ReadInt16LittleEndian(wavBytes, chunkData);
                channels = ReadInt16LittleEndian(wavBytes, chunkData + 2);
                sampleRate = ReadInt32LittleEndian(wavBytes, chunkData + 4);
                bitsPerSample = ReadInt16LittleEndian(wavBytes, chunkData + 14);
                if (format != 1 && format != 3)
                {
                    return false;
                }
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkData;
                dataSize = chunkSize;
            }

            offset = chunkData + chunkSize + (chunkSize % 2);
        }

        if (channels <= 0 || sampleRate <= 0 || dataOffset < 0 || dataSize <= 0)
        {
            return false;
        }

        int bytesPerSample = bitsPerSample / 8;
        if (bytesPerSample <= 0)
        {
            return false;
        }

        int totalSamples = dataSize / bytesPerSample;
        int frameCount = totalSamples / channels;
        if (frameCount <= 0)
        {
            return false;
        }

        float[] samples = new float[frameCount * channels];
        int readOffset = dataOffset;
        for (int i = 0; i < samples.Length && readOffset + bytesPerSample <= wavBytes.Length; i++)
        {
            if (bitsPerSample == 16)
            {
                short value = ReadInt16LittleEndian(wavBytes, readOffset);
                samples[i] = Mathf.Clamp(value / 32768f, -1f, 1f);
            }
            else if (bitsPerSample == 24)
            {
                int value = wavBytes[readOffset] | (wavBytes[readOffset + 1] << 8) | (wavBytes[readOffset + 2] << 16);
                if ((value & 0x800000) != 0)
                {
                    value |= unchecked((int)0xff000000);
                }
                samples[i] = Mathf.Clamp(value / 8388608f, -1f, 1f);
            }
            else if (bitsPerSample == 32)
            {
                samples[i] = BitConverter.ToSingle(wavBytes, readOffset);
            }
            else
            {
                return false;
            }

            readOffset += bytesPerSample;
        }

        clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return true;
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

    private static string ReadAscii(byte[] source, int offset, int length)
    {
        if (source == null || offset < 0 || offset + length > source.Length)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(source, offset, length);
    }

    private static short ReadInt16LittleEndian(byte[] source, int offset)
    {
        return (short)(source[offset] | (source[offset + 1] << 8));
    }

    private static int ReadInt32LittleEndian(byte[] source, int offset)
    {
        return source[offset] |
               (source[offset + 1] << 8) |
               (source[offset + 2] << 16) |
               (source[offset + 3] << 24);
    }

    private void WarnMissingConfig()
    {
        if (warnedMissingConfig)
        {
            return;
        }

        warnedMissingConfig = true;
        UnityEngine.Debug.LogWarning("[RobotVoice] No Rudolf client-side speech provider is configured. Server Groq transcription is enabled, but spoken TTS replies need either an OpenAI key or the Wit TTS fallback.");
    }

    [Serializable]
    private sealed class WitSpeechResponse
    {
        public string text;
        public string _text;
    }
}
