using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Mentora.Network;

[RequireComponent(typeof(Collider))]
public class CppQuestionPadCinematic : MonoBehaviour
{
    private enum QuizLanguage
    {
        Romanian = 0,
        English = 1
    }

    private struct QuizQuestion
    {
        public string PromptRo;
        public string PromptEn;
        public string[] OptionsRo;
        public string[] OptionsEn;
        public int CorrectIndex;
        public string CorrectTextRo;
        public string CorrectTextEn;
        public string HintRo;
        public string HintEn;

        public QuizQuestion(
            string promptRo,
            string promptEn,
            string aRo,
            string bRo,
            string cRo,
            string aEn,
            string bEn,
            string cEn,
            int correctIndex,
            string correctTextRo,
            string correctTextEn,
            string hintRo,
            string hintEn)
        {
            PromptRo = promptRo;
            PromptEn = promptEn;
            OptionsRo = new[] { aRo, bRo, cRo };
            OptionsEn = new[] { aEn, bEn, cEn };
            CorrectIndex = correctIndex;
            CorrectTextRo = correctTextRo;
            CorrectTextEn = correctTextEn;
            HintRo = hintRo;
            HintEn = hintEn;
        }
    }

    [Header("Portal")]
    [SerializeField] private float portalForwardDistance = 1.4f;
    [SerializeField] private float portalSideDistance = 2.8f;
    [SerializeField] private float portalHeightOffset = 2.35f;
    [SerializeField] private float portalSpawnDuration = 0.45f;
    [SerializeField] private float lookAtPortalDuration = 0.55f;
    [SerializeField] private float waitBeforeSuction = 0.5f;
    [SerializeField] private float suctionDuration = 0.24f;
    [SerializeField] private float portalCollapseDuration = 0.32f;

    [Header("Exit")]
    [SerializeField] private float reenterCooldown = 0f;
    [SerializeField] private float exitHeightOffset = 0.2f;
    [SerializeField] private float exitBackOffset = 0.15f;
    [SerializeField] private float exitMoveDuration = 0.26f;

    [Header("Screen")]
    [SerializeField] private float fadeToWhiteDuration = 0.38f;
    [SerializeField] private float menuFadeDuration = 0.22f;

    [Header("Visuals")]
    [SerializeField] private float cameraFovBoost = 10f;
    [SerializeField] private Color portalCoreColor = new Color(0.62f, 0.92f, 1f, 0.92f);
    [SerializeField] private Color portalRingColor = new Color(1f, 0.94f, 0.76f, 0.95f);
    [SerializeField] private Color pulseColor = new Color(0.82f, 0.96f, 1f, 1f);
    [SerializeField] private Color pageTint = new Color(0.98f, 0.98f, 0.96f, 1f);
    [SerializeField] private Color cardColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color accentColor = new Color(0.12f, 0.55f, 0.98f, 1f);
    [SerializeField] private Color secondaryAccentColor = new Color(0.0f, 0.71f, 0.74f, 1f);
    [SerializeField] private Color correctColor = new Color(0.18f, 0.62f, 0.32f, 1f);
    [SerializeField] private Color wrongColor = new Color(0.83f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.14f, 0.14f, 0.14f, 0.92f);
    [SerializeField] private Color optionColor = new Color(0.90f, 0.95f, 1f, 1f);

    [Header("Typography")]
    [SerializeField] private int titleSize = 42;
    [SerializeField] private int questionSize = 32;
    [SerializeField] private int bodySize = 24;
    [SerializeField] private int buttonTextSize = 26;

    private static readonly QuizQuestion[] Questions =
    {
        new QuizQuestion(
            "Ce tip de date folosesti de obicei pentru un numar intreg in C++?",
            "Which data type do you usually use for an integer in C++?",
            "A. int",
            "B. string",
            "C. bool",
            "A. int",
            "B. string",
            "C. bool",
            0,
            "Raspuns corect: A. `int` este tipul standard pentru numere intregi.",
            "Correct answer: A. `int` is the standard type for integer values.",
            "Hint: Cauta tipul numeric standard pentru valori intregi. `string` este pentru text, iar `bool` pentru adevarat/fals. Raspuns corect: A. `int`.",
            "Hint: Look for the standard numeric type for whole numbers. `string` is for text and `bool` is for true/false. Correct answer: A. `int`."),
        new QuizQuestion(
            "Ce afiseaza `std::cout << 2 + 3;` ?",
            "What does `std::cout << 2 + 3;` print?",
            "A. 23",
            "B. 5",
            "C. 6",
            "A. 23",
            "B. 5",
            "C. 6",
            1,
            "Raspuns corect: B. Expresia `2 + 3` este evaluata si rezultatul este 5.",
            "Correct answer: B. The expression `2 + 3` is evaluated and the result is 5.",
            "Hint: `cout` afiseaza rezultatul expresiei, nu lipeste cifrele una langa alta. Mai intai se calculeaza `2 + 3`, deci raspunsul corect este B. 5.",
            "Hint: `cout` prints the result of the expression, it does not glue the digits together. `2 + 3` is evaluated first, so the correct answer is B. 5."),
        new QuizQuestion(
            "Cum se termina corect majoritatea instructiunilor in C++?",
            "How do most statements end in C++?",
            "A. Cu `:`",
            "B. Cu `;`",
            "C. Cu `#`",
            "A. With `:`",
            "B. With `;`",
            "C. With `#`",
            1,
            "Raspuns corect: B. In C++, instructiunile se termina cu `;`.",
            "Correct answer: B. In C++, statements end with `;`.",
            "Hint: In C++, punctuatia importanta la final de instructiune este `;`. `:` apare in alte contexte, iar `#` este folosit la directive. Raspuns corect: B.",
            "Hint: In C++, the usual statement terminator is `;`. `:` is used in other contexts and `#` is for directives. Correct answer: B."),
        new QuizQuestion(
            "Ce cuvant cheie folosesti pentru a declara o variabila constanta?",
            "Which keyword do you use to declare a constant variable?",
            "A. fixed",
            "B. let",
            "C. const",
            "A. fixed",
            "B. let",
            "C. const",
            2,
            "Raspuns corect: C. `const` marcheaza o valoare care nu se mai modifica.",
            "Correct answer: C. `const` marks a value that should not change.",
            "Hint: In C++, cuvantul cheie standard pentru o valoare care nu se modifica este `const`. Raspuns corect: C. `const`.",
            "Hint: In C++, the standard keyword for a value that should not change is `const`. Correct answer: C. `const`."),
        new QuizQuestion(
            "Ce face operatorul `%` in C++?",
            "What does the `%` operator do in C++?",
            "A. Calculeaza restul impartirii",
            "B. Inmulteste doua numere",
            "C. Concateneaza text",
            "A. Computes the remainder",
            "B. Multiplies two numbers",
            "C. Concatenates text",
            0,
            "Raspuns corect: A. `%` intoarce restul impartirii intre numere intregi.",
            "Correct answer: A. `%` returns the remainder of an integer division.",
            "Hint: Gandeste-te la expresii de tipul `7 % 2`, unde rezultatul este `1`. Operatorul calculeaza restul impartirii. Raspuns corect: A.",
            "Hint: Think about `7 % 2`, which gives `1`. The operator returns the remainder after division. Correct answer: A.")
    };

    private static Canvas overlayCanvas;
    private static Image pulseImage;
    private static Image whiteImage;
    private static Image menuCard;
    private static Text titleText;
    private static Text counterText;
    private static Text questionText;
    private static Text feedbackText;
    private static Text languagePromptText;
    private static Text hintScreenText;
    private static Button leaveButton;
    private static Text leaveButtonText;
    private static Button hintScreenBackButton;
    private static Text hintScreenBackButtonText;
    private static Button nextButton;
    private static Text nextButtonText;
    private static Button backButton;
    private static Text backButtonText;
    private static Button hintButton;
    private static Text hintButtonText;
    private static Button aiButton;
    private static Text aiButtonText;
    private static Button retryWrongButton;
    private static Text retryWrongButtonText;
    private static Button languageRoButton;
    private static Button languageEnButton;
    private static Button[] optionButtons;
    private static Text[] optionButtonTexts;
    private static Image aiChatPanel;
    private static Text aiChatTitleText;
    private static Text aiChatHistoryText;
    private static InputField aiChatInput;
    private static Text aiChatInputText;
    private static Text aiChatPlaceholderText;
    private static Button aiChatSendButton;
    private static Text aiChatSendButtonText;
    private static Button aiChatBackButton;
    private static Text aiChatBackButtonText;

    private Collider triggerCollider;
    private bool running;
    private bool leaveRequested;
    private bool answerChosen;
    private bool answerWasCorrect;
    private int selectedAnswerIndex = -1;
    private bool nextRequested;
    private bool backRequested;
    private bool hintRequested;
    private bool aiChatRequested;
    private bool retryWrongRequested;
    private bool hintScreenBackRequested;
    private bool aiChatBackRequested;
    private bool aiChatSendRequested;
    private bool languageChosen;
    private QuizLanguage selectedLanguage = QuizLanguage.Romanian;
    private GameObject activePortal;
    private bool[] answeredCorrectly;
    private int activeQuestionIndex = -1;
    private bool awaitingAiResponse;
    private bool networkSubscribed;
    private readonly List<string> aiChatLines = new List<string>();
    private const int MaxChatLines = 14;

    private void Awake()
    {
        EnsureTriggerReady();
        EnsureOverlay();
        ResetOverlay();
    }

    private void OnEnable()
    {
        EnsureTriggerReady();
        leaveRequested = false;
        running = false;
        EnsureDispatcher();
        RegisterNetworkHandlers();
    }

    private void OnDisable()
    {
        UnregisterNetworkHandlers();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStartSequence(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryStartSequence(other);
    }

    private void TryStartSequence(Collider other)
    {
        if (running)
        {
            return;
        }

        if (!TryGetPlayer(other, out BeanController sphere, out FirstPersonControllerSimple fps))
        {
            return;
        }

        StartCoroutine(PlaySequence(sphere, fps));
    }

    private void EnsureDispatcher()
    {
        if (!UnityMainThreadDispatcher.IsInitialized)
        {
            UnityMainThreadDispatcher.Initialize();
        }
    }

    private void RegisterNetworkHandlers()
    {
        if (networkSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived += OnPacketReceived;
        networkSubscribed = true;
    }

    private void UnregisterNetworkHandlers()
    {
        if (!networkSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived -= OnPacketReceived;
        networkSubscribed = false;
    }

    private void OnPacketReceived(Packet packet)
    {
        if (packet is AiResponsePacket aiResponse)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleAiResponse(aiResponse));
        }
    }

    private void HandleAiResponse(AiResponsePacket response)
    {
        if (!awaitingAiResponse)
        {
            return;
        }

        awaitingAiResponse = false;
        AppendChatLine((selectedLanguage == QuizLanguage.Romanian ? "AI: " : "AI: ") + (response.Response ?? string.Empty));
    }

    private async Task<bool> SendPacketWithConnectAsync(Packet packet)
    {
        if (GameClient.Instance == null)
        {
            return false;
        }

        if (!GameClient.Instance.IsConnected)
        {
            await GameClient.Instance.Connect();
        }

        if (!GameClient.Instance.IsConnected)
        {
            return false;
        }

        await GameClient.Instance.SendPacket(packet);
        return true;
    }

    private IEnumerator SendPacketWithConnect(Packet packet, System.Action<bool> onDone)
    {
        Task<bool> task = SendPacketWithConnectAsync(packet);
        while (!task.IsCompleted)
        {
            yield return null;
        }

        onDone?.Invoke(task.Result);
    }

    private IEnumerator PlaySequence(BeanController sphere, FirstPersonControllerSimple fps)
    {
        running = true;
        leaveRequested = false;
        EnsureDispatcher();
        RegisterNetworkHandlers();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        SetPlayerLockState(sphere, fps, true, true);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(false);
        }

        Transform playerRoot = fps != null ? fps.transform : sphere != null ? sphere.transform : null;
        Camera activeCamera = ResolveCamera(fps);
        Transform camTransform = activeCamera != null ? activeCamera.transform : null;
        if (playerRoot == null || camTransform == null)
        {
            RestorePlayerState(sphere, fps);
            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
            }
            yield break;
        }

        Vector3 startPosition = playerRoot.position;
        Quaternion startRotation = playerRoot.rotation;
        Vector3 startCameraLocalPosition = camTransform.localPosition;
        Quaternion startCameraLocalRotation = camTransform.localRotation;
        float startFov = activeCamera.fieldOfView;

        Vector3 portalPosition = GetPortalPosition(playerRoot, startPosition);
        Quaternion portalRotation = Quaternion.LookRotation((startPosition + Vector3.up) - portalPosition, Vector3.up);

        activePortal = CreatePortal(portalPosition, portalRotation);
        yield return AnimatePortalAppear(activePortal.transform);
        yield return TurnPlayerTowardPortal(playerRoot, camTransform, activeCamera, startCameraLocalPosition, startCameraLocalRotation, startFov, portalPosition);
        yield return new WaitForSeconds(waitBeforeSuction);
        yield return PullPlayerToPortal(playerRoot, camTransform, activeCamera, startCameraLocalPosition, startCameraLocalRotation, startFov, portalPosition);

        yield return FadeImage(whiteImage, 0f, 1f, fadeToWhiteDuration, pageTint);
        whiteImage.color = new Color(pageTint.r, pageTint.g, pageTint.b, 1f);
        yield return null;
        SetPulseAlpha(0f);

        SetCursorVisible(true);
        ShowLeaveButton(true);
        yield return AnimateMenuCard(true);
        yield return ShowLanguageSelection();
        yield return RunQuiz();
        ResetOverlay();
        SetCursorVisible(false);

        Vector3 exitPosition = GetSafeExitPosition(startPosition, playerRoot);
        if (activePortal == null)
        {
            activePortal = CreatePortal(portalPosition, portalRotation);
            yield return AnimatePortalAppear(activePortal.transform);
        }

        Vector3 facing = exitPosition - portalPosition;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = playerRoot.forward;
        }

        TeleportPlayer(sphere, fps, portalPosition - facing.normalized * 0.15f, Quaternion.LookRotation(facing.normalized, Vector3.up));
        camTransform.localPosition = startCameraLocalPosition;
        camTransform.localRotation = startCameraLocalRotation;
        activeCamera.fieldOfView = startFov;

        yield return FadeImage(whiteImage, 1f, 0f, 0.2f, pageTint);
        yield return MovePlayerOut(playerRoot, exitPosition, startRotation);
        yield return CollapsePortal(activePortal);

        RestorePlayerState(sphere, fps);
        StartCoroutine(ReenableTriggerAfterDelay());
    }

    private IEnumerator RunQuiz()
    {
        answeredCorrectly = new bool[Questions.Length];
        titleText.text = selectedLanguage == QuizLanguage.Romanian ? "Quiz C++ pentru incepatori" : "C++ Starter Quiz";
        feedbackText.text = string.Empty;
        leaveButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Iesire" : "Leave";
        nextButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Inainte" : "Next";
        backButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Inapoi" : "Back";
        hintButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Indiciu" : "Hint";
        aiButtonText.text = "AI";
        retryWrongButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Refa intrebarile gresite" : "Retry wrong questions";
        nextButton.gameObject.SetActive(false);
        retryWrongButton.gameObject.SetActive(false);

        int[] allIndices = new int[Questions.Length];
        for (int i = 0; i < Questions.Length; i++)
        {
            allIndices[i] = i;
        }

        yield return RunQuizSet(allIndices, false);

        while (!leaveRequested)
        {
            yield return null;
        }
    }

    private IEnumerator RunQuizSet(int[] indices, bool retryMode)
    {
        int current = 0;
        while (current < indices.Length && !leaveRequested)
        {
            QuizStepResult step = new QuizStepResult();
            yield return ShowQuestion(indices, current, retryMode, result => step = result);

            if (leaveRequested)
            {
                yield break;
            }

            if (step.GoBack)
            {
                current = Mathf.Max(0, current - 1);
                continue;
            }

            if (step.AnsweredCorrectly)
            {
                answeredCorrectly[indices[current]] = true;
                current++;
                continue;
            }

            current++;
        }

        int correctCount = 0;
        int wrongCount = 0;
        for (int i = 0; i < Questions.Length; i++)
        {
            if (answeredCorrectly[i])
            {
                correctCount++;
            }
            else
            {
                wrongCount++;
            }
        }

        if (correctCount == Questions.Length)
        {
            PauseMenuManager.CompleteTaskByTitle("C++ Starter Quiz");
        }

        counterText.text = selectedLanguage == QuizLanguage.Romanian ? "Rezultat final" : "Final result";
        questionText.text = selectedLanguage == QuizLanguage.Romanian
            ? "Ai raspuns corect la " + correctCount + " din " + Questions.Length + ". Ai deblocat nivelul medium."
            : "You answered " + correctCount + " out of " + Questions.Length + " correctly. You unlocked the medium level.";
        feedbackText.text = wrongCount > 0
            ? (selectedLanguage == QuizLanguage.Romanian
                ? "Poti reface intrebarile gresite."
                : "You can retry the wrong questions.")
            : (selectedLanguage == QuizLanguage.Romanian
                ? "Ai rezolvat totul corect."
                : "You solved everything correctly.");
        feedbackText.color = wrongCount > 0 ? wrongColor : correctColor;
        retryWrongButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Refa intrebarile gresite" : "Retry wrong questions";

        HideOptions();
        nextButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);
        hintButton.gameObject.SetActive(false);
        aiButton.gameObject.SetActive(false);
        retryWrongButton.onClick.RemoveAllListeners();
        retryWrongButton.gameObject.SetActive(wrongCount > 0);
        if (wrongCount > 0)
        {
            retryWrongButton.onClick.AddListener(OnRetryWrongClicked);
            retryWrongRequested = false;
            while (!retryWrongRequested && !leaveRequested)
            {
                yield return null;
            }

            if (leaveRequested)
            {
                yield break;
            }

            int[] wrongIndices = new int[wrongCount];
            int write = 0;
            for (int i = 0; i < Questions.Length; i++)
            {
                if (!answeredCorrectly[i])
                {
                    wrongIndices[write++] = i;
                }
            }

            retryWrongButton.gameObject.SetActive(false);
            yield return RunQuizSet(wrongIndices, true);
        }
    }

    private struct QuizStepResult
    {
        public bool AnsweredCorrectly;
        public bool GoBack;
    }

    private IEnumerator ShowQuestion(int[] indices, int sessionPosition, bool retryMode, System.Action<QuizStepResult> onFinished)
    {
        int index = indices[sessionPosition];
        QuizQuestion question = Questions[index];
        answerChosen = false;
        answerWasCorrect = false;
        selectedAnswerIndex = -1;
        nextRequested = false;
        backRequested = false;
        hintRequested = false;
        aiChatRequested = false;
        activeQuestionIndex = index;

        counterText.text = selectedLanguage == QuizLanguage.Romanian
            ? (retryMode ? "Refacere " + (sessionPosition + 1) + " / " + indices.Length : "Intrebarea " + (sessionPosition + 1) + " / " + indices.Length)
            : (retryMode ? "Retry " + (sessionPosition + 1) + " / " + indices.Length : "Question " + (sessionPosition + 1) + " / " + indices.Length);
        questionText.text = selectedLanguage == QuizLanguage.Romanian ? question.PromptRo : question.PromptEn;
        feedbackText.text = string.Empty;
        feedbackText.color = textColor;
        nextButton.gameObject.SetActive(false);
        retryWrongButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(sessionPosition > 0);
        hintButton.gameObject.SetActive(true);
        aiButton.gameObject.SetActive(true);
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(OnBackClicked);
        hintButton.onClick.RemoveAllListeners();
        hintButton.onClick.AddListener(OnHintClicked);
        aiButton.onClick.RemoveAllListeners();
        aiButton.onClick.AddListener(OnAiChatClicked);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int optionIndex = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => SelectAnswer(optionIndex, question));
            optionButtons[i].interactable = true;
            optionButtons[i].gameObject.SetActive(true);
            optionButtonTexts[i].text = selectedLanguage == QuizLanguage.Romanian ? question.OptionsRo[i] : question.OptionsEn[i];

            Image image = optionButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = optionColor;
            }

            yield return AnimateOption(optionButtons[i].transform, true, i * 0.035f);
        }

        while (!answerChosen && !leaveRequested)
        {
            if (backRequested)
            {
                yield return AnimateQuestionSwapOut();
                onFinished(new QuizStepResult { AnsweredCorrectly = false, GoBack = true });
                yield break;
            }

            if (hintRequested)
            {
                hintRequested = false;
                yield return ShowHintScreen(selectedLanguage == QuizLanguage.Romanian ? question.HintRo : question.HintEn);
                if (leaveRequested)
                {
                    yield break;
                }
                RestoreQuestionUi(question, sessionPosition);
                hintRequested = false;
            }
            else if (aiChatRequested)
            {
                aiChatRequested = false;
                yield return ShowAiChatScreen();
                if (leaveRequested)
                {
                    yield break;
                }
                RestoreQuestionUi(question, sessionPosition);
            }
            yield return null;
        }

        if (leaveRequested)
        {
            yield break;
        }

        if (answerWasCorrect)
        {
            feedbackText.text = selectedLanguage == QuizLanguage.Romanian
                ? "Corect. Trecem la urmatoarea intrebare."
                : "Correct. Moving to the next question.";
            feedbackText.color = correctColor;
            yield return new WaitForSeconds(0.55f);
            yield return AnimateQuestionSwapOut();
            onFinished(new QuizStepResult { AnsweredCorrectly = true, GoBack = false });
            yield break;
        }

        feedbackText.text = selectedLanguage == QuizLanguage.Romanian ? question.CorrectTextRo : question.CorrectTextEn;
        feedbackText.color = wrongColor;
        nextButton.gameObject.SetActive(true);
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(OnNextClicked);

        while (!nextRequested && !leaveRequested)
        {
            if (backRequested)
            {
                nextButton.gameObject.SetActive(false);
                yield return AnimateQuestionSwapOut();
                onFinished(new QuizStepResult { AnsweredCorrectly = false, GoBack = true });
                yield break;
            }

            if (hintRequested)
            {
                hintRequested = false;
                yield return ShowHintScreen(selectedLanguage == QuizLanguage.Romanian ? question.HintRo : question.HintEn);
                if (leaveRequested)
                {
                    yield break;
                }
                RestoreQuestionUi(question, sessionPosition);
                hintRequested = false;
            }
            else if (aiChatRequested)
            {
                aiChatRequested = false;
                yield return ShowAiChatScreen();
                if (leaveRequested)
                {
                    yield break;
                }
                RestoreQuestionUi(question, sessionPosition);
            }
            yield return null;
        }

        nextButton.gameObject.SetActive(false);
        yield return AnimateQuestionSwapOut();
        onFinished(new QuizStepResult { AnsweredCorrectly = false, GoBack = false });
    }

    private void SelectAnswer(int optionIndex, QuizQuestion question)
    {
        if (answerChosen)
        {
            return;
        }

        answerChosen = true;
        answerWasCorrect = optionIndex == question.CorrectIndex;
        selectedAnswerIndex = optionIndex;
        RecordLearningEvent("quiz_answer", ResolveQuestionTopic(question), answerWasCorrect ? 1 : 0, BuildQuizDetails(question));

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].interactable = false;
            Image image = optionButtons[i].GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            if (i == question.CorrectIndex)
            {
                image.color = correctColor;
            }
            else if (i == optionIndex && !answerWasCorrect)
            {
                image.color = wrongColor;
            }
            else
            {
                image.color = optionColor * 0.92f;
            }
        }
    }

    private void RecordLearningEvent(string eventType, string topic, int correctness, string details)
    {
        if (GameClient.Instance == null)
        {
            return;
        }

        StartCoroutine(SendPacketWithConnect(new RecordLearningEventPacket(eventType, topic, correctness, details), null));
    }

    private string ResolveQuestionTopic(QuizQuestion question)
    {
        string prompt = selectedLanguage == QuizLanguage.Romanian ? question.PromptRo : question.PromptEn;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "cpp_quiz";
        }

        string[] lines = prompt.Split('\n');
        string title = lines.Length > 0 ? lines[0].Trim() : "quiz";
        return "cpp:" + title;
    }

    private string BuildQuizDetails(QuizQuestion question)
    {
        string prompt = selectedLanguage == QuizLanguage.Romanian ? question.PromptRo : question.PromptEn;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "mode=cpp_quiz";
        }
        string title = prompt.Split('\n')[0].Trim();
        return "mode=cpp_quiz;question=" + title;
    }

    private void OnNextClicked()
    {
        nextRequested = true;
    }

    private void OnBackClicked()
    {
        backRequested = true;
    }

    private void OnHintClicked()
    {
        hintRequested = true;
    }

    private void OnAiChatClicked()
    {
        aiChatRequested = true;
    }

    private void OnAiChatSendClicked()
    {
        aiChatSendRequested = true;
    }

    private void OnAiChatBackClicked()
    {
        aiChatBackRequested = true;
    }

    private void OnRetryWrongClicked()
    {
        retryWrongRequested = true;
    }

    private void OnHintScreenBackClicked()
    {
        hintScreenBackRequested = true;
    }

    private IEnumerator AnimateQuestionSwapOut()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            yield return AnimateOption(optionButtons[i].transform, false, 0f);
            optionButtons[i].gameObject.SetActive(false);
        }
    }

    private void RestoreQuestionUi(QuizQuestion question, int sessionPosition)
    {
        menuCard.gameObject.SetActive(true);
        titleText.gameObject.SetActive(true);
        counterText.gameObject.SetActive(true);
        questionText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(true);
        languagePromptText.gameObject.SetActive(false);

        retryWrongButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(sessionPosition > 0);
        hintButton.gameObject.SetActive(true);
        aiButton.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(answerChosen && !answerWasCorrect);
        aiButton.onClick.RemoveAllListeners();
        aiButton.onClick.AddListener(OnAiChatClicked);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].gameObject.SetActive(true);
            optionButtons[i].interactable = !answerChosen;
            optionButtonTexts[i].text = selectedLanguage == QuizLanguage.Romanian ? question.OptionsRo[i] : question.OptionsEn[i];

            Image image = optionButtons[i].GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            if (!answerChosen)
            {
                image.color = optionColor;
                continue;
            }

            if (i == question.CorrectIndex)
            {
                image.color = correctColor;
            }
            else if (i == selectedAnswerIndex && !answerWasCorrect)
            {
                image.color = wrongColor;
            }
            else
            {
                image.color = optionColor * 0.92f;
            }
        }
    }

    private IEnumerator AnimateOption(Transform optionTransform, bool show, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Vector3 startScale = show ? new Vector3(0.92f, 0.92f, 1f) : optionTransform.localScale;
        Vector3 endScale = show ? Vector3.one : new Vector3(0.92f, 0.92f, 1f);
        Graphic graphic = optionTransform.GetComponent<Graphic>();
        Color baseColor = graphic != null ? graphic.color : Color.white;
        float fromAlpha = show ? 0f : 1f;
        float toAlpha = show ? 1f : 0f;

        float elapsed = 0f;
        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(elapsed / 0.12f);
            optionTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            if (graphic != null)
            {
                graphic.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(fromAlpha, toAlpha, t));
            }
            yield return null;
        }

        optionTransform.localScale = endScale;
        if (graphic != null)
        {
            graphic.color = new Color(baseColor.r, baseColor.g, baseColor.b, toAlpha);
        }
    }

    private IEnumerator AnimateMenuCard(bool show)
    {
        if (show)
        {
            menuCard.gameObject.SetActive(true);
            titleText.gameObject.SetActive(true);
            counterText.gameObject.SetActive(true);
            questionText.gameObject.SetActive(true);
            feedbackText.gameObject.SetActive(true);
            languagePromptText.gameObject.SetActive(false);
            languageRoButton.gameObject.SetActive(false);
            languageEnButton.gameObject.SetActive(false);
        }

        RectTransform rect = menuCard.rectTransform;
        Vector3 startScale = show ? new Vector3(0.94f, 0.94f, 1f) : rect.localScale;
        Vector3 endScale = show ? Vector3.one : new Vector3(0.94f, 0.94f, 1f);
        float fromAlpha = show ? 0f : 1f;
        float toAlpha = show ? 1f : 0f;

        float elapsed = 0f;
        while (elapsed < menuFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(elapsed / Mathf.Max(0.01f, menuFadeDuration));
            rect.localScale = Vector3.Lerp(startScale, endScale, t);
            SetMenuAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        rect.localScale = endScale;
        SetMenuAlpha(toAlpha);

        if (!show)
        {
            HideOptions();
            menuCard.gameObject.SetActive(false);
            titleText.gameObject.SetActive(false);
            counterText.gameObject.SetActive(false);
            questionText.gameObject.SetActive(false);
            feedbackText.gameObject.SetActive(false);
        }
    }

    private IEnumerator ShowHintScreen(string hintText)
    {
        hintScreenBackRequested = false;

        HideOverlayImmediate();

        hintScreenText.text = hintText;
        hintScreenText.gameObject.SetActive(true);

        hintScreenBackButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Inapoi" : "Back";
        hintScreenBackButton.onClick.RemoveAllListeners();
        hintScreenBackButton.onClick.AddListener(OnHintScreenBackClicked);
        hintScreenBackButton.gameObject.SetActive(true);

        while (!hintScreenBackRequested && !leaveRequested)
        {
            yield return null;
        }

        hintScreenText.gameObject.SetActive(false);
        hintScreenBackButton.gameObject.SetActive(false);
        if (leaveRequested)
        {
            yield break;
        }

        menuCard.gameObject.SetActive(true);
        titleText.gameObject.SetActive(true);
        counterText.gameObject.SetActive(true);
        questionText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(true);
    }

    private IEnumerator ShowAiChatScreen()
    {
        aiChatBackRequested = false;
        aiChatSendRequested = false;

        HideOverlayImmediate();
        ShowAiChatUi(true);

        aiChatTitleText.text = selectedLanguage == QuizLanguage.Romanian ? "Asistent AI" : "AI Assistant";
        aiChatSendButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Trimite" : "Send";
        aiChatBackButtonText.text = selectedLanguage == QuizLanguage.Romanian ? "Inapoi" : "Back";
        if (aiChatPlaceholderText != null)
        {
            aiChatPlaceholderText.text = selectedLanguage == QuizLanguage.Romanian ? "Intreaba AI..." : "Ask the AI...";
        }

        aiChatSendButton.onClick.RemoveAllListeners();
        aiChatSendButton.onClick.AddListener(OnAiChatSendClicked);
        aiChatBackButton.onClick.RemoveAllListeners();
        aiChatBackButton.onClick.AddListener(OnAiChatBackClicked);

        if (aiChatLines.Count == 0)
        {
            AppendChatLine(selectedLanguage == QuizLanguage.Romanian
                ? "AI este gata. Pune o intrebare!"
                : "AI is ready. Ask a question!");
        }

        aiChatInput.text = string.Empty;
        aiChatInput.ActivateInputField();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(aiChatInput.gameObject);
        }
        SetCursorVisible(true);

        while (!aiChatBackRequested && !leaveRequested)
        {
            if (aiChatSendRequested)
            {
                aiChatSendRequested = false;
                string message = aiChatInput.text;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    aiChatInput.text = string.Empty;
                    yield return SendAiChatMessage(message);
                }
            }

            yield return null;
        }

        ShowAiChatUi(false);
        if (leaveRequested)
        {
            yield break;
        }

        menuCard.gameObject.SetActive(true);
        titleText.gameObject.SetActive(true);
        counterText.gameObject.SetActive(true);
        questionText.gameObject.SetActive(true);
        feedbackText.gameObject.SetActive(true);
    }

    private void ShowAiChatUi(bool visible)
    {
        if (aiChatPanel == null)
        {
            return;
        }

        aiChatPanel.gameObject.SetActive(visible);
        aiChatTitleText.gameObject.SetActive(visible);
        aiChatHistoryText.gameObject.SetActive(visible);
        aiChatInput.gameObject.SetActive(visible);
        aiChatSendButton.gameObject.SetActive(visible);
        aiChatBackButton.gameObject.SetActive(visible);
    }

    private IEnumerator SendAiChatMessage(string message)
    {
        if (awaitingAiResponse)
        {
            AppendChatLine(selectedLanguage == QuizLanguage.Romanian
                ? "AI lucreaza deja. Asteapta un raspuns."
                : "AI is already working. Please wait.");
            yield break;
        }

        AppendChatLine((selectedLanguage == QuizLanguage.Romanian ? "Tu: " : "You: ") + message);
        awaitingAiResponse = true;
        aiChatSendButton.interactable = false;

        string question = BuildAiChatQuestion(message);
        bool sendOk = false;
        yield return SendPacketWithConnect(new AskAiPacket(question, "cpp_quiz_hint"), success => sendOk = success);
        if (!sendOk)
        {
            awaitingAiResponse = false;
            aiChatSendButton.interactable = true;
            AppendChatLine(selectedLanguage == QuizLanguage.Romanian
                ? "Nu pot contacta serverul AI acum."
                : "Can't reach the AI server right now.");
            yield break;
        }

        float elapsed = 0f;
        while (awaitingAiResponse && elapsed < 12f && !leaveRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        aiChatSendButton.interactable = true;
        if (awaitingAiResponse)
        {
            awaitingAiResponse = false;
            AppendChatLine(selectedLanguage == QuizLanguage.Romanian
                ? "AI nu a raspuns la timp."
                : "AI did not respond in time.");
        }
    }

    private void AppendChatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        aiChatLines.Add(line.Trim());
        if (aiChatLines.Count > MaxChatLines)
        {
            aiChatLines.RemoveAt(0);
        }

        RefreshChatText();
    }

    private void RefreshChatText()
    {
        if (aiChatHistoryText == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < aiChatLines.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }
            builder.Append(aiChatLines[i]);
        }

        aiChatHistoryText.text = builder.ToString();
        aiChatHistoryText.alignment = TextAnchor.UpperLeft;
    }

    private string BuildAiChatQuestion(string userMessage)
    {
        if (activeQuestionIndex < 0 || activeQuestionIndex >= Questions.Length)
        {
            return userMessage;
        }

        QuizQuestion question = Questions[activeQuestionIndex];
        string prompt = selectedLanguage == QuizLanguage.Romanian ? question.PromptRo : question.PromptEn;
        string[] options = selectedLanguage == QuizLanguage.Romanian ? question.OptionsRo : question.OptionsEn;
        return "Question:\\n" + prompt + "\\n\\nOptions:\\n" + options[0] + "\\n" + options[1] + "\\n" + options[2]
            + "\\n\\nStudent question:\\n" + userMessage + "\\n\\nGive a helpful hint without revealing the answer.";
    }

    private void SetMenuAlpha(float alpha)
    {
        menuCard.color = new Color(cardColor.r, cardColor.g, cardColor.b, alpha * cardColor.a);
        titleText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        counterText.color = new Color(secondaryAccentColor.r, secondaryAccentColor.g, secondaryAccentColor.b, alpha);
        questionText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        feedbackText.color = new Color(feedbackText.color.r, feedbackText.color.g, feedbackText.color.b, alpha);
        Image leaveImage = leaveButton.GetComponent<Image>();
        if (leaveImage != null)
        {
            leaveImage.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, alpha * buttonColor.a);
        }
        Color leaveBase = leaveButtonText.color;
        leaveButtonText.color = new Color(leaveBase.r, leaveBase.g, leaveBase.b, alpha);
        Image backImage = backButton.GetComponent<Image>();
        if (backImage != null)
        {
            backImage.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, alpha * buttonColor.a);
        }
        Color backBase = backButtonText.color;
        backButtonText.color = new Color(backBase.r, backBase.g, backBase.b, alpha);
        Image hintImage = hintButton.GetComponent<Image>();
        if (hintImage != null)
        {
            hintImage.color = new Color(secondaryAccentColor.r, secondaryAccentColor.g, secondaryAccentColor.b, alpha);
        }
        Color hintBase = hintButtonText.color;
        hintButtonText.color = new Color(hintBase.r, hintBase.g, hintBase.b, alpha);
        Image aiImage = aiButton.GetComponent<Image>();
        if (aiImage != null)
        {
            aiImage.color = new Color(buttonColor.r, buttonColor.g, buttonColor.b, alpha);
        }
        Color aiBase = aiButtonText.color;
        aiButtonText.color = new Color(aiBase.r, aiBase.g, aiBase.b, alpha);
        Image nextImage = nextButton.GetComponent<Image>();
        if (nextImage != null)
        {
            nextImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, alpha);
        }
        Color nextBase = nextButtonText.color;
        nextButtonText.color = new Color(nextBase.r, nextBase.g, nextBase.b, alpha);
        Image retryImage = retryWrongButton.GetComponent<Image>();
        if (retryImage != null)
        {
            retryImage.color = new Color(secondaryAccentColor.r, secondaryAccentColor.g, secondaryAccentColor.b, alpha);
        }
        Color retryBase = retryWrongButtonText.color;
        retryWrongButtonText.color = new Color(retryBase.r, retryBase.g, retryBase.b, alpha);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            Image optionImage = optionButtons[i].GetComponent<Image>();
            if (optionImage != null)
            {
                Color c = optionImage.color;
                optionImage.color = new Color(c.r, c.g, c.b, alpha);
            }

            Color optionTextBase = optionButtonTexts[i].color;
            optionButtonTexts[i].color = new Color(optionTextBase.r, optionTextBase.g, optionTextBase.b, alpha);
        }
    }

    private void HideOptions()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].gameObject.SetActive(false);
        }
        nextButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);
        hintButton.gameObject.SetActive(false);
    }

    private IEnumerator ShowLanguageSelection()
    {
        HideOptions();
        nextButton.gameObject.SetActive(false);
        aiButton.gameObject.SetActive(false);
        languageChosen = false;
        titleText.text = selectedLanguage == QuizLanguage.Romanian ? "Quiz C++ pentru incepatori" : "C++ Starter Quiz";
        counterText.text = string.Empty;
        questionText.text = string.Empty;
        feedbackText.text = string.Empty;
        languagePromptText.text = selectedLanguage == QuizLanguage.Romanian ? "Alege limba" : "Choose language";
        languagePromptText.gameObject.SetActive(true);
        languageRoButton.gameObject.SetActive(true);
        languageEnButton.gameObject.SetActive(true);

        languageRoButton.onClick.RemoveAllListeners();
        languageEnButton.onClick.RemoveAllListeners();
        languageRoButton.onClick.AddListener(() => SelectLanguage(QuizLanguage.Romanian));
        languageEnButton.onClick.AddListener(() => SelectLanguage(QuizLanguage.English));

        while (!languageChosen && !leaveRequested)
        {
            yield return null;
        }

        languagePromptText.gameObject.SetActive(false);
        languageRoButton.gameObject.SetActive(false);
        languageEnButton.gameObject.SetActive(false);
    }

    private void SelectLanguage(QuizLanguage language)
    {
        selectedLanguage = language;
        languageChosen = true;
    }

    private Vector3 GetPortalPosition(Transform playerRoot, Vector3 playerPosition)
    {
        return playerPosition
            + (playerRoot.forward * portalForwardDistance)
            + (playerRoot.right * portalSideDistance)
            + (Vector3.up * portalHeightOffset);
    }

    private Vector3 GetSafeExitPosition(Vector3 fallbackPosition, Transform playerRoot)
    {
        Bounds padBounds = triggerCollider != null ? triggerCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 candidate = padBounds.center;
        candidate.y = padBounds.max.y + exitHeightOffset;
        candidate -= playerRoot.forward * exitBackOffset;

        Vector3 rayOrigin = candidate + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            candidate.y = hit.point.y + exitHeightOffset;
        }

        if (float.IsNaN(candidate.x) || float.IsNaN(candidate.y) || float.IsNaN(candidate.z))
        {
            return fallbackPosition + Vector3.up * exitHeightOffset;
        }

        return candidate;
    }

    private IEnumerator TurnPlayerTowardPortal(
        Transform playerRoot,
        Transform camTransform,
        Camera activeCamera,
        Vector3 baseCameraLocalPosition,
        Quaternion baseCameraLocalRotation,
        float baseFov,
        Vector3 portalPosition)
    {
        Vector3 lookDirection = portalPosition - playerRoot.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = playerRoot.forward;
        }

        Quaternion fromRotation = playerRoot.rotation;
        Quaternion toRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < lookAtPortalDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, lookAtPortalDuration));
            playerRoot.rotation = Quaternion.Slerp(fromRotation, toRotation, t);
            camTransform.localPosition = Vector3.Lerp(baseCameraLocalPosition, baseCameraLocalPosition + new Vector3(0f, 0.012f, 0f), t);
            camTransform.localRotation = Quaternion.Slerp(baseCameraLocalRotation, baseCameraLocalRotation * Quaternion.Euler(1.1f, 0f, 1.4f), t);
            activeCamera.fieldOfView = Mathf.Lerp(baseFov, baseFov + cameraFovBoost * 0.35f, t);
            yield return null;
        }

        playerRoot.rotation = toRotation;
        camTransform.localPosition = baseCameraLocalPosition;
        camTransform.localRotation = baseCameraLocalRotation;
    }

    private IEnumerator PullPlayerToPortal(
        Transform playerRoot,
        Transform camTransform,
        Camera activeCamera,
        Vector3 baseCameraLocalPosition,
        Quaternion baseCameraLocalRotation,
        float baseFov,
        Vector3 portalPosition)
    {
        Vector3 fromPosition = playerRoot.position;
        Vector3 toPosition = portalPosition - ((portalPosition - fromPosition).normalized * 0.22f);
        Quaternion fromRotation = playerRoot.rotation;
        Quaternion toRotation = Quaternion.LookRotation((portalPosition - fromPosition).normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < suctionDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, suctionDuration));
            playerRoot.position = Vector3.Lerp(fromPosition, toPosition, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.08f);
            playerRoot.rotation = Quaternion.Slerp(fromRotation, toRotation, t);
            camTransform.localRotation = Quaternion.Slerp(baseCameraLocalRotation, baseCameraLocalRotation * Quaternion.Euler(0.8f, 0f, 1.6f), t);
            activeCamera.fieldOfView = Mathf.Lerp(baseFov + cameraFovBoost * 0.35f, baseFov + cameraFovBoost, t);
            SetPulseAlpha(Mathf.Lerp(0.18f, 0.34f, t));
            yield return null;
        }
    }

    private IEnumerator MovePlayerOut(Transform playerRoot, Vector3 toPosition, Quaternion toRotation)
    {
        Vector3 startPosition = playerRoot.position;
        Quaternion startRotation = playerRoot.rotation;

        float elapsed = 0f;
        while (elapsed < exitMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, exitMoveDuration));
            playerRoot.position = Vector3.Lerp(startPosition, toPosition, t);
            playerRoot.rotation = Quaternion.Slerp(startRotation, toRotation, t);
            yield return null;
        }

        playerRoot.position = toPosition;
        playerRoot.rotation = toRotation;
    }

    private IEnumerator AnimatePortalAppear(Transform portalTransform)
    {
        Vector3 targetScale = portalTransform.localScale;
        portalTransform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < portalSpawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(elapsed / Mathf.Max(0.01f, portalSpawnDuration));
            portalTransform.localScale = targetScale * t;
            SetPulseAlpha(Mathf.Lerp(0.08f, 0.22f, t));
            yield return null;
        }

        portalTransform.localScale = targetScale;
    }

    private IEnumerator CollapsePortal(GameObject portal)
    {
        if (portal == null)
        {
            yield break;
        }

        Transform portalTransform = portal.transform;
        Vector3 startScale = portalTransform.localScale;

        float elapsed = 0f;
        while (elapsed < portalCollapseDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, portalCollapseDuration));
            portalTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            SetPulseAlpha(Mathf.Lerp(0.2f, 0f, t));
            yield return null;
        }

        Destroy(portal);
        activePortal = null;
        SetPulseAlpha(0f);
    }

    private IEnumerator WaitForLeave()
    {
        leaveRequested = false;
        while (!leaveRequested)
        {
            yield return null;
        }
    }

    private IEnumerator ReenableTriggerAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, reenterCooldown));
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
        running = false;
    }

    private void RestorePlayerState(BeanController sphere, FirstPersonControllerSimple fps)
    {
        SetPlayerLockState(sphere, fps, false, false);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(true);
        }
        SetCursorVisible(false);
        running = false;
    }

    private static void TeleportPlayer(BeanController sphere, FirstPersonControllerSimple fps, Vector3 position, Quaternion rotation)
    {
        if (fps != null)
        {
            CharacterController controller = fps.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null)
            {
                controller.enabled = false;
            }

            fps.transform.SetPositionAndRotation(position, rotation);

            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }
        }

        if (sphere != null)
        {
            Rigidbody rb = sphere.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rotation;
            }

            sphere.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private GameObject CreatePortal(Vector3 position, Quaternion rotation)
    {
        GameObject root = new GameObject("CppPortal");
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = Vector3.one;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = new Vector3(1.45f, 2.05f, 0.16f);
        Destroy(core.GetComponent<Collider>());
        ApplyEmissionMaterial(core, portalCoreColor, 2.8f);

        GameObject outerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outerRing.name = "OuterRing";
        outerRing.transform.SetParent(root.transform, false);
        outerRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        outerRing.transform.localScale = new Vector3(1.35f, 0.04f, 1.35f);
        Destroy(outerRing.GetComponent<Collider>());
        ApplyEmissionMaterial(outerRing, portalRingColor, 4.2f);

        GameObject innerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        innerRing.name = "InnerRing";
        innerRing.transform.SetParent(root.transform, false);
        innerRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        innerRing.transform.localScale = new Vector3(1.05f, 0.025f, 1.05f);
        Destroy(innerRing.GetComponent<Collider>());
        ApplyEmissionMaterial(innerRing, pulseColor, 3.2f);

        GameObject lightRoot = new GameObject("PortalLight");
        lightRoot.transform.SetParent(root.transform, false);
        Light portalLight = lightRoot.AddComponent<Light>();
        portalLight.type = LightType.Point;
        portalLight.range = 10f;
        portalLight.intensity = 5.4f;
        portalLight.color = pulseColor;

        CreatePortalParticles(root.transform);

        PortalLoopAnimator animator = root.AddComponent<PortalLoopAnimator>();
        animator.Initialize(pulseColor);
        return root;
    }

    private void CreatePortalParticles(Transform parent)
    {
        GameObject fxRoot = new GameObject("PortalParticles");
        fxRoot.transform.SetParent(parent, false);
        fxRoot.SetActive(false);

        ParticleSystem ps = fxRoot.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.maxParticles = 80;
        main.startColor = new ParticleSystem.MinMaxGradient(pulseColor, portalRingColor);

        var emission = ps.emission;
        emission.rateOverTime = 34f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 0.9f;
        shape.radiusThickness = 0.15f;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(1.25f);
        velocity.radial = new ParticleSystem.MinMaxCurve(0.04f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fxRoot.SetActive(true);
        ps.Play();
    }

    private static void ApplyEmissionMaterial(GameObject target, Color color, float emissionIntensity)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * emissionIntensity);
        }
        material.EnableKeyword("_EMISSION");
        renderer.material = material;
    }

    private static Camera ResolveCamera(FirstPersonControllerSimple fps)
    {
        if (fps != null)
        {
            Camera childCamera = fps.GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                return childCamera;
            }
        }

        return Camera.main;
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

    private static void SetPlayerLockState(BeanController sphere, FirstPersonControllerSimple fps, bool movementLocked, bool hardFreeze)
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

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas == null)
        {
            GameObject canvasObject = GameObject.Find("CppQuestionPadCinematicCanvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("CppQuestionPadCinematicCanvas");
                overlayCanvas = canvasObject.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 12000;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
                Object.DontDestroyOnLoad(canvasObject);
            }
            else
            {
                overlayCanvas = canvasObject.GetComponent<Canvas>();
            }
        }

        if (overlayCanvas != null && !overlayCanvas.gameObject.activeSelf)
        {
            overlayCanvas.gameObject.SetActive(true);
        }

        Transform root = overlayCanvas.transform;
        pulseImage = EnsureImage(root, "PulseImage", pageTint);
        whiteImage = EnsureImage(root, "WhiteImage", pageTint);
        menuCard = EnsurePanel(root, "QuizCard");
        titleText = EnsureText(menuCard.transform, "TitleText", new Vector2(0.5f, 0.85f), new Vector2(780f, 70f), titleSize, FontStyle.Bold, textColor);
        counterText = EnsureText(menuCard.transform, "CounterText", new Vector2(0.5f, 0.75f), new Vector2(760f, 50f), bodySize, FontStyle.Bold, secondaryAccentColor);
        questionText = EnsureText(menuCard.transform, "QuestionText", new Vector2(0.5f, 0.59f), new Vector2(760f, 140f), questionSize, FontStyle.Bold, textColor);
        feedbackText = EnsureText(menuCard.transform, "FeedbackText", new Vector2(0.5f, 0.055f), new Vector2(760f, 110f), bodySize, FontStyle.Bold, textColor);
        languagePromptText = EnsureText(menuCard.transform, "LanguagePromptText", new Vector2(0.5f, 0.59f), new Vector2(760f, 80f), questionSize, FontStyle.Bold, textColor);
        hintScreenText = EnsureText(root, "HintScreenText", new Vector2(0.5f, 0.58f), new Vector2(1020f, 340f), questionSize, FontStyle.Bold, textColor);
        leaveButton = EnsureButton(root, "LeaveButton", new Vector2(0.11f, 0.11f), new Vector2(180f, 56f), buttonColor, "Leave", buttonTextSize);
        hintScreenBackButton = EnsureButton(root, "HintScreenBackButton", new Vector2(0.27f, 0.11f), new Vector2(180f, 56f), buttonColor, "Back", buttonTextSize);
        nextButton = EnsureButton(menuCard.transform, "NextButton", new Vector2(0.82f, 0.13f), new Vector2(170f, 52f), accentColor, "Next", buttonTextSize);
        backButton = EnsureButton(menuCard.transform, "BackButton", new Vector2(0.18f, 0.13f), new Vector2(170f, 52f), buttonColor, "Back", buttonTextSize);
        hintButton = EnsureButton(menuCard.transform, "HintButton", new Vector2(0.44f, 0.13f), new Vector2(140f, 52f), secondaryAccentColor, "Hint", buttonTextSize);
        aiButton = EnsureButton(menuCard.transform, "AiButton", new Vector2(0.56f, 0.13f), new Vector2(140f, 52f), buttonColor, "AI", buttonTextSize);
        retryWrongButton = EnsureButton(menuCard.transform, "RetryWrongButton", new Vector2(0.5f, 0.13f), new Vector2(300f, 56f), secondaryAccentColor, "Retry Wrong Questions", buttonTextSize);
        languageRoButton = EnsureButton(menuCard.transform, "LanguageRoButton", new Vector2(0.38f, 0.44f), new Vector2(220f, 64f), accentColor, "Romana", buttonTextSize);
        languageEnButton = EnsureButton(menuCard.transform, "LanguageEnButton", new Vector2(0.62f, 0.44f), new Vector2(220f, 64f), secondaryAccentColor, "English", buttonTextSize);
        leaveButtonText = leaveButton.GetComponentInChildren<Text>(true);
        hintScreenBackButtonText = hintScreenBackButton.GetComponentInChildren<Text>(true);
        nextButtonText = nextButton.GetComponentInChildren<Text>(true);
        backButtonText = backButton.GetComponentInChildren<Text>(true);
        hintButtonText = hintButton.GetComponentInChildren<Text>(true);
        aiButtonText = aiButton.GetComponentInChildren<Text>(true);
        retryWrongButtonText = retryWrongButton.GetComponentInChildren<Text>(true);

        optionButtons = new Button[3];
        optionButtonTexts = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = 0.46f - (i * 0.12f);
            optionButtons[i] = EnsureButton(menuCard.transform, "OptionButton_" + i, new Vector2(0.5f, y), new Vector2(760f, 72f), optionColor, string.Empty, buttonTextSize);
            optionButtonTexts[i] = optionButtons[i].GetComponentInChildren<Text>(true);
            optionButtonTexts[i].color = textColor;
        }

        aiChatPanel = EnsurePanel(root, "AiChatPanel");
        aiChatPanel.color = cardColor;
        aiChatTitleText = EnsureText(aiChatPanel.transform, "AiChatTitleText", new Vector2(0.5f, 0.86f), new Vector2(780f, 60f), titleSize - 4, FontStyle.Bold, textColor);
        aiChatHistoryText = EnsureText(aiChatPanel.transform, "AiChatHistoryText", new Vector2(0.5f, 0.58f), new Vector2(860f, 320f), bodySize, FontStyle.Normal, textColor);
        aiChatInput = EnsureChatInput(aiChatPanel.transform);
        aiChatSendButton = EnsureButton(aiChatPanel.transform, "AiChatSendButton", new Vector2(0.76f, 0.12f), new Vector2(170f, 52f), accentColor, "Send", buttonTextSize);
        aiChatBackButton = EnsureButton(aiChatPanel.transform, "AiChatBackButton", new Vector2(0.24f, 0.12f), new Vector2(170f, 52f), buttonColor, "Back", buttonTextSize);
        aiChatSendButtonText = aiChatSendButton.GetComponentInChildren<Text>(true);
        aiChatBackButtonText = aiChatBackButton.GetComponentInChildren<Text>(true);
        aiChatInputText = aiChatInput.textComponent;
        aiChatPlaceholderText = aiChatInput.placeholder as Text;

        EnsureEventSystem();
    }

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image EnsurePanel(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
        }
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 10f);
        rect.sizeDelta = new Vector2(860f, 620f);

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private static Text EnsureText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, FontStyle fontStyle, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
        }
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Text text = go.GetComponent<Text>();
        if (text == null)
        {
            text = go.AddComponent<Text>();
        }
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Button EnsureButton(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, string label, int fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
        }
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }
        image.color = color;

        Button button = go.GetComponent<Button>();
        if (button == null)
        {
            button = go.AddComponent<Button>();
        }

        Transform textChild = go.transform.Find("Label");
        GameObject textObj = textChild != null ? textChild.gameObject : new GameObject("Label");
        textObj.transform.SetParent(go.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (textRect == null)
        {
            textRect = textObj.AddComponent<RectTransform>();
        }
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.GetComponent<Text>();
        if (text == null)
        {
            text = textObj.AddComponent<Text>();
        }
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        text.color = Color.white;
        text.raycastTarget = false;

        return button;
    }

    private InputField EnsureChatInput(Transform parent)
    {
        Transform existing = parent.Find("AiChatInput");
        GameObject root = existing != null ? existing.gameObject : new GameObject("AiChatInput");
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = root.AddComponent<RectTransform>();
        }
        rect.anchorMin = new Vector2(0.5f, 0.22f);
        rect.anchorMax = new Vector2(0.5f, 0.22f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(760f, 64f);
        rect.localScale = Vector3.one;

        Image image = root.GetComponent<Image>();
        if (image == null)
        {
            image = root.AddComponent<Image>();
        }
        image.color = Color.white * 0.98f;
        image.raycastTarget = true;
        Outline outline = root.GetComponent<Outline>();
        if (outline == null)
        {
            outline = root.AddComponent<Outline>();
        }
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);
        RectMask2D mask = root.GetComponent<RectMask2D>();
        if (mask == null)
        {
            mask = root.AddComponent<RectMask2D>();
        }
        mask.padding = Vector4.zero;

        InputField input = root.GetComponent<InputField>();
        if (input == null)
        {
            input = root.AddComponent<InputField>();
        }
        input.lineType = InputField.LineType.SingleLine;
        input.transition = Selectable.Transition.None;
        input.selectionColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.22f);
        input.caretColor = accentColor;
        input.customCaretColor = true;

        GameObject textObj = existing != null ? existing.Find("Text")?.gameObject : null;
        if (textObj == null)
        {
            textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);
        }
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (textRect == null)
        {
            textRect = textObj.AddComponent<RectTransform>();
        }
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);
        Text text = textObj.GetComponent<Text>() ?? textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = bodySize + 1;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = textColor;
        text.supportRichText = false;
        input.textComponent = text;

        GameObject placeholderObj = existing != null ? existing.Find("Placeholder")?.gameObject : null;
        if (placeholderObj == null)
        {
            placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(root.transform, false);
        }
        RectTransform placeRect = placeholderObj.GetComponent<RectTransform>();
        if (placeRect == null)
        {
            placeRect = placeholderObj.AddComponent<RectTransform>();
        }
        placeRect.anchorMin = new Vector2(0f, 0f);
        placeRect.anchorMax = new Vector2(1f, 1f);
        placeRect.offsetMin = new Vector2(16f, 10f);
        placeRect.offsetMax = new Vector2(-16f, -10f);
        Text placeholder = placeholderObj.GetComponent<Text>() ?? placeholderObj.AddComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = bodySize + 1;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
        placeholder.text = "Ask the AI...";
        placeholder.resizeTextForBestFit = false;
        input.placeholder = placeholder;
        input.targetGraphic = image;
        Navigation navigation = input.navigation;
        navigation.mode = Navigation.Mode.None;
        input.navigation = navigation;
        return input;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = EventSystem.current != null ? EventSystem.current : Object.FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            StandaloneInputModule legacyModule = existing.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.Destroy(legacyModule);
            }

            if (existing.GetComponent<InputSystemUIInputModule>() == null)
            {
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            return;
        }

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        Object.DontDestroyOnLoad(go);
    }

    private void ResetOverlay()
    {
        if (overlayCanvas != null && !overlayCanvas.gameObject.activeSelf)
        {
            overlayCanvas.gameObject.SetActive(true);
        }

        pulseImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, 0f);
        pulseImage.enabled = false;
        whiteImage.color = new Color(pageTint.r, pageTint.g, pageTint.b, 0f);
        whiteImage.enabled = false;
        menuCard.color = new Color(cardColor.r, cardColor.g, cardColor.b, 0f);
        menuCard.rectTransform.localScale = new Vector3(0.94f, 0.94f, 1f);
        titleText.text = selectedLanguage == QuizLanguage.Romanian ? "Quiz C++ pentru incepatori" : "C++ Starter Quiz";
        counterText.text = string.Empty;
        questionText.text = string.Empty;
        feedbackText.text = string.Empty;
        titleText.gameObject.SetActive(false);
        counterText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);
        languagePromptText.gameObject.SetActive(false);
        hintScreenText.gameObject.SetActive(false);
        menuCard.gameObject.SetActive(false);
        leaveButton.onClick.RemoveAllListeners();
        leaveButton.gameObject.SetActive(false);
        hintScreenBackButton.onClick.RemoveAllListeners();
        hintScreenBackButton.gameObject.SetActive(false);
        nextButton.onClick.RemoveAllListeners();
        nextButton.gameObject.SetActive(false);
        backButton.onClick.RemoveAllListeners();
        backButton.gameObject.SetActive(false);
        hintButton.onClick.RemoveAllListeners();
        hintButton.gameObject.SetActive(false);
        aiButton.onClick.RemoveAllListeners();
        aiButton.gameObject.SetActive(false);
        retryWrongButton.onClick.RemoveAllListeners();
        retryWrongButton.gameObject.SetActive(false);
        languageRoButton.onClick.RemoveAllListeners();
        languageRoButton.gameObject.SetActive(false);
        languageEnButton.onClick.RemoveAllListeners();
        languageEnButton.gameObject.SetActive(false);
        if (aiChatPanel != null) aiChatPanel.gameObject.SetActive(false);
        if (aiChatTitleText != null) aiChatTitleText.gameObject.SetActive(false);
        if (aiChatHistoryText != null) aiChatHistoryText.gameObject.SetActive(false);
        if (aiChatInput != null) aiChatInput.gameObject.SetActive(false);
        if (aiChatSendButton != null) aiChatSendButton.gameObject.SetActive(false);
        if (aiChatBackButton != null) aiChatBackButton.gameObject.SetActive(false);
        HideOptions();
    }

    private void HideOverlayImmediate()
    {
        if (overlayCanvas == null)
        {
            return;
        }

        pulseImage.enabled = false;
        pulseImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, 0f);
        whiteImage.enabled = false;
        whiteImage.color = new Color(pageTint.r, pageTint.g, pageTint.b, 0f);
        menuCard.gameObject.SetActive(false);
        titleText.gameObject.SetActive(false);
        counterText.gameObject.SetActive(false);
        questionText.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);
        languagePromptText.gameObject.SetActive(false);
        hintScreenText.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);
        hintScreenBackButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        backButton.gameObject.SetActive(false);
        hintButton.gameObject.SetActive(false);
        aiButton.gameObject.SetActive(false);
        retryWrongButton.gameObject.SetActive(false);
        languageRoButton.gameObject.SetActive(false);
        languageEnButton.gameObject.SetActive(false);
        if (aiChatPanel != null) aiChatPanel.gameObject.SetActive(false);
        if (aiChatTitleText != null) aiChatTitleText.gameObject.SetActive(false);
        if (aiChatHistoryText != null) aiChatHistoryText.gameObject.SetActive(false);
        if (aiChatInput != null) aiChatInput.gameObject.SetActive(false);
        if (aiChatSendButton != null) aiChatSendButton.gameObject.SetActive(false);
        if (aiChatBackButton != null) aiChatBackButton.gameObject.SetActive(false);
        HideOptions();
    }

    private void ForceSceneOnlyVisibility()
    {
        HideOverlayImmediate();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(false);
        }

        SetCursorVisible(false);
    }

    private void ShowLeaveButton(bool visible)
    {
        leaveButton.onClick.RemoveAllListeners();
        if (visible)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
        leaveButton.gameObject.SetActive(visible);
    }

    private void OnLeaveClicked()
    {
        ForceSceneOnlyVisibility();
        leaveRequested = true;
    }

    private IEnumerator FadeImage(Image image, float from, float to, float duration, Color color)
    {
        image.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, duration));
            image.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, to, t));
            yield return null;
        }

        image.color = new Color(color.r, color.g, color.b, to);
        image.enabled = to > 0.0001f;
    }

    private void SetPulseAlpha(float alpha)
    {
        pulseImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, Mathf.Clamp01(alpha));
        pulseImage.enabled = alpha > 0.0001f;
    }

    private static void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void EnsureTriggerReady()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = true;
        }
    }
}

public static class CppQuestionPadCinematicBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachIfNeeded()
    {
        AttachToPad("CppQuestionPad");
    }

    private static void AttachToPad(string padName)
    {
        GameObject pad = GameObject.Find(padName);
        if (pad == null)
        {
            return;
        }

        CodeChallengePadCinematic codeChallenge = pad.GetComponent<CodeChallengePadCinematic>();
        if (codeChallenge != null)
        {
            codeChallenge.enabled = false;
        }

        Collider collider = pad.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
            collider.enabled = true;
        }

        if (pad.GetComponent<CppQuestionPadCinematic>() == null)
        {
            pad.AddComponent<CppQuestionPadCinematic>();
        }
    }
}

public class PortalLoopAnimator : MonoBehaviour
{
    private Transform outerRing;
    private Transform innerRing;
    private Transform core;
    private Light portalLight;
    private Color glowColor;

    public void Initialize(Color color)
    {
        glowColor = color;
        outerRing = transform.Find("OuterRing");
        innerRing = transform.Find("InnerRing");
        core = transform.Find("Core");
        portalLight = GetComponentInChildren<Light>(true);
    }

    private void Update()
    {
        float t = Time.time;

        if (outerRing != null)
        {
            outerRing.Rotate(Vector3.forward, 56f * Time.deltaTime, Space.Self);
        }

        if (innerRing != null)
        {
            innerRing.Rotate(Vector3.forward, -84f * Time.deltaTime, Space.Self);
        }

        if (core != null)
        {
            float pulse = 1f + Mathf.Sin(t * 3.6f) * 0.04f;
            core.localScale = new Vector3(1.45f, 2.05f, 0.16f) * pulse;
        }

        if (portalLight != null)
        {
            portalLight.intensity = 5.2f + Mathf.Sin(t * 4f) * 0.4f;
            portalLight.color = Color.Lerp(glowColor, Color.white, 0.25f + Mathf.Sin(t * 2.5f) * 0.12f);
        }
    }
}
