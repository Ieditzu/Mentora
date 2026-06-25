using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mentora.Network;

/// <summary>
/// Fully self-contained AI Challenge Pad.
/// Auto-spawns near the sakura tree at startup via RuntimeInitializeOnLoadMethod.
/// No Inspector wiring needed — builds all UI in code.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AiChallengePad : MonoBehaviour
{
    // ── State ────────────────────────────────────────────────────────────────

    [SerializeField] private string language = "python";

    private bool playerInside;
    private bool challengeActive;
    private bool running;
    private long currentTaskId = -1;

    // Built UI refs
    private GameObject promptOverlay;   // "Walk closer — AI challenge awaits"
    private GameObject padPanel;        // main coding UI
    private InputField codeEditor;
    private Text titleText;
    private Text descriptionText;
    private Text pointsText;
    private Text outputText;
    private Text statusText;
    private Button runButton;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildProximityPrompt();
        BuildPadPanel();
        GameClient.Instance.OnPacketReceived += OnPacket;
    }

    private void Update()
    {
        // Keep the proximity prompt always facing the player camera
        if (promptOverlay != null && promptOverlay.activeSelf && Camera.main != null)
        {
            Vector3 dir = promptOverlay.transform.position - Camera.main.transform.position;
            if (dir.sqrMagnitude > 0.001f)
                promptOverlay.transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
            GameClient.Instance.OnPacketReceived -= OnPacket;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (challengeActive) return;
        if (other.GetComponent<BeanController>() == null && other.GetComponent<FirstPersonControllerSimple>() == null) return;

        playerInside = true;
        if (promptOverlay != null) promptOverlay.SetActive(true);

        // Auto-start after a short delay so the player sees the prompt first
        StartCoroutine(AutoRequest());
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BeanController>() == null && other.GetComponent<FirstPersonControllerSimple>() == null) return;
        playerInside = false;
        if (!challengeActive && promptOverlay != null) promptOverlay.SetActive(false);
        StopAllCoroutines();
    }

    private IEnumerator AutoRequest()
    {
        yield return new WaitForSeconds(1.8f);
        if (playerInside && !challengeActive)
            RequestChallenge();
    }

    // ── UI builders ──────────────────────────────────────────────────────────

    private void BuildProximityPrompt()
    {
        promptOverlay = new GameObject("AiPad_Prompt");
        promptOverlay.transform.SetParent(transform, false);
        promptOverlay.transform.localPosition = new Vector3(0f, 3.2f, 0f);

        var canvas = promptOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rt = promptOverlay.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 120f);
        rt.localScale = Vector3.one * 0.006f;

        AddBG(promptOverlay.transform, new Color(0.06f, 0.04f, 0.16f, 0.9f));

        var txt = AddText(promptOverlay.transform, "✦  AI Challenge  ✦\n<size=11>Walk here to get a challenge built for you</size>",
            18, Color.white, new Vector2(16, 8));
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = true;

        promptOverlay.SetActive(false);
    }

    private void BuildPadPanel()
    {
        padPanel = new GameObject("AiPad_Panel");
        // Attach to a screen-space canvas so it covers the screen like the other pads
        var canvasGo = new GameObject("AiPad_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGo.AddComponent<GraphicRaycaster>();
        // Only add EventSystem if there isn't one already in the scene
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            canvasGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

        padPanel.transform.SetParent(canvasGo.transform, false);
        var panelRt = padPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

        AddBG(padPanel.transform, new Color(0.04f, 0.03f, 0.10f, 0.97f));

        // Header bar
        var header = AddRect(padPanel.transform, "Header",
            new Vector2(0, 0.88f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        AddBG(header.transform, new Color(0.18f, 0.06f, 0.42f, 1f));
        var headerTxt = AddText(header.transform, "✦ AI Challenge", 22, Color.white, Vector2.zero);
        headerTxt.alignment = TextAnchor.MiddleLeft;
        var headerRt = headerTxt.GetComponent<RectTransform>();
        headerRt.anchorMin = Vector2.zero; headerRt.anchorMax = Vector2.one;
        headerRt.offsetMin = new Vector2(24, 0); headerRt.offsetMax = Vector2.zero;

        pointsText = AddText(header.transform, "+? pts", 18, new Color(0.9f, 0.75f, 0.2f), Vector2.zero);
        pointsText.alignment = TextAnchor.MiddleRight;
        var ptRt = pointsText.GetComponent<RectTransform>();
        ptRt.anchorMin = Vector2.zero; ptRt.anchorMax = Vector2.one;
        ptRt.offsetMin = Vector2.zero; ptRt.offsetMax = new Vector2(-24, 0);

        // Title
        titleText = AddText(padPanel.transform, "Generating your challenge…",
            20, new Color(0.85f, 0.65f, 1f), Vector2.zero);
        titleText.fontStyle = FontStyle.Bold;
        SetAnchors(titleText.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.78f), new Vector2(0.98f, 0.88f), Vector2.zero, Vector2.zero);

        // Description
        descriptionText = AddText(padPanel.transform, "", 14, new Color(0.78f, 0.78f, 0.9f), Vector2.zero);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetAnchors(descriptionText.GetComponent<RectTransform>(),
            new Vector2(0.02f, 0.70f), new Vector2(0.98f, 0.78f), Vector2.zero, Vector2.zero);

        // Code editor (left half)
        var editorBg = AddRect(padPanel.transform, "EditorBg",
            new Vector2(0.01f, 0.22f), new Vector2(0.58f, 0.70f), Vector2.zero, Vector2.zero);
        AddBG(editorBg.transform, new Color(0.08f, 0.06f, 0.14f, 1f));
        var editorLabel = AddText(editorBg.transform, "Code", 12, new Color(0.5f, 0.4f, 0.8f), Vector2.zero);
        SetAnchors(editorLabel.GetComponent<RectTransform>(),
            new Vector2(0, 0.92f), new Vector2(1, 1f), new Vector2(8, 0), Vector2.zero);

        codeEditor = AddInputField(editorBg.transform);
        SetAnchors(codeEditor.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -26));

        // Output (right half)
        var outputBg = AddRect(padPanel.transform, "OutputBg",
            new Vector2(0.60f, 0.22f), new Vector2(0.99f, 0.70f), Vector2.zero, Vector2.zero);
        AddBG(outputBg.transform, new Color(0.05f, 0.08f, 0.06f, 1f));
        AddText(outputBg.transform, "Output", 12, new Color(0.3f, 0.7f, 0.4f), Vector2.zero);

        outputText = AddText(outputBg.transform, "", 13, new Color(0.7f, 0.95f, 0.7f), Vector2.zero);
        outputText.horizontalOverflow = HorizontalWrapMode.Wrap;
        outputText.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchors(outputText.GetComponent<RectTransform>(),
            Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -26));

        // Status text
        statusText = AddText(padPanel.transform, "", 18, Color.white, Vector2.zero);
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.fontStyle = FontStyle.Bold;
        SetAnchors(statusText.GetComponent<RectTransform>(),
            new Vector2(0.25f, 0.13f), new Vector2(0.75f, 0.21f), Vector2.zero, Vector2.zero);

        // Run button
        runButton = AddButton(padPanel.transform, "▶  Run",
            new Color(0.18f, 0.42f, 0.18f), new Vector2(0.35f, 0.04f), new Vector2(0.65f, 0.12f));
        runButton.onClick.AddListener(OnRunClicked);

        // Exit button
        var exitBtn = AddButton(padPanel.transform, "✕  Exit",
            new Color(0.35f, 0.1f, 0.1f), new Vector2(0.80f, 0.04f), new Vector2(0.99f, 0.12f));
        exitBtn.onClick.AddListener(OnExitClicked);

        padPanel.transform.parent.gameObject.SetActive(false); // hide canvas until needed
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void OnRunClicked()
    {
        if (running || currentTaskId < 0 || codeEditor == null) return;
        string code = codeEditor.text;
        if (string.IsNullOrWhiteSpace(code)) return;

        running = true;
        runButton.interactable = false;
        if (statusText) statusText.text = "Running…";
        if (outputText) outputText.text = "";

        Packet p = language == "cpp"
            ? (Packet)new ExecuteCPPCodePacket(code)
            : (Packet)new ExecutePythonCodePacket(code);
        _ = GameClient.Instance.SendPacket(p);
    }

    private void OnExitClicked()
    {
        challengeActive = false;
        currentTaskId = -1;
        padPanel.transform.parent.gameObject.SetActive(false);

        // Lock cursor and re-enable player movement
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        SetPlayerMovement(true);

        if (playerInside && promptOverlay != null)
            promptOverlay.SetActive(true);
        RobotCompanion.Trigger("idle");
    }

    private static void SetPlayerMovement(bool enabled)
    {
        var fps = FindObjectOfType<FirstPersonControllerSimple>();
        if (fps != null) fps.enabled = enabled;
        var bean = FindObjectOfType<BeanController>();
        if (bean != null) bean.enabled = enabled;
    }

    // ── Packet handling ──────────────────────────────────────────────────────

    private void OnPacket(Packet p)
    {
        switch (p)
        {
            case GenerateAiTaskResponsePacket r:
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnTaskReceived(r));
                break;
            case ExecutePythonCodeResponsePacket py:
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnOutput(py.Output, py.Error));
                break;
            case ExecuteCPPCodeResponsePacket cpp:
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnOutput(cpp.Output, cpp.Error));
                break;
            case AiResponsePacket ai:
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnEval(ai.Response));
                break;
        }
    }

    private void RequestChallenge()
    {
        challengeActive = true;
        if (promptOverlay) promptOverlay.SetActive(false);

        // Unlock cursor and disable player movement
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SetPlayerMovement(false);

        // Show the pad with loading state
        padPanel.transform.parent.gameObject.SetActive(true);
        if (titleText) titleText.text = "Analysing your learning profile…";
        if (descriptionText) descriptionText.text = "";
        if (codeEditor) codeEditor.text = "";
        if (outputText) outputText.text = "";
        if (statusText) statusText.text = "";
        if (runButton) runButton.interactable = false;

        _ = GameClient.Instance.SendPacket(new GenerateAiTaskPacket(language));
    }

    private void OnTaskReceived(GenerateAiTaskResponsePacket r)
    {
        currentTaskId = r.TaskId;
        if (titleText)       titleText.text      = r.Title;
        if (descriptionText) descriptionText.text = r.Description;
        if (pointsText)      pointsText.text      = $"+{r.PointValue} pts";
        if (codeEditor)      codeEditor.text       = r.CodeTemplate;
        if (runButton)       runButton.interactable = true;
        RobotCompanion.Trigger("entering_" + r.Language);
    }

    private void OnOutput(string output, string error)
    {
        string combined = string.IsNullOrWhiteSpace(error) ? output : output + "\n[ERR] " + error;
        if (outputText) outputText.text = combined;

        string question =
            $"Task: {(titleText ? titleText.text : "")}\n" +
            $"Code:\n{(codeEditor ? codeEditor.text : "")}\n" +
            $"Output:\n{combined}\n" +
            "Reply with exactly CORRECT or INCORRECT and one short sentence of feedback.";
        _ = GameClient.Instance.SendPacket(new AskAiPacket(question, language + "_eval"));
    }

    private void OnEval(string response)
    {
        running = false;
        if (runButton) runButton.interactable = true;

        bool correct = response != null &&
            response.TrimStart().StartsWith("CORRECT", System.StringComparison.OrdinalIgnoreCase);

        if (statusText) statusText.text = correct ? "✓  CORRECT!" : "✗  INCORRECT";

        if (correct && currentTaskId > 0)
        {
            long cid = GetChildId();
            if (cid > 0)
                _ = GameClient.Instance.SendPacket(new CompleteTaskPacket(cid, currentTaskId));
            _ = GameClient.Instance.SendPacket(new RecordLearningEventPacket(
                "code_challenge", language + "_ai_challenge", 1,
                titleText ? titleText.text : ""));
            RobotCompanion.Trigger("challenge_success");
        }
        else
        {
            _ = GameClient.Instance.SendPacket(new RecordLearningEventPacket(
                "code_challenge", language + "_ai_challenge", 0,
                outputText ? outputText.text : ""));
            RobotCompanion.Trigger("challenge_fail");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static long GetChildId()
    {
        string v = PlayerPrefs.GetString("loggedInChildId", "-1");
        return long.TryParse(v, out long id) ? id : -1;
    }

    // ── UI factory helpers ───────────────────────────────────────────────────

    private static void AddBG(Transform parent, Color color)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.transform.SetAsFirstSibling();
    }

    private static Text AddText(Transform parent, string content, int size, Color color, Vector2 padding)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = content;
        txt.fontSize = size;
        txt.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = padding; rt.offsetMax = -padding;
        return txt;
    }

    private static InputField AddInputField(Transform parent)
    {
        var go = new GameObject("InputField");
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.18f, 1f);
        var field = go.AddComponent<InputField>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var txt = textGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = new Color(0.9f, 0.95f, 1f);
        txt.supportRichText = false;
        var txtRt = textGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(6, 4); txtRt.offsetMax = new Vector2(-6, -4);

        field.textComponent = txt;
        field.lineType = InputField.LineType.MultiLineNewline;
        return field;
    }

    private static Button AddButton(Transform parent, string label, Color bg,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Button_" + label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = label; txt.fontSize = 16;
        txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
        var tRt = txtGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        return btn;
    }

    private static RectTransform AddRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        return rt;
    }

    private static void SetAnchors(RectTransform rt,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }
}
