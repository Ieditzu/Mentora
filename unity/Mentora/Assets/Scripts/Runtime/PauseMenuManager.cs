using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mentora.Network;
using System.Collections.Generic;
using System.IO;

public class PauseMenuManager : MonoBehaviour
{
    private const string MouseSensitivityPrefKey = "MouseSensitivity";
    private static PauseMenuManager instance;

    public static bool IsGamePaused { get; private set; }

    private Canvas canvas;
    private GameObject mainPanel;
    private GameObject tasksPanel;
    private GameObject goalsPanel;

    private CanvasGroup menuGroup;
    private Coroutine menuAnim;
    private const float menuAnimDuration = 0.18f;

    private Slider sensitivitySlider; // legacy, keep null
    private InputField sensitivityInput;
    private Text sensitivityValueText;
    private FirstPersonControllerSimple fpsController;
    private float previousTimeScale = 1f;
    private bool initialized;

    private Text qrStatusText;
    private RawImage qrCodeImage;
    private Button qrButton;
    private long loggedInChildId = -1;
    private string loggedInChildName = "";
    private int loggedInChildPoints = 0;

    private GameObject taskListContainer;
    private List<FetchTasksResponsePacket.TaskDto> availableTasks = new List<FetchTasksResponsePacket.TaskDto>();

    private GameObject goalListContainer;
    private List<FetchGoalsResponsePacket.GoalDto> childGoals = new List<FetchGoalsResponsePacket.GoalDto>();

    private HashSet<string> completedTaskTitles = new HashSet<string>();

    private Text progressText;
    private Image progressBarFill;
    private Text streakText;
    private int serverStreak;
    private int serverCompletedTaskCount;
    private int serverTotalTaskCount;

    private string SessionFilePath => Path.Combine(Application.persistentDataPath, "session.json");

    public static void CompleteTaskByTitle(string titleSubstring)
    {
        if (instance == null || instance.loggedInChildId == -1) return;
        if (GameClient.Instance == null || !GameClient.Instance.IsConnected) return;

        foreach (var task in instance.availableTasks)
        {
            if (task.Title.IndexOf(titleSubstring, System.StringComparison.OrdinalIgnoreCase) >= 0
                && !instance.completedTaskTitles.Contains(task.Title))
            {
                instance.completedTaskTitles.Add(task.Title);
                _ = GameClient.Instance.SendPacket(new CompleteTaskPacket(instance.loggedInChildId, task.Id));
                Debug.Log("Auto-completing task: " + task.Title);
                return;
            }
        }
    }

    [System.Serializable]
    private class SessionData { public long childId; public string token; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityMainThreadDispatcher.Initialize(); 

        if (instance != null)
        {
            instance.RebuildIfNeeded();
            return;
        }

        GameObject root = new GameObject("PauseMenuManager");
        instance = root.AddComponent<PauseMenuManager>();
        root.AddComponent<GameClient>(); 
        DontDestroyOnLoad(root);
    }

    private void Start()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPacketReceived += OnPacketReceived;
            _ = ConnectAndTryAutoLogin();
        }
    }

    private async System.Threading.Tasks.Task ConnectAndTryAutoLogin()
    {
        await GameClient.Instance.Connect();
        
        if (File.Exists(SessionFilePath))
        {
            try {
                string json = File.ReadAllText(SessionFilePath);
                SessionData data = JsonUtility.FromJson<SessionData>(json);
                if (data != null && !string.IsNullOrEmpty(data.token))
                {
                    Debug.Log("Found saved session, attempting auto-login...");
                    await GameClient.Instance.SendPacket(new VerifySessionPacket(data.childId, data.token));
                }
            } catch (System.Exception e) {
                Debug.LogError("Failed to load session: " + e.Message);
            }
        }
    }

    private void OnPacketReceived(Packet packet)
    {
        if (packet is QRLoginResponsePacket qrResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (qrStatusText != null)
                    qrStatusText.text = "Scan the QR code below";
                
                StartCoroutine(DownloadQRCode(qrResp.Token));
            });
        }
        else if (packet is ChildAuthResponsePacket authResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (authResp.Success)
                {
                    loggedInChildId = authResp.ChildId;
                    loggedInChildName = authResp.ChildName;
                    
                    // Save session locally
                    try {
                        SessionData data = new SessionData { childId = authResp.ChildId, token = authResp.SessionToken };
                        File.WriteAllText(SessionFilePath, JsonUtility.ToJson(data));
                        Debug.Log("Session saved to " + SessionFilePath);
                    } catch (System.Exception e) {
                        Debug.LogError("Failed to save session: " + e.Message);
                    }

                    _ = GameClient.Instance.SendPacket(new FetchChildStatsPacket());
                    _ = GameClient.Instance.SendPacket(new FetchTasksPacket());
                    _ = GameClient.Instance.SendPacket(new FetchGoalsPacket(-1));

                    if (qrButton != null) qrButton.interactable = false;
                    if (qrCodeImage != null) qrCodeImage.gameObject.SetActive(false);
                }
                else
                {
                    if (qrStatusText != null)
                        qrStatusText.text = "LOGIN FAILED / EXPIRED";
                    
                    // Clear invalid session
                    if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
                }
            });
        }
        else if (packet is FetchChildStatsResponsePacket statsResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                loggedInChildPoints = statsResp.TotalPoints;
                serverStreak = statsResp.Streak;
                serverCompletedTaskCount = statsResp.CompletedTaskCount;
                serverTotalTaskCount = statsResp.TotalTaskCount;
                if (qrStatusText != null)
                    qrStatusText.text = statsResp.Name + " | " + statsResp.TotalPoints + " pts";
                UpdateProgressBar();
                UpdateStreakDisplay();
            });
        }
        else if (packet is FetchTasksResponsePacket tasksResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                availableTasks = tasksResp.Tasks;
                RebuildTaskList();
                UpdateProgressBar();
            });
        }
        else if (packet is FetchGoalsResponsePacket goalsResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                childGoals = goalsResp.Goals;
                RebuildGoalList();
            });
        }
        else if (packet is ActionResponsePacket actionResp)
        {
            if (actionResp.RequestPacketId == 8 && actionResp.Success)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    _ = GameClient.Instance.SendPacket(new FetchChildStatsPacket());
                    if (loggedInChildId != -1)
                        _ = GameClient.Instance.SendPacket(new FetchGoalsPacket(-1));
                    UpdateProgressBar();
                });
            }
        }
    }

    private System.Collections.IEnumerator DownloadQRCode(string token)
    {
        string url = "https://api.qrserver.com/v1/create-qr-code/?size=256x256&data=" + token;
        using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error downloading QR code: " + webRequest.error);
            }
            else
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(webRequest);
                if (qrCodeImage != null)
                {
                    qrCodeImage.texture = texture;
                    qrCodeImage.gameObject.SetActive(true);
                }
            }
        }
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        RebuildIfNeeded();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
            IsGamePaused = false;
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        ReacquireControllerIfNeeded();
        if (IsEscapePressed())
        {
            if (IsGamePaused) ResumeGame();
            else PauseGame();
        }
    }

    private static bool IsEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebuildIfNeeded();
        ForceHiddenIfNotPaused();
        ReacquireControllerIfNeeded();
        ApplySavedSensitivity();
    }

    private void RebuildIfNeeded()
    {
        if (!initialized)
        {
            BuildUi();
            initialized = true;
        }
        if (canvas == null) BuildUi();
        ForceHiddenIfNotPaused();
        EnsureEventSystem();
    }

    private void BuildUi()
    {
        if (canvas != null) Destroy(canvas.gameObject);

        GameObject canvasObject = new GameObject("PauseMenuCanvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject dimmer = CreateUiObject("Dimmer", canvas.transform);
        dimmer.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.82f);
        StretchToFullscreen(dimmer.GetComponent<RectTransform>());

        // MAIN PANEL
        mainPanel = CreateUiObject("MainPanel", canvas.transform);
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        mainRect.sizeDelta = new Vector2(720f, 560f);
        mainRect.anchoredPosition = Vector2.zero;
        mainPanel.AddComponent<Image>().color = new Color(0.09f, 0.12f, 0.18f, 0.96f);
        mainPanel.AddComponent<Outline>().effectColor = new Color(0.0f, 0.7f, 1f, 0.4f);

        menuGroup = mainPanel.AddComponent<CanvasGroup>();
        menuGroup.alpha = 0f;
        mainPanel.transform.localScale = Vector3.one * 0.95f;
        mainPanel.SetActive(false);

        GameObject topBar = CreateUiObject("TopBar", mainPanel.transform);
        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.sizeDelta = new Vector2(720f, 88f);
        topRect.anchoredPosition = new Vector2(0f, 216f);
        topBar.AddComponent<Image>().color = new Color(0.12f, 0.20f, 0.32f, 0.96f);
        topBar.AddComponent<Outline>().effectColor = new Color(0f, 0.9f, 1f, 0.35f);
        CreateText("PauseTitle", topBar.transform, "PAUSED", 34, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.93f, 0.97f, 1f, 1f), Vector2.zero, new Vector2(420f, 52f));

        // Body container
        GameObject body = CreateUiObject("Body", mainPanel.transform);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.sizeDelta = new Vector2(680f, 380f);
        bodyRect.anchoredPosition = new Vector2(0f, -20f);

        // Info card (status + QR)
        GameObject qrSection = CreateUiObject("QrSection", body.transform);
        RectTransform qrRect = qrSection.GetComponent<RectTransform>();
        qrRect.sizeDelta = new Vector2(340f, 300f);
        qrRect.anchoredPosition = new Vector2(-170f, 20f);
        qrSection.AddComponent<Image>().color = new Color(0.13f, 0.18f, 0.26f, 0.96f);
        qrSection.AddComponent<Outline>().effectColor = new Color(0f, 0.8f, 1f, 0.25f);

        qrStatusText = CreateText("QrStatus", qrSection.transform, 
            loggedInChildId == -1 ? "Not logged in" : loggedInChildName + " | " + loggedInChildPoints + " pts", 
            28, FontStyle.Italic, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 110), new Vector2(380, 70));

        GameObject qrImgObj = CreateUiObject("QrCodeImage", qrSection.transform);
        qrCodeImage = qrImgObj.AddComponent<RawImage>();
        RectTransform qrImgRect = qrImgObj.GetComponent<RectTransform>();
        qrImgRect.sizeDelta = new Vector2(160, 160);
        qrImgRect.anchoredPosition = new Vector2(0, 20);
        qrImgObj.SetActive(false);

        qrButton = CreateButton(qrSection.transform, "QrButton", "Generate QR Login", new Vector2(0f, -90f), new Color(0.4f, 0.2f, 0.8f, 1f));
        qrButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        qrButton.onClick.AddListener(GenerateQrLogin);
        if (loggedInChildId != -1) qrButton.interactable = false;

        Button logoutButton = CreateButton(qrSection.transform, "LogoutButton", "Log Out", new Vector2(0f, -140f), new Color(0.65f, 0.22f, 0.22f, 1f));
        logoutButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        logoutButton.onClick.AddListener(LogoutAccount);

        // Progress bar + streak row (bottom of main panel)
        GameObject bottomBar = CreateUiObject("BottomBar", mainPanel.transform);
        RectTransform bottomRect = bottomBar.GetComponent<RectTransform>();
        bottomRect.sizeDelta = new Vector2(680f, 44f);
        bottomRect.anchoredPosition = new Vector2(0f, -230f);
        bottomBar.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.22f, 0.95f);

        // Streak text (left side)
        streakText = CreateText("StreakText", bottomBar.transform, "", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(1f, 0.85f, 0.3f), new Vector2(-230f, 0f), new Vector2(180f, 34f));

        // Progress text (right side)
        progressText = CreateText("ProgressText", bottomBar.transform, "0 / 0 tasks", 13, FontStyle.Normal, TextAnchor.MiddleRight,
            new Color(0.8f, 0.9f, 1f), new Vector2(230f, 0f), new Vector2(180f, 34f));

        // Progress bar background
        GameObject barBg = CreateUiObject("BarBg", bottomBar.transform);
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.sizeDelta = new Vector2(260f, 12f);
        barBgRect.anchoredPosition = new Vector2(50f, 0f);
        barBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);

        // Progress bar fill
        GameObject barFill = CreateUiObject("BarFill", barBg.transform);
        progressBarFill = barFill.AddComponent<Image>();
        progressBarFill.color = new Color(0.2f, 0.8f, 0.5f, 1f);
        RectTransform fillRect = barFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 0f);

        UpdateProgressBar();
        UpdateStreakDisplay();

        // Actions stack
        GameObject actions = CreateUiObject("Actions", body.transform);
        RectTransform actionsRect = actions.GetComponent<RectTransform>();
        actionsRect.sizeDelta = new Vector2(260f, 300f);
        actionsRect.anchoredPosition = new Vector2(190f, 20f);
        actions.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        actions.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("ActionsTitle", actions.transform, "Quick Actions", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 115f), new Vector2(200f, 30f));

        Button resumeButton = CreateButton(actions.transform, "ResumeButton", "Resume", new Vector2(0f, 55f), new Color(0.18f, 0.63f, 0.43f, 1f));
        resumeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 44f);
        resumeButton.onClick.AddListener(ResumeGame);

        Button tasksBtn = CreateButton(actions.transform, "TasksBtn", "Dev Options", new Vector2(0f, 0f), new Color(0.45f, 0.45f, 0.5f, 1f));
        tasksBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 44f);
        tasksBtn.onClick.AddListener(() => {
            if (qrCodeImage != null) qrCodeImage.gameObject.SetActive(false);
            ShowPanel(tasksPanel);
        });

        Button goalsBtn = CreateButton(actions.transform, "GoalsBtn", "View Goals", new Vector2(0f, -55f), new Color(0.6f, 0.4f, 0.8f, 1f));
        goalsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 44f);
        goalsBtn.onClick.AddListener(() => {
            if (qrCodeImage != null) qrCodeImage.gameObject.SetActive(false);
            if (loggedInChildId != -1)
                _ = GameClient.Instance.SendPacket(new FetchGoalsPacket(-1));
            ShowPanel(goalsPanel);
        });

        Button quitButton = CreateButton(actions.transform, "QuitButton", "Quit Game", new Vector2(0f, -110f), new Color(0.72f, 0.24f, 0.26f, 1f));
        quitButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 44f);
        quitButton.onClick.AddListener(QuitGame);

        // TASKS PANEL
        tasksPanel = CreateUiObject("TasksPanel", canvas.transform);
        RectTransform tasksRect = tasksPanel.GetComponent<RectTransform>();
        tasksRect.sizeDelta = new Vector2(620f, 460f);
        tasksRect.anchoredPosition = Vector2.zero;
        tasksPanel.AddComponent<Image>().color = new Color(0.05f, 0.1f, 0.2f, 0.98f);
        tasksPanel.AddComponent<Outline>().effectColor = Color.cyan;

        CreateText("TasksTitle", tasksPanel.transform, "DEV OPTIONS", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.cyan, new Vector2(0, 190), new Vector2(360, 48));

        taskListContainer = CreateScrollableList("TaskScroll", tasksPanel.transform, new Vector2(520, 310), new Vector2(0, -10));

        Button backBtn = CreateButton(tasksPanel.transform, "BackBtn", "Back", new Vector2(0, -190), new Color(0.4f, 0.4f, 0.4f));
        backBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 38);
        backBtn.onClick.AddListener(() => {
            ShowPanel(mainPanel);
            if (qrCodeImage != null && qrCodeImage.texture != null && loggedInChildId == -1) qrCodeImage.gameObject.SetActive(true);
        });

        tasksPanel.SetActive(false);

        // GOALS PANEL
        goalsPanel = CreateUiObject("GoalsPanel", canvas.transform);
        RectTransform goalsRect = goalsPanel.GetComponent<RectTransform>();
        goalsRect.sizeDelta = new Vector2(620f, 460f);
        goalsRect.anchoredPosition = Vector2.zero;
        goalsPanel.AddComponent<Image>().color = new Color(0.06f, 0.05f, 0.15f, 0.98f);
        goalsPanel.AddComponent<Outline>().effectColor = new Color(0.7f, 0.4f, 1f, 0.8f);

        CreateText("GoalsTitle", goalsPanel.transform, "PARENT GOALS", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.7f, 0.5f, 1f), new Vector2(0, 190), new Vector2(360, 48));

        goalListContainer = CreateScrollableList("GoalScroll", goalsPanel.transform, new Vector2(540, 310), new Vector2(0, -10));

        Button goalsBackBtn = CreateButton(goalsPanel.transform, "GoalsBackBtn", "Back", new Vector2(0, -190), new Color(0.4f, 0.4f, 0.4f));
        goalsBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 38);
        goalsBackBtn.onClick.AddListener(() => {
            ShowPanel(mainPanel);
            if (qrCodeImage != null && qrCodeImage.texture != null && loggedInChildId == -1) qrCodeImage.gameObject.SetActive(true);
        });

        goalsPanel.SetActive(false);

        canvasObject.SetActive(false);
        ApplySavedSensitivity();
        RebuildTaskList();
        RebuildGoalList();
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (tasksPanel != null) tasksPanel.SetActive(false);
        if (goalsPanel != null) goalsPanel.SetActive(false);
        if (panel != null) panel.SetActive(true);
    }

    private void RebuildTaskList()
    {
        if (taskListContainer == null) return;
        foreach (Transform child in taskListContainer.transform) Destroy(child.gameObject);

        float itemHeight = 50f;
        float spacing = 4f;
        float totalHeight = availableTasks.Count * (itemHeight + spacing);
        RectTransform contentRect = taskListContainer.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        float y = -spacing;
        foreach (var task in availableTasks)
        {
            bool isCompleted = completedTaskTitles.Contains(task.Title);

            GameObject item = CreateUiObject("TaskItem_" + task.Id, taskListContainer.transform);
            RectTransform iRect = item.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 1); iRect.anchorMax = new Vector2(1, 1);
            iRect.pivot = new Vector2(0.5f, 1);
            iRect.sizeDelta = new Vector2(-20, itemHeight);
            iRect.anchoredPosition = new Vector2(0, y);
            item.AddComponent<Image>().color = isCompleted
                ? new Color(0.15f, 0.35f, 0.15f, 0.35f)
                : new Color(1, 1, 1, 0.05f);

            string label = (isCompleted ? "[DONE] " : "") + task.Title + " (" + task.Points + " pts)";
            CreateText("Label", item.transform, label, 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                isCompleted ? new Color(0.6f, 1f, 0.6f) : Color.white, new Vector2(-50, 0), new Vector2(340, 40));

            if (!isCompleted)
            {
                Button completeBtn = CreateButton(item.transform, "Btn", "Complete", new Vector2(200, 0), new Color(0.2f, 0.6f, 0.3f));
                completeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 34);
                completeBtn.GetComponentInChildren<Text>().fontSize = 13;
                long tid = task.Id;
                string tTitle = task.Title;
                completeBtn.onClick.AddListener(() => {
                    if (loggedInChildId != -1)
                    {
                        completedTaskTitles.Add(tTitle);
                        _ = GameClient.Instance.SendPacket(new CompleteTaskPacket(loggedInChildId, tid));
                    }
                });
            }
            y -= (itemHeight + spacing);
        }
    }

    private void RebuildGoalList()
    {
        if (goalListContainer == null) return;
        foreach (Transform child in goalListContainer.transform) Destroy(child.gameObject);

        RectTransform contentRect = goalListContainer.GetComponent<RectTransform>();

        if (childGoals.Count == 0)
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 60);
            CreateText("NoGoals", goalListContainer.transform, loggedInChildId == -1 ? "Log in to see goals" : "No goals set by parent yet",
                16, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.5f), new Vector2(0, -20), new Vector2(400, 40));
            return;
        }

        float itemHeight = 60f;
        float spacing = 4f;
        float totalHeight = childGoals.Count * (itemHeight + spacing);
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        float y = -spacing;
        foreach (var goal in childGoals)
        {
            GameObject item = CreateUiObject("GoalItem_" + goal.Id, goalListContainer.transform);
            RectTransform iRect = item.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 1); iRect.anchorMax = new Vector2(1, 1);
            iRect.pivot = new Vector2(0.5f, 1);
            iRect.sizeDelta = new Vector2(-20, itemHeight);
            iRect.anchoredPosition = new Vector2(0, y);
            item.AddComponent<Image>().color = goal.IsCompleted
                ? new Color(0.15f, 0.35f, 0.15f, 0.4f)
                : new Color(1f, 1f, 1f, 0.05f);

            string statusIcon = goal.IsCompleted ? "[DONE] " : "";
            string requirement = goal.RequiredPoints > 0
                ? " (need " + goal.RequiredPoints + " pts)"
                : goal.RequiredTaskId > 0 ? " (complete task)" : "";

            CreateText("GoalTitle", item.transform, statusIcon + goal.Title + requirement,
                14, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, new Vector2(-10, 10), new Vector2(480, 28));

            Color rewardColor = goal.IsCompleted ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.85f, 0.4f);
            CreateText("GoalReward", item.transform, "Reward: " + goal.Reward,
                12, FontStyle.Italic, TextAnchor.MiddleLeft, rewardColor, new Vector2(-10, -10), new Vector2(480, 24));

            y -= (itemHeight + spacing);
        }
    }

    private void UpdateProgressBar()
    {
        if (progressText == null || progressBarFill == null) return;
        int total = serverTotalTaskCount;
        int done = serverCompletedTaskCount;
        progressText.text = done + " / " + total + " tasks";

        float ratio = total > 0 ? (float)done / total : 0f;
        RectTransform fillRect = progressBarFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(ratio, 1);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Color shifts green→gold as progress increases
        progressBarFill.color = Color.Lerp(new Color(0.2f, 0.7f, 0.4f), new Color(1f, 0.85f, 0.2f), ratio);
    }

    private void UpdateStreakDisplay()
    {
        if (streakText != null)
            streakText.text = serverStreak > 0 ? serverStreak + " day streak" : "";
    }

    private void CreateSensitivitySection(Transform parent)
    {
        GameObject card = CreateUiObject("SensitivityCard", parent);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(400f, 140f);
        cardRect.anchoredPosition = new Vector2(0f, 190f);
        card.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 0.96f);

        CreateText("SensitivityLabel", card.transform, "Sensitivity", 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 36f), new Vector2(200f, 30f));
        sensitivityValueText = null;

        GameObject inputObj = CreateUiObject("SensitivityInput", card.transform);
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(120f, 36f);
        inputRect.anchoredPosition = new Vector2(0f, -12f);

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
        Outline inputOutline = inputObj.AddComponent<Outline>();
        inputOutline.effectColor = new Color(0.30f, 0.84f, 0.97f, 0.6f);
        inputOutline.effectDistance = new Vector2(1f, -1f);

        sensitivityInput = inputObj.AddComponent<InputField>();
        sensitivityInput.contentType = InputField.ContentType.DecimalNumber;
        sensitivityInput.textComponent = CreateText("InputText", inputObj.transform, "1.80", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(0f, 0f));
        sensitivityInput.placeholder = CreateText("Placeholder", inputObj.transform, "1.80", 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f,1f,1f,0.4f), Vector2.zero, new Vector2(0f,0f));
        sensitivityInput.textComponent.rectTransform.anchorMin = Vector2.zero; sensitivityInput.textComponent.rectTransform.anchorMax = Vector2.one;
        sensitivityInput.textComponent.rectTransform.offsetMin = new Vector2(8f, 6f); sensitivityInput.textComponent.rectTransform.offsetMax = new Vector2(-8f, -6f);
        ((Text)sensitivityInput.placeholder).rectTransform.anchorMin = Vector2.zero; ((Text)sensitivityInput.placeholder).rectTransform.anchorMax = Vector2.one;
        ((Text)sensitivityInput.placeholder).rectTransform.offsetMin = new Vector2(8f, 6f); ((Text)sensitivityInput.placeholder).rectTransform.offsetMax = new Vector2(-8f, -6f);

        sensitivityInput.onEndEdit.AddListener(OnSensitivityInputChanged);
    }

    private void GenerateQrLogin()
    {
        if (GameClient.Instance != null && GameClient.Instance.IsConnected)
        {
            qrStatusText.text = "Generating...";
            _ = GameClient.Instance.SendPacket(new GenerateQRLoginPacket());
        }
        else if (GameClient.Instance != null) { _ = ConnectAndTryAutoLogin(); }
    }

    private void LogoutAccount()
    {
        loggedInChildId = -1;
        loggedInChildName = "";
        loggedInChildPoints = 0;
        childGoals.Clear();
        RebuildGoalList();
        if (qrStatusText != null) qrStatusText.text = "Not logged in";
        if (qrCodeImage != null)
        {
            qrCodeImage.texture = null;
            qrCodeImage.gameObject.SetActive(false);
        }
        if (qrButton != null) qrButton.interactable = true;
        try
        {
            if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to clear session file: " + e.Message);
        }
    }

    private void PauseGame()
    {
        RebuildIfNeeded();
        ReacquireControllerIfNeeded();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsGamePaused = true;
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
            ShowPanel(mainPanel);
            if (qrCodeImage != null && qrCodeImage.texture != null && loggedInChildId == -1) qrCodeImage.gameObject.SetActive(true);
            PlayMenuAnimation(true);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumeGame()
    {
        PlayMenuAnimation(false);
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        IsGamePaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ForceHiddenIfNotPaused() { if (!IsGamePaused && canvas != null) canvas.gameObject.SetActive(false); }

    private void PlayMenuAnimation(bool show)
    {
        if (menuGroup == null || mainPanel == null)
        {
            if (canvas != null) canvas.gameObject.SetActive(show);
            return;
        }
        if (menuAnim != null) StopCoroutine(menuAnim);
        if (show)
        {
            mainPanel.SetActive(true);
            if (canvas != null) canvas.gameObject.SetActive(true);
        }
        menuAnim = StartCoroutine(AnimateMenu(show));
    }

    private System.Collections.IEnumerator AnimateMenu(bool show)
    {
        float startAlpha = menuGroup.alpha;
        float startScale = mainPanel.transform.localScale.x;
        float targetAlpha = show ? 1f : 0f;
        float targetScale = show ? 1f : 0.96f;
        float t = 0f;
        while (t < menuAnimDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / menuAnimDuration));
            menuGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, k);
            mainPanel.transform.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, k);
            yield return null;
        }
        menuGroup.alpha = targetAlpha;
        mainPanel.transform.localScale = Vector3.one * targetScale;
        if (!show && canvas != null) canvas.gameObject.SetActive(false);
        menuAnim = null;
    }

    private void SaveSettings()
    {
        float val = GetCurrentSensitivity();
        PlayerPrefs.SetFloat(MouseSensitivityPrefKey, val);
        PlayerPrefs.Save();
        if (fpsController != null) fpsController.SetMouseSensitivity(val);
        UpdateSensitivityLabel(val);
    }

    public static string GetLoggedInChildName() => instance != null ? instance.loggedInChildName : string.Empty;

    private void QuitGame()
    {
        SaveSettings();
        Time.timeScale = 1f;
        IsGamePaused = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnSensitivityChanged(float v) { SetSensitivityValue(v, syncInput: true); }

    private void OnSensitivityInputChanged(string text)
    {
        if (!float.TryParse(text, out float v))
        {
            v = PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f);
        }
        v = Mathf.Clamp(v, 0.2f, 6f);
        SetSensitivityValue(v, syncInput: false);
    }

    private void SetSensitivityValue(float v, bool syncInput)
    {
        if (syncInput && sensitivityInput != null) sensitivityInput.SetTextWithoutNotify(v.ToString("0.00"));
        UpdateSensitivityLabel(v);
        if (fpsController != null) fpsController.SetMouseSensitivity(v);
    }

    private float GetCurrentSensitivity()
    {
        if (sensitivityInput != null && float.TryParse(sensitivityInput.text, out float val))
        {
            return Mathf.Clamp(val, 0.2f, 6f);
        }
        if (sensitivitySlider != null) return Mathf.Clamp(sensitivitySlider.value, 0.2f, 6f);
        return PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f);
    }

    private void UpdateSensitivityLabel(float v) { if (sensitivityValueText != null) sensitivityValueText.text = v.ToString("0.00"); }

    private void ReacquireControllerIfNeeded() { if (fpsController == null) fpsController = PlayerCache.GetFps(); }

    private void ApplySavedSensitivity()
    {
        float v = PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(v);
        if (sensitivityInput != null) sensitivityInput.SetTextWithoutNotify(v.ToString("0.00"));
        SetSensitivityValue(v, syncInput: false);
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreateScrollableList(string name, Transform parent, Vector2 viewportSize, Vector2 position)
    {
        GameObject scrollObj = CreateUiObject(name, parent);
        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.sizeDelta = viewportSize;
        scrollRect.anchoredPosition = position;

        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.01f);

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        Mask mask = scrollObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUiObject("Content", scrollObj.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;

        return content;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void StretchToFullscreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private static Text CreateText(string name, Transform parent, string content, int size, FontStyle style, TextAnchor align, Color col, Vector2 pos, Vector2 s)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.sizeDelta = s; r.anchoredPosition = pos;
        Text t = obj.AddComponent<Text>();
        t.text = content; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.color = col;
        return t;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color col)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(420f, 58f); r.anchoredPosition = pos;
        Image img = obj.AddComponent<Image>(); img.color = col;
        Button b = obj.AddComponent<Button>(); b.targetGraphic = img;
        CreateText(name + "L", obj.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, new Vector2(360, 36));
        return b;
    }
}
