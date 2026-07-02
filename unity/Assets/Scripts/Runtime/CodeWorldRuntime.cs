using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mentora.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CodeWorldRuntime : MonoBehaviour
{
    private const string HostModePrefKey = "MP_HostMode";
    private const string CodeWorldModeValue = "codeworld";
    private const string CodeWorldActivePrefKey = "MP_CodeWorldActive";

    private static readonly Vector3 BuildSpawn = new Vector3(220f, 32f, 520f);
    private static readonly Quaternion BuildRotation = Quaternion.Euler(0f, 180f, 0f);

    private static CodeWorldRuntime instance;

    private readonly Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> commandHistory = new List<string>();

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
    private readonly Stack<string> editorUndoStack = new Stack<string>();
    private bool modeActive;
    private bool editorVisible;
    private string lastEditorTrackedText = string.Empty;
    private bool suppressEditorTracking;

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

        packet = new CodeWorldStatePacket(true, instance.SerializeHistory());
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
        AcquireSessionManager();
        BuildEditorUi();
        UpdateEditorVisibility(false);

        // Do not auto-enable code-world from saved prefs.
        // It should only activate when the host explicitly chooses the code-world option.
    }

    private void OnEnable()
    {
        AcquireSessionManager();
        SubscribePackets(true);
    }

    private void OnDisable()
    {
        SubscribePackets(false);
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

        if (keyboard.backquoteKey.wasPressedThisFrame)
        {
            UpdateEditorVisibility(!editorVisible);
        }

        if (!editorVisible)
        {
            return;
        }

        TrackEditorUndoState();

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
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
    }

    private void OnGUI()
    {
        if (!editorVisible || editorInput == null || !editorInput.isFocused)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.KeyDown)
        {
            return;
        }

        if ((currentEvent.control || currentEvent.command) && currentEvent.keyCode == KeyCode.C)
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

    private void HandleNetworkPacket(Packet packet)
    {
        if (packet is CodeWorldStatePacket statePacket)
        {
            ApplySnapshot(statePacket);
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
        UpdateEditorVisibility(true);
        SetStatus("Code World is live. Press ` to open the editor.");
        BroadcastStateToRemotes();
    }

    private void ActivateLocal(bool teleportPlayer, bool persistPrefs)
    {
        modeActive = true;
        EnsureWorld();
        EnableNoclip(true);

        if (teleportPlayer)
        {
            TeleportLocalPlayer(BuildSpawn, BuildRotation);
        }

        if (persistPrefs)
        {
            PlayerPrefs.SetString(HostModePrefKey, CodeWorldModeValue);
            PlayerPrefs.Save();
        }
    }

    private void DisableMode(bool clearWorld)
    {
        modeActive = false;
        UpdateEditorVisibility(false);
        EnableNoclip(false);

        if (clearWorld)
        {
            ClearSpawnedObjects();
            commandHistory.Clear();
            RefreshHistoryText();
        }

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

        string[] lines = script.Replace(";", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int successCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = NormalizeCommand(lines[i]);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            bool hostAppliesDirectly = sessionManager == null || !sessionManager.IsConnectedToSession || sessionManager.IsHosting;
            if (hostAppliesDirectly)
            {
                if (TryRunCommand(line, true, out string feedback, out bool mutatedWorld))
                {
                    successCount++;
                    if (mutatedWorld)
                    {
                        commandHistory.Add(line);
                        BroadcastCommandFromHost(line);
                    }

                    SetStatus(feedback);
                }
                else
                {
                    SetStatus(feedback);
                    break;
                }
            }
            else
            {
                if (TryRunCommand(line, false, out string feedback, out bool mutatedWorld))
                {
                    if (mutatedWorld)
                    {
                        successCount++;
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
                    SetStatus(feedback);
                    break;
                }
            }
        }

        RefreshHistoryText();
        if (successCount > 0 && editorInput != null)
        {
            lastEditorTrackedText = editorInput.text;
            editorInput.ActivateInputField();
        }
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

        sessionManager.BroadcastQuizPacketToRemotes(new CodeWorldStatePacket(true, SerializeHistory()));
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
                feedback = "Commands: cube name x y z | move name x y z | rotate name x y z | scale name x y z | color name red/#ff0/255 0 0 | delete name | clear | list";
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

        Text helpText = CreateText("Help", editorPanel.transform, "Press Ctrl+Enter to run. Press ` to hide. Example C#-style code:", 15, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.75f, 0.86f, 1f), new Vector2(-2f, 184f), new Vector2(580f, 28f));
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
        editorInput.onValueChanged.AddListener(_ => RefreshEditorLayout());
        RefreshEditorLayout();

        statusText = CreateText("Status", editorPanel.transform, "Press ` to open the editor.", 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.98f, 0.86f, 0.34f), new Vector2(-2f, -160f), new Vector2(580f, 48f));
        statusText.raycastTarget = false;
        historyText = CreateText("History", editorPanel.transform, "History: none", 14, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.66f, 0.8f, 0.96f), new Vector2(-2f, -230f), new Vector2(580f, 90f));
        historyText.raycastTarget = false;
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

    private enum TransformMode
    {
        Position,
        Rotation,
        Scale
    }
}
