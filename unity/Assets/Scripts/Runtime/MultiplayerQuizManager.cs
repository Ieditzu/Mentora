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
    private static readonly string[] AnswerLabels = { "1", "2", "3", "4" };

    // ── State ────────────────────────────────────────────────────────────────

    private static MultiplayerQuizManager instance;

    // Theater screen
    private Canvas  theaterCanvas;
    private Text    theaterQuestionText;
    private Text    theaterSubText;
    private Text[]  theaterAnswerTexts = new Text[4];
    private Image[] theaterAnswerBgs   = new Image[4];

    private bool quizInteractionActive;
    private bool answerLocked;
    private int  lockedAnswerIndex = -1;

    // Host panel — UI references injected by PauseMenuManager
    private Text   hostStatusText;
    private Text   courseListText;
    private Button startQuizButton;
    private Button prevCourseButton;
    private Button nextCourseButton;
    private Text   selectedCourseLabel;

    // Called by PauseMenuManager after it builds the quiz options panel
    public static void InjectQuizUI(Text status, Text courseList, Text selectedLabel,
                                     Button fetch, Button start, Button prev, Button next)
    {
        if (instance == null) return;
        instance.hostStatusText      = status;
        instance.courseListText      = courseList;
        instance.selectedCourseLabel = selectedLabel;
        instance.startQuizButton     = start;
        instance.prevCourseButton    = prev;
        instance.nextCourseButton    = next;
        if (fetch != null) fetch.onClick.AddListener(instance.OnFetchCommunityClicked);
        if (start != null) start.onClick.AddListener(instance.OnStartQuizClicked);
        if (prev  != null) prev.onClick.AddListener(instance.OnPrevCourse);
        if (next  != null) next.onClick.AddListener(instance.OnNextCourse);
    }

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
    private Coroutine pendingEarlyFinishRoutine;
    private readonly Dictionary<string, int> scores         = new Dictionary<string, int>();
    private readonly Dictionary<string, int> pendingAnswers = new Dictionary<string, int>();
    private readonly HashSet<string> expectedAnswerClientIds = new HashSet<string>();

    // ── Static entry point called from PauseMenuManager ──────────────────────

    /// <summary>
    /// Opens the host fetch panel so the host can load a community quiz and start it.
    /// Called from the Quiz Options panel in the pause menu.
    /// </summary>
    public static void HostStartQuiz()
    {
        if (instance == null) return;
        instance.isHost = true;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void Start()
    {
        BuildTheaterScreen();
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

        if (theaterSubText != null && quizInteractionActive)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, timerRemaining));
            theaterSubText.text  = "Question " + (currentQuestionIndex + 1) + " of " + totalQuestions + "  |  " + secs + "s  |  Press 1-4 to answer";
            theaterSubText.color = timerRemaining < 5f ? new Color(1f, 0.4f, 0.4f) : new Color(0.7f, 0.7f, 1f);
        }

        if (timerRemaining <= 0f && isHost)
        {
            timerActive = false;
            CancelPendingEarlyFinish();
            BroadcastResults();
        }

        if (quizInteractionActive && !answerLocked)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) OnAnswerChosen(0);
                else if (kb.digit2Key.wasPressedThisFrame) OnAnswerChosen(1);
                else if (kb.digit3Key.wasPressedThisFrame) OnAnswerChosen(2);
                else if (kb.digit4Key.wasPressedThisFrame) OnAnswerChosen(3);
            }
        }
    }


    // ── Trigger ──────────────────────────────────────────────────────────────

    private void EnsureTrigger()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center    = new Vector3(0f, 1f, 0f);
        col.size      = new Vector3(6f, 3f, 6f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Trigger zone kept for future use (e.g. visual effects when entering the quiz area).
        // Host panel is now opened exclusively from the pause menu Quiz Options button.
    }

    private void OnTriggerExit(Collider other)
    {
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
        MultiplayerSessionManager sessionManager = MultiplayerSessionManager.Instance;

        for (int i = 0; i < 4; i++)
        {
            if (theaterAnswerBgs[i] != null)  theaterAnswerBgs[i].color  = new Color(0.10f, 0.14f, 0.22f, 1f);
            if (theaterAnswerTexts[i] != null)
            {
                if (i < sorted.Count)
                {
                    string playerName = sessionManager != null ? sessionManager.ResolvePlayerName(sorted[i].Key) : "Player";
                    theaterAnswerTexts[i].text = (i + 1) + ".  " + playerName + "   " + sorted[i].Value + " pts";
                }
                else theaterAnswerTexts[i].text = "";
            }
        }
    }


    private void ShowInteraction(string[] options)
    {
        answerLocked          = false;
        lockedAnswerIndex     = -1;
        quizInteractionActive = true;

        for (int i = 0; i < 4; i++)
        {
            bool has = options != null && i < options.Length;
            if (theaterAnswerBgs[i] != null)
                theaterAnswerBgs[i].color = has ? AnswerColors[i] * 0.7f : new Color(0.1f, 0.1f, 0.18f, 1f);
        }
    }

    private void HideInteraction()
    {
        quizInteractionActive = false;
    }

    private void OnAnswerChosen(int idx)
    {
        if (answerLocked) return;
        answerLocked      = true;
        lockedAnswerIndex = idx;

        for (int i = 0; i < 4; i++)
        {
            if (theaterAnswerBgs[i] != null)
                theaterAnswerBgs[i].color = (i == idx) ? AnswerColors[i] : AnswerColors[i] * 0.3f;
        }

        string myId = GetLocalQuizParticipantId();
        if (isHost)
        {
            pendingAnswers[myId] = idx;
            TryFinishQuestionEarly();
        }

        if (!string.IsNullOrEmpty(MultiplayerSessionManager.Instance?.LocalClientId))
            MultiplayerSessionManager.Instance?.SendQuizPacketToHost(new QuizAnswerPacket(myId, idx));
    }

    // ── Quiz course controls ──────────────────────────────────────────────────

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
        StartCoroutine(CountdownThenQuestion());
    }

    private IEnumerator CountdownThenQuestion()
    {
        // Show player count for 2 seconds
        int playerCount = MultiplayerSessionManager.Instance?.ConnectedPlayerCount ?? 1;
        if (theaterQuestionText != null)
        {
            theaterQuestionText.fontSize = 48;
            theaterQuestionText.text     = playerCount + " player" + (playerCount != 1 ? "s" : "") + " in the session";
        }
        if (theaterSubText != null)
        {
            theaterSubText.text  = "Quiz is starting…";
            theaterSubText.color = new Color(0.7f, 0.7f, 1f);
        }
        for (int j = 0; j < 4; j++)
            if (theaterAnswerBgs[j] != null) theaterAnswerBgs[j].color = new Color(0.1f, 0.1f, 0.18f, 1f);
        yield return new WaitForSeconds(2f);

        // Countdown 5 → 1
        for (int i = 5; i >= 1; i--)
        {
            if (theaterSubText != null) theaterSubText.text = "Get ready!";
            if (theaterQuestionText != null)
            {
                theaterQuestionText.fontSize = 120;
                theaterQuestionText.text     = i.ToString();
                theaterQuestionText.color    = Color.white;
            }
            yield return new WaitForSeconds(1f);
        }

        if (theaterQuestionText != null) theaterQuestionText.fontSize = 34;
        SendNextQuestion();
    }

    private void SendNextQuestion()
    {
        if (currentQuestionIndex >= totalQuestions) { EndQuiz(); return; }

        CancelPendingEarlyFinish();
        pendingAnswers.Clear();
        expectedAnswerClientIds.Clear();
        var q = questions[currentQuestionIndex];
        CaptureExpectedAnswerParticipants();

        SetTheaterQuestion(q.prompt, q.options, "Question " + (currentQuestionIndex + 1) + " of " + totalQuestions + "  |  " + QuizTimerSeconds + "s");
        ShowInteraction(q.options);
        timerRemaining = QuizTimerSeconds;
        timerActive    = true;

        // Broadcast to clients
        MultiplayerSessionManager.Instance?.BroadcastQuizPacket(
            new QuizStartPacket(q.prompt, string.Join("|", q.options), q.correctIndex,
                                currentQuestionIndex, totalQuestions, QuizTimerSeconds));
    }

    private void BroadcastResults()
    {
        HideInteraction();
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
        CancelPendingEarlyFinish();
        HideInteraction();

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
                if (isHost)
                {
                    pendingAnswers[p.ClientId] = p.AnswerIndex;
                    TryFinishQuestionEarly();
                }
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
        SetTheaterQuestion(p.Prompt, opts, "Question " + (p.QuestionIndex + 1) + " of " + p.Total + "  |  " + p.TimerSeconds + "s");
        ShowInteraction(opts);
        timerRemaining = p.TimerSeconds;
        timerActive    = true;
        answerLocked   = false;
    }

    private void HandleQuizResult(QuizResultPacket p)
    {
        timerActive = false;
        HideInteraction();
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

    private void CaptureExpectedAnswerParticipants()
    {
        MultiplayerSessionManager sessionManager = MultiplayerSessionManager.Instance;
        if (sessionManager == null)
        {
            expectedAnswerClientIds.Add(GetLocalQuizParticipantId());
            return;
        }

        List<string> ids = sessionManager.GetConnectedPlayerIds(GetLocalQuizParticipantId());
        for (int i = 0; i < ids.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
            {
                expectedAnswerClientIds.Add(ids[i]);
            }
        }

        if (expectedAnswerClientIds.Count == 0)
        {
            expectedAnswerClientIds.Add(GetLocalQuizParticipantId());
        }
    }

    private string GetLocalQuizParticipantId()
    {
        string localId = MultiplayerSessionManager.Instance != null ? MultiplayerSessionManager.Instance.LocalClientId : string.Empty;
        return string.IsNullOrWhiteSpace(localId) ? "__host_local__" : localId;
    }

    private void CancelPendingEarlyFinish()
    {
        if (pendingEarlyFinishRoutine == null)
        {
            return;
        }

        StopCoroutine(pendingEarlyFinishRoutine);
        pendingEarlyFinishRoutine = null;
    }

    private IEnumerator DelayedBroadcastResults(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        pendingEarlyFinishRoutine = null;

        if (!quizRunning)
        {
            yield break;
        }

        BroadcastResults();
    }

    private void TryFinishQuestionEarly()
    {
        if (!isHost || !timerActive)
        {
            return;
        }

        if (expectedAnswerClientIds.Count == 0)
        {
            CaptureExpectedAnswerParticipants();
        }

        foreach (string clientId in expectedAnswerClientIds)
        {
            if (!pendingAnswers.ContainsKey(clientId))
            {
                return;
            }
        }

        if (pendingAnswers.Count == 0)
        {
            return;
        }

        timerActive = false;
        CancelPendingEarlyFinish();
        pendingEarlyFinishRoutine = StartCoroutine(DelayedBroadcastResults(2f));
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
