using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Mentora.Network;
using UnityEngine;
using UnityEngine.Networking;
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
    private Button generateAiQuizButton;
    private Button startQuizButton;
    private Button prevCourseButton;
    private Button nextCourseButton;
    private Text   selectedCourseLabel;

    // Called by PauseMenuManager after it builds the quiz options panel
    public static void InjectQuizUI(Text status, Text courseList, Text selectedLabel,
                                     Button fetch, Button generateAi, Button start, Button prev, Button next)
    {
        if (instance == null) return;
        instance.hostStatusText      = status;
        instance.courseListText      = courseList;
        instance.generateAiQuizButton = generateAi;
        instance.selectedCourseLabel = selectedLabel;
        instance.startQuizButton     = start;
        instance.prevCourseButton    = prev;
        instance.nextCourseButton    = next;
        if (fetch != null) fetch.onClick.AddListener(instance.OnFetchCommunityClicked);
        if (generateAi != null) generateAi.onClick.AddListener(instance.OnGenerateAiQuizClicked);
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
    private string           fetchedCourseSummary = string.Empty;

    // Quiz session
    private bool   isHost;
    private bool   quizRunning;
    private int    currentQuestionIndex;
    private int    totalQuestions;
    private QuizQuestion[] questions;
    private float  timerRemaining;
    private bool   timerActive;
    private Coroutine pendingEarlyFinishRoutine;
    private Coroutine countdownAudioRoutine;
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
        AudioManager.Play(MenSfx.QuizAnswerLock);

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

    private void OnGenerateAiQuizClicked()
    {
        if (fetchInProgress)
        {
            return;
        }

        isHost = true;
        StartCoroutine(GenerateAiQuizRoutine());
    }

    private void SetHostStatus(string msg)
    {
        if (hostStatusText != null) hostStatusText.text = msg;
    }

    private void SetQuizActionButtonsInteractable(bool enabled)
    {
        if (generateAiQuizButton != null)
        {
            generateAiQuizButton.interactable = enabled;
        }

        if (startQuizButton != null)
        {
            startQuizButton.interactable = enabled && fetchedCourse != null && fetchedCourse.questions != null && fetchedCourse.questions.Length > 0;
        }
    }

    // ── Quiz Flow (host) ─────────────────────────────────────────────────────

    private void StartQuiz(QuizQuestion[] qs)
    {
        fetchInProgress = false;
        questions            = qs;
        totalQuestions       = qs.Length;
        currentQuestionIndex = 0;
        quizRunning          = true;
        scores.Clear();
        AudioManager.Play(MenSfx.QuizStart);
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
            AudioManager.Play(MenSfx.QuizCountdownTick);
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
        AudioManager.Play(MenSfx.QuizQuestionReveal);
        StartQuizUrgencyAudio();

        // Broadcast to clients
        MultiplayerSessionManager.Instance?.BroadcastQuizPacket(
            new QuizStartPacket(q.prompt, string.Join("|", q.options), q.correctIndex,
                                currentQuestionIndex, totalQuestions, QuizTimerSeconds));
    }

    private void BroadcastResults()
    {
        StopQuizUrgencyAudio();
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
        AudioManager.Play(MenSfx.QuizResultsReveal);
        PlayLocalAnswerOutcome(correct);

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
        StopQuizUrgencyAudio();
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
        AudioManager.Play(MenSfx.QuizQuestionReveal);
        StartQuizUrgencyAudio();
    }

    private void HandleQuizResult(QuizResultPacket p)
    {
        timerActive = false;
        StopQuizUrgencyAudio();
        HideInteraction();
        SetTheaterResult(p.CorrectIndex);
        AudioManager.Play(MenSfx.QuizResultsReveal);
        PlayLocalAnswerOutcome(p.CorrectIndex);

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

    private void StartQuizUrgencyAudio()
    {
        StopQuizUrgencyAudio();
        countdownAudioRoutine = StartCoroutine(QuizUrgencyAudioLoop());
    }

    private void StopQuizUrgencyAudio()
    {
        if (countdownAudioRoutine == null)
        {
            return;
        }

        StopCoroutine(countdownAudioRoutine);
        countdownAudioRoutine = null;
    }

    private void PlayLocalAnswerOutcome(int correctIndex)
    {
        if (lockedAnswerIndex < 0)
        {
            return;
        }

        AudioManager.Play(lockedAnswerIndex == correctIndex ? MenSfx.AnswerCorrect : MenSfx.AnswerWrong);
    }

    private IEnumerator QuizUrgencyAudioLoop()
    {
        int lastPlayedSecond = int.MinValue;
        while (timerActive && quizRunning)
        {
            int secondsRemaining = Mathf.CeilToInt(Mathf.Max(0f, timerRemaining));
            if (secondsRemaining <= 5 && secondsRemaining > 0 && secondsRemaining != lastPlayedSecond)
            {
                AudioManager.Play(MenSfx.QuizCountdownTick);
                lastPlayedSecond = secondsRemaining;
            }

            yield return null;
        }

        countdownAudioRoutine = null;
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
            if (generateAiQuizButton != null) generateAiQuizButton.interactable = true;

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
            fetchedCourseSummary = fetchedCourse.title;
            StartQuiz(fetchedCourse.questions);
        }
        catch (Exception ex) { SetHostStatus("Parse error: " + ex.Message); if (startQuizButton != null) startQuizButton.interactable = true; }
    }

    private IEnumerator GenerateAiQuizRoutine()
    {
        fetchInProgress = true;
        fetchedCourse = null;
        fetchedCourseSummary = string.Empty;
        SetQuizActionButtonsInteractable(false);

        string profileContext = PauseMenuManager.BuildAiQuizProfileContext();
        long childId = PauseMenuManager.GetLoggedInChildId();
        if (childId <= 0 || string.IsNullOrWhiteSpace(profileContext))
        {
            fetchInProgress = false;
            SetHostStatus("Log into a child profile first.");
            if (generateAiQuizButton != null) generateAiQuizButton.interactable = true;
            yield break;
        }

        SetHostStatus("Generating a quiz from the player profile…");
        CommunityQuizData aiQuiz = null;
        string error = string.Empty;
        string apiKey = PlayerPrefs.GetString(RobotVoiceBridge.OpenAiApiKeyPrefKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            bool completed = false;
            yield return StartCoroutine(RequestOpenAiQuizRoutine(profileContext, apiKey,
                result =>
                {
                    aiQuiz = result;
                    completed = true;
                },
                failure =>
                {
                    error = failure;
                    completed = true;
                }));

            if (!completed && aiQuiz == null && string.IsNullOrWhiteSpace(error))
            {
                error = "AI quiz generation did not complete.";
            }
        }
        else
        {
            error = "No OpenAI key configured.";
        }

        if (aiQuiz == null)
        {
            aiQuiz = BuildFallbackAiQuiz(profileContext);
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "AI quiz generation failed, using a local profile-based fallback.";
            }
        }

        fetchedCourse = aiQuiz;
        fetchedCourseSummary = BuildFetchedCourseSummary(aiQuiz);
        fetchInProgress = false;

        if (prevCourseButton != null) prevCourseButton.gameObject.SetActive(false);
        if (nextCourseButton != null) nextCourseButton.gameObject.SetActive(false);
        if (courseListText != null) courseListText.text = fetchedCourseSummary;
        if (selectedCourseLabel != null) selectedCourseLabel.text = "Selected: " + (aiQuiz != null ? aiQuiz.title : "AI Quiz");

        if (generateAiQuizButton != null) generateAiQuizButton.interactable = true;
        if (startQuizButton != null) startQuizButton.interactable = aiQuiz != null && aiQuiz.questions != null && aiQuiz.questions.Length > 0;

        if (aiQuiz != null && aiQuiz.questions != null && aiQuiz.questions.Length > 0)
        {
            bool usedFallback = !string.IsNullOrWhiteSpace(error);
            SetHostStatus((usedFallback ? error + " " : string.Empty) + "Press Start Quiz.");
        }
        else
        {
            SetHostStatus("Could not generate a quiz.");
        }
    }

    private IEnumerator RequestOpenAiQuizRoutine(string profileContext, string apiKey, Action<CommunityQuizData> onSuccess, Action<string> onFailure)
    {
        const string url = "https://api.openai.com/v1/chat/completions";
        OpenAiChatRequest payload = new OpenAiChatRequest
        {
            model = "gpt-4o-mini",
            temperature = 0.8f,
            messages = new[]
            {
                new OpenAiChatMessage
                {
                    role = "system",
                    content = "Generate kid-friendly PROGRAMMING-ONLY multiple-choice quiz JSON only. Return strict JSON with this shape: {\"title\":\"...\",\"questions\":[{\"prompt\":\"...\",\"options\":[\"...\",\"...\",\"...\",\"...\"],\"correctIndex\":0}]}. Use exactly 5 questions. Every question must have exactly 4 options and a valid correctIndex from 0 to 3. The quiz must be only about programming fundamentals like variables, loops, conditions, output, debugging, Python, C++, and logic. Do not include general school trivia, motivation, or non-programming life questions. Personalize the quiz using the full lobby profile context and the connected player roster."
                },
                new OpenAiChatMessage
                {
                    role = "user",
                    content = "Create a personalized multiplayer programming quiz for this whole lobby profile:\n" + profileContext
                }
            }
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onFailure?.Invoke("AI quiz request failed: " + request.error);
                yield break;
            }

            string json = request.downloadHandler.text ?? string.Empty;
            OpenAiChatResponse response = JsonUtility.FromJson<OpenAiChatResponse>(json);
            string rawContent = response != null && response.choices != null && response.choices.Length > 0 && response.choices[0].message != null
                ? response.choices[0].message.content
                : string.Empty;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                onFailure?.Invoke("AI quiz response was empty.");
                yield break;
            }

            string cleanedJson = ExtractJsonObject(rawContent);
            AiQuizPayload payloadResult = JsonUtility.FromJson<AiQuizPayload>(cleanedJson);
            CommunityQuizData quiz = ConvertAiPayloadToQuiz(payloadResult);
            if (quiz == null || quiz.questions == null || quiz.questions.Length == 0)
            {
                onFailure?.Invoke("AI quiz JSON could not be parsed.");
                yield break;
            }

            onSuccess?.Invoke(quiz);
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "{}";
        }

        string trimmed = raw.Trim();
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return trimmed;
    }

    private CommunityQuizData ConvertAiPayloadToQuiz(AiQuizPayload payload)
    {
        if (payload == null || payload.questions == null || payload.questions.Length == 0)
        {
            return null;
        }

        List<QuizQuestion> list = new List<QuizQuestion>();
        for (int i = 0; i < payload.questions.Length; i++)
        {
            AiQuizQuestion q = payload.questions[i];
            if (q == null || string.IsNullOrWhiteSpace(q.prompt) || q.options == null || q.options.Length != 4)
            {
                continue;
            }

            list.Add(new QuizQuestion
            {
                prompt = q.prompt.Trim(),
                options = new[]
                {
                    q.options[0] ?? string.Empty,
                    q.options[1] ?? string.Empty,
                    q.options[2] ?? string.Empty,
                    q.options[3] ?? string.Empty
                },
                correctIndex = Mathf.Clamp(q.correctIndex, 0, 3)
            });
        }

        if (list.Count == 0)
        {
            return null;
        }

        return new CommunityQuizData
        {
            title = string.IsNullOrWhiteSpace(payload.title) ? "AI Profile Quiz" : payload.title.Trim(),
            questions = list.ToArray()
        };
    }

    private CommunityQuizData BuildFallbackAiQuiz(string profileContext)
    {
        string childName = PauseMenuManager.GetLoggedInChildName();
        if (string.IsNullOrWhiteSpace(childName))
        {
            childName = "Player";
        }

        string focusTopic = GuessProfileTopic(profileContext);
        return new CommunityQuizData
        {
            title = childName + "'s Programming Profile Quiz",
            questions = new[]
            {
                new QuizQuestion
                {
                    prompt = childName + "'s lobby has been practicing " + focusTopic + ". Which line best represents assigning the number 5 to a variable in code?",
                    options = new[] { "score = 5", "score == 5?", "print(score = 5 =)", "if 5 score" },
                    correctIndex = 0
                },
                new QuizQuestion
                {
                    prompt = "Which programming idea is used when you want to repeat a block of code several times?",
                    options = new[] { "A loop", "A comment", "A texture", "A score table" },
                    correctIndex = 0
                },
                new QuizQuestion
                {
                    prompt = "What will this print? x = 3; x = x + 2; print(x)",
                    options = new[] { "1", "3", "5", "32" },
                    correctIndex = 1
                },
                new QuizQuestion
                {
                    prompt = "Which option best describes an if statement?",
                    options = new[] { "It checks a condition before choosing a path", "It stores every image in memory", "It creates multiplayer voice chat", "It makes the code run backward" },
                    correctIndex = 0
                },
                new QuizQuestion
                {
                    prompt = "If code gives the wrong answer, what is debugging?",
                    options = new[] { "Finding and fixing the mistake", "Making the font bigger", "Deleting the whole project", "Turning the screen off" },
                    correctIndex = 0
                }
            }
        };
    }

    private string BuildFetchedCourseSummary(CommunityQuizData quiz)
    {
        if (quiz == null || quiz.questions == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(quiz.title);
        builder.AppendLine(string.Empty);
        builder.AppendLine("Questions: " + quiz.questions.Length);
        for (int i = 0; i < quiz.questions.Length; i++)
        {
            builder.AppendLine((i + 1) + ". " + quiz.questions[i].prompt);
        }

        return builder.ToString().TrimEnd();
    }

    private static string GuessProfileTopic(string profileContext)
    {
        if (string.IsNullOrWhiteSpace(profileContext))
        {
            return "coding";
        }

        string lower = profileContext.ToLowerInvariant();
        if (lower.Contains("python")) return "Python";
        if (lower.Contains("c++") || lower.Contains("cpp")) return "C++";
        if (lower.Contains("logic")) return "logic";
        if (lower.Contains("quiz")) return "quizzes";
        return "coding";
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
    [Serializable] private class AiQuizPayload { public string title; public AiQuizQuestion[] questions; }
    [Serializable] private class AiQuizQuestion { public string prompt; public string[] options; public int correctIndex; }
    [Serializable] private class OpenAiChatRequest { public string model; public float temperature; public OpenAiChatMessage[] messages; }
    [Serializable] private class OpenAiChatMessage { public string role; public string content; }
    [Serializable] private class OpenAiChatResponse { public OpenAiChatChoice[] choices; }
    [Serializable] private class OpenAiChatChoice { public OpenAiChatMessage message; }

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
