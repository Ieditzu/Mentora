using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CppQuestionTrigger : MonoBehaviour
{
    public Transform focusPoint;
    public Transform answersRoot;

    [TextArea(2, 4)] [SerializeField] private string questionPrompt = "What is the\nvalue of c?";
    [TextArea(3, 6)] [SerializeField] private string codeSnippet = "int a = 4;\nint b = 2;\nint c = a * b;";

    [SerializeField] private bool spawnNextQuestionOnCorrect = true;
    [SerializeField] private int questionStage = 1;
    [SerializeField] private float typeCharacterDelay = 0.045f;
    [SerializeField] private float feedbackCharacterDelay = 0.03f;

    [SerializeField] private bool useFirstPersonQuestionView = true;
    [SerializeField] private Vector3 firstPersonQuestionTextLocalToAnswers = new Vector3(-1.6f, 1.95f, 3.05f);
    [SerializeField] private Vector3 firstPersonCodeTextLocalToAnswers = new Vector3(1.6f, 1.95f, 3.05f);
    [SerializeField] private float firstPersonFixedGapBetweenTexts = 1.35f;
    [SerializeField] private float firstPersonFallbackTextWidth = 1.8f;
    [SerializeField] private float firstPersonTextSizeMultiplier = 0.78f;

    [SerializeField] private float questionSideOffset = 6.8f;
    [SerializeField] private float textHeight = 0.92f;
    [SerializeField] private float codeVerticalOffset = 0.5f;
    [SerializeField] private bool showQuestionBelowCode;
    [SerializeField] private float questionBelowCodeSpacing = 2.3f;

    [SerializeField] private float feedbackDuration = 1.05f;
    [SerializeField] private float feedbackPulseDuration = 0.75f;
    [SerializeField] private int feedbackFontSize = 42;

    [SerializeField] private Transform postAnswerPath;
    [SerializeField] private Transform nextQuestionRoot;

    private bool isAnimating;
    private bool awaitingAnswer;
    private bool feedbackActive;
    private bool solved;
    private bool keepQuestionVisible;

    private TextMesh questionText;
    private TextMesh codeText;

    private static Canvas overlayCanvas;
    private static RectTransform overlayRect;
    private static Text overlayText;

    private void Awake()
    {
        questionText = EnsureTextChild(
            "CppQuestionText",
            92,
            0.12f,
            TextAnchor.MiddleCenter,
            TextAlignment.Center,
            Color.white,
            new Vector3(0f, 0.26f, 0f));

        codeText = EnsureTextChild(
            "CppCodeText",
            68,
            0.09f,
            TextAnchor.UpperLeft,
            TextAlignment.Left,
            new Color(0.92f, 0.96f, 1f, 1f),
            new Vector3(0f, 0.26f, 0f));

        if (postAnswerPath == null && answersRoot != null)
        {
            Transform found = answersRoot.Find("PostAnswerPath");
            if (found != null)
            {
                postAnswerPath = found;
            }
        }

        if (useFirstPersonQuestionView)
        {
            firstPersonTextSizeMultiplier = Mathf.Clamp(firstPersonTextSizeMultiplier, 0.4f, 1f);
            ApplyFirstPersonTextSize(questionText);
            ApplyFirstPersonTextSize(codeText);
        }

        EnsureFeedbackOverlay();
        HideFeedbackOverlayImmediate();
    }

    private void LateUpdate()
    {
        if (!keepQuestionVisible)
        {
            return;
        }

        if (useFirstPersonQuestionView)
        {
            UpdateFirstPersonTextLayout();
            FaceTextsFirstPerson();
        }
        else
        {
            KeepTextsTopDown();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isAnimating || awaitingAnswer || solved || feedbackActive)
        {
            return;
        }

        if (!TryGetPlayer(other, out BeanController sphere, out FirstPersonControllerSimple fps))
        {
            return;
        }

        StartCoroutine(PlayQuestionSequence(sphere, fps));
    }

    private IEnumerator PlayQuestionSequence(BeanController sphere, FirstPersonControllerSimple fps)
    {
        isAnimating = true;
        awaitingAnswer = false;
        feedbackActive = false;
        keepQuestionVisible = true;

        SetPlayerLockState(sphere, fps, true, true);
        if (answersRoot != null)
        {
            answersRoot.gameObject.SetActive(false);
        }

        Vector3 anchor = focusPoint != null ? focusPoint.position : transform.position;
        Vector3 direction = Vector3.forward;
        if (answersRoot != null)
        {
            Vector3 toAnswers = answersRoot.position - anchor;
            toAnswers.y = 0f;
            if (toAnswers.sqrMagnitude > 0.001f)
            {
                direction = toAnswers.normalized;
            }
        }

        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 questionPos = anchor + (right * questionSideOffset) + (direction * 0.4f);
        Vector3 codePos = anchor - (right * 5.2f) + (direction * 0.4f);

        if (useFirstPersonQuestionView && answersRoot != null)
        {
            GetFirstPersonQuestionAndCodePositions(out questionPos, out codePos);
        }
        else if (showQuestionBelowCode)
        {
            codePos = anchor - (right * 5.2f) + (direction * 0.4f);
            questionPos = codePos - (direction * questionBelowCodeSpacing);
        }

        PlaceWorldText(questionText, questionPos + new Vector3(0f, textHeight, 0f));
        PlaceWorldText(codeText, codePos + new Vector3(0f, textHeight + codeVerticalOffset, 0f));

        if (questionText != null)
        {
            questionText.text = string.Empty;
            questionText.color = Color.white;
        }
        if (codeText != null)
        {
            codeText.text = string.Empty;
        }

        yield return StartCoroutine(TypeText(questionText, questionPrompt, typeCharacterDelay));
        yield return StartCoroutine(TypeText(codeText, codeSnippet, typeCharacterDelay));

        SetPlayerLockState(sphere, fps, false, false);
        if (answersRoot != null)
        {
            answersRoot.gameObject.SetActive(true);
        }

        awaitingAnswer = true;
        isAnimating = false;
    }

    public void SubmitAnswer(bool correct, BeanController sphere, FirstPersonControllerSimple fps)
    {
        if (!awaitingAnswer || solved || feedbackActive)
        {
            return;
        }

        awaitingAnswer = false;
        feedbackActive = true;

        if (correct)
        {
            solved = true;
        }

        StartCoroutine(ResolveAnswerFeedback(sphere, fps, correct));
    }

    private IEnumerator ResolveAnswerFeedback(BeanController sphere, FirstPersonControllerSimple fps, bool correct)
    {
        SetPlayerLockState(sphere, fps, true, true);

        string label = correct ? "Correct!" : "Wrong!";
        Color color = correct ? new Color(0.45f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);

        yield return StartCoroutine(TypeOverlayText(label, color, feedbackCharacterDelay));
        yield return StartCoroutine(PulseOverlay(color));
        yield return new WaitForSeconds(feedbackDuration);

        HideFeedbackOverlayImmediate();
        SetPlayerLockState(sphere, fps, false, false);

        if (correct)
        {
            keepQuestionVisible = false;
            if (questionText != null)
            {
                questionText.text = string.Empty;
            }
            if (codeText != null)
            {
                codeText.text = string.Empty;
            }

            RevealContinuationObjects();
        }
        else
        {
            awaitingAnswer = true;
            if (answersRoot != null)
            {
                answersRoot.gameObject.SetActive(true);
            }
        }

        feedbackActive = false;
    }

    private void RevealContinuationObjects()
    {
        if (answersRoot == null)
        {
            return;
        }

        answersRoot.gameObject.SetActive(true);
        for (int i = 0; i < answersRoot.childCount; i++)
        {
            Transform child = answersRoot.GetChild(i);
            if (child != null && child.name.StartsWith("AnswerPad"))
            {
                child.gameObject.SetActive(false);
            }
        }

        if (postAnswerPath != null)
        {
            postAnswerPath.gameObject.SetActive(true);
        }
        else
        {
            Transform fallback = answersRoot.Find("PostAnswerPath");
            if (fallback != null)
            {
                fallback.gameObject.SetActive(true);
            }
        }

        if (spawnNextQuestionOnCorrect && nextQuestionRoot != null)
        {
            nextQuestionRoot.gameObject.SetActive(true);
        }
    }

    public void SetQuestionContent(string question, string code)
    {
        questionPrompt = question;
        codeSnippet = code;
    }

    public void SetSpawnNextQuestionOnCorrect(bool enabled)
    {
        spawnNextQuestionOnCorrect = enabled;
    }

    public void SetQuestionStage(int stage)
    {
        questionStage = Mathf.Max(1, stage);
    }

    public int GetQuestionStage()
    {
        return questionStage;
    }

    public void SetQuestionSideOffset(float offset)
    {
        questionSideOffset = Mathf.Max(0.5f, offset);
    }

    public void SetCodeVerticalOffset(float offset)
    {
        codeVerticalOffset = Mathf.Max(0f, offset);
    }

    public void SetLayoutQuestionBelowCode(bool enabled)
    {
        showQuestionBelowCode = enabled;
    }

    public void SetPostAnswerPath(Transform pathRoot)
    {
        postAnswerPath = pathRoot;
    }

    public void SetNextQuestionRoot(Transform root)
    {
        nextQuestionRoot = root;
    }

    private TextMesh EnsureTextChild(
        string childName,
        int fontSize,
        float charSize,
        TextAnchor anchor,
        TextAlignment alignment,
        Color color,
        Vector3 localPosition)
    {
        Transform existing = transform.Find(childName);
        GameObject textObject;
        if (existing == null)
        {
            textObject = new GameObject(childName);
            textObject.transform.SetParent(transform);
        }
        else
        {
            textObject = existing.gameObject;
        }

        TextMesh textMesh = textObject.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = textObject.AddComponent<TextMesh>();
        }

        textMesh.text = string.Empty;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = charSize;
        textMesh.anchor = anchor;
        textMesh.alignment = alignment;
        textMesh.color = color;

        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = useFirstPersonQuestionView
            ? Quaternion.Euler(0f, -180f, 0f)
            : Quaternion.Euler(90f, 0f, 0f);
        textObject.transform.localScale = Vector3.one;

        return textMesh;
    }

    private void PlaceWorldText(TextMesh mesh, Vector3 worldPosition)
    {
        if (mesh == null)
        {
            return;
        }

        mesh.transform.position = worldPosition;
        mesh.transform.rotation = useFirstPersonQuestionView
            ? Quaternion.Euler(0f, -180f, 0f)
            : Quaternion.Euler(90f, 0f, 0f);
    }

    private void FaceTextsFirstPerson()
    {
        if (questionText != null)
        {
            questionText.transform.rotation = Quaternion.Euler(0f, -180f, 0f);
        }
        if (codeText != null)
        {
            codeText.transform.rotation = Quaternion.Euler(0f, -180f, 0f);
        }
    }

    private void KeepTextsTopDown()
    {
        if (questionText != null)
        {
            questionText.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        if (codeText != null)
        {
            codeText.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private void ApplyFirstPersonTextSize(TextMesh mesh)
    {
        if (mesh == null)
        {
            return;
        }

        mesh.characterSize *= firstPersonTextSizeMultiplier;
    }

    private void GetFirstPersonQuestionAndCodePositions(out Vector3 questionPos, out Vector3 codePos)
    {
        if (answersRoot == null)
        {
            questionPos = transform.position + new Vector3(-1f, 1.8f, 0f);
            codePos = transform.position + new Vector3(1f, 1.8f, 0f);
            return;
        }

        Vector3 baseQuestion = answersRoot.TransformPoint(firstPersonQuestionTextLocalToAnswers);
        Vector3 baseCode = answersRoot.TransformPoint(firstPersonCodeTextLocalToAnswers);
        Vector3 middle = (baseQuestion + baseCode) * 0.5f;

        Vector3 lateral = answersRoot.right;
        lateral.y = 0f;
        if (lateral.sqrMagnitude < 0.001f)
        {
            lateral = Vector3.right;
        }
        lateral.Normalize();

        float questionWidth = GetTextWorldWidth(questionText);
        float codeWidth = GetTextWorldWidth(codeText);
        float halfSeparation = (questionWidth * 0.5f) + (codeWidth * 0.5f) + (firstPersonFixedGapBetweenTexts * 0.5f);

        questionPos = middle - (lateral * halfSeparation);
        codePos = middle + (lateral * halfSeparation);
        questionPos.y = baseQuestion.y;
        codePos.y = baseCode.y;
    }

    private float GetTextWorldWidth(TextMesh mesh)
    {
        if (mesh == null)
        {
            return firstPersonFallbackTextWidth;
        }

        Renderer meshRenderer = mesh.GetComponent<Renderer>();
        if (meshRenderer == null)
        {
            return firstPersonFallbackTextWidth;
        }

        float width = meshRenderer.bounds.size.x;
        return width > 0.001f ? width : firstPersonFallbackTextWidth;
    }

    private void UpdateFirstPersonTextLayout()
    {
        if (answersRoot == null)
        {
            return;
        }

        GetFirstPersonQuestionAndCodePositions(out Vector3 questionPos, out Vector3 codePos);

        if (questionText != null)
        {
            questionText.transform.position = questionPos + new Vector3(0f, textHeight, 0f);
        }

        if (codeText != null)
        {
            codeText.transform.position = codePos + new Vector3(0f, textHeight + codeVerticalOffset, 0f);
        }
    }

    private void EnsureFeedbackOverlay()
    {
        if (overlayCanvas != null && overlayRect != null && overlayText != null)
        {
            return;
        }

        GameObject existingCanvas = GameObject.Find("QuestionFeedbackOverlay");
        if (existingCanvas != null)
        {
            overlayCanvas = existingCanvas.GetComponent<Canvas>();
            Transform existingText = existingCanvas.transform.Find("CenterFeedbackText");
            if (existingText != null)
            {
                overlayRect = existingText.GetComponent<RectTransform>();
                overlayText = existingText.GetComponent<Text>();
            }

            if (overlayCanvas != null && overlayRect != null && overlayText != null)
            {
                return;
            }
        }

        GameObject canvasObj = new GameObject("QuestionFeedbackOverlay");
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 3000;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("CenterFeedbackText");
        textObj.transform.SetParent(canvasObj.transform, false);

        overlayRect = textObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = new Vector2(760f, 130f);

        overlayText = textObj.AddComponent<Text>();
        overlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overlayText.fontSize = Mathf.Clamp(feedbackFontSize, 22, 72);
        overlayText.alignment = TextAnchor.MiddleCenter;
        overlayText.horizontalOverflow = HorizontalWrapMode.Overflow;
        overlayText.verticalOverflow = VerticalWrapMode.Overflow;
        overlayText.raycastTarget = false;
        overlayText.text = string.Empty;
        overlayText.color = new Color(1f, 1f, 1f, 0f);
    }

    private void HideFeedbackOverlayImmediate()
    {
        EnsureFeedbackOverlay();
        if (overlayText != null)
        {
            overlayText.text = string.Empty;
            Color c = overlayText.color;
            c.a = 0f;
            overlayText.color = c;
        }
        if (overlayRect != null)
        {
            overlayRect.localScale = Vector3.one;
        }
    }

    private IEnumerator TypeOverlayText(string fullText, Color color, float charDelay)
    {
        EnsureFeedbackOverlay();
        if (overlayText == null)
        {
            yield break;
        }

        overlayText.fontSize = Mathf.Clamp(feedbackFontSize, 22, 72);
        overlayText.text = string.Empty;
        overlayText.color = new Color(color.r, color.g, color.b, 1f);

        string typed = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            typed += fullText[i];
            overlayText.text = typed;
            yield return new WaitForSeconds(charDelay);
        }
    }

    private IEnumerator PulseOverlay(Color color)
    {
        EnsureFeedbackOverlay();
        if (overlayText == null || overlayRect == null)
        {
            yield break;
        }

        Vector3 baseScale = Vector3.one;
        float t = 0f;
        while (t < feedbackPulseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, feedbackPulseDuration));
            float pulse = 1f + Mathf.Sin(k * Mathf.PI * 3f) * (1f - k) * 0.22f;
            float alpha = 1f - (k * 0.1f);

            overlayRect.localScale = baseScale * pulse;
            overlayText.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        overlayRect.localScale = Vector3.one;
        overlayText.color = new Color(color.r, color.g, color.b, 1f);
    }

    private IEnumerator TypeText(TextMesh mesh, string fullText, float charDelay)
    {
        if (mesh == null)
        {
            yield break;
        }

        mesh.text = string.Empty;
        Color color = mesh.color;
        color.a = 1f;
        mesh.color = color;

        string typed = string.Empty;
        for (int i = 0; i < fullText.Length; i++)
        {
            typed += fullText[i];
            mesh.text = typed;

            if (fullText[i] == '\n')
            {
                yield return new WaitForSeconds(charDelay * 2.4f);
            }
            else
            {
                yield return new WaitForSeconds(charDelay);
            }
        }
    }

    private static bool TryGetPlayer(Collider other, out BeanController sphere, out FirstPersonControllerSimple fps)
    {
        sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            sphere = other.GetComponentInParent<BeanController>();
        }

        fps = other.GetComponent<FirstPersonControllerSimple>();
        if (fps == null)
        {
            fps = other.GetComponentInParent<FirstPersonControllerSimple>();
        }

        return sphere != null || fps != null;
    }

    private static void SetPlayerLockState(
        BeanController sphere,
        FirstPersonControllerSimple fps,
        bool movementLocked,
        bool hardFreeze)
    {
        if (sphere != null)
        {
            sphere.SetMovementLocked(movementLocked);
            sphere.SetHardFreeze(hardFreeze);
        }

        if (fps != null)
        {
            fps.SetMovementLocked(movementLocked);
            fps.SetHardFreeze(hardFreeze);
        }
    }
}
