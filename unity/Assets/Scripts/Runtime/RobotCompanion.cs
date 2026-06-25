using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mentora.Network;

/// <summary>
/// ARIA — always-present robot companion.
/// Spawns itself from Resources/Robot/low.obj at game start.
/// Follows the player, reacts to coding pad approaches and challenge results.
///
/// Other scripts call:  RobotCompanion.Trigger("challenge_fail");
/// Pads call:           RobotCompanion.TriggerWithContext("python_pad", "Practice 1 — fix multiply_by_two");
/// </summary>
public class RobotCompanion : MonoBehaviour
{
    // ── Static trigger API ───────────────────────────────────────────────────

    public static event Action<string, string> OnTrigger; // (trigger, extraContext)

    /// <summary>Fire a named trigger, e.g. "challenge_fail", "challenge_success".</summary>
    public static void Trigger(string trigger) => OnTrigger?.Invoke(trigger, "");

    /// <summary>Fire a trigger with extra context shown to the AI, e.g. the pad description.</summary>
    public static void TriggerWithContext(string trigger, string context) =>
        OnTrigger?.Invoke(trigger, context);

    // ── Auto-spawn ───────────────────────────────────────────────────────────

    private static RobotCompanion _instance;
    public static RobotCompanion Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
            return;

        // Load the wrapper prefab (correctly centred, avoids raw .obj import offset issues)
        var model = Resources.Load<GameObject>("Robot/ARIA");
        if (model == null)
        {
            Debug.LogWarning("[RobotCompanion] Could not load Resources/Robot/ARIA — companion won't spawn.");
            return;
        }

        // Spawn at the same position FpsBootstrap uses so ARIA starts next to the player
        Vector3 spawnPos = new Vector3(75f, 3f, 222f); // matches FpsBootstrap default + hover height
        var existingFps = UnityEngine.Object.FindObjectOfType<FirstPersonControllerSimple>();
        if (existingFps != null)
            spawnPos = existingFps.transform.position + Vector3.up * 3f;

        var go = UnityEngine.Object.Instantiate(model, spawnPos, Quaternion.identity);
        go.name = "ARIA_Companion";
        go.transform.localScale = Vector3.one * 0.2f;

        // Strip the CoinRotator's RobotLookAt if it somehow got onto the model
        var oldLookAt = go.GetComponent<RobotLookAt>();
        if (oldLookAt != null) Destroy(oldLookAt);

        go.AddComponent<RobotCompanion>();
    }

    // ── Config (tweakable in Inspector if you add this manually) ─────────────

    [Header("Follow")]
    [SerializeField] private float followDistance  = 1.8f;
    [SerializeField] private float followSpeed     = 5f;
    [SerializeField] private float rotSpeed        = 6f;

    [Header("Float")]
    [SerializeField] private float hoverHeight     = 3.8f;   // match FPS eyeHeight (4.0) minus a bit so ARIA is at eye level
    [SerializeField] private float bobAmplitude    = 0.18f;  // up/down bob range
    [SerializeField] private float bobSpeed        = 1.8f;   // bob cycles per second
    [SerializeField] private float tiltAngle       = 12f;    // gentle tilt while moving

    [Header("Speech")]
    [SerializeField] private float bubbleTime      = 5f;
    [SerializeField] private float fadeDuration    = 0.5f;
    [SerializeField] private float minCooldown     = 8f;

    // ── Internal ─────────────────────────────────────────────────────────────

    private Transform  player;
    private bool       snappedToPlayer;  // first-frame teleport so ARIA starts next to player
    private GameObject bubble;
    private Text       bubbleText;
    private CanvasGroup bubbleCg;
    private Coroutine  hideCoroutine;
    private bool       waiting;
    private float      lastSpoke;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BuildBubble();
        ResolvePlayer();

        if (GameClient.Instance != null)
            GameClient.Instance.OnPacketReceived += OnPacket;

        OnTrigger += HandleTrigger;

        // Greet after 3 seconds
        StartCoroutine(GreetAfterDelay(3f));
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
            GameClient.Instance.OnPacketReceived -= OnPacket;
        OnTrigger -= HandleTrigger;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (player == null) { ResolvePlayer(); return; }

        // ── Snap to player on first frame so ARIA never rubber-bands from afar ─
        if (!snappedToPlayer)
        {
            snappedToPlayer = true;
            Vector3 sideForwardSnap = (player.forward + player.right).normalized;
            transform.position = player.position + Vector3.up * hoverHeight + sideForwardSnap * followDistance;
        }

        // ── Float target position ─────────────────────────────────────────────
        // Stay forward-right so ARIA is always in the player's field of view.
        // 45° between forward and right = always visible without blocking the view.
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Vector3 sideForward = (player.forward + player.right).normalized; // 45° forward-right
        Vector3 targetPos = player.position
            + Vector3.up * (hoverHeight + bob)
            + sideForward * followDistance;

        // ── Smooth follow ────────────────────────────────────────────────────
        float dist = Vector3.Distance(transform.position, targetPos);
        if (dist > 0.05f)
        {
            transform.position = Vector3.Lerp(
                transform.position, targetPos, followSpeed * Time.deltaTime);
        }

        // ── Face the player's head — use the FPS camera, not Camera.main ────────
        // Camera.main in this project is an overview cam far from the player.
        // The real player camera is a child of the FPS controller.
        Vector3 headPos = player.position + Vector3.up * 4f; // FPS eyeHeight default
        var fpsCam = PlayerCache.GetFps()?.GetComponentInChildren<Camera>();
        if (fpsCam != null)
            headPos = fpsCam.transform.position;

        Vector3 lookDir = headPos - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized);
            desiredRot *= Quaternion.Euler(0f, 0f, -tiltAngle * Mathf.Clamp01(dist - followDistance));
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotSpeed * Time.deltaTime);
        }

        // ── Bubble always faces camera ───────────────────────────────────────
        if (bubble != null && bubble.activeSelf && Camera.main != null)
            bubble.transform.rotation = Quaternion.LookRotation(
                bubble.transform.position - Camera.main.transform.position);
    }

    // ── Speech bubble builder ────────────────────────────────────────────────

    private void BuildBubble()
    {
        bubble = new GameObject("ARIA_Bubble");
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 2.6f, 0f);

        var canvas = bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = bubble.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340f, 90f);
        rt.localScale = Vector3.one * 0.009f;

        bubbleCg = bubble.AddComponent<CanvasGroup>();

        // Dark rounded-ish background
        var bg = new GameObject("BG");
        bg.transform.SetParent(bubble.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.04f, 0.14f, 0.92f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Name label
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(bubble.transform, false);
        var nameTxt = nameGo.AddComponent<Text>();
        nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameTxt.text = "ARIA";
        nameTxt.fontSize = 14;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.color = new Color(0.7f, 0.5f, 1f);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0.65f); nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = new Vector2(10, 0); nameRt.offsetMax = new Vector2(-10, -4);

        // Speech text
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(bubble.transform, false);
        bubbleText = textGo.AddComponent<Text>();
        bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleText.fontSize = 13;
        bubbleText.color = Color.white;
        bubbleText.alignment = TextAnchor.UpperLeft;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = new Vector2(1, 0.65f);
        textRt.offsetMin = new Vector2(10, 6); textRt.offsetMax = new Vector2(-10, 0);

        bubble.SetActive(false);
    }

    // ── Trigger handling ─────────────────────────────────────────────────────

    private void HandleTrigger(string trigger, string context)
    {
        if (Time.time - lastSpoke < minCooldown) return;
        if (waiting) return;

        lastSpoke = Time.time;
        waiting = true;

        StartCoroutine(RequestLine(trigger, context));
    }

    private IEnumerator RequestLine(string trigger, string context)
    {
        // Wait a frame so the trigger caller finishes its own logic first
        yield return null;

        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            waiting = false;
            ShowLine(FallbackLine(trigger, context));
            yield break;
        }

        string fullTrigger = string.IsNullOrEmpty(context) ? trigger : trigger + "|" + context;
        yield return GameClient.Instance.SendPacket(new CompanionSpeakPacket(fullTrigger));
        // Response handled in OnPacket
    }

    private IEnumerator GreetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HandleTrigger("greet", "");
    }

    // ── Packet handling ──────────────────────────────────────────────────────

    private void OnPacket(Packet p)
    {
        if (p is CompanionSpeakResponsePacket r)
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                waiting = false;
                ShowLine(r.Line);
            });
    }

    // ── Show speech bubble ───────────────────────────────────────────────────

    private void ShowLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (bubbleText != null) bubbleText.text = line;
        if (bubble != null)
        {
            bubble.SetActive(true);
            if (bubbleCg != null) bubbleCg.alpha = 1f;
        }

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideBubble());
    }

    private IEnumerator HideBubble()
    {
        yield return new WaitForSeconds(bubbleTime);
        if (bubbleCg != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                if (bubbleCg != null) bubbleCg.alpha = 1f - t / fadeDuration;
                yield return null;
            }
        }
        if (bubble != null) bubble.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ResolvePlayer()
    {
        player = PlayerCache.ResolvePlayerTransform();
    }

    private static string FallbackLine(string trigger, string context)
    {
        return trigger switch
        {
            "challenge_success" => "Let's go! You got it!",
            "task_complete"     => "One more task down. You're on a roll.",
            "challenge_fail"    => "Don't sweat it — read the error carefully.",
            "greet"             => "Hey! I'll be right here with you.",
            "entering_python"   => "Python pad ahead — I've seen you struggle with loops, heads up.",
            "entering_cpp"      => "C++ zone. Watch your semicolons.",
            "hint_requested"    => "Good call asking. That's how you learn faster.",
            _ when context.Length > 0 => $"About to tackle: {context.Split('|')[0]}. You've got this.",
            _                   => "I'm right here if you need me.",
        };
    }
}
