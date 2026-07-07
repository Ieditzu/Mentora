using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum MenSfx
{
    Jump = 0,
    Land = 1,
    Respawn = 2,
    ButtonClick = 3,
    AnswerCorrect = 4,
    AnswerWrong = 5,
    ChallengeComplete = 6,
    PortalEnter = 7,
    PipeDescend = 8,
    PipeAscend = 9,
    ButtonHover = 10,
    Footstep = 11,
    QuizStart = 12,
    QuizCountdownTick = 13,
    QuizQuestionReveal = 14,
    QuizAnswerLock = 15,
    QuizResultsReveal = 16,
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInstance();
            }

            return instance;
        }
    }

    private static AudioManager instance;

    [Header("Player")]
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landClip;
    [SerializeField] private AudioClip respawnClip;

    [Header("UI")]
    [SerializeField] private AudioClip buttonClickClip;

    [Header("Quiz Feedback")]
    [SerializeField] private AudioClip answerCorrectClip;
    [SerializeField] private AudioClip answerWrongClip;
    [SerializeField] private AudioClip challengeCompleteClip;

    [Header("Portals & Transitions")]
    [SerializeField] private AudioClip portalEnterClip;
    [SerializeField] private AudioClip pipeDescendClip;
    [SerializeField] private AudioClip pipeAscendClip;

    [Header("Mix")]
    [SerializeField] private float masterSfxVolume = 0.9f;
    [SerializeField] private float ambientVolume = 0.18f;
    [SerializeField] private bool enableAmbientLoop = true;

    private AudioSource source;
    private AudioSource ambientSource;
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();
    private readonly Dictionary<MenSfx, AudioClip> generatedClips = new Dictionary<MenSfx, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static AudioManager EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<AudioManager>();
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new GameObject("AudioManager");
        instance = root.AddComponent<AudioManager>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = false;

        ambientSource = transform.Find("AmbientSource")?.GetComponent<AudioSource>();
        if (ambientSource == null)
        {
            GameObject ambientObject = new GameObject("AmbientSource");
            ambientObject.transform.SetParent(transform, false);
            ambientSource = ambientObject.AddComponent<AudioSource>();
        }

        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume;
    }

    private void Start()
    {
        EnsureAmbientLoop();
        StartCoroutine(AutoRegisterButtons());
    }

    private IEnumerator AutoRegisterButtons()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            foreach (var btn in FindObjectsOfType<Button>(true))
            {
                if (!ShouldRegisterDefaultClick(btn))
                {
                    continue;
                }

                if (registeredButtons.Add(btn))
                {
                    btn.onClick.AddListener(() => Play(MenSfx.ButtonClick));
                }
            }
            yield return wait;
        }
    }

    private static bool ShouldRegisterDefaultClick(Button button)
    {
        return button != null && button.GetComponent<MobileJumpButton>() == null;
    }

    public static void Play(MenSfx sfx)
    {
        if (Instance == null || Instance.source == null)
        {
            return;
        }

        Instance.PlayInternal(sfx, 1f);
    }

    public static void PlayHover()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.PlayInternal(MenSfx.ButtonHover, 0.7f);
    }

    public static void PlayFootstep(float volumeScale = 1f)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.PlayInternal(MenSfx.Footstep, Mathf.Clamp(volumeScale, 0.1f, 1.25f));
    }

    private void PlayInternal(MenSfx sfx, float volumeScale)
    {
        if (source == null)
        {
            return;
        }

        AudioClip clip = GetClip(sfx);
        if (clip == null)
        {
            return;
        }

        float pitch = 1f;
        switch (sfx)
        {
            case MenSfx.Footstep:
                pitch = Random.Range(0.9f, 1.08f);
                break;
            case MenSfx.ButtonHover:
                pitch = Random.Range(1.08f, 1.18f);
                break;
            case MenSfx.ButtonClick:
                pitch = Random.Range(0.98f, 1.04f);
                break;
        }

        source.pitch = pitch;
        source.PlayOneShot(clip, volumeScale * masterSfxVolume);
        source.pitch = 1f;
    }

    private void EnsureAmbientLoop()
    {
        if (!enableAmbientLoop || ambientSource == null || ambientSource.isPlaying)
        {
            return;
        }

        ambientSource.clip = CreateAmbientClip();
        if (ambientSource.clip != null)
        {
            ambientSource.volume = ambientVolume;
            ambientSource.Play();
        }
    }

    private AudioClip GetClip(MenSfx sfx) => sfx switch
    {
        MenSfx.Jump              => jumpClip != null ? jumpClip : GetGeneratedClip(MenSfx.Jump),
        MenSfx.Land              => landClip != null ? landClip : GetGeneratedClip(MenSfx.Land),
        MenSfx.Respawn           => respawnClip != null ? respawnClip : GetGeneratedClip(MenSfx.Respawn),
        MenSfx.ButtonClick       => buttonClickClip != null ? buttonClickClip : GetGeneratedClip(MenSfx.ButtonClick),
        MenSfx.AnswerCorrect     => answerCorrectClip != null ? answerCorrectClip : GetGeneratedClip(MenSfx.AnswerCorrect),
        MenSfx.AnswerWrong       => answerWrongClip != null ? answerWrongClip : GetGeneratedClip(MenSfx.AnswerWrong),
        MenSfx.ChallengeComplete => challengeCompleteClip != null ? challengeCompleteClip : GetGeneratedClip(MenSfx.ChallengeComplete),
        MenSfx.PortalEnter       => portalEnterClip != null ? portalEnterClip : GetGeneratedClip(MenSfx.PortalEnter),
        MenSfx.PipeDescend       => pipeDescendClip != null ? pipeDescendClip : GetGeneratedClip(MenSfx.PipeDescend),
        MenSfx.PipeAscend        => pipeAscendClip != null ? pipeAscendClip : GetGeneratedClip(MenSfx.PipeAscend),
        MenSfx.ButtonHover       => GetGeneratedClip(MenSfx.ButtonHover),
        MenSfx.Footstep          => GetGeneratedClip(MenSfx.Footstep),
        MenSfx.QuizStart         => GetGeneratedClip(MenSfx.QuizStart),
        MenSfx.QuizCountdownTick => GetGeneratedClip(MenSfx.QuizCountdownTick),
        MenSfx.QuizQuestionReveal => GetGeneratedClip(MenSfx.QuizQuestionReveal),
        MenSfx.QuizAnswerLock    => GetGeneratedClip(MenSfx.QuizAnswerLock),
        MenSfx.QuizResultsReveal => GetGeneratedClip(MenSfx.QuizResultsReveal),
        _                        => GetGeneratedClip(sfx),
    };

    private AudioClip GetGeneratedClip(MenSfx sfx)
    {
        if (generatedClips.TryGetValue(sfx, out AudioClip cached))
        {
            return cached;
        }

        AudioClip clip = sfx switch
        {
            MenSfx.Jump => CreateToneClip("JumpFallback", 520f, 0.11f, 0.26f, 0.18f, true),
            MenSfx.Land => CreateNoiseClip("LandFallback", 0.12f, 0.26f, 0.08f),
            MenSfx.Respawn => CreateToneClip("RespawnFallback", 310f, 0.22f, 0.24f, 0.1f, false),
            MenSfx.ButtonClick => CreateToneClip("ButtonClickFallback", 880f, 0.05f, 0.18f, 0.32f, false),
            MenSfx.ButtonHover => CreateToneClip("ButtonHoverFallback", 1180f, 0.035f, 0.12f, 0.35f, false),
            MenSfx.AnswerCorrect => CreateToneClip("CorrectFallback", 760f, 0.18f, 0.22f, 0.26f, true),
            MenSfx.AnswerWrong => CreateToneClip("WrongFallback", 180f, 0.24f, 0.24f, 0.22f, false),
            MenSfx.ChallengeComplete => CreateToneClip("CompleteFallback", 640f, 0.36f, 0.24f, 0.22f, true),
            MenSfx.PortalEnter => CreateToneClip("PortalFallback", 260f, 0.42f, 0.22f, 0.15f, true),
            MenSfx.PipeDescend => CreateToneClip("PipeDescendFallback", 220f, 0.28f, 0.2f, 0.14f, false),
            MenSfx.PipeAscend => CreateToneClip("PipeAscendFallback", 420f, 0.28f, 0.2f, 0.14f, true),
            MenSfx.Footstep => CreateNoiseClip("FootstepFallback", 0.05f, 0.09f, 0.06f),
            MenSfx.QuizStart => CreateChordClip("QuizStartFallback", new[] { 392f, 523.25f, 659.25f }, 0.55f, 0.16f),
            MenSfx.QuizCountdownTick => CreateToneClip("QuizCountdownTickFallback", 980f, 0.08f, 0.16f, 0.08f, false),
            MenSfx.QuizQuestionReveal => CreateChordClip("QuizRevealFallback", new[] { 523.25f, 659.25f, 783.99f }, 0.24f, 0.18f),
            MenSfx.QuizAnswerLock => CreateToneClip("QuizAnswerLockFallback", 700f, 0.1f, 0.16f, 0.05f, true),
            MenSfx.QuizResultsReveal => CreateChordClip("QuizResultsRevealFallback", new[] { 329.63f, 493.88f, 659.25f }, 0.34f, 0.18f),
            _ => null,
        };

        if (clip != null)
        {
            generatedClips[sfx] = clip;
        }

        return clip;
    }

    private AudioClip CreateAmbientClip()
    {
        const int sampleRate = 22050;
        const float duration = 8f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float phaseA = 0f;
        float phaseB = 0f;
        float phaseC = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            phaseA += 2f * Mathf.PI * 72f / sampleRate;
            phaseB += 2f * Mathf.PI * 109f / sampleRate;
            phaseC += 2f * Mathf.PI * 146f / sampleRate;

            float drone = Mathf.Sin(phaseA) * 0.38f + Mathf.Sin(phaseB) * 0.22f + Mathf.Sin(phaseC) * 0.14f;
            float shimmer = Mathf.Sin(t * 0.48f) * 0.12f + Mathf.Sin(t * 0.21f) * 0.08f;
            samples[i] = Mathf.Clamp((drone * (0.45f + shimmer)) * 0.24f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("AmbientFallback", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateToneClip(string clipName, float frequency, float duration, float amplitude, float vibratoDepth, bool rising)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / Mathf.Max(1f, sampleCount - 1f);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float vibrato = 1f + Mathf.Sin(progress * Mathf.PI * 8f) * vibratoDepth;
            float sweep = rising ? Mathf.Lerp(0.86f, 1.14f, progress) : Mathf.Lerp(1.14f, 0.82f, progress);
            phase += 2f * Mathf.PI * frequency * vibrato * sweep / sampleRate;
            samples[i] = Mathf.Sin(phase) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNoiseClip(string clipName, float duration, float amplitude, float smoothing)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float current = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / Mathf.Max(1f, sampleCount - 1f);
            float target = Random.Range(-1f, 1f);
            current = Mathf.Lerp(current, target, smoothing);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            samples[i] = current * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateChordClip(string clipName, float[] frequencies, float duration, float amplitude)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float[] phases = new float[frequencies.Length];

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / Mathf.Max(1f, sampleCount - 1f);
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float sample = 0f;

            for (int j = 0; j < frequencies.Length; j++)
            {
                phases[j] += 2f * Mathf.PI * frequencies[j] / sampleRate;
                sample += Mathf.Sin(phases[j]);
            }

            sample /= Mathf.Max(1, frequencies.Length);
            samples[i] = sample * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
