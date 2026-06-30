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

    public event Action<string> FullTranscriptionReceived;
    public float MicLevel { get; private set; }
    public bool HasSpeechRecognition => witConfigured;
    public bool IsListening => listeningRequested && microphoneManager != null && microphoneManager.IsMicrophoneCapturing;
    public bool IsSpeaking => speaker != null && speaker.IsActive;

    private TTSSpeaker speaker;
    private TTSWit ttsService;
    private MultiplayerSessionManager microphoneManager;
    private string witClientAccessToken;
    private string speechEndpointUrl;
    private bool witConfigured;
    private bool warnedMissingConfig;
    private bool listeningRequested;
    private bool microphoneAcquired;
    private bool utteranceActive;
    private bool transcriptionRequestActive;
    private float utteranceStartTime;
    private float lastSpeechTime;
    private int utteranceSampleRate = 16000;
    private readonly List<byte> utterancePcm = new List<byte>(16000 * 2 * 8);

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

        speechEndpointUrl = BuildSpeechEndpointUrl(config);

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

        AudioSource speechSource = ttsObject.AddComponent<AudioSource>();
        speechSource.playOnAwake = false;
        speechSource.spatialBlend = 0.35f;

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
        if (!listeningRequested || pcm16 == null || pcm16.Length == 0 || IsSpeaking || transcriptionRequestActive)
        {
            return;
        }

        float now = Time.unscaledTime;
        bool hasSpeech = level >= (utteranceActive ? SpeechContinueLevel : SpeechStartLevel);
        if (hasSpeech)
        {
            if (!utteranceActive)
            {
                utteranceActive = true;
                utteranceStartTime = now;
                utterancePcm.Clear();
                utteranceSampleRate = sampleRate;
            }

            lastSpeechTime = now;
        }

        if (!utteranceActive)
        {
            return;
        }

        utterancePcm.AddRange(pcm16);
        if ((now - lastSpeechTime >= EndSilenceSeconds) || (now - utteranceStartTime >= MaxUtteranceSeconds))
        {
            SubmitUtterance();
        }
    }

    private void SubmitUtterance()
    {
        float utteranceSeconds = Time.unscaledTime - utteranceStartTime;
        if (utteranceSeconds < MinUtteranceSeconds || utterancePcm.Count < utteranceSampleRate)
        {
            ResetUtterance();
            return;
        }

        byte[] wavBytes = BuildWavBytes(utterancePcm, utteranceSampleRate);
        ResetUtterance();

        if (!witConfigured || transcriptionRequestActive)
        {
            return;
        }

        StartCoroutine(TranscribeWav(wavBytes));
    }

    private void ResetUtterance()
    {
        utteranceActive = false;
        utteranceStartTime = 0f;
        lastSpeechTime = 0f;
        utterancePcm.Clear();
    }

    private IEnumerator TranscribeWav(byte[] wavBytes)
    {
        transcriptionRequestActive = true;

        using (UnityWebRequest request = new UnityWebRequest(speechEndpointUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(wavBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + witClientAccessToken);
            request.SetRequestHeader("Content-Type", "audio/wav");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            transcriptionRequestActive = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning("[RobotVoice] Wit speech request failed: " + request.error + " " + request.downloadHandler.text);
                yield break;
            }

            string transcript = ExtractTranscript(request.downloadHandler.text);
            RaiseFullTranscription(transcript);
        }
    }

    private static string BuildSpeechEndpointUrl(WitConfiguration config)
    {
        IWitRequestEndpointInfo endpoint = config.GetEndpointInfo();
        string url = endpoint.UriScheme + "://" + endpoint.Authority;
        if (endpoint.Port > 0 && endpoint.Port != 443)
        {
            url += ":" + endpoint.Port;
        }

        return url + "/" + endpoint.Speech + "?v=" + UnityWebRequest.EscapeURL(endpoint.WitApiVersion);
    }

    private static string ExtractTranscript(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        WitSpeechResponse response = JsonUtility.FromJson<WitSpeechResponse>(json);
        if (response == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(response.text) ? response.text : response._text;
    }

    private static byte[] BuildWavBytes(List<byte> pcm16, int sampleRate)
    {
        int dataLength = pcm16.Count;
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
        pcm16.CopyTo(wav, 44);
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
