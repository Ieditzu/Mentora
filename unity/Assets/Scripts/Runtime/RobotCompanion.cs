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
    public const string GuideDebugLinesPrefKey = "RudolfGuideDebugLines";

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
    public static bool GuideDebugLinesEnabled => PlayerPrefs.GetInt(GuideDebugLinesPrefKey, 0) == 1;

    public static void SetGuideDebugLinesEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(GuideDebugLinesPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (_instance != null)
        {
            _instance.ApplyGuideDebugLinePreference();
        }
    }

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
    [SerializeField] private float guideSpeed = 8.0f;
    [SerializeField] private float guideWaitSpeed = 3.5f;
    [SerializeField] private float guideWaitForPlayerDistance = 22f;
    [SerializeField] private float guidePlayerArrivalDistance = 10f;
    [SerializeField] private float guideHoverAboveGround = 0.65f;
    [SerializeField] private float guideWaypointReachDistance = 1.35f;
    [SerializeField] private float guideNavMeshSampleDistance = 14f;
    [SerializeField] private float guideCollisionRadius = 0.48f;
    [SerializeField] private float guideObstacleProbeDistance = 2.6f;
    [SerializeField] private float guideStuckRepathSeconds = 1.25f;
    [SerializeField] private float guideOutlineWidth = 0.045f;
    [SerializeField] private float guideAgentHeight = 1.4f;
    [SerializeField] private float guideFallbackGroundProbeHeight = 36f;
    [SerializeField] private float guideFallbackGroundProbeDistance = 90f;
    [SerializeField] private float guideAStarCellSize = 4f;
    [SerializeField] private float guideAStarSearchMargin = 28f;
    [SerializeField] private int guideAStarMaxGridAxis = 96;
    [SerializeField] private float guideAStarClearanceRadius = 1.35f;
    [SerializeField] private float guideAStarBodyHeight = 2.2f;
    [SerializeField] private float guideAStarMaxStepHeight = 5.75f;
    [SerializeField] private bool guideIgnoreFoliageClearance = true;
    [SerializeField] private float guideThinObstacleMaxFootprint = 0.35f;
    [SerializeField] private float guideSimplifyMaxDistance = 10f;
    [SerializeField] private float guideSimplifyMaxHeightDelta = 1.1f;
    [SerializeField] private float guideSampleMaxHeightAboveProbe = 4.25f;
    [SerializeField] private bool guideDebugEnabled = true;
    [SerializeField] private bool guideDebugDrawLines = true;
    [SerializeField] private float guideDebugLineHeight = 0.08f;

    // ── Internal ─────────────────────────────────────────────────────────────

    private Transform  player;
    private bool       snappedToPlayer;
    private float      orbitAngle;
    private Rigidbody  rb;
    private Vector3    baseLocalScale = Vector3.one * 0.2f;
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
    private string     pendingVoiceTranscript;
    private bool       voiceWasSpeaking;
    private float      resumeVoiceListeningAt;
    private float      lastNoTranscriptPromptAt = -999f;
    private float      nextSakuraCollisionRefresh;
    private int        ignoredSakuraColliderCount = -1;
    private RudolfIslandGuideTarget guideTarget;
    private bool       guideActive;
    private bool       guideWaitingForPlayer;
    private Light      guideLight;
    private float      lastGuideWaitLine = -999f;
    private Vector3    guideDestinationPosition;
    private Vector3    lastGuideProgressPosition;
    private float      guideStuckTimer;
    private GameObject guideAgentProxy;
    private NavMeshAgent guideAgent;
    private bool       guideFallbackActive;
    private int        guideFallbackIndex;
    private readonly List<Vector3> guideFallbackCorners = new List<Vector3>(6);
    private GameObject guideDebugRoot;
    private LineRenderer guideDebugPathLine;
    private LineRenderer guideDebugTargetLine;
    private Material   guideDebugLineMaterial;
    private readonly List<Vector3> guideDebugPathPoints = new List<Vector3>(128);
    private string     guideLastFailureReason = "";
    private float      lastGuideDebugLogAt = -999f;
    private GameObject guideGlowRoot;
    private GameObject boosterRoot;
    private Material   guideOutlineMaterial;
    private Material   guideChamsMaterial;
    private Material   boosterRingMaterial;
    private readonly List<Renderer> guideShellRenderers = new List<Renderer>(16);
    private readonly List<LineRenderer> boosterRings = new List<LineRenderer>(3);
    private static readonly Color GuideAqua = new Color(0.0f, 0.95f, 1f, 1f);
    private static readonly Vector3 GuideHubPosition = new Vector3(55f, 3f, 218.4f);
    private static readonly Vector3 CommunityBridgeEastAnchor = new Vector3(23.5f, 1.0f, 210.4f);
    private static readonly Vector3 CommunityBridgeCrownAnchor = new Vector3(19.5f, 5.6f, 214.4f);
    private static readonly Vector3[] CppGuideAnchorProbes =
    {
        new Vector3(90f, 1f, 218f),
        new Vector3(110f, 3.3f, 218f),
        new Vector3(130f, 6.8f, 218f),
        new Vector3(145f, 6.6f, 216f),
        new Vector3(155f, 1.4f, 218f),
        new Vector3(166f, 3.2f, 236f),
        new Vector3(172f, 2.9f, 233f),
        new Vector3(175f, 4.2f, 239f),
        new Vector3(181f, 5.8f, 242f)
    };
    private static readonly Vector2Int[] GuideAStarDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };
    private const float PostSpeechListenDelay = 1.25f;
    private readonly List<string> conversationHistory = new List<string>(8);

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        baseLocalScale = transform.localScale;
    }

    private void Start()
    {
        BuildNametag();
        BuildBubble();
        ResolvePlayer();
        SetupPhysics();
        SetupGuideAgent();
        RefreshIgnoredSakuraCollisions(true);
        SetupGuideHighlight();
        SetupGuideGlowVisuals();
        SetupBoosterRings();
        ApplyGuideDebugLinePreference();
        SetupGuideDebugVisuals();
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
            voiceBridge.VoiceUtteranceCapturedForServer -= OnVoiceUtteranceCapturedForServer;
            voiceBridge.TranscriptionFailedWithoutText -= OnVoiceNoTranscript;
        }
        if (guideAgentProxy != null)
        {
            Destroy(guideAgentProxy);
            guideAgentProxy = null;
            guideAgent = null;
        }
        if (guideDebugRoot != null)
        {
            Destroy(guideDebugRoot);
            guideDebugRoot = null;
        }
        if (guideDebugLineMaterial != null)
        {
            Destroy(guideDebugLineMaterial);
            guideDebugLineMaterial = null;
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
            targetPos = GetGuideGroundTarget();
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
                float pitch = Mathf.Sin(Time.time * 6.0f) * 1.25f;
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
        MaintainRootScale();
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
        col.isTrigger = true;

        var mat = new PhysicMaterial("RudolfBounce");
        mat.bounciness      = 0f;
        mat.dynamicFriction = 0.25f;
        mat.staticFriction  = 0.25f;
        mat.bounceCombine   = PhysicMaterialCombine.Minimum;
        mat.frictionCombine = PhysicMaterialCombine.Minimum;
        col.material = mat;

        // Kinematic — we drive position manually, no physics fighting the movement
        rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity    = false;
        rb.isKinematic   = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.detectCollisions = false;

        SetRudolfPhysicalCollisions(false);
    }

    private void SetRudolfPhysicalCollisions(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enabled;
            }
        }

        if (rb != null)
        {
            rb.detectCollisions = enabled;
        }
    }

    private void SetupGuideAgent()
    {
        if (guideAgent != null)
        {
            return;
        }

        guideAgentProxy = new GameObject("RudolfGuideAgent");
        guideAgentProxy.hideFlags = HideFlags.HideInHierarchy;
        guideAgentProxy.transform.position = transform.position;
        guideAgentProxy.SetActive(false);
        DontDestroyOnLoad(guideAgentProxy);

        guideAgent = guideAgentProxy.AddComponent<NavMeshAgent>();
        guideAgent.enabled = false;
        guideAgent.radius = Mathf.Max(0.18f, guideCollisionRadius);
        guideAgent.height = Mathf.Max(0.6f, guideAgentHeight);
        guideAgent.speed = guideSpeed;
        guideAgent.acceleration = 18f;
        guideAgent.angularSpeed = 720f;
        guideAgent.stoppingDistance = Mathf.Max(0.35f, guideWaypointReachDistance * 0.6f);
        guideAgent.autoBraking = true;
        guideAgent.updateRotation = false;
        guideAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    private void OnCollisionEnter(Collision col)
    {
        if (IsGuideActive())
        {
            return;
        }

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
            if (IsGuideActive())
            {
                yield break;
            }

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
        voiceBridge.VoiceUtteranceCapturedForServer += OnVoiceUtteranceCapturedForServer;
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
        if (waiting || !IsRudolfVoiceInputAllowed())
        {
            return;
        }

        bool wasConversationActive = IsConversationActive();
        if (!wasConversationActive && Time.time - lastSpoke < 1f)
        {
            return;
        }

        string command = wasConversationActive
            ? StripWakePhraseIfPresent(transcript).Trim()
            : (transcript ?? string.Empty).Trim();
        if (ShouldDiscardVoiceTranscript(command, wasConversationActive))
        {
            return;
        }
        if (TryHandleConversationExit(command, wasConversationActive))
        {
            return;
        }

        lastSpoke = Time.time;
        if (wasConversationActive)
        {
            conversationActive = true;
            lastConversationHeard = Time.time;
        }

        waiting = true;
        StartCoroutine(RequestVoiceReply(command, wasConversationActive));
    }

    private static bool ShouldDiscardVoiceTranscript(string transcript, bool conversationIsActive)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return true;
        }

        string normalized = NormalizeWakeText(transcript);
        if (normalized.Length < (conversationIsActive ? 2 : 4))
        {
            return true;
        }

        string[] words = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return true;
        }

        if (!conversationIsActive && words.Length == 1 && normalized.Length < 7)
        {
            return true;
        }

        return normalized == "um" ||
               normalized == "uh" ||
               normalized == "ah" ||
               normalized == "hmm" ||
               normalized == "mmm" ||
               normalized == "test";
    }

    private void OnVoiceNoTranscript(int byteCount, float vadPeak, float audioPeak, float appliedGain)
    {
        if (waiting || voiceBridge == null || voiceBridge.IsSpeaking || !IsRudolfVoiceInputAllowed())
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

        // No-transcript clips are noise for the new flow. Do not open a
        // conversation from them; the server gate only receives real text.
    }

    private IEnumerator RequestVoiceReply(string transcript)
    {
        return RequestVoiceReply(transcript, IsConversationActive());
    }

    private IEnumerator RequestVoiceReply(string transcript, bool wasConversationActive)
    {
        yield return null;

        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            waiting = false;
            ShowLine("I heard you, but I'm not connected to the mentor server.");
            yield break;
        }

        string context = BuildVoiceRequestContext(wasConversationActive);

        pendingVoiceTranscript = transcript;
        yield return GameClient.Instance.SendPacket(new CompanionVoiceTextPacket(transcript, context));
    }

    private void OnVoiceUtteranceCapturedForServer(byte[] pcm16, int sampleRate, float peakLevel)
    {
        if (waiting || !IsRudolfVoiceInputAllowed())
        {
            return;
        }

        bool wasConversationActive = IsConversationActive();
        if (!wasConversationActive && Time.time - lastSpoke < 1f)
        {
            return;
        }

        lastSpoke = Time.time;
        if (wasConversationActive)
        {
            conversationActive = true;
            lastConversationHeard = Time.time;
        }

        waiting = true;
        StartCoroutine(RequestVoiceAudioReply(pcm16, sampleRate, peakLevel, wasConversationActive));
    }

    private IEnumerator RequestVoiceAudioReply(byte[] pcm16, int sampleRate, float peakLevel, bool wasConversationActive)
    {
        yield return null;

        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            waiting = false;
            ShowLine("I heard you, but I'm not connected to the mentor server.");
            yield break;
        }

        string context = BuildVoiceRequestContext(wasConversationActive) +
            "\ntranscription_provider=groq_whisper_server" +
            "\nvad_peak=" + peakLevel.ToString("0.000");

        pendingVoiceTranscript = null;
        yield return GameClient.Instance.SendPacket(new CompanionVoiceAudioPacket(sampleRate, pcm16, context));
    }

    private string BuildVoiceRequestContext(bool wasConversationActive)
    {
        string context = IsInMultiplayerSession()
            ? "multiplayer_player_looked_at_robot"
            : "singleplayer_passive_robot_mic";
        context += "\nconversation_active=" + (wasConversationActive ? "true" : "false");
        string history = BuildConversationHistoryContext();
        if (!string.IsNullOrWhiteSpace(history))
        {
            context += "\nRecent conversation:\n" + history;
        }

        return context;
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
                string transcript = string.IsNullOrWhiteSpace(pendingVoiceTranscript)
                    ? r.SourceTranscript
                    : pendingVoiceTranscript;
                pendingVoiceTranscript = null;

                if (!string.IsNullOrWhiteSpace(transcript) && TryHandleConversationExit(transcript, true))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(r.Line) ||
                    string.Equals(r.Emotion, "ignore", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsConversationActive())
                    {
                        conversationActive = false;
                    }
                    return;
                }

                if (!string.IsNullOrWhiteSpace(transcript) && TryHandleGuideCommand(transcript))
                {
                    return;
                }

                conversationActive = true;
                lastConversationHeard = Time.time;
                AddConversationTurn("Student", transcript);
                AddConversationTurn("Rudolf", r.Line);
                ShowLine(r.Line);
                if (voiceBridge != null)
                    voiceBridge.Speak(r.Line, r.Emotion);
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

    private void SetupGuideDebugVisuals()
    {
        if (!ShouldDrawGuideDebugLines() || guideDebugRoot != null)
        {
            return;
        }

        guideDebugRoot = new GameObject("RudolfGuideDebugLines");
        guideDebugRoot.hideFlags = HideFlags.DontSave;
        DontDestroyOnLoad(guideDebugRoot);

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
        {
            lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        guideDebugLineMaterial = lineShader != null ? new Material(lineShader) : null;
        if (guideDebugLineMaterial != null)
        {
            guideDebugLineMaterial.name = "RudolfGuideDebugLineRuntime";
            guideDebugLineMaterial.color = GuideAqua;
        }

        guideDebugPathLine = CreateGuideDebugLine("Path", GuideAqua, 0.11f);
        guideDebugTargetLine = CreateGuideDebugLine("Target", new Color(1f, 0.78f, 0.12f, 1f), 0.065f);
        SetGuideDebugLinesActive(false);
    }

    private LineRenderer CreateGuideDebugLine(string lineName, Color color, float width)
    {
        GameObject lineObject = new GameObject("RudolfGuideDebug_" + lineName);
        lineObject.hideFlags = HideFlags.DontSave;
        lineObject.transform.SetParent(guideDebugRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 0;
        line.sharedMaterial = guideDebugLineMaterial;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private void SetGuideDebugLinesActive(bool active)
    {
        if (!ShouldDrawGuideDebugLines())
        {
            return;
        }

        if (guideDebugRoot == null)
        {
            SetupGuideDebugVisuals();
        }

        if (guideDebugPathLine != null)
        {
            guideDebugPathLine.enabled = active && guideDebugPathLine.positionCount > 1;
        }

        if (guideDebugTargetLine != null)
        {
            guideDebugTargetLine.enabled = active && guideDebugTargetLine.positionCount > 1;
        }
    }

    private void ClearGuideDebugLines()
    {
        if (guideDebugPathLine != null)
        {
            guideDebugPathLine.positionCount = 0;
            guideDebugPathLine.enabled = false;
        }

        if (guideDebugTargetLine != null)
        {
            guideDebugTargetLine.positionCount = 0;
            guideDebugTargetLine.enabled = false;
        }
    }

    private void UpdateGuideDebugPathLine(IList<Vector3> points, Color color)
    {
        if (!ShouldDrawGuideDebugLines() || points == null || points.Count < 2)
        {
            return;
        }

        if (guideDebugDrawLines)
        {
            if (guideDebugPathLine == null)
            {
                SetupGuideDebugVisuals();
            }

            if (guideDebugPathLine != null)
            {
                guideDebugPathLine.positionCount = points.Count;
                guideDebugPathLine.startColor = color;
                guideDebugPathLine.endColor = color;
                for (int i = 0; i < points.Count; i++)
                {
                    guideDebugPathLine.SetPosition(i, points[i] + Vector3.up * guideDebugLineHeight);
                }

                guideDebugPathLine.enabled = true;
            }
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Debug.DrawLine(points[i] + Vector3.up * guideDebugLineHeight, points[i + 1] + Vector3.up * guideDebugLineHeight, color, 8f, false);
        }
    }

    private void UpdateGuideDebugPathLine(Vector3 start, List<Vector3> corners, Color color)
    {
        guideDebugPathPoints.Clear();
        guideDebugPathPoints.Add(start);
        if (corners != null)
        {
            guideDebugPathPoints.AddRange(corners);
        }

        UpdateGuideDebugPathLine(guideDebugPathPoints, color);
    }

    private void UpdateGuideDebugPathLine(Vector3 start, Vector3 end, Color color)
    {
        guideDebugPathPoints.Clear();
        guideDebugPathPoints.Add(start);
        guideDebugPathPoints.Add(end);
        UpdateGuideDebugPathLine(guideDebugPathPoints, color);
    }

    private void UpdateGuideDebugTargetLine(Vector3 target, Color color)
    {
        if (!ShouldDrawGuideDebugLines())
        {
            return;
        }

        Vector3 start = transform.position + Vector3.up * guideDebugLineHeight;
        Vector3 end = target + Vector3.up * guideDebugLineHeight;
        Debug.DrawLine(start, end, color, 0f, false);

        if (!guideDebugDrawLines)
        {
            return;
        }

        if (guideDebugTargetLine == null)
        {
            SetupGuideDebugVisuals();
        }

        if (guideDebugTargetLine == null)
        {
            return;
        }

        guideDebugTargetLine.positionCount = 2;
        guideDebugTargetLine.SetPosition(0, start);
        guideDebugTargetLine.SetPosition(1, end);
        guideDebugTargetLine.startColor = color;
        guideDebugTargetLine.endColor = color;
        guideDebugTargetLine.enabled = true;
    }

    private void GuideLog(string message)
    {
        if (guideDebugEnabled)
        {
            Debug.Log("[RudolfGuide] " + message);
        }
    }

    private void GuideWarn(string message)
    {
        if (guideDebugEnabled)
        {
            Debug.LogWarning("[RudolfGuide] " + message);
        }
    }

    private void ApplyGuideDebugLinePreference()
    {
        guideDebugDrawLines = GuideDebugLinesEnabled;
        if (!guideDebugDrawLines)
        {
            ClearGuideDebugLines();
            return;
        }

        SetupGuideDebugVisuals();
    }

    private bool ShouldDrawGuideDebugLines()
    {
        return guideDebugEnabled && guideDebugDrawLines;
    }

    private static string FormatGuideVector(Vector3 value)
    {
        return "(" + value.x.ToString("0.0") + ", " + value.y.ToString("0.0") + ", " + value.z.ToString("0.0") + ")";
    }

    private static string FormatGuideBounds(Bounds bounds)
    {
        return "center=" + FormatGuideVector(bounds.center) + " size=" + FormatGuideVector(bounds.size);
    }

    private static string GetGuideColliderPath(Collider collider)
    {
        return collider != null ? GetGuideTransformPath(collider.transform) : "<null>";
    }

    private static string GetGuideTransformPath(Transform source)
    {
        if (source == null)
        {
            return "<null>";
        }

        StringBuilder builder = new StringBuilder(source.name);
        Transform current = source.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
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

    private void MaintainRootScale()
    {
        if ((transform.localScale - baseLocalScale).sqrMagnitude > 0.0001f)
        {
            transform.localScale = baseLocalScale;
        }
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
        lastGuideProgressPosition = transform.position;
        guideLastFailureReason = "";
        ClearGuideDebugLines();
        GuideLog("Start target=" + target.DisplayName +
                 " robot=" + FormatGuideVector(transform.position) +
                 " target=" + FormatGuideVector(target.transform.position) +
                 " player=" + (player != null ? FormatGuideVector(player.position) : "<none>"));
        if (!BuildGuidePath(target))
        {
            guideActive = false;
            StopGuideAgent();
            StopFallbackGuide();
            SetGuideHighlight(false);
            UpdateGuideDebugPathLine(transform.position, target.transform.position, Color.red);
            GuideWarn("Path failed target=" + target.DisplayName +
                      " robot=" + FormatGuideVector(transform.position) +
                      " target=" + FormatGuideVector(target.transform.position) +
                      " reason=" + guideLastFailureReason);
            string noPathLine = "I can't find a safe ground path to " + target.DisplayName + " yet.";
            ShowLine(noPathLine, 4f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(noPathLine, "concerned");
            }
            return;
        }
        conversationActive = true;
        lastConversationHeard = Time.time;
        SetGuideHighlight(true);

        string line = "Follow me — I'll guide you to " + target.DisplayName + ".";
        AddConversationTurn("Student", "Guide me to " + target.DisplayName + ".");
        AddConversationTurn("Rudolf", line);
        ShowLine(line, 5f);
        if (voiceBridge != null)
        {
            voiceBridge.Speak(line, "excited");
        }
    }

    private void StopGuide(string line)
    {
        guideActive = false;
        guideTarget = null;
        guideWaitingForPlayer = false;
        guideStuckTimer = 0f;
        StopGuideAgent();
        StopFallbackGuide();
        SetGuideHighlight(false);
        ClearGuideDebugLines();

        if (!string.IsNullOrWhiteSpace(line))
        {
            AddConversationTurn("Rudolf", line);
            ShowLine(line, 4f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(line, "excited");
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
        if (IsGuideAgentRunning())
        {
            ConfigureGuideAgentSpeed(speed);
            Vector3 navTarget = GetGuideGroundTarget();
            UpdateGuideDebugTargetLine(navTarget, guideWaitingForPlayer ? Color.yellow : GuideAqua);
            return navTarget;
        }

        if (guideFallbackActive)
        {
            Vector3 fallbackTarget = GetGuideGroundTarget();
            UpdateGuideDebugTargetLine(fallbackTarget, guideWaitingForPlayer ? Color.yellow : GuideAqua);
            Vector3 next = Vector3.MoveTowards(transform.position, fallbackTarget, speed * Time.deltaTime);
            return SnapFallbackGuidePosition(next, fallbackTarget.y - guideHoverAboveGround);
        }

        return transform.position;
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
        UpdateGuideDebugTargetLine(PeekGuideGroundTarget(), guideWaitingForPlayer ? Color.yellow : GuideAqua);

        if (guideDebugEnabled && Time.time - lastGuideDebugLogAt >= 2f)
        {
            lastGuideDebugLogAt = Time.time;
            GuideLog("Progress mode=" + GetGuideModeLabel() +
                     " waiting=" + guideWaitingForPlayer +
                     " robot=" + FormatGuideVector(transform.position) +
                     " target=" + FormatGuideVector(PeekGuideGroundTarget()) +
                     " remaining=" + GetGuideRemainingDistance().ToString("0.0") +
                     " fallbackIndex=" + guideFallbackIndex + "/" + guideFallbackCorners.Count);
        }

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
                GuideWarn("Stuck; rebuilding path. mode=" + GetGuideModeLabel() +
                          " robot=" + FormatGuideVector(transform.position) +
                          " target=" + FormatGuideVector(PeekGuideGroundTarget()) +
                          " remaining=" + GetGuideRemainingDistance().ToString("0.0") +
                          " fallbackIndex=" + guideFallbackIndex + "/" + guideFallbackCorners.Count +
                          " lastFailure=" + guideLastFailureReason);
                BuildGuidePath(guideTarget);
            }
        }

        float robotDistance = GetGuideRemainingDistance();
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
                voiceBridge.Speak(arrivedLine, "happy");
            }
        }
    }

    private void SetGuideHighlight(bool active)
    {
        if (guideLight != null)
        {
            guideLight.enabled = active;
            if (!active)
            {
                guideLight.intensity = 0f;
            }
        }

        SetGuideShellsActive(active);
        if (!active)
        {
            if (guideOutlineMaterial != null)
            {
                guideOutlineMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, 0f));
            }

            if (guideChamsMaterial != null)
            {
                guideChamsMaterial.SetColor("_Color", new Color(GuideAqua.r, GuideAqua.g, GuideAqua.b, 0f));
            }
        }
    }

    private bool BuildGuidePath(RudolfIslandGuideTarget target)
    {
        if (target == null)
        {
            guideLastFailureReason = "target was null";
            StopGuideAgent();
            StopFallbackGuide();
            return false;
        }

        guideLastFailureReason = "";
        guideDestinationPosition = target.transform.position;
        Vector3 startSource = transform.position;
        NavMeshHit startHit;
        NavMeshHit targetHit;
        bool startSampled = NavMesh.SamplePosition(startSource, out startHit, guideNavMeshSampleDistance, NavMesh.AllAreas);
        bool targetSampled = NavMesh.SamplePosition(target.transform.position, out targetHit, guideNavMeshSampleDistance, NavMesh.AllAreas);
        if (!startSampled || !targetSampled)
        {
            GuideLog("NavMesh sample failed startSampled=" + startSampled +
                     " targetSampled=" + targetSampled +
                     " start=" + FormatGuideVector(startSource) +
                     " target=" + FormatGuideVector(target.transform.position) +
                     " sampleDistance=" + guideNavMeshSampleDistance.ToString("0.0") +
                     "; using fallback A*.");
            return BuildFallbackGuidePath(target);
        }

        guideDestinationPosition = targetHit.position;
        NavMeshPath path = new NavMeshPath();
        bool navPathCalculated = NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path);
        if (!navPathCalculated || path.status != NavMeshPathStatus.PathComplete)
        {
            GuideLog("NavMesh path incomplete calculated=" + navPathCalculated +
                     " status=" + path.status +
                     " startHit=" + FormatGuideVector(startHit.position) +
                     " targetHit=" + FormatGuideVector(targetHit.position) +
                     " corners=" + (path.corners != null ? path.corners.Length : 0) +
                     "; using fallback A*.");
            return BuildFallbackGuidePath(target);
        }

        if (guideAgent == null)
        {
            SetupGuideAgent();
        }

        guideAgentProxy.transform.position = startHit.position;
        guideAgentProxy.SetActive(true);
        guideAgent.enabled = true;
        if (!guideAgent.Warp(startHit.position))
        {
            GuideLog("NavMesh agent warp failed at " + FormatGuideVector(startHit.position) + "; using fallback A*.");
            return BuildFallbackGuidePath(target);
        }

        ConfigureGuideAgentSpeed(guideSpeed);
        if (!guideAgent.SetDestination(guideDestinationPosition))
        {
            GuideLog("NavMesh SetDestination failed destination=" + FormatGuideVector(guideDestinationPosition) + "; using fallback A*.");
            return BuildFallbackGuidePath(target);
        }

        StopFallbackGuide();
        UpdateGuideDebugPathLine(path.corners, GuideAqua);
        GuideLog("NavMesh path OK target=" + target.DisplayName +
                 " start=" + FormatGuideVector(startHit.position) +
                 " destination=" + FormatGuideVector(guideDestinationPosition) +
                 " corners=" + (path.corners != null ? path.corners.Length : 0));
        return true;
    }

    private bool BuildFallbackGuidePath(RudolfIslandGuideTarget target)
    {
        StopGuideAgent();
        guideFallbackCorners.Clear();
        guideFallbackIndex = 0;
        guideFallbackActive = false;

        if (target == null)
        {
            guideLastFailureReason = "fallback target was null";
            return false;
        }

        Vector3 destinationProbe = target.transform.position;
        if (!TrySampleGuideWalkablePoint(destinationProbe, out Vector3 destination, out string destinationSampleReason))
        {
            GuideWarn("Destination ground sample failed target=" + target.DisplayName +
                      " probe=" + FormatGuideVector(destinationProbe) +
                      " reason=" + destinationSampleReason +
                      "; snapping as emergency fallback.");
            destination = SnapFallbackGuidePosition(destinationProbe, destinationProbe.y);
        }

        Vector3 startProbe = transform.position - Vector3.up * guideHoverAboveGround;
        if (!TrySampleGuideWalkablePoint(startProbe, out Vector3 start, out string startSampleReason))
        {
            GuideWarn("Start ground sample failed probe=" + FormatGuideVector(startProbe) +
                      " reason=" + startSampleReason +
                      "; snapping as emergency fallback.");
            start = SnapFallbackGuidePosition(startProbe, startProbe.y);
        }

        guideDestinationPosition = destination - Vector3.up * guideHoverAboveGround;
        GuideLog("Fallback A* build target=" + target.DisplayName +
                 " start=" + FormatGuideVector(start) +
                 " destination=" + FormatGuideVector(destination) +
                 " cell=" + GetGuideEffectiveAStarCellSize().ToString("0.0") +
                 " margin=" + guideAStarSearchMargin.ToString("0.0") +
                 " clearance=" + guideAStarClearanceRadius.ToString("0.0"));

        if (!TryBuildAStarGuidePath(start, destination, guideFallbackCorners, out string directFailureReason))
        {
            GuideWarn("Direct fallback A* failed target=" + target.DisplayName + " reason=" + directFailureReason);
            List<Vector3> firstLeg = new List<Vector3>(64);
            List<Vector3> secondLeg = new List<Vector3>(64);
            Vector3 hub = GuideHubPosition;
            if (!TrySampleGuideWalkablePoint(hub, out hub, out string hubSampleReason))
            {
                GuideWarn("Hub ground sample failed hubProbe=" + FormatGuideVector(GuideHubPosition) +
                          " reason=" + hubSampleReason +
                          "; snapping hub.");
                hub = SnapFallbackGuidePosition(hub, hub.y);
            }

            bool firstLegOk = TryBuildAStarGuidePath(start, hub, firstLeg, out string firstLegFailureReason);
            bool secondLegOk = TryBuildAStarGuidePath(hub, destination, secondLeg, out string secondLegFailureReason);
            if (!firstLegOk || !secondLegOk)
            {
                guideFallbackCorners.Clear();
                string hubFailureReason = "direct=(" + directFailureReason + ") viaHub firstOk=" + firstLegOk +
                                          " first=(" + firstLegFailureReason + ") secondOk=" + secondLegOk +
                                          " second=(" + secondLegFailureReason + ")";
                if (TryBuildIslandAnchorFallbackPath(target, start, destination, guideFallbackCorners, out string anchorRouteReason))
                {
                    GuideLog("Fallback A* using island anchors target=" + target.DisplayName + " " + anchorRouteReason);
                }
                else
                {
                    guideLastFailureReason = hubFailureReason + " anchors=(" + anchorRouteReason + ")";
                    UpdateGuideDebugPathLine(start, destination, Color.red);
                    GuideWarn("Fallback A* failed target=" + target.DisplayName + " reason=" + guideLastFailureReason);
                    return false;
                }
            }
            else
            {
                GuideLog("Fallback A* using hub " + FormatGuideVector(hub) +
                         " firstLegCorners=" + firstLeg.Count +
                         " secondLegCorners=" + secondLeg.Count);
                guideFallbackCorners.AddRange(firstLeg);
                for (int i = 0; i < secondLeg.Count; i++)
                {
                    if (guideFallbackCorners.Count == 0 ||
                        HorizontalDistance(guideFallbackCorners[guideFallbackCorners.Count - 1], secondLeg[i]) > guideWaypointReachDistance)
                    {
                        guideFallbackCorners.Add(secondLeg[i]);
                    }
                }
            }
        }

        SimplifyGuideFallbackCorners(start);
        guideFallbackActive = guideFallbackCorners.Count > 0;
        if (!guideFallbackActive)
        {
            guideLastFailureReason = "fallback path contained no corners after simplification";
            GuideWarn("Fallback A* failed target=" + target.DisplayName + " reason=" + guideLastFailureReason);
            UpdateGuideDebugPathLine(start, destination, Color.red);
            return false;
        }

        UpdateGuideDebugPathLine(start, guideFallbackCorners, GuideAqua);
        GuideLog("Fallback A* path OK target=" + target.DisplayName +
                 " corners=" + guideFallbackCorners.Count +
                 " firstCorner=" + FormatGuideVector(guideFallbackCorners[0]) +
                 " finalCorner=" + FormatGuideVector(guideFallbackCorners[guideFallbackCorners.Count - 1]) +
                 " remaining=" + GetFallbackRemainingDistance().ToString("0.0"));
        return guideFallbackActive;
    }

    private bool TryBuildIslandAnchorFallbackPath(
        RudolfIslandGuideTarget target,
        Vector3 start,
        Vector3 destination,
        List<Vector3> output,
        out string routeReason)
    {
        output.Clear();
        routeReason = "";
        if (target == null)
        {
            routeReason = "target was null";
            return false;
        }

        if (target.Island == RudolfIslandGuideTarget.IslandId.Community)
        {
            return TryBuildGuidePathThroughAnchors(
                start,
                destination,
                output,
                out routeReason,
                GuideHubPosition,
                CommunityBridgeEastAnchor,
                CommunityBridgeCrownAnchor);
        }

        if (target.Island == RudolfIslandGuideTarget.IslandId.Cpp)
        {
            return TryBuildCppGuideAnchorFallbackPath(start, destination, output, out routeReason);
        }

        routeReason = "no configured anchors for target";
        return false;
    }

    private bool TryBuildCppGuideAnchorFallbackPath(
        Vector3 start,
        Vector3 destination,
        List<Vector3> output,
        out string routeReason)
    {
        output.Clear();
        routeReason = "";
        List<Vector3> sampledAnchors = new List<Vector3>(CppGuideAnchorProbes.Length);
        List<Vector3> selectedAnchorProbes = new List<Vector3>(CppGuideAnchorProbes.Length);
        int nearestAnchorIndex = -1;
        float nearestAnchorDistance = float.PositiveInfinity;

        for (int i = 0; i < CppGuideAnchorProbes.Length; i++)
        {
            Vector3 anchorProbe = CppGuideAnchorProbes[i];
            if (!TrySampleGuideWalkablePoint(anchorProbe, out Vector3 sampledAnchor, out string sampleReason))
            {
                GuideWarn("C++ anchor sample failed index=" + i +
                          " probe=" + FormatGuideVector(anchorProbe) +
                          " reason=" + sampleReason +
                          "; using configured anchor height.");
                sampledAnchor = anchorProbe + Vector3.up * guideHoverAboveGround;
            }

            sampledAnchors.Add(sampledAnchor);
            float distance = HorizontalDistance(start, sampledAnchor);
            if (distance < nearestAnchorDistance)
            {
                nearestAnchorDistance = distance;
                nearestAnchorIndex = i;
            }
        }

        if (nearestAnchorIndex < 0)
        {
            routeReason = "cpp anchors unavailable";
            return false;
        }

        for (int i = nearestAnchorIndex; i < CppGuideAnchorProbes.Length; i++)
        {
            selectedAnchorProbes.Add(CppGuideAnchorProbes[i]);
        }

        if (TryBuildGuidePathThroughAnchors(
                start,
                destination,
                output,
                out string strictRouteReason,
                selectedAnchorProbes.ToArray()))
        {
            routeReason = "cppStrict " + strictRouteReason + " startAnchor=" + nearestAnchorIndex;
            return true;
        }

        output.Clear();
        for (int i = nearestAnchorIndex; i < sampledAnchors.Count; i++)
        {
            Vector3 anchor = sampledAnchors[i];
            if (output.Count == 0 ||
                HorizontalDistance(output[output.Count - 1], anchor) > guideWaypointReachDistance)
            {
                output.Add(anchor);
            }
        }

        if (output.Count == 0 ||
            HorizontalDistance(output[output.Count - 1], destination) > guideWaypointReachDistance)
        {
            output.Add(destination);
        }

        routeReason = "cppLoose strictFailed=(" + strictRouteReason + ") startAnchor=" + nearestAnchorIndex +
                      " anchors=" + (sampledAnchors.Count - nearestAnchorIndex) +
                      " corners=" + output.Count;
        return output.Count > 0;
    }

    private bool TryBuildGuidePathThroughAnchors(
        Vector3 start,
        Vector3 destination,
        List<Vector3> output,
        out string routeReason,
        params Vector3[] anchorProbes)
    {
        output.Clear();
        routeReason = "";
        List<Vector3> routePoints = new List<Vector3>(anchorProbes.Length + 2);
        routePoints.Add(start);
        for (int i = 0; i < anchorProbes.Length; i++)
        {
            Vector3 anchorProbe = anchorProbes[i];
            if (!TrySampleGuideWalkablePoint(anchorProbe, out Vector3 anchor, out string sampleReason))
            {
                routeReason = "anchorSampleFailed index=" + i +
                              " probe=" + FormatGuideVector(anchorProbe) +
                              " reason=" + sampleReason;
                return false;
            }

            if (HorizontalDistance(routePoints[routePoints.Count - 1], anchor) > guideWaypointReachDistance)
            {
                routePoints.Add(anchor);
            }
        }

        routePoints.Add(destination);

        for (int i = 0; i < routePoints.Count - 1; i++)
        {
            List<Vector3> leg = new List<Vector3>(64);
            if (!TryBuildAStarGuidePath(routePoints[i], routePoints[i + 1], leg, out string legFailureReason))
            {
                routeReason = "legFailed index=" + i +
                              " from=" + FormatGuideVector(routePoints[i]) +
                              " to=" + FormatGuideVector(routePoints[i + 1]) +
                              " reason=" + legFailureReason;
                output.Clear();
                return false;
            }

            for (int corner = 0; corner < leg.Count; corner++)
            {
                if (output.Count == 0 ||
                    HorizontalDistance(output[output.Count - 1], leg[corner]) > guideWaypointReachDistance)
                {
                    output.Add(leg[corner]);
                }
            }
        }

        routeReason = "anchors=" + anchorProbes.Length + " corners=" + output.Count;
        return output.Count > 0;
    }

    private bool TryBuildAStarGuidePath(Vector3 start, Vector3 destination, List<Vector3> output, out string failureReason)
    {
        output.Clear();
        failureReason = "";

        float cellSize = GetGuideEffectiveAStarCellSize();
        float minX = Mathf.Min(start.x, destination.x) - guideAStarSearchMargin;
        float maxX = Mathf.Max(start.x, destination.x) + guideAStarSearchMargin;
        float minZ = Mathf.Min(start.z, destination.z) - guideAStarSearchMargin;
        float maxZ = Mathf.Max(start.z, destination.z) + guideAStarSearchMargin;
        float spanX = Mathf.Max(cellSize, maxX - minX);
        float spanZ = Mathf.Max(cellSize, maxZ - minZ);
        int maxAxis = Mathf.Clamp(guideAStarMaxGridAxis, 24, 160);
        float requiredCell = Mathf.Max(spanX, spanZ) / Mathf.Max(1, maxAxis - 1);
        cellSize = Mathf.Max(cellSize, requiredCell);

        int width = Mathf.Clamp(Mathf.CeilToInt(spanX / cellSize) + 1, 2, maxAxis);
        int height = Mathf.Clamp(Mathf.CeilToInt(spanZ / cellSize) + 1, 2, maxAxis);

        bool[,] walkable = new bool[width, height];
        Vector3[,] points = new Vector3[width, height];
        float[,] gScore = new float[width, height];
        float[,] fScore = new float[width, height];
        bool[,] closed = new bool[width, height];
        bool[,] inOpen = new bool[width, height];
        Vector2Int[,] parent = new Vector2Int[width, height];
        int walkableCount = 0;
        int noHitCount = 0;
        int noWalkableGroundCount = 0;
        int clearanceBlockedCount = 0;
        string lastSampleFailure = "";

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                gScore[x, z] = float.PositiveInfinity;
                fScore[x, z] = float.PositiveInfinity;
                parent[x, z] = new Vector2Int(-1, -1);

                Vector3 probe = new Vector3(minX + x * cellSize, Mathf.Max(start.y, destination.y), minZ + z * cellSize);
                if (TrySampleGuideWalkablePoint(probe, out Vector3 snapped, out string sampleFailure))
                {
                    walkable[x, z] = true;
                    points[x, z] = snapped;
                    walkableCount++;
                }
                else
                {
                    lastSampleFailure = sampleFailure;
                    if (sampleFailure.StartsWith("no ray hits", StringComparison.Ordinal))
                    {
                        noHitCount++;
                    }
                    else if (sampleFailure.Contains("clearance"))
                    {
                        clearanceBlockedCount++;
                    }
                    else
                    {
                        noWalkableGroundCount++;
                    }
                }
            }
        }

        bool startNodeFound = TryFindNearestWalkableNode(start, walkable, points, out Vector2Int startNode);
        bool destinationNodeFound = TryFindNearestWalkableNode(destination, walkable, points, out Vector2Int destinationNode);
        if (!startNodeFound || !destinationNodeFound)
        {
            failureReason = "nearest walkable node failed startFound=" + startNodeFound +
                            " destinationFound=" + destinationNodeFound +
                            " grid=" + width + "x" + height +
                            " cell=" + cellSize.ToString("0.00") +
                            " walkable=" + walkableCount +
                            " noHit=" + noHitCount +
                            " noGround=" + noWalkableGroundCount +
                            " clearanceBlocked=" + clearanceBlockedCount +
                            " start=" + FormatGuideVector(start) +
                            " destination=" + FormatGuideVector(destination) +
                            " lastSampleFailure=" + lastSampleFailure;
            return false;
        }

        List<Vector2Int> open = new List<Vector2Int>(width * height);
        gScore[startNode.x, startNode.y] = 0f;
        fScore[startNode.x, startNode.y] = HorizontalDistance(points[startNode.x, startNode.y], points[destinationNode.x, destinationNode.y]);
        open.Add(startNode);
        inOpen[startNode.x, startNode.y] = true;
        int closedCount = 0;
        int diagonalBlockedCount = 0;
        int stepBlockedCount = 0;
        int segmentBlockedCount = 0;
        string lastSegmentFailure = "";
        bool hasLastBlockedSegment = false;
        Vector3 lastBlockedSegmentStart = Vector3.zero;
        Vector3 lastBlockedSegmentEnd = Vector3.zero;

        while (open.Count > 0)
        {
            int bestOpenIndex = 0;
            Vector2Int current = open[0];
            float bestScore = fScore[current.x, current.y];
            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                float candidateScore = fScore[candidate.x, candidate.y];
                if (candidateScore < bestScore)
                {
                    bestScore = candidateScore;
                    bestOpenIndex = i;
                    current = candidate;
                }
            }

            open.RemoveAt(bestOpenIndex);
            inOpen[current.x, current.y] = false;
            closed[current.x, current.y] = true;
            closedCount++;

            if (current == destinationNode)
            {
                ReconstructGuideAStarPath(current, parent, points, output);
                GuideLog("A* path OK grid=" + width + "x" + height +
                         " cell=" + cellSize.ToString("0.00") +
                         " walkable=" + walkableCount +
                         " closed=" + closedCount +
                         " corners=" + output.Count +
                         " startNode=" + startNode +
                         " destinationNode=" + destinationNode);
                return output.Count > 0;
            }

            for (int i = 0; i < GuideAStarDirections.Length; i++)
            {
                Vector2Int direction = GuideAStarDirections[i];
                int nx = current.x + direction.x;
                int nz = current.y + direction.y;
                if (nx < 0 || nz < 0 || nx >= width || nz >= height ||
                    !walkable[nx, nz] ||
                    closed[nx, nz])
                {
                    continue;
                }

                if (direction.x != 0 && direction.y != 0 &&
                    (!walkable[current.x + direction.x, current.y] ||
                     !walkable[current.x, current.y + direction.y]))
                {
                    diagonalBlockedCount++;
                    continue;
                }

                Vector3 currentPoint = points[current.x, current.y];
                Vector3 neighborPoint = points[nx, nz];
                float maxStepHeight = GetGuideEffectiveMaxStepHeight();
                if (Mathf.Abs(neighborPoint.y - currentPoint.y) > maxStepHeight)
                {
                    stepBlockedCount++;
                    hasLastBlockedSegment = true;
                    lastBlockedSegmentStart = currentPoint;
                    lastBlockedSegmentEnd = neighborPoint;
                    lastSegmentFailure = "height jump from " + FormatGuideVector(currentPoint) +
                                         " to " + FormatGuideVector(neighborPoint) +
                                         " delta=" + Mathf.Abs(neighborPoint.y - currentPoint.y).ToString("0.00") +
                                         " max=" + maxStepHeight.ToString("0.00");
                    continue;
                }

                if (!IsGuideSegmentWalkable(currentPoint, neighborPoint, out string segmentFailure))
                {
                    segmentBlockedCount++;
                    hasLastBlockedSegment = true;
                    lastBlockedSegmentStart = currentPoint;
                    lastBlockedSegmentEnd = neighborPoint;
                    lastSegmentFailure = segmentFailure;
                    continue;
                }

                float tentativeScore = gScore[current.x, current.y] + Vector3.Distance(currentPoint, neighborPoint);
                if (tentativeScore >= gScore[nx, nz])
                {
                    continue;
                }

                parent[nx, nz] = current;
                gScore[nx, nz] = tentativeScore;
                fScore[nx, nz] = tentativeScore + HorizontalDistance(neighborPoint, points[destinationNode.x, destinationNode.y]);
                if (!inOpen[nx, nz])
                {
                    open.Add(new Vector2Int(nx, nz));
                    inOpen[nx, nz] = true;
                }
            }
        }

        failureReason = "open set exhausted grid=" + width + "x" + height +
                        " cell=" + cellSize.ToString("0.00") +
                        " walkable=" + walkableCount +
                        " closed=" + closedCount +
                        " noHit=" + noHitCount +
                        " noGround=" + noWalkableGroundCount +
                        " clearanceBlocked=" + clearanceBlockedCount +
                        " diagonalBlocked=" + diagonalBlockedCount +
                        " stepBlocked=" + stepBlockedCount +
                        " segmentBlocked=" + segmentBlockedCount +
                        " startNode=" + startNode +
                        " destinationNode=" + destinationNode +
                        " lastSampleFailure=" + lastSampleFailure +
                        " lastSegmentFailure=" + lastSegmentFailure;
        if (hasLastBlockedSegment)
        {
            UpdateGuideDebugPathLine(lastBlockedSegmentStart, lastBlockedSegmentEnd, Color.red);
        }
        return false;
    }

    private bool TryFindNearestWalkableNode(Vector3 worldPoint, bool[,] walkable, Vector3[,] points, out Vector2Int node)
    {
        node = new Vector2Int(-1, -1);
        float bestDistance = float.PositiveInfinity;
        int width = walkable.GetLength(0);
        int height = walkable.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (!walkable[x, z])
                {
                    continue;
                }

                float distance = HorizontalDistance(worldPoint, points[x, z]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    node = new Vector2Int(x, z);
                }
            }
        }

        return node.x >= 0;
    }

    private void ReconstructGuideAStarPath(Vector2Int endNode, Vector2Int[,] parent, Vector3[,] points, List<Vector3> output)
    {
        List<Vector3> reversed = new List<Vector3>(64);
        Vector2Int current = endNode;
        while (current.x >= 0 && current.y >= 0)
        {
            reversed.Add(points[current.x, current.y]);
            current = parent[current.x, current.y];
        }

        for (int i = reversed.Count - 2; i >= 0; i--)
        {
            output.Add(reversed[i]);
        }
    }

    private void SimplifyGuideFallbackCorners(Vector3 start)
    {
        if (guideFallbackCorners.Count <= 2)
        {
            return;
        }

        List<Vector3> simplified = new List<Vector3>(guideFallbackCorners.Count);
        Vector3 anchor = start;
        int index = 0;
        while (index < guideFallbackCorners.Count)
        {
            int best = index;
            for (int candidate = guideFallbackCorners.Count - 1; candidate > index; candidate--)
            {
                if (CanSimplifyGuideSegment(anchor, guideFallbackCorners[candidate]))
                {
                    best = candidate;
                    break;
                }
            }

            simplified.Add(guideFallbackCorners[best]);
            anchor = guideFallbackCorners[best];
            index = best + 1;
        }

        guideFallbackCorners.Clear();
        guideFallbackCorners.AddRange(simplified);
    }

    private bool CanSimplifyGuideSegment(Vector3 from, Vector3 to)
    {
        if (HorizontalDistance(from, to) > guideSimplifyMaxDistance)
        {
            return false;
        }

        if (Mathf.Abs(to.y - from.y) > guideSimplifyMaxHeightDelta)
        {
            return false;
        }

        return IsGuideSegmentWalkable(from, to);
    }

    private bool IsGuideSegmentWalkable(Vector3 from, Vector3 to)
    {
        return IsGuideSegmentWalkable(from, to, out _);
    }

    private bool IsGuideSegmentWalkable(Vector3 from, Vector3 to, out string failureReason)
    {
        float distance = HorizontalDistance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.75f, GetGuideEffectiveAStarCellSize() * 0.5f)));
        Vector3 previous = from;
        failureReason = "";
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 probe = Vector3.Lerp(from, to, t);
            if (!TrySampleGuideWalkablePoint(probe, out Vector3 sampled, out string sampleFailure))
            {
                failureReason = "segment sample failed t=" + t.ToString("0.00") +
                                " probe=" + FormatGuideVector(probe) +
                                " from=" + FormatGuideVector(from) +
                                " to=" + FormatGuideVector(to) +
                                " reason=" + sampleFailure;
                return false;
            }

            float stepHeight = Mathf.Abs(sampled.y - previous.y);
            float maxStepHeight = GetGuideEffectiveMaxStepHeight();
            if (stepHeight > maxStepHeight)
            {
                failureReason = "segment height jump t=" + t.ToString("0.00") +
                                " previous=" + FormatGuideVector(previous) +
                                " sampled=" + FormatGuideVector(sampled) +
                                " delta=" + stepHeight.ToString("0.00") +
                                " max=" + maxStepHeight.ToString("0.00");
                return false;
            }

            previous = sampled;
        }

        return true;
    }

    private bool TrySampleGuideWalkablePoint(Vector3 probe, out Vector3 point)
    {
        return TrySampleGuideWalkablePoint(probe, out point, out _);
    }

    private bool TrySampleGuideWalkablePoint(Vector3 probe, out Vector3 point, out string failureReason)
    {
        point = probe;
        failureReason = "";
        Vector3 probeOrigin = new Vector3(
            probe.x,
            Mathf.Max(Mathf.Max(probe.y, transform.position.y), Mathf.Max(guideDestinationPosition.y, GuideHubPosition.y)) + guideFallbackGroundProbeHeight,
            probe.z);
        RaycastHit[] hits = Physics.RaycastAll(
            probeOrigin,
            Vector3.down,
            guideFallbackGroundProbeDistance,
            GetGuideCollisionMask(),
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            failureReason = "no ray hits origin=" + FormatGuideVector(probeOrigin) +
                            " distance=" + guideFallbackGroundProbeDistance.ToString("0.0");
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        int groundRejected = 0;
        int clearanceRejected = 0;
        int heightRejected = 0;
        int acceptedCandidates = 0;
        string lastRejected = "";
        bool foundPoint = false;
        Vector3 bestPoint = probe;
        float bestScore = float.PositiveInfinity;
        string bestGround = "";
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsGuideWalkableGroundHit(hit, out string groundRejectReason))
            {
                groundRejected++;
                lastRejected = "ground " + GetGuideColliderPath(hit.collider) + " rejected: " + groundRejectReason;
                continue;
            }

            Vector3 candidate = hit.point + Vector3.up * guideHoverAboveGround;
            float maxHeightAboveProbe = GetGuideEffectiveSampleMaxHeightAboveProbe();
            float heightAboveProbe = candidate.y - probe.y;
            if (heightAboveProbe > maxHeightAboveProbe)
            {
                heightRejected++;
                lastRejected = "height rejected at " + FormatGuideVector(candidate) +
                               " ground=" + GetGuideColliderPath(hit.collider) +
                               " aboveProbe=" + heightAboveProbe.ToString("0.0") +
                               " maxAbove=" + maxHeightAboveProbe.ToString("0.0");
                continue;
            }

            if (HasGuideClearance(candidate, hit.collider, out string clearanceRejectReason))
            {
                acceptedCandidates++;
                float heightDelta = Mathf.Abs(candidate.y - probe.y);
                float surfacePenalty = GetGuideSurfacePenalty(hit.collider, candidate.y);
                float distanceTieBreaker = hit.distance * 0.001f;
                float score = heightDelta * 5f + surfacePenalty + distanceTieBreaker;
                if (score < bestScore)
                {
                    foundPoint = true;
                    bestScore = score;
                    bestPoint = candidate;
                    bestGround = GetGuideColliderPath(hit.collider);
                }
                continue;
            }

            clearanceRejected++;
            lastRejected = "clearance rejected at " + FormatGuideVector(candidate) +
                           " ground=" + GetGuideColliderPath(hit.collider) +
                           " blocker=" + clearanceRejectReason;
        }

        if (foundPoint)
        {
            point = bestPoint;
            if (guideDebugEnabled && Mathf.Abs(bestPoint.y - probe.y) > GetGuideEffectiveMaxStepHeight())
            {
                GuideWarn("Sample accepted far-height ground probe=" + FormatGuideVector(probe) +
                          " point=" + FormatGuideVector(bestPoint) +
                          " ground=" + bestGround +
                          " acceptedCandidates=" + acceptedCandidates +
                          " score=" + bestScore.ToString("0.0"));
            }
            return true;
        }

        failureReason = "no accepted ground hits=" + hits.Length +
                        " groundRejected=" + groundRejected +
                        " clearanceRejected=" + clearanceRejected +
                        " heightRejected=" + heightRejected +
                        " acceptedCandidates=" + acceptedCandidates +
                        " probe=" + FormatGuideVector(probe) +
                        " lastRejected=" + lastRejected;
        return false;
    }

    private bool IsGuideWalkableGroundHit(RaycastHit hit)
    {
        return IsGuideWalkableGroundHit(hit, out _);
    }

    private bool IsGuideWalkableGroundHit(RaycastHit hit, out string rejectReason)
    {
        rejectReason = "";
        if (hit.collider == null)
        {
            rejectReason = "missing collider";
            return false;
        }

        if (hit.collider.isTrigger)
        {
            rejectReason = "trigger collider";
            return false;
        }

        if (hit.collider.transform.IsChildOf(transform))
        {
            rejectReason = "Rudolf's own collider";
            return false;
        }

        if (IsGuideWaterSurface(hit.collider.transform))
        {
            rejectReason = "water surface";
            return false;
        }

        if (hit.normal.y < 0.35f)
        {
            rejectReason = "normal too steep " + hit.normal.y.ToString("0.00");
            return false;
        }

        if (IsTreeOrFoliage(hit.collider.transform))
        {
            rejectReason = "tree or foliage";
            return false;
        }

        return true;
    }

    private bool HasGuideClearance(Vector3 hoverPoint, Collider groundCollider)
    {
        return HasGuideClearance(hoverPoint, groundCollider, out _);
    }

    private bool HasGuideClearance(Vector3 hoverPoint, Collider groundCollider, out string blockerReason)
    {
        blockerReason = "";
        Vector3 center = hoverPoint + Vector3.up * Mathf.Max(0.4f, guideAStarBodyHeight * 0.5f);
        Collider[] colliders = Physics.OverlapSphere(
            center,
            Mathf.Max(0.3f, guideAStarClearanceRadius),
            GetGuideCollisionMask(),
            QueryTriggerInteraction.Ignore);

        if (colliders == null || colliders.Length == 0)
        {
            return true;
        }

        float bodyBottom = hoverPoint.y - 0.05f;
        float bodyTop = hoverPoint.y + Mathf.Max(0.6f, guideAStarBodyHeight);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null ||
                collider == groundCollider ||
                collider.isTrigger ||
                collider.transform.IsChildOf(transform) ||
                IsGuidePlayerCollider(collider))
            {
                continue;
            }

            if (IsTreeOrFoliage(collider.transform))
            {
                if (guideIgnoreFoliageClearance)
                {
                    continue;
                }

                blockerReason = GetGuideColliderPath(collider) + " tree/foliage " + FormatGuideBounds(collider.bounds);
                return false;
            }

            if (IsGuideWaterSurface(collider.transform))
            {
                continue;
            }

            if (IsGuideThinDecorativeObstacle(collider))
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (IsGuideGroundLikeCollider(collider, hoverPoint.y))
            {
                continue;
            }

            if (bounds.max.y <= bodyBottom + 0.2f ||
                bounds.min.y >= bodyTop)
            {
                continue;
            }

            blockerReason = GetGuideColliderPath(collider) + " " + FormatGuideBounds(bounds);
            return false;
        }

        return true;
    }

    private bool IsGuideGroundLikeCollider(Collider collider, float hoverY)
    {
        if (collider == null || IsGuideWaterSurface(collider.transform) || IsTreeOrFoliage(collider.transform))
        {
            return false;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            string currentName = current.name.ToLowerInvariant();
            if (currentName.Contains("ground") ||
                currentName.Contains("floor") ||
                currentName.Contains("path") ||
                currentName.Contains("road") ||
                currentName.Contains("bridge") ||
                currentName.Contains("bruecke") ||
                currentName.Contains("brücke") ||
                currentName.Contains("brucke") ||
                currentName.Contains("stair") ||
                currentName.Contains("step") ||
                currentName.Contains("platform") ||
                currentName.Contains("island"))
            {
                return true;
            }

            current = current.parent;
        }

        Bounds bounds = collider.bounds;
        return bounds.max.y <= hoverY + 0.35f ||
               (bounds.size.y <= 0.75f && bounds.center.y <= hoverY);
    }

    private float GetGuideSurfacePenalty(Collider collider, float hoverY)
    {
        if (IsGuidePreferredPathSurface(collider))
        {
            return -18f;
        }

        return IsGuideGroundLikeCollider(collider, hoverY) ? 0f : 12f;
    }

    private bool IsGuidePreferredPathSurface(Collider collider)
    {
        if (collider == null || IsGuideWaterSurface(collider.transform) || IsTreeOrFoliage(collider.transform))
        {
            return false;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            string currentName = current.name.ToLowerInvariant();
            if (currentName.Contains("bridge") ||
                currentName.Contains("bruecke") ||
                currentName.Contains("brücke") ||
                currentName.Contains("brucke") ||
                currentName.Contains("stair") ||
                currentName.Contains("step") ||
                currentName.Contains("path") ||
                currentName.Contains("road"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsGuidePlayerCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (player != null && collider.transform.IsChildOf(player))
        {
            return true;
        }

        Transform current = collider.transform;
        while (current != null)
        {
            string currentName = current.name.ToLowerInvariant();
            if (currentName.Contains("fps_player") ||
                currentName.Contains("player"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsGuideThinDecorativeObstacle(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        float maxFootprint = Mathf.Max(0.05f, guideThinObstacleMaxFootprint);
        return bounds.size.x <= maxFootprint && bounds.size.z <= maxFootprint;
    }

    private float GetGuideEffectiveMaxStepHeight()
    {
        return Mathf.Max(6.25f, guideAStarMaxStepHeight);
    }

    private float GetGuideEffectiveSampleMaxHeightAboveProbe()
    {
        return Mathf.Max(0.5f, guideSampleMaxHeightAboveProbe);
    }

    private float GetGuideEffectiveAStarCellSize()
    {
        return Mathf.Clamp(Mathf.Min(guideAStarCellSize, 2f), 1.25f, 2f);
    }

    private void ConfigureGuideAgentSpeed(float speed)
    {
        if (guideAgent == null)
        {
            return;
        }

        guideAgent.speed = Mathf.Max(0.1f, speed);
        guideAgent.acceleration = Mathf.Max(8f, guideAgent.speed * 4f);
        guideAgent.stoppingDistance = Mathf.Max(0.35f, guideWaypointReachDistance * 0.6f);
    }

    private void StopGuideAgent()
    {
        if (guideAgent != null)
        {
            if (guideAgent.enabled && guideAgent.isOnNavMesh)
            {
                guideAgent.ResetPath();
            }
            guideAgent.enabled = false;
        }

        if (guideAgentProxy != null)
        {
            guideAgentProxy.SetActive(false);
        }
    }

    private void StopFallbackGuide()
    {
        guideFallbackActive = false;
        guideFallbackIndex = 0;
        guideFallbackCorners.Clear();
    }

    private bool IsGuideAgentRunning()
    {
        return guideAgent != null &&
               guideAgent.enabled &&
               guideAgentProxy != null &&
               guideAgentProxy.activeSelf &&
               guideAgent.isOnNavMesh;
    }

    private float GetGuideRemainingDistance()
    {
        if (IsGuideAgentRunning() &&
            !guideAgent.pathPending &&
            !float.IsInfinity(guideAgent.remainingDistance))
        {
            return guideAgent.remainingDistance;
        }

        if (guideFallbackActive)
        {
            return GetFallbackRemainingDistance();
        }

        return Vector3.Distance(transform.position, guideDestinationPosition + Vector3.up * guideHoverAboveGround);
    }

    private float GetFallbackRemainingDistance()
    {
        if (!guideFallbackActive || guideFallbackCorners.Count == 0)
        {
            return Vector3.Distance(transform.position, guideDestinationPosition + Vector3.up * guideHoverAboveGround);
        }

        int index = Mathf.Clamp(guideFallbackIndex, 0, guideFallbackCorners.Count - 1);
        float distance = Vector3.Distance(transform.position, guideFallbackCorners[index]);
        for (int i = index; i < guideFallbackCorners.Count - 1; i++)
        {
            distance += Vector3.Distance(guideFallbackCorners[i], guideFallbackCorners[i + 1]);
        }

        return distance;
    }

    private string GetGuideModeLabel()
    {
        if (IsGuideAgentRunning())
        {
            return "NavMesh";
        }

        if (guideFallbackActive)
        {
            return "FallbackAStar";
        }

        return "Idle";
    }

    private Vector3 PeekGuideGroundTarget()
    {
        if (guideTarget == null)
        {
            return transform.position;
        }

        if (IsGuideAgentRunning())
        {
            return guideAgentProxy.transform.position + Vector3.up * guideHoverAboveGround;
        }

        if (guideFallbackActive && guideFallbackCorners.Count > 0)
        {
            int index = Mathf.Clamp(guideFallbackIndex, 0, guideFallbackCorners.Count - 1);
            return guideFallbackCorners[index];
        }

        return guideDestinationPosition + Vector3.up * guideHoverAboveGround;
    }

    private Vector3 GetGuideGroundTarget()
    {
        if (guideTarget == null)
        {
            return transform.position;
        }

        if (IsGuideAgentRunning())
        {
            return guideAgentProxy.transform.position + Vector3.up * guideHoverAboveGround;
        }

        if (guideFallbackActive)
        {
            return GetFallbackGuideTarget();
        }

        return transform.position;
    }

    private Vector3 GetFallbackGuideTarget()
    {
        if (!guideFallbackActive || guideFallbackCorners.Count == 0)
        {
            return transform.position;
        }

        while (guideFallbackIndex < guideFallbackCorners.Count - 1 &&
               HorizontalDistance(transform.position, guideFallbackCorners[guideFallbackIndex]) <= guideWaypointReachDistance)
        {
            guideFallbackIndex++;
        }

        return guideFallbackCorners[Mathf.Clamp(guideFallbackIndex, 0, guideFallbackCorners.Count - 1)];
    }

    private Vector3 SnapFallbackGuidePosition(Vector3 position, float fallbackGroundY)
    {
        if (TrySampleGuideWalkablePoint(position, out Vector3 snapped))
        {
            return snapped;
        }

        float targetY = fallbackGroundY + guideHoverAboveGround;
        if (Application.isPlaying)
        {
            targetY = Mathf.MoveTowards(transform.position.y, targetY, 2.5f * Time.deltaTime);
        }

        position.y = targetY;
        return position;
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
        if (IsSakuraNoClipTransform(hitTransform))
        {
            return true;
        }

        if (IsGuideTerrainSurface(hitTransform))
        {
            return false;
        }

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

    private static bool IsGuideTerrainSurface(Transform hitTransform)
    {
        if (hitTransform == null)
        {
            return false;
        }

        string leafName = hitTransform.name.ToLowerInvariant();
        if (leafName == "grnd" ||
            leafName == "ground" ||
            leafName == "terrain" ||
            leafName == "floor" ||
            leafName == "path" ||
            leafName == "road" ||
            leafName == "slope" ||
            leafName == "island")
        {
            return true;
        }

        if (leafName == "all")
        {
            Transform parent = hitTransform.parent;
            while (parent != null)
            {
                string parentName = parent.name.ToLowerInvariant();
                if (parentName.Contains("low_poly_trees") ||
                    parentName.Contains("terrain") ||
                    parentName.Contains("island"))
                {
                    return true;
                }

                parent = parent.parent;
            }
        }

        return false;
    }

    private static bool IsGuideWaterSurface(Transform hitTransform)
    {
        while (hitTransform != null)
        {
            string name = hitTransform.name.ToLowerInvariant();
            if (name.Contains("water") ||
                name.Contains("lake") ||
                name.Contains("river") ||
                name.Contains("ocean") ||
                name.Contains("sea"))
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

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string rendererName = renderer.name;
            if (rendererName.EndsWith("_GuideOutline", StringComparison.Ordinal) ||
                rendererName.EndsWith("_GuideChams", StringComparison.Ordinal))
            {
                renderer.enabled = active;
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

    private bool TryHandleConversationExit(string transcript, bool conversationWasActive)
    {
        if (!conversationWasActive && !IsConversationActive())
        {
            return false;
        }

        string normalized = NormalizeCommand(StripWakePhraseIfPresent(transcript));
        if (!IsConversationExitCommand(normalized))
        {
            return false;
        }

        EndVoiceConversation("Okay — talk later.");
        return true;
    }

    private void EndVoiceConversation(string line)
    {
        waiting = false;
        conversationActive = false;
        pendingVoiceTranscript = null;
        lastConversationHeard = -999f;
        lastNoTranscriptPromptAt = -999f;
        resumeVoiceListeningAt = Time.unscaledTime + 0.8f;
        conversationHistory.Clear();

        if (!string.IsNullOrWhiteSpace(line))
        {
            ShowLine(line, 2.6f);
            if (voiceBridge != null)
            {
                voiceBridge.Speak(line, "happy");
            }
        }
    }

    private static bool IsConversationExitCommand(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized == "bye" ||
               normalized == "goodbye" ||
               normalized == "good bye" ||
               normalized == "good by" ||
               normalized == "see ya" ||
               normalized == "see you" ||
               normalized == "later" ||
               normalized.StartsWith("bye ") ||
               normalized.EndsWith(" bye") ||
               normalized.Contains(" bye ") ||
               normalized.StartsWith("goodbye ") ||
               normalized.EndsWith(" goodbye") ||
               normalized.Contains(" goodbye ") ||
               normalized.StartsWith("good bye ") ||
               normalized.EndsWith(" good bye") ||
               normalized.Contains(" good bye ") ||
               normalized.StartsWith("good by ") ||
               normalized.EndsWith(" good by") ||
               normalized.Contains(" good by ") ||
               normalized.Contains("see you later") ||
               normalized.Contains("talk later") ||
               normalized.Contains("talk to you later") ||
               normalized.Contains("thats all") ||
               normalized.Contains("that is all") ||
               normalized.Contains("end conversation") ||
               normalized.Contains("stop listening") ||
               normalized.Contains("stop talking") ||
               normalized.Contains("go away") ||
               normalized.Contains("good night") ||
               normalized.Contains("goodnight");
    }

    private void UpdateVoiceListening(Camera fpsCamera)
    {
        if (voiceBridge == null)
        {
            return;
        }

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

        bool voiceInputAllowed = IsRudolfVoiceInputAllowed();
        if (!voiceInputAllowed)
        {
            voiceBridge.SetListening(false);
            return;
        }

        EnsureVoiceServerConnection();

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

    private static bool IsRudolfVoiceInputAllowed()
    {
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        return manager != null && manager.IsVoiceInputAllowedByMode();
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
