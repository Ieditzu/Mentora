using System;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Dictation;
using Meta.WitAi.Requests;
using Meta.WitAi.TTS.Integrations;
using Meta.WitAi.TTS.Utilities;
using UnityEngine;

public sealed class RobotVoiceBridge : MonoBehaviour
{
    private const string WitConfigurationResourcePath = "Voice/RobotWitConfiguration";

    public event Action<string> FullTranscriptionReceived;
    public float MicLevel { get; private set; }
    public bool HasSpeechRecognition => dictation != null && witConfigured;
    public bool IsListening => dictation != null && dictation.Active;
    public bool IsSpeaking => speaker != null && speaker.IsActive;

    private WitDictation dictation;
    private TTSSpeaker speaker;
    private TTSWit ttsService;
    private bool witConfigured;
    private bool warnedMissingConfig;

    public void Initialize()
    {
        if (dictation != null)
        {
            return;
        }

        WitConfiguration config = Resources.Load<WitConfiguration>(WitConfigurationResourcePath);
        witConfigured = config != null && !string.IsNullOrWhiteSpace(config.GetClientAccessToken());
        if (!witConfigured)
        {
            WarnMissingConfig();
            return;
        }

        dictation = gameObject.AddComponent<WitDictation>();
        dictation.RuntimeConfiguration = new WitRuntimeConfiguration
        {
            witConfiguration = config,
            maxRecordingTime = 6f,
            minKeepAliveVolume = 0.02f,
            minKeepAliveTimeInSeconds = 0.75f,
            minTranscriptionKeepAliveTimeInSeconds = 0.5f,
            soundWakeThreshold = 0.02f,
            sampleLengthInMs = 20,
            micBufferLengthInSeconds = 1f,
            sendAudioToWit = true,
            alwaysRecord = false,
            preferredActivationOffset = -0.25f
        };
        dictation.DictationEvents.OnFullTranscription.AddListener(OnFullTranscription);
        dictation.DictationEvents.OnMicLevelChanged.AddListener(OnMicLevelChanged);
        dictation.DictationEvents.OnError.AddListener(OnDictationError);

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
        if (!witConfigured || dictation == null)
        {
            if (shouldListen)
            {
                WarnMissingConfig();
            }
            return;
        }

        if (shouldListen)
        {
            if (!dictation.Active && !dictation.IsRequestActive && !IsSpeaking)
            {
                dictation.Activate();
            }
        }
        else if (dictation.Active)
        {
            dictation.Deactivate();
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
        if (dictation == null)
        {
            return;
        }

        dictation.DictationEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
        dictation.DictationEvents.OnMicLevelChanged.RemoveListener(OnMicLevelChanged);
        dictation.DictationEvents.OnError.RemoveListener(OnDictationError);
    }

    private void OnFullTranscription(string text)
    {
        string trimmed = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        FullTranscriptionReceived?.Invoke(trimmed);
    }

    private void OnMicLevelChanged(float level)
    {
        MicLevel = Mathf.Clamp01(level);
    }

    private void OnDictationError(string error, string message)
    {
        UnityEngine.Debug.LogWarning("[RobotVoice] Dictation error: " + error + " " + message);
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
}
