using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialHoleTrigger : MonoBehaviour
{
    [SerializeField] private CppQuestionTrigger questionTrigger;
    [TextArea] [SerializeField] private string tutorialMessage = "Tutorial message.";
    [SerializeField] private float alignToHoleDuration = 0.18f;
    [SerializeField] private float descendIntoHoleDuration = 0.42f;
    [SerializeField] private float riseFromHoleDuration = 0.3f;
    [SerializeField] private float holeMouthHeight = 0.16f;
    [SerializeField] private float shaftBottomOffset = -0.58f;
    [SerializeField] private float exitSpeed = 6.8f;
    [SerializeField] private Vector3 tutorialRoomCenter = new Vector3(2400f, 2200f, 2400f);
    [SerializeField] private float tutorialCameraHeight = 9f;

    private bool transitionRunning;
    private bool leaveRequested;
    [SerializeField] private float reenterDelaySeconds = 1f;
    private float nextAllowedEnterTime;

    private Camera cam;
    private BeanCameraFollow camFollow;
    private BackgroundColorCycler camCycler;
    private Vector3 previousCamPosition;
    private Quaternion previousCamRotation;
    private int previousCullingMask;
    private CameraClearFlags previousClearFlags;
    private Color previousBackgroundColor;

    private Canvas tutorialCanvas;
    private CanvasGroup tutorialCanvasGroup;
    private Button beginLearningButton;
    private Button leaveButton;
    private Button returnToQuestionButton;
    private Text learningText;
    private Text codeLineA;
    private Text codeLineB;
    private Text codeLineC;
    private Text codeLineD;
    private Text explanationText;
    private Text calculationText;
    private Text finalAnswerText;
    private GameObject learningSequenceRoot;
    private TutorialBackdropAnimator backdropAnimator;
    private Coroutine learningRoutine;
    private bool learningSequenceRunning;
    private static readonly Color CodeLineBaseColor = new Color(0.86f, 0.95f, 1f, 1f);
    private static readonly Color CodeLineSettledColor = new Color(0.9f, 0.98f, 1f, 1f);
    private static readonly Color CodeLineHighlightColor = new Color(1f, 0.95f, 0.62f, 1f);
    [SerializeField] private string tutorialCodeLine1 = "int a = 4;";
    [SerializeField] private string tutorialCodeLine2 = "int b = 2;";
    [SerializeField] private string tutorialCodeLine3 = "int c = a * b;";
    [SerializeField] private string tutorialCodeLine4 = "";
    [SerializeField] private string tutorialExplanationLine1 = "int a = 4; -> Variable a stores the value 4.";
    [SerializeField] private string tutorialExplanationLine2 = "int b = 2; -> Variable b stores the value 2.";
    [SerializeField] private string tutorialExplanationLine3 = "int c = a * b; -> We multiply a and b.";
    [SerializeField] private string tutorialExplanationLine4 = "";
    [SerializeField] private string tutorialCalculationLine = "4 × 2 = 8";
    [SerializeField] private string tutorialFinalLine = "So the correct answer is 8.";

    public void SetQuestionTrigger(CppQuestionTrigger trigger)
    {
        questionTrigger = trigger;
    }

    public void SetTutorialText(string message)
    {
        tutorialMessage = message;
    }

    public void ConfigureLearningSequence(
        string codeLine1,
        string codeLine2,
        string codeLine3,
        string explanation1,
        string explanation2,
        string explanation3,
        string calculation,
        string finalLine,
        string codeLine4 = "",
        string explanation4 = "")
    {
        tutorialCodeLine1 = string.IsNullOrEmpty(codeLine1) ? tutorialCodeLine1 : codeLine1;
        tutorialCodeLine2 = string.IsNullOrEmpty(codeLine2) ? tutorialCodeLine2 : codeLine2;
        tutorialCodeLine3 = string.IsNullOrEmpty(codeLine3) ? tutorialCodeLine3 : codeLine3;
        tutorialCodeLine4 = codeLine4 ?? string.Empty;
        tutorialExplanationLine1 = string.IsNullOrEmpty(explanation1) ? tutorialExplanationLine1 : explanation1;
        tutorialExplanationLine2 = string.IsNullOrEmpty(explanation2) ? tutorialExplanationLine2 : explanation2;
        tutorialExplanationLine3 = string.IsNullOrEmpty(explanation3) ? tutorialExplanationLine3 : explanation3;
        tutorialExplanationLine4 = explanation4 ?? string.Empty;
        tutorialCalculationLine = string.IsNullOrEmpty(calculation) ? tutorialCalculationLine : calculation;
        tutorialFinalLine = string.IsNullOrEmpty(finalLine) ? tutorialFinalLine : finalLine;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (transitionRunning || questionTrigger == null || Time.time < nextAllowedEnterTime)
        {
            return;
        }

        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        ApplyStageSpecificTutorialContent();
        StartCoroutine(PlayTutorialHoleFlow(sphere));
    }

    private void ApplyStageSpecificTutorialContent()
    {
        if (questionTrigger == null)
        {
            return;
        }

        if (questionTrigger.GetQuestionStage() == 2)
        {
            ConfigureLearningSequence(
                "int x = 5;",
                "if (x > 3) {",
                " x = x + 2;",
                "int x = 5; → Variable x starts with the value 5.",
                "if (x > 3) → We check if x is greater than 3.",
                "5 > 3 → This condition is true.",
                "5 + 2 = 7",
                "So the correct answer is 7.",
                "}",
                "x = x + 2 → We add 2 to x.");
        }
    }

    private void OnDisable()
    {
        transitionRunning = false;
        leaveRequested = false;
        if (tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlayTutorialHoleFlow(BeanController sphere)
    {
        transitionRunning = true;

        var rb = sphere.GetComponent<Rigidbody>();
        sphere.SetMovementLocked(true);
        sphere.SetHardFreeze(true);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 start = sphere.transform.position;
        Vector3 mouth = transform.position + (Vector3.up * holeMouthHeight);
        Vector3 bottom = mouth + new Vector3(0f, shaftBottomOffset, 0f);
        Vector3 tutorialMouth = tutorialRoomCenter + (Vector3.up * holeMouthHeight);
        Vector3 tutorialBottom = tutorialMouth + new Vector3(0f, shaftBottomOffset, 0f);

        yield return MoveSphere(sphere.transform, start, mouth, alignToHoleDuration);
        yield return MoveSphere(sphere.transform, mouth, bottom, descendIntoHoleDuration);

        EnterTutorialRoomView();
        EnsureTutorialUi();

        sphere.transform.position = tutorialBottom;
        yield return MoveSphere(sphere.transform, tutorialBottom, tutorialMouth, riseFromHoleDuration);

        ShowTutorialUi();
        yield return new WaitUntil(() => leaveRequested);

        yield return FadeTutorialUi(false, 0.2f);
        if (tutorialCanvas != null)
        {
            tutorialCanvas.gameObject.SetActive(false);
        }

        yield return MoveSphere(sphere.transform, tutorialMouth, tutorialBottom, descendIntoHoleDuration);

        ExitTutorialRoomView();

        sphere.transform.position = bottom;
        yield return MoveSphere(sphere.transform, bottom, mouth, riseFromHoleDuration);

        sphere.SetHardFreeze(false);
        sphere.SetMovementLocked(false);
        if (rb != null)
        {
            rb.velocity = Vector3.up * exitSpeed * 0.22f;
            rb.angularVelocity = Vector3.zero;
        }

        transitionRunning = false;
        leaveRequested = false;
        nextAllowedEnterTime = Time.time + Mathf.Max(0f, reenterDelaySeconds);
    }

    private void EnterTutorialRoomView()
    {
        cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        camFollow = cam.GetComponent<BeanCameraFollow>();
        camCycler = cam.GetComponent<BackgroundColorCycler>();
        previousCamPosition = cam.transform.position;
        previousCamRotation = cam.transform.rotation;
        previousCullingMask = cam.cullingMask;
        previousClearFlags = cam.clearFlags;
        previousBackgroundColor = cam.backgroundColor;

        if (camFollow != null)
        {
            camFollow.enabled = false;
        }
        if (camCycler != null)
        {
            camCycler.enabled = false;
        }

        // Full blackout: world is hidden to make this feel like an isolated screen.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        cam.transform.position = tutorialRoomCenter + new Vector3(0f, tutorialCameraHeight, 0f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void ExitTutorialRoomView()
    {
        if (cam == null)
        {
            return;
        }

        cam.transform.position = previousCamPosition;
        cam.transform.rotation = previousCamRotation;
        cam.cullingMask = previousCullingMask;
        cam.clearFlags = previousClearFlags;
        cam.backgroundColor = previousBackgroundColor;

        if (camFollow != null)
        {
            camFollow.enabled = true;
        }
        if (camCycler != null)
        {
            camCycler.enabled = true;
        }
    }

    private void EnsureTutorialUi()
    {
        if (tutorialCanvas != null)
        {
            return;
        }

        var root = new GameObject("QuestionTutorialUI");
        tutorialCanvas = root.AddComponent<Canvas>();
        tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tutorialCanvas.sortingOrder = 1000;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root.AddComponent<GraphicRaycaster>();
        tutorialCanvasGroup = root.AddComponent<CanvasGroup>();
        tutorialCanvasGroup.alpha = 0f;
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialCanvasGroup.interactable = false;

        EnsureEventSystem();
        CreateBackdrop(root.transform);

        beginLearningButton = CreateButton(
            root.transform,
            "BeginLearningButton",
            new Vector2(520f, 118f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 18f),
            "Begin Learning",
            58,
            new Color(0.06f, 0.18f, 0.24f, 1f),
            new Color(0.14f, 0.34f, 0.44f, 1f),
            1.03f
        );

        leaveButton = CreateButton(
            root.transform,
            "LeavePipeButton",
            new Vector2(280f, 74f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(86f, 46f),
            "Leave Pipe",
            38,
            new Color(0.07f, 0.12f, 0.16f, 1f),
            new Color(0.18f, 0.24f, 0.3f, 1f),
            1.02f
        );

        returnToQuestionButton = CreateButton(
            root.transform,
            "ReturnToQuestionButton",
            new Vector2(420f, 88f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -300f),
            "Return to Question",
            44,
            new Color(0.08f, 0.2f, 0.24f, 1f),
            new Color(0.16f, 0.38f, 0.46f, 1f),
            1.03f
        );
        returnToQuestionButton.gameObject.SetActive(false);

        CreateLearningSequenceUi(root.transform);

        beginLearningButton.onClick.RemoveAllListeners();
        beginLearningButton.onClick.AddListener(OnBeginLearningClicked);

        leaveButton.onClick.RemoveAllListeners();
        leaveButton.onClick.AddListener(OnLeavePipeClicked);

        returnToQuestionButton.onClick.RemoveAllListeners();
        returnToQuestionButton.onClick.AddListener(OnReturnToQuestionClicked);

        tutorialCanvas.gameObject.SetActive(false);
    }

    private void ShowTutorialUi()
    {
        leaveRequested = false;
        learningSequenceRunning = false;
        if (learningRoutine != null)
        {
            StopCoroutine(learningRoutine);
            learningRoutine = null;
        }

        if (beginLearningButton != null)
        {
            beginLearningButton.interactable = true;
            beginLearningButton.gameObject.SetActive(true);
            var label = beginLearningButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = "Begin Learning";
            }
        }
        if (leaveButton != null)
        {
            leaveButton.interactable = true;
            leaveButton.gameObject.SetActive(true);
        }

        if (returnToQuestionButton != null)
        {
            returnToQuestionButton.gameObject.SetActive(false);
        }

        if (codeLineA != null) codeLineA.text = string.Empty;
        if (codeLineB != null) codeLineB.text = string.Empty;
        if (codeLineC != null) codeLineC.text = string.Empty;
        if (codeLineD != null) codeLineD.text = string.Empty;
        if (codeLineA != null) codeLineA.color = CodeLineBaseColor;
        if (codeLineB != null) codeLineB.color = CodeLineBaseColor;
        if (codeLineC != null) codeLineC.color = CodeLineBaseColor;
        if (codeLineD != null) codeLineD.color = CodeLineBaseColor;
        if (explanationText != null) explanationText.text = string.Empty;
        if (calculationText != null) calculationText.text = string.Empty;
        if (finalAnswerText != null) finalAnswerText.text = string.Empty;
        if (learningSequenceRoot != null) learningSequenceRoot.SetActive(false);

        tutorialCanvas.gameObject.SetActive(true);
        StartCoroutine(FadeTutorialUi(true, 0.28f));
    }

    private IEnumerator FadeTutorialUi(bool visible, float duration)
    {
        if (tutorialCanvasGroup == null)
        {
            yield break;
        }

        tutorialCanvasGroup.blocksRaycasts = visible;
        tutorialCanvasGroup.interactable = visible;

        float from = tutorialCanvasGroup.alpha;
        float to = visible ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));
            float smooth = k * k * (3f - (2f * k));
            tutorialCanvasGroup.alpha = Mathf.Lerp(from, to, smooth);
            yield return null;
        }

        tutorialCanvasGroup.alpha = to;
        if (!visible)
        {
            tutorialCanvasGroup.blocksRaycasts = false;
            tutorialCanvasGroup.interactable = false;
        }
    }

    private void OnBeginLearningClicked()
    {
        if (learningSequenceRunning)
        {
            return;
        }

        if (beginLearningButton != null)
        {
            beginLearningButton.gameObject.SetActive(false);
        }

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
        }

        if (returnToQuestionButton != null)
        {
            returnToQuestionButton.gameObject.SetActive(false);
        }

        learningRoutine = StartCoroutine(PlayQuestionOneLearningSequence());
    }

    private void OnLeavePipeClicked()
    {
        if (learningRoutine != null)
        {
            StopCoroutine(learningRoutine);
            learningRoutine = null;
        }
        learningSequenceRunning = false;
        if (learningSequenceRoot != null)
        {
            learningSequenceRoot.SetActive(false);
        }
        leaveRequested = true;
    }

    private void OnReturnToQuestionClicked()
    {
        OnLeavePipeClicked();
    }

    private void CreateLearningSequenceUi(Transform parent)
    {
        learningSequenceRoot = new GameObject("LearningSequenceRoot");
        learningSequenceRoot.transform.SetParent(parent, false);
        var rootRect = learningSequenceRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject codePanel = new GameObject("CodePanel");
        codePanel.transform.SetParent(learningSequenceRoot.transform, false);
        RectTransform codePanelRect = codePanel.AddComponent<RectTransform>();
        codePanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        codePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        codePanelRect.pivot = new Vector2(0.5f, 0.5f);
        codePanelRect.anchoredPosition = new Vector2(-330f, 140f);
        codePanelRect.sizeDelta = new Vector2(650f, 320f);

        Image codePanelBg = codePanel.AddComponent<Image>();
        codePanelBg.color = new Color(0.04f, 0.07f, 0.11f, 0.86f);
        Outline codeOutline = codePanel.AddComponent<Outline>();
        codeOutline.effectColor = new Color(0.4f, 0.82f, 1f, 0.5f);
        codeOutline.effectDistance = new Vector2(1.5f, -1.5f);

        codeLineA = CreateCodeLine(codePanel.transform, "CodeLineA", new Vector2(20f, -38f));
        codeLineB = CreateCodeLine(codePanel.transform, "CodeLineB", new Vector2(20f, -104f));
        codeLineC = CreateCodeLine(codePanel.transform, "CodeLineC", new Vector2(20f, -170f));
        codeLineD = CreateCodeLine(codePanel.transform, "CodeLineD", new Vector2(20f, -236f));

        explanationText = CreateInfoText(
            learningSequenceRoot.transform,
            "ExplanationText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(360f, 220f),
            new Vector2(1200f, 220f),
            34,
            new Color(0.82f, 0.97f, 1f, 1f));
        explanationText.horizontalOverflow = HorizontalWrapMode.Overflow;
        explanationText.verticalOverflow = VerticalWrapMode.Overflow;

        calculationText = CreateInfoText(
            learningSequenceRoot.transform,
            "CalculationText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -54f),
            new Vector2(780f, 90f),
            58,
            new Color(0.78f, 0.97f, 1f, 1f));

        finalAnswerText = CreateInfoText(
            learningSequenceRoot.transform,
            "FinalAnswerText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -170f),
            new Vector2(1100f, 120f),
            56,
            new Color(0.98f, 0.98f, 0.98f, 1f));

        learningSequenceRoot.SetActive(false);
    }

    private static Text CreateCodeLine(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(610f, 56f);

        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = CodeLineBaseColor;
        text.text = string.Empty;
        return text;
    }

    private static Text CreateInfoText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Text text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = string.Empty;
        return text;
    }

    private IEnumerator PlayQuestionOneLearningSequence()
    {
        learningSequenceRunning = true;
        if (learningSequenceRoot != null)
        {
            learningSequenceRoot.SetActive(true);
        }

        Text[] codeTargets = { codeLineA, codeLineB, codeLineC, codeLineD };
        string[] codeLines = { tutorialCodeLine1, tutorialCodeLine2, tutorialCodeLine3, tutorialCodeLine4 };
        for (int i = 0; i < codeLines.Length; i++)
        {
            if (string.IsNullOrEmpty(codeLines[i]))
            {
                continue;
            }

            yield return TypeTextAnimated(codeTargets[i], codeLines[i], 0.05f);
            if (codeTargets[i] != null) codeTargets[i].color = CodeLineSettledColor;
            if (leaveRequested) yield break;
            yield return WaitRealtime(0.35f);
        }
        yield return WaitRealtime(0.6f);

        string[] explanationLines =
        {
            tutorialExplanationLine1,
            tutorialExplanationLine2,
            tutorialExplanationLine3,
            tutorialExplanationLine4
        };
        for (int i = 0; i < explanationLines.Length; i++)
        {
            if (string.IsNullOrEmpty(explanationLines[i]))
            {
                continue;
            }

            yield return HighlightWithExplanation(codeTargets[i], explanationLines[i]);
            if (leaveRequested) yield break;
        }

        if (explanationText != null) explanationText.text = string.Empty;
        yield return TypeTextAnimated(calculationText, tutorialCalculationLine, 0.07f);
        if (leaveRequested) yield break;
        yield return WaitRealtime(1.2f);
        if (leaveRequested) yield break;

        if (finalAnswerText != null)
        {
            finalAnswerText.text = tutorialFinalLine;
            finalAnswerText.color = new Color(0.95f, 1f, 0.95f, 1f);
        }

        if (returnToQuestionButton != null)
        {
            returnToQuestionButton.gameObject.SetActive(true);
            returnToQuestionButton.interactable = true;
        }

        learningSequenceRunning = false;
        learningRoutine = null;
    }

    private IEnumerator HighlightWithExplanation(Text line, string explanation)
    {
        if (line == null)
        {
            yield break;
        }

        float t = 0f;
        while (t < 0.28f)
        {
            if (leaveRequested) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.28f);
            line.color = Color.Lerp(CodeLineSettledColor, CodeLineHighlightColor, k);
            yield return null;
        }

        yield return TypeAppendExplanationLine(explanation, 0.028f);
        yield return WaitRealtime(2f);
        if (leaveRequested) yield break;
        line.color = CodeLineSettledColor;
    }

    private IEnumerator TypeAppendExplanationLine(string line, float charDelay)
    {
        if (explanationText == null)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(explanationText.text))
        {
            explanationText.text += "\n";
        }

        for (int i = 0; i < line.Length; i++)
        {
            if (leaveRequested) yield break;
            explanationText.text += line[i];
            yield return WaitRealtime(charDelay);
        }
    }

    private IEnumerator TypeTextAnimated(Text target, string fullText, float charDelay)
    {
        if (target == null)
        {
            yield break;
        }

        target.text = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            if (leaveRequested) yield break;
            target.text += fullText[i];
            yield return WaitRealtime(charDelay);
        }
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Vector2 size,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        string label,
        int fontSize,
        Color normalColor,
        Color hoverColor,
        float hoverScale)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = obj.AddComponent<Image>();
        image.color = normalColor;

        var outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.85f, 1f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        var shadow = obj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -8f);

        var accentObj = new GameObject("AccentLine");
        accentObj.transform.SetParent(obj.transform, false);
        var accentRect = accentObj.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 4f);
        var accentImage = accentObj.AddComponent<Image>();
        accentImage.color = new Color(0.55f, 0.9f, 1f, 0.85f);

        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        button.colors = colors;

        var textObj = new GameObject("Label");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.9f, 0.99f, 1f, 1f);

        var hover = obj.AddComponent<FuturisticButtonHover>();
        hover.TargetImage = image;
        hover.TargetRect = rect;
        hover.NormalColor = normalColor;
        hover.HoverColor = hoverColor;
        hover.HoverScale = hoverScale;
        hover.AccentImage = accentImage;

        return button;
    }

    private void CreateBackdrop(Transform parent)
    {
        var root = new GameObject("TutorialBackdrop");
        root.transform.SetParent(parent, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var baseImage = root.AddComponent<Image>();
        baseImage.color = new Color(0.015f, 0.02f, 0.035f, 0.98f);

        Image topGlow = CreateBackdropLayer(root.transform, "TopGlow", new Color(0.06f, 0.16f, 0.24f, 0.42f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 340f));
        Image sideGlow = CreateBackdropLayer(root.transform, "SideGlow", new Color(0.04f, 0.11f, 0.18f, 0.33f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(420f, 900f));
        Image centerBand = CreateBackdropLayer(root.transform, "CenterBand", new Color(0.12f, 0.48f, 0.68f, 0.08f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1700f, 2f));

        backdropAnimator = root.AddComponent<TutorialBackdropAnimator>();
        backdropAnimator.TopGlow = topGlow;
        backdropAnimator.SideGlow = sideGlow;
        backdropAnimator.CenterBand = centerBand;
    }

    private static Image CreateBackdropLayer(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        var image = obj.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var evObj = new GameObject("EventSystem");
        evObj.AddComponent<EventSystem>();
        evObj.AddComponent<StandaloneInputModule>();
    }

    private static IEnumerator MoveSphere(Transform sphere, Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));
            float smooth = k * k * (3f - (2f * k));
            sphere.position = Vector3.Lerp(from, to, smooth);
            yield return null;
        }

        sphere.position = to;
    }
}

public class FuturisticButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image TargetImage;
    public RectTransform TargetRect;
    public Image AccentImage;
    public Color NormalColor = new Color(0.08f, 0.18f, 0.24f, 1f);
    public Color HoverColor = new Color(0.16f, 0.36f, 0.45f, 1f);
    public float HoverScale = 1.03f;

    private bool hovered;
    private bool pressed;

    private void Update()
    {
        if (TargetImage == null || TargetRect == null)
        {
            return;
        }

        Color targetColor = hovered ? HoverColor : NormalColor;
        float idlePulse = hovered ? 0f : Mathf.Sin(Time.unscaledTime * 2.2f) * 0.015f;
        float targetScale = hovered ? HoverScale : 1f + idlePulse;
        if (pressed)
        {
            targetScale -= 0.03f;
        }

        TargetImage.color = Color.Lerp(TargetImage.color, targetColor, Time.unscaledDeltaTime * 12f);
        TargetRect.localScale = Vector3.Lerp(TargetRect.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 12f);

        if (AccentImage != null)
        {
            Color accentTarget = hovered ? new Color(0.72f, 0.96f, 1f, 0.95f) : new Color(0.55f, 0.9f, 1f, 0.7f);
            AccentImage.color = Color.Lerp(AccentImage.color, accentTarget, Time.unscaledDeltaTime * 12f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }
}

public class TutorialBackdropAnimator : MonoBehaviour
{
    public Image TopGlow;
    public Image SideGlow;
    public Image CenterBand;

    private RectTransform topGlowRect;
    private RectTransform sideGlowRect;
    private RectTransform centerBandRect;

    private void Awake()
    {
        topGlowRect = TopGlow != null ? TopGlow.rectTransform : null;
        sideGlowRect = SideGlow != null ? SideGlow.rectTransform : null;
        centerBandRect = CenterBand != null ? CenterBand.rectTransform : null;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        if (topGlowRect != null)
        {
            topGlowRect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.35f) * 30f, -120f + Mathf.Sin(t * 0.7f) * 8f);
        }

        if (sideGlowRect != null)
        {
            sideGlowRect.anchoredPosition = new Vector2(-120f + Mathf.Sin(t * 0.4f) * 10f, Mathf.Cos(t * 0.55f) * 24f);
        }

        if (CenterBand != null)
        {
            float alpha = 0.06f + ((Mathf.Sin(t * 1.45f) * 0.5f + 0.5f) * 0.07f);
            var c = CenterBand.color;
            c.a = alpha;
            CenterBand.color = c;
        }

        if (centerBandRect != null)
        {
            centerBandRect.anchoredPosition = new Vector2(Mathf.Sin(t * 0.85f) * 50f, -18f);
        }
    }
}
