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
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

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

    private AudioSource source;
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.spatialBlend = 0f;
        source.playOnAwake = false;
    }

    private void Start()
    {
        StartCoroutine(AutoRegisterButtons());
    }

    private IEnumerator AutoRegisterButtons()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            foreach (var btn in FindObjectsOfType<Button>(true))
            {
                if (registeredButtons.Add(btn))
                {
                    btn.onClick.AddListener(() => Play(MenSfx.ButtonClick));
                }
            }
            yield return wait;
        }
    }

    public static void Play(MenSfx sfx)
    {
        if (Instance == null || Instance.source == null)
        {
            return;
        }
        AudioClip clip = Instance.GetClip(sfx);
        if (clip != null)
        {
            Instance.source.PlayOneShot(clip);
        }
    }

    private AudioClip GetClip(MenSfx sfx) => sfx switch
    {
        MenSfx.Jump              => jumpClip,
        MenSfx.Land              => landClip,
        MenSfx.Respawn           => respawnClip,
        MenSfx.ButtonClick       => buttonClickClip,
        MenSfx.AnswerCorrect     => answerCorrectClip,
        MenSfx.AnswerWrong       => answerWrongClip,
        MenSfx.ChallengeComplete => challengeCompleteClip,
        MenSfx.PortalEnter       => portalEnterClip,
        MenSfx.PipeDescend       => pipeDescendClip,
        MenSfx.PipeAscend        => pipeAscendClip,
        _                        => null,
    };
}
