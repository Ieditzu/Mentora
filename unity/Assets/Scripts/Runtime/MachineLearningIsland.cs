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
    private const int MaximumAiChatLines = 18;
    private const int MaximumAiChatLineCharacters = 2400;
    private const int MaximumAiContextCharacters = 16000;

    [Header("Scene references")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float fallbackSpawnYaw = 180f;

    private readonly List<MachineLearningProblem> visibleProblems = new List<MachineLearningProblem>(3);
    private readonly Dictionary<string, string> sourceDrafts = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> aiChatLinesByProblem = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> gradingFeedbackByProblem = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> revealedHintProblems = new HashSet<string>(StringComparer.Ordinal);

    private MachineLearningProblemCatalog catalog;
    private Difficulty selectedDifficulty = Difficulty.Easy;
    private int selectedProblemIndex;
    private string pendingCatalogRequestId = string.Empty;
    private string pendingSubmissionRequestId = string.Empty;
    private float catalogRequestStartedAt;
    private float submissionRequestStartedAt;
    private float aiRequestStartedAt;
    private bool awaitingAiResponse;
    private string pendingAiProblemSlug = string.Empty;
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
    private GameObject aiChatPanel;
    private Text aiChatTitleText;
    private Text aiChatHistoryText;
    private Text aiChatStatusText;
    private InputField aiChatInput;
    private Button aiChatSendButton;
    private Button aiChatBackButton;
    private ScrollRect aiChatScrollRect;
    private RectTransform aiChatContentRect;
    private RectTransform aiChatViewportRect;
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

        if (awaitingAiResponse && now - aiRequestStartedAt >= RequestTimeoutSeconds)
        {
            string timedOutProblemSlug = pendingAiProblemSlug;
            awaitingAiResponse = false;
            pendingAiProblemSlug = string.Empty;
            SetAiChatInteractable(true);
            SetAiChatStatus(Localize("The AI tutor did not answer in time. Try again.",
                "Tutorele AI nu a răspuns la timp. Încearcă din nou."), ErrorColor);
            AppendAiChatLine(timedOutProblemSlug,
                Localize("AI: I did not answer in time.", "AI: Nu am răspuns la timp."));
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
        awaitingAiResponse = false;
        pendingAiProblemSlug = string.Empty;

        if (canvasRoot == null)
        {
            BuildUi();
        }

        canvasRoot.SetActive(true);
        SetAiChatVisible(false);
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
        awaitingAiResponse = false;
        pendingAiProblemSlug = string.Empty;
        SetAiChatVisible(false);
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
        else if (packet is AiResponsePacket aiResponse && awaitingAiResponse)
        {
            string response = aiResponse.Response;
            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleAiResponse(response));
        }
    }

    private void HandleAiResponse(string response)
    {
        if (!awaitingAiResponse)
        {
            return;
        }

        string responseProblemSlug = pendingAiProblemSlug;
        awaitingAiResponse = false;
        pendingAiProblemSlug = string.Empty;
        SetAiChatInteractable(true);
        SetAiChatStatus(string.Empty, InfoColor);
        AppendAiChatLine(responseProblemSlug, Localize("AI: ", "AI: ") + (string.IsNullOrWhiteSpace(response)
            ? Localize("No answer came back.", "Nu a venit niciun răspuns.")
            : response.Trim()));
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
            RememberGradingFeedback(result.problemSlug, message);
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

        string gradingFeedback = builder.ToString();
        RememberGradingFeedback(result.problemSlug, gradingFeedback);
        SetResult(gradingFeedback, result.passed ? SuccessColor : ErrorColor);
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
        UpdateAiTutorButtonLabel();

        if (!hasProblem)
        {
            SetAiChatVisible(false);
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

        if (aiChatPanel != null && aiChatPanel.activeSelf)
        {
            RefreshAiChatUi();
        }
    }

    private void SelectRelativeProblem(int offset)
    {
        if (visibleProblems.Count == 0)
        {
            return;
        }

        SaveCurrentDraft();
        SetAiChatVisible(false);
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

        if (revealedHintProblems.Add(problem.slug))
        {
            string hint = problem.LocalizedHint;
            string message = string.IsNullOrWhiteSpace(hint)
                ? Localize("No curated hint is available for this challenge yet.",
                    "Nu există încă un indiciu pregătit pentru această provocare.")
                : Localize("AI tutor hint (not grading):\n", "Indiciu de la tutorele AI (nu e evaluare):\n") + hint;
            message += Localize(
                "\n\nPress AI Tutor again to chat and ask a specific question.",
                "\n\nApasă din nou Tutore AI pentru a discuta și a pune o întrebare specifică.");
            SetResult(message, HintColor);
            UpdateAiTutorButtonLabel();
            return;
        }

        SetAiChatVisible(true);
    }

    private void RememberGradingFeedback(string problemSlug, string feedback)
    {
        if (!string.IsNullOrWhiteSpace(problemSlug) && !string.IsNullOrWhiteSpace(feedback))
        {
            gradingFeedbackByProblem[problemSlug] = LimitVisibleOutput(feedback.Trim());
        }
    }

    private void UpdateAiTutorButtonLabel()
    {
        MachineLearningProblem problem = CurrentProblem;
        bool hintRevealed = problem != null && revealedHintProblems.Contains(problem.slug);
        SetButtonLabel(hintButton, hintRevealed
            ? Localize("CHAT WITH AI", "DISCUTĂ CU AI")
            : Localize("HINT / AI TUTOR", "INDICIU / TUTORE AI"));
    }

    private void SetAiChatVisible(bool visible)
    {
        if (aiChatPanel == null)
        {
            return;
        }

        MachineLearningProblem problem = CurrentProblem;
        if (visible && problem == null)
        {
            return;
        }

        aiChatPanel.SetActive(visible);
        if (!visible)
        {
            if (EventSystem.current != null && aiChatInput != null &&
                EventSystem.current.currentSelectedGameObject == aiChatInput.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            return;
        }

        SaveCurrentDraft();
        List<string> chatLines = GetAiChatLines(problem.slug);
        if (chatLines.Count == 0)
        {
            AppendAiChatLine(problem.slug, Localize(
                "AI: Ask me a specific question about the dataset, model, metric, or your Python code.",
                "AI: Pune-mi o întrebare specifică despre date, model, metrică sau codul tău Python."));
        }

        RefreshAiChatUi();
        SetAiChatInteractable(!awaitingAiResponse);
        SetAiChatStatus(awaitingAiResponse
                ? Localize("The AI tutor is thinking…", "Tutorele AI se gândește…")
                : string.Empty,
            InfoColor);
        aiChatInput.SetTextWithoutNotify(string.Empty);
        aiChatInput.ActivateInputField();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(aiChatInput.gameObject);
        }
    }

    private void SetAiChatInteractable(bool interactable)
    {
        if (aiChatInput != null)
        {
            aiChatInput.interactable = interactable;
        }
        if (aiChatSendButton != null)
        {
            aiChatSendButton.interactable = interactable;
        }
    }

    private void SetAiChatStatus(string message, Color color)
    {
        if (aiChatStatusText == null)
        {
            return;
        }

        aiChatStatusText.text = message ?? string.Empty;
        aiChatStatusText.color = color;
    }

    private List<string> GetAiChatLines(string problemSlug)
    {
        if (string.IsNullOrWhiteSpace(problemSlug))
        {
            return new List<string>();
        }

        if (!aiChatLinesByProblem.TryGetValue(problemSlug, out List<string> lines))
        {
            lines = new List<string>();
            aiChatLinesByProblem[problemSlug] = lines;
        }

        return lines;
    }

    private void AppendAiChatLine(string line)
    {
        MachineLearningProblem problem = CurrentProblem;
        AppendAiChatLine(problem != null ? problem.slug : string.Empty, line);
    }

    private void AppendAiChatLine(string problemSlug, string line)
    {
        if (string.IsNullOrWhiteSpace(problemSlug) || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string normalized = line.Trim();
        if (normalized.Length > MaximumAiChatLineCharacters)
        {
            normalized = normalized.Substring(0, MaximumAiChatLineCharacters) + "\n…";
        }

        List<string> lines = GetAiChatLines(problemSlug);
        lines.Add(normalized);
        while (lines.Count > MaximumAiChatLines)
        {
            lines.RemoveAt(0);
        }

        MachineLearningProblem current = CurrentProblem;
        if (current != null && string.Equals(current.slug, problemSlug, StringComparison.Ordinal))
        {
            RefreshAiChatUi();
        }
    }

    private void RefreshAiChatUi()
    {
        MachineLearningProblem problem = CurrentProblem;
        if (problem == null)
        {
            if (aiChatTitleText != null)
            {
                aiChatTitleText.text = Localize("AI TUTOR", "TUTORE AI");
            }
            if (aiChatHistoryText != null)
            {
                aiChatHistoryText.text = string.Empty;
            }
            return;
        }

        if (aiChatTitleText != null)
        {
            aiChatTitleText.text = Localize("AI TUTOR • ", "TUTORE AI • ") + problem.LocalizedTitle;
        }

        if (aiChatHistoryText == null)
        {
            return;
        }

        List<string> lines = GetAiChatLines(problem.slug);
        var builder = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("\n\n");
            }
            builder.Append(lines[i]);
        }
        aiChatHistoryText.text = builder.ToString();

        Canvas.ForceUpdateCanvases();
        if (aiChatContentRect != null)
        {
            float viewportHeight = aiChatViewportRect != null ? aiChatViewportRect.rect.height : 0f;
            float preferredHeight = Mathf.Max(viewportHeight, aiChatHistoryText.preferredHeight + 28f);
            aiChatContentRect.sizeDelta = new Vector2(0f, preferredHeight);
        }
        Canvas.ForceUpdateCanvases();
        if (aiChatScrollRect != null)
        {
            aiChatScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void HandleAiChatInputEndEdit(string _)
    {
        if (aiChatPanel == null || !aiChatPanel.activeSelf || aiChatInput == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return;
        }

        OnAiChatSendClicked();
    }

    private void OnAiChatSendClicked()
    {
        if (aiChatInput == null || CurrentProblem == null)
        {
            return;
        }
        if (awaitingAiResponse)
        {
            SetAiChatStatus(Localize("Wait for the current answer first.",
                "Așteaptă mai întâi răspunsul curent."), HintColor);
            return;
        }

        string message = (aiChatInput.text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetAiChatStatus(Localize("Type a specific question first.",
                "Scrie mai întâi o întrebare specifică."), HintColor);
            aiChatInput.ActivateInputField();
            return;
        }

        aiChatInput.SetTextWithoutNotify(string.Empty);
        _ = SendAiQuestionAsync(message);
    }

    private async Task SendAiQuestionAsync(string message)
    {
        MachineLearningProblem problem = CurrentProblem;
        if (problem == null || awaitingAiResponse)
        {
            return;
        }
        if (PauseMenuManager.GetLoggedInChildId() <= 0)
        {
            SetAiChatStatus(Localize("Log in with QR before using the AI tutor.",
                "Autentifică-te prin QR înainte de a folosi tutorele AI."), ErrorColor);
            SetAiChatInteractable(true);
            return;
        }

        SaveCurrentDraft();
        string problemSlug = problem.slug;
        string source = sourceDrafts.TryGetValue(problemSlug, out string savedSource)
            ? savedSource
            : sourceEditor != null ? sourceEditor.text ?? string.Empty : string.Empty;
        AppendAiChatLine(problemSlug, Localize("You: ", "Tu: ") + message);
        SetAiChatInteractable(false);
        SetAiChatStatus(Localize("Connecting to the AI tutor…", "Conectare la tutorele AI…"), InfoColor);

        try
        {
            TrySubscribeToNetwork();
            GameClient client = GameClient.Instance;
            if (client == null)
            {
                throw new InvalidOperationException(Localize("The game client is unavailable.",
                    "Clientul jocului nu este disponibil."));
            }

            if (!client.IsConnected)
            {
                await client.Connect();
            }
            if (!client.IsConnected)
            {
                throw new InvalidOperationException(Localize("The AI server is offline.",
                    "Serverul AI este offline."));
            }
            if (!IsOpen)
            {
                SetAiChatInteractable(true);
                return;
            }

            pendingAiProblemSlug = problemSlug;
            awaitingAiResponse = true;
            aiRequestStartedAt = Time.unscaledTime;
            SetAiChatStatus(Localize("The AI tutor is thinking…", "Tutorele AI se gândește…"), InfoColor);
            await client.SendPacket(new AskAiPacket(BuildAiQuestion(message), BuildAiContext(problem, source)));
        }
        catch (Exception exception)
        {
            if (string.Equals(pendingAiProblemSlug, problemSlug, StringComparison.Ordinal))
            {
                awaitingAiResponse = false;
                pendingAiProblemSlug = string.Empty;
            }
            SetAiChatInteractable(true);
            SetAiChatStatus(Localize("Could not contact the AI tutor.",
                "Tutorele AI nu a putut fi contactat."), ErrorColor);
            AppendAiChatLine(problemSlug, Localize("AI: Connection failed: ", "AI: Conexiunea a eșuat: ") + exception.Message);
        }
    }

    private static string BuildAiQuestion(string message)
    {
        string language = MentoraLocalization.IsRomanian ? "Romanian" : "English";
        return "You are Mentora's machine-learning tutor for the active exercise only. " +
               "Answer the player's exact question in " + language + ". Give short, concrete, code-aware guidance and a useful next step. " +
               "Prefer explanation, debugging, and small snippets; do not provide a complete final submission. " +
               "Never claim that hidden tests pass or replace the authoritative server grader. " +
               "Player question: " + LimitAiContextPart(message, 2200);
    }

    private string BuildAiContext(MachineLearningProblem problem, string source)
    {
        var builder = new StringBuilder();
        builder.AppendLine("machine_learning_tutor_chat");
        builder.AppendLine("The player is solving one Python machine-learning exercise on Mentora's AI/ML island.");
        builder.AppendLine("Server grading and hidden datasets are authoritative; the tutor must not invent a grade or passing result.");
        builder.Append("Language: ").AppendLine(MentoraLocalization.IsRomanian ? "Romanian" : "English");
        builder.Append("Slug: ").AppendLine(problem.slug ?? string.Empty);
        builder.Append("Difficulty: ").AppendLine(problem.difficulty ?? selectedDifficulty.ToString());
        builder.Append("Title: ").AppendLine(problem.LocalizedTitle);
        builder.AppendLine("Problem:");
        builder.AppendLine(LimitAiContextPart(problem.LocalizedDescription, 2200));
        builder.Append("Concepts: ").AppendLine(problem.concepts != null && problem.concepts.Length > 0
            ? string.Join(", ", problem.concepts)
            : "none listed");
        builder.Append("Dataset columns: ").AppendLine(problem.datasetColumns != null && problem.datasetColumns.Length > 0
            ? string.Join(", ", problem.datasetColumns)
            : "none listed");
        builder.AppendLine("Most recent authoritative grading feedback:");
        builder.AppendLine(gradingFeedbackByProblem.TryGetValue(problem.slug, out string gradingFeedback)
            ? LimitAiContextPart(gradingFeedback, 2200)
            : "No submission has been graded for this problem in the current session.");
        builder.AppendLine("Recent conversation:");
        List<string> lines = GetAiChatLines(problem.slug);
        int firstLine = Mathf.Max(0, lines.Count - 6);
        for (int i = firstLine; i < lines.Count; i++)
        {
            builder.AppendLine(LimitAiContextPart(lines[i], 700));
        }
        builder.AppendLine("Dataset preview:");
        builder.AppendLine(LimitAiContextPart(problem.datasetPreview, 1800));
        builder.AppendLine("Current Python source:");
        builder.AppendLine(LimitAiContextPart(source, 4200));

        string context = builder.ToString();
        return context.Length <= MaximumAiContextCharacters
            ? context
            : context.Substring(0, MaximumAiContextCharacters) + "\n… context truncated …";
    }

    private static string LimitAiContextPart(string value, int maximumCharacters)
    {
        string normalized = value ?? string.Empty;
        return normalized.Length <= maximumCharacters
            ? normalized
            : normalized.Substring(0, maximumCharacters) + "\n… truncated …";
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

        aiChatPanel = CreatePanel(backdrop.transform, "AiTutorChat", new Vector2(0.14f, 0.11f), new Vector2(0.86f, 0.89f),
            new Color(0.025f, 0.035f, 0.085f, 0.995f));
        aiChatTitleText = CreateText(aiChatPanel.transform, "Title", string.Empty, 27, new Color(0.76f, 0.91f, 1f),
            new Vector2(0.04f, 0.86f), new Vector2(0.75f, 0.97f), TextAnchor.MiddleLeft, FontStyle.Bold);
        aiChatBackButton = CreateButton(aiChatPanel.transform, "BackButton", string.Empty,
            new Vector2(0.78f, 0.875f), new Vector2(0.96f, 0.96f), new Color(0.24f, 0.27f, 0.42f));

        GameObject chatViewport = new GameObject("HistoryViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        chatViewport.transform.SetParent(aiChatPanel.transform, false);
        aiChatViewportRect = chatViewport.GetComponent<RectTransform>();
        aiChatViewportRect.anchorMin = new Vector2(0.04f, 0.23f);
        aiChatViewportRect.anchorMax = new Vector2(0.96f, 0.84f);
        aiChatViewportRect.offsetMin = Vector2.zero;
        aiChatViewportRect.offsetMax = Vector2.zero;
        chatViewport.GetComponent<Image>().color = new Color(0.008f, 0.014f, 0.038f, 0.96f);
        chatViewport.GetComponent<Mask>().showMaskGraphic = true;

        GameObject chatContent = new GameObject("HistoryContent", typeof(RectTransform));
        chatContent.transform.SetParent(chatViewport.transform, false);
        aiChatContentRect = chatContent.GetComponent<RectTransform>();
        aiChatContentRect.anchorMin = new Vector2(0f, 1f);
        aiChatContentRect.anchorMax = new Vector2(1f, 1f);
        aiChatContentRect.pivot = new Vector2(0.5f, 1f);
        aiChatContentRect.anchoredPosition = Vector2.zero;
        aiChatContentRect.sizeDelta = new Vector2(0f, 100f);
        aiChatHistoryText = CreateText(chatContent.transform, "History", string.Empty, 17, new Color(0.89f, 0.93f, 1f),
            Vector2.zero, Vector2.one, TextAnchor.UpperLeft, FontStyle.Normal);
        aiChatHistoryText.rectTransform.offsetMin = new Vector2(16f, 12f);
        aiChatHistoryText.rectTransform.offsetMax = new Vector2(-16f, -12f);
        aiChatHistoryText.supportRichText = false;

        aiChatScrollRect = aiChatPanel.AddComponent<ScrollRect>();
        aiChatScrollRect.content = aiChatContentRect;
        aiChatScrollRect.viewport = aiChatViewportRect;
        aiChatScrollRect.horizontal = false;
        aiChatScrollRect.vertical = true;
        aiChatScrollRect.movementType = ScrollRect.MovementType.Clamped;
        aiChatScrollRect.scrollSensitivity = 35f;

        aiChatStatusText = CreateText(aiChatPanel.transform, "Status", string.Empty, 14, InfoColor,
            new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.225f), TextAnchor.MiddleLeft, FontStyle.Italic);
        aiChatInput = CreateSingleLineInput(aiChatPanel.transform, new Vector2(0.04f, 0.055f), new Vector2(0.76f, 0.16f));
        aiChatSendButton = CreateButton(aiChatPanel.transform, "SendButton", string.Empty,
            new Vector2(0.78f, 0.055f), new Vector2(0.96f, 0.16f), new Color(0.18f, 0.48f, 0.68f));

        closeButton.onClick.AddListener(Close);
        previousButton.onClick.AddListener(() => SelectRelativeProblem(-1));
        nextButton.onClick.AddListener(() => SelectRelativeProblem(1));
        resetButton.onClick.AddListener(ResetCurrentSource);
        hintButton.onClick.AddListener(ShowHint);
        runButton.onClick.AddListener(SubmitCurrentSolution);
        aiChatBackButton.onClick.AddListener(() => SetAiChatVisible(false));
        aiChatSendButton.onClick.AddListener(OnAiChatSendClicked);
        aiChatInput.onEndEdit.AddListener(HandleAiChatInputEndEdit);

        ApplyStaticLocalization();
        aiChatPanel.SetActive(false);
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
        UpdateAiTutorButtonLabel();
        SetButtonLabel(runButton, Localize("▶ RUN ON SERVER", "▶ RULEAZĂ PE SERVER"));
        SetButtonLabel(aiChatBackButton, Localize("BACK", "ÎNAPOI"));
        SetButtonLabel(aiChatSendButton, Localize("SEND", "TRIMITE"));
        if (aiChatInput != null && aiChatInput.placeholder is Text chatPlaceholder)
        {
            chatPlaceholder.text = Localize("Ask about the model, data, metric, or your code…",
                "Întreabă despre model, date, metrică sau codul tău…");
        }
        RefreshAiChatUi();
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

    private static InputField CreateSingleLineInput(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject inputObject = new GameObject("QuestionInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.012f, 0.018f, 0.045f, 1f);

        Text inputText = CreateText(inputObject.transform, "Text", string.Empty, 17, new Color(0.88f, 0.96f, 1f),
            Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, FontStyle.Normal);
        inputText.rectTransform.offsetMin = new Vector2(14f, 4f);
        inputText.rectTransform.offsetMax = new Vector2(-14f, -4f);
        inputText.supportRichText = false;

        Text placeholder = CreateText(inputObject.transform, "Placeholder", string.Empty, 16,
            new Color(0.42f, 0.5f, 0.62f), Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, FontStyle.Italic);
        placeholder.rectTransform.offsetMin = new Vector2(14f, 4f);
        placeholder.rectTransform.offsetMax = new Vector2(-14f, -4f);

        InputField input = inputObject.GetComponent<InputField>();
        input.targetGraphic = background;
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;
        input.contentType = InputField.ContentType.Standard;
        input.characterLimit = 2000;
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
