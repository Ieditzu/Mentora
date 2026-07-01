using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
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

    [Header("Guide")]
    [SerializeField] private float guideSpeed = 6.5f;
    [SerializeField] private float guideWaitSpeed = 0.35f;
    [SerializeField] private float guideWaitForPlayerDistance = 16f;
    [SerializeField] private float guidePlayerArrivalDistance = 10f;
    [SerializeField] private float guideHoverAboveGround = 3.4f;
    [SerializeField] private float guideWaypointReachDistance = 3f;
    [SerializeField] private float guideNavMeshSampleDistance = 14f;
    [SerializeField] private float guideGroundProbeHeight = 26f;
    [SerializeField] private float guideGroundProbeDistance = 70f;
    [SerializeField] private float guideCollisionRadius = 0.48f;
    [SerializeField] private float guideObstacleProbeDistance = 2.6f;
    [SerializeField] private float guideDetourDistance = 4.5f;
    [SerializeField] private float guideDetourReachDistance = 1.6f;
    [SerializeField] private float guideStuckRepathSeconds = 1.25f;
    [SerializeField] private float guideOutlineWidth = 0.045f;

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
    private bool       voiceWasSpeaking;
    private float      resumeVoiceListeningAt;
    private float      lastNoTranscriptWakeAt = -999f;
    private float      lastNoTranscriptPromptAt = -999f;
    private float      nextSakuraCollisionRefresh;
    private int        ignoredSakuraColliderCount = -1;
    private RudolfIslandGuideTarget guideTarget;
    private bool       guideActive;
    private bool       guideWaitingForPlayer;
    private Light      guideLight;
    private float      lastGuideWaitLine = -999f;
    private int        guidePathIndex;
    private Vector3    guideDestinationPosition;
    private Vector3    lastGuideProgressPosition;
    private Vector3    guideDetourTarget;
    private float      guideStuckTimer;
    private bool       guideDetourActive;
    private readonly List<Vector3> guidePathCorners = new List<Vector3>(16);
    private GameObject guideGlowRoot;
    private GameObject boosterRoot;
    private Material   guideOutlineMaterial;
    private Material   guideChamsMaterial;
    private Material   boosterRingMaterial;
    private readonly List<Renderer> guideShellRenderers = new List<Renderer>(16);
    private readonly List<LineRenderer> boosterRings = new List<LineRenderer>(3);
    private static readonly Color GuideAqua = new Color(0.0f, 0.95f, 1f, 1f);
    private const float PostSpeechListenDelay = 1.25f;
    private const float NoTranscriptWakeCooldown = 2f;
    private const float NoTranscriptWakeVadPeak = 0.026f;
    private const float NoTranscriptWakeAudioPeak = 0.012f;
    private readonly List<string> conversationHistory = new List<string>(8);

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
        RefreshIgnoredSakuraCollisions(true);
        SetupGuideHighlight();
        SetupGuideGlowVisuals();
        SetupBoosterRings();
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
        {
            voiceBridge.FullTranscriptionReceived -= OnVoiceTranscription;
            voiceBridge.TranscriptionFailedWithoutText -= OnVoiceNoTranscript;
        }
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
        bool guiding = IsGuideActive();

        // ── Orbit slowly — smooth world-space angle, no snapping ─────────────
        if (!inConversation && !guiding)
            orbitAngle = (orbitAngle + 12f * Time.deltaTime) % 360f;

        float bob        = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Vector3 orbitDir  = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward;
        Vector3 targetPos = player.position
            + Vector3.up * (hoverHeight + bob)
            + orbitDir * followDistance;
        if (guiding && guideTarget != null)
        {
            targetPos = GetGuideFlightTarget(bob);
        }
        else if (inConversation && fpsCam != null)
        {
            targetPos = fpsCam.transform.position
                + fpsCam.transform.forward * 2.35f
                + Vector3.down * 0.35f
                + Vector3.right * 0.15f;
        }

        // ── Move toward orbit target — exponential smoothing, no snapping ───────
        Vector3 next;
        if (guiding)
        {
            next = CalculateGuideNextPosition(targetPos);
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            Vector3 desiredDirection = (targetPos - transform.position).normalized;
            Vector3 steer = inConversation
                ? desiredDirection
                : SteerAround(transform.position, desiredDirection);
            next = Vector3.Lerp(transform.position, transform.position + steer * Vector3.Distance(transform.position, targetPos), t);
        }

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

        Vector3 lookDir = guiding
            ? GetGuideLookDirection(targetPos, next)
            : headPos - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized);
            if (guiding)
            {
                float bank = -tiltAngle * Mathf.Clamp01(Vector3.Distance(transform.position, targetPos) / 7f);
                float pitch = Mathf.Sin(Time.time * 7.5f) * 3f;
                desiredRot *= Quaternion.Euler(pitch, 0f, bank);
            }
            else if (!inConversation)
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

        UpdateGuideState();
        UpdateBoosterRings(guiding);
        RefreshIgnoredSakuraCollisions(false);
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
        float probeRadius = Mathf.Max(0.35f, guideCollisionRadius);
        float probeLength = Mathf.Max(1.2f, guideObstacleProbeDistance);

        // Happy path — nothing ahead
        if (!Physics.SphereCast(origin, probeRadius, desired, out _, probeLength,
                GetGuideCollisionMask(), QueryTriggerInteraction.Ignore))
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
                    GetGuideCollisionMask(), QueryTriggerInteraction.Ignore))
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
        if (col != null && col.collider != null && IsSakuraNoClipTransform(col.collider.transform))
        {
            return;
        }

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
        voiceBridge.TranscriptionFailedWithoutText += OnVoiceNoTranscript;
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
        if (waiting)
        {
            return;
        }

        bool wasConversationActive = IsConversationActive();
        if (!wasConversationActive && Time.time - lastSpoke < 1f)
        {
            return;
        }

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

        if (TryHandleGuideCommand(command))
        {
            return;
        }

        waiting = true;
        StartCoroutine(RequestVoiceReply(command));
    }

    private void OnVoiceNoTranscript(int byteCount, float vadPeak, float audioPeak, float appliedGain)
    {
        if (waiting || voiceBridge == null || voiceBridge.IsSpeaking)
        {
            return;
        }

        bool active = IsConversationActive();
        if (active)
        {
            lastConversationHeard = Time.time;
            if (Time.time - lastNoTranscriptPromptAt > 3.5f)
            {
                lastNoTranscriptPromptAt = Time.time;
                ShowLine("I didn't catch that — try again.", 2.2f);
            }
            return;
        }

        if (Time.time - lastNoTranscriptWakeAt < NoTranscriptWakeCooldown ||
            vadPeak < NoTranscriptWakeVadPeak ||
            audioPeak < NoTranscriptWakeAudioPeak)
        {
            return;
        }

        if (IsInMultiplayerSession())
        {
            Camera fpsCamera = PlayerCache.GetFps()?.GetComponentInChildren<Camera>();
            if (!IsPlayerLookingAtRudolf(fpsCamera))
            {
                return;
            }
        }

        lastNoTranscriptWakeAt = Time.time;
        lastNoTranscriptPromptAt = Time.time;
        conversationHistory.Clear();
        conversationActive = true;
        lastConversationHeard = Time.time;
        ShowLine("Listening…", 3f);
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

    private void SetupGuideHighlight()
    {
        Transform existing = transform.Find("RudolfGuideHighlight");
        GameObject lightObject = existing != null ? existing.gameObject : new GameObject("RudolfGuideHighlight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.65f;
        lightObject.transform.localRotation = Quaternion.identity;

        guideLight = lightObject.GetComponent<Light>();
        if (guideLight == null)
        {
            guideLight = lightObject.AddComponent<Light>();
        }

        guideLight.type = LightType.Point;
        guideLight.color = new Color(0.25f, 0.85f, 1f, 1f);
        guideLight.range = 7f;
        guideLight.intensity = 0f;
        guideLight.shadows = LightShadows.None;
        guideLight.enabled = false;
    }

    private void SetupGuideGlowVisuals()
    {
        Transform existingRoot = transform.Find("RudolfGuideGlowShells");
        guideGlowRoot = existingRoot != null ? existingRoot.gameObject : new GameObject("RudolfGuideGlowShells");
        guideGlowRoot.transform.SetParent(transform, false);
        guideGlowRoot.transform.localPosition = Vector3.zero;
        guideGlowRoot.transform.localRotation = Quaternion.identity;
        guideGlowRoot.transform.localScale = Vector3.one;
        guideGlowRoot.layer = gameObject.layer;

        Shader glowShader = Shader.Find("Mentora/RudolfGuideGlow");
        if (glowShader == null)
        {
            glowShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        guideOutlineMaterial = new Material(glowShader);
        guideOutlineMaterial.name = "RudolfGuideOutlineRuntime";
        guideOutlineMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, 0.72f));
        guideOutlineMaterial.SetFloat("_Extrude", guideOutlineWidth);
        guideOutlineMaterial.SetFloat("_ZTest", 4f);
        guideOutlineMaterial.SetFloat("_Cull", 1f);

        guideChamsMaterial = new Material(glowShader);
        guideChamsMaterial.name = "RudolfGuideChamsRuntime";
        guideChamsMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, 0.22f));
        guideChamsMaterial.SetFloat("_Extrude", 0.006f);
        guideChamsMaterial.SetFloat("_ZTest", 8f);
        guideChamsMaterial.SetFloat("_Cull", 0f);

        guideShellRenderers.Clear();
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            MeshRenderer source = meshRenderers[i];
            if (source == null ||
                source.transform.IsChildOf(guideGlowRoot.transform) ||
                source.name.EndsWith("_GuideOutline", StringComparison.Ordinal) ||
                source.name.EndsWith("_GuideChams", StringComparison.Ordinal))
            {
                continue;
            }

            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            CreateGuideMeshShell(source, filter.sharedMesh, guideOutlineMaterial, "_GuideOutline");
            CreateGuideMeshShell(source, filter.sharedMesh, guideChamsMaterial, "_GuideChams");
        }

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer source = skinnedRenderers[i];
            if (source == null ||
                source.sharedMesh == null ||
                source.transform.IsChildOf(guideGlowRoot.transform) ||
                source.name.EndsWith("_GuideOutline", StringComparison.Ordinal) ||
                source.name.EndsWith("_GuideChams", StringComparison.Ordinal))
            {
                continue;
            }

            CreateGuideSkinnedShell(source, guideOutlineMaterial, "_GuideOutline");
            CreateGuideSkinnedShell(source, guideChamsMaterial, "_GuideChams");
        }

        SetGuideShellsActive(false);
    }

    private void CreateGuideMeshShell(MeshRenderer source, Mesh mesh, Material material, string suffix)
    {
        GameObject shell = new GameObject(source.name + suffix);
        shell.layer = gameObject.layer;
        shell.transform.SetParent(source.transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = Vector3.one;

        MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
        shellFilter.sharedMesh = mesh;

        MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = material;
        shellRenderer.enabled = false;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        guideShellRenderers.Add(shellRenderer);
    }

    private void CreateGuideSkinnedShell(SkinnedMeshRenderer source, Material material, string suffix)
    {
        GameObject shell = new GameObject(source.name + suffix);
        shell.layer = gameObject.layer;
        shell.transform.SetParent(source.transform.parent != null ? source.transform.parent : transform, false);
        shell.transform.localPosition = source.transform.localPosition;
        shell.transform.localRotation = source.transform.localRotation;
        shell.transform.localScale = source.transform.localScale;

        SkinnedMeshRenderer shellRenderer = shell.AddComponent<SkinnedMeshRenderer>();
        shellRenderer.sharedMesh = source.sharedMesh;
        shellRenderer.bones = source.bones;
        shellRenderer.rootBone = source.rootBone;
        shellRenderer.updateWhenOffscreen = true;
        shellRenderer.sharedMaterial = material;
        shellRenderer.enabled = false;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        guideShellRenderers.Add(shellRenderer);
    }

    private void SetupBoosterRings()
    {
        Transform existingRoot = transform.Find("RudolfBoostRings");
        GameObject boosterRootObject = existingRoot != null ? existingRoot.gameObject : new GameObject("RudolfBoostRings");
        boosterRoot = boosterRootObject;
        boosterRootObject.layer = gameObject.layer;
        boosterRootObject.transform.SetParent(transform, false);
        boosterRootObject.transform.localPosition = Vector3.down * 0.38f;
        boosterRootObject.transform.localRotation = Quaternion.identity;
        boosterRootObject.transform.localScale = GetInverseParentScale();

        Shader ringShader = Shader.Find("Sprites/Default");
        if (ringShader == null)
        {
            ringShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        boosterRingMaterial = new Material(ringShader);
        boosterRingMaterial.name = "RudolfBoostRingRuntime";
        boosterRingMaterial.color = GuideAqua;

        boosterRings.Clear();
        for (int i = 0; i < 3; i++)
        {
            Transform existingRing = boosterRootObject.transform.Find("BoostRing_" + i);
            GameObject ringObject = existingRing != null ? existingRing.gameObject : new GameObject("BoostRing_" + i);
            ringObject.layer = gameObject.layer;
            ringObject.transform.SetParent(boosterRootObject.transform, false);
            ringObject.transform.localRotation = Quaternion.identity;

            LineRenderer ring = ringObject.GetComponent<LineRenderer>();
            if (ring == null)
            {
                ring = ringObject.AddComponent<LineRenderer>();
            }

            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 72;
            ring.enabled = true;
            ring.sharedMaterial = boosterRingMaterial;
            ring.textureMode = LineTextureMode.Stretch;
            ring.alignment = LineAlignment.View;
            ring.numCapVertices = 4;
            ring.numCornerVertices = 4;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            SetRingCircle(ring, 0.62f + i * 0.12f);
            boosterRings.Add(ring);
        }
    }

    private static void SetRingCircle(LineRenderer ring, float radius)
    {
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = ((float)i / ring.positionCount) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void UpdateBoosterRings(bool guiding)
    {
        if (boosterRoot != null)
        {
            boosterRoot.transform.position = transform.position + Vector3.down * 0.38f;
            boosterRoot.transform.rotation = Quaternion.identity;
            boosterRoot.transform.localScale = GetInverseParentScale();
        }

        float alphaBase = guiding ? 0.78f : 0.42f;
        float speed = guiding ? 1.85f : 1.1f;
        for (int i = 0; i < boosterRings.Count; i++)
        {
            LineRenderer ring = boosterRings[i];
            if (ring == null)
            {
                continue;
            }

            float phase = Mathf.Repeat(Time.time * speed + i * 0.33f, 1f);
            float alpha = alphaBase * (1f - phase);
            float scale = Mathf.Lerp(0.48f, guiding ? 1.08f : 0.88f, phase);
            ring.transform.localScale = new Vector3(scale, scale, scale);
            ring.transform.localPosition = Vector3.down * (0.05f + phase * 0.42f);
            ring.transform.Rotate(Vector3.up, (guiding ? 95f : 52f) * Time.deltaTime * (i % 2 == 0 ? 1f : -1f), Space.Self);
            ring.widthMultiplier = Mathf.Lerp(0.045f, 0.012f, phase);

            Color ringColor = new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, alpha);
            ring.startColor = ringColor;
            ring.endColor = ringColor;
        }
    }

    private Vector3 GetInverseParentScale()
    {
        Vector3 parentScale = transform.lossyScale;
        return new Vector3(SafeInverseScale(parentScale.x), SafeInverseScale(parentScale.y), SafeInverseScale(parentScale.z));
    }

    private static float SafeInverseScale(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private bool TryHandleGuideCommand(string command)
    {
        string normalized = NormalizeCommand(command);
        if (IsGuideCancelCommand(normalized))
        {
            StopGuide("Okay, I'll stop guiding.");
            return true;
        }

        if (!RudolfIslandGuideTarget.TryParse(command, out RudolfIslandGuideTarget.IslandId islandId))
        {
            return false;
        }

        if (!HasGuideIntent(normalized))
        {
            return false;
        }

        StartGuide(islandId);
        return true;
    }

    private void StartGuide(RudolfIslandGuideTarget.IslandId islandId)
    {
        RudolfIslandGuideTarget target = RudolfIslandGuideTarget.Find(islandId);
        string islandName = RudolfIslandGuideTarget.GetDisplayName(islandId);
        if (target == null)
        {
            string missingLine = "I don't have a guide marker for " + islandName + " yet.";
            ShowLine(missingLine, 5f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(missingLine);
            }
            return;
        }

        guideTarget = target;
        guideActive = true;
        guideWaitingForPlayer = false;
        lastGuideWaitLine = -999f;
        guideStuckTimer = 0f;
        guideDetourActive = false;
        lastGuideProgressPosition = transform.position;
        BuildGuidePath(target);
        conversationActive = true;
        lastConversationHeard = Time.time;
        SetGuideHighlight(true);

        string line = "Follow me — I'll guide you to " + target.DisplayName + ".";
        AddConversationTurn("Student", "Guide me to " + target.DisplayName + ".");
        AddConversationTurn("Rudolf", line);
        ShowLine(line, 5f);
        if (voiceBridge != null)
        {
            voiceBridge.Speak(line);
        }
    }

    private void StopGuide(string line)
    {
        guideActive = false;
        guideTarget = null;
        guideWaitingForPlayer = false;
        guidePathCorners.Clear();
        guidePathIndex = 0;
        guideDetourActive = false;
        guideStuckTimer = 0f;
        SetGuideHighlight(false);

        if (!string.IsNullOrWhiteSpace(line))
        {
            AddConversationTurn("Rudolf", line);
            ShowLine(line, 4f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(line);
            }
        }
    }

    private bool IsGuideActive()
    {
        if (!guideActive)
        {
            return false;
        }

        if (guideTarget != null)
        {
            return true;
        }

        guideActive = false;
        SetGuideHighlight(false);
        return false;
    }

    private Vector3 CalculateGuideNextPosition(Vector3 targetPos)
    {
        if (guideTarget == null)
        {
            return transform.position;
        }

        float playerDistance = player != null ? Vector3.Distance(player.position, transform.position) : 0f;
        guideWaitingForPlayer = playerDistance > guideWaitForPlayerDistance;

        float speed = guideWaitingForPlayer ? guideWaitSpeed : guideSpeed;
        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.sqrMagnitude <= 0.0025f)
        {
            return targetPos;
        }

        Vector3 steer = SteerAround(transform.position, toTarget.normalized);
        Vector3 next = transform.position + steer * speed * Time.deltaTime;
        next = ResolveGuideCollision(next);
        next = ApplyGuideHover(next, targetPos.y, Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        if (Vector3.Distance(transform.position, targetPos) <= speed * Time.deltaTime)
        {
            return targetPos;
        }

        return next;
    }

    private void UpdateGuideState()
    {
        if (!IsGuideActive())
        {
            return;
        }

        if (guideLight != null)
        {
            guideLight.intensity = 2.1f + Mathf.Sin(Time.time * 5.5f) * 0.55f;
            guideLight.range = 7.5f + Mathf.Sin(Time.time * 3f) * 0.8f;
        }
        UpdateGuideGlowMaterials();

        if (guideWaitingForPlayer && Time.time - lastGuideWaitLine > 8f)
        {
            lastGuideWaitLine = Time.time;
            ShowLine("I'll slow down — follow the glow.", 3.2f);
        }

        if (!guideWaitingForPlayer)
        {
            if (HorizontalDistance(transform.position, lastGuideProgressPosition) < 0.08f)
            {
                guideStuckTimer += Time.deltaTime;
            }
            else
            {
                guideStuckTimer = 0f;
                lastGuideProgressPosition = transform.position;
            }

            if (guideStuckTimer >= guideStuckRepathSeconds)
            {
                guideStuckTimer = 0f;
                guideDetourActive = false;
                BuildGuidePath(guideTarget);
            }
        }

        float robotDistance = Vector3.Distance(transform.position, ApplyGuideHover(guideDestinationPosition, guideDestinationPosition.y + guideHoverAboveGround, 0f));
        float playerDistance = player != null ? Vector3.Distance(player.position, guideTarget.transform.position) : float.MaxValue;
        if (robotDistance <= guideTarget.ArrivalRadius && playerDistance <= guidePlayerArrivalDistance)
        {
            string arrivedLine = "We're here — this is " + guideTarget.DisplayName + ".";
            StopGuide(null);
            conversationActive = true;
            lastConversationHeard = Time.time;
            AddConversationTurn("Rudolf", arrivedLine);
            ShowLine(arrivedLine, 5f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(arrivedLine);
            }
        }
    }

    private void SetGuideHighlight(bool active)
    {
        if (guideLight == null)
        {
            return;
        }

        guideLight.enabled = active;
        if (!active)
        {
            guideLight.intensity = 0f;
        }

        SetGuideShellsActive(active);
    }

    private void BuildGuidePath(RudolfIslandGuideTarget target)
    {
        guidePathCorners.Clear();
        guidePathIndex = 0;
        guideDetourActive = false;
        if (target == null)
        {
            return;
        }

        guideDestinationPosition = target.transform.position;
        Vector3 startSource = transform.position;
        NavMeshHit startHit;
        NavMeshHit targetHit;
        if (NavMesh.SamplePosition(startSource, out startHit, guideNavMeshSampleDistance, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(target.transform.position, out targetHit, guideNavMeshSampleDistance, NavMesh.AllAreas))
        {
            guideDestinationPosition = targetHit.position;
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path) &&
                path.corners != null &&
                path.corners.Length > 1)
            {
                for (int i = 1; i < path.corners.Length; i++)
                {
                    guidePathCorners.Add(path.corners[i]);
                }
            }
        }

        if (guidePathCorners.Count == 0)
        {
            guidePathCorners.Add(guideDestinationPosition);
        }
        else
        {
            Vector3 finalCorner = guidePathCorners[guidePathCorners.Count - 1];
            if (Vector3.Distance(finalCorner, guideDestinationPosition) > guideWaypointReachDistance)
            {
                guidePathCorners.Add(guideDestinationPosition);
            }
        }
    }

    private Vector3 GetGuideFlightTarget(float bob)
    {
        if (guideTarget == null)
        {
            return transform.position;
        }

        if (guideDetourActive)
        {
            if (HorizontalDistance(transform.position, guideDetourTarget) <= guideDetourReachDistance)
            {
                guideDetourActive = false;
            }
            else
            {
                return ApplyGuideHover(guideDetourTarget, guideDetourTarget.y + guideHoverAboveGround + bob, bob);
            }
        }

        if (guidePathCorners.Count == 0)
        {
            guidePathCorners.Add(guideDestinationPosition);
        }

        while (guidePathIndex < guidePathCorners.Count - 1 &&
               HorizontalDistance(transform.position, guidePathCorners[guidePathIndex]) <= guideWaypointReachDistance)
        {
            guidePathIndex++;
        }

        Vector3 waypoint = guidePathCorners[Mathf.Clamp(guidePathIndex, 0, guidePathCorners.Count - 1)];
        return ApplyGuideHover(waypoint, waypoint.y + guideHoverAboveGround + bob, bob);
    }

    private Vector3 GetGuideLookDirection(Vector3 targetPos, Vector3 next)
    {
        Vector3 direction = next - transform.position;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = targetPos - transform.position;
        }

        direction.y *= 0.35f;
        if (direction.sqrMagnitude < 0.01f && guideTarget != null)
        {
            direction = guideTarget.transform.position - transform.position;
            direction.y = 0f;
        }

        return direction;
    }

    private Vector3 ApplyGuideHover(Vector3 position, float fallbackY, float bob)
    {
        Vector3 probeOrigin = new Vector3(position.x, position.y + guideGroundProbeHeight, position.z);
        RaycastHit[] hits = Physics.RaycastAll(probeOrigin, Vector3.down, guideGroundProbeDistance, GetGuideCollisionMask(), QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsGuideGroundHit(hit))
                {
                    position.y = hit.point.y + guideHoverAboveGround + bob;
                    return position;
                }
            }
        }

        position.y = fallbackY;
        return position;
    }

    private Vector3 ResolveGuideCollision(Vector3 desiredNext)
    {
        Vector3 move = desiredNext - transform.position;
        float distance = move.magnitude;
        if (distance <= 0.001f)
        {
            return desiredNext;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            guideCollisionRadius,
            move.normalized,
            distance + 0.08f,
            GetGuideCollisionMask(),
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return desiredNext;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (ShouldIgnoreGuideCollider(hit.collider))
            {
                continue;
            }

            Vector3 detour;
            if (TryFindGuideDetour(hit, desiredNext, out detour))
            {
                guideDetourTarget = detour;
                guideDetourActive = true;
                Vector3 toDetour = detour - transform.position;
                if (toDetour.sqrMagnitude > 0.01f)
                {
                    return transform.position + toDetour.normalized * Mathf.Min(distance, guideSpeed * Time.deltaTime);
                }
            }

            float safeDistance = Mathf.Max(0f, hit.distance - 0.08f);
            Vector3 safePosition = transform.position + move.normalized * Mathf.Min(safeDistance, distance);
            Vector3 remainingMove = desiredNext - safePosition;
            Vector3 slide = Vector3.ProjectOnPlane(remainingMove, hit.normal);
            if (slide.sqrMagnitude <= 0.0001f)
            {
                return safePosition;
            }

            return safePosition + slide.normalized * Mathf.Min(slide.magnitude, guideSpeed * Time.deltaTime * 0.65f);
        }

        return desiredNext;
    }

    private bool TryFindGuideDetour(RaycastHit obstacleHit, Vector3 desiredNext, out Vector3 detour)
    {
        detour = Vector3.zero;
        if (obstacleHit.collider == null)
        {
            return false;
        }

        Vector3 desiredFlat = desiredNext - transform.position;
        desiredFlat.y = 0f;
        if (desiredFlat.sqrMagnitude < 0.01f && guideTarget != null)
        {
            desiredFlat = guideDestinationPosition - transform.position;
            desiredFlat.y = 0f;
        }
        if (desiredFlat.sqrMagnitude < 0.01f)
        {
            return false;
        }

        Vector3 forward = desiredFlat.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            return false;
        }

        Bounds bounds = obstacleHit.collider.bounds;
        float obstacleRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + guideDetourDistance, guideDetourDistance, 10f);
        Vector3 basePoint = obstacleHit.point;
        basePoint.y = transform.position.y;

        Vector3[] candidates =
        {
            basePoint + right * obstacleRadius + forward * 1.5f,
            basePoint - right * obstacleRadius + forward * 1.5f,
            bounds.center + right * obstacleRadius,
            bounds.center - right * obstacleRadius,
            transform.position + right * guideDetourDistance,
            transform.position - right * guideDetourDistance,
            transform.position + (right + forward).normalized * guideDetourDistance,
            transform.position + (-right + forward).normalized * guideDetourDistance
        };

        float bestScore = float.MaxValue;
        Vector3 best = Vector3.zero;
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 candidate = ProjectGuideDetourToWalkable(candidates[i]);
            candidate = ApplyGuideHover(candidate, candidate.y + guideHoverAboveGround, 0f);
            if (!IsGuidePathClear(transform.position, candidate))
            {
                continue;
            }

            float score = HorizontalDistance(candidate, guideDestinationPosition) + HorizontalDistance(transform.position, candidate) * 0.25f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (bestScore >= float.MaxValue * 0.5f)
        {
            return false;
        }

        detour = best;
        return true;
    }

    private Vector3 ProjectGuideDetourToWalkable(Vector3 candidate)
    {
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(candidate, out navHit, guideNavMeshSampleDistance, NavMesh.AllAreas))
        {
            candidate.x = navHit.position.x;
            candidate.z = navHit.position.z;
            candidate.y = navHit.position.y;
        }

        return candidate;
    }

    private bool IsGuidePathClear(Vector3 from, Vector3 to)
    {
        Vector3 move = to - from;
        float distance = move.magnitude;
        if (distance <= 0.05f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            from,
            guideCollisionRadius,
            move.normalized,
            distance,
            GetGuideCollisionMask(),
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            if (!ShouldIgnoreGuideCollider(hits[i].collider))
            {
                return false;
            }
        }

        return true;
    }

    private int GetGuideCollisionMask()
    {
        return ~LayerMask.GetMask("Rudolf", "Ignore Raycast");
    }

    private void RefreshIgnoredSakuraCollisions(bool force)
    {
        if (!force && Time.unscaledTime < nextSakuraCollisionRefresh)
        {
            return;
        }

        nextSakuraCollisionRefresh = Time.unscaledTime + (ignoredSakuraColliderCount > 0 ? 6f : 1f);
        Collider[] rudolfColliders = GetComponentsInChildren<Collider>(true);
        if (rudolfColliders == null || rudolfColliders.Length == 0)
        {
            return;
        }

        Collider[] sceneColliders = UnityEngine.Object.FindObjectsOfType<Collider>(true);
        int ignoredCount = 0;
        for (int i = 0; i < sceneColliders.Length; i++)
        {
            Collider sakuraCollider = sceneColliders[i];
            if (!IsSakuraNoClipCollider(sakuraCollider))
            {
                continue;
            }

            ignoredCount++;
            for (int j = 0; j < rudolfColliders.Length; j++)
            {
                Collider rudolfCollider = rudolfColliders[j];
                if (rudolfCollider == null || rudolfCollider == sakuraCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(rudolfCollider, sakuraCollider, true);
            }
        }

        ignoredSakuraColliderCount = ignoredCount;
    }

    private bool IsGuideGroundHit(RaycastHit hit)
    {
        if (ShouldIgnoreGuideCollider(hit.collider))
        {
            return false;
        }

        if (hit.normal.y < 0.45f)
        {
            return false;
        }

        return !IsTreeOrFoliage(hit.collider.transform);
    }

    private bool ShouldIgnoreGuideCollider(Collider guideCollider)
    {
        return guideCollider == null ||
               guideCollider.isTrigger ||
               guideCollider.transform.IsChildOf(transform) ||
               IsSakuraNoClipCollider(guideCollider);
    }

    private static bool IsSakuraNoClipCollider(Collider guideCollider)
    {
        return guideCollider != null && IsSakuraNoClipTransform(guideCollider.transform);
    }

    private static bool IsSakuraNoClipTransform(Transform hitTransform)
    {
        while (hitTransform != null)
        {
            string name = hitTransform.name.ToLowerInvariant();
            if (name == "sakura2" ||
                name.Contains("sakura2") ||
                name.Contains("sakura") ||
                name.Contains("cherry") ||
                name.Contains("blossom"))
            {
                return true;
            }

            hitTransform = hitTransform.parent;
        }

        return false;
    }

    private static bool IsTreeOrFoliage(Transform hitTransform)
    {
        while (hitTransform != null)
        {
            string name = hitTransform.name.ToLowerInvariant();
            if (name.Contains("tree") ||
                name.Contains("leaf") ||
                name.Contains("leaves") ||
                name.Contains("foliage") ||
                name.Contains("branch") ||
                name.Contains("trunk"))
            {
                return true;
            }

            hitTransform = hitTransform.parent;
        }

        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void SetGuideShellsActive(bool active)
    {
        for (int i = 0; i < guideShellRenderers.Count; i++)
        {
            if (guideShellRenderers[i] != null)
            {
                guideShellRenderers[i].enabled = active;
            }
        }
    }

    private void UpdateGuideGlowMaterials()
    {
        if (guideOutlineMaterial == null || guideChamsMaterial == null)
        {
            return;
        }

        float pulse = 0.5f + Mathf.Sin(Time.time * 6f) * 0.5f;
        guideOutlineMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, Mathf.Lerp(0.62f, 0.88f, pulse)));
        guideOutlineMaterial.SetFloat("_Extrude", Mathf.Lerp(guideOutlineWidth * 0.8f, guideOutlineWidth * 1.25f, pulse));
        guideChamsMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, Mathf.Lerp(0.16f, 0.28f, pulse)));
    }

    private static string NormalizeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return string.Empty;
        }

        string lower = command.ToLowerInvariant().Replace("+", " plus ");
        var builder = new StringBuilder(lower.Length);
        for (int index = 0; index < lower.Length; index++)
        {
            char current = lower[index];
            builder.Append(char.IsLetterOrDigit(current) ? current : ' ');
        }

        string normalized = builder.ToString();
        while (normalized.Contains("  "))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized.Trim();
    }

    private static bool HasGuideIntent(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("take me") ||
               normalized.Contains("guide me") ||
               normalized.Contains("lead me") ||
               normalized.Contains("show me") ||
               normalized.Contains("bring me") ||
               normalized.Contains("go to") ||
               normalized.Contains("where is") ||
               normalized.Contains("walk me") ||
               normalized.Contains("fly me") ||
               normalized.Contains("path to") ||
               normalized.Contains("take us") ||
               normalized.Contains("guide us") ||
               normalized.Contains("lead us");
    }

    private static bool IsGuideCancelCommand(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("stop guiding") ||
               normalized.Contains("stop guide") ||
               normalized.Contains("cancel guide") ||
               normalized.Contains("cancel guiding") ||
               normalized.Contains("never mind") ||
               normalized.Contains("nevermind") ||
               normalized.Contains("dont guide") ||
               normalized.Contains("do not guide");
    }

    private void UpdateVoiceListening(Camera fpsCamera)
    {
        if (voiceBridge == null)
        {
            return;
        }

        EnsureVoiceServerConnection();

        bool inConversation = IsConversationActive();
        bool speaking = voiceBridge.IsSpeaking;
        if (speaking)
        {
            voiceWasSpeaking = true;
            resumeVoiceListeningAt = Time.unscaledTime + PostSpeechListenDelay;
            voiceBridge.SetListening(false);
            return;
        }

        if (voiceWasSpeaking)
        {
            voiceWasSpeaking = false;
            resumeVoiceListeningAt = Time.unscaledTime + PostSpeechListenDelay;
        }

        bool shouldListen = voiceBridge.HasSpeechRecognition && !waiting && Time.unscaledTime >= resumeVoiceListeningAt;
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
        int wakeStartIndex;
        int wakeEndIndex;
        if (!TryFindWakeName(words, out wakeStartIndex, out wakeEndIndex))
        {
            return false;
        }

        if (!HasWakeActivationLead(words, wakeStartIndex))
        {
            return false;
        }

        command = BuildCommandAfterWake(words, wakeEndIndex);
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
        int wakeStartIndex;
        int wakeEndIndex;
        if (!TryFindWakeName(words, out wakeStartIndex, out wakeEndIndex))
        {
            return transcript;
        }

        string command = BuildCommandAfterWake(words, wakeEndIndex);
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

    private static bool TryFindWakeName(string[] words, out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;
        int limit = Mathf.Min(words.Length, 8);
        for (int i = 0; i < limit; i++)
        {
            if (IsWakeName(words[i]))
            {
                startIndex = i;
                endIndex = i;
                return true;
            }

            if (i + 1 < words.Length && IsWakeName(words[i] + words[i + 1]))
            {
                startIndex = i;
                endIndex = i + 1;
                return true;
            }

            if (i + 2 < words.Length && IsWakeName(words[i] + words[i + 1] + words[i + 2]))
            {
                startIndex = i;
                endIndex = i + 2;
                return true;
            }
        }

        return false;
    }

    private static bool HasWakeActivationLead(string[] words, int wakeStartIndex)
    {
        if (wakeStartIndex <= 0)
        {
            return true;
        }

        int leadStart = Mathf.Max(0, wakeStartIndex - 3);
        for (int i = leadStart; i < wakeStartIndex; i++)
        {
            if (IsWakeLead(words[i]))
            {
                return true;
            }
        }

        return wakeStartIndex <= 2;
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

        string compact = word.Replace(" ", "").Replace("-", "");
        if (compact == "rudolf" ||
            compact == "rudolph" ||
            compact == "rodolf" ||
            compact == "rudolfs" ||
            compact == "rudolphs" ||
            compact == "robot" ||
            compact == "robo" ||
            compact == "rudo" ||
            compact == "rudy" ||
            compact == "rudoff" ||
            compact == "rudeoff" ||
            compact == "routeoff" ||
            compact == "rootoff" ||
            compact == "roadoff" ||
            compact == "ruleoff")
        {
            return true;
        }

        if (compact.StartsWith("rudol", StringComparison.Ordinal) ||
            compact.StartsWith("rudop", StringComparison.Ordinal) ||
            compact.StartsWith("rudof", StringComparison.Ordinal) ||
            compact.StartsWith("rodol", StringComparison.Ordinal) ||
            compact.StartsWith("rudolph", StringComparison.Ordinal))
        {
            return true;
        }

        if (compact.Length >= 4 && compact.Length <= 9)
        {
            return WakeEditDistance(compact, "rudolf") <= 2 ||
                   WakeEditDistance(compact, "rudolph") <= 2 ||
                   WakeEditDistance(compact, "rodolf") <= 2;
        }

        return false;
    }

    private static int WakeEditDistance(string a, string b)
    {
        int[,] distances = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Mathf.Min(
                    Mathf.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
    }

    private static string BuildCommandAfterWake(string[] words, int wakeEndIndex)
    {
        int start = wakeEndIndex + 1;
        while (start < words.Length && IsWakeCommandFiller(words[start]))
        {
            start++;
        }

        if (start >= words.Length)
        {
            return string.Empty;
        }

        return string.Join(" ", words, start, words.Length - start).Trim();
    }

    private static bool IsWakeCommandFiller(string word)
    {
        return word == "please" ||
               word == "can" ||
               word == "could" ||
               word == "would" ||
               word == "will" ||
               word == "you";
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
