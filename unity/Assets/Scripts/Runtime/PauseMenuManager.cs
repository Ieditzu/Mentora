using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using Mentora.Network;
using System.Collections.Generic;
using System.IO;
using System.Text;


using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using Oculus.Interaction;

public class PauseMenuManager : MonoBehaviour
{
    private const string MouseSensitivityPrefKey = "MouseSensitivity";
    private const string DevUnlockCode = "dvlp";
    private const string OfficialServerUrl = "wss://neuro.serenityutils.club";
    private static PauseMenuManager instance;

    public static bool IsGamePaused { get; private set; }

    public static void VrTogglePause()
    {
        if (instance == null) return;
        if (IsGamePaused) instance.ResumeGame();
        else instance.PauseGame();
    }

    private Canvas canvas;
    private GameObject mainPanel;
    private GameObject tasksPanel;
    private GameObject goalsPanel;
    private GameObject serverPanel;
    private GameObject settingsPanel;
    private GameObject multiplayerPanel;
    private GameObject hostGamePanel;
    private GameObject quizOptionsPanel;
    private Button quizOptionsButton;
    private MultiplayerSessionManager multiplayerSession;

    private CanvasGroup menuGroup;
    private Coroutine menuAnim;
    private const float menuAnimDuration = 0.18f;

    private Slider sensitivitySlider;
    private InputField sensitivityInput;
    private Text sensitivityValueText;
    private FirstPersonControllerSimple fpsController;
    private Camera menuCamera;
    private float previousTimeScale = 1f;
    private bool initialized;

    private Text qrStatusText;
    private RawImage qrCodeImage;
    private Button qrButton;
    private Button logoutButton;
    private Button devOptionsButton;
    private Text devAuthStatusText;
    private Text serverStatusText;
    private InputField localPortInput;
    private Text multiplayerStatusText;
    private InputField multiplayerNameInput;
    private InputField multiplayerAddressInput;
    private Button playerModelButton;
    private Button languageButton;
    private Button voiceChatButton;
    private Button rudolfVoiceModeButton;
    private Button microphoneDeviceButton;
    private Button guideDebugLinesButton;
    private InputField rudolfApiKeyInput;
    private Text voiceHintText;
    private GameObject joinSubPanel;
    private GameObject friendsSubPanel;
    private GameObject friendListContainer;
    private Text friendStatusText;
    private long loggedInChildId = -1;
    private string loggedInChildName = "";
    private int loggedInChildPoints = 0;
    private string loggedInChildGameStatsJson = string.Empty;
    private string loggedInChildProgrammingProfileSummary = string.Empty;

    private GameObject taskListContainer;
    private List<FetchTasksResponsePacket.TaskDto> availableTasks = new List<FetchTasksResponsePacket.TaskDto>();
    private List<FetchChildrenResponsePacket.ChildDto> availableProfiles = new List<FetchChildrenResponsePacket.ChildDto>();

    private GameObject goalListContainer;
    private List<FetchGoalsResponsePacket.GoalDto> childGoals = new List<FetchGoalsResponsePacket.GoalDto>();

    private HashSet<string> completedTaskTitles = new HashSet<string>();

    private Text progressText;
    private Image progressBarFill;
    private Text streakText;
    private int serverStreak;
    private int serverCompletedTaskCount;
    private int serverTotalTaskCount;
    private bool devOptionsUnlocked;
    private int devUnlockProgress;
    private int devProfileCounter = 1;
    private bool vrPauseButtonWasPressed;
    private const float VrMenuDistance = 2.4f;
    private const float VrMenuHeightOffset = -0.1f;
    private static readonly Vector3 VrMenuScale = Vector3.one * 0.00135f;

    // VR Pointer fields
    private PointableCanvas pointableCanvas;
    private GameObject vrCursor;
    private RectTransform vrCursorRect;
    private Image vrCursorImage;
    private Outline vrCursorOutline;
    private Color vrCursorDefaultColor = new Color(1f, 1f, 1f, 0.8f);
    private Color vrCursorHoverColor = new Color(1f, 0.96f, 0.35f, 1f);
    private Color vrCursorSelectColor = new Color(1f, 0.55f, 0.18f, 1f);
    private GraphicRaycaster canvasRaycaster;
    private PointerEventData vrPointerEventData;
    private EventSystem vrPointerEventSystem;
    private readonly List<RaycastResult> vrRaycastResults = new List<RaycastResult>();
    private GameObject vrHoveredObject;
    private GameObject vrPressedObject;
    private bool vrSelectWasPressed;
    private const float VrPointerMaxDistance = 6f;
    private const float VrPointerSensitivity = 0.7f;
    private const float VrPointerDownwardAngle = -30f;
    private static readonly Vector2 VrCursorHoverSize = new Vector2(42f, 42f);
    private static readonly Vector2 VrCursorPressedSize = new Vector2(32f, 32f);
    private static Sprite vrCursorSprite;

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
                    PlayerPrefs.SetString("loggedInChildId", authResp.ChildId.ToString());
                    PlayerPrefs.SetString("loggedInChildName", authResp.ChildName ?? string.Empty);
                    PlayerPrefs.Save();
                    
                    // Save session locally
                    try {
                        SessionData data = new SessionData { childId = authResp.ChildId, token = authResp.SessionToken };
                        File.WriteAllText(SessionFilePath, JsonUtility.ToJson(data));
                        Debug.Log("Session saved to " + SessionFilePath);
                    } catch (System.Exception e) {
                        Debug.LogError("Failed to save session: " + e.Message);
                    }

                    _ = GameClient.Instance.SendPacket(new FetchChildStatsPacket());
                    _ = GameClient.Instance.SendPacket(new FetchProgrammingProfileSummaryPacket());
                    _ = GameClient.Instance.SendPacket(new FetchTasksPacket());
                    _ = GameClient.Instance.SendPacket(new FetchGoalsPacket(-1));

                    RefreshLoginButtons();
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
                loggedInChildGameStatsJson = statsResp.GameStatsJson ?? string.Empty;
                serverStreak = statsResp.Streak;
                serverCompletedTaskCount = statsResp.CompletedTaskCount;
                serverTotalTaskCount = statsResp.TotalTaskCount;
                PlayerPrefs.SetString("loggedInChildName", statsResp.Name ?? loggedInChildName ?? string.Empty);
                PlayerPrefs.SetString("loggedInChildPoints", statsResp.TotalPoints.ToString());
                PlayerPrefs.SetString("loggedInChildGameStatsJson", loggedInChildGameStatsJson);
                PlayerPrefs.Save();
                if (qrStatusText != null)
                    qrStatusText.text = statsResp.Name + " | " + statsResp.TotalPoints + " pts";
                UpdateProgressBar();
                UpdateStreakDisplay();
            });
        }
        else if (packet is FetchProgrammingProfileSummaryResponsePacket programmingProfileResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                loggedInChildProgrammingProfileSummary = programmingProfileResp.ProfileSummary ?? string.Empty;
                PlayerPrefs.SetString("loggedInChildProgrammingProfileSummary", loggedInChildProgrammingProfileSummary);
                PlayerPrefs.Save();

                if (!string.IsNullOrWhiteSpace(programmingProfileResp.ChildName))
                {
                    loggedInChildName = programmingProfileResp.ChildName;
                }

                loggedInChildPoints = programmingProfileResp.TotalPoints;

                MultiplayerSessionManager.Instance?.SetLocalProgrammingProfile(
                    programmingProfileResp.ChildId,
                    programmingProfileResp.ChildName,
                    programmingProfileResp.TotalPoints,
                    programmingProfileResp.Streak,
                    programmingProfileResp.CompletedTaskCount,
                    programmingProfileResp.TotalTaskCount,
                    programmingProfileResp.ProfileSummary);
            });
        }
        else if (packet is FetchTasksResponsePacket tasksResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                availableTasks = tasksResp.Tasks;
                UpdateProgressBar();
            });
        }
        else if (packet is FetchChildrenResponsePacket childrenByParentResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                MergeProfileList(childrenByParentResp.Children);
                if (devAuthStatusText != null)
                {
                    devAuthStatusText.text = "Loaded " + availableProfiles.Count + " profiles";
                }
                RebuildTaskList();
            });
        }
        else if (packet is FetchAllChildrenResponsePacket childrenResp)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                availableProfiles = new List<FetchChildrenResponsePacket.ChildDto>();
                List<FetchChildrenResponsePacket.ChildDto> normalizedProfiles = new List<FetchChildrenResponsePacket.ChildDto>();
                foreach (var child in childrenResp.Children)
                {
                    normalizedProfiles.Add(new FetchChildrenResponsePacket.ChildDto
                    {
                        Id = child.Id,
                        Name = child.Name,
                        TotalPoints = child.TotalPoints,
                        IsOnline = child.IsOnline,
                        ProfilePicture = child.ProfilePicture
                    });
                }
                MergeProfileList(normalizedProfiles);
                if (devAuthStatusText != null)
                {
                    devAuthStatusText.text = "Loaded " + availableProfiles.Count + " profiles";
                }
                RebuildTaskList();
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
            if (actionResp.RequestPacketId == 4 && actionResp.Success)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    FetchProfilesForDevOptions();
                });
            }
            else if (actionResp.RequestPacketId == 44 && actionResp.Success)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (devAuthStatusText != null)
                    {
                        devAuthStatusText.text = "Created profile. Refreshing...";
                    }
                    FetchProfilesForDevOptions();
                });
            }
            else if (actionResp.RequestPacketId == 41)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (devAuthStatusText != null)
                    {
                        devAuthStatusText.text = actionResp.Success ? "Fetch request accepted" : ("Fetch failed: " + actionResp.Message);
                    }
                });
            }
            else if (actionResp.RequestPacketId == 43)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (devAuthStatusText != null && !actionResp.Success)
                    {
                        devAuthStatusText.text = "Switch failed: " + actionResp.Message;
                    }
                });
            }

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
        
        if (pointableCanvas != null)
        {
            pointableCanvas.WhenPointerEventRaised -= OnVrPointerEvent;
        }

        if (multiplayerSession != null)
        {
            multiplayerSession.StatusChanged -= OnMultiplayerStatusChanged;
            multiplayerSession.FriendSessionsChanged -= OnFriendSessionsChanged;
            multiplayerSession = null;
        }
    }

    private bool isAutoConnecting = false;
    private float lastAutoConnectTime = 0f;
    private const float AutoConnectInterval = 5f;

    private void Update()
    {
        ReacquireControllerIfNeeded();
        UpdateMenuPresentation();
        UpdateVrPointer();
        if (IsGamePaused)
        {
            HandleDevUnlockInput();
        }
        if (WasPausePressedThisFrame())
        {
            if (IsGamePaused) ResumeGame();
            else PauseGame();
        }

        CheckAutoReconnect();
    }

    private void CheckAutoReconnect()
    {
        if (GameClient.Instance == null) return;
        
        if (!GameClient.Instance.IsConnected && !isAutoConnecting)
        {
            if (Time.time - lastAutoConnectTime > AutoConnectInterval)
            {
                if (File.Exists(SessionFilePath))
                {
                    lastAutoConnectTime = Time.time;
                    _ = AutoReconnectFlow();
                }
            }
        }
    }

    private async System.Threading.Tasks.Task AutoReconnectFlow()
    {
        isAutoConnecting = true;
        Debug.Log("[PauseMenuManager] Connection lost, attempting auto-reconnect...");
        await ConnectAndTryAutoLogin();
        isAutoConnecting = false;
    }

    private static bool IsEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private bool WasPausePressedThisFrame()
    {
        return IsEscapePressed() && !CodeWorldRuntime.ConsumesPauseInput;
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
        canvasRaycaster = canvasObject.AddComponent<GraphicRaycaster>();
        
        // Add PointableCanvas for VR ray interaction
        pointableCanvas = canvasObject.AddComponent<PointableCanvas>();
        pointableCanvas.InjectCanvas(canvas);
        
        ConfigureCanvasForCurrentMode();
        
        // Setup VR cursor visuals
        SetupVrPointer();

        GameObject dimmer = CreateUiObject("Dimmer", canvas.transform);
        dimmer.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.08f, 0.82f);
        StretchToFullscreen(dimmer.GetComponent<RectTransform>());

        // MAIN PANEL
        mainPanel = CreateUiObject("MainPanel", canvas.transform);
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        mainRect.sizeDelta = new Vector2(720f, 560f);
        mainRect.anchoredPosition = Vector2.zero;
        mainPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var mainOutline = mainPanel.AddComponent<Outline>();
        mainOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        mainOutline.effectDistance = new Vector2(2f, -2f);

        menuGroup = mainPanel.AddComponent<CanvasGroup>();
        menuGroup.alpha = 0f;
        mainPanel.transform.localScale = Vector3.one * 0.95f;
        mainPanel.SetActive(false);

        GameObject topBar = CreateUiObject("TopBar", mainPanel.transform);
        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.sizeDelta = new Vector2(720f, 88f);
        topRect.anchoredPosition = new Vector2(0f, 216f);
        topBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        topBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("PauseTitle", topBar.transform, "PAUSED", 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(420f, 52f));

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

        logoutButton = CreateButton(qrSection.transform, "LogoutButton", "Log Out", new Vector2(0f, -140f), new Color(0.65f, 0.22f, 0.22f, 1f));
        logoutButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        logoutButton.onClick.AddListener(LogoutAccount);
        RefreshLoginButtons();

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
        actionsRect.sizeDelta = new Vector2(260f, 340f);
        actionsRect.anchoredPosition = new Vector2(190f, 20f);
        actions.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        actions.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("ActionsTitle", actions.transform, "Quick Actions", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 115f), new Vector2(200f, 30f));

        Button resumeButton = CreateButton(actions.transform, "ResumeButton", "Resume", new Vector2(0f, 78f), new Color(0.18f, 0.63f, 0.43f, 1f));
        resumeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        resumeButton.onClick.AddListener(ResumeGame);

        Button goalsBtn = CreateButton(actions.transform, "GoalsBtn", "View Goals", new Vector2(0f, 34f), new Color(0.6f, 0.4f, 0.8f, 1f));
        goalsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        goalsBtn.onClick.AddListener(() => {
            if (qrCodeImage != null) qrCodeImage.gameObject.SetActive(false);
            if (loggedInChildId != -1)
                _ = GameClient.Instance.SendPacket(new FetchGoalsPacket(-1));
            ShowPanel(goalsPanel);
        });

        Button settingsButton = CreateButton(actions.transform, "SettingsBtn", "Settings", new Vector2(0f, -10f), new Color(0.26f, 0.50f, 0.58f, 1f));
        settingsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        settingsButton.GetComponentInChildren<Text>().fontSize = 14;
        settingsButton.onClick.AddListener(() => ShowPanel(settingsPanel));

        Button multiplayerButton = CreateButton(actions.transform, "MultiplayerBtn", "Multiplayer", new Vector2(0f, -54f), new Color(0.18f, 0.55f, 0.80f, 1f));
        multiplayerButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        multiplayerButton.GetComponentInChildren<Text>().color = Color.white;
        multiplayerButton.GetComponentInChildren<Text>().fontSize = 14;
        multiplayerButton.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0.15f, 0.25f, 0.7f);
        multiplayerButton.onClick.AddListener(() => ShowPanel(multiplayerPanel));

        Button quitButton = CreateButton(actions.transform, "QuitButton", "Quit Game", new Vector2(0f, -98f), new Color(0.72f, 0.24f, 0.26f, 1f));
        quitButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 38f);
        quitButton.onClick.AddListener(QuitGame);

        settingsPanel = CreateUiObject("SettingsPanel", canvas.transform);
        RectTransform settingsRect = settingsPanel.GetComponent<RectTransform>();
        settingsRect.sizeDelta = new Vector2(660f, 500f);
        settingsRect.anchoredPosition = Vector2.zero;
        settingsPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var settingsOutline = settingsPanel.AddComponent<Outline>();
        settingsOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        settingsOutline.effectDistance = new Vector2(2f, -2f);

        GameObject settingsTopBar = CreateUiObject("TopBar", settingsPanel.transform);
        RectTransform settingsTopRect = settingsTopBar.GetComponent<RectTransform>();
        settingsTopRect.sizeDelta = new Vector2(660f, 60f);
        settingsTopRect.anchoredPosition = new Vector2(0f, 205f);
        settingsTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        settingsTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("SettingsTitle", settingsTopBar.transform, "SETTINGS", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(380f, 38f));

        CreateSensitivitySection(settingsPanel.transform);
        CreateGlobalVoiceSettingsSection(settingsPanel.transform);

        Button settingsBackBtn = CreateButton(settingsPanel.transform, "SettingsBackBtn", "Back", new Vector2(0f, -223f), new Color(0.4f, 0.4f, 0.4f));
        settingsBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(148f, 32f);
        settingsBackBtn.GetComponentInChildren<Text>().fontSize = 13;
        settingsBackBtn.onClick.AddListener(() => ShowPanel(mainPanel));
        settingsPanel.SetActive(false);

        // TASKS PANEL
        tasksPanel = CreateUiObject("TasksPanel", canvas.transform);
        RectTransform tasksRect = tasksPanel.GetComponent<RectTransform>();
        tasksRect.sizeDelta = new Vector2(620f, 460f);
        tasksRect.anchoredPosition = Vector2.zero;
        tasksPanel.AddComponent<Image>().color = new Color(0.05f, 0.1f, 0.2f, 0.98f);
        tasksPanel.AddComponent<Outline>().effectColor = Color.cyan;

        CreateText("TasksTitle", tasksPanel.transform, "DEV OPTIONS", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.cyan, new Vector2(0, 190), new Vector2(360, 48));

        devAuthStatusText = CreateText("DevAuthStatus", tasksPanel.transform, "Browse and enter any child profile from the server", 13, FontStyle.Italic, TextAnchor.MiddleCenter,
            new Color(0.8f, 0.9f, 1f), new Vector2(0, 152), new Vector2(420, 24));

        Button serverBtn = CreateButton(tasksPanel.transform, "ServerBtn", "Server", new Vector2(0, 92), new Color(0.40f, 0.48f, 0.72f, 1f));
        serverBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 34);
        serverBtn.GetComponentInChildren<Text>().fontSize = 13;
        serverBtn.onClick.AddListener(() => ShowPanel(serverPanel));

        Button createProfileBtn = CreateButton(tasksPanel.transform, "CreateProfileBtn", "Create Profile", new Vector2(-130, 50), new Color(0.18f, 0.55f, 0.80f, 1f));
        createProfileBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 36);
        createProfileBtn.GetComponentInChildren<Text>().fontSize = 14;
        createProfileBtn.onClick.AddListener(CreateDevProfile);

        Button refreshProfilesBtn = CreateButton(tasksPanel.transform, "RefreshProfilesBtn", "Refresh", new Vector2(130, 50), new Color(0.28f, 0.42f, 0.62f, 1f));
        refreshProfilesBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 36);
        refreshProfilesBtn.GetComponentInChildren<Text>().fontSize = 14;
        refreshProfilesBtn.onClick.AddListener(FetchProfilesForDevOptions);

        taskListContainer = CreateScrollableList("TaskScroll", tasksPanel.transform, new Vector2(520, 230), new Vector2(0, -32));

        Button backBtn = CreateButton(tasksPanel.transform, "BackBtn", "Back", new Vector2(0, -190), new Color(0.4f, 0.4f, 0.4f));
        backBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 38);
        backBtn.onClick.AddListener(() => {
            ShowPanel(mainPanel);
            if (qrCodeImage != null && qrCodeImage.texture != null && loggedInChildId == -1) qrCodeImage.gameObject.SetActive(true);
        });

        tasksPanel.SetActive(false);

        // SERVER PANEL
        serverPanel = CreateUiObject("ServerPanel", canvas.transform);
        RectTransform serverRect = serverPanel.GetComponent<RectTransform>();
        serverRect.sizeDelta = new Vector2(620f, 460f);
        serverRect.anchoredPosition = Vector2.zero;
        serverPanel.AddComponent<Image>().color = new Color(0.05f, 0.1f, 0.2f, 0.98f);
        serverPanel.AddComponent<Outline>().effectColor = new Color(0.55f, 0.8f, 1f, 0.8f);

        CreateText("ServerTitle", serverPanel.transform, "SERVER", 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.8f, 0.92f, 1f), new Vector2(0, 190), new Vector2(360, 48));
        serverStatusText = CreateText("ServerStatus", serverPanel.transform, "", 14, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.85f, 0.92f, 1f), new Vector2(0, 145), new Vector2(500, 24));

        CreateText("PortLabel", serverPanel.transform, "Port", 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(-120, 88), new Vector2(80, 24));
        localPortInput = CreateInputField(serverPanel.transform, "LocalPortInput", "8080", new Vector2(10, 88), new Vector2(160, 34), false);
        localPortInput.SetTextWithoutNotify("8080");

        Button officialServerBtn = CreateButton(serverPanel.transform, "OfficialServerBtn", "Official", new Vector2(0, 28), new Color(0.18f, 0.55f, 0.80f, 1f));
        officialServerBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
        officialServerBtn.onClick.AddListener(() => SwitchGameServer(OfficialServerUrl));

        Button localServerBtn = CreateButton(serverPanel.transform, "LocalServerBtn", "Local", new Vector2(0, -28), new Color(0.24f, 0.62f, 0.36f, 1f));
        localServerBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);
        localServerBtn.onClick.AddListener(SwitchToLocalServer);

        Button serverBackBtn = CreateButton(serverPanel.transform, "ServerBackBtn", "Back", new Vector2(0, -190), new Color(0.4f, 0.4f, 0.4f));
        serverBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 38);
        serverBackBtn.onClick.AddListener(() => ShowPanel(tasksPanel));

        serverPanel.SetActive(false);

        // MULTIPLAYER PANEL
        multiplayerPanel = CreateUiObject("MultiplayerPanel", canvas.transform);
        RectTransform multiplayerRect = multiplayerPanel.GetComponent<RectTransform>();
        multiplayerRect.sizeDelta = new Vector2(720f, 560f);
        multiplayerRect.anchoredPosition = Vector2.zero;
        multiplayerPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var mpOutline = multiplayerPanel.AddComponent<Outline>();
        mpOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        mpOutline.effectDistance = new Vector2(2f, -2f);

        // Top bar
        GameObject mpTopBar = CreateUiObject("TopBar", multiplayerPanel.transform);
        RectTransform mpTopRect = mpTopBar.GetComponent<RectTransform>();
        mpTopRect.sizeDelta = new Vector2(720f, 88f);
        mpTopRect.anchoredPosition = new Vector2(0f, 216f);
        mpTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        mpTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("MultiplayerTitle", mpTopBar.transform, "MULTIPLAYER", 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(480f, 52f));

        // Left column: host/join/disconnect buttons + expandable join sub-panel + status
        GameObject mpLeft = CreateUiObject("MultiplayerLeft", multiplayerPanel.transform);
        RectTransform mpLeftRect = mpLeft.GetComponent<RectTransform>();
        mpLeftRect.sizeDelta = new Vector2(290f, 380f);
        mpLeftRect.anchoredPosition = new Vector2(-180f, -15f);
        mpLeft.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        mpLeft.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("SessionLabel", mpLeft.transform, "Session", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 116f), new Vector2(220f, 30f));

        Button hostGameBtn = CreateButton(mpLeft.transform, "HostGameBtn", "Host Game", new Vector2(0f, 54f), new Color(0.18f, 0.55f, 0.80f, 1f));
        hostGameBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        hostGameBtn.onClick.AddListener(() => {
            if (joinSubPanel != null) joinSubPanel.SetActive(false);
            ShowPanel(hostGamePanel);
        });

        Button joinGameBtn = CreateButton(mpLeft.transform, "JoinGameBtn", "Join Game", new Vector2(0f, 0f), new Color(0.24f, 0.62f, 0.36f, 1f));
        joinGameBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        joinGameBtn.onClick.AddListener(() => {
            if (joinSubPanel != null)
            {
                bool show = !joinSubPanel.activeSelf;
                joinSubPanel.SetActive(show);
                if (show) joinSubPanel.transform.SetAsLastSibling();
            }
        });

        Button friendsBtn = CreateButton(mpLeft.transform, "FriendsBtn", "Friends / LAN", new Vector2(0f, -54f), new Color(0.28f, 0.42f, 0.72f, 1f));
        friendsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 40f);
        friendsBtn.GetComponentInChildren<Text>().fontSize = 14;
        friendsBtn.onClick.AddListener(ShowFriendsPanel);

        // Join sub-panel — full card overlaid on the canvas, same theme as the rest of the menu
        joinSubPanel = CreateUiObject("JoinSubPanel", canvas.transform);
        RectTransform joinSubRect = joinSubPanel.GetComponent<RectTransform>();
        joinSubRect.sizeDelta = new Vector2(420f, 280f);
        joinSubRect.anchoredPosition = new Vector2(0f, 0f);
        joinSubPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var joinOutline = joinSubPanel.AddComponent<Outline>();
        joinOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        joinOutline.effectDistance = new Vector2(2f, -2f);

        // Top bar matching pause menu style
        GameObject joinTopBar = CreateUiObject("JoinTopBar", joinSubPanel.transform);
        RectTransform joinTopRect = joinTopBar.GetComponent<RectTransform>();
        joinTopRect.sizeDelta = new Vector2(420f, 56f);
        joinTopRect.anchoredPosition = new Vector2(0f, 96f);
        joinTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        joinTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("JoinTitle", joinTopBar.transform, "JOIN GAME", 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(340f, 36f));

        // Body card
        GameObject joinBody = CreateUiObject("JoinBody", joinSubPanel.transform);
        RectTransform joinBodyRect = joinBody.GetComponent<RectTransform>();
        joinBodyRect.sizeDelta = new Vector2(380f, 130f);
        joinBodyRect.anchoredPosition = new Vector2(0f, 10f);
        joinBody.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        joinBody.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("HostIPLabel", joinBody.transform, "Host IP", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(-100f, 28f), new Vector2(80f, 26f));
        multiplayerAddressInput = CreateInputField(joinBody.transform, "HostAddressInput", "192.168.x.x", new Vector2(60f, 28f), new Vector2(190f, 36f), false);
        multiplayerAddressInput.contentType = InputField.ContentType.Standard;
        multiplayerAddressInput.characterLimit = 32;

        CreateText("PortNote", joinBody.transform, "Port: 7777 (auto)", 12, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.55f, 0.72f, 0.95f), new Vector2(0f, -18f), new Vector2(300f, 22f));

        Button confirmJoinBtn = CreateButton(joinSubPanel.transform, "ConfirmJoinBtn", "Join", new Vector2(0f, -78f), new Color(0.24f, 0.62f, 0.36f, 1f));
        confirmJoinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 44f);
        confirmJoinBtn.GetComponentInChildren<Text>().fontSize = 16;
        confirmJoinBtn.onClick.AddListener(() => {
            joinSubPanel.SetActive(false);
            JoinMultiplayerGame();
        });

        Button cancelJoinBtn = CreateButton(joinSubPanel.transform, "CancelJoinBtn", "Cancel", new Vector2(0f, -128f), new Color(0.4f, 0.4f, 0.4f));
        cancelJoinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);
        cancelJoinBtn.GetComponentInChildren<Text>().fontSize = 14;
        cancelJoinBtn.onClick.AddListener(() => joinSubPanel.SetActive(false));

        joinSubPanel.SetActive(false);

        friendsSubPanel = CreateUiObject("FriendsSubPanel", canvas.transform);
        RectTransform friendsRect = friendsSubPanel.GetComponent<RectTransform>();
        friendsRect.sizeDelta = new Vector2(540f, 430f);
        friendsRect.anchoredPosition = Vector2.zero;
        friendsSubPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var friendsOutline = friendsSubPanel.AddComponent<Outline>();
        friendsOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        friendsOutline.effectDistance = new Vector2(2f, -2f);

        GameObject friendsTopBar = CreateUiObject("FriendsTopBar", friendsSubPanel.transform);
        RectTransform friendsTopRect = friendsTopBar.GetComponent<RectTransform>();
        friendsTopRect.sizeDelta = new Vector2(540f, 62f);
        friendsTopRect.anchoredPosition = new Vector2(0f, 184f);
        friendsTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        friendsTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("FriendsTitle", friendsTopBar.transform, "FRIENDS ON LAN", 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(420f, 38f));

        friendStatusText = CreateText("FriendsStatus", friendsSubPanel.transform, "Looking for hosted games...", 13, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.70f, 0.84f, 1f), new Vector2(0f, 132f), new Vector2(460f, 28f));
        friendListContainer = CreateScrollableList("FriendsScroll", friendsSubPanel.transform, new Vector2(460f, 230f), new Vector2(0f, 0f));

        Button refreshFriendsBtn = CreateButton(friendsSubPanel.transform, "RefreshFriendsBtn", "Refresh", new Vector2(-140f, -166f), new Color(0.18f, 0.55f, 0.80f, 1f));
        refreshFriendsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 38f);
        refreshFriendsBtn.GetComponentInChildren<Text>().fontSize = 14;
        refreshFriendsBtn.onClick.AddListener(() => {
            EnsureMultiplayerSession();
            if (multiplayerSession != null) multiplayerSession.RefreshFriendSessions();
            RebuildFriendSessionsList();
        });

        Button manualJoinBtn = CreateButton(friendsSubPanel.transform, "ManualJoinBtn", "Manual IP", new Vector2(0f, -166f), new Color(0.26f, 0.50f, 0.58f, 1f));
        manualJoinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 38f);
        manualJoinBtn.GetComponentInChildren<Text>().fontSize = 14;
        manualJoinBtn.onClick.AddListener(() => {
            friendsSubPanel.SetActive(false);
            joinSubPanel.SetActive(true);
            joinSubPanel.transform.SetAsLastSibling();
        });

        Button closeFriendsBtn = CreateButton(friendsSubPanel.transform, "CloseFriendsBtn", "Close", new Vector2(140f, -166f), new Color(0.4f, 0.4f, 0.4f));
        closeFriendsBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 38f);
        closeFriendsBtn.GetComponentInChildren<Text>().fontSize = 14;
        closeFriendsBtn.onClick.AddListener(() => friendsSubPanel.SetActive(false));

        friendsSubPanel.SetActive(false);

        Button mpDisconnectBtn = CreateButton(mpLeft.transform, "DisconnectBtn", "Disconnect", new Vector2(0f, -104f), new Color(0.65f, 0.22f, 0.22f, 1f));
        mpDisconnectBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 40f);
        mpDisconnectBtn.GetComponentInChildren<Text>().fontSize = 14;
        mpDisconnectBtn.onClick.AddListener(() => {
            if (joinSubPanel != null) joinSubPanel.SetActive(false);
            if (friendsSubPanel != null) friendsSubPanel.SetActive(false);
            if (quizOptionsButton != null) quizOptionsButton.gameObject.SetActive(false);
            CodeWorldRuntime.DeactivateLocal();
            PlayerPrefs.SetInt("MP_UseCustomSpawn", 0);
            PlayerPrefs.Save();
            EnsureMultiplayerSession();
            if (multiplayerSession != null) multiplayerSession.StopAllNetworking();
        });

        multiplayerStatusText = CreateText("MultiplayerStatus", mpLeft.transform, "Offline", 12, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.75f, 0.88f, 1f), new Vector2(0f, -154f), new Vector2(260f, 44f));
        multiplayerStatusText.resizeTextForBestFit = false;

        // Right column: player name and multiplayer-only options
        GameObject mpRight = CreateUiObject("MultiplayerRight", multiplayerPanel.transform);
        RectTransform mpRightRect = mpRight.GetComponent<RectTransform>();
        mpRightRect.sizeDelta = new Vector2(290f, 380f);
        mpRightRect.anchoredPosition = new Vector2(180f, -15f);
        mpRight.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        mpRight.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("NameLabel", mpRight.transform, "Your Name", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 86f), new Vector2(220f, 30f));
        multiplayerNameInput = CreateInputField(mpRight.transform, "PlayerNameInput", "Enter your name", new Vector2(0f, 38f), new Vector2(240f, 40f), false);
        multiplayerNameInput.contentType = InputField.ContentType.Standard;
        multiplayerNameInput.characterLimit = 18;

        CreateText("MovedSettingsHint", mpRight.transform, "Model, microphone, and voice mode are in Settings.", 13, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.65f, 0.78f, 0.95f), new Vector2(0f, -32f), new Vector2(240f, 58f));

        Button openSettingsFromMultiplayerBtn = CreateButton(mpRight.transform, "OpenSettingsBtn", "Open Settings", new Vector2(0f, -86f), new Color(0.26f, 0.50f, 0.58f, 1f));
        openSettingsFromMultiplayerBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 40f);
        openSettingsFromMultiplayerBtn.GetComponentInChildren<Text>().fontSize = 15;
        openSettingsFromMultiplayerBtn.onClick.AddListener(() => ShowPanel(settingsPanel));

        // Quiz Options button — only visible to host when Quiz Island is selected
        quizOptionsButton = CreateButton(mpRight.transform, "QuizOptionsBtn", "Quiz Options", new Vector2(0f, 140f), new Color(0.45f, 0.18f, 0.72f, 1f));
        quizOptionsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        quizOptionsButton.GetComponentInChildren<Text>().fontSize = 16;
        quizOptionsButton.onClick.AddListener(() => ShowPanel(quizOptionsPanel));
        quizOptionsButton.gameObject.SetActive(false);

        Button multiplayerBackBtn = CreateButton(multiplayerPanel.transform, "MultiplayerBackBtn", "Back", new Vector2(0f, -240f), new Color(0.4f, 0.4f, 0.4f));
        multiplayerBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 40f);
        multiplayerBackBtn.GetComponentInChildren<Text>().fontSize = 15;
        multiplayerBackBtn.onClick.AddListener(() => {
            if (joinSubPanel != null) joinSubPanel.SetActive(false);
            ShowPanel(mainPanel);
        });

        multiplayerPanel.SetActive(false);

        // HOST GAME PANEL
        hostGamePanel = CreateUiObject("HostGamePanel", canvas.transform);
        RectTransform hostGameRect = hostGamePanel.GetComponent<RectTransform>();
        hostGameRect.sizeDelta = new Vector2(720f, 560f);
        hostGameRect.anchoredPosition = Vector2.zero;
        hostGamePanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var hgOutline = hostGamePanel.AddComponent<Outline>();
        hgOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        hgOutline.effectDistance = new Vector2(2f, -2f);

        // Top bar
        GameObject hgTopBar = CreateUiObject("TopBar", hostGamePanel.transform);
        RectTransform hgTopRect = hgTopBar.GetComponent<RectTransform>();
        hgTopRect.sizeDelta = new Vector2(720f, 88f);
        hgTopRect.anchoredPosition = new Vector2(0f, 216f);
        hgTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        hgTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("HostGameTitle", hgTopBar.transform, "HOST GAME", 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f, 1f), Vector2.zero, new Vector2(480f, 52f));

        // Subtitle
        CreateText("HostGameSub", hostGamePanel.transform, "Choose which multiplayer mode to host:", 20, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f), new Vector2(0f, 160f), new Vector2(560f, 30f));

        // Local Island button
        Button localIslandBtn = CreateButton(hostGamePanel.transform, "LocalIslandBtn", "Local Island", new Vector2(0f, 60f), new Color(0.18f, 0.55f, 0.80f, 1f));
        localIslandBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 72f);
        localIslandBtn.GetComponentInChildren<Text>().fontSize = 22;
        localIslandBtn.onClick.AddListener(() => {
            CodeWorldRuntime.DeactivateLocal();
            HostMultiplayerGame();
            if (quizOptionsButton != null) quizOptionsButton.gameObject.SetActive(false);
            PlayerPrefs.SetInt("MP_UseCustomSpawn", 0);
            PlayerPrefs.SetString("MP_HostMode", "local");
            PlayerPrefs.Save();
            ShowPanel(multiplayerPanel);
        });

        // Description under Local Island
        CreateText("LocalIslandDesc", hostGamePanel.transform, "Host on the main island. Players spawn at the default location.", 15, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.6f, 0.75f, 0.9f), new Vector2(0f, 22f), new Vector2(500f, 26f));

        // Quiz Island button
        Button quizIslandBtn = CreateButton(hostGamePanel.transform, "QuizIslandBtn", "Quiz Island", new Vector2(0f, -60f), new Color(0.45f, 0.18f, 0.72f, 1f));
        quizIslandBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 72f);
        quizIslandBtn.GetComponentInChildren<Text>().fontSize = 22;
        quizIslandBtn.onClick.AddListener(() => {
            CodeWorldRuntime.DeactivateLocal();
            HostMultiplayerGame();
            TeleportPlayerToQuizIsland();
            if (quizOptionsButton != null) quizOptionsButton.gameObject.SetActive(true);
            PlayerPrefs.SetString("MP_HostMode", "quiz");
            PlayerPrefs.Save();
            ShowPanel(multiplayerPanel);
        });

        // Description under Quiz Island
        CreateText("QuizIslandDesc", hostGamePanel.transform, "Host on the Quiz Island. All players will spawn there.", 15, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.75f, 0.6f, 1f), new Vector2(0f, -98f), new Vector2(500f, 26f));

        Button codeWorldBtn = CreateButton(hostGamePanel.transform, "CodeWorldBtn", "Code Quest Island", new Vector2(0f, -180f), new Color(0.96f, 0.48f, 0.18f, 1f));
        codeWorldBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 72f);
        codeWorldBtn.GetComponentInChildren<Text>().fontSize = 22;
        codeWorldBtn.onClick.AddListener(() => {
            CodeWorldRuntime.DeactivateLocal();
            HostMultiplayerGame();
            TeleportPlayerToCodeQuestIsland();
            if (quizOptionsButton != null) quizOptionsButton.gameObject.SetActive(false);
            PlayerPrefs.SetString("MP_HostMode", "codequest");
            PlayerPrefs.Save();
            ShowPanel(multiplayerPanel);
        });

        CreateText("CodeWorldDesc", hostGamePanel.transform, "Host the Code Quest hub. Pick Easy, Medium, Hard, AI Profile, or Free Sandbox portals there.", 15, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.98f, 0.8f, 0.62f), new Vector2(0f, -228f), new Vector2(560f, 44f));

        // Back button
        Button hgBackBtn = CreateButton(hostGamePanel.transform, "HGBackBtn", "Back", new Vector2(0f, -250f), new Color(0.4f, 0.4f, 0.4f));
        hgBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 40f);
        hgBackBtn.GetComponentInChildren<Text>().fontSize = 15;
        hgBackBtn.onClick.AddListener(() => ShowPanel(multiplayerPanel));

        hostGamePanel.SetActive(false);

        // QUIZ OPTIONS PANEL — same size/theme as every other panel
        quizOptionsPanel = CreateUiObject("QuizOptionsPanel", canvas.transform);
        RectTransform qopRect = quizOptionsPanel.GetComponent<RectTransform>();
        qopRect.sizeDelta = new Vector2(720f, 560f);
        qopRect.anchoredPosition = Vector2.zero;
        quizOptionsPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        var qopOutline = quizOptionsPanel.AddComponent<Outline>();
        qopOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.7f);
        qopOutline.effectDistance = new Vector2(2f, -2f);

        // Top bar — identical to every other panel
        GameObject qopTopBar = CreateUiObject("TopBar", quizOptionsPanel.transform);
        RectTransform qopTopRect = qopTopBar.GetComponent<RectTransform>();
        qopTopRect.sizeDelta = new Vector2(720f, 88f);
        qopTopRect.anchoredPosition = new Vector2(0f, 216f);
        qopTopBar.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.28f, 1f);
        qopTopBar.AddComponent<Outline>().effectColor = new Color(0.3f, 0.65f, 1f, 0.8f);
        CreateText("QOPTitle", qopTopBar.transform, "QUIZ OPTIONS", 36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f), Vector2.zero, new Vector2(480f, 52f));

        // Left column: fetch + start controls
        GameObject qopLeft = CreateUiObject("QOPLeft", quizOptionsPanel.transform);
        RectTransform qopLeftRect = qopLeft.GetComponent<RectTransform>();
        qopLeftRect.sizeDelta = new Vector2(290f, 380f);
        qopLeftRect.anchoredPosition = new Vector2(-180f, -15f);
        qopLeft.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        qopLeft.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("QOPActionsLabel", qopLeft.transform, "Quiz Controls", 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0f, 116f), new Vector2(220f, 30f));

        Button qopFetchBtn = CreateButton(qopLeft.transform, "FetchBtn", "↻  Fetch Quizzes", new Vector2(0f, 66f), new Color(0.18f, 0.45f, 0.72f, 1f));
        qopFetchBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        qopFetchBtn.GetComponentInChildren<Text>().fontSize = 14;

        Button qopAiBtn = CreateButton(qopLeft.transform, "AiQuizBtn", "✦  AI Profile Quiz", new Vector2(0f, 12f), new Color(0.78f, 0.45f, 0.16f, 1f));
        qopAiBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        qopAiBtn.GetComponentInChildren<Text>().fontSize = 14;

        Button qopStartBtn = CreateButton(qopLeft.transform, "StartBtn", "▶  Start Quiz", new Vector2(0f, -42f), new Color(0.18f, 0.63f, 0.30f, 1f));
        qopStartBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        qopStartBtn.GetComponentInChildren<Text>().fontSize = 14;
        qopStartBtn.interactable = false;

        Button qopPrevBtn = CreateButton(qopLeft.transform, "PrevBtn", "◀", new Vector2(-70f, -96f), new Color(0.25f, 0.30f, 0.42f, 1f));
        qopPrevBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 36f);
        qopPrevBtn.gameObject.SetActive(false);

        Button qopNextBtn = CreateButton(qopLeft.transform, "NextBtn", "▶", new Vector2(70f, -96f), new Color(0.25f, 0.30f, 0.42f, 1f));
        qopNextBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 36f);
        qopNextBtn.gameObject.SetActive(false);

        // Right column: status + course list
        GameObject qopRight = CreateUiObject("QOPRight", quizOptionsPanel.transform);
        RectTransform qopRightRect = qopRight.GetComponent<RectTransform>();
        qopRightRect.sizeDelta = new Vector2(290f, 380f);
        qopRightRect.anchoredPosition = new Vector2(180f, -15f);
        qopRight.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        qopRight.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        Text qopStatusTxt = CreateText("QOPStatus", qopRight.transform, "Press Fetch to load quizzes.", 13, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.75f, 0.88f, 1f), new Vector2(0f, 80f), new Vector2(250f, 60f));
        qopStatusTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text qopCourseTxt = CreateText("QOPCourseList", qopRight.transform, "", 12, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.90f, 1f), new Vector2(0f, 0f), new Vector2(250f, 160f));
        qopCourseTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        qopCourseTxt.verticalOverflow   = VerticalWrapMode.Overflow;

        Text qopSelectedTxt = CreateText("QOPSelected", qopRight.transform, "", 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.55f), new Vector2(0f, -120f), new Vector2(260f, 34f));

        // Back button
        Button qopBackBtn = CreateButton(quizOptionsPanel.transform, "QOPBackBtn", "Back", new Vector2(0f, -240f), new Color(0.4f, 0.4f, 0.4f));
        qopBackBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 40f);
        qopBackBtn.GetComponentInChildren<Text>().fontSize = 15;
        qopBackBtn.onClick.AddListener(() => ShowPanel(multiplayerPanel));

        quizOptionsPanel.SetActive(false);

        // Wire all quiz UI refs into MultiplayerQuizManager
        MultiplayerQuizManager.InjectQuizUI(qopStatusTxt, qopCourseTxt, qopSelectedTxt,
                                             qopFetchBtn, qopAiBtn, qopStartBtn, qopPrevBtn, qopNextBtn);
        qopStartBtn.onClick.AddListener(MultiplayerQuizManager.HostStartQuiz);

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
        EnsureMultiplayerSession();
        ApplySavedSensitivity();
        RebuildTaskList();
        RebuildGoalList();
    }

    private void ShowPanel(GameObject panel)
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (tasksPanel != null) tasksPanel.SetActive(false);
        if (goalsPanel != null) goalsPanel.SetActive(false);
        if (serverPanel != null) serverPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
        if (hostGamePanel != null) hostGamePanel.SetActive(false);
        if (quizOptionsPanel != null) quizOptionsPanel.SetActive(false);
        if (joinSubPanel != null) joinSubPanel.SetActive(false);
        if (friendsSubPanel != null) friendsSubPanel.SetActive(false);
        if (panel != null) panel.SetActive(true);

        // Keep cursor locked so player movement isn't interrupted
        if (panel == tasksPanel)
        {
            FetchProfilesForDevOptions();
        }
        else if (panel == serverPanel)
        {
            RefreshServerStatusLabel();
        }
        else if (panel == multiplayerPanel)
        {
            // Placeholder screen for the future multiplayer flow.
            RefreshMultiplayerDefaults();
        }
        else if (panel == settingsPanel)
        {
            RefreshVoiceChatButton();
            RefreshRudolfVoiceModeButton();
            RefreshPlayerModelButton();
            RefreshGuideDebugLinesButton();
            RefreshRudolfApiKeyInput();
        }
    }

    private void EnsureMultiplayerSession()
    {
        if (multiplayerSession != null)
        {
            return;
        }

        multiplayerSession = MultiplayerSessionManager.Instance;
        if (multiplayerSession != null)
        {
            multiplayerSession.StatusChanged += OnMultiplayerStatusChanged;
            multiplayerSession.FriendSessionsChanged += OnFriendSessionsChanged;
            OnMultiplayerStatusChanged(multiplayerSession.CurrentStatus);
            RefreshVoiceChatButton();
            RefreshRudolfVoiceModeButton();
            RefreshPlayerModelButton();
        }
    }

    private void OnPlayerModelClicked()
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null)
        {
            return;
        }

        multiplayerSession.CyclePlayerModel();
        RefreshPlayerModelButton();
    }

    private void OnVoiceChatClicked()
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null)
        {
            return;
        }

        multiplayerSession.CycleVoiceMode();
        RefreshVoiceChatButton();
    }

    private void OnRudolfVoiceModeClicked()
    {
        RobotCompanion.CycleRudolfVoiceMode();
        RefreshRudolfVoiceModeButton();
        RefreshVoiceChatButton();
    }

    private void OnMicrophoneDeviceClicked()
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null)
        {
            return;
        }

        multiplayerSession.CycleMicrophoneDevice();
        RefreshVoiceChatButton();
    }

    private void OnGuideDebugLinesClicked()
    {
        RobotCompanion.SetGuideDebugLinesEnabled(!RobotCompanion.GuideDebugLinesEnabled);
        RefreshGuideDebugLinesButton();
    }

    private void OnLanguageClicked()
    {
        MentoraLanguage nextLanguage = (MentoraLanguage)(((int)MentoraLocalization.CurrentLanguage + 1) % 4);
        MentoraLocalization.SetLanguage(nextLanguage);
        RefreshLanguageButton();
        RefreshGuideDebugLinesButton();
        RefreshPlayerModelButton();
        RefreshRudolfVoiceModeButton();
        RefreshVoiceChatButton();
    }

    private void RefreshLanguageButton()
    {
        if (languageButton == null)
        {
            return;
        }

        Text label = languageButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            MentoraLocalization.SetText(label, "Language: " + MentoraLocalization.CurrentLanguageLabel);
        }
    }

    private void RefreshGuideDebugLinesButton()
    {
        if (guideDebugLinesButton == null)
        {
            return;
        }

        bool enabled = RobotCompanion.GuideDebugLinesEnabled;
        Text label = guideDebugLinesButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            MentoraLocalization.SetText(label, "Rudolf Path Lines: " + (enabled ? "On" : "Off"));
        }

        Image image = guideDebugLinesButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = enabled
                ? new Color(0.0f, 0.55f, 0.70f, 1f)
                : new Color(0.35f, 0.34f, 0.50f, 1f);
        }
    }

    private void OnRudolfApiKeyChanged(string value)
    {
        string cleaned = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        PlayerPrefs.SetString(RobotVoiceBridge.OpenAiApiKeyPrefKey, cleaned);
        PlayerPrefs.Save();
        RefreshRudolfApiKeyInput();
    }

    private void RefreshPlayerModelButton()
    {
        if (playerModelButton == null || multiplayerSession == null)
        {
            return;
        }

        Text label = playerModelButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            MentoraLocalization.SetText(label, "Model: " + multiplayerSession.CurrentPlayerModelLabel);
        }
    }

    private void RefreshRudolfVoiceModeButton()
    {
        if (rudolfVoiceModeButton == null)
        {
            return;
        }

        RobotCompanion.RudolfVoiceMode mode = RobotCompanion.CurrentRudolfVoiceMode;
        Text label = rudolfVoiceModeButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            MentoraLocalization.SetText(label, "Rudolf: " + RobotCompanion.GetRudolfVoiceModeLabel());
        }

        Image image = rudolfVoiceModeButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = mode switch
            {
                RobotCompanion.RudolfVoiceMode.PushToTalk => new Color(0.42f, 0.36f, 0.72f, 1f),
                RobotCompanion.RudolfVoiceMode.Disabled => new Color(0.55f, 0.22f, 0.22f, 1f),
                _ => new Color(0.14f, 0.58f, 0.52f, 1f),
            };
        }
    }

    private void RefreshVoiceChatButton()
    {
        if (voiceChatButton == null || multiplayerSession == null)
        {
            return;
        }

        bool enabled = multiplayerSession.IsVoiceChatEnabled;
        Text label = voiceChatButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            MentoraLocalization.SetText(label, "Mode: " + multiplayerSession.GetVoiceModeLabel());
        }

        Image image = voiceChatButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = enabled ? new Color(0.18f, 0.55f, 0.80f, 1f) : new Color(0.55f, 0.22f, 0.22f, 1f);
        }

        if (microphoneDeviceButton != null)
        {
            Text micLabel = microphoneDeviceButton.GetComponentInChildren<Text>();
            if (micLabel != null)
            {
                MentoraLocalization.SetText(micLabel, "Mic: " + TruncateUiLabel(multiplayerSession.CurrentMicrophoneDevice, 24));
            }

            microphoneDeviceButton.interactable = multiplayerSession.GetMicrophoneDevices().Length > 0;
            Image micImage = microphoneDeviceButton.GetComponent<Image>();
            if (micImage != null)
            {
                micImage.color = microphoneDeviceButton.interactable
                    ? new Color(0.26f, 0.42f, 0.68f, 1f)
                    : new Color(0.28f, 0.28f, 0.32f, 1f);
            }
        }

        if (voiceHintText != null)
        {
            string provider = "Rudolf STT: server Groq";
            string tts = HasRudolfOpenAiKeyConfigured() ? "TTS: OpenAI" : "TTS: Wit fallback";
            string multiplayerMode = multiplayerSession.CurrentVoiceMode switch
            {
                MultiplayerSessionManager.VoiceChatMode.PushToTalk => "MP: hold V",
                MultiplayerSessionManager.VoiceChatMode.Muted => "MP: muted",
                _ => "MP: always",
            };
            string rudolfMode = RobotCompanion.CurrentRudolfVoiceMode switch
            {
                RobotCompanion.RudolfVoiceMode.PushToTalk => "Rudolf: hold " + RobotCompanion.RudolfPushToTalkLabel,
                RobotCompanion.RudolfVoiceMode.Disabled => "Rudolf: disabled",
                _ => "Rudolf: always",
            };
            voiceHintText.text = provider + " • " + tts + " • " + multiplayerMode + " • " + rudolfMode;
        }
    }

    private void OnFriendSessionsChanged()
    {
        if (friendsSubPanel != null && friendsSubPanel.activeSelf)
        {
            RebuildFriendSessionsList();
        }
    }

    private void RefreshRudolfApiKeyInput()
    {
        if (rudolfApiKeyInput == null)
        {
            return;
        }

        string savedKey = PlayerPrefs.GetString(RobotVoiceBridge.OpenAiApiKeyPrefKey, string.Empty);
        if (rudolfApiKeyInput.text != savedKey)
        {
            rudolfApiKeyInput.SetTextWithoutNotify(savedKey);
        }
    }

    private static bool HasRudolfOpenAiKeyConfigured()
    {
        string savedKey = PlayerPrefs.GetString(RobotVoiceBridge.OpenAiApiKeyPrefKey, string.Empty);
        return !string.IsNullOrWhiteSpace(savedKey) || !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }

    private void OnMultiplayerStatusChanged(string status)
    {
        if (multiplayerStatusText == null) return;

        // When hosting, keep showing the "Started server on <IP> port <port>" text
        // instead of the internal status messages from MultiplayerSessionManager.
        if (multiplayerStatusText.text.StartsWith("Started server on"))
        {
            // Only replace once we go offline again.
            if (string.IsNullOrWhiteSpace(status) || status == "Offline")
                multiplayerStatusText.text = "Offline";
            return;
        }

        multiplayerStatusText.text = string.IsNullOrWhiteSpace(status) ? "Offline" : status;
    }

    private void RefreshMultiplayerDefaults()
    {
        if (multiplayerNameInput != null)
        {
            string savedName = !string.IsNullOrWhiteSpace(PauseMenuManager.GetLoggedInChildName())
                ? PauseMenuManager.GetLoggedInChildName()
                : PlayerPrefs.GetString("MultiplayerPlayerName", "Player");

            if (string.IsNullOrWhiteSpace(multiplayerNameInput.text))
            {
                multiplayerNameInput.SetTextWithoutNotify(savedName);
            }
        }

        if (multiplayerAddressInput != null && string.IsNullOrWhiteSpace(multiplayerAddressInput.text))
        {
            multiplayerAddressInput.SetTextWithoutNotify(PlayerPrefs.GetString("MultiplayerHostAddress", ""));
        }

        RefreshVoiceChatButton();
        RefreshPlayerModelButton();
        RebuildFriendSessionsList();
    }

    private static string GetLocalLanIp()
    {
        try
        {
            foreach (var addr in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
            {
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !System.Net.IPAddress.IsLoopback(addr))
                {
                    return addr.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private static string TruncateUiLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Default";
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, Mathf.Max(0, maxLength - 1)) + "…";
    }

    private void HostMultiplayerGame()
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null) return;

        string playerName = multiplayerNameInput != null ? multiplayerNameInput.text : "Player";

        // Try port 7777 first; fall back to 7778 if it is in use.
        int port = 7777;
        try
        {
            var testListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 7777);
            testListener.Start();
            testListener.Stop();
        }
        catch
        {
            port = 7778;
        }

        string lanIp = GetLocalLanIp();
        multiplayerSession.HostGame(playerName, port);

        // Override the status text with a friendly message once the session manager
        // sets its own status — we piggyback on the StatusChanged event below but
        // for immediate feedback we set it here.
        if (multiplayerStatusText != null)
            multiplayerStatusText.text = "Started server on " + lanIp + " port " + port;
    }

    private static readonly Vector3 QuizIslandSpawn = new Vector3(76f, 9f, 426f);
    private static readonly Quaternion CodeWorldSpawnRotation = Quaternion.Euler(0f, 180f, 0f);

    private static void TeleportPlayerToQuizIsland()
    {
        // Move the local player to the quiz island spawn point
        Transform player = PlayerCache.ResolvePlayerTransform();
        if (player != null)
            player.position = QuizIslandSpawn;

        // Store spawn so joining clients also spawn there
        PlayerPrefs.SetFloat("MP_SpawnX", QuizIslandSpawn.x);
        PlayerPrefs.SetFloat("MP_SpawnY", QuizIslandSpawn.y);
        PlayerPrefs.SetFloat("MP_SpawnZ", QuizIslandSpawn.z);
        PlayerPrefs.SetInt("MP_UseCustomSpawn", 1);
        PlayerPrefs.Save();
    }

    private static void TeleportPlayerToCodeWorld()
    {
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.TeleportTo(CodeWorldRuntime.SpawnPosition, CodeWorldSpawnRotation);
        }
        else
        {
            Transform player = PlayerCache.ResolvePlayerTransform();
            if (player != null)
            {
                player.SetPositionAndRotation(CodeWorldRuntime.SpawnPosition, CodeWorldSpawnRotation);
            }
        }

        PlayerPrefs.SetFloat("MP_SpawnX", CodeWorldRuntime.SpawnPosition.x);
        PlayerPrefs.SetFloat("MP_SpawnY", CodeWorldRuntime.SpawnPosition.y);
        PlayerPrefs.SetFloat("MP_SpawnZ", CodeWorldRuntime.SpawnPosition.z);
        PlayerPrefs.SetInt("MP_UseCustomSpawn", 1);
        PlayerPrefs.Save();
    }

    private static void TeleportPlayerToCodeQuestIsland()
    {
        CodeWorldQuestIsland island = CodeWorldQuestIsland.GenerateFreshForFreestyle();
        Vector3 spawnPosition = CodeWorldQuestIsland.GetSpawnPosition(island);
        Quaternion spawnRotation = CodeWorldQuestIsland.GetSpawnRotation(island);
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.TeleportTo(spawnPosition, spawnRotation);
        }
        else
        {
            Transform player = PlayerCache.ResolvePlayerTransform();
            if (player != null)
            {
                player.SetPositionAndRotation(spawnPosition, spawnRotation);
            }
        }

        PlayerPrefs.SetFloat("MP_SpawnX", spawnPosition.x);
        PlayerPrefs.SetFloat("MP_SpawnY", spawnPosition.y);
        PlayerPrefs.SetFloat("MP_SpawnZ", spawnPosition.z);
        PlayerPrefs.SetInt("MP_UseCustomSpawn", 1);
        PlayerPrefs.Save();
    }

    private void JoinMultiplayerGame()
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null) return;

        string playerName = multiplayerNameInput != null ? multiplayerNameInput.text : "Player";
        string address = multiplayerAddressInput != null ? multiplayerAddressInput.text.Trim() : "127.0.0.1";
        if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";

        // Manual joins default to 7777; discovered LAN hosts carry their actual port.
        JoinMultiplayerGame(address, 7777);
    }

    private void JoinMultiplayerGame(string address, int port)
    {
        EnsureMultiplayerSession();
        if (multiplayerSession == null) return;

        string playerName = multiplayerNameInput != null ? multiplayerNameInput.text : "Player";
        string cleanedAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        int cleanedPort = Mathf.Clamp(port, 1024, 65535);
        multiplayerSession.JoinGame(playerName, cleanedAddress, cleanedPort);
        if (multiplayerAddressInput != null)
        {
            multiplayerAddressInput.SetTextWithoutNotify(cleanedAddress);
        }
    }

    private void ShowFriendsPanel()
    {
        EnsureMultiplayerSession();
        if (joinSubPanel != null) joinSubPanel.SetActive(false);
        if (friendsSubPanel == null) return;

        friendsSubPanel.SetActive(true);
        friendsSubPanel.transform.SetAsLastSibling();
        if (multiplayerSession != null)
        {
            multiplayerSession.RefreshFriendSessions();
        }
        RebuildFriendSessionsList();
    }

    private void RebuildFriendSessionsList()
    {
        if (friendListContainer == null)
        {
            return;
        }

        foreach (Transform child in friendListContainer.transform)
        {
            Destroy(child.gameObject);
        }

        List<MultiplayerSessionManager.FriendSession> sessions = multiplayerSession != null
            ? multiplayerSession.GetDiscoveredFriendSessions()
            : new List<MultiplayerSessionManager.FriendSession>();

        RectTransform contentRect = friendListContainer.GetComponent<RectTransform>();
        if (sessions.Count == 0)
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 80f);
            if (friendStatusText != null)
            {
                friendStatusText.text = "No hosted LAN games found yet.";
            }
            CreateText("NoFriends", friendListContainer.transform, "Ask your friend to host a game, then press Refresh.\nManual IP is still available.", 14, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.62f), new Vector2(0f, -34f), new Vector2(410f, 64f));
            return;
        }

        if (friendStatusText != null)
        {
            friendStatusText.text = "Click a discovered host to join.";
        }

        float itemHeight = 64f;
        float spacing = 6f;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, sessions.Count * (itemHeight + spacing));
        float y = -spacing;
        for (int i = 0; i < sessions.Count; i++)
        {
            MultiplayerSessionManager.FriendSession session = sessions[i];
            GameObject item = CreateUiObject("FriendSession_" + i, friendListContainer.transform);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.sizeDelta = new Vector2(-20f, itemHeight);
            itemRect.anchoredPosition = new Vector2(0f, y);
            item.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.055f);

            CreateText("FriendName", item.transform, session.DisplayName, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(-104f, 12f), new Vector2(230f, 24f));
            CreateText("FriendEndpoint", item.transform, session.Endpoint, 12, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.66f, 0.82f, 1f), new Vector2(-104f, -12f), new Vector2(230f, 22f));

            Button joinBtn = CreateButton(item.transform, "JoinFriendBtn", "Join", new Vector2(166f, 0f), new Color(0.24f, 0.62f, 0.36f, 1f));
            joinBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 36f);
            joinBtn.GetComponentInChildren<Text>().fontSize = 13;
            string address = session.Address;
            int port = session.Port;
            joinBtn.onClick.AddListener(() => {
                if (friendsSubPanel != null) friendsSubPanel.SetActive(false);
                JoinMultiplayerGame(address, port);
            });

            y -= itemHeight + spacing;
        }
    }

    private void RebuildTaskList()
    {
        if (taskListContainer == null) return;
        foreach (Transform child in taskListContainer.transform) Destroy(child.gameObject);

        float itemHeight = 56f;
        float spacing = 4f;
        if (availableProfiles.Count == 0)
        {
            RectTransform emptyRect = taskListContainer.GetComponent<RectTransform>();
            emptyRect.sizeDelta = new Vector2(emptyRect.sizeDelta.x, 70f);
            CreateText("EmptyProfiles", taskListContainer.transform, "No child profiles found on the server.", 15, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.65f), new Vector2(0f, -24f), new Vector2(420f, 32f));
            return;
        }

        float totalHeight = availableProfiles.Count * (itemHeight + spacing);
        RectTransform contentRect = taskListContainer.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, totalHeight);

        float y = -spacing;
        foreach (var profile in availableProfiles)
        {
            bool isCurrent = profile.Id == loggedInChildId;
            GameObject item = CreateUiObject("TaskItem_" + profile.Id, taskListContainer.transform);
            RectTransform iRect = item.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0, 1); iRect.anchorMax = new Vector2(1, 1);
            iRect.pivot = new Vector2(0.5f, 1);
            iRect.sizeDelta = new Vector2(-20, itemHeight);
            iRect.anchoredPosition = new Vector2(0, y);
            item.AddComponent<Image>().color = isCurrent
                ? new Color(0.15f, 0.35f, 0.15f, 0.35f)
                : new Color(1f, 1f, 1f, 0.05f);

            string stateLabel = isCurrent ? "[CURRENT] " : profile.IsOnline ? "[ONLINE] " : string.Empty;
            CreateText("Label", item.transform, stateLabel + profile.Name, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                isCurrent ? new Color(0.6f, 1f, 0.6f) : Color.white, new Vector2(-120, 10), new Vector2(300, 24));
            CreateText("Points", item.transform, profile.TotalPoints + " pts", 12, FontStyle.Italic, TextAnchor.MiddleLeft,
                new Color(0.8f, 0.9f, 1f), new Vector2(-120, -12), new Vector2(220, 20));

            Button enterBtn = CreateButton(item.transform, "EnterBtn", isCurrent ? "Current" : "Enter", new Vector2(180, 0), isCurrent ? new Color(0.25f, 0.45f, 0.28f) : new Color(0.2f, 0.6f, 0.3f));
            enterBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 34);
            enterBtn.GetComponentInChildren<Text>().fontSize = 13;
            enterBtn.interactable = !isCurrent;
            long profileId = profile.Id;
            enterBtn.onClick.AddListener(() => SwitchToProfile(profileId));
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
        cardRect.sizeDelta = new Vector2(450f, 110f);
        cardRect.anchoredPosition = new Vector2(0f, 92f);
        card.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 0.96f);
        card.AddComponent<Outline>().effectColor = new Color(0.0f, 0.7f, 1f, 0.25f);

        GameObject innerCard = CreateUiObject("SensitivityInnerCard", card.transform);
        RectTransform innerRect = innerCard.GetComponent<RectTransform>();
        innerRect.sizeDelta = new Vector2(410f, 80f);
        innerRect.anchoredPosition = new Vector2(0f, -1f);
        innerCard.AddComponent<Image>().color = new Color(0.11f, 0.16f, 0.23f, 0.9f);
        innerCard.AddComponent<Outline>().effectColor = new Color(0.0f, 0.65f, 1f, 0.3f);

        CreateText("SensitivityLabel", innerCard.transform, "Sensitivity", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, new Vector2(-72f, 20f), new Vector2(180f, 24f));
        sensitivityValueText = CreateText("SensitivityValue", innerCard.transform, "1.80", 14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.55f, 0.92f, 1f), new Vector2(132f, 20f), new Vector2(56f, 22f));
        CreateText("SensitivityHint", innerCard.transform, "Lower = steadier aim, higher = faster turn.", 10, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.68f, 0.78f, 0.92f), new Vector2(0f, -24f), new Vector2(352f, 18f));

        sensitivityInput = null;
        sensitivitySlider = CreateSlider(innerCard.transform, "SensitivitySlider", new Vector2(0f, -1f), new Vector2(280f, 22f), 0.2f, 6f, PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f));
        sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
    }

    private void CreateGlobalVoiceSettingsSection(Transform parent)
    {
        GameObject card = CreateUiObject("VoiceSettingsCard", parent);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(450f, 258f);
        cardRect.anchoredPosition = new Vector2(0f, -62f);
        card.AddComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 0.96f);
        card.AddComponent<Outline>().effectColor = new Color(0.0f, 0.7f, 1f, 0.25f);

        languageButton = CreateButton(card.transform, "LanguageBtn", "Language: English", new Vector2(0f, 102f), new Color(0.52f, 0.30f, 0.74f, 1f));
        languageButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 26f);
        languageButton.GetComponentInChildren<Text>().fontSize = 11;
        languageButton.onClick.AddListener(OnLanguageClicked);
        RefreshLanguageButton();

        playerModelButton = CreateButton(card.transform, "PlayerModelBtn", "Model: Girl", new Vector2(0f, 72f), new Color(0.26f, 0.50f, 0.58f, 1f));
        playerModelButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 28f);
        playerModelButton.GetComponentInChildren<Text>().fontSize = 12;
        playerModelButton.onClick.AddListener(OnPlayerModelClicked);

        voiceChatButton = CreateButton(card.transform, "VoiceChatBtn", "Mode: Always On", new Vector2(0f, 40f), new Color(0.18f, 0.55f, 0.80f, 1f));
        voiceChatButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 30f);
        voiceChatButton.GetComponentInChildren<Text>().fontSize = 12;
        voiceChatButton.onClick.AddListener(OnVoiceChatClicked);

        rudolfVoiceModeButton = CreateButton(card.transform, "RudolfVoiceModeBtn", "Rudolf: Always On", new Vector2(0f, 8f), new Color(0.14f, 0.58f, 0.52f, 1f));
        rudolfVoiceModeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 30f);
        rudolfVoiceModeButton.GetComponentInChildren<Text>().fontSize = 12;
        rudolfVoiceModeButton.onClick.AddListener(OnRudolfVoiceModeClicked);

        microphoneDeviceButton = CreateButton(card.transform, "MicrophoneDeviceBtn", "Mic: Default", new Vector2(0f, -24f), new Color(0.26f, 0.42f, 0.68f, 1f));
        microphoneDeviceButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 30f);
        microphoneDeviceButton.GetComponentInChildren<Text>().fontSize = 12;
        microphoneDeviceButton.onClick.AddListener(OnMicrophoneDeviceClicked);

        guideDebugLinesButton = CreateButton(card.transform, "GuideDebugLinesBtn", "Rudolf Path Lines: Off", new Vector2(0f, -56f), new Color(0.35f, 0.34f, 0.50f, 1f));
        guideDebugLinesButton.GetComponent<RectTransform>().sizeDelta = new Vector2(224f, 28f);
        guideDebugLinesButton.GetComponentInChildren<Text>().fontSize = 11;
        guideDebugLinesButton.onClick.AddListener(OnGuideDebugLinesClicked);

        CreateText("RudolfApiKeyLabel", card.transform, "Optional OpenAI TTS Key", 11, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.8f, 0.93f, 1f), new Vector2(0f, -82f), new Vector2(220f, 18f));
        rudolfApiKeyInput = CreateInputField(card.transform, "RudolfOpenAiKeyInput", "sk-...", new Vector2(0f, -102f), new Vector2(280f, 28f), false);
        rudolfApiKeyInput.contentType = InputField.ContentType.Password;
        rudolfApiKeyInput.asteriskChar = '•';
        rudolfApiKeyInput.SetTextWithoutNotify(PlayerPrefs.GetString(RobotVoiceBridge.OpenAiApiKeyPrefKey, string.Empty));
        rudolfApiKeyInput.onEndEdit.AddListener(OnRudolfApiKeyChanged);

        voiceHintText = CreateText("VoiceHintText", card.transform, "Used by multiplayer voice and Rudolf.", 10, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.65f, 0.78f, 0.95f), new Vector2(0f, -118f), new Vector2(340f, 22f));
        RefreshRudolfVoiceModeButton();
        RefreshGuideDebugLinesButton();
    }

    private void GenerateQrLogin()
    {
        if (GameClient.Instance != null && GameClient.Instance.IsConnected)
        {
            MentoraLocalization.SetText(qrStatusText, "Generating...");
            _ = GameClient.Instance.SendPacket(new GenerateQRLoginPacket());
        }
        else if (GameClient.Instance != null) { _ = ConnectAndTryAutoLogin(); }
    }

    private void LogoutAccount()
    {
        loggedInChildId = -1;
        loggedInChildName = "";
        loggedInChildPoints = 0;
        loggedInChildGameStatsJson = string.Empty;
        loggedInChildProgrammingProfileSummary = string.Empty;
        childGoals.Clear();
        RebuildGoalList();
        if (qrStatusText != null) qrStatusText.text = "Not logged in";
        if (qrCodeImage != null)
        {
            qrCodeImage.texture = null;
            qrCodeImage.gameObject.SetActive(false);
        }
        RefreshLoginButtons();
        try
        {
            if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to clear session file: " + e.Message);
        }

        PlayerPrefs.DeleteKey("loggedInChildId");
        PlayerPrefs.DeleteKey("loggedInChildName");
        PlayerPrefs.DeleteKey("loggedInChildPoints");
        PlayerPrefs.DeleteKey("loggedInChildGameStatsJson");
        PlayerPrefs.DeleteKey("loggedInChildProgrammingProfileSummary");
        PlayerPrefs.Save();
        MultiplayerSessionManager.Instance?.ClearLocalProgrammingProfile();
    }

    private void PauseGame()
    {
        RebuildIfNeeded();
        ReacquireControllerIfNeeded();
        ConfigureCanvasForCurrentMode();
        EnsureEventSystem();
        IsGamePaused = true;
        if (fpsController != null) fpsController.SetCameraControlEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (canvas != null)
        {
            canvas.gameObject.SetActive(true);
            UpdateVrCanvasPlacement(true);
            ShowPanel(mainPanel);
            RefreshDevOptionsVisibility();
            devUnlockProgress = 0;
            if (qrCodeImage != null && qrCodeImage.texture != null && loggedInChildId == -1) qrCodeImage.gameObject.SetActive(true);
            PlayMenuAnimation(true);
        }
        // Keep cursor locked so player can still move
    }

    private void ResumeGame()
    {
        ResetVrPointerState();
        PlayMenuAnimation(false);
        IsGamePaused = false;
        if (fpsController != null) fpsController.SetCameraControlEnabled(true);
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

    public static long GetLoggedInChildId()
    {
        if (instance != null && instance.loggedInChildId > 0)
        {
            return instance.loggedInChildId;
        }

        string stored = PlayerPrefs.GetString("loggedInChildId", "-1");
        return long.TryParse(stored, out long parsed) ? parsed : -1;
    }

    public static string BuildAiQuizProfileContext()
    {
        MultiplayerSessionManager session = MultiplayerSessionManager.Instance;
        if (session != null)
        {
            string mergedProfileContext = session.BuildMergedProgrammingProfileContext();
            if (!string.IsNullOrWhiteSpace(mergedProfileContext))
            {
                return mergedProfileContext;
            }
        }

        StringBuilder builder = new StringBuilder();
        string name = instance != null && !string.IsNullOrWhiteSpace(instance.loggedInChildName)
            ? instance.loggedInChildName
            : PlayerPrefs.GetString("loggedInChildName", "Player");
        int points = instance != null ? instance.loggedInChildPoints : ParseStoredInt("loggedInChildPoints");
        string statsJson = instance != null ? instance.loggedInChildGameStatsJson : PlayerPrefs.GetString("loggedInChildGameStatsJson", string.Empty);
        string programmingProfileSummary = instance != null
            ? instance.loggedInChildProgrammingProfileSummary
            : PlayerPrefs.GetString("loggedInChildProgrammingProfileSummary", string.Empty);

        builder.AppendLine("Child name: " + name);
        builder.AppendLine("Total points: " + points);
        if (!string.IsNullOrWhiteSpace(programmingProfileSummary))
        {
            builder.AppendLine("Programming profile summary:");
            builder.AppendLine(programmingProfileSummary);
        }

        if (instance != null)
        {
            builder.AppendLine("Streak: " + instance.serverStreak);
            builder.AppendLine("Completed tasks: " + instance.serverCompletedTaskCount + " / " + instance.serverTotalTaskCount);

            if (instance.availableTasks != null && instance.availableTasks.Count > 0)
            {
                builder.AppendLine("Known tasks:");
                int maxTasks = Mathf.Min(8, instance.availableTasks.Count);
                for (int i = 0; i < maxTasks; i++)
                {
                    builder.AppendLine("- " + instance.availableTasks[i].Title + " (" + instance.availableTasks[i].Points + " pts)");
                }
            }

            if (instance.childGoals != null && instance.childGoals.Count > 0)
            {
                builder.AppendLine("Parent goals:");
                int maxGoals = Mathf.Min(6, instance.childGoals.Count);
                for (int i = 0; i < maxGoals; i++)
                {
                    var goal = instance.childGoals[i];
                    builder.AppendLine("- " + goal.Title + " | reward: " + goal.Reward + " | completed: " + (goal.IsCompleted ? "yes" : "no"));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(statsJson))
        {
            builder.AppendLine("Game stats JSON:");
            builder.AppendLine(statsJson);
        }

        if (session != null)
        {
            List<string> ids = session.GetConnectedPlayerIds();
            if (ids != null && ids.Count > 0)
            {
                builder.AppendLine("Connected multiplayer lobby:");
                for (int i = 0; i < ids.Count; i++)
                {
                    builder.AppendLine("- " + session.ResolvePlayerName(ids[i]));
                }
                builder.AppendLine("Use the whole lobby roster as the group learning context for the quiz.");
            }
        }

        return builder.ToString().Trim();
    }

    private static int ParseStoredInt(string key)
    {
        string stored = PlayerPrefs.GetString(key, "0");
        return int.TryParse(stored, out int value) ? value : 0;
    }

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
        v = Mathf.Clamp(v, 0.2f, 6f);
        if (sensitivitySlider != null && !Mathf.Approximately(sensitivitySlider.value, v))
        {
            sensitivitySlider.SetValueWithoutNotify(v);
        }

        if (syncInput && sensitivityInput != null)
        {
            sensitivityInput.SetTextWithoutNotify(v.ToString("0.00"));
        }

        PlayerPrefs.SetFloat(MouseSensitivityPrefKey, v);
        PlayerPrefs.Save();
        UpdateSensitivityLabel(v);
        if (fpsController != null) fpsController.SetMouseSensitivity(v);
    }

    private float GetCurrentSensitivity()
    {
        if (sensitivitySlider != null)
        {
            return Mathf.Clamp(sensitivitySlider.value, 0.2f, 6f);
        }

        if (sensitivityInput != null && float.TryParse(sensitivityInput.text, out float val))
        {
            return Mathf.Clamp(val, 0.2f, 6f);
        }

        return PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f);
    }

    private void RefreshLoginButtons()
    {
        bool isLoggedIn = loggedInChildId != -1;

        if (qrButton != null)
        {
            qrButton.gameObject.SetActive(!isLoggedIn);
            qrButton.interactable = !isLoggedIn;
        }

        if (logoutButton != null)
        {
            logoutButton.gameObject.SetActive(isLoggedIn);
            logoutButton.interactable = isLoggedIn;
        }
    }

    private void UpdateSensitivityLabel(float v) { if (sensitivityValueText != null) sensitivityValueText.text = v.ToString("0.00"); }

    private void ReacquireControllerIfNeeded() { if (fpsController == null) fpsController = PlayerCache.GetFps(); }

    private void UpdateMenuPresentation()
    {
        if (canvas == null)
        {
            return;
        }

        ConfigureCanvasForCurrentMode();
        if (IsGamePaused)
        {
            UpdateVrCanvasPlacement(false);
        }
    }

    private void UpdateVrPointer()
    {
        if (!IsGamePaused || canvas == null || canvas.renderMode != RenderMode.WorldSpace || !IsVrPauseMenuActive())
        {
            ResetVrPointerState();
            return;
        }

        if (!TryGetRightControllerRay(out Vector3 rayOrigin, out Vector3 rayDirection))
        {
            ResetVrPointerState();
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            ResetVrPointerState();
            return;
        }

        Plane canvasPlane = new Plane(-canvas.transform.forward, canvas.transform.position);
        if (!canvasPlane.Raycast(new Ray(rayOrigin, rayDirection), out float distanceToPlane) ||
            distanceToPlane < 0f ||
            distanceToPlane > VrPointerMaxDistance)
        {
            if (vrCursor != null)
            {
                vrCursor.SetActive(false);
            }
            ClearVrHover();
            HandleVrSelectReleaseIfNeeded();
            return;
        }

        Vector3 hitPoint = rayOrigin + (rayDirection * distanceToPlane);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, hitPoint);

        if (!IsWorldPointInsideCanvas(canvasRect, hitPoint))
        {
            ClearVrHover();
            HandleVrSelectReleaseIfNeeded();
            return;
        }

        EnsureVrPointerEventData();
        if (vrPointerEventData == null || canvasRaycaster == null)
        {
            ResetVrPointerState();
            return;
        }

        vrPointerEventData.Reset();
        vrPointerEventData.button = PointerEventData.InputButton.Left;
        vrPointerEventData.position = screenPoint;
        vrPointerEventData.pointerCurrentRaycast = default;
        vrRaycastResults.Clear();
        canvasRaycaster.Raycast(vrPointerEventData, vrRaycastResults);

        RaycastResult raycastResult = FindFirstInteractiveRaycast(vrRaycastResults);
        GameObject hitObject = raycastResult.gameObject;

        UpdateVrHover(hitObject);
        vrPointerEventData.pointerCurrentRaycast = raycastResult;

        bool selectPressed = IsVrSelectPressed();
        bool selectPressedThisFrame = selectPressed && !vrSelectWasPressed;
        bool selectReleasedThisFrame = !selectPressed && vrSelectWasPressed;
        vrSelectWasPressed = selectPressed;

        if (selectPressedThisFrame && hitObject != null)
        {
            vrPressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) ?? hitObject;
            vrPointerEventData.eligibleForClick = true;
            vrPointerEventData.pointerPress = vrPressedObject;
            vrPointerEventData.rawPointerPress = hitObject;
            vrPointerEventData.pressPosition = screenPoint;
            vrPointerEventData.pointerPressRaycast = raycastResult;
            ExecuteEvents.Execute(vrPressedObject, vrPointerEventData, ExecuteEvents.pointerDownHandler);
        }

        if (selectReleasedThisFrame)
        {
            HandleVrSelectRelease(hitObject);
        }

        GameObject currentClickHandler = hitObject != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) ?? hitObject : null;
        bool isPressingCurrent = selectPressed && currentClickHandler != null && currentClickHandler == vrPressedObject;
        ShowVrCursor(hitPoint, isPressingCurrent);
    }

    private void ConfigureCanvasForCurrentMode()
    {
        if (canvas == null)
        {
            return;
        }

        bool vrActive = IsVrPauseMenuActive();
        Camera targetCamera = GetMenuCamera();

        if (vrActive && targetCamera != null)
        {
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }

            canvas.worldCamera = targetCamera;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.localScale = VrMenuScale;
            }
        }
        else
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            canvas.worldCamera = null;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localPosition = Vector3.zero;
                canvasRect.localRotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one;
            }
        }
    }

    private void UpdateVrCanvasPlacement(bool snap)
    {
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
        {
            return;
        }

        Camera targetCamera = GetMenuCamera();
        if (targetCamera == null)
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

        Vector3 desiredPosition = camTransform.position + forward * VrMenuDistance;
        desiredPosition.y = camTransform.position.y + VrMenuHeightOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(desiredPosition - camTransform.position, Vector3.up);

        Transform canvasTransform = canvas.transform;
        if (snap)
        {
            canvasTransform.position = desiredPosition;
            canvasTransform.rotation = desiredRotation;
        }
        else
        {
            float moveT = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            float rotateT = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            canvasTransform.position = Vector3.Lerp(canvasTransform.position, desiredPosition, moveT);
            canvasTransform.rotation = Quaternion.Slerp(canvasTransform.rotation, desiredRotation, rotateT);
        }
    }

    private bool IsVrPauseMenuActive()
    {
        if (XRSettings.enabled && XRSettings.isDeviceActive) return true;
        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid) return true;
        try
        {
            return OVRInput.IsControllerConnected(OVRInput.Controller.LTouch) ||
                   OVRInput.IsControllerConnected(OVRInput.Controller.Touch);
        }
        catch { return false; }
    }

    private Camera GetMenuCamera()
    {
        if (fpsController != null)
        {
            Camera fpsCamera = fpsController.GetComponentInChildren<Camera>(true);
            if (fpsCamera != null)
            {
                menuCamera = fpsCamera;
                return menuCamera;
            }
        }

        if (Camera.main != null)
        {
            menuCamera = Camera.main;
            return menuCamera;
        }

        return menuCamera;
    }

    private void ApplySavedSensitivity()
    {
        float v = PlayerPrefs.GetFloat(MouseSensitivityPrefKey, 1.8f);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(v);
        if (sensitivityInput != null) sensitivityInput.SetTextWithoutNotify(v.ToString("0.00"));
        SetSensitivityValue(v, syncInput: false);
    }

    private void HandleDevUnlockInput()
    {
        if (devOptionsUnlocked || Keyboard.current == null)
        {
            return;
        }

        KeyControl pressedKey = GetLetterKeyPressedThisFrame();
        if (pressedKey == null)
        {
            return;
        }

        char expected = DevUnlockCode[devUnlockProgress];
        char typed = char.ToLowerInvariant(pressedKey.displayName.Length > 0 ? pressedKey.displayName[0] : '\0');

        if (typed == expected)
        {
            devUnlockProgress++;
            if (devUnlockProgress >= DevUnlockCode.Length)
            {
                devOptionsUnlocked = true;
                devUnlockProgress = 0;
                RefreshDevOptionsVisibility();
            }
            return;
        }

        devUnlockProgress = typed == DevUnlockCode[0] ? 1 : 0;
    }

    private static KeyControl GetLetterKeyPressedThisFrame()
    {
        if (Keyboard.current == null)
        {
            return null;
        }

        KeyControl[] keys =
        {
            Keyboard.current.aKey, Keyboard.current.bKey, Keyboard.current.cKey, Keyboard.current.dKey,
            Keyboard.current.eKey, Keyboard.current.fKey, Keyboard.current.gKey, Keyboard.current.hKey,
            Keyboard.current.iKey, Keyboard.current.jKey, Keyboard.current.kKey, Keyboard.current.lKey,
            Keyboard.current.mKey, Keyboard.current.nKey, Keyboard.current.oKey, Keyboard.current.pKey,
            Keyboard.current.qKey, Keyboard.current.rKey, Keyboard.current.sKey, Keyboard.current.tKey,
            Keyboard.current.uKey, Keyboard.current.vKey, Keyboard.current.wKey, Keyboard.current.xKey,
            Keyboard.current.yKey, Keyboard.current.zKey
        };

        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] != null && keys[i].wasPressedThisFrame)
            {
                return keys[i];
            }
        }

        return null;
    }

    private void RefreshDevOptionsVisibility()
    {
        if (devOptionsButton != null)
        {
            devOptionsButton.gameObject.SetActive(devOptionsUnlocked);
        }
    }

    private void CreateDevProfile()
    {
        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            return;
        }

        string profileName = "DevKid " + devProfileCounter.ToString("000");
        devProfileCounter++;
        _ = GameClient.Instance.SendPacket(new DevCreateChildProfilePacket(profileName));
    }

    private void FetchProfilesForDevOptions()
    {
        if (!devOptionsUnlocked || !IsGamePaused || GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            return;
        }

        if (devAuthStatusText != null)
        {
            devAuthStatusText.text = "Loading profiles...";
        }
        _ = GameClient.Instance.SendPacket(new FetchAllChildrenPacket());
        if (loggedInChildId != -1)
        {
            _ = GameClient.Instance.SendPacket(new FetchChildrenPacket());
        }
    }

    private void SwitchToProfile(long childId)
    {
        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            return;
        }

        if (devAuthStatusText != null)
        {
            devAuthStatusText.text = "Switching profile...";
        }
        _ = GameClient.Instance.SendPacket(new DevLoginAsChildPacket(childId));
    }

    private async void SwitchGameServer(string url)
    {
        if (GameClient.Instance == null)
        {
            return;
        }

        if (serverStatusText != null)
        {
            serverStatusText.text = "Switching...";
        }

        await GameClient.Instance.SwitchServer(url);
        RefreshServerStatusLabel();
    }

    private void SwitchToLocalServer()
    {
        string port = localPortInput != null && !string.IsNullOrWhiteSpace(localPortInput.text) ? localPortInput.text.Trim() : "8080";
        SwitchGameServer("ws://127.0.0.1:" + port);
    }

    private void RefreshServerStatusLabel()
    {
        if (serverStatusText == null || GameClient.Instance == null)
        {
            return;
        }

        serverStatusText.text = "Current: " + GameClient.Instance.ServerUrl;
    }

    private void MergeProfileList(IEnumerable<FetchChildrenResponsePacket.ChildDto> incomingProfiles)
    {
        if (incomingProfiles == null)
        {
            return;
        }

        Dictionary<long, FetchChildrenResponsePacket.ChildDto> merged = new Dictionary<long, FetchChildrenResponsePacket.ChildDto>();
        for (int i = 0; i < availableProfiles.Count; i++)
        {
            merged[availableProfiles[i].Id] = availableProfiles[i];
        }

        foreach (var profile in incomingProfiles)
        {
            merged[profile.Id] = profile;
        }

        availableProfiles = new List<FetchChildrenResponsePacket.ChildDto>(merged.Values);
        availableProfiles.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
    }


    private void SetupVrPointer()
    {
        // Ensure PointableCanvasModule exists in scene
        EnsurePointableCanvasModule();
        canvasRaycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : canvasRaycaster;
        
        // Create VR cursor
        if (vrCursor == null)
        {
            vrCursor = CreateUiObject("PauseMenuVrCursor", canvas.transform);
            vrCursorRect = vrCursor.GetComponent<RectTransform>();
            vrCursorRect.anchorMin = new Vector2(0.5f, 0.5f);
            vrCursorRect.anchorMax = new Vector2(0.5f, 0.5f);
            vrCursorRect.pivot = new Vector2(0.5f, 0.5f);
            vrCursorRect.sizeDelta = VrCursorHoverSize;

            vrCursorImage = vrCursor.AddComponent<Image>();
            vrCursorImage.sprite = GetVrCursorSprite();
            vrCursorImage.color = vrCursorHoverColor;
            vrCursorImage.raycastTarget = false;

            vrCursorOutline = vrCursor.AddComponent<Outline>();
            vrCursorOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            vrCursorOutline.effectDistance = new Vector2(2f, -2f);

            vrCursor.SetActive(false);
        }
        else if (vrCursorRect == null)
        {
            vrCursorRect = vrCursor.GetComponent<RectTransform>();
            vrCursorImage = vrCursor.GetComponent<Image>();
            vrCursorOutline = vrCursor.GetComponent<Outline>();
        }
        
        // Subscribe to pointer events
        if (pointableCanvas != null)
        {
            pointableCanvas.WhenPointerEventRaised += OnVrPointerEvent;
        }
    }

    private static Sprite GetVrCursorSprite()
    {
        if (vrCursorSprite != null)
        {
            return vrCursorSprite;
        }

        var texture = Texture2D.whiteTexture;
        vrCursorSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return vrCursorSprite;
    }
    
    private void OnVrPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                ShowVrCursor(evt.Pose.position, evt.Pose.rotation);
                break;
            case PointerEventType.Unhover:
                HideVrCursor();
                break;
            case PointerEventType.Select:
                SetVrCursorSelect(true);
                break;
            case PointerEventType.Unselect:
                SetVrCursorSelect(false);
                break;
        }
    }
    
    private void ShowVrCursor(Vector3 position, Quaternion rotation)
    {
        _ = rotation;
        ShowVrCursor(position, false);
    }

    private void ShowVrCursor(Vector3 position, bool isSelecting)
    {
        if (vrCursor != null && vrCursorRect != null)
        {
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, position),
                    canvas.worldCamera,
                    out localPoint))
            {
                vrCursor.SetActive(false);
                return;
            }

            vrCursor.SetActive(true);
            vrCursor.transform.SetAsLastSibling();
            vrCursorRect.anchoredPosition = localPoint * VrPointerSensitivity;
            vrCursorRect.sizeDelta = isSelecting ? VrCursorPressedSize : VrCursorHoverSize;

            if (vrCursorImage != null)
            {
                vrCursorImage.color = isSelecting ? vrCursorSelectColor : vrCursorHoverColor;
            }
        }
    }
    
    private void HideVrCursor()
    {
        if (vrCursor != null)
        {
            vrCursor.SetActive(false);
        }
    }
    
    private void SetVrCursorSelect(bool selecting)
    {
        if (vrCursorImage != null && vrCursor.activeSelf)
        {
            vrCursorImage.color = selecting ? vrCursorSelectColor : vrCursorHoverColor;
            if (vrCursorRect != null)
            {
                vrCursorRect.sizeDelta = selecting ? VrCursorPressedSize : VrCursorHoverSize;
            }
        }
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
            HandleVrSelectRelease(null);
        }
    }

    private void HandleVrSelectRelease(GameObject currentHitObject)
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

    private bool TryGetRightControllerRay(out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;

        Transform reference = fpsController != null ? fpsController.transform : null;

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
    
    private static void EnsurePointableCanvasModule()
    {
        PointableCanvasModule existing = Object.FindObjectOfType<PointableCanvasModule>();
        if (existing != null) return;
        
        var eventSystem = EventSystem.current;
        GameObject target = eventSystem != null ? eventSystem.gameObject : null;
        
        if (target == null)
        {
            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null) target = es.gameObject;
        }
        
        if (target == null)
        {
            target = new GameObject("EventSystem");
            target.AddComponent<EventSystem>();
            target.AddComponent<InputSystemUIInputModule>();
        }
        
        target.AddComponent<PointableCanvasModule>();
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
        MentoraLocalization.Register(t, content);
        return t;
    }

    private static Sprite _roundedSprite;
    private static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        const int size = 128;
        const float radius = 40f;
        const float edgeFeather = 1.75f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        Vector2 halfSize = Vector2.one * (size * 0.5f);
        Vector2 roundedBox = new Vector2(halfSize.x - radius, halfSize.y - radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - halfSize;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - roundedBox;
                float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                float signedDistance = outside + inside - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - signedDistance / edgeFeather) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        _roundedSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            Vector4.one * radius);
        return _roundedSprite;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color col)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform r = obj.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(420f, 58f);
        r.anchoredPosition = pos;

        Image img = obj.AddComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type   = Image.Type.Sliced;
        img.color  = col;

        Button b = obj.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        cb.pressedColor     = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.selectedColor    = Color.white;
        cb.fadeDuration     = 0.08f;
        b.colors = cb;

        // Subtle glow outline
        var outline = obj.AddComponent<Outline>();
        outline.effectColor    = new Color(col.r * 1.4f, col.g * 1.4f, col.b * 1.4f, 0.55f);
        outline.effectDistance = new Vector2(0f, -2f);

        // Label
        var lbl = CreateText(name + "L", obj.transform, label, 20, FontStyle.Bold,
                             TextAnchor.MiddleCenter, Color.white,
                             Vector2.zero, new Vector2(380f, 44f));
        lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
        return b;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, float value)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        Slider slider = obj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        GameObject background = CreateUiObject("Background", obj.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 10f);
        backgroundRect.anchoredPosition = Vector2.zero;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.sprite = GetRoundedSprite();
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(0.08f, 0.11f, 0.17f, 1f);

        GameObject fillArea = CreateUiObject("Fill Area", obj.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(-22f, 10f);
        fillAreaRect.anchoredPosition = Vector2.zero;

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.sprite = GetRoundedSprite();
        fillImage.type = Image.Type.Sliced;
        fillImage.color = new Color(0.10f, 0.78f, 0.95f, 1f);

        GameObject handleArea = CreateUiObject("Handle Slide Area", obj.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        GameObject handle = CreateUiObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 22f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.sprite = GetRoundedSprite();
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color(0.88f, 0.97f, 1f, 1f);
        Outline handleOutline = handle.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0f, 0.85f, 1f, 0.7f);
        handleOutline.effectDistance = new Vector2(1f, -1f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
        return slider;
    }

    private static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size, bool multiLine)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.13f, 0.17f, 0.24f, 0.98f);

        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.72f, 0.95f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);

        InputField input = obj.AddComponent<InputField>();
        input.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;

        Text text = CreateText(name + "Text", obj.transform, string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 6f);
        textRect.offsetMax = new Vector2(-10f, -6f);
        text.alignment = multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
        text.supportRichText = false;

        Text hint = CreateText(name + "Placeholder", obj.transform, placeholder, 16, FontStyle.Italic, text.alignment, new Color(1f, 1f, 1f, 0.35f), Vector2.zero, size);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = new Vector2(10f, 6f);
        hintRect.offsetMax = new Vector2(-10f, -6f);
        hint.supportRichText = false;

        input.textComponent = text;
        input.placeholder = hint;
        return input;
    }

}
