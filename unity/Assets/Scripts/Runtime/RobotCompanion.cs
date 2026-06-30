using System;
using System.Collections;
using System.Text;
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
        go.name = "Rudolf";
        go.transform.localScale = Vector3.one * 0.2f;

        // Strip the CoinRotator's RobotLookAt if it somehow got onto the model
        var oldLookAt = go.GetComponent<RobotLookAt>();
        if (oldLookAt != null) Destroy(oldLookAt);

        go.AddComponent<RobotCompanion>();
    }

    // ── Config (tweakable in Inspector if you add this manually) ─────────────

    [Header("Follow")]
    [SerializeField] private float followDistance  = 1.8f;
    [SerializeField] private float followSpeed     = 12f;
    [SerializeField] private float rotSpeed        = 8f;

    [Header("Float")]
    [SerializeField] private float hoverHeight     = 3.8f;   // match FPS eyeHeight (4.0) minus a bit so ARIA is at eye level
    [SerializeField] private float bobAmplitude    = 0.18f;  // up/down bob range
    [SerializeField] private float bobSpeed        = 1.8f;   // bob cycles per second
    [SerializeField] private float tiltAngle       = 12f;    // gentle tilt while moving

    [Header("Speech")]
    [SerializeField] private float bubbleTime      = 5f;
    [SerializeField] private float fadeDuration    = 0.5f;
    [SerializeField] private float minCooldown     = 8f;
    [SerializeField] private float conversationTimeout = 18f;

    // ── Internal ─────────────────────────────────────────────────────────────

    private Transform  player;
    private bool       snappedToPlayer;
    private float      orbitAngle;
    private Rigidbody  rb;
    private Vector3    bounceVelocity;   // extra velocity from collision impacts
    private GameObject bubble;
    private RectTransform bubbleRect;
    private Text       bubbleText;
    private Text       codeBadgeText;
    private CanvasGroup bubbleCg;
    private Coroutine  hideCoroutine;
    private RobotVoiceBridge voiceBridge;
    private bool       waiting;
    private float      lastSpoke;
    private bool       voiceConnectInFlight;
    private float      lastVoiceConnectAttempt = -999f;
    private float      nextVoiceConnectAttempt = 1.5f;
    private int        voiceConnectFailureCount;
    private bool       conversationActive;
    private float      lastConversationHeard = -999f;
    private readonly System.Collections.Generic.List<string> conversationHistory = new System.Collections.Generic.List<string>(8);

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BuildNametag();
        BuildBubble();
        ResolvePlayer();
        SetupPhysics();
        SetupVoiceBridge();
        EnsureGameClientExists();

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
        if (voiceBridge != null)
            voiceBridge.FullTranscriptionReceived -= OnVoiceTranscription;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (player == null) { ResolvePlayer(); return; }

        // ── Snap to player on first frame ────────────────────────────────────
        if (!snappedToPlayer)
        {
            snappedToPlayer = true;
            orbitAngle = 45f;
            Vector3 snapDir = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward;
            transform.position = player.position + Vector3.up * hoverHeight + snapDir * followDistance;
        }

        var fpsCam = PlayerCache.GetFps()?.GetComponentInChildren<Camera>();
        bool inConversation = IsConversationActive();

        // ── Orbit slowly — smooth world-space angle, no snapping ─────────────
        if (!inConversation)
            orbitAngle = (orbitAngle + 12f * Time.deltaTime) % 360f;

        float bob        = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Vector3 orbitDir  = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward;
        Vector3 targetPos = player.position
            + Vector3.up * (hoverHeight + bob)
            + orbitDir * followDistance;
        if (inConversation && fpsCam != null)
        {
            targetPos = fpsCam.transform.position
                + fpsCam.transform.forward * 2.35f
                + Vector3.down * 0.35f
                + Vector3.right * 0.15f;
        }

        // ── Move toward orbit target — exponential smoothing, no snapping ───────
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Vector3 steer = inConversation
            ? (targetPos - transform.position).normalized
            : SteerAround(transform.position, (targetPos - transform.position).normalized);
        Vector3 next = Vector3.Lerp(transform.position, transform.position + steer * Vector3.Distance(transform.position, targetPos), t);
        if (rb != null && rb.isKinematic)
            rb.MovePosition(next);
        else
            transform.position = next;

        // ── Face the player's head — use the FPS camera, not Camera.main ────────
        // Camera.main in this project is an overview cam far from the player.
        // The real player camera is a child of the FPS controller.
        Vector3 headPos = player.position + Vector3.up * 4f; // FPS eyeHeight default
        if (fpsCam != null)
            headPos = fpsCam.transform.position;

        Vector3 lookDir = headPos - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized);
            if (!inConversation)
            {
                desiredRot *= Quaternion.Euler(0f, 0f, -tiltAngle * Mathf.Clamp01(
                    Vector3.Distance(transform.position, targetPos) - followDistance));
            }
            Quaternion smoothRot = Quaternion.Slerp(transform.rotation, desiredRot, rotSpeed * Time.deltaTime);
            if (rb != null && rb.isKinematic)
                rb.MoveRotation(smoothRot);
            else
                transform.rotation = smoothRot;
        }

        // ── Nametag + Bubble — shared camera reference ────────────────────────
        var fpsCamForBubble = PlayerCache.GetFps()?.GetComponentInChildren<Camera>();

        if (_nametag != null && fpsCamForBubble != null)
        {
            _nametag.position = transform.position + Vector3.up * 0.55f;
            Vector3 nDir = _nametag.position - fpsCamForBubble.transform.position;
            if (nDir.sqrMagnitude > 0.001f)
                _nametag.rotation = Quaternion.LookRotation(nDir);
        }

        // ── Bubble: push in front of Rudolf toward camera, always face camera ───
        if (bubble != null && fpsCamForBubble != null)
        {
            // Offset bubble toward the camera so it floats in front of the robot
            Vector3 toCamera = (fpsCamForBubble.transform.position - transform.position).normalized;
            bubble.transform.position = transform.position + toCamera * 0.55f + Vector3.up * 0.3f;
            // Face the camera
            bubble.transform.rotation = Quaternion.LookRotation(
                bubble.transform.position - fpsCamForBubble.transform.position);
        }

        UpdateVoiceListening(fpsCamForBubble);
    }

    // ── Obstacle avoidance ───────────────────────────────────────────────────

    /// <summary>
    /// SphereCasts forward. If blocked, tries 8 escape directions (left, right,
    /// up, diagonals). Returns the best clear direction, or the original if all
    /// are blocked (so Rudolf at least doesn't clip — he'll slow to a crawl).
    /// </summary>
    private Vector3 SteerAround(Vector3 origin, Vector3 desired)
    {
        const float probeRadius  = 0.35f; // slightly smaller than the collider
        const float probeLength  = 1.2f;  // how far ahead to look

        // Happy path — nothing ahead
        if (!Physics.SphereCast(origin, probeRadius, desired, out _, probeLength,
                ~LayerMask.GetMask("Rudolf", "Ignore Raycast")))
            return desired;

        // Try escape directions in priority order
        Vector3 right = Vector3.Cross(desired, Vector3.up).normalized;
        Vector3[] candidates =
        {
            right,                                          // right
            -right,                                         // left
            Vector3.up,                                     // up
            (right   + Vector3.up).normalized,              // right-up
            (-right  + Vector3.up).normalized,              // left-up
            (desired + Vector3.up).normalized,              // forward-up
            (desired + right).normalized,                   // forward-right
            (desired - right).normalized,                   // forward-left
        };

        foreach (var dir in candidates)
        {
            if (!Physics.SphereCast(origin, probeRadius, dir, out _, probeLength,
                    ~LayerMask.GetMask("Rudolf", "Ignore Raycast")))
                return dir;
        }

        // Everything blocked — move upward to escape
        return Vector3.up;
    }

    // ── Physics setup ────────────────────────────────────────────────────────

    private void SetupPhysics()
    {
        // Put Rudolf on his own layer so the SteerAround SphereCast (which masks
        // out "Rudolf") never trips on his own collider while probing for walls.
        int rudolfLayer = LayerMask.NameToLayer("Rudolf");
        if (rudolfLayer >= 0) gameObject.layer = rudolfLayer;

        var col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        col.center = Vector3.zero;

        var mat = new PhysicMaterial("RudolfBounce");
        mat.bounciness      = 0.75f;
        mat.dynamicFriction = 0.05f;
        mat.staticFriction  = 0.05f;
        mat.bounceCombine   = PhysicMaterialCombine.Maximum;
        mat.frictionCombine = PhysicMaterialCombine.Minimum;
        col.material = mat;

        // Kinematic — we drive position manually, no physics fighting the movement
        rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity    = false;
        rb.isKinematic   = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnCollisionEnter(Collision col)
    {
        bounceVelocity = col.contacts[0].normal * 3.5f;
        bounceVelocity.y = Mathf.Abs(bounceVelocity.y) + 1.2f;
        StartCoroutine(BounceRoutine());
    }

    private System.Collections.IEnumerator BounceRoutine()
    {
        float elapsed = 0f;
        const float duration = 0.45f;
        Vector3 startVel = bounceVelocity;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Decay the bounce velocity over time and apply it as a positional offset
            float frac = 1f - elapsed / duration;
            Vector3 offset = startVel * frac * Time.deltaTime;
            Vector3 next = transform.position + offset;
            if (rb != null && rb.isKinematic)
                rb.MovePosition(next);
            else
                transform.position = next;
            yield return null;
        }
        bounceVelocity = Vector3.zero;
    }

    // ── Speech bubble builder ────────────────────────────────────────────────

    private void BuildNametag()
    {
        var tag = new GameObject("Nametag");
        tag.transform.SetParent(transform, false);
        tag.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        var canvas = tag.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = tag.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(200f, 50f);
        rt.localScale = Vector3.one * 0.006f;

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(tag.transform, false);
        var txt = tgo.AddComponent<UnityEngine.UI.Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text      = "Rudolf";
        txt.fontSize  = 28;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        var tRt = tgo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        _nametag = tag.transform;
    }

    private Transform _nametag;

    private void BuildBubble()
    {
        // Bubble is NOT parented to ARIA — we position it freely in Update()
        // so it always floats between ARIA and the camera without clipping
        bubble = new GameObject("ARIA_Bubble");
        bubble.transform.position = transform.position + Vector3.up * 0.5f;

        var canvas = bubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        bubbleRect = bubble.GetComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(300f, 82f);
        bubbleRect.localScale = Vector3.one * 0.0058f;

        bubbleCg = bubble.AddComponent<CanvasGroup>();

        // Solid dark background — fully opaque so text never clips into the robot
        var bg = new GameObject("BG");
        bg.transform.SetParent(bubble.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.02f, 0.12f, 0.97f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Name label
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(bubble.transform, false);
        var nameTxt = nameGo.AddComponent<Text>();
        nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameTxt.text = "Rudolf";
        nameTxt.fontSize = 11;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.color = new Color(0.85f, 0.65f, 1f);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 1f); nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 1f);
        nameRt.sizeDelta = new Vector2(0f, 22f);
        nameRt.anchoredPosition = new Vector2(0f, -5f);
        nameRt.offsetMin = new Vector2(10, nameRt.offsetMin.y);
        nameRt.offsetMax = new Vector2(-10, nameRt.offsetMax.y);

        var badgeGo = new GameObject("CodeBadge");
        badgeGo.transform.SetParent(bubble.transform, false);
        codeBadgeText = badgeGo.AddComponent<Text>();
        codeBadgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        codeBadgeText.text = "CODE";
        codeBadgeText.fontSize = 10;
        codeBadgeText.fontStyle = FontStyle.Bold;
        codeBadgeText.color = new Color(0.08f, 0.95f, 1f);
        codeBadgeText.alignment = TextAnchor.MiddleRight;
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(1f, 1f); badgeRt.anchorMax = new Vector2(1f, 1f);
        badgeRt.pivot = new Vector2(1f, 1f);
        badgeRt.sizeDelta = new Vector2(70f, 22f);
        badgeRt.anchoredPosition = new Vector2(-10f, -5f);
        codeBadgeText.gameObject.SetActive(false);

        // Speech text
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(bubble.transform, false);
        bubbleText = textGo.AddComponent<Text>();
        bubbleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubbleText.fontSize = 14;
        bubbleText.color = Color.white;
        bubbleText.alignment = TextAnchor.UpperLeft;
        bubbleText.supportRichText = true;
        bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bubbleText.verticalOverflow = VerticalWrapMode.Overflow;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12, 10); textRt.offsetMax = new Vector2(-12, -30);

        bubble.SetActive(false);
    }

    // ── Trigger handling ─────────────────────────────────────────────────────

    private void SetupVoiceBridge()
    {
        voiceBridge = gameObject.AddComponent<RobotVoiceBridge>();
        voiceBridge.Initialize();
        voiceBridge.FullTranscriptionReceived += OnVoiceTranscription;
    }

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

    private void OnVoiceTranscription(string transcript)
    {
        if (waiting || Time.time - lastSpoke < 1.5f)
        {
            return;
        }

        bool wasConversationActive = IsConversationActive();
        string command;
        if (wasConversationActive)
        {
            command = StripWakePhraseIfPresent(transcript).Trim();
        }
        else if (!TryExtractWakeCommand(transcript, out command))
        {
            return;
        }
        else
        {
            conversationHistory.Clear();
        }

        lastSpoke = Time.time;
        conversationActive = true;
        lastConversationHeard = Time.time;

        if (string.IsNullOrWhiteSpace(command))
        {
            ShowLine("Listening…", 2.8f);
            return;
        }

        waiting = true;
        StartCoroutine(RequestVoiceReply(command));
    }

    private IEnumerator RequestVoiceReply(string transcript)
    {
        yield return null;

        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            waiting = false;
            ShowLine("I heard you, but I'm not connected to the mentor server.");
            yield break;
        }

        string context = IsInMultiplayerSession()
            ? "multiplayer_player_looked_at_robot"
            : "singleplayer_robot_always_listening";
        string history = BuildConversationHistoryContext();
        if (!string.IsNullOrWhiteSpace(history))
        {
            context += "\nRecent conversation:\n" + history;
        }

        yield return GameClient.Instance.SendPacket(new CompanionVoiceTextPacket(transcript, context));
        AddConversationTurn("Student", transcript);
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
                conversationActive = true;
                lastConversationHeard = Time.time;
                AddConversationTurn("Rudolf", r.Line);
                ShowLine(r.Line);
                if (voiceBridge != null)
                    voiceBridge.Speak(r.Line);
            });
    }

    // ── Show speech bubble ───────────────────────────────────────────────────

    private void ShowLine(string line)
    {
        ShowLine(line, -1f);
    }

    private void ShowLine(string line, float overrideDuration)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        string formattedLine = FormatBubbleText(line, out bool hasCode);
        if (bubbleText != null) bubbleText.text = formattedLine;
        if (codeBadgeText != null) codeBadgeText.gameObject.SetActive(hasCode);
        ApplyBubbleSize(line, hasCode);
        if (bubble != null)
        {
            bubble.SetActive(true);
            if (bubbleCg != null) bubbleCg.alpha = 1f;
        }

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        float displaySeconds = overrideDuration > 0f ? overrideDuration : CalculateDisplayTime(line, hasCode);
        hideCoroutine = StartCoroutine(HideBubble(displaySeconds));
    }

    private IEnumerator HideBubble(float displaySeconds)
    {
        yield return new WaitForSeconds(displaySeconds);
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

    private void ApplyBubbleSize(string rawLine, bool hasCode)
    {
        if (bubbleRect == null)
        {
            return;
        }

        int lineBreaks = 1;
        int longestLine = 0;
        int currentLine = 0;
        for (int i = 0; i < rawLine.Length; i++)
        {
            if (rawLine[i] == '\n')
            {
                lineBreaks++;
                longestLine = Mathf.Max(longestLine, currentLine);
                currentLine = 0;
            }
            else
            {
                currentLine++;
            }
        }
        longestLine = Mathf.Max(longestLine, currentLine);

        float width = Mathf.Clamp(260f + longestLine * (hasCode ? 5.8f : 4.4f), 300f, hasCode ? 620f : 460f);
        int wrappedLines = Mathf.CeilToInt(Mathf.Max(1f, rawLine.Length) / Mathf.Max(28f, width / 12f));
        int estimatedLines = Mathf.Max(lineBreaks, wrappedLines);
        float height = Mathf.Clamp(58f + estimatedLines * (hasCode ? 20f : 18f), 82f, hasCode ? 430f : 260f);

        bubbleRect.sizeDelta = new Vector2(width, height);
    }

    private float CalculateDisplayTime(string rawLine, bool hasCode)
    {
        float lengthTime = rawLine.Length * 0.055f;
        float lineTime = CountLines(rawLine) * 0.35f;
        float codeBonus = hasCode ? 5f : 0f;
        return Mathf.Clamp(bubbleTime + lengthTime + lineTime + codeBonus, bubbleTime, hasCode ? 28f : 18f);
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        int lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string FormatBubbleText(string rawLine, out bool hasCode)
    {
        hasCode = false;
        if (string.IsNullOrEmpty(rawLine))
        {
            return string.Empty;
        }

        string[] lines = rawLine.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        bool inFence = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                hasCode = true;
                inFence = !inFence;
                if (inFence)
                {
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append("<color=#48F1FF><b>▣ CODE</b></color>");
                }
                continue;
            }

            bool codeLine = inFence || line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal);
            if (codeLine)
            {
                hasCode = true;
                if (builder.Length > 0) builder.Append('\n');
                builder.Append("<color=#A6F7FF>│ ");
                builder.Append(EscapeRichText(line.TrimEnd()));
                builder.Append("</color>");
            }
            else
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(EscapeRichText(line));
            }
        }

        return builder.ToString();
    }

    private static string EscapeRichText(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("<", "‹").Replace(">", "›");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ResolvePlayer()
    {
        player = PlayerCache.ResolvePlayerTransform();
    }

    private void UpdateVoiceListening(Camera fpsCamera)
    {
        if (voiceBridge == null)
        {
            return;
        }

        EnsureVoiceServerConnection();

        bool inConversation = IsConversationActive();
        bool shouldListen = voiceBridge.HasSpeechRecognition && !waiting && !voiceBridge.IsSpeaking;
        if (shouldListen && IsInMultiplayerSession() && !inConversation)
        {
            shouldListen = IsPlayerLookingAtRudolf(fpsCamera);
        }

        voiceBridge.SetListening(shouldListen);
    }

    private static void EnsureGameClientExists()
    {
        if (GameClient.Instance != null)
        {
            return;
        }

        var clientObject = new GameObject("GameClient");
        clientObject.AddComponent<GameClient>();
    }

    private void EnsureVoiceServerConnection()
    {
        EnsureGameClientExists();

        if (voiceBridge == null || !voiceBridge.HasSpeechRecognition)
        {
            return;
        }

        if (GameClient.Instance == null ||
            GameClient.Instance.IsConnected ||
            GameClient.Instance.IsConnecting ||
            voiceConnectInFlight)
        {
            return;
        }

        if (Time.unscaledTime < nextVoiceConnectAttempt)
        {
            return;
        }

        lastVoiceConnectAttempt = Time.unscaledTime;
        _ = ConnectVoiceServerAsync();
    }

    private async System.Threading.Tasks.Task ConnectVoiceServerAsync()
    {
        voiceConnectInFlight = true;
        try
        {
            if (GameClient.Instance != null && !GameClient.Instance.IsConnected)
            {
                await GameClient.Instance.Connect();
            }

            if (GameClient.Instance != null && GameClient.Instance.IsConnected)
            {
                voiceConnectFailureCount = 0;
                nextVoiceConnectAttempt = Time.unscaledTime + 5f;
            }
            else
            {
                RegisterVoiceConnectFailure();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RobotCompanion] Voice server connection failed: " + ex.Message);
            RegisterVoiceConnectFailure();
        }
        finally
        {
            voiceConnectInFlight = false;
        }
    }

    private void RegisterVoiceConnectFailure()
    {
        voiceConnectFailureCount++;
        float delay = Mathf.Clamp(10f * voiceConnectFailureCount, 15f, 90f);
        nextVoiceConnectAttempt = Time.unscaledTime + delay;
    }

    private bool IsPlayerLookingAtRudolf(Camera fpsCamera)
    {
        if (fpsCamera == null)
        {
            return false;
        }

        Vector3 toRudolf = transform.position - fpsCamera.transform.position;
        float distance = toRudolf.magnitude;
        if (distance > 8f || distance < 0.05f)
        {
            return false;
        }

        float dot = Vector3.Dot(fpsCamera.transform.forward, toRudolf.normalized);
        return dot >= 0.86f;
    }

    private static bool IsInMultiplayerSession()
    {
        return MultiplayerSessionManager.Instance != null &&
               !string.IsNullOrEmpty(MultiplayerSessionManager.Instance.LocalClientId);
    }

    private bool IsConversationActive()
    {
        if (!conversationActive)
        {
            return false;
        }

        if (waiting)
        {
            return true;
        }

        if (voiceBridge != null && voiceBridge.IsSpeaking)
        {
            lastConversationHeard = Time.time;
            return true;
        }

        if (Time.time - lastConversationHeard <= conversationTimeout)
        {
            return true;
        }

        conversationActive = false;
        conversationHistory.Clear();
        return false;
    }

    private void AddConversationTurn(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string cleanText = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (cleanText.Length > 360)
        {
            cleanText = cleanText.Substring(0, 360) + "…";
        }

        conversationHistory.Add(speaker + ": " + cleanText);
        while (conversationHistory.Count > 8)
        {
            conversationHistory.RemoveAt(0);
        }
    }

    private string BuildConversationHistoryContext()
    {
        if (conversationHistory.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < conversationHistory.Count; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(conversationHistory[i]);
        }

        return builder.ToString();
    }

    private static bool TryExtractWakeCommand(string transcript, out string command)
    {
        command = string.Empty;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        string trimmed = transcript.Trim();
        string normalized = NormalizeWakeText(trimmed);
        string[] words = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int wakeIndex = FindWakeNameIndex(words);
        if (wakeIndex < 0)
        {
            return false;
        }

        bool hasWakeLead = wakeIndex == 0 ||
            (wakeIndex <= 4 && IsWakeLead(words[Mathf.Max(0, wakeIndex - 1)])) ||
            (wakeIndex <= 5 && normalized.Contains("hey " + words[wakeIndex]));
        if (!hasWakeLead)
        {
            return false;
        }

        command = BuildCommandAfterWake(words, wakeIndex);
        return true;
    }

    private static string StripWakePhraseIfPresent(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        string normalized = NormalizeWakeText(transcript);
        string[] words = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int wakeIndex = FindWakeNameIndex(words);
        if (wakeIndex < 0)
        {
            return transcript;
        }

        string command = BuildCommandAfterWake(words, wakeIndex);
        return string.IsNullOrWhiteSpace(command) ? transcript : command;
    }

    private static string NormalizeWakeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return builder.ToString();
    }

    private static int FindWakeNameIndex(string[] words)
    {
        int limit = Mathf.Min(words.Length, 8);
        for (int i = 0; i < limit; i++)
        {
            if (IsWakeName(words[i]))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsWakeLead(string word)
    {
        return word == "hey" || word == "hi" || word == "hello" || word == "yo" || word == "ok" || word == "okay";
    }

    private static bool IsWakeName(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        return word == "rudolf" ||
               word == "rudolph" ||
               word == "rodolf" ||
               word == "rudolfs" ||
               word == "rudolphs" ||
               word == "robot" ||
               word.StartsWith("rudol", StringComparison.Ordinal) ||
               word.StartsWith("rudop", StringComparison.Ordinal);
    }

    private static string BuildCommandAfterWake(string[] words, int wakeIndex)
    {
        int start = wakeIndex + 1;
        while (start < words.Length && (words[start] == "please" || words[start] == "can" || words[start] == "you"))
        {
            break;
        }

        if (start >= words.Length)
        {
            return string.Empty;
        }

        return string.Join(" ", words, start, words.Length - start).Trim();
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
