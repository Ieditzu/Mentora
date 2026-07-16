using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mentora.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Scene-authored AI/ML island controller. The island geometry and three portals live in the
/// scene; this component owns the authenticated, server-authoritative challenge experience.
/// </summary>
public sealed class MachineLearningIsland : MonoBehaviour
{
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard
    }

    private const float RequestTimeoutSeconds = 20f;
    private const int MaximumSourceBytes = 64 * 1024;
    private const int MaximumVisibleOutputCharacters = 5000;

    [Header("Scene references")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float fallbackSpawnYaw = 180f;

    private readonly List<MachineLearningProblem> visibleProblems = new List<MachineLearningProblem>(3);
    private readonly Dictionary<string, string> sourceDrafts = new Dictionary<string, string>(StringComparer.Ordinal);

    private MachineLearningProblemCatalog catalog;
    private Difficulty selectedDifficulty = Difficulty.Easy;
    private int selectedProblemIndex;
    private string pendingCatalogRequestId = string.Empty;
    private string pendingSubmissionRequestId = string.Empty;
    private float catalogRequestStartedAt;
    private float submissionRequestStartedAt;
    private GameClient subscribedClient;

    private GameObject canvasRoot;
    private Text headerText;
    private Text difficultyText;
    private Text problemTitleText;
    private Text problemBodyText;
    private Text datasetText;
    private Text resultText;
    private InputField sourceEditor;
    private Button previousButton;
    private Button nextButton;
    private Button resetButton;
    private Button hintButton;
    private Button runButton;
    private Button closeButton;
    private bool touchHudSuppressed;
    private GameObject hiddenTouchHudObject;
    private Canvas hiddenTouchCanvas;
    private bool hiddenTouchHudWasActive;
    private bool hiddenTouchCanvasWasEnabled;

    public bool IsOpen => canvasRoot != null && canvasRoot.activeSelf;

    private void Awake()
    {
        if (spawnPoint == null)
        {
            Transform sceneSpawn = transform.Find("SpawnPoint");
            if (sceneSpawn != null)
            {
                spawnPoint = sceneSpawn;
            }
        }

        UnityMainThreadDispatcher.Initialize();
    }

    private void Start()
    {
        BuildUi();
        TrySubscribeToNetwork();
        MentoraLocalization.LanguageChanged += OnLanguageChanged;
        RefreshWorldLabels();
    }

    private void Update()
    {
        TrySubscribeToNetwork();

        if (!IsOpen)
        {
            return;
        }

        MaintainTouchHudSuppression();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        float now = Time.unscaledTime;
        if (!string.IsNullOrEmpty(pendingCatalogRequestId) && now - catalogRequestStartedAt >= RequestTimeoutSeconds)
        {
            pendingCatalogRequestId = string.Empty;
            SetResult(Localize("The server did not return the ML catalog in time. Try again.",
                "Serverul nu a returnat catalogul ML la timp. Încearcă din nou."), ErrorColor);
            SetRunInteractable(visibleProblems.Count > 0);
        }

        if (!string.IsNullOrEmpty(pendingSubmissionRequestId) && now - submissionRequestStartedAt >= RequestTimeoutSeconds)
        {
            pendingSubmissionRequestId = string.Empty;
            SetResult(Localize("Grading timed out. The attempt was not confirmed; you can run it again.",
                "Evaluarea a expirat. Încercarea nu a fost confirmată; o poți rula din nou."), ErrorColor);
            SetRunInteractable(visibleProblems.Count > 0);
        }
    }

    private void OnDestroy()
    {
        MentoraLocalization.LanguageChanged -= OnLanguageChanged;
        if (subscribedClient != null)
        {
            subscribedClient.OnPacketReceived -= OnPacketReceived;
        }

        RestoreTouchHud();
        RestorePlayerControl();
    }

    public void OpenDifficulty(Difficulty difficulty)
    {
        selectedDifficulty = difficulty;
        selectedProblemIndex = 0;
        pendingSubmissionRequestId = string.Empty;

        if (canvasRoot == null)
        {
            BuildUi();
        }

        canvasRoot.SetActive(true);
        HideTouchHud();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPlayerMovement(false);
        ApplyStaticLocalization();
        RebuildVisibleProblems(null);
        SetResult(Localize("Loading the nine server challenges…", "Se încarcă cele nouă provocări de pe server…"), InfoColor);
        _ = FetchCatalogAsync();
    }

    public void Close()
    {
        SaveCurrentDraft();
        pendingCatalogRequestId = string.Empty;
        pendingSubmissionRequestId = string.Empty;
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(false);
        }

        RestoreTouchHud();
        RestorePlayerControl();
    }

    public Vector3 GetSpawnPosition()
    {
        return spawnPoint != null ? spawnPoint.position : transform.position + new Vector3(0f, 2.2f, -14f);
    }

    public Quaternion GetSpawnRotation()
    {
        return spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(0f, fallbackSpawnYaw, 0f);
    }

    public static bool TeleportLocalPlayer()
    {
        MachineLearningIsland island = FindObjectOfType<MachineLearningIsland>(true);
        if (island == null)
        {
            Debug.LogWarning("[MachineLearningIsland] No scene-authored ML island was found.");
            return false;
        }

        Vector3 position = island.GetSpawnPosition();
        Quaternion rotation = island.GetSpawnRotation();
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.TeleportTo(position, rotation);
        }
        else
        {
            Transform player = PlayerCache.ResolvePlayerTransform();
            if (player == null)
            {
                return false;
            }

            player.SetPositionAndRotation(position, rotation);
        }

        PlayerPrefs.SetFloat("MP_SpawnX", position.x);
        PlayerPrefs.SetFloat("MP_SpawnY", position.y);
        PlayerPrefs.SetFloat("MP_SpawnZ", position.z);
        PlayerPrefs.SetInt("MP_UseCustomSpawn", 1);
        PlayerPrefs.Save();
        return true;
    }

    private async Task FetchCatalogAsync()
    {
        if (PauseMenuManager.GetLoggedInChildId() <= 0)
        {
            SetResult(Localize("Log in with QR first to load ML challenges.",
                "Autentifică-te mai întâi prin QR pentru a încărca provocările ML."), ErrorColor);
            return;
        }

        TrySubscribeToNetwork();
        GameClient client = GameClient.Instance;
        if (client == null)
        {
            SetResult(Localize("The game client is not available in this scene.",
                "Clientul jocului nu este disponibil în această scenă."), ErrorColor);
            return;
        }

        try
        {
            if (!client.IsConnected)
            {
                SetResult(Localize("Connecting to the learning server…", "Conectare la serverul de învățare…"), InfoColor);
                await client.Connect();
            }

            if (!client.IsConnected || !IsOpen)
            {
                if (IsOpen)
                {
                    SetResult(Localize("Could not connect to the server.", "Nu s-a putut realiza conexiunea la server."), ErrorColor);
                }
                return;
            }

            pendingCatalogRequestId = Guid.NewGuid().ToString("N");
            catalogRequestStartedAt = Time.unscaledTime;
            await client.SendPacket(new FetchMlProblemsPacket(pendingCatalogRequestId));
        }
        catch (Exception exception)
        {
            pendingCatalogRequestId = string.Empty;
            SetResult(Localize("Could not load ML challenges: ", "Provocările ML nu au putut fi încărcate: ") + exception.Message, ErrorColor);
        }
    }

    private void SubmitCurrentSolution()
    {
        if (!IsOpen || !string.IsNullOrEmpty(pendingSubmissionRequestId) || visibleProblems.Count == 0)
        {
            return;
        }

        MachineLearningProblem problem = visibleProblems[selectedProblemIndex];
        if (PauseMenuManager.GetLoggedInChildId() <= 0)
        {
            SetResult(Localize("Log in with QR first before submitting a solution.",
                "Autentifică-te mai întâi prin QR înainte de a trimite o soluție."), ErrorColor);
            return;
        }

        string source = sourceEditor != null ? sourceEditor.text ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            SetResult(Localize("Write a Python solution before running it.", "Scrie o soluție Python înainte de rulare."), ErrorColor);
            return;
        }

        int sourceBytes = Encoding.UTF8.GetByteCount(source);
        if (sourceBytes > MaximumSourceBytes)
        {
            SetResult(Localize("Source code is larger than the 64 KB server limit.",
                "Codul sursă depășește limita serverului de 64 KB."), ErrorColor);
            return;
        }

        GameClient client = GameClient.Instance;
        if (client == null || !client.IsConnected)
        {
            SetResult(Localize("The learning server is offline. Re-enter the portal to reconnect.",
                "Serverul de învățare este offline. Intră din nou în portal pentru reconectare."), ErrorColor);
            return;
        }

        SaveCurrentDraft();
        pendingSubmissionRequestId = Guid.NewGuid().ToString("N");
        submissionRequestStartedAt = Time.unscaledTime;
        SetRunInteractable(false);
        SetResult(Localize("Running Python securely and grading hidden data…",
            "Python rulează securizat și se evaluează datele ascunse…"), InfoColor);
        _ = client.SendPacket(new SubmitMlSolutionPacket(pendingSubmissionRequestId, problem.slug, source));
    }

    private void TrySubscribeToNetwork()
    {
        GameClient client = GameClient.Instance;
        if (client == null || client == subscribedClient)
        {
            return;
        }

        if (subscribedClient != null)
        {
            subscribedClient.OnPacketReceived -= OnPacketReceived;
        }

        subscribedClient = client;
        subscribedClient.OnPacketReceived += OnPacketReceived;
    }

    private void OnPacketReceived(Packet packet)
    {
        if (packet is MlProblemsResponsePacket problems)
        {
            string requestId = problems.RequestId;
            string json = problems.ProblemsJson;
            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleCatalogResponse(requestId, json));
        }
        else if (packet is MlSubmissionResultPacket submission)
        {
            string requestId = submission.RequestId;
            string json = submission.ResultJson;
            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleSubmissionResponse(requestId, json));
        }
    }

    private void HandleCatalogResponse(string requestId, string json)
    {
        if (string.IsNullOrEmpty(pendingCatalogRequestId) ||
            !string.Equals(requestId, pendingCatalogRequestId, StringComparison.Ordinal))
        {
            return;
        }

        pendingCatalogRequestId = string.Empty;
        if (!MachineLearningJson.TryParseCatalog(json, out MachineLearningProblemCatalog parsed, out string parseError))
        {
            SetResult(parseError, ErrorColor);
            return;
        }

        string selectedSlug = CurrentProblem != null ? CurrentProblem.slug : null;
        catalog = parsed;
        RebuildVisibleProblems(selectedSlug);
        int count = catalog.problems.Length;
        string status = count == 9
            ? Localize("Nine challenges loaded. Server grading is authoritative.",
                "Cele nouă provocări au fost încărcate. Evaluarea serverului este decisivă.")
            : string.Format(Localize("Loaded {0} challenge(s); this difficulty shows {1}.",
                "S-au încărcat {0} provocări; această dificultate afișează {1}."), count, visibleProblems.Count);
        SetResult(status, InfoColor);
    }

    private void HandleSubmissionResponse(string requestId, string json)
    {
        if (string.IsNullOrEmpty(pendingSubmissionRequestId) ||
            !string.Equals(requestId, pendingSubmissionRequestId, StringComparison.Ordinal))
        {
            return;
        }

        pendingSubmissionRequestId = string.Empty;
        SetRunInteractable(visibleProblems.Count > 0);
        if (!MachineLearningJson.TryParseResult(json, out MachineLearningSubmissionResult result, out string parseError))
        {
            SetResult(parseError, ErrorColor);
            return;
        }

        ApplyProgress(result);
        if (result.infrastructureError)
        {
            string message = Localize(
                "The secure Python runner is temporarily unavailable. This was not graded as an incorrect attempt.",
                "Mediul securizat Python este temporar indisponibil. Aceasta nu a fost evaluată ca încercare greșită.");
            if (!string.IsNullOrWhiteSpace(result.error))
            {
                message += "\n\n" + result.error;
            }
            SetResult(message, InfrastructureColor);
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine(result.passed
            ? Localize("✓ PASSED", "✓ REUȘIT")
            : Localize("✗ KEEP ITERATING", "✗ CONTINUĂ SĂ ÎNCERCI"));
        builder.Append(Localize("Score", "Scor")).Append(": ").Append(result.score.ToString("0.###"));
        if (!string.IsNullOrWhiteSpace(result.metricName))
        {
            builder.Append("   •   ").Append(result.metricName).Append(": ")
                .Append(result.metricValue.ToString("0.###")).Append(" / ")
                .Append(result.threshold.ToString("0.###"));
        }

        builder.Append("\n").Append(Localize("Attempts", "Încercări")).Append(": ").Append(result.attemptCount)
            .Append("   •   ").Append(Localize("Best", "Cel mai bun")).Append(": ").Append(result.bestScore.ToString("0.###"));
        if (result.rewardGranted)
        {
            builder.Append("   •   +").Append(result.rewardPoints).Append(" pts");
        }

        if (!string.IsNullOrWhiteSpace(result.feedback))
        {
            builder.Append("\n\n").Append(result.feedback.Trim());
        }
        if (!string.IsNullOrWhiteSpace(result.stdout))
        {
            builder.Append("\n\nstdout:\n").Append(LimitVisibleOutput(result.stdout));
        }
        if (!string.IsNullOrWhiteSpace(result.error))
        {
            builder.Append("\n\nerror:\n").Append(LimitVisibleOutput(result.error));
        }

        SetResult(builder.ToString(), result.passed ? SuccessColor : ErrorColor);
        ShowCurrentProblem(false);
    }

    private void ApplyProgress(MachineLearningSubmissionResult result)
    {
        if (catalog == null || catalog.problems == null || string.IsNullOrWhiteSpace(result.problemSlug))
        {
            return;
        }

        for (int i = 0; i < catalog.problems.Length; i++)
        {
            MachineLearningProblem problem = catalog.problems[i];
            if (problem == null || !string.Equals(problem.slug, result.problemSlug, StringComparison.Ordinal))
            {
                continue;
            }

            problem.attemptCount = result.attemptCount;
            problem.bestScore = Mathf.Max(problem.bestScore, result.bestScore);
            problem.completed = problem.completed || result.completed || result.passed;
            break;
        }
    }

    private void RebuildVisibleProblems(string selectedSlug)
    {
        SaveCurrentDraft();
        visibleProblems.Clear();
        if (catalog != null && catalog.problems != null)
        {
            string targetDifficulty = selectedDifficulty.ToString();
            for (int i = 0; i < catalog.problems.Length; i++)
            {
                MachineLearningProblem problem = catalog.problems[i];
                if (problem != null && string.Equals(problem.difficulty, targetDifficulty, StringComparison.OrdinalIgnoreCase))
                {
                    visibleProblems.Add(problem);
                }
            }
        }

        selectedProblemIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedSlug))
        {
            int matchingIndex = visibleProblems.FindIndex(problem =>
                string.Equals(problem.slug, selectedSlug, StringComparison.Ordinal));
            if (matchingIndex >= 0)
            {
                selectedProblemIndex = matchingIndex;
            }
        }

        ShowCurrentProblem(true);
    }

    private MachineLearningProblem CurrentProblem =>
        visibleProblems.Count == 0 || selectedProblemIndex < 0 || selectedProblemIndex >= visibleProblems.Count
            ? null
            : visibleProblems[selectedProblemIndex];

    private void ShowCurrentProblem(bool updateEditor)
    {
        MachineLearningProblem problem = CurrentProblem;
        int completedCount = visibleProblems.Count(item => item.completed);
        difficultyText.text = DifficultyLabel(selectedDifficulty) + "   •   " + completedCount + "/" + visibleProblems.Count + " " +
                              Localize("completed", "finalizate");

        bool hasProblem = problem != null;
        previousButton.interactable = hasProblem && selectedProblemIndex > 0;
        nextButton.interactable = hasProblem && selectedProblemIndex + 1 < visibleProblems.Count;
        resetButton.interactable = hasProblem;
        hintButton.interactable = hasProblem;
        SetRunInteractable(hasProblem && string.IsNullOrEmpty(pendingSubmissionRequestId));

        if (!hasProblem)
        {
            problemTitleText.text = Localize("No challenges loaded yet", "Nu există încă provocări încărcate");
            problemBodyText.text = Localize("The catalog comes from the Java learning server.",
                "Catalogul provine de pe serverul Java de învățare.");
            datasetText.text = string.Empty;
            if (updateEditor)
            {
                sourceEditor.SetTextWithoutNotify(string.Empty);
            }
            return;
        }

        string completionBadge = problem.completed ? "  ✓" : string.Empty;
        problemTitleText.text = (selectedProblemIndex + 1) + "/" + visibleProblems.Count + "  " +
                                problem.LocalizedTitle + completionBadge + "   •   +" + problem.rewardPoints + " pts";
        string concepts = problem.concepts.Length == 0 ? "—" : string.Join(", ", problem.concepts);
        problemBodyText.text = problem.LocalizedDescription + "\n\n" + Localize("Concepts", "Concepte") + ": " + concepts +
                               "\n" + Localize("Attempts", "Încercări") + ": " + problem.attemptCount +
                               "   •   " + Localize("Best", "Cel mai bun") + ": " + problem.bestScore.ToString("0.###");
        string columns = problem.datasetColumns.Length == 0 ? "—" : string.Join(", ", problem.datasetColumns);
        datasetText.text = Localize("Dataset columns", "Coloanele setului de date") + ": " + columns + "\n\n" +
                           (problem.datasetPreview ?? string.Empty);

        if (updateEditor)
        {
            string source = sourceDrafts.TryGetValue(problem.slug, out string saved)
                ? saved
                : problem.starterCode ?? string.Empty;
            sourceEditor.SetTextWithoutNotify(source);
        }
    }

    private void SelectRelativeProblem(int offset)
    {
        if (visibleProblems.Count == 0)
        {
            return;
        }

        SaveCurrentDraft();
        selectedProblemIndex = Mathf.Clamp(selectedProblemIndex + offset, 0, visibleProblems.Count - 1);
        SetResult(string.Empty, InfoColor);
        ShowCurrentProblem(true);
    }

    private void ResetCurrentSource()
    {
        MachineLearningProblem problem = CurrentProblem;
        if (problem == null)
        {
            return;
        }

        string starter = problem.starterCode ?? string.Empty;
        sourceDrafts[problem.slug] = starter;
        sourceEditor.SetTextWithoutNotify(starter);
        SetResult(Localize("Starter code restored.", "Codul inițial a fost restaurat."), InfoColor);
    }

    private void ShowHint()
    {
        MachineLearningProblem problem = CurrentProblem;
        if (problem == null)
        {
            return;
        }

        string hint = problem.LocalizedHint;
        SetResult(string.IsNullOrWhiteSpace(hint)
            ? Localize("No hint is available for this challenge yet.", "Nu există încă un indiciu pentru această provocare.")
            : Localize("AI tutor hint (not grading):\n", "Indiciu de la tutorele AI (nu e evaluare):\n") + hint,
            HintColor);
    }

    private void SaveCurrentDraft()
    {
        MachineLearningProblem problem = CurrentProblem;
        if (problem != null && sourceEditor != null)
        {
            sourceDrafts[problem.slug] = sourceEditor.text ?? string.Empty;
        }
    }

    private void BuildUi()
    {
        if (canvasRoot != null)
        {
            return;
        }

        canvasRoot = new GameObject("MachineLearningChallengeCanvas");
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("MachineLearningEventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject backdrop = CreatePanel(canvasRoot.transform, "Backdrop", Vector2.zero, Vector2.one,
            new Color(0.015f, 0.025f, 0.065f, 0.985f));
        GameObject header = CreatePanel(backdrop.transform, "Header", new Vector2(0f, 0.89f), Vector2.one,
            new Color(0.18f, 0.055f, 0.34f, 1f));
        headerText = CreateText(header.transform, "HeaderText", string.Empty, 34, Color.white,
            new Vector2(0.02f, 0.16f), new Vector2(0.62f, 0.92f), TextAnchor.MiddleLeft, FontStyle.Bold);
        difficultyText = CreateText(header.transform, "DifficultyText", string.Empty, 20, new Color(0.86f, 0.78f, 1f),
            new Vector2(0.60f, 0.16f), new Vector2(0.98f, 0.92f), TextAnchor.MiddleRight, FontStyle.Bold);

        GameObject brief = CreatePanel(backdrop.transform, "ProblemBrief", new Vector2(0.015f, 0.16f), new Vector2(0.475f, 0.875f),
            new Color(0.045f, 0.07f, 0.13f, 0.98f));
        problemTitleText = CreateText(brief.transform, "ProblemTitle", string.Empty, 23, new Color(0.72f, 0.94f, 1f),
            new Vector2(0.035f, 0.86f), new Vector2(0.965f, 0.98f), TextAnchor.MiddleLeft, FontStyle.Bold);
        problemBodyText = CreateText(brief.transform, "ProblemBody", string.Empty, 18, new Color(0.88f, 0.91f, 0.98f),
            new Vector2(0.035f, 0.47f), new Vector2(0.965f, 0.85f), TextAnchor.UpperLeft, FontStyle.Normal);
        datasetText = CreateText(brief.transform, "Dataset", string.Empty, 16, new Color(0.62f, 0.93f, 0.78f),
            new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.45f), TextAnchor.UpperLeft, FontStyle.Normal);

        GameObject workbench = CreatePanel(backdrop.transform, "Workbench", new Vector2(0.485f, 0.16f), new Vector2(0.985f, 0.875f),
            new Color(0.025f, 0.045f, 0.09f, 0.99f));
        CreateText(workbench.transform, "EditorLabel", "PYTHON solve(train_path, test_path)", 18, new Color(0.72f, 0.66f, 1f),
            new Vector2(0.025f, 0.94f), new Vector2(0.975f, 0.995f), TextAnchor.MiddleLeft, FontStyle.Bold);
        sourceEditor = CreateSourceEditor(workbench.transform, new Vector2(0.025f, 0.28f), new Vector2(0.975f, 0.935f));
        resultText = CreateText(workbench.transform, "Result", string.Empty, 15, InfoColor,
            new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.265f), TextAnchor.UpperLeft, FontStyle.Normal);

        closeButton = CreateButton(backdrop.transform, "CloseButton", string.Empty,
            new Vector2(0.015f, 0.045f), new Vector2(0.10f, 0.125f), new Color(0.52f, 0.12f, 0.18f));
        previousButton = CreateButton(backdrop.transform, "PreviousButton", string.Empty,
            new Vector2(0.115f, 0.045f), new Vector2(0.205f, 0.125f), new Color(0.16f, 0.25f, 0.42f));
        nextButton = CreateButton(backdrop.transform, "NextButton", string.Empty,
            new Vector2(0.215f, 0.045f), new Vector2(0.305f, 0.125f), new Color(0.16f, 0.25f, 0.42f));
        resetButton = CreateButton(backdrop.transform, "ResetButton", string.Empty,
            new Vector2(0.32f, 0.045f), new Vector2(0.42f, 0.125f), new Color(0.28f, 0.28f, 0.38f));
        hintButton = CreateButton(backdrop.transform, "HintButton", string.Empty,
            new Vector2(0.435f, 0.045f), new Vector2(0.565f, 0.125f), new Color(0.52f, 0.29f, 0.08f));
        runButton = CreateButton(backdrop.transform, "RunButton", string.Empty,
            new Vector2(0.745f, 0.035f), new Vector2(0.985f, 0.135f), new Color(0.14f, 0.58f, 0.38f));

        closeButton.onClick.AddListener(Close);
        previousButton.onClick.AddListener(() => SelectRelativeProblem(-1));
        nextButton.onClick.AddListener(() => SelectRelativeProblem(1));
        resetButton.onClick.AddListener(ResetCurrentSource);
        hintButton.onClick.AddListener(ShowHint);
        runButton.onClick.AddListener(SubmitCurrentSolution);

        ApplyStaticLocalization();
        canvasRoot.SetActive(false);
    }

    private void ApplyStaticLocalization()
    {
        if (headerText == null)
        {
            return;
        }

        headerText.text = Localize("AI & MACHINE LEARNING ISLAND", "INSULA AI ȘI ÎNVĂȚARE AUTOMATĂ");
        SetButtonLabel(closeButton, Localize("CLOSE", "ÎNCHIDE"));
        SetButtonLabel(previousButton, Localize("◀ PREVIOUS", "◀ ANTERIOR"));
        SetButtonLabel(nextButton, Localize("NEXT ▶", "URMĂTOR ▶"));
        SetButtonLabel(resetButton, Localize("RESET", "RESETEAZĂ"));
        SetButtonLabel(hintButton, Localize("HINT / AI TUTOR", "INDICIU / TUTORE AI"));
        SetButtonLabel(runButton, Localize("▶ RUN ON SERVER", "▶ RULEAZĂ PE SERVER"));
        if (IsOpen)
        {
            ShowCurrentProblem(false);
        }
    }

    private void OnLanguageChanged(MentoraLanguage language)
    {
        ApplyStaticLocalization();
        RefreshWorldLabels();
    }

    private void RefreshWorldLabels()
    {
        TextMesh[] labels = GetComponentsInChildren<TextMesh>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMesh label = labels[i];
            string lower = label.gameObject.name.ToLowerInvariant();
            if (lower.Contains("islandtitle"))
            {
                label.text = Localize("AI & MACHINE LEARNING", "AI ȘI ÎNVĂȚARE AUTOMATĂ");
            }
            else if (lower.Contains("easylabel"))
            {
                label.text = Localize("EASY • DATA", "UȘOR • DATE");
            }
            else if (lower.Contains("mediumlabel"))
            {
                label.text = Localize("MEDIUM • MODELS", "MEDIU • MODELE");
            }
            else if (lower.Contains("hardlabel"))
            {
                label.text = Localize("HARD • LLM FOUNDATIONS", "GREU • BAZELE LLM");
            }
        }
    }

    private void RestorePlayerControl()
    {
        if (!PauseMenuManager.IsGamePaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetPlayerMovement(true);
        }
    }

    private void HideTouchHud()
    {
        if (touchHudSuppressed)
        {
            return;
        }

        touchHudSuppressed = true;
        MobileTouchHud.SetExternalOverlaySuppressed(true);

        MobileTouchHud hud = FindObjectOfType<MobileTouchHud>(true);
        if (hud != null)
        {
            hiddenTouchHudObject = hud.gameObject;
            hiddenTouchHudWasActive = hiddenTouchHudObject.activeSelf;
            hiddenTouchCanvas = hud.GetComponent<Canvas>();
            hiddenTouchCanvasWasEnabled = hiddenTouchCanvas != null && hiddenTouchCanvas.enabled;
            if (hiddenTouchCanvas != null)
            {
                hiddenTouchCanvas.enabled = false;
            }
            hiddenTouchHudObject.SetActive(false);
        }
    }

    private void RestoreTouchHud()
    {
        if (hiddenTouchHudObject != null)
        {
            hiddenTouchHudObject.SetActive(hiddenTouchHudWasActive);
        }
        if (hiddenTouchCanvas != null)
        {
            hiddenTouchCanvas.enabled = hiddenTouchCanvasWasEnabled;
        }
        if (touchHudSuppressed)
        {
            MobileTouchHud.SetExternalOverlaySuppressed(false);
        }

        touchHudSuppressed = false;
        hiddenTouchHudObject = null;
        hiddenTouchCanvas = null;
        hiddenTouchHudWasActive = false;
        hiddenTouchCanvasWasEnabled = false;
    }

    private void MaintainTouchHudSuppression()
    {
        if (hiddenTouchHudObject == null)
        {
            MobileTouchHud hud = FindObjectOfType<MobileTouchHud>(true);
            if (hud != null)
            {
                hiddenTouchHudObject = hud.gameObject;
                hiddenTouchHudWasActive = hiddenTouchHudObject.activeSelf || Input.touchSupported;
                hiddenTouchCanvas = hud.GetComponent<Canvas>();
                hiddenTouchCanvasWasEnabled = hiddenTouchCanvas != null && hiddenTouchCanvas.enabled;
            }
        }

        if (hiddenTouchCanvas != null && hiddenTouchCanvas.enabled)
        {
            hiddenTouchCanvas.enabled = false;
        }
        if (hiddenTouchHudObject != null && hiddenTouchHudObject.activeSelf)
        {
            hiddenTouchHudObject.SetActive(false);
        }
    }

    private static void SetPlayerMovement(bool enabled)
    {
        FirstPersonControllerSimple fps = FindObjectOfType<FirstPersonControllerSimple>();
        if (fps != null)
        {
            fps.enabled = enabled;
        }

        BeanController bean = FindObjectOfType<BeanController>();
        if (bean != null)
        {
            bean.enabled = enabled;
        }
    }

    private void SetRunInteractable(bool interactable)
    {
        if (runButton != null)
        {
            runButton.interactable = interactable;
        }
    }

    private void SetResult(string message, Color color)
    {
        if (resultText == null)
        {
            return;
        }

        resultText.text = LimitVisibleOutput(message ?? string.Empty);
        resultText.color = color;
    }

    private static string LimitVisibleOutput(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaximumVisibleOutputCharacters)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, MaximumVisibleOutputCharacters) + "\n… output truncated in the client …";
    }

    private static string DifficultyLabel(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Medium:
                return Localize("MEDIUM", "MEDIU");
            case Difficulty.Hard:
                return Localize("HARD", "GREU");
            default:
                return Localize("EASY", "UȘOR");
        }
    }

    private static string Localize(string english, string romanian)
    {
        return MentoraLocalization.IsRomanian ? romanian : english;
    }

    private static readonly Color InfoColor = new Color(0.68f, 0.83f, 1f);
    private static readonly Color SuccessColor = new Color(0.38f, 1f, 0.62f);
    private static readonly Color ErrorColor = new Color(1f, 0.49f, 0.54f);
    private static readonly Color InfrastructureColor = new Color(1f, 0.74f, 0.27f);
    private static readonly Color HintColor = new Color(1f, 0.86f, 0.44f);

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string value, int fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin,
        Vector2 anchorMax, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.disabledColor = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.65f);
        button.colors = colors;
        CreateText(buttonObject.transform, "Label", label, 17, Color.white, Vector2.zero, Vector2.one,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        return button;
    }

    private static InputField CreateSourceEditor(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject editorObject = new GameObject("SourceEditor", typeof(RectTransform), typeof(Image), typeof(InputField));
        editorObject.transform.SetParent(parent, false);
        RectTransform rect = editorObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image background = editorObject.GetComponent<Image>();
        background.color = new Color(0.012f, 0.018f, 0.04f, 1f);

        Text sourceText = CreateText(editorObject.transform, "Text", string.Empty, 17, new Color(0.82f, 0.96f, 0.9f),
            Vector2.zero, Vector2.one, TextAnchor.UpperLeft, FontStyle.Normal);
        RectTransform sourceRect = sourceText.rectTransform;
        sourceRect.offsetMin = new Vector2(14f, 12f);
        sourceRect.offsetMax = new Vector2(-14f, -12f);
        sourceText.supportRichText = false;

        Text placeholder = CreateText(editorObject.transform, "Placeholder", "# Python solution", 17,
            new Color(0.42f, 0.48f, 0.58f), Vector2.zero, Vector2.one, TextAnchor.UpperLeft, FontStyle.Italic);
        placeholder.rectTransform.offsetMin = new Vector2(14f, 12f);
        placeholder.rectTransform.offsetMax = new Vector2(-14f, -12f);

        InputField input = editorObject.GetComponent<InputField>();
        input.targetGraphic = background;
        input.textComponent = sourceText;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.MultiLineNewline;
        input.contentType = InputField.ContentType.Standard;
        input.characterLimit = 65536;
        return input;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null)
        {
            return;
        }

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = value;
        }
    }
}
