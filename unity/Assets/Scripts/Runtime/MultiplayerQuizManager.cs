using System;
using System.Collections;
using System.Collections.Generic;
using Mentora.Network;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kahoot-style multiplayer quiz for the quiz island.
/// - Host: walks into the trigger → fetches community courses → picks one → starts.
/// - All players: see a large world-space theater screen with the question.
/// - Each player: gets a personal screen-space answer tablet + countdown timer.
///
/// Auto-attaches to a GameObject named "QuizIslandAnchor" at runtime.
/// </summary>
public class MultiplayerQuizManager : MonoBehaviour
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const int QuizTimerSeconds = 20;
    private static readonly Vector3 TheaterScreenLocalPos   = new Vector3(0f, 5f, 6f);
    private static readonly Vector3 TheaterScreenLocalScale = new Vector3(0.018f, 0.018f, 1f);
    private static readonly Vector2 TheaterCanvasSize       = new Vector2(900f, 500f);

    private static readonly Color[] AnswerColors = {
        new Color(0.87f, 0.20f, 0.20f, 1f), // A — red
        new Color(0.13f, 0.53f, 0.90f, 1f), // B — blue
        new Color(0.13f, 0.69f, 0.30f, 1f), // C — green
        new Color(0.95f, 0.61f, 0.07f, 1f), // D — orange
    };
    private static readonly string[] AnswerLabels = { "A", "B", "C", "D" };

    // ── State ────────────────────────────────────────────────────────────────

    private static MultiplayerQuizManager instance;

    // Theater screen
    private Canvas theaterCanvas;
    private Text   theaterQuestionText;
    private Text   theaterSubText;
    private Text[] theaterAnswerTexts = new Text[4];
    private Image[] theaterAnswerBgs  = new Image[4];

    // Personal answer tablet (screen-space)
    private Canvas   answerCanvas;
    private Text     answerTimerText;
    private Text     answerQuestionText;
    private Button[] answerButtons     = new Button[4];
    private Text[]   answerButtonTexts = new Text[4];
    private bool     answerLocked;

    // Host panel
    private Canvas hostCanvas;
    private Text   hostStatusText;
    private Text   courseListText;        // shows fetched course names
    private Button startQuizButton;
    private Button prevCourseButton;
    private Button nextCourseButton;
    private Text   selectedCourseLabel;
    private bool   hostPanelOpen;

    // Community courses
    private CourseItemDto[]  availableCourses;
    private int              selectedCourseIndex;
    private CommunityQuizData fetchedCourse;
    private bool             fetchInProgress;
    private bool             packetSubscribed;

    // Quiz session
    private bool   isHost;
    private bool   quizRunning;
    private int    currentQuestionIndex;
    private int    totalQuestions;
    private QuizQuestion[] questions;
    private float  timerRemaining;
    private bool   timerActive;
    private readonly Dictionary<string, int> scores         = new Dictionary<string, int>();
    private readonly Dictionary<string, int> pendingAnswers = new Dictionary<string, int>();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void Start()
    {
        BuildTheaterScreen();
        BuildAnswerWindow();
        BuildHostPanel();
        EnsureTrigger();
        SubscribeQuizPackets();
    }

    private void OnDestroy()
    {
        UnsubscribeQuizPackets();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!timerActive) return;
        timerRemaining -= Time.deltaTime;

        if (answerCanvas != null && answerCanvas.gameObject.activeSelf && answerTimerText != null)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, timerRemaining));
            answerTimerText.text  = secs.ToString();
            answerTimerText.color = timerRemaining < 5f ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }

        if (timerRemaining <= 0f && isHost)
        {
            timerActive = false;
            BroadcastResults();
        }
    }

    // ── Trigger ──────────────────────────────────────────────────────────────

    private void EnsureTrigger()
    {
        var rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        var col = GetComponent<BoxCollider>() ?? gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center    = new Vector3(0f, 1f, 0f);
        col.size      = new Vector3(6f, 3f, 6f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BeanController>() == null &&
            other.GetComponent<FirstPersonControllerSimple>() == null) return;

        // Open host panel for the host (anyone who started the server).
        // Also allow opening in single-player / editor so you can test without a second client.
        bool hosting = MultiplayerSessionManager.Instance == null ||
                       MultiplayerSessionManager.Instance.CurrentStatus.Contains("Hosting") ||
                       MultiplayerSessionManager.Instance.CurrentStatus.Contains("Offline");

        if (hosting && !quizRunning)
        {
            isHost = true;
            OpenHostPanel();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BeanController>() == null &&
            other.GetComponent<FirstPersonControllerSimple>() == null) return;

        if (!quizRunning && hostPanelOpen) CloseHostPanel();
    }

    // ── Theater Screen ───────────────────────────────────────────────────────

    private void BuildTheaterScreen()
    {
        var go = new GameObject("QuizTheaterScreen");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = TheaterScreenLocalPos;
        go.transform.localScale    = TheaterScreenLocalScale;

        theaterCanvas             = go.AddComponent<Canvas>();
        theaterCanvas.renderMode  = RenderMode.WorldSpace;
        go.GetComponent<RectTransform>().sizeDelta = TheaterCanvasSize;

        var bg = MakeImage(go.transform, "BG", new Color(0.05f, 0.05f, 0.12f, 0.97f));
        StretchFull(bg.rectTransform);

        var topBar = MakeImage(go.transform, "TopBar", new Color(0.15f, 0.08f, 0.38f, 1f));
        topBar.rectTransform.anchorMin = new Vector2(0f, 0.85f);
        topBar.rectTransform.anchorMax = Vector2.one;
        topBar.rectTransform.offsetMin = topBar.rectTransform.offsetMax = Vector2.zero;
        var topTxt = MakeText(topBar.transform, "MENTORA QUIZ", 36, Color.white, FontStyle.Bold);
        StretchFull(topTxt.rectTransform);
        topTxt.alignment = TextAnchor.MiddleCenter;

        theaterSubText = MakeText(go.transform, "Waiting for host to start…", 22, new Color(0.7f, 0.7f, 1f), FontStyle.Normal);
        theaterSubText.rectTransform.anchorMin = new Vector2(0f, 0.74f);
        theaterSubText.rectTransform.anchorMax = new Vector2(1f, 0.85f);
        theaterSubText.rectTransform.offsetMin = new Vector2(20f, 0f);
        theaterSubText.rectTransform.offsetMax = new Vector2(-20f, 0f);
        theaterSubText.alignment = TextAnchor.MiddleCenter;

        theaterQuestionText = MakeText(go.transform, "", 34, Color.white, FontStyle.Bold);
        theaterQuestionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        theaterQuestionText.verticalOverflow   = VerticalWrapMode.Overflow;
        theaterQuestionText.rectTransform.anchorMin = new Vector2(0f, 0.42f);
        theaterQuestionText.rectTransform.anchorMax = new Vector2(1f, 0.74f);
        theaterQuestionText.rectTransform.offsetMin = new Vector2(24f, 0f);
        theaterQuestionText.rectTransform.offsetMax = new Vector2(-24f, 0f);
        theaterQuestionText.alignment = TextAnchor.MiddleCenter;

        Vector2[] mins = { new Vector2(0.02f, 0.22f), new Vector2(0.52f, 0.22f), new Vector2(0.02f, 0.02f), new Vector2(0.52f, 0.02f) };
        Vector2[] maxs = { new Vector2(0.48f, 0.40f), new Vector2(0.98f, 0.40f), new Vector2(0.48f, 0.20f), new Vector2(0.98f, 0.20f) };

        for (int i = 0; i < 4; i++)
        {
            var box = MakeImage(go.transform, "Ans" + i, new Color(0.1f, 0.1f, 0.18f, 1f));
            box.rectTransform.anchorMin = mins[i];
            box.rectTransform.anchorMax = maxs[i];
            box.rectTransform.offsetMin = new Vector2(4f, 4f);
            box.rectTransform.offsetMax = new Vector2(-4f, -4f);
            theaterAnswerBgs[i] = box;

            var lbl = MakeText(box.transform, AnswerLabels[i] + ". —", 26, Color.white, FontStyle.Bold);
            lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
            StretchFull(lbl.rectTransform);
            lbl.rectTransform.offsetMin = new Vector2(10f, 4f);
            lbl.rectTransform.offsetMax = new Vector2(-10f, -4f);
            lbl.alignment = TextAnchor.MiddleLeft;
            theaterAnswerTexts[i] = lbl;
        }
    }

    private void SetTheaterQuestion(string question, string[] options, string sub)
    {
        if (theaterQuestionText != null) theaterQuestionText.text = question;
        if (theaterSubText != null)      theaterSubText.text      = sub;
        for (int i = 0; i < 4; i++)
        {
            bool has = options != null && i < options.Length;
            if (theaterAnswerBgs[i] != null)  theaterAnswerBgs[i].color  = has ? AnswerColors[i] * 0.7f : new Color(0.1f, 0.1f, 0.18f, 1f);
            if (theaterAnswerTexts[i] != null) theaterAnswerTexts[i].text = has ? AnswerLabels[i] + ". " + options[i] : "";
        }
    }

    private void SetTheaterResult(int correctIndex)
    {
        for (int i = 0; i < 4; i++)
        {
            if (theaterAnswerBgs[i] != null)
                theaterAnswerBgs[i].color = (i == correctIndex) ? AnswerColors[2] : new Color(0.2f, 0.1f, 0.1f, 1f);
            if (theaterAnswerTexts[i] != null && i == correctIndex)
                theaterAnswerTexts[i].text = "✓ " + theaterAnswerTexts[i].text;
        }
        if (theaterSubText != null) theaterSubText.text = "Time's up! See the correct answer above.";
        if (theaterQuestionText != null) theaterQuestionText.text = "";
    }

    private void ShowScoresOnTheater(Dictionary<string, int> scoreMap)
    {
        var sorted = new List<KeyValuePair<string, int>>(scoreMap);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        for (int i = 0; i < 4; i++)
        {
            if (theaterAnswerBgs[i] != null)  theaterAnswerBgs[i].color  = new Color(0.10f, 0.14f, 0.22f, 1f);
            if (theaterAnswerTexts[i] != null)
            {
                if (i < sorted.Count)
                {
                    string id = sorted[i].Key.Length > 8 ? sorted[i].Key.Substring(0, 8) + "…" : sorted[i].Key;
                    theaterAnswerTexts[i].text = (i + 1) + ".  " + id + "   " + sorted[i].Value + " pts";
                }
                else theaterAnswerTexts[i].text = "";
            }
        }
    }

    // ── Answer Tablet (screen-space, per player) ─────────────────────────────

    private void BuildAnswerWindow()
    {
        var go = new GameObject("QuizAnswerCanvas");
        DontDestroyOnLoad(go);
        answerCanvas             = go.AddComponent<Canvas>();
        answerCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        answerCanvas.sortingOrder = 55;
        go.AddComponent<GraphicRaycaster>();
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight  = 0.5f;
        EnsureEventSystem();

        // Semi-transparent backdrop — bottom half of screen
        var backdrop = MakeImage(go.transform, "Backdrop", new Color(0.04f, 0.03f, 0.10f, 0.93f));
        backdrop.rectTransform.anchorMin = new Vector2(0f, 0f);
        backdrop.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;

        // Timer top-right
        answerTimerText = MakeText(backdrop.transform, "20", 72, Color.white, FontStyle.Bold);
        answerTimerText.rectTransform.anchorMin = new Vector2(0.85f, 0.75f);
        answerTimerText.rectTransform.anchorMax = Vector2.one;
        answerTimerText.rectTransform.offsetMin = answerTimerText.rectTransform.offsetMax = Vector2.zero;
        answerTimerText.alignment = TextAnchor.MiddleCenter;

        // Question echo top-left
        answerQuestionText = MakeText(backdrop.transform, "", 22, new Color(0.8f, 0.8f, 1f), FontStyle.Normal);
        answerQuestionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        answerQuestionText.rectTransform.anchorMin = new Vector2(0f, 0.78f);
        answerQuestionText.rectTransform.anchorMax = new Vector2(0.84f, 1f);
        answerQuestionText.rectTransform.offsetMin = new Vector2(16f, 0f);
        answerQuestionText.rectTransform.offsetMax = new Vector2(-8f, -4f);
        answerQuestionText.alignment = TextAnchor.MiddleLeft;

        // 4 answer buttons — 2x2
        Vector2[] bMins = { new Vector2(0.01f, 0.42f), new Vector2(0.51f, 0.42f), new Vector2(0.01f, 0.02f), new Vector2(0.51f, 0.02f) };
        Vector2[] bMaxs = { new Vector2(0.49f, 0.76f), new Vector2(0.99f, 0.76f), new Vector2(0.49f, 0.38f), new Vector2(0.99f, 0.38f) };

        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var btn = MakeButton(backdrop.transform, AnswerLabels[i], AnswerColors[i]);
            var rt  = btn.GetComponent<RectTransform>();
            rt.anchorMin = bMins[i]; rt.anchorMax = bMaxs[i];
            rt.offsetMin = new Vector2(6f, 6f); rt.offsetMax = new Vector2(-6f, -6f);
            var txt = btn.GetComponentInChildren<Text>();
            txt.fontSize = 30; txt.fontStyle = FontStyle.Bold;
            answerButtonTexts[i] = txt;
            answerButtons[i]     = btn;
            btn.onClick.AddListener(() => OnAnswerChosen(idx));
        }

        go.SetActive(false);
    }

    private void ShowAnswerWindow(string question, string[] options)
    {
        answerLocked = false;
        if (answerQuestionText != null) answerQuestionText.text = question;
        for (int i = 0; i < 4; i++)
        {
            bool has = options != null && i < options.Length;
            if (answerButtons[i] != null)     answerButtons[i].gameObject.SetActive(has);
            if (answerButtonTexts[i] != null) answerButtonTexts[i].text = has ? AnswerLabels[i] + "  " + options[i] : "";
            var img = answerButtons[i]?.GetComponent<Image>();
            if (img != null) img.color = AnswerColors[i];
            if (answerButtons[i] != null) answerButtons[i].interactable = true;
        }
        if (answerCanvas != null) answerCanvas.gameObject.SetActive(true);
        SetPlayerMovement(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void HideAnswerWindow()
    {
        if (answerCanvas != null) answerCanvas.gameObject.SetActive(false);
        SetPlayerMovement(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void OnAnswerChosen(int idx)
    {
        if (answerLocked) return;
        answerLocked = true;
        for (int i = 0; i < 4; i++)
        {
            var img = answerButtons[i]?.GetComponent<Image>();
            if (img != null) img.color = (i == idx) ? AnswerColors[i] : AnswerColors[i] * 0.35f;
            if (answerButtons[i] != null) answerButtons[i].interactable = false;
        }
        string myId = MultiplayerSessionManager.Instance?.LocalClientId ?? string.Empty;
        if (!string.IsNullOrEmpty(myId))
            MultiplayerSessionManager.Instance?.SendQuizPacketToHost(new QuizAnswerPacket(myId, idx));
    }

    // ── Host Panel ────────────────────────────────────────────────────────────

    private void BuildHostPanel()
    {
        var go = new GameObject("QuizHostCanvas");
        DontDestroyOnLoad(go);
        hostCanvas             = go.AddComponent<Canvas>();
        hostCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        hostCanvas.sortingOrder = 56;
        go.AddComponent<GraphicRaycaster>();
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight  = 0.5f;
        EnsureEventSystem();

        // Panel
        var panel = MakeImage(go.transform, "Panel", new Color(0.06f, 0.04f, 0.14f, 0.97f));
        panel.rectTransform.anchorMin = new Vector2(0.25f, 0.15f);
        panel.rectTransform.anchorMax = new Vector2(0.75f, 0.85f);
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        // Header
        var header = MakeImage(panel.transform, "Header", new Color(0.18f, 0.08f, 0.40f, 1f));
        header.rectTransform.anchorMin = new Vector2(0f, 0.88f);
        header.rectTransform.anchorMax = Vector2.one;
        header.rectTransform.offsetMin = header.rectTransform.offsetMax = Vector2.zero;
        var hTxt = MakeText(header.transform, "Quiz Island — Host", 28, Color.white, FontStyle.Bold);
        StretchFull(hTxt.rectTransform);
        hTxt.alignment = TextAnchor.MiddleCenter;

        // Status
        hostStatusText = MakeText(panel.transform, "Press Fetch to load community quizzes.", 20, new Color(0.7f, 0.85f, 1f), FontStyle.Normal);
        hostStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hostStatusText.rectTransform.anchorMin = new Vector2(0.02f, 0.76f);
        hostStatusText.rectTransform.anchorMax = new Vector2(0.98f, 0.88f);
        hostStatusText.rectTransform.offsetMin = hostStatusText.rectTransform.offsetMax = Vector2.zero;
        hostStatusText.alignment = TextAnchor.MiddleCenter;

        // Course list display
        courseListText = MakeText(panel.transform, "", 22, new Color(0.9f, 0.9f, 1f), FontStyle.Normal);
        courseListText.horizontalOverflow = HorizontalWrapMode.Wrap;
        courseListText.verticalOverflow   = VerticalWrapMode.Overflow;
        courseListText.rectTransform.anchorMin = new Vector2(0.02f, 0.30f);
        courseListText.rectTransform.anchorMax = new Vector2(0.98f, 0.76f);
        courseListText.rectTransform.offsetMin = courseListText.rectTransform.offsetMax = Vector2.zero;
        courseListText.alignment = TextAnchor.UpperLeft;

        // Selected course highlight
        selectedCourseLabel = MakeText(panel.transform, "", 24, new Color(0.4f, 1f, 0.6f), FontStyle.Bold);
        selectedCourseLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        selectedCourseLabel.rectTransform.anchorMin = new Vector2(0.02f, 0.20f);
        selectedCourseLabel.rectTransform.anchorMax = new Vector2(0.98f, 0.30f);
        selectedCourseLabel.rectTransform.offsetMin = selectedCourseLabel.rectTransform.offsetMax = Vector2.zero;
        selectedCourseLabel.alignment = TextAnchor.MiddleCenter;

        // Prev / Next course picker
        prevCourseButton = MakeButton(panel.transform, "◀", new Color(0.20f, 0.20f, 0.35f, 1f));
        prevCourseButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.12f);
        prevCourseButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.20f);
        prevCourseButton.GetComponent<RectTransform>().offsetMin = prevCourseButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        prevCourseButton.onClick.AddListener(OnPrevCourse);
        prevCourseButton.gameObject.SetActive(false);

        nextCourseButton = MakeButton(panel.transform, "▶", new Color(0.20f, 0.20f, 0.35f, 1f));
        nextCourseButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.82f, 0.12f);
        nextCourseButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.20f);
        nextCourseButton.GetComponent<RectTransform>().offsetMin = nextCourseButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        nextCourseButton.onClick.AddListener(OnNextCourse);
        nextCourseButton.gameObject.SetActive(false);

        // Buttons row
        var fetchBtn = MakeButton(panel.transform, "↻  Fetch Quizzes", new Color(0.13f, 0.40f, 0.60f, 1f));
        fetchBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.02f);
        fetchBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.42f, 0.10f);
        fetchBtn.GetComponent<RectTransform>().offsetMin = fetchBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        fetchBtn.onClick.AddListener(OnFetchCommunityClicked);

        startQuizButton = MakeButton(panel.transform, "▶  Start Quiz", new Color(0.13f, 0.55f, 0.20f, 1f));
        startQuizButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.44f, 0.02f);
        startQuizButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.76f, 0.10f);
        startQuizButton.GetComponent<RectTransform>().offsetMin = startQuizButton.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        startQuizButton.onClick.AddListener(OnStartQuizClicked);
        startQuizButton.interactable = false;

        var closeBtn = MakeButton(panel.transform, "✕", new Color(0.40f, 0.12f, 0.12f, 1f));
        closeBtn.GetComponent<RectTransform>().anchorMin = new Vector2(0.78f, 0.02f);
        closeBtn.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.10f);
        closeBtn.GetComponent<RectTransform>().offsetMin = closeBtn.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        closeBtn.onClick.AddListener(CloseHostPanel);

        go.SetActive(false);
    }

    private void OpenHostPanel()
    {
        if (hostPanelOpen) return;
        hostPanelOpen = true;
        if (hostCanvas != null) hostCanvas.gameObject.SetActive(true);
        SetPlayerMovement(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void CloseHostPanel()
    {
        hostPanelOpen = false;
        if (hostCanvas != null) hostCanvas.gameObject.SetActive(false);
        if (!quizRunning) { SetPlayerMovement(true); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    private void OnPrevCourse()
    {
        if (availableCourses == null || availableCourses.Length == 0) return;
        selectedCourseIndex = (selectedCourseIndex - 1 + availableCourses.Length) % availableCourses.Length;
        RefreshCourseSelection();
    }

    private void OnNextCourse()
    {
        if (availableCourses == null || availableCourses.Length == 0) return;
        selectedCourseIndex = (selectedCourseIndex + 1) % availableCourses.Length;
        RefreshCourseSelection();
    }

    private void RefreshCourseSelection()
    {
        if (availableCourses == null || availableCourses.Length == 0) return;
        var c = availableCourses[selectedCourseIndex];
        if (selectedCourseLabel != null)
            selectedCourseLabel.text = "Selected: " + c.title + "  (" + (selectedCourseIndex + 1) + " / " + availableCourses.Length + ")";
    }

    private async void OnFetchCommunityClicked()
    {
        if (fetchInProgress) return;
        fetchInProgress = true;
        SetHostStatus("Fetching quizzes from community server…");
        if (startQuizButton != null) startQuizButton.interactable = false;

        try
        {
            if (GameClient.Instance == null)            { SetHostStatus("Game client not available."); fetchInProgress = false; return; }
            if (!GameClient.Instance.IsConnected)       await GameClient.Instance.Connect();
            if (!GameClient.Instance.IsConnected)       { SetHostStatus("Could not connect to server."); fetchInProgress = false; return; }
            SubscribeCommunityPackets();
            await GameClient.Instance.SendPacket(new FetchPublishedCoursesPacket());
        }
        catch (Exception ex) { fetchInProgress = false; SetHostStatus("Fetch failed: " + ex.Message); }
    }

    private async void OnStartQuizClicked()
    {
        if (fetchedCourse != null && fetchedCourse.questions != null && fetchedCourse.questions.Length > 0)
        {
            // Already loaded — start immediately
            CloseHostPanel();
            StartQuiz(fetchedCourse.questions);
            return;
        }

        if (availableCourses == null || availableCourses.Length == 0) { SetHostStatus("Fetch quizzes first."); return; }

        // Load the selected course detail
        fetchInProgress = true;
        SetHostStatus("Loading course details…");
        if (startQuizButton != null) startQuizButton.interactable = false;
        try
        {
            SubscribeCommunityPackets();
            await GameClient.Instance.SendPacket(new FetchCourseDetailPacket(availableCourses[selectedCourseIndex].id));
        }
        catch (Exception ex) { fetchInProgress = false; SetHostStatus("Load failed: " + ex.Message); }
    }

    private void SetHostStatus(string msg)
    {
        if (hostStatusText != null) hostStatusText.text = msg;
    }

    // ── Quiz Flow (host) ─────────────────────────────────────────────────────

    private void StartQuiz(QuizQuestion[] qs)
    {
        questions            = qs;
        totalQuestions       = qs.Length;
        currentQuestionIndex = 0;
        quizRunning          = true;
        scores.Clear();
        SendNextQuestion();
    }

    private void SendNextQuestion()
    {
        if (currentQuestionIndex >= totalQuestions) { EndQuiz(); return; }

        pendingAnswers.Clear();
        var q = questions[currentQuestionIndex];

        SetTheaterQuestion(q.prompt, q.options, "Question " + (currentQuestionIndex + 1) + " of " + totalQuestions);
        ShowAnswerWindow(q.prompt, q.options);
        timerRemaining = QuizTimerSeconds;
        timerActive    = true;

        // Broadcast to clients
        MultiplayerSessionManager.Instance?.BroadcastQuizPacket(
            new QuizStartPacket(q.prompt, string.Join("|", q.options), q.correctIndex,
                                currentQuestionIndex, totalQuestions, QuizTimerSeconds));
    }

    private void BroadcastResults()
    {
        HideAnswerWindow();
        if (currentQuestionIndex >= totalQuestions) { EndQuiz(); return; }

        var q = questions[currentQuestionIndex];
        int correct = q.correctIndex;

        foreach (var kv in pendingAnswers)
        {
            if (kv.Value == correct)
            {
                if (!scores.ContainsKey(kv.Key)) scores[kv.Key] = 0;
                scores[kv.Key] += 1000;
            }
        }

        SetTheaterResult(correct);

        var sb = new System.Text.StringBuilder();
        foreach (var kv in scores) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append(':').Append(kv.Value); }

        MultiplayerSessionManager.Instance?.BroadcastQuizPacket(new QuizResultPacket(correct, sb.ToString()));

        currentQuestionIndex++;
        StartCoroutine(ShowScoresThenNext());
    }

    private IEnumerator ShowScoresThenNext()
    {
        yield return new WaitForSeconds(2f);
        ShowScoresOnTheater(scores);
        yield return new WaitForSeconds(3f);
        SendNextQuestion();
    }

    private void EndQuiz()
    {
        quizRunning = false;
        timerActive = false;
        HideAnswerWindow();

        if (theaterSubText != null)      theaterSubText.text      = "Quiz Over! Final Scores:";
        if (theaterQuestionText != null) theaterQuestionText.text = "";
        ShowScoresOnTheater(scores);
    }

    // ── Packet wiring ─────────────────────────────────────────────────────────

    private void SubscribeQuizPackets()
    {
        if (MultiplayerSessionManager.Instance != null)
            MultiplayerSessionManager.Instance.OnQuizPacket += OnQuizPacket;
    }

    private void UnsubscribeQuizPackets()
    {
        if (MultiplayerSessionManager.Instance != null)
            MultiplayerSessionManager.Instance.OnQuizPacket -= OnQuizPacket;
    }

    private void SubscribeCommunityPackets()
    {
        if (packetSubscribed || GameClient.Instance == null) return;
        GameClient.Instance.OnPacketReceived += OnCommunityPacket;
        packetSubscribed = true;
    }

    private void UnsubscribeCommunityPackets()
    {
        if (!packetSubscribed || GameClient.Instance == null) return;
        GameClient.Instance.OnPacketReceived -= OnCommunityPacket;
        packetSubscribed = false;
    }

    private void OnQuizPacket(Packet packet)
    {
        switch (packet)
        {
            case QuizStartPacket p:
                // Clients receive this; host already handled it locally in SendNextQuestion
                if (!isHost) HandleQuizStart(p);
                break;

            case QuizAnswerPacket p:
                // Only host accumulates answers
                if (isHost) pendingAnswers[p.ClientId] = p.AnswerIndex;
                break;

            case QuizResultPacket p:
                // Clients receive result; host already handled it locally
                if (!isHost) HandleQuizResult(p);
                break;
        }
    }

    private void HandleQuizStart(QuizStartPacket p)
    {
        quizRunning  = true;
        string[] opts = p.OptionsStr.Split('|');
        SetTheaterQuestion(p.Prompt, opts, "Question " + (p.QuestionIndex + 1) + " of " + p.Total);
        ShowAnswerWindow(p.Prompt, opts);
        timerRemaining = p.TimerSeconds;
        timerActive    = true;
        answerLocked   = false;
    }

    private void HandleQuizResult(QuizResultPacket p)
    {
        timerActive = false;
        HideAnswerWindow();
        SetTheaterResult(p.CorrectIndex);

        // Parse scores
        var map = new Dictionary<string, int>();
        if (!string.IsNullOrEmpty(p.ScoresJson))
            foreach (var entry in p.ScoresJson.Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int s))
                    map[parts[0]] = s;
            }

        StartCoroutine(ClientShowScoreDelay(map));
    }

    private IEnumerator ClientShowScoreDelay(Dictionary<string, int> map)
    {
        yield return new WaitForSeconds(2f);
        ShowScoresOnTheater(map);
    }

    private void OnCommunityPacket(Packet packet)
    {
        if (packet is FetchPublishedCoursesResponsePacket courses)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                fetchInProgress = false;
                UnsubscribeCommunityPackets();
                OnCoursesReceived(courses.CoursesJson);
            });
        }
        else if (packet is FetchCourseDetailResponsePacket detail)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                fetchInProgress = false;
                UnsubscribeCommunityPackets();
                OnCourseDetailReceived(detail.CourseJson);
            });
        }
    }

    private void OnCoursesReceived(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<CourseListWrapper>("{\"items\":" + json + "}");
            if (wrapper?.items == null || wrapper.items.Length == 0)
            {
                SetHostStatus("No community quizzes found."); return;
            }

            availableCourses    = wrapper.items;
            selectedCourseIndex = 0;

            // Build course list display
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < wrapper.items.Length; i++)
                sb.AppendLine((i + 1) + ".  " + wrapper.items[i].title);
            if (courseListText != null) courseListText.text = sb.ToString();

            if (prevCourseButton != null) prevCourseButton.gameObject.SetActive(wrapper.items.Length > 1);
            if (nextCourseButton != null) nextCourseButton.gameObject.SetActive(wrapper.items.Length > 1);
            if (startQuizButton  != null) startQuizButton.interactable = true;

            RefreshCourseSelection();
            SetHostStatus("Pick a quiz and press Start Quiz.");
        }
        catch (Exception ex) { SetHostStatus("Parse error: " + ex.Message); }
    }

    private void OnCourseDetailReceived(string json)
    {
        try
        {
            var detail = JsonUtility.FromJson<CourseDetailWrapper>(json);
            if (detail?.questions == null || detail.questions.Length == 0)
            {
                SetHostStatus("Course has no questions."); return;
            }

            var list = new List<QuizQuestion>();
            foreach (var q in detail.questions)
                list.Add(new QuizQuestion { prompt = q.prompt, options = q.options, correctIndex = q.correctIndex });

            fetchedCourse = new CommunityQuizData { title = detail.title, questions = list.ToArray() };
            CloseHostPanel();
            StartQuiz(fetchedCourse.questions);
        }
        catch (Exception ex) { SetHostStatus("Parse error: " + ex.Message); if (startQuizButton != null) startQuizButton.interactable = true; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetPlayerMovement(bool enabled)
    {
        var fps  = FindObjectOfType<FirstPersonControllerSimple>();
        if (fps  != null) fps.enabled  = enabled;
        var bean = FindObjectOfType<BeanController>();
        if (bean != null) bean.enabled = enabled;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }

    // ── UI factory ────────────────────────────────────────────────────────────

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(Transform parent, string content, int size, Color color, FontStyle style)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t  = go.AddComponent<Text>();
        t.font             = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text             = content;
        t.fontSize         = size;
        t.color            = color;
        t.fontStyle        = style;
        t.supportRichText  = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        return t;
    }

    private static Button MakeButton(Transform parent, string label, Color bg)
    {
        var go  = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = new Color(Mathf.Min(1f, bg.r + 0.1f), Mathf.Min(1f, bg.g + 0.1f), Mathf.Min(1f, bg.b + 0.1f), bg.a);
        cb.pressedColor     = new Color(Mathf.Max(0f, bg.r - 0.1f), Mathf.Max(0f, bg.g - 0.1f), Mathf.Max(0f, bg.b - 0.1f), bg.a);
        btn.colors = cb;

        var txt = MakeText(go.transform, label, 24, Color.white, FontStyle.Bold);
        StretchFull(txt.rectTransform);
        txt.alignment = TextAnchor.MiddleCenter;
        return btn;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Data types ────────────────────────────────────────────────────────────

    [Serializable] private class QuizQuestion { public string prompt; public string[] options; public int correctIndex; }
    private class CommunityQuizData { public string title; public QuizQuestion[] questions; }

    [Serializable] private class CourseListWrapper   { public CourseItemDto[] items; }
    [Serializable] private class CourseItemDto       { public long id; public string title; }
    [Serializable] private class CourseDetailWrapper { public string title; public CqDto[] questions; }
    [Serializable] private class CqDto              { public string prompt; public string[] options; public int correctIndex; }
}

// ── Bootstrap ─────────────────────────────────────────────────────────────────

public static class MultiplayerQuizBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        var anchor = GameObject.Find("QuizIslandAnchor");
        if (anchor == null) return;
        if (anchor.GetComponent<MultiplayerQuizManager>() == null)
            anchor.AddComponent<MultiplayerQuizManager>();
    }
}
