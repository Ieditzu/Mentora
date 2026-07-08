using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mentora.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

public class CodeWorldRuntime : MonoBehaviour
{
    private const string HostModePrefKey = "MP_HostMode";
    private const string CodeWorldModeValue = "codeworld";
    private const string CodeWorldActivePrefKey = "MP_CodeWorldActive";
    private const float EditorSyncInterval = 0.12f;
    private const float CursorSyncInterval = 0.08f;
    private static readonly Vector3 BuildSpawn = new Vector3(220f, 32f, 520f);
    private static readonly Quaternion BuildRotation = Quaternion.Euler(0f, 180f, 0f);

    private static CodeWorldRuntime instance;

    private readonly Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> commandHistory = new List<string>();
    private readonly Dictionary<string, RemoteCursorVisual> remoteCursorVisuals = new Dictionary<string, RemoteCursorVisual>(StringComparer.OrdinalIgnoreCase);

    private MultiplayerSessionManager sessionManager;
    private GameObject worldRoot;
    private GameObject staticEnvironmentRoot;
    private GameObject dynamicObjectsRoot;
    private Canvas editorCanvas;
    private GameObject editorPanel;
    private RectTransform editorPanelRect;
    private GraphicRaycaster editorCanvasRaycaster;
    private InputField editorInput;
    private ScrollRect editorScrollRect;
    private RectTransform editorContentRect;
    private RectTransform editorViewportRect;
    private Text statusText;
    private Text historyText;
    private ScrollRect historyScrollRect;
    private RectTransform historyContentRect;
    private RectTransform historyViewportRect;
    private Text editorHintText;
    private GameObject aiPanel;
    private RectTransform aiPanelRect;
    private InputField aiInput;
    private Text aiHistoryText;
    private ScrollRect aiHistoryScrollRect;
    private RectTransform aiHistoryContentRect;
    private RectTransform aiHistoryViewportRect;
    private Button runButton;
    private Button aiOpenButton;
    private Button aiCloseButton;
    private GameObject challengePanel;
    private Text challengeTitleText;
    private Text challengeDescriptionText;
    private Text challengeChecklistText;
    private Button challengeVerifyButton;
    private Button challengeResetButton;
    private Button challengeSandboxButton;
    private CodeWorldChallengeDefinition activeChallenge;
    private readonly Stack<string> editorUndoStack = new Stack<string>();
    private readonly List<string> aiChatLines = new List<string>();
    private bool suppressBackquoteTextMutation;
    private string backquoteGuardText = string.Empty;
    private int backquoteGuardAnchor;
    private int backquoteGuardFocus;
    private int backquoteGuardCaret;
    private bool modeActive;
    private bool editorVisible;
    private bool aiVisible;
    private string lastEditorTrackedText = string.Empty;
    private bool suppressEditorTracking;
    private bool suppressEditorSync;
    private bool editorSyncDirty;
    private float nextEditorSyncTime;
    private bool cursorSyncDirty;
    private float nextCursorSyncTime;
    private int lastLocalCaretIndex = -1;
    private bool awaitingAiResponse;
    private string pendingAiResponse = string.Empty;
    private bool awaitingCodeWorldPythonResponse;
    private string pendingCodeWorldRequestId = string.Empty;
    private CodeWorldPythonResponsePacket pendingCodeWorldPythonResponse;
    private bool aiNetworkSubscribed;
    private bool editorHintDismissed;
    private Coroutine localExecutionRoutine;
    private bool localExecutionRunning;
    private bool localExecutionStopRequested;
    private bool vrCodeTogglePressed;
    private GameObject vrCursor;
    private RectTransform vrCursorRect;
    private Image vrCursorImage;
    private Outline vrCursorOutline;
    private PointerEventData vrPointerEventData;
    private EventSystem vrPointerEventSystem;
    private readonly List<RaycastResult> vrRaycastResults = new List<RaycastResult>();
    private readonly List<RaycastResult> uiTapRaycastResults = new List<RaycastResult>();
    private GameObject vrHoveredObject;
    private GameObject vrPressedObject;
    private bool vrSelectWasPressed;
    private bool vrCursorHasSmoothedPosition;
    private Vector2 vrCursorSmoothedAnchoredPosition;
    private Vector2 vrCursorVelocity;
    private InputField vrFocusedInputField;
    private TouchScreenKeyboard vrTouchKeyboard;
    private bool suppressVrKeyboardSync;
    private Button stopButton;
    private const int MaxAiChatLines = 16;
    private const int MaxLoopIterations = 256;
    private const float VrEditorCanvasDistance = 1.45f;
    private const float VrEditorCanvasHeightOffset = -0.04f;
    private const float VrPointerMaxDistance = 6f;
    private const float VrPointerDownwardAngle = -30f;
    private const float VrCursorSmoothTime = 0.06f;
    private static readonly Vector3 VrEditorCanvasScale = Vector3.one * 0.0011f;
    private static readonly Vector2 VrCursorHoverSize = new Vector2(34f, 34f);
    private static readonly Vector2 VrCursorPressedSize = new Vector2(26f, 26f);
    private static Sprite vrCursorSprite;

    private sealed class RemoteCursorVisual
    {
        public string ClientId;
        public int CaretIndex;
        public string PlayerName;
        public Color Color;
        public GameObject Root;
        public RectTransform RootRect;
        public Image CaretImage;
        public Text NameText;
    }

    public static bool ConsumesPauseInput => instance != null && instance.editorVisible;
    public static Vector3 SpawnPosition => BuildSpawn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void ActivateForHost()
    {
        EnsureInstance().ActivateAsHost();
    }

    public static void StartSandboxFromPortal()
    {
        EnsureInstance().StartSandboxMode();
    }

    public static void StartChallengeFromPortal(CodeWorldChallengeDefinition definition)
    {
        if (definition == null)
        {
            StartSandboxFromPortal();
            return;
        }

        EnsureInstance().StartChallengeMode(definition);
    }

    public static bool ShouldShowMobileToggle
    {
        get
        {
            if (instance == null || !instance.modeActive)
            {
                return false;
            }

            return instance.sessionManager != null && instance.sessionManager.IsHosting;
        }
    }

    public static void ToggleEditorFromMobile()
    {
        if (instance == null || !instance.modeActive)
        {
            return;
        }

        instance.UpdateEditorVisibility(!instance.editorVisible);
    }

    public static void DeactivateLocal()
    {
        if (instance != null)
        {
            instance.DisableMode(false);
        }

        PlayerPrefs.DeleteKey(CodeWorldActivePrefKey);
        PlayerPrefs.DeleteKey(HostModePrefKey);
        PlayerPrefs.Save();
    }

    public static bool TryCreateSnapshotPacket(out CodeWorldStatePacket packet)
    {
        if (instance == null || !instance.modeActive)
        {
            packet = null;
            return false;
        }

        packet = new CodeWorldStatePacket(true, instance.SerializeHistory(), instance.GetEditorText());
        return true;
    }

    private static CodeWorldRuntime EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<CodeWorldRuntime>();
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new GameObject("CodeWorldRuntime");
        instance = root.AddComponent<CodeWorldRuntime>();
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
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
        editorHintDismissed = false;
        AcquireSessionManager();
        BuildEditorUi();
        UpdateEditorVisibility(false);
        UpdateEditorHintVisibility();

        // Do not auto-enable code-world from saved prefs.
        // It should only activate when the host explicitly chooses the code-world option.
    }

    private void OnEnable()
    {
        AcquireSessionManager();
        SubscribePackets(true);
        RegisterAiNetworkHandlers();
    }

    private void OnDisable()
    {
        StopLocalScriptExecution(false);
        SubscribePackets(false);
        UnregisterAiNetworkHandlers();
    }

    private void OnDestroy()
    {
        StopLocalScriptExecution(false);
    }

    private void Update()
    {
        AcquireSessionManager();

        if (!modeActive)
        {
            ResetVrPointerState();
            return;
        }

        if (!editorVisible || !IsQuestVrCodeIslandUi())
        {
            UpdateEditorCanvasMode(false);
        }
        UpdateVrEditorPointer();
        SyncVrKeyboard();

        Keyboard keyboard = Keyboard.current;

        if (TryConsumeVrEditorToggle())
        {
            UpdateEditorVisibility(!editorVisible);
        }

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && localExecutionRunning)
        {
            StopLocalScriptExecution(true);
            return;
        }

        if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
        {
            if (editorVisible)
            {
                BeginBackquoteTextGuard();
            }
            else
            {
                suppressBackquoteTextMutation = false;
            }

            UpdateEditorVisibility(!editorVisible);
            return;
        }

        if (!editorVisible)
        {
            return;
        }

        if (Application.isMobilePlatform && TryHandleMobileOutsideTapClose())
        {
            return;
        }

        TrackEditorUndoState();

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (localExecutionRunning)
            {
                StopLocalScriptExecution(true);
                return;
            }

            UpdateEditorVisibility(false);
            return;
        }

        if (keyboard == null)
        {
            TrackLocalCursorState();
            FlushPendingEditorSync();
            FlushPendingCursorSync();
            return;
        }

        bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        if (ctrlHeld && keyboard.zKey.wasPressedThisFrame)
        {
            UndoEditorText();
            return;
        }

        bool runPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        if (ctrlHeld && runPressed)
        {
            ExecuteLocalScript(editorInput != null ? editorInput.text : string.Empty);
        }

        TrackLocalCursorState();
        FlushPendingEditorSync();
        FlushPendingCursorSync();
    }

    private bool TryConsumeVrEditorToggle()
    {
        bool pressedNow = false;

        UnityEngine.XR.InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid)
        {
            leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out pressedNow);
        }

        if (!pressedNow)
        {
            try { pressedNow = OVRInput.Get(OVRInput.RawButton.X); } catch { }
        }

        bool pressedThisFrame = pressedNow && !vrCodeTogglePressed;
        vrCodeTogglePressed = pressedNow;
        return pressedThisFrame;
    }

    private void OnGUI()
    {
        if (!editorVisible)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.KeyDown)
        {
            return;
        }

        if ((currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.C && editorInput != null && editorInput.isFocused)
        {
            CopySelectedEditorText();
            currentEvent.Use();
        }

        if (currentEvent.keyCode == KeyCode.BackQuote || currentEvent.character == '`')
        {
            currentEvent.Use();
        }
    }

    private void AcquireSessionManager()
    {
        MultiplayerSessionManager current = MultiplayerSessionManager.Instance;
        if (current == sessionManager)
        {
            return;
        }

        SubscribePackets(false);
        sessionManager = current;
        SubscribePackets(true);
    }

    private void SubscribePackets(bool subscribe)
    {
        if (sessionManager == null)
        {
            return;
        }

        if (subscribe)
        {
            sessionManager.OnQuizPacket -= HandleNetworkPacket;
            sessionManager.OnQuizPacket += HandleNetworkPacket;
        }
        else
        {
            sessionManager.OnQuizPacket -= HandleNetworkPacket;
        }
    }

    private void RegisterAiNetworkHandlers()
    {
        if (aiNetworkSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived += HandleAiNetworkPacket;
        aiNetworkSubscribed = true;
    }

    private void UnregisterAiNetworkHandlers()
    {
        if (!aiNetworkSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived -= HandleAiNetworkPacket;
        aiNetworkSubscribed = false;
    }

    private void HandleAiNetworkPacket(Packet packet)
    {
        if (packet is ActionResponsePacket actionResponse && actionResponse.RequestPacketId == 74)
        {
            if (!UnityMainThreadDispatcher.IsInitialized)
            {
                UnityMainThreadDispatcher.Initialize();
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleCodeWorldActionResponse(actionResponse));
            return;
        }

        if (packet is CodeWorldPythonResponsePacket codeWorldResponse)
        {
            if (!UnityMainThreadDispatcher.IsInitialized)
            {
                UnityMainThreadDispatcher.Initialize();
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleCodeWorldPythonResponse(codeWorldResponse));
            return;
        }

        if (!(packet is AiResponsePacket aiResponse))
        {
            return;
        }

        if (!UnityMainThreadDispatcher.IsInitialized)
        {
            UnityMainThreadDispatcher.Initialize();
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() => HandleAiResponse(aiResponse));
    }

    private void HandleCodeWorldPythonResponse(CodeWorldPythonResponsePacket response)
    {
        if (!awaitingCodeWorldPythonResponse || response == null)
        {
            return;
        }

        if (!string.Equals(response.RequestId ?? string.Empty, pendingCodeWorldRequestId ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        pendingCodeWorldPythonResponse = response;
        awaitingCodeWorldPythonResponse = false;
    }

    private void HandleCodeWorldActionResponse(ActionResponsePacket response)
    {
        if (!awaitingCodeWorldPythonResponse || response == null)
        {
            return;
        }

        if (response.Success)
        {
            return;
        }

        pendingCodeWorldPythonResponse = new CodeWorldPythonResponsePacket
        {
            RequestId = pendingCodeWorldRequestId,
            CommandsText = string.Empty,
            Output = string.Empty,
            Error = response.Message ?? "Code execution request failed."
        };
        awaitingCodeWorldPythonResponse = false;
    }

    private void HandleNetworkPacket(Packet packet)
    {
        if (packet is CodeWorldStatePacket statePacket)
        {
            ApplySnapshot(statePacket);
            return;
        }

        if (packet is CodeWorldEditorSyncPacket editorSyncPacket)
        {
            ApplyRemoteEditorText(editorSyncPacket);
            return;
        }

        if (packet is CodeWorldCursorPacket cursorPacket)
        {
            ApplyRemoteCursor(cursorPacket);
            return;
        }

        if (!(packet is CodeWorldCommandPacket commandPacket))
        {
            return;
        }

        if (!modeActive)
        {
            ActivateLocal(false, false);
        }

        string localClientId = sessionManager != null ? sessionManager.LocalClientId : string.Empty;
        if (!string.IsNullOrEmpty(localClientId) && commandPacket.AuthorClientId == localClientId && sessionManager != null && sessionManager.IsHosting)
        {
            return;
        }

        if (TryRunCommand(commandPacket.CommandText, true, out string feedback, out bool mutatedWorld) && mutatedWorld)
        {
            commandHistory.Add(NormalizeCommand(commandPacket.CommandText));
            RefreshHistoryText();
            SetStatus(feedback);
        }
        else if (!string.IsNullOrWhiteSpace(feedback))
        {
            SetStatus(feedback);
        }
    }

    private void ActivateAsHost()
    {
        activeChallenge = null;
        ActivateLocal(true, true);
        commandHistory.Clear();
        ClearSpawnedObjects();
        RefreshHistoryText();
        RefreshChallengeUi();
        UpdateEditorVisibility(false);
        BroadcastStateToRemotes();
    }

    private void StartSandboxMode()
    {
        activeChallenge = null;
        ActivateLocal(true, false);
        commandHistory.Clear();
        ClearSpawnedObjects();
        SetEditorText(BuildStarterScript());
        RefreshHistoryText();
        RefreshChallengeUi();
        BroadcastStateToRemotes();
        UpdateEditorVisibility(true);
        SetStatus("Sandbox ready. Build anything with Python.");
    }

    private void StartChallengeMode(CodeWorldChallengeDefinition definition)
    {
        activeChallenge = definition;
        ActivateLocal(true, false);
        ResetActiveChallengeWorld();
        RefreshChallengeUi();
        BroadcastStateToRemotes();
        UpdateEditorVisibility(true);
        SetStatus("Challenge loaded: " + definition.Title);
    }

    private void ResetActiveChallengeWorld()
    {
        commandHistory.Clear();
        ClearSpawnedObjects();
        RefreshHistoryText();

        if (activeChallenge == null)
        {
            SetEditorText(BuildStarterScript());
            RefreshChallengeUi();
            return;
        }

        ApplyChallengeSetupCommands(activeChallenge);
        SetEditorText(string.IsNullOrWhiteSpace(activeChallenge.StarterCode) ? BuildStarterScript() : activeChallenge.StarterCode);
        RefreshHistoryText();
        RefreshChallengeUi();
        BroadcastStateToRemotes();
    }

    private void ApplyChallengeSetupCommands(CodeWorldChallengeDefinition definition)
    {
        if (definition == null || definition.SetupCommands == null)
        {
            return;
        }

        for (int i = 0; i < definition.SetupCommands.Length; i++)
        {
            string setupCommand = NormalizeCommand(definition.SetupCommands[i]);
            if (string.IsNullOrWhiteSpace(setupCommand))
            {
                continue;
            }

            if (TryRunCommand(setupCommand, true, out _, out bool mutatedWorld) && mutatedWorld)
            {
                commandHistory.Add(setupCommand);
            }
        }
    }

    private void ActivateLocal(bool teleportPlayer, bool persistPrefs)
    {
        modeActive = true;
        EnsureWorld();
        EnableNoclip(true);
        RobotCompanion.SetCompanionVisible(false);
        RegisterAiNetworkHandlers();

        if (teleportPlayer)
        {
            TeleportLocalPlayer(BuildSpawn, BuildRotation);
        }

        if (persistPrefs)
        {
            PlayerPrefs.SetString(HostModePrefKey, CodeWorldModeValue);
            PlayerPrefs.Save();
        }

        ShowEditorHintIfNeeded();
    }

    private void DisableMode(bool clearWorld)
    {
        modeActive = false;
        UpdateEditorVisibility(false);
        UpdateAiVisibility(false);
        UpdateEditorHintVisibility();
        EnableNoclip(false);
        RobotCompanion.SetCompanionVisible(true);

        if (clearWorld)
        {
            ClearSpawnedObjects();
            commandHistory.Clear();
            RefreshHistoryText();
        }

        ClearRemoteCursorVisuals();

        if (worldRoot != null)
        {
            worldRoot.SetActive(false);
        }
    }

    private void ApplySnapshot(CodeWorldStatePacket packet)
    {
        if (packet == null)
        {
            return;
        }

        if (!packet.IsActive)
        {
            activeChallenge = null;
            DisableMode(true);
            return;
        }

        activeChallenge = null;
        ActivateLocal(true, false);
        ClearSpawnedObjects();
        commandHistory.Clear();
        ClearRemoteCursorVisuals();
        SetEditorText(packet.EditorText);

        if (!string.IsNullOrWhiteSpace(packet.HistoryText))
        {
            string[] lines = packet.HistoryText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = NormalizeCommand(lines[i]);
                if (TryRunCommand(line, true, out _, out bool mutatedWorld) && mutatedWorld)
                {
                    commandHistory.Add(line);
                }
            }
        }

        RefreshHistoryText();
        SetStatus("Code World synced from host.");
    }

    private void ApplyRemoteEditorText(CodeWorldEditorSyncPacket packet)
    {
        if (packet == null)
        {
            return;
        }

        if (!modeActive)
        {
            ActivateLocal(false, false);
        }

        if (sessionManager != null && packet.AuthorClientId == sessionManager.LocalClientId)
        {
            return;
        }

        string incomingText = packet.EditorText ?? string.Empty;
        if (editorInput != null && string.Equals(editorInput.text ?? string.Empty, incomingText, StringComparison.Ordinal))
        {
            return;
        }

        SetEditorText(incomingText);
        SetStatus("Shared code updated.");
    }

    private void ApplyRemoteCursor(CodeWorldCursorPacket packet)
    {
        if (packet == null || string.IsNullOrWhiteSpace(packet.AuthorClientId))
        {
            return;
        }

        if (sessionManager != null && packet.AuthorClientId == sessionManager.LocalClientId)
        {
            return;
        }

        RemoteCursorVisual visual = GetOrCreateRemoteCursorVisual(packet.AuthorClientId, packet.PlayerName);
        visual.CaretIndex = Mathf.Max(0, packet.CaretIndex);
        visual.PlayerName = string.IsNullOrWhiteSpace(packet.PlayerName) ? "Player" : packet.PlayerName.Trim();
        if (visual.NameText != null)
        {
            visual.NameText.text = visual.PlayerName;
        }

        RefreshRemoteCursorVisual(visual);
    }

    private void EnsureWorld()
    {
        if (worldRoot == null)
        {
            worldRoot = new GameObject("CodeWorldRoot");
        }

        worldRoot.SetActive(true);

        if (staticEnvironmentRoot == null)
        {
            staticEnvironmentRoot = new GameObject("Environment");
            staticEnvironmentRoot.transform.SetParent(worldRoot.transform, false);
            BuildEnvironment(staticEnvironmentRoot.transform);
        }

        if (dynamicObjectsRoot == null)
        {
            dynamicObjectsRoot = new GameObject("Objects");
            dynamicObjectsRoot.transform.SetParent(worldRoot.transform, false);
        }
    }

    private void BuildEnvironment(Transform parent)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "BuildPlatform";
        platform.transform.SetParent(parent, false);
        platform.transform.position = BuildSpawn + new Vector3(0f, -2f, 0f);
        platform.transform.localScale = new Vector3(80f, 2f, 80f);
        ApplyMaterial(platform, new Color(0.08f, 0.11f, 0.16f));
    }

    private void ExecuteLocalScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            SetStatus("Type commands first. Example: cube box 0 1 0");
            return;
        }

        StartLocalScriptExecution(script);
    }

    private void StartLocalScriptExecution(string script)
    {
        StopLocalScriptExecution(false);
        localExecutionStopRequested = false;
        localExecutionRoutine = StartCoroutine(RunServerPythonScriptRoutine(script));
    }

    private void StopLocalScriptExecution(bool updateStatus)
    {
        localExecutionStopRequested = true;

        if (localExecutionRoutine != null)
        {
            StopCoroutine(localExecutionRoutine);
            localExecutionRoutine = null;
        }

        if (localExecutionRunning && updateStatus)
        {
            SetStatus("Script stopped.");
        }

        awaitingCodeWorldPythonResponse = false;
        pendingCodeWorldRequestId = string.Empty;
        pendingCodeWorldPythonResponse = null;
        localExecutionRunning = false;
    }

    private IEnumerator RunServerPythonScriptRoutine(string script)
    {
        localExecutionRunning = true;
        pendingCodeWorldRequestId = Guid.NewGuid().ToString("N");
        pendingCodeWorldPythonResponse = null;
        awaitingCodeWorldPythonResponse = true;
        SetStatus("Running Python on server...");

        bool sent = false;
        yield return SendPacketWithConnect(new CodeWorldPythonRunPacket(pendingCodeWorldRequestId, script), success => sent = success);
        if (!sent)
        {
            awaitingCodeWorldPythonResponse = false;
            SetStatus("Could not reach the code execution server.");
            localExecutionRunning = false;
            localExecutionRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (!localExecutionStopRequested && awaitingCodeWorldPythonResponse && elapsed < 20f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (localExecutionStopRequested)
        {
            localExecutionRunning = false;
            localExecutionRoutine = null;
            yield break;
        }

        if (awaitingCodeWorldPythonResponse)
        {
            awaitingCodeWorldPythonResponse = false;
            SetStatus("Python server did not answer in time.");
            localExecutionRunning = false;
            localExecutionRoutine = null;
            yield break;
        }

        CodeWorldPythonResponsePacket response = pendingCodeWorldPythonResponse;
        pendingCodeWorldPythonResponse = null;
        pendingCodeWorldRequestId = string.Empty;

        if (response == null)
        {
            SetStatus("Python server returned no response.");
            localExecutionRunning = false;
            localExecutionRoutine = null;
            yield break;
        }

        string error = (response.Error ?? string.Empty).Trim();
        string output = (response.Output ?? string.Empty).Trim();
        string commandsText = response.CommandsText ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(error))
        {
            SetStatus("Python error: " + ShortenStatus(error));
        }
        else if (!string.IsNullOrWhiteSpace(output))
        {
            SetStatus("Python output: " + ShortenStatus(output));
        }
        else
        {
            SetStatus("Python finished.");
        }

        int appliedCount = 0;
        string commandFeedback = string.Empty;
        yield return ApplyServerCommandLinesRoutine(commandsText, count => appliedCount = count, feedback => commandFeedback = feedback);

        if (!string.IsNullOrWhiteSpace(commandFeedback) && string.IsNullOrWhiteSpace(error))
        {
            SetStatus(commandFeedback);
        }
        else if (string.IsNullOrWhiteSpace(error))
        {
            SetStatus(appliedCount > 0 ? "Python applied " + appliedCount + " world command(s)." : "Python finished. No world commands were created.");
        }

        RefreshHistoryText();
        RefreshChallengeUi();
        if (editorInput != null)
        {
            lastEditorTrackedText = editorInput.text;
            editorInput.ActivateInputField();
        }

        localExecutionRunning = false;
        localExecutionRoutine = null;
        localExecutionStopRequested = false;
        yield break;
    }

    private IEnumerator ApplyServerCommandLinesRoutine(string commandsText, Action<int> onAppliedCount, Action<string> onFeedback)
    {
        int appliedCount = 0;
        if (string.IsNullOrWhiteSpace(commandsText))
        {
            onAppliedCount?.Invoke(0);
            yield break;
        }

        string[] lines = commandsText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        bool hostAppliesDirectly = sessionManager == null || !sessionManager.IsConnectedToSession || sessionManager.IsHosting;
        for (int i = 0; i < lines.Length; i++)
        {
            if (localExecutionStopRequested)
            {
                break;
            }

            string line = NormalizeCommand(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (hostAppliesDirectly)
            {
                if (TryRunCommand(line, true, out string feedback, out bool mutatedWorld))
                {
                    if (mutatedWorld)
                    {
                        commandHistory.Add(line);
                        BroadcastCommandFromHost(line);
                        appliedCount++;
                    }

                    if (!string.IsNullOrWhiteSpace(feedback))
                    {
                        onFeedback?.Invoke(feedback);
                    }
                }
                else
                {
                    onFeedback?.Invoke(feedback);
                    break;
                }
            }
            else
            {
                sessionManager.SendQuizPacketToHost(new CodeWorldCommandPacket(line, sessionManager.LocalClientId));
                appliedCount++;
            }

            yield return null;
        }

        if (!hostAppliesDirectly && appliedCount > 0)
        {
            onFeedback?.Invoke("Sent " + appliedCount + " Python world command(s) to host.");
        }

        onAppliedCount?.Invoke(appliedCount);
    }

    private IEnumerator ExecuteStatementListCoroutine(List<string> statements, ScriptCursor cursor, Dictionary<string, float> variables, Action<string> onError)
    {
        while (!localExecutionStopRequested && cursor.Index < statements.Count)
        {
            string statement = statements[cursor.Index];
            if (string.IsNullOrWhiteSpace(statement))
            {
                cursor.Index++;
                continue;
            }

            if (statement == "}")
            {
                cursor.Index++;
                yield break;
            }

            if (statement == "{")
            {
                cursor.Index++;
                continue;
            }

            if (IsIgnorableScriptStatement(statement))
            {
                cursor.Index++;
                continue;
            }

            if (TryParseLoopHeader(statement, "for", out string loopHeader))
            {
                cursor.Index++;
                if (!TryExtractLoopBody(statements, ref cursor.Index, out List<string> bodyStatements, out string feedback))
                {
                    onError?.Invoke(feedback);
                    yield break;
                }

                yield return ExecuteForLoopCoroutine(loopHeader, bodyStatements, variables, onError);
                if (localExecutionStopRequested)
                {
                    yield break;
                }

                continue;
            }

            if (TryParseLoopHeader(statement, "while", out loopHeader))
            {
                cursor.Index++;
                if (!TryExtractLoopBody(statements, ref cursor.Index, out List<string> bodyStatements, out string feedback))
                {
                    onError?.Invoke(feedback);
                    yield break;
                }

                yield return ExecuteWhileLoopCoroutine(loopHeader, bodyStatements, variables, onError);
                if (localExecutionStopRequested)
                {
                    yield break;
                }

                continue;
            }

            if (!TryExecuteVariableStatement(statement, variables, out bool handledVariable, out string variableFeedback))
            {
                onError?.Invoke(variableFeedback);
                yield break;
            }

            if (handledVariable)
            {
                cursor.Index++;
                yield return null;
                continue;
            }

            if (TryCompileCommandStatement(statement, variables, out string compiledCommand, out string commandFeedback))
            {
                cursor.Index++;
                if (!string.IsNullOrWhiteSpace(compiledCommand))
                {
                    yield return ExecuteCommandCoroutine(compiledCommand, onError);
                }

                continue;
            }

            onError?.Invoke(commandFeedback);
            yield break;
        }
    }

    private IEnumerator ExecuteForLoopCoroutine(string header, List<string> bodyStatements, Dictionary<string, float> variables, Action<string> onError)
    {
        string[] sections = SplitTopLevelSemicolons(header);
        if (sections.Length != 3)
        {
            onError?.Invoke("For syntax: for (int i = 0; i < 5; i++)");
            yield break;
        }

        if (!TryExecuteVariableStatement(sections[0], variables, out _, out string feedback))
        {
            onError?.Invoke(feedback);
            yield break;
        }

        yield return null;

        while (!localExecutionStopRequested)
        {
            if (!EvaluateConditionExpression(sections[1], variables, out bool condition, out feedback))
            {
                onError?.Invoke(feedback);
                yield break;
            }

            if (!condition)
            {
                yield break;
            }

            yield return ExecuteStatementListCoroutine(bodyStatements, new ScriptCursor(), variables, onError);
            if (localExecutionStopRequested)
            {
                yield break;
            }

            if (!TryExecuteVariableStatement(sections[2], variables, out _, out feedback))
            {
                onError?.Invoke(feedback);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ExecuteWhileLoopCoroutine(string header, List<string> bodyStatements, Dictionary<string, float> variables, Action<string> onError)
    {
        while (!localExecutionStopRequested)
        {
            if (!EvaluateConditionExpression(header, variables, out bool condition, out string feedback))
            {
                onError?.Invoke(feedback);
                yield break;
            }

            if (!condition)
            {
                yield break;
            }

            yield return ExecuteStatementListCoroutine(bodyStatements, new ScriptCursor(), variables, onError);
            if (localExecutionStopRequested)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ExecuteCommandCoroutine(string compiledCommand, Action<string> onError)
    {
        string line = NormalizeCommand(compiledCommand);
        if (string.IsNullOrWhiteSpace(line))
        {
            yield return null;
            yield break;
        }

        bool hostAppliesDirectly = sessionManager == null || !sessionManager.IsConnectedToSession || sessionManager.IsHosting;
        if (hostAppliesDirectly)
        {
            if (TryRunCommand(line, true, out string feedback, out bool mutatedWorld))
            {
                if (mutatedWorld)
                {
                    commandHistory.Add(line);
                    BroadcastCommandFromHost(line);
                }

                SetStatus(feedback);
                RefreshHistoryText();
            }
            else
            {
                onError?.Invoke(feedback);
                yield break;
            }
        }
        else
        {
            if (TryRunCommand(line, false, out string feedback, out bool mutatedWorld))
            {
                if (mutatedWorld)
                {
                    sessionManager.SendQuizPacketToHost(new CodeWorldCommandPacket(line, sessionManager.LocalClientId));
                    SetStatus("Sent to host: " + line);
                }
                else
                {
                    SetStatus(feedback);
                }
            }
            else
            {
                onError?.Invoke(feedback);
                yield break;
            }
        }

        yield return null;
    }

    private void BroadcastCommandFromHost(string line)
    {
        if (sessionManager == null || !sessionManager.IsHosting)
        {
            return;
        }

        sessionManager.BroadcastQuizPacketToRemotes(new CodeWorldCommandPacket(line, sessionManager.LocalClientId));
    }

    private void BroadcastStateToRemotes()
    {
        if (sessionManager == null || !sessionManager.IsHosting)
        {
            return;
        }

        sessionManager.BroadcastQuizPacketToRemotes(new CodeWorldStatePacket(true, SerializeHistory(), GetEditorText()));
    }

    private bool TryRunCommand(string commandLine, bool applyChange, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        string line = NormalizeCommand(commandLine);
        if (string.IsNullOrWhiteSpace(line))
        {
            feedback = string.Empty;
            return true;
        }

        if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal))
        {
            feedback = "Comment ignored.";
            return true;
        }

        string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            feedback = string.Empty;
            return true;
        }

        string command = tokens[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                feedback = "Commands: cube/box/sphere/ball/orb/ellipsoid/oval/capsule/cylinder/rectangle/circle/panel/plane, move, rotate, scale, color, delete, clear, list, plus simple for/while loops.";
                return true;

            case "list":
                feedback = spawnedObjects.Count == 0 ? "No objects yet." : "Objects: " + string.Join(", ", spawnedObjects.Keys);
                return true;

            case "cube":
            case "box":
            case "sphere":
            case "ball":
            case "orb":
            case "ellipsoid":
            case "oval":
            case "capsule":
            case "cylinder":
            case "rect":
            case "rectangle":
            case "panel":
            case "plane":
            case "circle":
            case "disc":
                return TryHandleSpawn(command, tokens, applyChange, out feedback, out mutatedWorld);

            case "move":
            case "setpos":
            case "position":
                return TryHandleAbsoluteTransform(tokens, applyChange, TransformMode.Position, out feedback, out mutatedWorld);

            case "rotate":
            case "setrot":
                return TryHandleAbsoluteTransform(tokens, applyChange, TransformMode.Rotation, out feedback, out mutatedWorld);

            case "scale":
            case "resize":
                return TryHandleAbsoluteTransform(tokens, applyChange, TransformMode.Scale, out feedback, out mutatedWorld);

            case "translate":
                return TryHandleRelativeTransform(tokens, applyChange, TransformMode.Position, out feedback, out mutatedWorld);

            case "turn":
                return TryHandleRelativeTransform(tokens, applyChange, TransformMode.Rotation, out feedback, out mutatedWorld);

            case "color":
                return TryHandleColor(tokens, applyChange, out feedback, out mutatedWorld);

            case "delete":
            case "destroy":
                return TryHandleDelete(tokens, applyChange, out feedback, out mutatedWorld);

            case "clear":
                feedback = "World cleared.";
                mutatedWorld = true;
                if (applyChange)
                {
                    ClearSpawnedObjects();
                }
                return true;

            default:
                feedback = "Unknown command: " + tokens[0];
                return false;
        }
    }

    private bool TryCompileScriptCommands(string script, out List<string> commands, out string feedback)
    {
        commands = new List<string>();
        List<string> statements = TokenizeScript(script);
        Dictionary<string, float> variables = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        return TryCompileStatements(statements, ref index, variables, commands, out feedback);
    }

    private bool TryCompileStatements(List<string> statements, ref int index, Dictionary<string, float> variables, List<string> commands, out string feedback)
    {
        while (index < statements.Count)
        {
            string statement = statements[index];
            if (string.IsNullOrWhiteSpace(statement))
            {
                index++;
                continue;
            }

            if (statement == "}")
            {
                index++;
                feedback = string.Empty;
                return true;
            }

            if (statement == "{")
            {
                index++;
                continue;
            }

            if (IsIgnorableScriptStatement(statement))
            {
                index++;
                continue;
            }

            if (TryParseLoopHeader(statement, "for", out string loopHeader))
            {
                index++;
                if (!TryExtractLoopBody(statements, ref index, out List<string> bodyStatements, out feedback))
                {
                    return false;
                }

                if (!TryCompileForLoop(loopHeader, bodyStatements, variables, commands, out feedback))
                {
                    return false;
                }

                continue;
            }

            if (TryParseLoopHeader(statement, "while", out loopHeader))
            {
                index++;
                if (!TryExtractLoopBody(statements, ref index, out List<string> bodyStatements, out feedback))
                {
                    return false;
                }

                if (!TryCompileWhileLoop(loopHeader, bodyStatements, variables, commands, out feedback))
                {
                    return false;
                }

                continue;
            }

            if (!TryExecuteVariableStatement(statement, variables, out bool handledVariable, out feedback))
            {
                return false;
            }

            if (handledVariable)
            {
                index++;
                continue;
            }

            if (TryCompileCommandStatement(statement, variables, out string compiledCommand, out feedback))
            {
                if (!string.IsNullOrWhiteSpace(compiledCommand))
                {
                    commands.Add(compiledCommand);
                }

                index++;
                continue;
            }

            return false;
        }

        feedback = string.Empty;
        return true;
    }

    private bool TryCompileForLoop(string header, List<string> bodyStatements, Dictionary<string, float> variables, List<string> commands, out string feedback)
    {
        string[] sections = SplitTopLevelSemicolons(header);
        if (sections.Length != 3)
        {
            feedback = "For syntax: for (int i = 0; i < 5; i++)";
            return false;
        }

        if (!TryExecuteVariableStatement(sections[0], variables, out _, out feedback))
        {
            return false;
        }

        int guard = 0;
        while (true)
        {
            if (++guard > MaxLoopIterations)
            {
                feedback = "Loop stopped after too many iterations.";
                return false;
            }

            if (!EvaluateConditionExpression(sections[1], variables, out bool condition, out feedback))
            {
                return false;
            }

            if (!condition)
            {
                break;
            }

            if (!TryCompileBodyStatements(bodyStatements, variables, commands, out feedback))
            {
                return false;
            }

            if (!TryExecuteVariableStatement(sections[2], variables, out _, out feedback))
            {
                return false;
            }
        }

        feedback = string.Empty;
        return true;
    }

    private bool TryCompileWhileLoop(string header, List<string> bodyStatements, Dictionary<string, float> variables, List<string> commands, out string feedback)
    {
        int guard = 0;
        while (true)
        {
            if (++guard > MaxLoopIterations)
            {
                feedback = "Loop stopped after too many iterations.";
                return false;
            }

            if (!EvaluateConditionExpression(header, variables, out bool condition, out feedback))
            {
                return false;
            }

            if (!condition)
            {
                break;
            }

            if (!TryCompileBodyStatements(bodyStatements, variables, commands, out feedback))
            {
                return false;
            }
        }

        feedback = string.Empty;
        return true;
    }

    private bool TryCompileBodyStatements(List<string> bodyStatements, Dictionary<string, float> variables, List<string> commands, out string feedback)
    {
        int bodyIndex = 0;
        return TryCompileStatements(bodyStatements, ref bodyIndex, variables, commands, out feedback);
    }

    private static List<string> TokenizeScript(string script)
    {
        List<string> statements = new List<string>();
        if (string.IsNullOrWhiteSpace(script))
        {
            return statements;
        }

        StringBuilder current = new StringBuilder();
        int parenDepth = 0;
        bool inString = false;
        string normalized = script.Replace("\r\n", "\n").Replace('\r', '\n');

        for (int i = 0; i < normalized.Length; i++)
        {
            char ch = normalized[i];
            if (ch == '"' && (i == 0 || normalized[i - 1] != '\\'))
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                {
                    parenDepth++;
                }
                else if (ch == ')')
                {
                    parenDepth = Mathf.Max(0, parenDepth - 1);
                }

                if (parenDepth == 0 && (ch == '{' || ch == '}'))
                {
                    AddScriptStatement(statements, current.ToString());
                    current.Length = 0;
                    statements.Add(ch.ToString());
                    continue;
                }

                if (parenDepth == 0 && (ch == ';' || ch == '\n'))
                {
                    AddScriptStatement(statements, current.ToString());
                    current.Length = 0;
                    continue;
                }
            }

            current.Append(ch);
        }

        AddScriptStatement(statements, current.ToString());
        return statements;
    }

    private static void AddScriptStatement(List<string> statements, string value)
    {
        if (statements == null)
        {
            return;
        }

        string trimmed = value != null ? value.Trim() : string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            statements.Add(trimmed);
        }
    }

    private static bool IsIgnorableScriptStatement(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return true;
        }

        string line = statement.Trim();
        return line == "{" ||
            line == "}" ||
            line == "};" ||
            line == "using UnityEngine;" ||
            line == "using UnityEngine" ||
            line.StartsWith("public ", StringComparison.Ordinal) ||
            line.StartsWith("private ", StringComparison.Ordinal) ||
            line.StartsWith("protected ", StringComparison.Ordinal) ||
            line.StartsWith("static ", StringComparison.Ordinal);
    }

    private static bool TryParseLoopHeader(string statement, string keyword, out string header)
    {
        header = string.Empty;
        string trimmed = statement != null ? statement.Trim() : string.Empty;
        if (!trimmed.StartsWith(keyword + "(", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith(keyword + " (", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int openParen = trimmed.IndexOf('(');
        int closeParen = trimmed.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
        {
            return false;
        }

        header = trimmed.Substring(openParen + 1, closeParen - openParen - 1).Trim();
        return true;
    }

    private static bool TryExtractLoopBody(List<string> statements, ref int index, out List<string> bodyStatements, out string feedback)
    {
        bodyStatements = new List<string>();
        feedback = string.Empty;
        if (index >= statements.Count)
        {
            feedback = "Loop body is missing.";
            return false;
        }

        if (statements[index] != "{")
        {
            bodyStatements.Add(statements[index]);
            index++;
            return true;
        }

        index++;
        int depth = 1;
        while (index < statements.Count)
        {
            string current = statements[index];
            index++;

            if (current == "{")
            {
                depth++;
                bodyStatements.Add(current);
                continue;
            }

            if (current == "}")
            {
                depth--;
                if (depth == 0)
                {
                    return true;
                }

                bodyStatements.Add(current);
                continue;
            }

            bodyStatements.Add(current);
        }

        feedback = "Missing } for loop body.";
        return false;
    }

    private bool TryExecuteVariableStatement(string statement, Dictionary<string, float> variables, out bool handledVariable, out string feedback)
    {
        handledVariable = false;
        feedback = string.Empty;
        if (variables == null || string.IsNullOrWhiteSpace(statement))
        {
            return true;
        }

        string line = statement.Trim();
        if (line.EndsWith("++", StringComparison.Ordinal) || line.EndsWith("--", StringComparison.Ordinal))
        {
            string variableName = line.Substring(0, line.Length - 2).Trim();
            if (!IsIdentifier(variableName))
            {
                feedback = "Invalid variable name: " + variableName;
                return false;
            }

            handledVariable = true;
            variables.TryGetValue(variableName, out float currentValue);
            variables[variableName] = currentValue + (line.EndsWith("++", StringComparison.Ordinal) ? 1f : -1f);
            return true;
        }

        Match compoundMatch = Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>\+=|-=)\s*(?<expr>.+)$");
        if (compoundMatch.Success)
        {
            handledVariable = true;
            string variableName = compoundMatch.Groups["name"].Value;
            string operation = compoundMatch.Groups["op"].Value;
            string expression = compoundMatch.Groups["expr"].Value;
            if (!TryEvaluateNumericExpression(expression, variables, out float delta, out feedback))
            {
                return false;
            }

            variables.TryGetValue(variableName, out float currentValue);
            variables[variableName] = currentValue + (operation == "+=" ? delta : -delta);
            return true;
        }

        Match assignmentMatch = Regex.Match(line, @"^(?:(?:int|float|var)\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<expr>.+)$");
        if (assignmentMatch.Success)
        {
            handledVariable = true;
            string variableName = assignmentMatch.Groups["name"].Value;
            string expression = assignmentMatch.Groups["expr"].Value;
            if (!TryEvaluateNumericExpression(expression, variables, out float result, out feedback))
            {
                return false;
            }

            variables[variableName] = result;
            return true;
        }

        return true;
    }

    private bool TryCompileCommandStatement(string statement, Dictionary<string, float> variables, out string compiledCommand, out string feedback)
    {
        compiledCommand = string.Empty;
        feedback = "Unsupported line: " + statement;
        string line = statement != null ? statement.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            feedback = string.Empty;
            return true;
        }

        string methodCommand = TryNormalizeMethodCall(line, variables, out feedback);
        if (!string.IsNullOrWhiteSpace(methodCommand))
        {
            compiledCommand = methodCommand;
            feedback = string.Empty;
            return true;
        }

        string substituted = SubstituteVariablesInPlainCommand(line, variables);
        string normalized = NormalizeCommand(substituted);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            compiledCommand = normalized;
            feedback = string.Empty;
            return true;
        }

        return false;
    }

    private static string[] SplitTopLevelSemicolons(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        List<string> results = new List<string>();
        StringBuilder current = new StringBuilder();
        int depth = 0;
        bool inString = false;

        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (ch == '"' && (i == 0 || value[i - 1] != '\\'))
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth = Mathf.Max(0, depth - 1);
                }
                else if (ch == ';' && depth == 0)
                {
                    results.Add(current.ToString().Trim());
                    current.Length = 0;
                    continue;
                }
            }

            current.Append(ch);
        }

        string tail = current.ToString().Trim();
        if (tail.Length > 0)
        {
            results.Add(tail);
        }

        return results.ToArray();
    }

    private static bool EvaluateConditionExpression(string expression, Dictionary<string, float> variables, out bool result, out string feedback)
    {
        result = false;
        feedback = string.Empty;
        string[] operators = { "<=", ">=", "==", "!=", "<", ">" };
        for (int i = 0; i < operators.Length; i++)
        {
            int opIndex = IndexOfTopLevelOperator(expression, operators[i]);
            if (opIndex < 0)
            {
                continue;
            }

            string left = expression.Substring(0, opIndex).Trim();
            string right = expression.Substring(opIndex + operators[i].Length).Trim();
            if (!TryEvaluateNumericExpression(left, variables, out float leftValue, out feedback) ||
                !TryEvaluateNumericExpression(right, variables, out float rightValue, out feedback))
            {
                return false;
            }

            switch (operators[i])
            {
                case "<=": result = leftValue <= rightValue; return true;
                case ">=": result = leftValue >= rightValue; return true;
                case "==": result = Mathf.Approximately(leftValue, rightValue); return true;
                case "!=": result = !Mathf.Approximately(leftValue, rightValue); return true;
                case "<": result = leftValue < rightValue; return true;
                case ">": result = leftValue > rightValue; return true;
            }
        }

        if (TryEvaluateNumericExpression(expression, variables, out float numericResult, out feedback))
        {
            result = Mathf.Abs(numericResult) > 0.0001f;
            return true;
        }

        return false;
    }

    private static int IndexOfTopLevelOperator(string expression, string op)
    {
        if (string.IsNullOrWhiteSpace(expression) || string.IsNullOrEmpty(op))
        {
            return -1;
        }

        int depth = 0;
        bool inString = false;
        for (int i = 0; i <= expression.Length - op.Length; i++)
        {
            char ch = expression[i];
            if (ch == '"' && (i == 0 || expression[i - 1] != '\\'))
            {
                inString = !inString;
            }

            if (inString)
            {
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth = Mathf.Max(0, depth - 1);
                continue;
            }

            if (depth == 0 && string.CompareOrdinal(expression, i, op, 0, op.Length) == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryEvaluateNumericExpression(string expression, Dictionary<string, float> variables, out float result, out string feedback)
    {
        result = 0f;
        feedback = string.Empty;
        try
        {
            NumericExpressionParser parser = new NumericExpressionParser(expression, variables);
            result = parser.Parse();
            return true;
        }
        catch (Exception ex)
        {
            feedback = "Bad expression: " + expression + " (" + ex.Message + ")";
            return false;
        }
    }

    private static bool TryEvaluateStringExpression(string expression, Dictionary<string, float> variables, out string result, out string feedback)
    {
        result = string.Empty;
        feedback = string.Empty;
        string[] pieces = SplitTopLevelPlus(expression);
        if (pieces.Length == 0)
        {
            return true;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i].Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            if (piece.Length >= 2 && piece.StartsWith("\"", StringComparison.Ordinal) && piece.EndsWith("\"", StringComparison.Ordinal))
            {
                builder.Append(piece.Substring(1, piece.Length - 2));
                continue;
            }

            if (variables != null && variables.TryGetValue(piece, out float variableValue))
            {
                builder.Append(FormatNumericLiteral(variableValue));
                continue;
            }

            if (!TryEvaluateNumericExpression(piece, variables, out float numericValue, out feedback))
            {
                return false;
            }

            builder.Append(FormatNumericLiteral(numericValue));
        }

        result = builder.ToString();
        return true;
    }

    private static string[] SplitTopLevelPlus(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Array.Empty<string>();
        }

        List<string> pieces = new List<string>();
        StringBuilder current = new StringBuilder();
        int depth = 0;
        bool inString = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char ch = expression[i];
            if (ch == '"' && (i == 0 || expression[i - 1] != '\\'))
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth = Mathf.Max(0, depth - 1);
                }
                else if (ch == '+' && depth == 0)
                {
                    pieces.Add(current.ToString());
                    current.Length = 0;
                    continue;
                }
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        return pieces.ToArray();
    }

    private static string FormatNumericLiteral(float value)
    {
        float rounded = Mathf.Round(value);
        if (Mathf.Abs(value - rounded) <= 0.0001f)
        {
            return ((int)rounded).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeCommand(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string line = value.Trim();
        if (line == "{" || line == "}" || line == "};" || line == "using UnityEngine;" || line.StartsWith("public ", StringComparison.Ordinal) || line.StartsWith("static ", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (line.EndsWith(";", StringComparison.Ordinal))
        {
            line = line.Substring(0, line.Length - 1).Trim();
        }

        string normalized = TryNormalizeMethodCall(line);
        return string.IsNullOrWhiteSpace(normalized) ? line : normalized;
    }

    private static string TryNormalizeMethodCall(string line)
    {
        int openParen = line.IndexOf('(');
        int closeParen = line.LastIndexOf(')');
        if (openParen <= 0 || closeParen <= openParen)
        {
            return line;
        }

        string methodName = line.Substring(0, openParen).Trim();
        int lastDot = methodName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            methodName = methodName.Substring(lastDot + 1);
        }

        string args = line.Substring(openParen + 1, closeParen - openParen - 1);
        string[] splitArgs = SplitTopLevelArguments(args);

        switch (methodName.ToLowerInvariant())
        {
            case "cube":
            case "box":
            case "sphere":
            case "ball":
            case "orb":
            case "ellipsoid":
            case "oval":
            case "capsule":
            case "cylinder":
            case "rect":
            case "rectangle":
            case "panel":
            case "plane":
            case "circle":
            case "disc":
                if (splitArgs.Length == 1)
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]);
                }
                if (splitArgs.Length == 2 && TryParseVectorExpression(splitArgs[1], out Vector3 spawnPos))
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]) + " " + FormatVector(spawnPos);
                }
                if (splitArgs.Length == 3 &&
                    TryParseVectorExpression(splitArgs[1], out spawnPos) &&
                    TryParseVectorExpression(splitArgs[2], out Vector3 spawnScale))
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]) + " " + FormatVector(spawnPos) + " " + FormatVector(spawnScale);
                }
                break;

            case "move":
            case "setpos":
            case "position":
            case "rotate":
            case "setrot":
            case "scale":
            case "resize":
            case "translate":
            case "turn":
                if (splitArgs.Length == 2 && TryParseVectorExpression(splitArgs[1], out Vector3 transformValue))
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]) + " " + FormatVector(transformValue);
                }
                break;

            case "color":
                if (splitArgs.Length == 2)
                {
                    return "color " + Unquote(splitArgs[0]) + " " + Unquote(splitArgs[1]);
                }
                break;

            case "delete":
            case "destroy":
                if (splitArgs.Length == 1)
                {
                    return "delete " + Unquote(splitArgs[0]);
                }
                break;

            case "clear":
            case "list":
            case "help":
                return methodName.ToLowerInvariant();
        }

        return line;
    }

    private static string TryNormalizeMethodCall(string line, Dictionary<string, float> variables, out string feedback)
    {
        feedback = string.Empty;
        int openParen = line.IndexOf('(');
        int closeParen = line.LastIndexOf(')');
        if (openParen <= 0 || closeParen <= openParen)
        {
            return string.Empty;
        }

        string methodName = line.Substring(0, openParen).Trim();
        int lastDot = methodName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            methodName = methodName.Substring(lastDot + 1);
        }

        string args = line.Substring(openParen + 1, closeParen - openParen - 1);
        string[] splitArgs = SplitTopLevelArguments(args);
        switch (methodName.ToLowerInvariant())
        {
            case "cube":
            case "box":
            case "sphere":
            case "ball":
            case "orb":
            case "ellipsoid":
            case "oval":
            case "capsule":
            case "cylinder":
            case "rect":
            case "rectangle":
            case "panel":
            case "plane":
            case "circle":
            case "disc":
                if (splitArgs.Length == 1 && TryEvaluateStringExpression(splitArgs[0], variables, out string spawnNameOnly, out feedback))
                {
                    return methodName.ToLowerInvariant() + " " + spawnNameOnly;
                }

                if (splitArgs.Length == 2 &&
                    TryEvaluateStringExpression(splitArgs[0], variables, out string spawnName, out feedback) &&
                    TryParseVectorExpression(splitArgs[1], variables, out Vector3 spawnPos, out feedback))
                {
                    return methodName.ToLowerInvariant() + " " + spawnName + " " + FormatVector(spawnPos);
                }

                if (splitArgs.Length == 3 &&
                    TryEvaluateStringExpression(splitArgs[0], variables, out spawnName, out feedback) &&
                    TryParseVectorExpression(splitArgs[1], variables, out spawnPos, out feedback) &&
                    TryParseVectorExpression(splitArgs[2], variables, out Vector3 spawnScale, out feedback))
                {
                    return methodName.ToLowerInvariant() + " " + spawnName + " " + FormatVector(spawnPos) + " " + FormatVector(spawnScale);
                }
                break;

            case "move":
            case "setpos":
            case "position":
            case "rotate":
            case "setrot":
            case "scale":
            case "resize":
            case "translate":
            case "turn":
                if (splitArgs.Length == 2 &&
                    TryEvaluateStringExpression(splitArgs[0], variables, out string targetName, out feedback) &&
                    TryParseVectorExpression(splitArgs[1], variables, out Vector3 transformValue, out feedback))
                {
                    return methodName.ToLowerInvariant() + " " + targetName + " " + FormatVector(transformValue);
                }
                break;

            case "color":
                if (splitArgs.Length == 2 &&
                    TryEvaluateStringExpression(splitArgs[0], variables, out string colorTargetName, out feedback) &&
                    TryEvaluateStringExpression(splitArgs[1], variables, out string colorValue, out feedback))
                {
                    return "color " + colorTargetName + " " + colorValue;
                }
                break;

            case "delete":
            case "destroy":
                if (splitArgs.Length == 1 && TryEvaluateStringExpression(splitArgs[0], variables, out string deleteName, out feedback))
                {
                    return "delete " + deleteName;
                }
                break;

            case "clear":
            case "list":
            case "help":
                return methodName.ToLowerInvariant();
        }

        feedback = "Unsupported command call: " + line;
        return string.Empty;
    }

    private static string SubstituteVariablesInPlainCommand(string line, Dictionary<string, float> variables)
    {
        if (string.IsNullOrWhiteSpace(line) || variables == null || variables.Count == 0)
        {
            return line;
        }

        string output = line;
        foreach (KeyValuePair<string, float> pair in variables)
        {
            output = Regex.Replace(output, $@"\b{Regex.Escape(pair.Key)}\b", FormatNumericLiteral(pair.Value));
        }

        return output;
    }

    private static string[] SplitTopLevelArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return Array.Empty<string>();
        }

        List<string> results = new List<string>();
        StringBuilder current = new StringBuilder();
        int depth = 0;
        bool inString = false;

        for (int i = 0; i < args.Length; i++)
        {
            char ch = args[i];
            if (ch == '"' && (i == 0 || args[i - 1] != '\\'))
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth = Mathf.Max(0, depth - 1);
                }
                else if (ch == ',' && depth == 0)
                {
                    results.Add(current.ToString().Trim());
                    current.Length = 0;
                    continue;
                }
            }

            current.Append(ch);
        }

        string tail = current.ToString().Trim();
        if (tail.Length > 0)
        {
            results.Add(tail);
        }

        return results.ToArray();
    }

    private static string Unquote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }

        return trimmed;
    }

    private static string FormatVector(Vector3 vector)
    {
        return vector.x.ToString("0.###", CultureInfo.InvariantCulture) + " "
            + vector.y.ToString("0.###", CultureInfo.InvariantCulture) + " "
            + vector.z.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryParseVectorExpression(string value, out Vector3 vector)
    {
        vector = Vector3.zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value.Trim();
        int openParen = text.IndexOf('(');
        int closeParen = text.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
        {
            return false;
        }

        string inside = text.Substring(openParen + 1, closeParen - openParen - 1);
        string[] parts = inside.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            parts = inside.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }
        }

        if (!TryParseFloat(parts[0].Replace("f", string.Empty).Trim(), out float x) ||
            !TryParseFloat(parts[1].Replace("f", string.Empty).Trim(), out float y) ||
            !TryParseFloat(parts[2].Replace("f", string.Empty).Trim(), out float z))
        {
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseVectorExpression(string value, Dictionary<string, float> variables, out Vector3 vector, out string feedback)
    {
        vector = Vector3.zero;
        feedback = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            feedback = "Vector is empty.";
            return false;
        }

        string text = value.Trim();
        int openParen = text.IndexOf('(');
        int closeParen = text.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
        {
            feedback = "Vector syntax should look like new Vector3(x, y, z).";
            return false;
        }

        string inside = text.Substring(openParen + 1, closeParen - openParen - 1);
        string[] parts = SplitTopLevelArguments(inside);
        if (parts.Length != 3)
        {
            feedback = "Vector needs x, y, z values.";
            return false;
        }

        if (!TryEvaluateNumericExpression(parts[0], variables, out float x, out feedback) ||
            !TryEvaluateNumericExpression(parts[1], variables, out float y, out feedback) ||
            !TryEvaluateNumericExpression(parts[2], variables, out float z, out feedback))
        {
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private bool TryHandleSpawn(string command, string[] tokens, bool applyChange, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        if (tokens.Length != 2 && tokens.Length != 5 && tokens.Length != 8)
        {
            feedback = "Spawn syntax: sphere name [x y z] [sx sy sz]";
            return false;
        }

        string objectName = tokens[1];
        if (!IsValidObjectName(objectName))
        {
            feedback = "Use letters, numbers, _ or - in object names.";
            return false;
        }

        Vector3 position = GetDefaultSpawnPoint();
        if (tokens.Length >= 5 && !TryParseVector3(tokens, 2, out position))
        {
            feedback = "Invalid position. Use numbers like 0 1 0.";
            return false;
        }

        Vector3 scale = GetDefaultSpawnScale(command);
        if (tokens.Length == 8 && !TryParseVector3(tokens, 5, out scale))
        {
            feedback = "Invalid size. Use numbers like 1 1 1.";
            return false;
        }

        feedback = "Spawned " + command + " " + objectName + ".";
        mutatedWorld = true;
        if (!applyChange)
        {
            return true;
        }

        EnsureWorld();
        if (spawnedObjects.TryGetValue(objectName, out GameObject existing) && existing != null)
        {
            Destroy(existing);
        }

        PrimitiveType primitiveType = ResolveSpawnPrimitiveType(command);

        GameObject created = GameObject.CreatePrimitive(primitiveType);
        created.name = objectName;
        created.transform.SetParent(dynamicObjectsRoot.transform, false);
        created.transform.position = position;
        created.transform.rotation = Quaternion.identity;
        created.transform.localScale = scale;
        ApplyMaterial(created, GetColorFromName("white"));
        spawnedObjects[objectName] = created;
        return true;
    }

    private static PrimitiveType ResolveSpawnPrimitiveType(string command)
    {
        return command switch
        {
            "sphere" => PrimitiveType.Sphere,
            "ball" => PrimitiveType.Sphere,
            "orb" => PrimitiveType.Sphere,
            "ellipsoid" => PrimitiveType.Sphere,
            "oval" => PrimitiveType.Sphere,
            "capsule" => PrimitiveType.Capsule,
            "cylinder" => PrimitiveType.Cylinder,
            "circle" => PrimitiveType.Cylinder,
            "disc" => PrimitiveType.Cylinder,
            "plane" => PrimitiveType.Cube,
            "panel" => PrimitiveType.Cube,
            "rect" => PrimitiveType.Cube,
            "rectangle" => PrimitiveType.Cube,
            _ => PrimitiveType.Cube,
        };
    }

    private static Vector3 GetDefaultSpawnScale(string command)
    {
        return command switch
        {
            "ball" => new Vector3(1.15f, 1.15f, 1.15f),
            "orb" => new Vector3(1.4f, 1.4f, 1.4f),
            "ellipsoid" => new Vector3(1.8f, 1.1f, 1.2f),
            "oval" => new Vector3(1.8f, 1.1f, 1.2f),
            "circle" => new Vector3(1.5f, 0.15f, 1.5f),
            "disc" => new Vector3(1.5f, 0.15f, 1.5f),
            "rect" => new Vector3(2.5f, 1f, 0.35f),
            "rectangle" => new Vector3(2.5f, 1f, 0.35f),
            "panel" => new Vector3(2.5f, 2f, 0.2f),
            "plane" => new Vector3(4f, 0.2f, 4f),
            _ => Vector3.one,
        };
    }

    private bool TryHandleAbsoluteTransform(string[] tokens, bool applyChange, TransformMode mode, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        if (tokens.Length != 5)
        {
            feedback = mode switch
            {
                TransformMode.Position => "Move syntax: move name x y z",
                TransformMode.Rotation => "Rotate syntax: rotate name x y z",
                _ => "Scale syntax: scale name x y z",
            };
            return false;
        }

        if (!TryGetObject(tokens[1], out GameObject target, applyChange, out feedback))
        {
            return false;
        }

        if (!TryParseVector3(tokens, 2, out Vector3 value))
        {
            feedback = "Use numeric x y z values.";
            return false;
        }

        feedback = mode switch
        {
            TransformMode.Position => "Moved " + tokens[1] + ".",
            TransformMode.Rotation => "Rotated " + tokens[1] + ".",
            _ => "Scaled " + tokens[1] + ".",
        };
        mutatedWorld = true;

        if (!applyChange)
        {
            return true;
        }

        switch (mode)
        {
            case TransformMode.Position:
                target.transform.position = value;
                break;
            case TransformMode.Rotation:
                target.transform.rotation = Quaternion.Euler(value);
                break;
            case TransformMode.Scale:
                target.transform.localScale = value;
                break;
        }

        return true;
    }

    private bool TryHandleRelativeTransform(string[] tokens, bool applyChange, TransformMode mode, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        if (tokens.Length != 5)
        {
            feedback = mode == TransformMode.Position ? "Translate syntax: translate name dx dy dz" : "Turn syntax: turn name dx dy dz";
            return false;
        }

        if (!TryGetObject(tokens[1], out GameObject target, applyChange, out feedback))
        {
            return false;
        }

        if (!TryParseVector3(tokens, 2, out Vector3 delta))
        {
            feedback = "Use numeric x y z values.";
            return false;
        }

        feedback = mode == TransformMode.Position ? "Translated " + tokens[1] + "." : "Turned " + tokens[1] + ".";
        mutatedWorld = true;

        if (!applyChange)
        {
            return true;
        }

        if (mode == TransformMode.Position)
        {
            target.transform.position += delta;
        }
        else
        {
            target.transform.rotation = Quaternion.Euler(target.transform.eulerAngles + delta);
        }

        return true;
    }

    private bool TryHandleColor(string[] tokens, bool applyChange, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        if (tokens.Length != 3 && tokens.Length != 5 && tokens.Length != 6)
        {
            feedback = "Color syntax: color name red OR color name 255 0 0";
            return false;
        }

        if (!TryGetObject(tokens[1], out GameObject target, applyChange, out feedback))
        {
            return false;
        }

        if (!TryParseColor(tokens, 2, out Color color))
        {
            feedback = "Color must be a name, #hex, or RGB numbers.";
            return false;
        }

        feedback = "Colored " + tokens[1] + ".";
        mutatedWorld = true;
        if (!applyChange)
        {
            return true;
        }

        ApplyMaterial(target, color);
        return true;
    }

    private bool TryHandleDelete(string[] tokens, bool applyChange, out string feedback, out bool mutatedWorld)
    {
        mutatedWorld = false;
        if (tokens.Length != 2)
        {
            feedback = "Delete syntax: delete name";
            return false;
        }

        if (!TryGetObject(tokens[1], out GameObject target, applyChange, out feedback))
        {
            return false;
        }

        feedback = "Deleted " + tokens[1] + ".";
        mutatedWorld = true;
        if (!applyChange)
        {
            return true;
        }

        spawnedObjects.Remove(tokens[1]);
        Destroy(target);
        return true;
    }

    private bool TryGetObject(string objectName, out GameObject target, bool requireExistingSceneObject, out string feedback)
    {
        target = null;
        if (!spawnedObjects.TryGetValue(objectName, out GameObject found) || found == null)
        {
            feedback = "Object not found: " + objectName;
            return false;
        }

        target = found;
        feedback = string.Empty;
        return true;
    }

    private void ClearSpawnedObjects()
    {
        foreach (GameObject value in spawnedObjects.Values)
        {
            if (value != null)
            {
                Destroy(value);
            }
        }

        spawnedObjects.Clear();
    }

    private static bool TryParseVector3(string[] tokens, int startIndex, out Vector3 vector)
    {
        vector = Vector3.zero;
        if (tokens.Length <= startIndex + 2)
        {
            return false;
        }

        if (!TryParseFloat(tokens[startIndex], out float x) ||
            !TryParseFloat(tokens[startIndex + 1], out float y) ||
            !TryParseFloat(tokens[startIndex + 2], out float z))
        {
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private bool TryParseColor(string[] tokens, int startIndex, out Color color)
    {
        color = Color.white;
        if (tokens.Length == startIndex + 1)
        {
            return TryParseNamedColor(tokens[startIndex], out color);
        }

        if (tokens.Length != startIndex + 3 && tokens.Length != startIndex + 4)
        {
            return false;
        }

        if (!TryParseFloat(tokens[startIndex], out float r) ||
            !TryParseFloat(tokens[startIndex + 1], out float g) ||
            !TryParseFloat(tokens[startIndex + 2], out float b))
        {
            return false;
        }

        float a = 255f;
        if (tokens.Length == startIndex + 4 && !TryParseFloat(tokens[startIndex + 3], out a))
        {
            return false;
        }

        bool isByteRange = r > 1f || g > 1f || b > 1f || a > 1f;
        color = isByteRange
            ? new Color(r / 255f, g / 255f, b / 255f, a / 255f)
            : new Color(r, g, b, a);
        return true;
    }

    private static bool TryParseNamedColor(string value, out Color color)
    {
        if (ColorUtility.TryParseHtmlString(value, out color))
        {
            return true;
        }

        color = GetColorFromName(value);
        return color.a > 0f || string.Equals(value, "black", StringComparison.OrdinalIgnoreCase);
    }

    private static Color GetColorFromName(string value)
    {
        switch (value.ToLowerInvariant())
        {
            case "red": return new Color(0.92f, 0.28f, 0.28f);
            case "green": return new Color(0.2f, 0.82f, 0.34f);
            case "blue": return new Color(0.25f, 0.56f, 0.95f);
            case "yellow": return new Color(0.96f, 0.82f, 0.24f);
            case "orange": return new Color(0.96f, 0.55f, 0.2f);
            case "purple": return new Color(0.62f, 0.34f, 0.92f);
            case "cyan": return new Color(0.18f, 0.84f, 0.88f);
            case "magenta": return new Color(0.92f, 0.2f, 0.76f);
            case "black": return Color.black;
            case "gray":
            case "grey": return Color.gray;
            case "white": return Color.white;
            default: return new Color(0f, 0f, 0f, 0f);
        }
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsValidObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (!char.IsLetterOrDigit(current) && current != '_' && current != '-')
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 GetDefaultSpawnPoint()
    {
        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player != null)
        {
            return player.position + player.forward * 4f + Vector3.up * 0.5f;
        }

        return BuildSpawn + new Vector3(0f, 1f, 0f);
    }

    private static void ApplyMaterial(GameObject target, Color color)
    {
        if (target == null)
        {
            return;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        renderer.sharedMaterial = material;
    }

    private void EnableNoclip(bool enabled)
    {
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.SetNoclipEnabled(enabled);
        }
    }

    private void TeleportLocalPlayer(Vector3 position, Quaternion rotation)
    {
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.TeleportTo(position, rotation);
            return;
        }

        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player != null)
        {
            player.SetPositionAndRotation(position, rotation);
        }
    }

    private string SerializeHistory()
    {
        if (commandHistory.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < commandHistory.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(commandHistory[i]);
        }

        return builder.ToString();
    }

    private void BuildEditorUi()
    {
        if (editorCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("CodeWorldCanvas");
        canvasObject.transform.SetParent(transform, false);

        editorCanvas = canvasObject.AddComponent<Canvas>();
        editorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        editorCanvas.sortingOrder = 11050;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        editorCanvasRaycaster = canvasObject.AddComponent<GraphicRaycaster>();

        editorPanel = CreateUiObject("EditorPanel", editorCanvas.transform, new Vector2(640f, 560f), Vector2.zero);
        editorPanelRect = editorPanel.GetComponent<RectTransform>();
        editorPanel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.94f);
        Outline outline = editorPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.62f, 0.95f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject titleBar = CreateUiObject("TitleBar", editorPanel.transform, new Vector2(640f, 64f), new Vector2(0f, 248f));
        Image titleBarImage = titleBar.AddComponent<Image>();
        titleBarImage.color = new Color(0.08f, 0.12f, 0.24f, 1f);
        titleBarImage.raycastTarget = true;
        titleBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        Text titleText = CreateText("Title", titleBar.transform, "YOUR CODE CONTROLS THE WORLD", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(560f, 34f));
        titleText.raycastTarget = false;
        DraggableWindowHandle dragHandle = titleBar.AddComponent<DraggableWindowHandle>();
        dragHandle.Configure(editorPanelRect);

        stopButton = CreateButton("StopButton", editorPanel.transform, "STOP", new Vector2(114f, -245f), new Vector2(70f, 38f), new Color(0.8f, 0.28f, 0.23f, 1f), 12);
        stopButton.onClick.AddListener(() => StopLocalScriptExecution(true));

        runButton = CreateButton("RunButton", editorPanel.transform, "RUN", new Vector2(196f, -245f), new Vector2(70f, 38f), new Color(0.18f, 0.68f, 0.32f, 1f), 12);
        runButton.onClick.AddListener(() => ExecuteLocalScript(editorInput != null ? editorInput.text : string.Empty));

        aiOpenButton = CreateButton("AiOpenButton", editorPanel.transform, "AI", new Vector2(278f, -245f), new Vector2(70f, 38f), new Color(0.16f, 0.52f, 0.82f, 1f), 12);
        aiOpenButton.onClick.AddListener(() => UpdateAiVisibility(true));

        Text helpText = CreateText("Help", editorPanel.transform, "Press Ctrl+Enter to run Python on the server. Press Esc or STOP to stop waiting. Press ` to hide. Example:", 15, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.75f, 0.86f, 1f), new Vector2(-2f, 184f), new Vector2(580f, 28f));
        helpText.raycastTarget = false;

        GameObject codeViewport = CreateUiObject("CodeViewport", editorPanel.transform, new Vector2(590f, 300f), new Vector2(0f, 10f));
        editorViewportRect = codeViewport.GetComponent<RectTransform>();
        codeViewport.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.21f, 0.98f);
        Outline codeOutline = codeViewport.AddComponent<Outline>();
        codeOutline.effectColor = new Color(0.35f, 0.72f, 0.95f, 0.55f);
        codeOutline.effectDistance = new Vector2(1f, -1f);

        Mask codeMask = codeViewport.AddComponent<Mask>();
        codeMask.showMaskGraphic = false;

        editorScrollRect = codeViewport.AddComponent<ScrollRect>();
        editorScrollRect.horizontal = false;
        editorScrollRect.vertical = true;
        editorScrollRect.scrollSensitivity = 24f;
        editorScrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject codeContent = CreateUiObject("CodeContent", codeViewport.transform, new Vector2(554f, 360f), Vector2.zero);
        editorContentRect = codeContent.GetComponent<RectTransform>();
        editorContentRect.anchorMin = new Vector2(0f, 1f);
        editorContentRect.anchorMax = new Vector2(1f, 1f);
        editorContentRect.pivot = new Vector2(0.5f, 1f);
        editorContentRect.anchoredPosition = new Vector2(0f, -12f);
        editorContentRect.sizeDelta = new Vector2(-24f, 360f);

        editorInput = codeViewport.AddComponent<InputField>();
        editorInput.targetGraphic = codeViewport.GetComponent<Image>();
        editorInput.lineType = InputField.LineType.MultiLineNewline;
        editorInput.text = BuildStarterScript();
        lastEditorTrackedText = editorInput.text;
        editorUndoStack.Clear();

        Text codeText = CreateText("CodeInputText", codeContent.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, Vector2.zero, new Vector2(530f, 320f));
        codeText.raycastTarget = false;
        RectTransform codeTextRect = codeText.rectTransform;
        codeTextRect.anchorMin = Vector2.zero;
        codeTextRect.anchorMax = Vector2.one;
        codeTextRect.offsetMin = new Vector2(12f, 10f);
        codeTextRect.offsetMax = new Vector2(-12f, -10f);
        codeText.fontSize = 18;

        Text codeHint = CreateText("CodePlaceholder", codeContent.transform, "Type Python code here...", 16, FontStyle.Italic, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, new Vector2(530f, 320f));
        codeHint.raycastTarget = false;
        RectTransform codeHintRect = codeHint.rectTransform;
        codeHintRect.anchorMin = Vector2.zero;
        codeHintRect.anchorMax = Vector2.one;
        codeHintRect.offsetMin = new Vector2(12f, 10f);
        codeHintRect.offsetMax = new Vector2(-12f, -10f);
        codeHint.fontSize = 16;

        editorInput.textComponent = codeText;
        editorInput.placeholder = codeHint;
        editorScrollRect.viewport = editorViewportRect;
        editorScrollRect.content = editorContentRect;
        editorScrollRect.inertia = false;
        editorInput.onValueChanged.AddListener(HandleEditorInputChanged);
        RefreshEditorLayout();

        statusText = CreateText("Status", editorPanel.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.98f, 0.86f, 0.34f), new Vector2(-128f, -158f), new Vector2(340f, 42f));
        statusText.raycastTarget = false;

        GameObject historyViewport = CreateUiObject("HistoryViewport", editorPanel.transform, new Vector2(340f, 82f), new Vector2(-128f, -226f));
        historyViewportRect = historyViewport.GetComponent<RectTransform>();
        historyViewport.AddComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.9f);
        Outline historyOutline = historyViewport.AddComponent<Outline>();
        historyOutline.effectColor = new Color(0.25f, 0.62f, 0.95f, 0.35f);
        historyOutline.effectDistance = new Vector2(1f, -1f);
        Mask historyMask = historyViewport.AddComponent<Mask>();
        historyMask.showMaskGraphic = false;

        historyScrollRect = historyViewport.AddComponent<ScrollRect>();
        historyScrollRect.horizontal = false;
        historyScrollRect.vertical = true;
        historyScrollRect.scrollSensitivity = 24f;
        historyScrollRect.movementType = ScrollRect.MovementType.Clamped;
        historyScrollRect.inertia = false;

        GameObject historyContent = CreateUiObject("HistoryContent", historyViewport.transform, new Vector2(312f, 96f), Vector2.zero);
        historyContentRect = historyContent.GetComponent<RectTransform>();
        historyContentRect.anchorMin = new Vector2(0f, 1f);
        historyContentRect.anchorMax = new Vector2(1f, 1f);
        historyContentRect.pivot = new Vector2(0.5f, 1f);
        historyContentRect.anchoredPosition = new Vector2(0f, -8f);
        historyContentRect.sizeDelta = new Vector2(-20f, 96f);

        historyText = CreateText("History", historyContent.transform, "History: none", 14, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.66f, 0.8f, 0.96f), Vector2.zero, new Vector2(300f, 78f));
        historyText.raycastTarget = false;
        RectTransform historyTextRect = historyText.rectTransform;
        historyTextRect.anchorMin = Vector2.zero;
        historyTextRect.anchorMax = Vector2.one;
        historyTextRect.offsetMin = new Vector2(10f, 8f);
        historyTextRect.offsetMax = new Vector2(-10f, -8f);
        historyScrollRect.viewport = historyViewportRect;
        historyScrollRect.content = historyContentRect;
        AttachScrollWheelForwarder(historyViewport, historyScrollRect);

        editorHintText = CreateScreenCornerText("EditorHint", editorCanvas.transform, "Press ` to show the code window.", 19, FontStyle.Bold, TextAnchor.LowerLeft, new Color(0.98f, 0.9f, 0.42f, 0.98f), new Vector2(34f, 28f), new Vector2(520f, 40f));
        editorHintText.raycastTarget = false;
        editorHintText.gameObject.SetActive(false);

        SetupVrPointer();
        BuildAiUi();
        BuildChallengeUi();
        UpdateEditorCanvasMode(true);
    }

    private void UpdateEditorVisibility(bool visible)
    {
        editorVisible = visible && modeActive;
        if (editorPanel != null)
        {
            editorPanel.SetActive(editorVisible);
        }

        UpdateEditorCanvasMode(editorVisible);

        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            bool vrActive = XRSettings.enabled;
            fps.SetMovementLocked(editorVisible && !vrActive);
            fps.SetCameraControlEnabled(vrActive || !editorVisible);
        }

        Cursor.visible = editorVisible;
        Cursor.lockState = editorVisible ? CursorLockMode.None : CursorLockMode.Locked;

        if (editorVisible && editorInput != null)
        {
            if (!editorHintDismissed)
            {
                editorHintDismissed = true;
                UpdateEditorHintVisibility();
            }

            if (string.IsNullOrWhiteSpace(editorInput.text))
            {
                suppressEditorTracking = true;
                editorInput.text = BuildStarterScript();
                suppressEditorTracking = false;
                lastEditorTrackedText = editorInput.text;
            }

            editorInput.ActivateInputField();
            editorInput.Select();
            RefreshEditorLayout();
            ScrollEditorToTop();

            if (IsQuestVrCodeIslandUi())
            {
                FocusVrInputField(editorInput);
            }
        }
        else if (!editorVisible)
        {
            ClearVrInputFocus();
        }

        UpdateAiVisibility(aiVisible);
        UpdateChallengeVisibility();
        UpdateRemoteCursorVisibility();
    }

    private void SetupVrPointer()
    {
        EnsureEventSystem();
        editorCanvasRaycaster = editorCanvas != null ? editorCanvas.GetComponent<GraphicRaycaster>() : editorCanvasRaycaster;

        if (editorCanvas == null)
        {
            return;
        }

        if (vrCursor == null)
        {
            vrCursor = CreateUiObject("CodeWorldVrCursor", editorCanvas.transform, VrCursorHoverSize, Vector2.zero);
            vrCursorRect = vrCursor.GetComponent<RectTransform>();
            vrCursorRect.anchorMin = new Vector2(0.5f, 0.5f);
            vrCursorRect.anchorMax = new Vector2(0.5f, 0.5f);
            vrCursorRect.pivot = new Vector2(0.5f, 0.5f);
            vrCursorRect.sizeDelta = VrCursorHoverSize;

            vrCursorImage = vrCursor.AddComponent<Image>();
            vrCursorImage.sprite = GetVrCursorSprite();
            vrCursorImage.color = new Color(1f, 0.96f, 0.35f, 1f);
            vrCursorImage.raycastTarget = false;

            vrCursorOutline = vrCursor.AddComponent<Outline>();
            vrCursorOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            vrCursorOutline.effectDistance = new Vector2(2f, -2f);

            vrCursor.SetActive(false);
        }
        else
        {
            vrCursorRect = vrCursor.GetComponent<RectTransform>();
            vrCursorImage = vrCursor.GetComponent<Image>();
            vrCursorOutline = vrCursor.GetComponent<Outline>();
        }
    }

    private static Sprite GetVrCursorSprite()
    {
        if (vrCursorSprite != null)
        {
            return vrCursorSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        const int size = 64;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.34f;
        float innerRadius = size * 0.24f;
        float coreRadius = size * 0.08f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ringAlpha = 0f;
                if (distance <= outerRadius && distance >= innerRadius)
                {
                    float outerFade = 1f - Mathf.InverseLerp(outerRadius - 3f, outerRadius, distance);
                    float innerFade = Mathf.InverseLerp(innerRadius, innerRadius + 3f, distance);
                    ringAlpha = Mathf.Clamp01(Mathf.Min(outerFade, innerFade));
                }

                float coreAlpha = distance <= coreRadius
                    ? 1f - Mathf.InverseLerp(coreRadius - 2f, coreRadius, distance)
                    : 0f;

                float alpha = Mathf.Clamp01(Mathf.Max(ringAlpha * 0.95f, coreAlpha));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        vrCursorSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        return vrCursorSprite;
    }

    private void UpdateEditorCanvasMode(bool snap)
    {
        if (editorCanvas == null)
        {
            return;
        }

        if (IsQuestVrCodeIslandUi())
        {
            Camera targetCamera = GetEditorCamera();
            if (targetCamera == null)
            {
                return;
            }

            if (editorCanvas.renderMode != RenderMode.WorldSpace)
            {
                editorCanvas.renderMode = RenderMode.WorldSpace;
            }

            editorCanvas.worldCamera = targetCamera;
            RectTransform canvasRect = editorCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.localScale = VrEditorCanvasScale;
            }

            if (snap)
            {
                UpdateVrEditorCanvasPlacement(targetCamera, true);
            }
            return;
        }

        if (editorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            editorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        editorCanvas.worldCamera = null;
        RectTransform rectTransform = editorCanvas.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }

    private void UpdateVrEditorCanvasPlacement(Camera targetCamera, bool snap)
    {
        if (editorCanvas == null || targetCamera == null || editorCanvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        Transform camTransform = targetCamera.transform;
        Vector3 forward = camTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = camTransform.forward;
        }

        forward.Normalize();
        Vector3 desiredPosition = camTransform.position + forward * VrEditorCanvasDistance;
        desiredPosition.y = camTransform.position.y + VrEditorCanvasHeightOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(desiredPosition - camTransform.position, Vector3.up);

        Transform canvasTransform = editorCanvas.transform;
        if (snap)
        {
            canvasTransform.position = desiredPosition;
            canvasTransform.rotation = desiredRotation;
            return;
        }

        float moveT = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        float rotateT = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        canvasTransform.position = Vector3.Lerp(canvasTransform.position, desiredPosition, moveT);
        canvasTransform.rotation = Quaternion.Slerp(canvasTransform.rotation, desiredRotation, rotateT);
    }

    private static bool IsQuestVrCodeIslandUi()
    {
        return Application.platform == RuntimePlatform.Android && XRSettings.enabled && XRSettings.isDeviceActive;
    }

    private Camera GetEditorCamera()
    {
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            Camera fpsCamera = fps.GetComponentInChildren<Camera>(true);
            if (fpsCamera != null)
            {
                return fpsCamera;
            }
        }

        return Camera.main;
    }

    private void UpdateVrEditorPointer()
    {
        if (!editorVisible || editorCanvas == null || editorCanvas.renderMode != RenderMode.WorldSpace || !IsQuestVrCodeIslandUi())
        {
            ResetVrPointerState();
            return;
        }

        if (!TryGetRightControllerRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            ResetVrPointerState();
            return;
        }

        RectTransform canvasRect = editorCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            ResetVrPointerState();
            return;
        }

        Plane canvasPlane = new Plane(-editorCanvas.transform.forward, editorCanvas.transform.position);
        if (!canvasPlane.Raycast(new Ray(rayOrigin, rayDirection), out float distanceToPlane) ||
            distanceToPlane < 0f ||
            distanceToPlane > VrPointerMaxDistance)
        {
            HideVrCursor();
            ClearVrHover();
            HandleVrSelectReleaseIfNeeded();
            return;
        }

        Vector3 hitPoint = rayOrigin + (rayDirection * distanceToPlane);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(editorCanvas.worldCamera, hitPoint);
        if (!IsWorldPointInsideCanvas(canvasRect, hitPoint))
        {
            HideVrCursor();
            ClearVrHover();
            HandleVrSelectReleaseIfNeeded();
            return;
        }

        EnsureVrPointerEventData();
        if (vrPointerEventData == null || editorCanvasRaycaster == null)
        {
            ResetVrPointerState();
            return;
        }

        vrPointerEventData.Reset();
        vrPointerEventData.button = PointerEventData.InputButton.Left;
        vrPointerEventData.position = screenPoint;
        vrPointerEventData.pointerCurrentRaycast = default;
        vrRaycastResults.Clear();
        editorCanvasRaycaster.Raycast(vrPointerEventData, vrRaycastResults);

        RaycastResult raycastResult = FindFirstInteractiveRaycast(vrRaycastResults);
        GameObject hitObject = raycastResult.gameObject;

        UpdateVrHover(hitObject);
        vrPointerEventData.pointerCurrentRaycast = raycastResult;

        bool selectPressed = IsVrSelectPressed();
        bool selectPressedThisFrame = selectPressed && !vrSelectWasPressed;
        bool selectReleasedThisFrame = !selectPressed && vrSelectWasPressed;
        vrSelectWasPressed = selectPressed;

        InputField hitInputField = hitObject != null ? hitObject.GetComponentInParent<InputField>() : null;

        if (selectPressedThisFrame && hitObject != null)
        {
            vrPressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) ?? hitObject;
            vrPointerEventData.eligibleForClick = true;
            vrPointerEventData.pointerPress = vrPressedObject;
            vrPointerEventData.rawPointerPress = hitObject;
            vrPointerEventData.pressPosition = screenPoint;
            vrPointerEventData.pointerPressRaycast = raycastResult;
            vrPointerEventData.delta = Vector2.zero;
            vrPointerEventData.dragging = false;
            vrPointerEventData.useDragThreshold = true;
            vrPointerEventData.clickCount = 1;
            vrPointerEventData.clickTime = Time.unscaledTime;
            ExecuteEvents.Execute(vrPressedObject, vrPointerEventData, ExecuteEvents.pointerDownHandler);

            if (hitInputField != null)
            {
                FocusVrInputField(hitInputField);
                SetVrInputCaretFromScreenPoint(hitInputField, screenPoint);
                hitInputField.OnPointerDown(vrPointerEventData);
            }
            else
            {
                ClearVrInputFocus();
            }
        }

        if (selectReleasedThisFrame)
        {
            HandleVrSelectRelease(hitObject, hitInputField);
        }

        GameObject currentClickHandler = hitObject != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) ?? hitObject : null;
        bool isPressingCurrent = selectPressed && currentClickHandler != null && currentClickHandler == vrPressedObject;
        ShowVrCursor(hitPoint, isPressingCurrent);
    }

    private void EnsureVrPointerEventData()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            EnsureEventSystem();
            eventSystem = EventSystem.current;
        }

        if (eventSystem == null)
        {
            return;
        }

        if (vrPointerEventData == null || vrPointerEventSystem != eventSystem)
        {
            vrPointerEventSystem = eventSystem;
            vrPointerEventData = new PointerEventData(eventSystem)
            {
                pointerId = -10
            };
        }
    }

    private void ShowVrCursor(Vector3 position, bool isSelecting)
    {
        if (vrCursor == null || vrCursorRect == null || editorCanvas == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                editorCanvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(editorCanvas.worldCamera, position),
                editorCanvas.worldCamera,
                out Vector2 localPoint))
        {
            vrCursor.SetActive(false);
            return;
        }

        vrCursor.SetActive(true);
        vrCursor.transform.SetAsLastSibling();
        if (!vrCursorHasSmoothedPosition)
        {
            vrCursorSmoothedAnchoredPosition = localPoint;
            vrCursorVelocity = Vector2.zero;
            vrCursorHasSmoothedPosition = true;
        }
        else
        {
            vrCursorSmoothedAnchoredPosition = Vector2.SmoothDamp(
                vrCursorSmoothedAnchoredPosition,
                localPoint,
                ref vrCursorVelocity,
                VrCursorSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
        }

        vrCursorRect.anchoredPosition = vrCursorSmoothedAnchoredPosition;
        vrCursorRect.sizeDelta = isSelecting ? VrCursorPressedSize : VrCursorHoverSize;

        if (vrCursorImage != null)
        {
            vrCursorImage.color = isSelecting
                ? new Color(1f, 0.48f, 0.16f, 0.98f)
                : new Color(0.98f, 0.95f, 0.78f, 0.96f);
        }
    }

    private void HideVrCursor()
    {
        if (vrCursor != null)
        {
            vrCursor.SetActive(false);
        }

        vrCursorHasSmoothedPosition = false;
        vrCursorVelocity = Vector2.zero;
    }

    private void UpdateVrHover(GameObject hitObject)
    {
        GameObject nextHover = hitObject != null
            ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hitObject) ?? hitObject
            : null;

        if (vrHoveredObject == nextHover)
        {
            if (vrHoveredObject != null && vrPointerEventData != null)
            {
                ExecuteEvents.Execute(vrHoveredObject, vrPointerEventData, ExecuteEvents.pointerMoveHandler);
            }
            return;
        }

        if (vrHoveredObject != null && vrPointerEventData != null)
        {
            ExecuteEvents.Execute(vrHoveredObject, vrPointerEventData, ExecuteEvents.pointerExitHandler);
        }

        vrHoveredObject = nextHover;

        if (vrHoveredObject != null && vrPointerEventData != null)
        {
            ExecuteEvents.Execute(vrHoveredObject, vrPointerEventData, ExecuteEvents.pointerEnterHandler);
        }
    }

    private void ClearVrHover()
    {
        if (vrHoveredObject != null && vrPointerEventData != null)
        {
            ExecuteEvents.Execute(vrHoveredObject, vrPointerEventData, ExecuteEvents.pointerExitHandler);
        }

        vrHoveredObject = null;
    }

    private void HandleVrSelectReleaseIfNeeded()
    {
        bool selectPressed = IsVrSelectPressed();
        bool selectReleasedThisFrame = !selectPressed && vrSelectWasPressed;
        vrSelectWasPressed = selectPressed;
        if (selectReleasedThisFrame)
        {
            HandleVrSelectRelease(null, null);
        }
    }

    private void HandleVrSelectRelease(GameObject currentHitObject, InputField hitInputField)
    {
        if (vrPressedObject != null && vrPointerEventData != null)
        {
            ExecuteEvents.Execute(vrPressedObject, vrPointerEventData, ExecuteEvents.pointerUpHandler);

            GameObject clickTarget = currentHitObject != null
                ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentHitObject)
                : null;

            if (vrPointerEventData.eligibleForClick && clickTarget != null && clickTarget == vrPressedObject)
            {
                ExecuteEvents.Execute(vrPressedObject, vrPointerEventData, ExecuteEvents.pointerClickHandler);
            }
        }

        if (hitInputField != null && vrPointerEventData != null)
        {
            SetVrInputCaretFromScreenPoint(hitInputField, vrPointerEventData.position);
            hitInputField.OnPointerClick(vrPointerEventData);
            FocusVrInputField(hitInputField);
        }

        if (vrPointerEventData != null)
        {
            vrPointerEventData.eligibleForClick = false;
            vrPointerEventData.pointerPress = null;
            vrPointerEventData.rawPointerPress = null;
        }

        vrPressedObject = null;
    }

    private void ResetVrPointerState()
    {
        HideVrCursor();
        ClearVrHover();
        HandleVrSelectReleaseIfNeeded();
    }

    private void FocusVrInputField(InputField inputField)
    {
        if (inputField == null)
        {
            ClearVrInputFocus();
            return;
        }

        vrFocusedInputField = inputField;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        }
        inputField.ActivateInputField();
        inputField.Select();
        OpenVrKeyboard(inputField);
    }

    private void ClearVrInputFocus()
    {
        vrFocusedInputField = null;

        if (vrTouchKeyboard != null)
        {
            try
            {
                vrTouchKeyboard.active = false;
            }
            catch
            {
            }
        }

        vrTouchKeyboard = null;
    }

    private void OpenVrKeyboard(InputField inputField)
    {
        if (inputField == null || !(Application.isMobilePlatform || Input.touchSupported))
        {
            return;
        }

        string text = inputField.text ?? string.Empty;
        bool isMultiline = inputField.lineType != InputField.LineType.SingleLine;

        try
        {
            vrTouchKeyboard = TouchScreenKeyboard.Open(
                text,
                TouchScreenKeyboardType.Default,
                false,
                isMultiline,
                false,
                false,
                string.Empty);
        }
        catch
        {
            vrTouchKeyboard = null;
        }
    }

    private void SyncVrKeyboard()
    {
        if (!IsQuestVrCodeIslandUi())
        {
            ClearVrInputFocus();
            return;
        }

        if (!editorVisible || vrFocusedInputField == null)
        {
            ClearVrInputFocus();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != vrFocusedInputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(vrFocusedInputField.gameObject);
        }

        if (vrTouchKeyboard == null)
        {
            OpenVrKeyboard(vrFocusedInputField);
            return;
        }

        if (vrTouchKeyboard.status == TouchScreenKeyboard.Status.Canceled ||
            vrTouchKeyboard.status == TouchScreenKeyboard.Status.Done ||
            !vrTouchKeyboard.active)
        {
            vrFocusedInputField.ActivateInputField();
            OpenVrKeyboard(vrFocusedInputField);
            return;
        }

        if (suppressVrKeyboardSync)
        {
            return;
        }

        string keyboardText = vrTouchKeyboard.text ?? string.Empty;
        string fieldText = vrFocusedInputField.text ?? string.Empty;
        if (!string.Equals(keyboardText, fieldText, StringComparison.Ordinal))
        {
            suppressVrKeyboardSync = true;
            vrFocusedInputField.text = keyboardText;
            vrFocusedInputField.caretPosition = keyboardText.Length;
            vrFocusedInputField.selectionAnchorPosition = keyboardText.Length;
            vrFocusedInputField.selectionFocusPosition = keyboardText.Length;
            suppressVrKeyboardSync = false;
        }
    }

    private void SetVrInputCaretFromScreenPoint(InputField inputField, Vector2 screenPoint)
    {
        if (inputField == null || inputField.textComponent == null)
        {
            return;
        }

        RectTransform textRect = inputField.textComponent.rectTransform;
        Camera eventCamera = editorCanvas != null ? editorCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(textRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        string currentText = inputField.text ?? string.Empty;
        int caretIndex = GetClosestCaretIndex(currentText, localPoint, textRect.rect);
        inputField.caretPosition = caretIndex;
        inputField.selectionAnchorPosition = caretIndex;
        inputField.selectionFocusPosition = caretIndex;
    }

    private int GetClosestCaretIndex(string text, Vector2 localPoint, Rect textRect)
    {
        string currentText = text ?? string.Empty;
        string[] lines = currentText.Split('\n');
        float lineHeight = GetEditorLineHeight();
        float localX = Mathf.Clamp(localPoint.x - textRect.xMin, 0f, textRect.width);
        float localYFromTop = Mathf.Clamp(textRect.yMax - localPoint.y, 0f, Mathf.Max(lineHeight, textRect.height));
        int lineIndex = Mathf.Clamp(Mathf.FloorToInt(localYFromTop / Mathf.Max(1f, lineHeight)), 0, Mathf.Max(0, lines.Length - 1));

        int lineStartIndex = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            lineStartIndex += lines[i].Length + 1;
        }

        string lineText = lines.Length > 0 ? lines[lineIndex] : string.Empty;
        int bestOffset = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i <= lineText.Length; i++)
        {
            float width = MeasureEditorTextWidth(lineText.Substring(0, i));
            float distance = Mathf.Abs(width - localX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestOffset = i;
            }
        }

        return Mathf.Clamp(lineStartIndex + bestOffset, 0, currentText.Length);
    }

    private bool TryGetRightControllerRay(out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;

        Transform reference = PlayerCache.GetFps() != null ? PlayerCache.GetFps().transform : null;
        if (VrHandTracking.TryGetPointerRay(XRNode.RightHand, reference, out origin, out direction))
        {
            return true;
        }

        XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid &&
            rightHand.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 localPosition) &&
            rightHand.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion localRotation))
        {
            Quaternion pointerRotation = localRotation * Quaternion.AngleAxis(VrPointerDownwardAngle, Vector3.right);
            if (reference != null)
            {
                origin = reference.TransformPoint(localPosition);
                direction = reference.TransformDirection(pointerRotation * Vector3.forward).normalized;
                return true;
            }

            origin = localPosition;
            direction = pointerRotation * Vector3.forward;
            return true;
        }

        try
        {
            if ((OVRInput.GetConnectedControllers() & OVRInput.Controller.RTouch) != 0)
            {
                Vector3 ovrLocalPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
                Quaternion ovrLocalRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
                Quaternion pointerRotation = ovrLocalRotation * Quaternion.AngleAxis(VrPointerDownwardAngle, Vector3.right);
                if (reference != null)
                {
                    origin = reference.TransformPoint(ovrLocalPosition);
                    direction = reference.TransformDirection(pointerRotation * Vector3.forward).normalized;
                    return true;
                }

                origin = ovrLocalPosition;
                direction = pointerRotation * Vector3.forward;
                return true;
            }
        }
        catch { }

        return false;
    }

    private bool IsVrSelectPressed()
    {
        if (VrHandTracking.IsPinching(XRNode.RightHand))
        {
            return true;
        }

        XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid)
        {
            if (rightHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton) && triggerButton)
            {
                return true;
            }

            if (rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryButton) && primaryButton)
            {
                return true;
            }
        }

        try
        {
            return OVRInput.Get(OVRInput.RawButton.RIndexTrigger) || OVRInput.Get(OVRInput.RawButton.A);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWorldPointInsideCanvas(RectTransform canvasRect, Vector3 worldPoint)
    {
        Vector3 localPoint3 = canvasRect.InverseTransformPoint(worldPoint);
        Vector2 localPoint = new Vector2(localPoint3.x, localPoint3.y);
        return canvasRect.rect.Contains(localPoint);
    }

    private static RaycastResult FindFirstInteractiveRaycast(List<RaycastResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject == null)
            {
                continue;
            }

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[i].gameObject) != null ||
                ExecuteEvents.GetEventHandler<ISubmitHandler>(results[i].gameObject) != null)
            {
                return results[i];
            }
        }

        return results.Count > 0 ? results[0] : default;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private bool TryHandleMobileOutsideTapClose()
    {
        if (editorPanelRect == null)
        {
            return false;
        }

        Vector2 screenPoint;
        if (!TryGetTapBeganThisFrame(out screenPoint, out int pointerId))
        {
            return false;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
        {
            return false;
        }

        if (IsScreenPointInsideEditorUi(screenPoint))
        {
            return false;
        }

        bool insideEditor = RectTransformUtility.RectangleContainsScreenPoint(editorPanelRect, screenPoint, null);
        bool insideAi = aiVisible && aiPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(aiPanelRect, screenPoint, null);
        if (insideEditor || insideAi)
        {
            return false;
        }

        UpdateEditorVisibility(false);
        return true;
    }

    private bool IsScreenPointInsideEditorUi(Vector2 screenPoint)
    {
        if (editorCanvasRaycaster == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left
        };

        uiTapRaycastResults.Clear();
        editorCanvasRaycaster.Raycast(pointerEventData, uiTapRaycastResults);
        for (int i = 0; i < uiTapRaycastResults.Count; i++)
        {
            GameObject hitObject = uiTapRaycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (editorPanel != null && hitObject.transform.IsChildOf(editorPanel.transform))
            {
                return true;
            }

            if (aiPanel != null && hitObject.transform.IsChildOf(aiPanel.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTapBeganThisFrame(out Vector2 screenPoint, out int pointerId)
    {
        pointerId = -1;
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            TouchControl primaryTouch = touchscreen.primaryTouch;
            if (primaryTouch != null && primaryTouch.press.wasPressedThisFrame)
            {
                screenPoint = primaryTouch.position.ReadValue();
                pointerId = primaryTouch.touchId.ReadValue();
                return true;
            }
        }

        screenPoint = Vector2.zero;
        return false;
    }

    private void ShowEditorHintIfNeeded()
    {
        UpdateEditorHintVisibility();
    }

    private void UpdateEditorHintVisibility()
    {
        if (editorHintText != null)
        {
            RectTransform hintRect = editorHintText.rectTransform;
            if (XRSettings.enabled)
            {
                editorHintText.text = "Press X on your left controller to open the menu.";
                editorHintText.alignment = TextAnchor.MiddleCenter;
                hintRect.anchorMin = new Vector2(0.5f, 0.5f);
                hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.pivot = new Vector2(0.5f, 0.5f);
                hintRect.anchoredPosition = new Vector2(0f, 0f);
                hintRect.sizeDelta = new Vector2(760f, 56f);
            }
            else
            {
                editorHintText.text = "Press ` to show the code window.";
                editorHintText.alignment = TextAnchor.LowerLeft;
                hintRect.anchorMin = new Vector2(0f, 0f);
                hintRect.anchorMax = new Vector2(0f, 0f);
                hintRect.pivot = new Vector2(0f, 0f);
                hintRect.anchoredPosition = new Vector2(34f, 28f);
                hintRect.sizeDelta = new Vector2(520f, 40f);
            }

            editorHintText.gameObject.SetActive(modeActive && !editorHintDismissed);
        }
    }

    private void BuildAiUi()
    {
        aiPanel = CreateUiObject("AiPanel", editorCanvas.transform, new Vector2(520f, 420f), new Vector2(470f, 34f));
        aiPanelRect = aiPanel.GetComponent<RectTransform>();
        aiPanel.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.15f, 0.96f);
        Outline outline = aiPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.72f, 0.95f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject titleBar = CreateUiObject("AiTitleBar", aiPanel.transform, new Vector2(520f, 56f), new Vector2(0f, 182f));
        Image titleBarImage = titleBar.AddComponent<Image>();
        titleBarImage.color = new Color(0.08f, 0.16f, 0.28f, 1f);
        DraggableWindowHandle dragHandle = titleBar.AddComponent<DraggableWindowHandle>();
        dragHandle.Configure(aiPanelRect);

        aiCloseButton = CreateButton("AiCloseButton", titleBar.transform, "X", new Vector2(-220f, 0f), new Vector2(44f, 34f), new Color(0.82f, 0.22f, 0.24f, 1f), 16);
        aiCloseButton.onClick.AddListener(() => UpdateAiVisibility(false));

        Text titleText = CreateText("AiTitle", titleBar.transform, "CODE ISLAND AI", 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(280f, 30f));
        titleText.raycastTarget = false;

        Text introText = CreateText("AiIntro", aiPanel.transform, "Ask about Code Island Python, mentora_world functions, vectors, colors, loops, or how to control objects.", 14, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.78f, 0.88f, 1f), new Vector2(0f, 132f), new Vector2(456f, 42f));
        introText.raycastTarget = false;

        GameObject historyViewport = CreateUiObject("AiHistoryViewport", aiPanel.transform, new Vector2(456f, 210f), new Vector2(0f, 12f));
        aiHistoryViewportRect = historyViewport.GetComponent<RectTransform>();
        historyViewport.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.22f, 0.98f);
        historyViewport.AddComponent<Outline>().effectColor = new Color(0.3f, 0.68f, 0.95f, 0.5f);
        Mask historyMask = historyViewport.AddComponent<Mask>();
        historyMask.showMaskGraphic = false;

        aiHistoryScrollRect = historyViewport.AddComponent<ScrollRect>();
        aiHistoryScrollRect.horizontal = false;
        aiHistoryScrollRect.vertical = true;
        aiHistoryScrollRect.scrollSensitivity = 24f;
        aiHistoryScrollRect.movementType = ScrollRect.MovementType.Clamped;
        aiHistoryScrollRect.inertia = false;

        GameObject historyContent = CreateUiObject("AiHistoryContent", historyViewport.transform, new Vector2(420f, 240f), Vector2.zero);
        aiHistoryContentRect = historyContent.GetComponent<RectTransform>();
        aiHistoryContentRect.anchorMin = new Vector2(0f, 1f);
        aiHistoryContentRect.anchorMax = new Vector2(1f, 1f);
        aiHistoryContentRect.pivot = new Vector2(0.5f, 1f);
        aiHistoryContentRect.anchoredPosition = new Vector2(0f, -10f);
        aiHistoryContentRect.sizeDelta = new Vector2(-20f, 240f);

        aiHistoryText = CreateText("AiHistoryText", historyContent.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.95f, 0.97f, 1f), Vector2.zero, new Vector2(408f, 220f));
        aiHistoryText.raycastTarget = false;
        RectTransform historyTextRect = aiHistoryText.rectTransform;
        historyTextRect.anchorMin = Vector2.zero;
        historyTextRect.anchorMax = Vector2.one;
        historyTextRect.offsetMin = new Vector2(10f, 10f);
        historyTextRect.offsetMax = new Vector2(-10f, -10f);
        aiHistoryText.supportRichText = false;

        aiHistoryScrollRect.viewport = aiHistoryViewportRect;
        aiHistoryScrollRect.content = aiHistoryContentRect;

        aiInput = CreateInputField(aiPanel.transform, "AiInput", "Ask about Code Island Python... Press Enter to send.", new Vector2(0f, -148f), new Vector2(456f, 130f));
        aiInput.lineType = InputField.LineType.MultiLineSubmit;
        aiInput.onEndEdit.AddListener(HandleAiInputEndEdit);

        AttachScrollWheelForwarder(aiPanel, aiHistoryScrollRect);
        AttachScrollWheelForwarder(historyViewport, aiHistoryScrollRect);
        AttachScrollWheelForwarder(aiInput.gameObject, aiHistoryScrollRect);

        AppendChatLine("AI: I can help with Code Island Python. Ask me how to use cube(), sphere(), move(), rotate(), scale(), color(), loops, and vectors.");
        UpdateAiVisibility(false);
    }

    private void BuildChallengeUi()
    {
        challengePanel = CreateUiObject("ChallengePanel", editorCanvas.transform, new Vector2(520f, 420f), new Vector2(-470f, 34f));
        challengePanel.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.13f, 0.96f);
        Outline outline = challengePanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.96f, 0.66f, 0.24f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject titleBar = CreateUiObject("ChallengeTitleBar", challengePanel.transform, new Vector2(520f, 56f), new Vector2(0f, 182f));
        titleBar.AddComponent<Image>().color = new Color(0.16f, 0.11f, 0.05f, 1f);
        DraggableWindowHandle dragHandle = titleBar.AddComponent<DraggableWindowHandle>();
        dragHandle.Configure(challengePanel.GetComponent<RectTransform>());

        challengeTitleText = CreateText("ChallengeTitle", titleBar.transform, "CODE QUEST", 21, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(450f, 34f));
        challengeTitleText.raycastTarget = false;

        challengeDescriptionText = CreateText("ChallengeDescription", challengePanel.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.92f, 0.96f, 1f), new Vector2(0f, 112f), new Vector2(456f, 116f));
        challengeDescriptionText.raycastTarget = false;

        GameObject checklistBox = CreateUiObject("ChallengeChecklistBox", challengePanel.transform, new Vector2(456f, 150f), new Vector2(0f, -42f));
        checklistBox.AddComponent<Image>().color = new Color(0.1f, 0.13f, 0.2f, 0.98f);
        Outline checklistOutline = checklistBox.AddComponent<Outline>();
        checklistOutline.effectColor = new Color(0.95f, 0.62f, 0.22f, 0.45f);
        checklistOutline.effectDistance = new Vector2(1f, -1f);

        challengeChecklistText = CreateText("ChallengeChecklist", checklistBox.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.95f, 0.97f, 1f), Vector2.zero, new Vector2(426f, 126f));
        challengeChecklistText.raycastTarget = false;

        challengeVerifyButton = CreateButton("ChallengeVerifyButton", challengePanel.transform, "VERIFY", new Vector2(146f, -174f), new Vector2(112f, 40f), new Color(0.18f, 0.68f, 0.32f, 1f), 14);
        challengeVerifyButton.onClick.AddListener(VerifyActiveChallenge);

        challengeResetButton = CreateButton("ChallengeResetButton", challengePanel.transform, "RESET", new Vector2(22f, -174f), new Vector2(104f, 40f), new Color(0.78f, 0.32f, 0.2f, 1f), 14);
        challengeResetButton.onClick.AddListener(() =>
        {
            ResetActiveChallengeWorld();
            SetStatus(activeChallenge == null ? "Sandbox reset." : "Challenge reset.");
        });

        challengeSandboxButton = CreateButton("ChallengeSandboxButton", challengePanel.transform, "SANDBOX", new Vector2(-132f, -174f), new Vector2(130f, 40f), new Color(0.16f, 0.52f, 0.82f, 1f), 14);
        challengeSandboxButton.onClick.AddListener(StartSandboxMode);

        UpdateChallengeVisibility();
    }

    private void UpdateChallengeVisibility()
    {
        if (challengePanel == null)
        {
            return;
        }

        challengePanel.SetActive(editorVisible && modeActive && activeChallenge != null);
        if (challengePanel.activeSelf)
        {
            RefreshChallengeUi();
        }
    }

    private void RefreshChallengeUi()
    {
        if (challengePanel == null)
        {
            return;
        }

        if (activeChallenge == null)
        {
            challengePanel.SetActive(false);
            return;
        }

        if (challengeTitleText != null)
        {
            challengeTitleText.text = string.IsNullOrWhiteSpace(activeChallenge.Title) ? "CODE QUEST" : activeChallenge.Title.Trim();
        }

        if (challengeDescriptionText != null)
        {
            challengeDescriptionText.text = string.IsNullOrWhiteSpace(activeChallenge.Description)
                ? "Use Python to build the requested world objects, then press VERIFY."
                : activeChallenge.Description.Trim();
        }

        if (challengeChecklistText != null)
        {
            challengeChecklistText.text = BuildChallengeChecklistText();
        }
    }

    private string BuildChallengeChecklistText()
    {
        if (activeChallenge == null || activeChallenge.Requirements.Count == 0)
        {
            return "Checklist:\n□ Build at least one named object.";
        }

        StringBuilder builder = new StringBuilder("Checklist:\n");
        for (int i = 0; i < activeChallenge.Requirements.Count; i++)
        {
            CodeWorldChallengeRequirement requirement = activeChallenge.Requirements[i];
            bool passed = EvaluateChallengeRequirement(requirement);
            builder.Append(passed ? "✓ " : "□ ");
            builder.Append(string.IsNullOrWhiteSpace(requirement.Label) ? requirement.Kind.ToString() : requirement.Label.Trim());
            if (i + 1 < activeChallenge.Requirements.Count)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private void VerifyActiveChallenge()
    {
        if (activeChallenge == null)
        {
            SetStatus("No active challenge.");
            return;
        }

        RefreshChallengeUi();
        bool complete = activeChallenge.Requirements.Count > 0;
        for (int i = 0; i < activeChallenge.Requirements.Count; i++)
        {
            if (!EvaluateChallengeRequirement(activeChallenge.Requirements[i]))
            {
                complete = false;
                break;
            }
        }

        SetStatus(complete ? "Challenge complete. Nice build." : "Not complete yet. Check the checklist.");
    }

    private bool EvaluateChallengeRequirement(CodeWorldChallengeRequirement requirement)
    {
        if (requirement == null)
        {
            return false;
        }

        switch (requirement.Kind)
        {
            case CodeWorldChallengeRequirementKind.ObjectExists:
                return HasSpawnedObject(requirement.ObjectName);

            case CodeWorldChallengeRequirementKind.ObjectMissing:
                return !HasSpawnedObject(requirement.ObjectName);

            case CodeWorldChallengeRequirementKind.ObjectNear:
                return TryGetSpawnedObject(requirement.ObjectName, out GameObject nearObject) &&
                       Vector3.Distance(nearObject.transform.position, requirement.VectorValue) <= Mathf.Max(0.05f, requirement.Tolerance);

            case CodeWorldChallengeRequirementKind.ScaleAtLeast:
                return TryGetSpawnedObject(requirement.ObjectName, out GameObject scaledObject) &&
                       scaledObject.transform.localScale.x >= requirement.VectorValue.x &&
                       scaledObject.transform.localScale.y >= requirement.VectorValue.y &&
                       scaledObject.transform.localScale.z >= requirement.VectorValue.z;

            case CodeWorldChallengeRequirementKind.ColorNear:
                return TryGetSpawnedObject(requirement.ObjectName, out GameObject coloredObject) &&
                       TryGetObjectColor(coloredObject, out Color objectColor) &&
                       ColorDistance(objectColor, requirement.ColorValue) <= Mathf.Max(0.02f, requirement.Tolerance);

            case CodeWorldChallengeRequirementKind.ObjectCountAtLeast:
                return CountLiveSpawnedObjects() >= Mathf.Max(0, requirement.Count);

            case CodeWorldChallengeRequirementKind.PrefixCountAtLeast:
                return CountObjectsWithPrefix(requirement.Prefix) >= Mathf.Max(0, requirement.Count);

            default:
                return false;
        }
    }

    private bool HasSpawnedObject(string objectName)
    {
        return TryGetSpawnedObject(objectName, out _);
    }

    private bool TryGetSpawnedObject(string objectName, out GameObject target)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        return spawnedObjects.TryGetValue(objectName.Trim(), out target) && target != null;
    }

    private int CountLiveSpawnedObjects()
    {
        int count = 0;
        foreach (KeyValuePair<string, GameObject> pair in spawnedObjects)
        {
            if (pair.Value != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountObjectsWithPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return 0;
        }

        int count = 0;
        foreach (KeyValuePair<string, GameObject> pair in spawnedObjects)
        {
            if (pair.Value != null && pair.Key.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryGetObjectColor(GameObject target, out Color color)
    {
        color = Color.clear;
        if (target == null)
        {
            return false;
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return false;
        }

        Material material = renderer.sharedMaterial;
        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        color = material.color;
        return true;
    }

    private static float ColorDistance(Color first, Color second)
    {
        float dr = first.r - second.r;
        float dg = first.g - second.g;
        float db = first.b - second.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
    }

    private static string ShortenStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        string normalized = Regex.Replace(message.Trim(), "\\s+", " ");
        return normalized.Length <= 180 ? normalized : normalized.Substring(0, 177) + "...";
    }

    private void RefreshHistoryText()
    {
        if (historyText == null)
        {
            return;
        }

        if (commandHistory.Count == 0)
        {
            historyText.text = "History: none";
            RefreshHistoryLayout();
            return;
        }

        StringBuilder builder = new StringBuilder("History:\n");
        for (int i = 0; i < commandHistory.Count; i++)
        {
            builder.Append(commandHistory[i]).Append('\n');
        }

        historyText.text = builder.ToString().TrimEnd();
        RefreshHistoryLayout();
        ScrollHistoryToBottom();
    }

    private void RefreshHistoryLayout()
    {
        if (historyText == null || historyContentRect == null || historyViewportRect == null)
        {
            return;
        }

        float lineHeight = Mathf.Max(17f, historyText.fontSize * 1.25f);
        int lineCount = Mathf.Max(1, (historyText.text ?? string.Empty).Split('\n').Length);
        float preferredHeight = lineCount * lineHeight + 18f;
        float viewportHeight = historyViewportRect.rect.height;
        historyContentRect.sizeDelta = new Vector2(historyContentRect.sizeDelta.x, Mathf.Max(viewportHeight, preferredHeight));
    }

    private void ScrollHistoryToBottom()
    {
        if (historyScrollRect != null)
        {
            historyScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return obj;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color, Vector2 position, Vector2 size)
    {
        GameObject obj = CreateUiObject(name, parent, size, position);
        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = color;
        text.text = value;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text CreateScreenCornerText(string name, Transform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = anchor;
        text.color = color;
        text.text = value;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 position, Vector2 size)
    {
        GameObject obj = CreateUiObject(name, parent, size, position);
        Image background = obj.AddComponent<Image>();
        background.color = new Color(0.1f, 0.14f, 0.21f, 0.98f);

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.72f, 0.95f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);

        InputField input = obj.AddComponent<InputField>();
        Text text = CreateText(name + "Text", obj.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, Vector2.zero, size);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;

        Text hint = CreateText(name + "Placeholder", obj.transform, placeholder, 16, FontStyle.Italic, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, size);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = new Vector2(12f, 10f);
        hintRect.offsetMax = new Vector2(-12f, -10f);
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 16;

        input.textComponent = text;
        input.placeholder = hint;
        input.contentType = InputField.ContentType.Standard;
        input.lineType = InputField.LineType.MultiLineNewline;
        return input;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color, int fontSize = 18)
    {
        GameObject obj = CreateUiObject(name, parent, size, position);
        Image image = obj.AddComponent<Image>();
        image.color = color;

        Button button = obj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        Text text = CreateText(name + "Label", obj.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 1f), size);
        text.raycastTarget = false;
        return button;
    }

    private sealed class ScriptCursor
    {
        public int Index;
    }

    private static void AttachScrollWheelForwarder(GameObject target, ScrollRect scrollRect)
    {
        if (target == null || scrollRect == null)
        {
            return;
        }

        ScrollWheelForwarder forwarder = target.GetComponent<ScrollWheelForwarder>();
        if (forwarder == null)
        {
            forwarder = target.AddComponent<ScrollWheelForwarder>();
        }

        forwarder.Configure(scrollRect);
    }

    private void TrackEditorUndoState()
    {
        if (suppressEditorTracking || editorInput == null || !editorInput.isFocused)
        {
            return;
        }

        string currentText = editorInput.text ?? string.Empty;
        if (currentText == lastEditorTrackedText)
        {
            return;
        }

        if (editorUndoStack.Count == 0 || editorUndoStack.Peek() != lastEditorTrackedText)
        {
            editorUndoStack.Push(lastEditorTrackedText);
            while (editorUndoStack.Count > 32)
            {
                Stack<string> trimmed = new Stack<string>();
                while (editorUndoStack.Count > 1)
                {
                    trimmed.Push(editorUndoStack.Pop());
                }

                if (editorUndoStack.Count > 0)
                {
                    editorUndoStack.Pop();
                }

                while (trimmed.Count > 0)
                {
                    editorUndoStack.Push(trimmed.Pop());
                }
            }
        }

        lastEditorTrackedText = currentText;
    }

    private void UndoEditorText()
    {
        if (editorInput == null)
        {
            return;
        }

        if (editorUndoStack.Count == 0)
        {
            SetStatus("Nothing to undo.");
            return;
        }

        suppressEditorTracking = true;
        string previousText = editorUndoStack.Pop();
        editorInput.text = previousText ?? string.Empty;
        editorInput.ActivateInputField();
        editorInput.Select();
        lastEditorTrackedText = editorInput.text;
        suppressEditorTracking = false;
        SetStatus("Undid last edit.");
    }

    private void HandleEditorInputChanged(string _)
    {
        if (TryRestoreBackquoteMutation())
        {
            return;
        }

        RefreshEditorLayout();

        if (suppressEditorSync)
        {
            return;
        }

        editorSyncDirty = true;
        nextEditorSyncTime = Time.unscaledTime + EditorSyncInterval;
        cursorSyncDirty = true;
        nextCursorSyncTime = Time.unscaledTime + CursorSyncInterval;
    }

    private void BeginBackquoteTextGuard()
    {
        if (editorInput == null)
        {
            return;
        }

        suppressBackquoteTextMutation = true;
        backquoteGuardText = editorInput.text ?? string.Empty;
        backquoteGuardAnchor = Mathf.Clamp(editorInput.selectionAnchorPosition, 0, backquoteGuardText.Length);
        backquoteGuardFocus = Mathf.Clamp(editorInput.selectionFocusPosition, 0, backquoteGuardText.Length);
        backquoteGuardCaret = Mathf.Clamp(editorInput.caretPosition, 0, backquoteGuardText.Length);
    }

    private bool TryRestoreBackquoteMutation()
    {
        if (!suppressBackquoteTextMutation || editorInput == null)
        {
            return false;
        }

        string currentText = editorInput.text ?? string.Empty;
        if (currentText == backquoteGuardText)
        {
            suppressBackquoteTextMutation = false;
            return false;
        }

        if (!LooksLikeBackquoteMutation(backquoteGuardText, currentText, backquoteGuardAnchor, backquoteGuardFocus))
        {
            suppressBackquoteTextMutation = false;
            return false;
        }

        suppressEditorTracking = true;
        suppressEditorSync = true;
        editorInput.text = backquoteGuardText;
        editorInput.selectionAnchorPosition = Mathf.Clamp(backquoteGuardAnchor, 0, editorInput.text.Length);
        editorInput.selectionFocusPosition = Mathf.Clamp(backquoteGuardFocus, 0, editorInput.text.Length);
        editorInput.caretPosition = Mathf.Clamp(backquoteGuardCaret, 0, editorInput.text.Length);
        lastEditorTrackedText = editorInput.text;
        suppressEditorSync = false;
        suppressEditorTracking = false;
        suppressBackquoteTextMutation = false;
        RefreshEditorLayout();
        return true;
    }

    private static bool LooksLikeBackquoteMutation(string previousText, string currentText, int anchor, int focus)
    {
        previousText = previousText ?? string.Empty;
        currentText = currentText ?? string.Empty;
        int start = Mathf.Clamp(Mathf.Min(anchor, focus), 0, previousText.Length);
        int end = Mathf.Clamp(Mathf.Max(anchor, focus), 0, previousText.Length);
        string expected = previousText.Substring(0, start) + "`" + previousText.Substring(end);
        if (currentText == "`")
        {
            return start == 0 && end == previousText.Length;
        }

        return string.Equals(currentText, expected, StringComparison.Ordinal);
    }

    private void FlushPendingEditorSync()
    {
        if (!editorSyncDirty || editorInput == null || sessionManager == null || !sessionManager.IsConnectedToSession)
        {
            return;
        }

        if (Time.unscaledTime < nextEditorSyncTime)
        {
            return;
        }

        string currentText = editorInput.text ?? string.Empty;
        CodeWorldEditorSyncPacket packet = new CodeWorldEditorSyncPacket(currentText, sessionManager.LocalClientId);
        if (sessionManager.IsHosting)
        {
            sessionManager.BroadcastQuizPacketToRemotes(packet);
        }
        else
        {
            sessionManager.SendQuizPacketToHost(packet);
        }

        editorSyncDirty = false;
    }

    private void TrackLocalCursorState()
    {
        if (editorInput == null || !editorVisible || !editorInput.isFocused)
        {
            return;
        }

        int caretIndex = Mathf.Max(0, editorInput.caretPosition);
        if (caretIndex == lastLocalCaretIndex)
        {
            return;
        }

        lastLocalCaretIndex = caretIndex;
        cursorSyncDirty = true;
        nextCursorSyncTime = Time.unscaledTime + CursorSyncInterval;
    }

    private void FlushPendingCursorSync()
    {
        if (!cursorSyncDirty || editorInput == null || sessionManager == null || !sessionManager.IsConnectedToSession)
        {
            return;
        }

        if (Time.unscaledTime < nextCursorSyncTime)
        {
            return;
        }

        int caretIndex = Mathf.Max(0, editorInput.caretPosition);
        string playerName = !string.IsNullOrWhiteSpace(sessionManager.LocalPlayerName) ? sessionManager.LocalPlayerName : "Player";
        CodeWorldCursorPacket packet = new CodeWorldCursorPacket(caretIndex, playerName, sessionManager.LocalClientId);
        if (sessionManager.IsHosting)
        {
            sessionManager.BroadcastQuizPacketToRemotes(packet);
        }
        else
        {
            sessionManager.SendQuizPacketToHost(packet);
        }

        cursorSyncDirty = false;
    }

    private void SetEditorText(string value)
    {
        if (editorInput == null)
        {
            return;
        }

        int anchor = Mathf.Clamp(editorInput.selectionAnchorPosition, 0, (value ?? string.Empty).Length);
        int focus = Mathf.Clamp(editorInput.selectionFocusPosition, 0, (value ?? string.Empty).Length);
        int caret = Mathf.Clamp(editorInput.caretPosition, 0, (value ?? string.Empty).Length);

        suppressEditorTracking = true;
        suppressEditorSync = true;
        editorInput.text = value ?? string.Empty;
        lastEditorTrackedText = editorInput.text;
        editorInput.selectionAnchorPosition = Mathf.Clamp(anchor, 0, editorInput.text.Length);
        editorInput.selectionFocusPosition = Mathf.Clamp(focus, 0, editorInput.text.Length);
        editorInput.caretPosition = Mathf.Clamp(caret, 0, editorInput.text.Length);
        suppressEditorTracking = false;
        suppressEditorSync = false;
        editorSyncDirty = false;
        cursorSyncDirty = true;
        nextCursorSyncTime = Time.unscaledTime + CursorSyncInterval;
        RefreshEditorLayout();
    }

    private void UpdateAiVisibility(bool visible)
    {
        aiVisible = visible && editorVisible && modeActive;
        if (aiPanel != null)
        {
            aiPanel.SetActive(aiVisible);
        }

        if (aiOpenButton != null)
        {
            aiOpenButton.gameObject.SetActive(editorVisible && modeActive);
        }

        if (aiVisible && aiInput != null)
        {
            aiInput.ActivateInputField();
            aiInput.Select();
            RefreshAiHistoryLayout();
            ScrollAiHistoryToBottom();
        }
    }

    private void OnAiSendClicked()
    {
        if (!modeActive || aiInput == null)
        {
            return;
        }

        string message = (aiInput.text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetStatus("Type an AI question first.");
            return;
        }

        aiInput.text = string.Empty;
        StartCoroutine(SendAiChatMessage(message));
    }

    private void HandleAiInputEndEdit(string _)
    {
        if (!aiVisible || aiInput == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return;
        }

        OnAiSendClicked();
        aiInput.ActivateInputField();
    }

    private IEnumerator SendAiChatMessage(string message)
    {
        if (awaitingAiResponse)
        {
            AppendChatLine("AI: I am already answering. Wait for the current reply.");
            yield break;
        }

        AppendChatLine("You: " + message);
        awaitingAiResponse = true;
        pendingAiResponse = string.Empty;
        bool sent = false;
        yield return SendPacketWithConnect(new AskAiPacket(BuildAiQuestion(message), BuildAiChatContext()), success => sent = success);
        if (!sent)
        {
            awaitingAiResponse = false;
            AppendChatLine("AI: I can't reach the AI server right now.");
            yield break;
        }

        float elapsed = 0f;
        while (awaitingAiResponse && elapsed < 15f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (awaitingAiResponse)
        {
            awaitingAiResponse = false;
            AppendChatLine("AI: I did not get a reply in time.");
        }
    }

    private void HandleAiResponse(AiResponsePacket response)
    {
        if (!awaitingAiResponse)
        {
            return;
        }

        awaitingAiResponse = false;
        pendingAiResponse = response != null ? response.Response ?? string.Empty : string.Empty;
        AppendChatLine("AI: " + (string.IsNullOrWhiteSpace(pendingAiResponse) ? "No answer came back." : pendingAiResponse.Trim()));
    }

    private async Task<bool> SendPacketWithConnectAsync(Packet packet)
    {
        if (GameClient.Instance == null)
        {
            return false;
        }

        if (!GameClient.Instance.IsConnected)
        {
            await GameClient.Instance.Connect();
        }

        if (!GameClient.Instance.IsConnected)
        {
            return false;
        }

        await GameClient.Instance.SendPacket(packet);
        return true;
    }

    private IEnumerator SendPacketWithConnect(Packet packet, Action<bool> onDone)
    {
        Task<bool> task = SendPacketWithConnectAsync(packet);
        while (!task.IsCompleted)
        {
            yield return null;
        }

        onDone?.Invoke(task.Result);
    }

    private void AppendChatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        aiChatLines.Add(line.Trim());
        if (aiChatLines.Count > MaxAiChatLines)
        {
            aiChatLines.RemoveAt(0);
        }

        RefreshAiHistoryText();
    }

    private void RefreshAiHistoryText()
    {
        if (aiHistoryText == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < aiChatLines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(aiChatLines[i]);
        }

        aiHistoryText.text = builder.ToString();
        RefreshAiHistoryLayout();
        ScrollAiHistoryToBottom();
    }

    private void RefreshAiHistoryLayout()
    {
        if (aiHistoryText == null || aiHistoryContentRect == null || aiHistoryViewportRect == null)
        {
            return;
        }

        float lineHeight = Mathf.Max(18f, aiHistoryText.fontSize * 1.28f);
        int lineCount = Mathf.Max(1, (aiHistoryText.text ?? string.Empty).Split('\n').Length);
        float preferredHeight = lineCount * lineHeight + 24f;
        float viewportHeight = aiHistoryViewportRect.rect.height;
        aiHistoryContentRect.sizeDelta = new Vector2(aiHistoryContentRect.sizeDelta.x, Mathf.Max(viewportHeight, preferredHeight));
    }

    private void ScrollAiHistoryToBottom()
    {
        if (aiHistoryScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            aiHistoryScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private static string BuildAiQuestion(string message)
    {
        return "You are the Code Island scripting assistant only. Answer only questions about Code Island code syntax, commands, loops, vectors, colors, object naming, and how to write scripts for this mode. If the player asks anything else, refuse briefly and redirect them to Code Island scripting only. Keep answers short, concrete, and example-driven. Player question: " + message;
    }

    private string BuildAiChatContext()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("code_world_syntax_helper\n");
        builder.Append("The player is on Code Island in Unity.\n");
        builder.Append("This assistant is restricted to Code Island scripting help only.\n");
        builder.Append("Do not answer general knowledge, Unity questions outside this mode, life advice, math tutoring, or unrelated chat.\n");
        builder.Append("If a question is outside Code Island scripting, answer with a short refusal and say you only help with Code Island code.\n");
        builder.Append("The editor runs real Python on the Mentora Java server. The server provides a custom mentora_world library.\n");
        builder.Append("Supported Python functions: cube, box, sphere, ball, orb, ellipsoid, oval, capsule, cylinder, rectangle, rect, circle, disc, panel, plane, move, rotate, scale, resize, translate, turn, color, delete, destroy, clear, list_objects, help, vector, vec3, Vec3.\n");
        builder.Append("Examples: cube(\"box1\", vector(220, 33, 520), scale=vector(2, 1, 2)); color(\"box1\", \"cyan\"); move(\"box1\", vector(220, 35, 520)).\n");
        builder.Append("Use normal Python loops, variables, functions, lists, and math. Do not use Unity C# wrappers or new Vector3 syntax.\n");
        builder.Append("Object names should use letters, numbers, _ or -.\n");
        builder.Append("Colors can be names, hex, or RGB values.\n");
        if (activeChallenge != null)
        {
            builder.Append("Active challenge:\n");
            builder.Append(activeChallenge.Title ?? "CodeWorld challenge").Append('\n');
            builder.Append(activeChallenge.Description ?? string.Empty).Append('\n');
            builder.Append(BuildChallengeChecklistText()).Append('\n');
        }
        builder.Append("Current editor code:\n");
        builder.Append(GetEditorText());
        return builder.ToString();
    }

    private void CopySelectedEditorText()
    {
        if (editorInput == null)
        {
            return;
        }

        string currentText = editorInput.text ?? string.Empty;
        int anchor = Mathf.Clamp(editorInput.selectionAnchorPosition, 0, currentText.Length);
        int focus = Mathf.Clamp(editorInput.selectionFocusPosition, 0, currentText.Length);
        int start = Mathf.Min(anchor, focus);
        int length = Mathf.Abs(anchor - focus);
        if (length <= 0)
        {
            return;
        }

        GUIUtility.systemCopyBuffer = currentText.Substring(start, length);
        SetStatus("Copied selection.");
    }

    private void RefreshEditorLayout()
    {
        if (editorInput == null || editorContentRect == null || editorViewportRect == null)
        {
            return;
        }

        Text textComponent = editorInput.textComponent;
        string currentText = editorInput.text ?? string.Empty;
        float lineHeight = textComponent != null ? Mathf.Max(20f, textComponent.fontSize * 1.25f) : 20f;
        int lineCount = Mathf.Max(1, currentText.Split('\n').Length);
        float preferredHeight = lineCount * lineHeight + 24f;
        float viewportHeight = editorViewportRect.rect.height;
        editorContentRect.sizeDelta = new Vector2(editorContentRect.sizeDelta.x, Mathf.Max(viewportHeight, preferredHeight));

        if (textComponent != null)
        {
            textComponent.text = currentText;
        }

        RefreshAllRemoteCursorVisuals();
    }

    private void ScrollEditorToTop()
    {
        if (editorScrollRect != null)
        {
            editorScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private static string BuildStarterScript()
    {
        return
            "from mentora_world import *\n" +
            "\n" +
            "sphere(\"ball1\", vector(220, 33, 520), scale=vector(1.2, 1.2, 1.2))\n" +
            "ellipsoid(\"blob1\", vector(224, 33, 520), scale=vector(2, 1, 1.4))\n" +
            "move(\"ball1\", vector(220, 35, 520))\n" +
            "rotate(\"blob1\", vector(0, 45, 0))\n" +
            "color(\"blob1\", \"cyan\")\n" +
            "\n" +
            "for i in range(5):\n" +
            "    cube(f\"step_{i}\", vector(228 + i * 2, 32 + i, 520), scale=vector(1.6, 0.5, 1.6))\n";
    }

    private string GetEditorText()
    {
        return editorInput != null ? editorInput.text ?? string.Empty : BuildStarterScript();
    }

    private RemoteCursorVisual GetOrCreateRemoteCursorVisual(string clientId, string playerName)
    {
        if (remoteCursorVisuals.TryGetValue(clientId, out RemoteCursorVisual existing) && existing != null)
        {
            return existing;
        }

        RemoteCursorVisual visual = new RemoteCursorVisual();
        visual.ClientId = clientId;
        visual.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        visual.Color = GetCursorColor(clientId);

        GameObject root = new GameObject("RemoteCursor_" + clientId, typeof(RectTransform));
        root.transform.SetParent(editorInput.textComponent.rectTransform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(160f, 28f);

        GameObject caretObject = new GameObject("Caret", typeof(RectTransform));
        caretObject.transform.SetParent(root.transform, false);
        RectTransform caretRect = caretObject.GetComponent<RectTransform>();
        caretRect.anchorMin = new Vector2(0f, 1f);
        caretRect.anchorMax = new Vector2(0f, 1f);
        caretRect.pivot = new Vector2(0f, 1f);
        caretRect.sizeDelta = new Vector2(3f, GetEditorLineHeight());
        Image caretImage = caretObject.AddComponent<Image>();
        caretImage.color = visual.Color;
        caretImage.raycastTarget = false;

        Text nameText = CreateText("Name", root.transform, visual.PlayerName, 12, FontStyle.Bold, TextAnchor.UpperLeft, visual.Color, new Vector2(8f, -1f), new Vector2(152f, 20f));
        nameText.raycastTarget = false;

        visual.Root = root;
        visual.RootRect = rootRect;
        visual.CaretImage = caretImage;
        visual.NameText = nameText;
        remoteCursorVisuals[clientId] = visual;
        UpdateRemoteCursorVisibility();
        return visual;
    }

    private void RefreshRemoteCursorVisual(RemoteCursorVisual visual)
    {
        if (visual == null || visual.RootRect == null)
        {
            return;
        }

        visual.RootRect.anchoredPosition = GetCaretUiPosition(visual.CaretIndex);
        if (visual.CaretImage != null)
        {
            visual.CaretImage.rectTransform.sizeDelta = new Vector2(3f, GetEditorLineHeight());
            visual.CaretImage.color = visual.Color;
        }

        if (visual.NameText != null)
        {
            visual.NameText.color = visual.Color;
            visual.NameText.text = visual.PlayerName;
        }
    }

    private void RefreshAllRemoteCursorVisuals()
    {
        foreach (RemoteCursorVisual visual in remoteCursorVisuals.Values)
        {
            RefreshRemoteCursorVisual(visual);
        }
    }

    private void UpdateRemoteCursorVisibility()
    {
        bool shouldShow = editorVisible && modeActive;
        foreach (RemoteCursorVisual visual in remoteCursorVisuals.Values)
        {
            if (visual != null && visual.Root != null)
            {
                visual.Root.SetActive(shouldShow);
            }
        }
    }

    private void ClearRemoteCursorVisuals()
    {
        foreach (RemoteCursorVisual visual in remoteCursorVisuals.Values)
        {
            if (visual != null && visual.Root != null)
            {
                Destroy(visual.Root);
            }
        }

        remoteCursorVisuals.Clear();
    }

    private Vector2 GetCaretUiPosition(int caretIndex)
    {
        string currentText = editorInput != null ? editorInput.text ?? string.Empty : string.Empty;
        int safeIndex = Mathf.Clamp(caretIndex, 0, currentText.Length);
        int lineIndex = 0;
        int lineStartIndex = 0;

        for (int i = 0; i < safeIndex; i++)
        {
            if (currentText[i] == '\n')
            {
                lineIndex++;
                lineStartIndex = i + 1;
            }
        }

        int segmentLength = Mathf.Max(0, safeIndex - lineStartIndex);
        string segment = segmentLength > 0 ? currentText.Substring(lineStartIndex, segmentLength) : string.Empty;
        float x = MeasureEditorTextWidth(segment);
        float y = -(lineIndex * GetEditorLineHeight());
        return new Vector2(x, y);
    }

    private float MeasureEditorTextWidth(string value)
    {
        Text textComponent = editorInput != null ? editorInput.textComponent : null;
        if (textComponent == null || string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        TextGenerationSettings settings = textComponent.GetGenerationSettings(new Vector2(10000f, GetEditorLineHeight()));
        return textComponent.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / Mathf.Max(0.0001f, textComponent.pixelsPerUnit);
    }

    private float GetEditorLineHeight()
    {
        Text textComponent = editorInput != null ? editorInput.textComponent : null;
        return textComponent != null ? Mathf.Max(20f, textComponent.fontSize * 1.25f) : 20f;
    }

    private static Color GetCursorColor(string clientId)
    {
        int hash = string.IsNullOrEmpty(clientId) ? 0 : clientId.GetHashCode();
        float hue = Mathf.Abs(hash % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.68f, 0.95f);
    }

    private sealed class DraggableWindowHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private RectTransform target;
        private Vector2 dragOffset;

        public void Configure(RectTransform targetRect)
        {
            target = targetRect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);
            dragOffset = target.anchoredPosition - localPoint;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            RectTransform parentRect = target.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                target.anchoredPosition = localPoint + dragOffset;
            }
        }
    }

    private sealed class ScrollWheelForwarder : MonoBehaviour, IScrollHandler
    {
        private ScrollRect target;

        public void Configure(ScrollRect scrollRect)
        {
            target = scrollRect;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (target == null || eventData == null)
            {
                return;
            }

            float delta = eventData.scrollDelta.y * target.scrollSensitivity * 0.0025f;
            float next = Mathf.Clamp01(target.verticalNormalizedPosition + delta);
            target.verticalNormalizedPosition = next;
            eventData.Use();
        }
    }

    private sealed class NumericExpressionParser
    {
        private readonly string expression;
        private readonly Dictionary<string, float> variables;
        private int index;

        public NumericExpressionParser(string expression, Dictionary<string, float> variables)
        {
            this.expression = expression ?? string.Empty;
            this.variables = variables;
        }

        public float Parse()
        {
            index = 0;
            float result = ParseExpression();
            SkipWhitespace();
            if (index < expression.Length)
            {
                throw new InvalidOperationException("Unexpected token.");
            }

            return result;
        }

        private float ParseExpression()
        {
            float value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    value += ParseTerm();
                }
                else if (Match('-'))
                {
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private float ParseTerm()
        {
            float value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                {
                    value *= ParseFactor();
                }
                else if (Match('/'))
                {
                    value /= ParseFactor();
                }
                else
                {
                    return value;
                }
            }
        }

        private float ParseFactor()
        {
            SkipWhitespace();
            if (Match('+'))
            {
                return ParseFactor();
            }

            if (Match('-'))
            {
                return -ParseFactor();
            }

            if (Match('('))
            {
                float nested = ParseExpression();
                SkipWhitespace();
                if (!Match(')'))
                {
                    throw new InvalidOperationException("Missing )");
                }

                return nested;
            }

            if (IsIdentifierStart(Peek()))
            {
                string identifier = ParseIdentifier();
                if (variables != null && variables.TryGetValue(identifier, out float variableValue))
                {
                    return variableValue;
                }

                throw new InvalidOperationException("Unknown variable " + identifier);
            }

            return ParseNumber();
        }

        private float ParseNumber()
        {
            SkipWhitespace();
            int start = index;
            while (index < expression.Length)
            {
                char ch = expression[index];
                if (char.IsDigit(ch) || ch == '.' || ch == 'f' || ch == 'F')
                {
                    index++;
                }
                else
                {
                    break;
                }
            }

            if (start == index)
            {
                throw new InvalidOperationException("Expected a number.");
            }

            string token = expression.Substring(start, index - start).Replace("f", string.Empty).Replace("F", string.Empty);
            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            {
                throw new InvalidOperationException("Invalid number " + token);
            }

            return number;
        }

        private string ParseIdentifier()
        {
            int start = index;
            index++;
            while (index < expression.Length)
            {
                char ch = expression[index];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    index++;
                }
                else
                {
                    break;
                }
            }

            return expression.Substring(start, index - start);
        }

        private void SkipWhitespace()
        {
            while (index < expression.Length && char.IsWhiteSpace(expression[index]))
            {
                index++;
            }
        }

        private bool Match(char expected)
        {
            if (Peek() != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private char Peek()
        {
            SkipWhitespace();
            return index < expression.Length ? expression[index] : '\0';
        }

        private static bool IsIdentifierStart(char ch)
        {
            return char.IsLetter(ch) || ch == '_';
        }
    }

    private enum TransformMode
    {
        Position,
        Rotation,
        Scale
    }
}
