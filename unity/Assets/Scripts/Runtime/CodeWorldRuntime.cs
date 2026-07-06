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
using UnityEngine.UI;

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
    private InputField editorInput;
    private ScrollRect editorScrollRect;
    private RectTransform editorContentRect;
    private RectTransform editorViewportRect;
    private Text statusText;
    private Text historyText;
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
    private readonly Stack<string> editorUndoStack = new Stack<string>();
    private readonly List<string> aiChatLines = new List<string>();
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
    private bool aiNetworkSubscribed;
    private bool editorHintDismissed;
    private Coroutine localExecutionRoutine;
    private bool localExecutionRunning;
    private bool localExecutionStopRequested;
    private Button stopButton;
    private const int MaxAiChatLines = 16;
    private const int MaxLoopIterations = 256;

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
        DontDestroyOnLoad(root);
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
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.escapeKey.wasPressedThisFrame && localExecutionRunning)
        {
            StopLocalScriptExecution(true);
            return;
        }

        if (keyboard.backquoteKey.wasPressedThisFrame)
        {
            UpdateEditorVisibility(!editorVisible);
        }

        if (!editorVisible)
        {
            return;
        }

        if ((Input.touchSupported || Application.isMobilePlatform) && TryHandleMobileOutsideTapClose())
        {
            return;
        }

        TrackEditorUndoState();

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (localExecutionRunning)
            {
                StopLocalScriptExecution(true);
                return;
            }

            UpdateEditorVisibility(false);
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
        ActivateLocal(true, true);
        commandHistory.Clear();
        ClearSpawnedObjects();
        RefreshHistoryText();
        UpdateEditorVisibility(false);
        BroadcastStateToRemotes();
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
            DisableMode(true);
            return;
        }

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
        localExecutionRoutine = StartCoroutine(RunLocalScriptRoutine(script));
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

        localExecutionRunning = false;
    }

    private IEnumerator RunLocalScriptRoutine(string script)
    {
        localExecutionRunning = true;
        SetStatus("Running code...");

        string feedback = string.Empty;
        yield return ExecuteStatementListCoroutine(
            TokenizeScript(script),
            new ScriptCursor(),
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
            result => feedback = result);

        if (!string.IsNullOrWhiteSpace(feedback))
        {
            SetStatus(feedback);
        }
        else if (!localExecutionStopRequested)
        {
            SetStatus("Code finished.");
        }

        RefreshHistoryText();
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
                feedback = "Commands: cube/move/rotate/scale/color/delete/clear/list plus simple for/while loops.";
                return true;

            case "list":
                feedback = spawnedObjects.Count == 0 ? "No objects yet." : "Objects: " + string.Join(", ", spawnedObjects.Keys);
                return true;

            case "cube":
            case "box":
            case "sphere":
            case "capsule":
            case "cylinder":
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
            case "capsule":
            case "cylinder":
                if (splitArgs.Length == 1)
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]);
                }
                if (splitArgs.Length == 2 && TryParseVectorExpression(splitArgs[1], out Vector3 spawnPos))
                {
                    return methodName.ToLowerInvariant() + " " + Unquote(splitArgs[0]) + " " + FormatVector(spawnPos);
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
            case "capsule":
            case "cylinder":
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
        if (tokens.Length != 2 && tokens.Length != 5)
        {
            feedback = "Spawn syntax: cube name [x y z]";
            return false;
        }

        string objectName = tokens[1];
        if (!IsValidObjectName(objectName))
        {
            feedback = "Use letters, numbers, _ or - in object names.";
            return false;
        }

        Vector3 position = GetDefaultSpawnPoint();
        if (tokens.Length == 5 && !TryParseVector3(tokens, 2, out position))
        {
            feedback = "Invalid position. Use numbers like 0 1 0.";
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

        PrimitiveType primitiveType = command switch
        {
            "sphere" => PrimitiveType.Sphere,
            "capsule" => PrimitiveType.Capsule,
            "cylinder" => PrimitiveType.Cylinder,
            _ => PrimitiveType.Cube,
        };

        GameObject created = GameObject.CreatePrimitive(primitiveType);
        created.name = objectName;
        created.transform.SetParent(dynamicObjectsRoot.transform, false);
        created.transform.position = position;
        created.transform.rotation = Quaternion.identity;
        created.transform.localScale = Vector3.one;
        ApplyMaterial(created, GetColorFromName("white"));
        spawnedObjects[objectName] = created;
        return true;
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
        material.color = color;
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

        canvasObject.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObject);

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

        Text helpText = CreateText("Help", editorPanel.transform, "Press Ctrl+Enter to run. Press Esc or STOP to stop running code. Press ` to hide. Example C#-style code:", 15, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.75f, 0.86f, 1f), new Vector2(-2f, 184f), new Vector2(580f, 28f));
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

        Text codeHint = CreateText("CodePlaceholder", codeContent.transform, "Type C#-style code here...", 16, FontStyle.Italic, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, new Vector2(530f, 320f));
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

        statusText = CreateText("Status", editorPanel.transform, string.Empty, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.98f, 0.86f, 0.34f), new Vector2(-2f, -160f), new Vector2(580f, 48f));
        statusText.raycastTarget = false;
        historyText = CreateText("History", editorPanel.transform, "History: none", 14, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.66f, 0.8f, 0.96f), new Vector2(-2f, -230f), new Vector2(580f, 90f));
        historyText.raycastTarget = false;
        editorHintText = CreateScreenCornerText("EditorHint", editorCanvas.transform, "Press ` to show the code window.", 19, FontStyle.Bold, TextAnchor.LowerLeft, new Color(0.98f, 0.9f, 0.42f, 0.98f), new Vector2(34f, 28f), new Vector2(520f, 40f));
        editorHintText.raycastTarget = false;
        editorHintText.gameObject.SetActive(false);

        BuildAiUi();
    }

    private void UpdateEditorVisibility(bool visible)
    {
        editorVisible = visible && modeActive;
        if (editorPanel != null)
        {
            editorPanel.SetActive(editorVisible);
        }

        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.SetMovementLocked(editorVisible);
            fps.SetCameraControlEnabled(!editorVisible);
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
        }

        UpdateAiVisibility(aiVisible);
        UpdateRemoteCursorVisibility();
    }

    private bool TryHandleMobileOutsideTapClose()
    {
        if (editorPanelRect == null)
        {
            return false;
        }

        Vector2 screenPoint;
        if (!TryGetTapBeganThisFrame(out screenPoint))
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

    private static bool TryGetTapBeganThisFrame(out Vector2 screenPoint)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            TouchControl primaryTouch = touchscreen.primaryTouch;
            if (primaryTouch != null && primaryTouch.press.wasPressedThisFrame)
            {
                screenPoint = primaryTouch.position.ReadValue();
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPoint = mouse.position.ReadValue();
            return true;
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

        Text introText = CreateText("AiIntro", aiPanel.transform, "Ask about this syntax, object commands, vectors, colors, or how to write C#-style lines for the island.", 14, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.78f, 0.88f, 1f), new Vector2(0f, 132f), new Vector2(456f, 42f));
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

        aiInput = CreateInputField(aiPanel.transform, "AiInput", "Ask about Code Island syntax... Press Enter to send.", new Vector2(0f, -148f), new Vector2(456f, 130f));
        aiInput.lineType = InputField.LineType.MultiLineSubmit;
        aiInput.onEndEdit.AddListener(HandleAiInputEndEdit);

        AttachScrollWheelForwarder(aiPanel, aiHistoryScrollRect);
        AttachScrollWheelForwarder(historyViewport, aiHistoryScrollRect);
        AttachScrollWheelForwarder(aiInput.gameObject, aiHistoryScrollRect);

        AppendChatLine("AI: I can help with Code Island syntax. Ask me how to spawn, move, rotate, scale, color, delete, or write C#-style commands.");
        UpdateAiVisibility(false);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }
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
            return;
        }

        int start = Mathf.Max(0, commandHistory.Count - 6);
        StringBuilder builder = new StringBuilder("History:\n");
        for (int i = start; i < commandHistory.Count; i++)
        {
            builder.Append(commandHistory[i]).Append('\n');
        }

        historyText.text = builder.ToString().TrimEnd();
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
        builder.Append("Supported commands: cube/box/sphere/capsule/cylinder name [x y z], move, rotate, scale, translate, turn, color, delete, clear, list, help.\n");
        builder.Append("Supported control flow: simple for loops, while loops, int/float/var assignments, ++, --, +=, -=, numeric expressions, and string concatenation for names.\n");
        builder.Append("The editor accepts C#-style lines like Cube(\"name\", new Vector3(...)); and normal command lines.\n");
        builder.Append("Ignore using UnityEngine and class wrappers if the player includes them.\n");
        builder.Append("Object names should use letters, numbers, _ or -.\n");
        builder.Append("Colors can be names, hex, or RGB values.\n");
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
            "public static class WorldScript\n" +
            "{\n" +
            "    public static void Run()\n" +
            "    {\n" +
            "        Cube(\"box1\", new Vector3(220f, 33f, 520f));\n" +
            "        Move(\"box1\", new Vector3(220f, 35f, 520f));\n" +
            "        Rotate(\"box1\", new Vector3(0f, 45f, 0f));\n" +
            "        Color(\"box1\", \"cyan\");\n" +
            "    }\n" +
            "}";
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
