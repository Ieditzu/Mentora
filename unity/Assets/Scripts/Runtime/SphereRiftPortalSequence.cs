using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SphereRiftPortalSequence : MonoBehaviour
{
    private enum QuizLanguage
    {
        Romanian = 0,
        English = 1
    }

    private struct PythonOutputQuestion
    {
        public string PromptRo;
        public string PromptEn;
        public string Code;
        public string[] OptionsRo;
        public string[] OptionsEn;
        public string HintRo;
        public string HintEn;
        public int CorrectIndex;

        public PythonOutputQuestion(string promptRo, string promptEn, string code, string aRo, string bRo, string cRo, string aEn, string bEn, string cEn, string hintRo, string hintEn, int correctIndex)
        {
            PromptRo = promptRo;
            PromptEn = promptEn;
            Code = code;
            OptionsRo = new[] { aRo, bRo, cRo };
            OptionsEn = new[] { aEn, bEn, cEn };
            HintRo = hintRo;
            HintEn = hintEn;
            CorrectIndex = correctIndex;
        }
    }

    private struct PythonCodeChallenge
    {
        public string PromptRo;
        public string PromptEn;
        public string InitialCode;
        public string ExpectedCode;
        public string HintRo;
        public string HintEn;

        public PythonCodeChallenge(string promptRo, string promptEn, string initialCode, string expectedCode, string hintRo, string hintEn)
        {
            PromptRo = promptRo;
            PromptEn = promptEn;
            InitialCode = initialCode;
            ExpectedCode = expectedCode;
            HintRo = hintRo;
            HintEn = hintEn;
        }
    }

    [SerializeField] private float stationHeightOffset = 0.22f;
    [SerializeField] private float triggerRadius = 1.2f;
    [SerializeField] private float reenterCooldown = 2f;
    [SerializeField] private float fadeToWhiteDuration = 0.55f;
    [SerializeField] private float textFadeDelay = 0.08f;
    [SerializeField] private Color stationBaseColor = new Color(0.08f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color stationScreenColor = new Color(0.2f, 0.78f, 1f, 0.96f);
    [SerializeField] private Color stationGlowColor = new Color(0.85f, 0.95f, 1f, 1f);
    [SerializeField] private Color pythonBlueColor = new Color(0.2f, 0.45f, 0.78f, 1f);
    [SerializeField] private Color pythonYellowColor = new Color(1f, 0.84f, 0.2f, 1f);

    private static Canvas overlayCanvas;
    private static Image whiteImage;
    private static Image quizCardImage;
    private static Image codeCardImage;
    private static Text centerText;
    private static Text quizTitleText;
    private static Text quizPromptText;
    private static Text quizCodeText;
    private static Text quizFeedbackText;
    private static Text quizHintText;
    private static Button[] optionButtons;
    private static Text[] optionButtonTexts;
    private static Button nextButton;
    private static Text nextButtonText;
    private static Button previousButton;
    private static Text previousButtonText;
    private static Button hintButton;
    private static Text hintButtonText;
    private static Button hintBackButton;
    private static Text hintBackButtonText;
    private static Button leaveButton;
    private static Text leaveButtonText;
    private static Button verifyButton;
    private static Text verifyButtonText;
    private static Button languageRoButton;
    private static Text languageRoButtonText;
    private static Button languageEnButton;
    private static Text languageEnButtonText;
    private static InputField codeInputField;
    private static Text codeInputText;
    private static Text codeInputPlaceholder;

    private static readonly PythonOutputQuestion[] PythonQuestions =
    {
        new PythonOutputQuestion(
            "Output prediction",
            "Output prediction",
            "x = 4\nprint(x + 3)",
            "A. 43",
            "B. 7",
            "C. 1",
            "A. 43",
            "B. 7",
            "C. 1",
            "Adunarea se face inainte de afisare: 4 + 3 devine 7.",
            "Addition happens before printing: 4 + 3 becomes 7.",
            1),
        new PythonOutputQuestion(
            "Output prediction",
            "Output prediction",
            "numbers = [1, 2, 3]\nprint(numbers[0])",
            "A. 0",
            "B. 1",
            "C. 3",
            "A. 0",
            "B. 1",
            "C. 3",
            "Listele Python pornesc de la indexul 0, deci primul element este 1.",
            "Python lists start at index 0, so the first element is 1.",
            1),
        new PythonOutputQuestion(
            "Output prediction",
            "Output prediction",
            "name = \"Py\"\nprint(name * 2)",
            "A. Py2",
            "B. Py Py",
            "C. PyPy",
            "A. Py2",
            "B. Py Py",
            "C. PyPy",
            "Un string inmultit cu 2 se repeta de doua ori fara spatiu.",
            "A string multiplied by 2 repeats twice with no space.",
            2),
        new PythonOutputQuestion(
            "Output prediction",
            "Output prediction",
            "value = 10\nif value > 5:\n    print(\"big\")",
            "A. big",
            "B. 10",
            "C. nimic",
            "A. big",
            "B. 10",
            "C. nothing",
            "Conditia este adevarata, asa ca se executa print(\"big\").",
            "The condition is true, so print(\"big\") runs.",
            0),
        new PythonOutputQuestion(
            "Output prediction",
            "Output prediction",
            "total = 0\nfor n in range(3):\n    total += n\nprint(total)",
            "A. 3",
            "B. 6",
            "C. 2",
            "A. 3",
            "B. 6",
            "C. 2",
            "range(3) inseamna 0, 1, 2. Suma lor este 3.",
            "range(3) means 0, 1, 2. Their sum is 3.",
            2)
    };

    private static readonly PythonCodeChallenge[] PythonChallenges =
    {
        new PythonCodeChallenge(
            "Schimba variabila x astfel incat codul sa afiseze 10.",
            "Change variable x so the code prints 10.",
            "x = 4\nprint(x)",
            "x = 10\nprint(x)",
            "Trebuie modificata doar valoarea lui x.",
            "You only need to change the value of x."),
        new PythonCodeChallenge(
            "Fa variabila name sa afiseze Ana.",
            "Make variable name print Ana.",
            "name = \"Mihai\"\nprint(name)",
            "name = \"Ana\"\nprint(name)",
            "Schimba textul dintre ghilimele.",
            "Change the text inside the quotes."),
        new PythonCodeChallenge(
            "Modifica variabila total ca rezultatul afisat sa fie 12.",
            "Change variable total so the printed result becomes 12.",
            "total = 8\nprint(total + 1)",
            "total = 11\nprint(total + 1)",
            "Rezultatul final este total + 1.",
            "The final result is total + 1.")
    };

    private SphereCollider portalTrigger;
    private Transform stationRoot;
    private Transform stationPedestal;
    private Transform stationScreen;
    private Transform stationFrame;
    private Transform stationMarker;
    private Light stationLight;
    private bool running;
    private bool optionSelected;
    private bool answerCorrect;
    private bool nextRequested;
    private bool previousRequested;
    private bool hintRequested;
    private bool leaveRequested;
    private bool languageChosen;
    private bool verifyRequested;
    private QuizLanguage selectedLanguage = QuizLanguage.Romanian;

    private void Awake()
    {
        RemoveDuplicateSequenceComponents();
        RemoveDuplicateStationRoots();
        if (!Application.isPlaying)
        {
            EnsureStationVisual();
            EnsurePortalTrigger();
            return;
        }

        EnsureStationVisual();
        EnsurePortalTrigger();
        EnsureOverlay();
        ResetOverlay();
    }

    private void OnEnable()
    {
        RemoveDuplicateSequenceComponents();
        RemoveDuplicateStationRoots();
        if (!Application.isPlaying)
        {
            EnsureStationVisual();
            EnsurePortalTrigger();
        }
    }

    private void OnValidate()
    {
        RemoveDuplicateSequenceComponents();
        RemoveDuplicateStationRoots();
        if (!Application.isPlaying)
        {
            EnsureStationVisual();
            EnsurePortalTrigger();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AnimateStationVisual();

        if (running)
        {
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStartSequenceFromTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryStartSequenceFromTrigger(other);
    }

    private void TryStartSequenceFromTrigger(Collider other)
    {
        if (!Application.isPlaying || running || other == null || !IsPlayerCollider(other))
        {
            return;
        }

        if (!TryGetPlayer(out BeanController sphere, out FirstPersonControllerSimple fps, out Transform playerRoot))
        {
            return;
        }

        StartCoroutine(PlaySequence(sphere, fps, playerRoot, GetStationInteractionPoint()));
    }

    private IEnumerator PlaySequence(BeanController sphere, FirstPersonControllerSimple fps, Transform playerRoot, Vector3 stationPosition)
    {
        running = true;
        if (portalTrigger != null)
        {
            portalTrigger.enabled = false;
        }

        EnsureOverlay();
        ResetOverlay();
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(true);
        }

        SetPlayerLockState(sphere, fps, true, true);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(false);
        }

        Vector3 facingDirection = (stationPosition - playerRoot.position);
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            SetPlayerPositionAndRotation(sphere, fps, playerRoot.position, targetRotation);
        }

        yield return FadeWhite(0f, 1f, fadeToWhiteDuration);
        if (textFadeDelay > 0f)
        {
            yield return new WaitForSeconds(textFadeDelay);
        }

        yield return RunRightPortalPythonQuiz();

        yield return FinishPortalSequence(sphere, fps);
    }

    private void EnsureStationVisual()
    {
        RemoveDuplicateStationRoots();
        if (stationRoot != null && !NeedsStationRebuild())
        {
            CleanupDuplicateStationChildren();
            return;
        }

        Transform existing = transform.Find("PythonQuestionStation");
        if (existing != null)
        {
            DestroySafe(existing.gameObject);
        }

        stationRoot = new GameObject("PythonQuestionStation").transform;
        stationRoot.SetParent(transform, false);
        stationRoot.localPosition = new Vector3(0f, stationHeightOffset, 0f);
        stationRoot.localRotation = Quaternion.identity;

        stationPedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        stationPedestal.name = "Pedestal";
        stationPedestal.SetParent(stationRoot, false);
        stationPedestal.localScale = new Vector3(0.42f, 0.12f, 0.42f);
        stationPedestal.localPosition = new Vector3(0f, -0.08f, 0f);
        DestroySafe(stationPedestal.GetComponent<Collider>());
        ApplyEmissionMaterial(stationPedestal.gameObject, stationBaseColor, 1.4f);

        stationFrame = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        stationFrame.name = "SupportStem";
        stationFrame.SetParent(stationRoot, false);
        stationFrame.localScale = new Vector3(0.06f, 0.14f, 0.06f);
        stationFrame.localPosition = new Vector3(0f, 0.0f, 0.02f);
        DestroySafe(stationFrame.GetComponent<Collider>());
        ApplyEmissionMaterial(stationFrame.gameObject, stationBaseColor, 2.1f);

        stationScreen = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        stationScreen.name = "Screen";
        stationScreen.SetParent(stationRoot, false);
        stationScreen.localPosition = new Vector3(0f, 0.16f, -0.045f);
        stationScreen.localRotation = Quaternion.identity;
        stationScreen.localScale = new Vector3(0.36f, 0.22f, 0.03f);
        DestroySafe(stationScreen.GetComponent<Collider>());
        ApplyEmissionMaterial(stationScreen.gameObject, stationScreenColor, 5.5f);

        stationMarker = CreatePythonLogoVisual();
        stationMarker.name = "PythonLogoMarker";
        stationMarker.SetParent(stationRoot, false);
        stationMarker.localPosition = new Vector3(0f, 0.5f, 0f);
        stationMarker.localRotation = Quaternion.Euler(90f, 0f, 0f);
        stationMarker.localScale = new Vector3(0.11525f, 0.324f, 0.5f);
        if (stationMarker.GetComponent<PythonLogoFloatSpin>() == null)
        {
            stationMarker.gameObject.AddComponent<PythonLogoFloatSpin>();
        }

        GameObject lightRoot = new GameObject("StationLight");
        lightRoot.transform.SetParent(stationRoot, false);
        lightRoot.transform.localPosition = new Vector3(0f, 0.26f, 0f);
        stationLight = lightRoot.AddComponent<Light>();
        stationLight.type = LightType.Point;
        stationLight.range = 7f;
        stationLight.intensity = 3.2f;
        stationLight.color = stationGlowColor;
        CleanupDuplicateStationChildren();
    }

    private bool NeedsStationRebuild()
    {
        if (stationRoot == null)
        {
            return true;
        }

        int screenCount = 0;
        int markerCount = 0;
        for (int i = 0; i < stationRoot.childCount; i++)
        {
            string childName = stationRoot.GetChild(i).name;
            if (childName == "Screen")
            {
                screenCount++;
            }
            else if (childName == "PythonLogoMarker")
            {
                markerCount++;
            }
        }

        return screenCount != 1 || markerCount != 1;
    }

    private void RemoveDuplicateStationRoots()
    {
        Transform firstRoot = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "PythonQuestionStation")
            {
                continue;
            }

            if (firstRoot == null)
            {
                firstRoot = child;
                continue;
            }

            DestroySafe(child.gameObject);
        }

        stationRoot = firstRoot;
    }

    private void CleanupDuplicateStationChildren()
    {
        if (stationRoot == null)
        {
            return;
        }

        Transform firstScreen = null;
        Transform firstMarker = null;
        for (int i = stationRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = stationRoot.GetChild(i);
            if (child.name == "Screen")
            {
                if (firstScreen == null)
                {
                    firstScreen = child;
                }
                else
                {
                    DestroySafe(child.gameObject);
                }
            }
            else if (child.name == "PythonLogoMarker")
            {
                if (firstMarker == null)
                {
                    firstMarker = child;
                }
                else
                {
                    DestroySafe(child.gameObject);
                }
            }
        }

        if (firstScreen != null)
        {
            stationScreen = firstScreen;
        }

        if (firstMarker != null)
        {
            stationMarker = firstMarker;
        }
    }

    private void RemoveDuplicateSequenceComponents()
    {
        SphereRiftPortalSequence[] components = GetComponents<SphereRiftPortalSequence>();
        if (components.Length <= 1)
        {
            return;
        }

        SphereRiftPortalSequence keeper = components[0];
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == keeper)
            {
                continue;
            }

            DestroySafe(components[i]);
        }
    }

    private void AnimateStationVisual()
    {
        if (stationRoot == null)
        {
            return;
        }

        float time = Time.time;
        stationRoot.localPosition = new Vector3(0f, stationHeightOffset, 0f);
        if (stationMarker != null)
        {
            stationMarker.localPosition = new Vector3(0f, 0.5f, 0f);
            stationMarker.localScale = new Vector3(0.11525f, 0.324f, 0.5f);
        }

        if (stationScreen != null)
        {
            float screenPulse = 1f + Mathf.Sin(time * 2.6f) * 0.04f;
            stationScreen.localScale = new Vector3(0.36f, 0.22f, 0.03f * screenPulse);
        }

        if (stationLight != null)
        {
            stationLight.intensity = 3.2f + Mathf.Sin(time * 2.4f) * 0.45f;
            stationLight.range = 7f + Mathf.Sin(time * 1.9f) * 0.35f;
        }
    }

    private Vector3 GetStationInteractionPoint()
    {
        if (stationRoot == null)
        {
            return transform.position + Vector3.up * stationHeightOffset;
        }

        if (stationScreen != null)
        {
            return stationScreen.position + (transform.forward * 0.18f);
        }

        return stationRoot.position + (transform.forward * 0.35f);
    }

    private Transform CreatePythonLogoVisual()
    {
#if UNITY_EDITOR
        GameObject pythonAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/python/source/python.fbx");
        if (pythonAsset != null)
        {
            GameObject pythonInstance = Instantiate(pythonAsset);
            pythonInstance.name = "PythonLogo";
            foreach (Collider collider in pythonInstance.GetComponentsInChildren<Collider>(true))
            {
                DestroySafe(collider);
            }

            return pythonInstance.transform;
        }
#endif

        Transform fallbackRoot = new GameObject("PythonLogo").transform;

        Transform pythonTop = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        pythonTop.name = "PythonTop";
        pythonTop.SetParent(fallbackRoot, false);
        pythonTop.localScale = new Vector3(0.13f, 0.08f, 0.026f);
        pythonTop.localRotation = Quaternion.Euler(0f, 0f, 90f);
        pythonTop.localPosition = new Vector3(-0.036f, 0.052f, 0f);
        DestroySafe(pythonTop.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonTop.gameObject, pythonBlueColor, 4.8f);

        Transform pythonTopHead = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        pythonTopHead.name = "PythonTopHead";
        pythonTopHead.SetParent(fallbackRoot, false);
        pythonTopHead.localScale = new Vector3(0.082f, 0.082f, 0.026f);
        pythonTopHead.localRotation = Quaternion.identity;
        pythonTopHead.localPosition = new Vector3(0.07f, 0.034f, 0f);
        DestroySafe(pythonTopHead.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonTopHead.gameObject, pythonBlueColor, 4.8f);

        Transform pythonTopBridge = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        pythonTopBridge.name = "PythonTopBridge";
        pythonTopBridge.SetParent(fallbackRoot, false);
        pythonTopBridge.localScale = new Vector3(0.092f, 0.038f, 0.026f);
        pythonTopBridge.localPosition = new Vector3(0.015f, 0.045f, 0f);
        DestroySafe(pythonTopBridge.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonTopBridge.gameObject, pythonBlueColor, 4.8f);

        Transform pythonBottom = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        pythonBottom.name = "PythonBottom";
        pythonBottom.SetParent(fallbackRoot, false);
        pythonBottom.localScale = new Vector3(0.13f, 0.08f, 0.026f);
        pythonBottom.localRotation = Quaternion.Euler(0f, 0f, 90f);
        pythonBottom.localPosition = new Vector3(0.036f, -0.052f, 0f);
        DestroySafe(pythonBottom.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonBottom.gameObject, pythonYellowColor, 4.8f);

        Transform pythonBottomHead = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        pythonBottomHead.name = "PythonBottomHead";
        pythonBottomHead.SetParent(fallbackRoot, false);
        pythonBottomHead.localScale = new Vector3(0.082f, 0.082f, 0.026f);
        pythonBottomHead.localRotation = Quaternion.identity;
        pythonBottomHead.localPosition = new Vector3(-0.07f, -0.034f, 0f);
        DestroySafe(pythonBottomHead.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonBottomHead.gameObject, pythonYellowColor, 4.8f);

        Transform pythonBottomBridge = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        pythonBottomBridge.name = "PythonBottomBridge";
        pythonBottomBridge.SetParent(fallbackRoot, false);
        pythonBottomBridge.localScale = new Vector3(0.092f, 0.038f, 0.026f);
        pythonBottomBridge.localPosition = new Vector3(-0.015f, -0.045f, 0f);
        DestroySafe(pythonBottomBridge.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonBottomBridge.gameObject, pythonYellowColor, 4.8f);

        Transform pythonTopEye = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        pythonTopEye.name = "PythonTopEye";
        pythonTopEye.SetParent(fallbackRoot, false);
        pythonTopEye.localScale = new Vector3(0.015f, 0.015f, 0.03f);
        pythonTopEye.localPosition = new Vector3(0.085f, 0.062f, -0.002f);
        DestroySafe(pythonTopEye.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonTopEye.gameObject, stationGlowColor, 5.8f);

        Transform pythonBottomEye = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        pythonBottomEye.name = "PythonBottomEye";
        pythonBottomEye.SetParent(fallbackRoot, false);
        pythonBottomEye.localScale = new Vector3(0.015f, 0.015f, 0.03f);
        pythonBottomEye.localPosition = new Vector3(-0.085f, -0.062f, -0.002f);
        DestroySafe(pythonBottomEye.GetComponent<Collider>());
        ApplyEmissionMaterial(pythonBottomEye.gameObject, stationGlowColor, 5.8f);

        return fallbackRoot;
    }

    private void EnsurePortalTrigger()
    {
        portalTrigger = GetComponent<SphereCollider>();
        if (portalTrigger == null)
        {
            portalTrigger = gameObject.AddComponent<SphereCollider>();
        }

        portalTrigger.isTrigger = true;
        portalTrigger.enabled = true;
        portalTrigger.radius = Mathf.Clamp(triggerRadius, 0.04f, 0.06f);
        portalTrigger.center = new Vector3(0f, stationHeightOffset + 0.5f, 0f);
    }

    private static bool TryGetPlayer(out BeanController sphere, out FirstPersonControllerSimple fps, out Transform playerRoot)
    {
        sphere = Object.FindObjectOfType<BeanController>();
        fps = Object.FindObjectOfType<FirstPersonControllerSimple>();
        playerRoot = fps != null ? fps.transform : sphere != null ? sphere.transform : null;
        return playerRoot != null;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other.GetComponentInParent<BeanController>() != null)
        {
            return true;
        }

        if (other.GetComponentInParent<FirstPersonControllerSimple>() != null)
        {
            return true;
        }

        return other.GetComponent<CharacterController>() != null
            || other.GetComponentInParent<CharacterController>() != null;
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

    private static void SetPlayerPositionAndRotation(BeanController sphere, FirstPersonControllerSimple fps, Vector3 position, Quaternion rotation)
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

    private void EnsureOverlay()
    {
        if (overlayCanvas != null && whiteImage != null && centerText != null && quizTitleText != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("SphereRiftPortalCanvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("SphereRiftPortalCanvas");
        }

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        if (overlayCanvas == null)
        {
            overlayCanvas = canvasObject.AddComponent<Canvas>();
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 16000;

        if (canvasObject.GetComponent<CanvasScaler>() == null)
        {
            canvasObject.AddComponent<CanvasScaler>();
        }

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        whiteImage = EnsureImage(canvasObject.transform, "WhiteImage", new Color(0.9f, 0.95f, 0.99f, 1f));
        Stretch(whiteImage.rectTransform);

        quizCardImage = EnsureImage(canvasObject.transform, "QuizCardImage", new Color(0.95f, 0.98f, 1f, 0.97f));
        SetRect(quizCardImage.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1040f, 560f));

        codeCardImage = EnsureImage(canvasObject.transform, "CodeCardImage", new Color(0.15f, 0.21f, 0.3f, 0.97f));
        SetRect(codeCardImage.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(820f, 138f));

        centerText = EnsureText(canvasObject.transform, "CenterText", "test", 72, new Color(0.08f, 0.08f, 0.08f, 1f));
        Stretch(centerText.rectTransform);

        quizTitleText = EnsureText(canvasObject.transform, "QuizTitleText", "Output prediction", 38, new Color(0.06f, 0.16f, 0.26f, 1f));
        SetRect(quizTitleText.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(860f, 56f));

        quizPromptText = EnsureText(canvasObject.transform, "QuizPromptText", "Utilizatorul vede cod Python scurt si trebuie sa spuna ce afiseaza.", 21, new Color(0.15f, 0.18f, 0.24f, 1f));
        SetRect(quizPromptText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(860f, 70f));
        quizPromptText.alignment = TextAnchor.UpperLeft;

        quizCodeText = EnsureText(canvasObject.transform, "QuizCodeText", string.Empty, 26, new Color(0.93f, 0.97f, 1f, 1f));
        SetRect(quizCodeText.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(760f, 104f));
        quizCodeText.alignment = TextAnchor.UpperLeft;

        quizHintText = EnsureText(canvasObject.transform, "QuizHintText", string.Empty, 19, new Color(0.18f, 0.31f, 0.4f, 1f));
        SetRect(quizHintText.rectTransform, new Vector2(0.5f, 0.46f), new Vector2(760f, 200f));
        quizHintText.alignment = TextAnchor.MiddleCenter;

        quizFeedbackText = EnsureText(canvasObject.transform, "QuizFeedbackText", string.Empty, 21, new Color(0.11f, 0.15f, 0.2f, 1f));
        SetRect(quizFeedbackText.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(860f, 40f));
        quizFeedbackText.alignment = TextAnchor.MiddleLeft;

        optionButtons = new Button[3];
        optionButtonTexts = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            optionButtons[i] = EnsureButton(canvasObject.transform, "OptionButton" + i, new Vector2(0.5f, 0.45f - (i * 0.082f)), new Vector2(620f, 50f), new Color(0.25f, 0.46f, 0.68f, 0.97f), "A", 21);
            optionButtonTexts[i] = optionButtons[i].GetComponentInChildren<Text>(true);
        }

        previousButton = EnsureButton(canvasObject.transform, "PreviousButton", new Vector2(0.35f, 0.1f), new Vector2(180f, 50f), new Color(0.36f, 0.49f, 0.64f, 0.96f), "Previous", 21);
        previousButtonText = previousButton.GetComponentInChildren<Text>(true);
        hintButton = EnsureButton(canvasObject.transform, "HintButton", new Vector2(0.51f, 0.1f), new Vector2(160f, 50f), new Color(0.31f, 0.67f, 0.71f, 0.96f), "Hint", 21);
        hintButtonText = hintButton.GetComponentInChildren<Text>(true);
        hintBackButton = EnsureButton(canvasObject.transform, "HintBackButton", new Vector2(0.5f, 0.14f), new Vector2(220f, 50f), new Color(0.32f, 0.47f, 0.67f, 0.96f), "Back", 21);
        hintBackButtonText = hintBackButton.GetComponentInChildren<Text>(true);
        nextButton = EnsureButton(canvasObject.transform, "NextButton", new Vector2(0.67f, 0.1f), new Vector2(180f, 50f), new Color(0.29f, 0.66f, 0.41f, 0.96f), "Next", 21);
        nextButtonText = nextButton.GetComponentInChildren<Text>(true);
        verifyButton = EnsureButton(canvasObject.transform, "VerifyButton", new Vector2(0.67f, 0.1f), new Vector2(180f, 50f), new Color(0.84f, 0.58f, 0.24f, 0.97f), "Verify", 21);
        verifyButtonText = verifyButton.GetComponentInChildren<Text>(true);
        leaveButton = EnsureButton(canvasObject.transform, "LeaveButton", new Vector2(0.14f, 0.1f), new Vector2(150f, 50f), new Color(0.27f, 0.29f, 0.35f, 0.96f), "Leave", 20);
        leaveButtonText = leaveButton.GetComponentInChildren<Text>(true);
        languageRoButton = EnsureButton(canvasObject.transform, "LanguageRoButton", new Vector2(0.43f, 0.62f), new Vector2(180f, 48f), new Color(0.28f, 0.56f, 0.8f, 0.95f), "Romana", 20);
        languageRoButtonText = languageRoButton.GetComponentInChildren<Text>(true);
        languageEnButton = EnsureButton(canvasObject.transform, "LanguageEnButton", new Vector2(0.57f, 0.62f), new Vector2(180f, 48f), new Color(0.18f, 0.44f, 0.68f, 0.95f), "English", 20);
        languageEnButtonText = languageEnButton.GetComponentInChildren<Text>(true);

        codeInputField = EnsureInputField(canvasObject.transform, "CodeInputField", new Vector2(0.5f, 0.61f), new Vector2(760f, 110f), out codeInputText, out codeInputPlaceholder);
    }

    private void ResetOverlay()
    {
        if (overlayCanvas == null || whiteImage == null || centerText == null)
        {
            return;
        }

        overlayCanvas.gameObject.SetActive(false);
        whiteImage.gameObject.SetActive(false);
        if (quizCardImage != null)
        {
            quizCardImage.gameObject.SetActive(false);
        }
        if (codeCardImage != null)
        {
            codeCardImage.gameObject.SetActive(false);
        }
        centerText.gameObject.SetActive(false);
        quizTitleText.gameObject.SetActive(false);
        quizPromptText.gameObject.SetActive(false);
        quizCodeText.gameObject.SetActive(false);
        quizHintText.gameObject.SetActive(false);
        quizFeedbackText.gameObject.SetActive(false);
        if (optionButtons != null)
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] != null)
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }
        }
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }
        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(false);
        }
        if (verifyButton != null)
        {
            verifyButton.gameObject.SetActive(false);
        }
        if (hintButton != null)
        {
            hintButton.gameObject.SetActive(false);
        }
        if (hintBackButton != null)
        {
            hintBackButton.gameObject.SetActive(false);
        }
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false);
        }
        if (languageRoButton != null)
        {
            languageRoButton.gameObject.SetActive(false);
        }
        if (languageEnButton != null)
        {
            languageEnButton.gameObject.SetActive(false);
        }
        if (codeInputField != null)
        {
            codeInputField.gameObject.SetActive(false);
            codeInputField.text = string.Empty;
        }

        Color clear = Color.white;
        clear.a = 0f;
        whiteImage.color = clear;
    }

    private IEnumerator FadeWhite(float from, float to, float duration)
    {
        if (whiteImage == null)
        {
            yield break;
        }

        Color color = whiteImage.color;
        color.a = from;
        whiteImage.color = color;
        whiteImage.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / Mathf.Max(0.01f, duration));
            color.a = Mathf.Lerp(from, to, t);
            whiteImage.color = color;
            yield return null;
        }

        color.a = to;
        whiteImage.color = color;
    }

    private IEnumerator RunRightPortalPythonQuiz()
    {
        leaveRequested = false;
        yield return PromptQuizLanguage();
        if (leaveRequested)
        {
            yield break;
        }

        quizTitleText.gameObject.SetActive(true);
        quizPromptText.gameObject.SetActive(true);
        quizCodeText.gameObject.SetActive(true);
        quizHintText.gameObject.SetActive(true);
        quizFeedbackText.gameObject.SetActive(true);
        if (quizCardImage != null)
        {
            quizCardImage.gameObject.SetActive(true);
        }
        if (codeCardImage != null)
        {
            codeCardImage.gameObject.SetActive(true);
        }
        ShowLeaveButton(true);
        bool[] wrongAnswers = new bool[PythonQuestions.Length];
        List<int> activeQuestions = CreateQuestionIndexList(wrongAnswers, false);
        bool firstRound = true;

        while (activeQuestions.Count > 0 && !leaveRequested)
        {
            for (int batchIndex = 0; batchIndex < activeQuestions.Count && !leaveRequested; batchIndex++)
            {
                int questionId = activeQuestions[batchIndex];
                PythonOutputQuestion question = PythonQuestions[questionId];
                optionSelected = false;
                answerCorrect = false;
                nextRequested = false;
                previousRequested = false;
                hintRequested = false;
                bool hintScreenOpen = false;

                ApplyQuizButtonLabels();
                quizTitleText.text = Localize("Ce va afisa codul?", "What will this code display?");
                quizPromptText.text = Localize("Utilizatorul vede cod Python scurt si trebuie sa spuna ce afiseaza.", "The player sees short Python code and must say what it prints.");
                quizPromptText.alignment = TextAnchor.UpperLeft;
                quizCodeText.text = question.Code;
                quizHintText.text = string.Empty;
                quizFeedbackText.text = string.Empty;
                quizFeedbackText.color = new Color(0.1f, 0.1f, 0.1f, 1f);

                for (int optionIndex = 0; optionIndex < optionButtons.Length; optionIndex++)
                {
                    int capturedIndex = optionIndex;
                    optionButtons[optionIndex].onClick.RemoveAllListeners();
                    optionButtons[optionIndex].onClick.AddListener(() => SelectQuizOption(capturedIndex, question.CorrectIndex));
                    optionButtons[optionIndex].gameObject.SetActive(true);
                    optionButtons[optionIndex].interactable = true;
                    optionButtonTexts[optionIndex].text = selectedLanguage == QuizLanguage.Romanian ? question.OptionsRo[optionIndex] : question.OptionsEn[optionIndex];
                }

                previousButton.gameObject.SetActive(batchIndex > 0);
                previousButton.onClick.RemoveAllListeners();
                previousButton.onClick.AddListener(() => previousRequested = true);
                hintButton.gameObject.SetActive(true);
                hintButton.onClick.RemoveAllListeners();
                hintButton.onClick.AddListener(() => hintRequested = true);
                hintBackButton.gameObject.SetActive(false);
                nextButton.gameObject.SetActive(false);

                while (!optionSelected && !previousRequested && !leaveRequested)
                {
                    if (hintRequested)
                    {
                        hintRequested = false;
                        hintScreenOpen = true;
                        quizTitleText.text = Localize("Hint", "Hint");
                        quizPromptText.text = string.Empty;
                        quizCodeText.gameObject.SetActive(false);
                        for (int i = 0; i < optionButtons.Length; i++)
                        {
                            optionButtons[i].gameObject.SetActive(false);
                        }
                        previousButton.gameObject.SetActive(false);
                        hintButton.gameObject.SetActive(false);
                        nextButton.gameObject.SetActive(false);
                        if (codeCardImage != null)
                        {
                            codeCardImage.gameObject.SetActive(false);
                        }
                        quizHintText.text = selectedLanguage == QuizLanguage.Romanian ? question.HintRo : question.HintEn;
                        quizHintText.alignment = TextAnchor.MiddleCenter;
                        hintBackButton.gameObject.SetActive(true);
                        hintBackButton.onClick.RemoveAllListeners();
                        hintBackButton.onClick.AddListener(() => hintScreenOpen = false);
                    }

                    if (hintScreenOpen)
                    {
                        yield return null;
                        if (!hintScreenOpen)
                        {
                            quizTitleText.text = Localize("Ce va afisa codul?", "What will this code display?");
                            quizPromptText.text = Localize("Utilizatorul vede cod Python scurt si trebuie sa spuna ce afiseaza.", "The player sees short Python code and must say what it prints.");
                            quizPromptText.alignment = TextAnchor.UpperLeft;
                            quizCodeText.gameObject.SetActive(true);
                            if (codeCardImage != null)
                            {
                                codeCardImage.gameObject.SetActive(true);
                            }
                            for (int i = 0; i < optionButtons.Length; i++)
                            {
                                optionButtons[i].gameObject.SetActive(true);
                            }
                            previousButton.gameObject.SetActive(batchIndex > 0);
                            hintButton.gameObject.SetActive(true);
                            quizHintText.text = string.Empty;
                            hintBackButton.gameObject.SetActive(false);
                        }
                        continue;
                    }
                    yield return null;
                }

                if (leaveRequested)
                {
                    yield break;
                }

                if (previousRequested)
                {
                    batchIndex = Mathf.Max(-1, batchIndex - 2);
                    continue;
                }

                if (answerCorrect)
                {
                    wrongAnswers[questionId] = false;
                    quizFeedbackText.text = Localize("Corect.", "Correct.");
                    quizFeedbackText.color = new Color(0.18f, 0.58f, 0.32f, 1f);
                    nextButton.gameObject.SetActive(true);
                    nextButton.onClick.RemoveAllListeners();
                    nextButton.onClick.AddListener(() => nextRequested = true);

                    while (!nextRequested && !previousRequested && !leaveRequested)
                    {
                        if (hintRequested)
                        {
                            hintRequested = false;
                            hintScreenOpen = true;
                            quizTitleText.text = Localize("Hint", "Hint");
                            quizPromptText.text = string.Empty;
                            quizCodeText.gameObject.SetActive(false);
                            nextButton.gameObject.SetActive(false);
                            previousButton.gameObject.SetActive(false);
                            hintButton.gameObject.SetActive(false);
                            if (codeCardImage != null)
                            {
                                codeCardImage.gameObject.SetActive(false);
                            }
                            quizHintText.text = selectedLanguage == QuizLanguage.Romanian ? question.HintRo : question.HintEn;
                            quizHintText.alignment = TextAnchor.MiddleCenter;
                            hintBackButton.gameObject.SetActive(true);
                            hintBackButton.onClick.RemoveAllListeners();
                            hintBackButton.onClick.AddListener(() => hintScreenOpen = false);
                        }
                        if (hintScreenOpen)
                        {
                            yield return null;
                            if (!hintScreenOpen)
                            {
                                quizTitleText.text = Localize("Ce va afisa codul?", "What will this code display?");
                                quizPromptText.text = Localize("Utilizatorul vede cod Python scurt si trebuie sa spuna ce afiseaza.", "The player sees short Python code and must say what it prints.");
                                quizPromptText.alignment = TextAnchor.UpperLeft;
                                quizCodeText.gameObject.SetActive(true);
                                if (codeCardImage != null)
                                {
                                    codeCardImage.gameObject.SetActive(true);
                                }
                                previousButton.gameObject.SetActive(batchIndex > 0);
                                hintButton.gameObject.SetActive(true);
                                nextButton.gameObject.SetActive(true);
                                quizHintText.text = string.Empty;
                                hintBackButton.gameObject.SetActive(false);
                            }
                            continue;
                        }
                        yield return null;
                    }

                    if (leaveRequested)
                    {
                        yield break;
                    }

                    if (previousRequested)
                    {
                        batchIndex = Mathf.Max(-1, batchIndex - 2);
                        continue;
                    }

                    nextButton.gameObject.SetActive(false);
                    continue;
                }

                wrongAnswers[questionId] = true;
                quizFeedbackText.text = Localize("Nu este corect.", "That is not correct.");
                quizFeedbackText.color = new Color(0.78f, 0.2f, 0.2f, 1f);
                nextButton.gameObject.SetActive(true);
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(() => nextRequested = true);

                while (!nextRequested && !leaveRequested)
                {
                    yield return null;
                }

                if (leaveRequested)
                {
                    yield break;
                }

                nextButton.gameObject.SetActive(false);
            }

            int wrongCount = CountWrongAnswers(wrongAnswers);
            int correctCount = PythonQuestions.Length - wrongCount;
            centerText.fontSize = 30;
            centerText.alignment = TextAnchor.MiddleCenter;
            centerText.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(true);
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => nextRequested = true);

            if (wrongCount > 0)
            {
                centerText.text = firstRound
                    ? Localize(
                        "Rezultat dupa 5 intrebari: " + correctCount + "/" + PythonQuestions.Length + ". Apasa butonul ca sa refaci doar intrebarile gresite.",
                        "Result after 5 questions: " + correctCount + "/" + PythonQuestions.Length + ". Press the button to retry only the wrong questions.")
                    : Localize(
                        "Rezultat curent: " + correctCount + "/" + PythonQuestions.Length + ". Mai ai intrebari gresite. Apasa butonul ca sa le refaci.",
                        "Current result: " + correctCount + "/" + PythonQuestions.Length + ". You still have wrong questions. Press the button to retry them.");
                nextButtonText.text = Localize("Refa greselile", "Retry wrong");
            }
            else
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
                previousButton.gameObject.SetActive(false);
                hintButton.gameObject.SetActive(false);
                hintBackButton.gameObject.SetActive(false);
                nextButton.gameObject.SetActive(false);
                quizTitleText.gameObject.SetActive(false);
                quizPromptText.gameObject.SetActive(false);
                quizCodeText.gameObject.SetActive(false);
                quizHintText.gameObject.SetActive(false);
                quizFeedbackText.gameObject.SetActive(false);
                if (codeCardImage != null)
                {
                    codeCardImage.gameObject.SetActive(false);
                }
                yield return RunChallengeStage();
                yield break;
            }

            SetRect(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.16f), new Vector2(220f, 52f));

            while (!nextRequested && !leaveRequested)
            {
                yield return null;
            }

            if (leaveRequested)
            {
                yield break;
            }

            centerText.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            firstRound = false;

            if (wrongCount <= 0)
            {
                break;
            }
            activeQuestions = CreateQuestionIndexList(wrongAnswers, true);
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].gameObject.SetActive(false);
        }
        nextButton.gameObject.SetActive(false);
        previousButton.gameObject.SetActive(false);
        hintButton.gameObject.SetActive(false);
        hintBackButton.gameObject.SetActive(false);
        ShowLeaveButton(false);
        quizTitleText.gameObject.SetActive(false);
        quizPromptText.gameObject.SetActive(false);
        quizCodeText.gameObject.SetActive(false);
        quizHintText.gameObject.SetActive(false);
        quizFeedbackText.gameObject.SetActive(false);
        if (codeCardImage != null)
        {
            codeCardImage.gameObject.SetActive(false);
        }

    }

    private IEnumerator RunChallengeStage()
    {
        nextRequested = false;
        verifyRequested = false;
        hintRequested = false;
        yield return PlayChallengeIntro();
        if (leaveRequested)
        {
            yield break;
        }

        for (int i = 0; i < PythonChallenges.Length && !leaveRequested; i++)
        {
            PythonCodeChallenge challenge = PythonChallenges[i];
            nextRequested = false;
            verifyRequested = false;
            hintRequested = false;
            bool hintScreenOpen = false;

            if (quizCardImage != null) quizCardImage.gameObject.SetActive(true);
            if (codeCardImage != null) codeCardImage.gameObject.SetActive(true);
            quizTitleText.gameObject.SetActive(true);
            quizPromptText.gameObject.SetActive(true);
            quizCodeText.gameObject.SetActive(false);
            quizHintText.gameObject.SetActive(true);
            quizFeedbackText.gameObject.SetActive(true);
            codeInputField.gameObject.SetActive(true);
            verifyButton.gameObject.SetActive(true);
            nextButton.gameObject.SetActive(false);
            hintButton.gameObject.SetActive(true);
            hintBackButton.gameObject.SetActive(false);
            previousButton.gameObject.SetActive(false);
            ShowLeaveButton(true);

            quizTitleText.text = Localize("Challenge Python", "Python Challenge");
            quizPromptText.text = selectedLanguage == QuizLanguage.Romanian ? challenge.PromptRo : challenge.PromptEn;
            quizPromptText.alignment = TextAnchor.UpperLeft;
            quizHintText.text = string.Empty;
            quizFeedbackText.text = string.Empty;
            quizFeedbackText.color = new Color(0.11f, 0.15f, 0.2f, 1f);
            codeInputField.text = challenge.InitialCode;
            if (codeInputPlaceholder != null)
            {
                codeInputPlaceholder.text = Localize("Scrie codul aici...", "Write the code here...");
            }

            verifyButton.onClick.RemoveAllListeners();
            verifyButton.onClick.AddListener(() => verifyRequested = true);
            hintButton.onClick.RemoveAllListeners();
            hintButton.onClick.AddListener(() => hintRequested = true);

            while (!nextRequested && !leaveRequested)
            {
                if (hintRequested)
                {
                    hintRequested = false;
                    hintScreenOpen = true;
                    quizTitleText.text = Localize("Hint challenge", "Challenge hint");
                    quizPromptText.text = string.Empty;
                    if (codeCardImage != null) codeCardImage.gameObject.SetActive(false);
                    codeInputField.gameObject.SetActive(false);
                    verifyButton.gameObject.SetActive(false);
                    nextButton.gameObject.SetActive(false);
                    hintButton.gameObject.SetActive(false);
                    quizHintText.text = selectedLanguage == QuizLanguage.Romanian ? challenge.HintRo : challenge.HintEn;
                    quizHintText.alignment = TextAnchor.MiddleCenter;
                    hintBackButton.gameObject.SetActive(true);
                    hintBackButton.onClick.RemoveAllListeners();
                    hintBackButton.onClick.AddListener(() => hintScreenOpen = false);
                }

                if (hintScreenOpen)
                {
                    yield return null;
                    if (!hintScreenOpen)
                    {
                        quizTitleText.text = Localize("Challenge Python", "Python Challenge");
                        quizPromptText.text = selectedLanguage == QuizLanguage.Romanian ? challenge.PromptRo : challenge.PromptEn;
                        quizPromptText.alignment = TextAnchor.UpperLeft;
                        quizHintText.text = string.Empty;
                        if (codeCardImage != null) codeCardImage.gameObject.SetActive(true);
                        codeInputField.gameObject.SetActive(true);
                        verifyButton.gameObject.SetActive(true);
                        hintButton.gameObject.SetActive(true);
                        hintBackButton.gameObject.SetActive(false);
                    }
                    continue;
                }

                if (verifyRequested)
                {
                    verifyRequested = false;
                    if (NormalizeCode(codeInputField.text) == NormalizeCode(challenge.ExpectedCode))
                    {
                        quizFeedbackText.text = Localize("Corect. Apasa Next.", "Correct. Press Next.");
                        quizFeedbackText.color = new Color(0.18f, 0.58f, 0.32f, 1f);
                        nextButton.gameObject.SetActive(true);
                        nextButtonText.text = i == PythonChallenges.Length - 1 ? Localize("Final", "Finish") : Localize("Next", "Next");
                        nextButton.onClick.RemoveAllListeners();
                        nextButton.onClick.AddListener(() => nextRequested = true);
                    }
                    else
                    {
                        quizFeedbackText.text = Localize("Nu este corect. Mai incearca.", "That is not correct. Try again.");
                        quizFeedbackText.color = new Color(0.78f, 0.2f, 0.2f, 1f);
                    }
                }

                yield return null;
            }
        }

        if (!leaveRequested)
        {
            if (quizCardImage != null) quizCardImage.gameObject.SetActive(false);
            if (codeCardImage != null) codeCardImage.gameObject.SetActive(false);
            quizTitleText.gameObject.SetActive(false);
            quizPromptText.gameObject.SetActive(false);
            quizHintText.gameObject.SetActive(false);
            quizFeedbackText.gameObject.SetActive(false);
            codeInputField.gameObject.SetActive(false);
            verifyButton.gameObject.SetActive(false);
            nextButton.gameObject.SetActive(false);
            hintButton.gameObject.SetActive(false);
            hintBackButton.gameObject.SetActive(false);
            centerText.fontSize = 34;
            centerText.alignment = TextAnchor.MiddleCenter;
            centerText.text = Localize("Ai terminat si challenge-urile Python.", "You finished the Python challenges.");
            centerText.gameObject.SetActive(true);
            while (!leaveRequested)
            {
                yield return null;
            }
        }
    }

    private IEnumerator PlayChallengeIntro()
    {
        nextRequested = false;
        if (whiteImage != null)
        {
            whiteImage.gameObject.SetActive(true);
            whiteImage.color = new Color(1f, 1f, 1f, 1f);
        }
        if (quizCardImage != null)
        {
            quizCardImage.gameObject.SetActive(false);
        }
        if (codeCardImage != null)
        {
            codeCardImage.gameObject.SetActive(false);
        }
        ShowLeaveButton(true);
        centerText.gameObject.SetActive(true);
        centerText.fontSize = 34;
        centerText.alignment = TextAnchor.MiddleCenter;
        centerText.text = Localize(
            "Ai trecut primul nivel.\nUrmeaza challenge-uri Python in care modifici variabile si verifici codul.",
            "You passed the first level.\nNext come Python challenges where you modify variables and verify the code.");
        centerText.color = new Color(0.05f, 0.05f, 0.05f, 0f);
        quizTitleText.gameObject.SetActive(false);
        quizPromptText.gameObject.SetActive(false);
        quizHintText.gameObject.SetActive(false);
        quizFeedbackText.gameObject.SetActive(false);
        quizCodeText.gameObject.SetActive(false);
        RectTransform textRect = centerText.rectTransform;
        Vector2 start = new Vector2(0f, -140f);
        Vector2 end = Vector2.zero;
        textRect.anchoredPosition = start;

        float elapsed = 0f;
        while (elapsed < 0.7f)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(elapsed / 0.7f);
            textRect.anchoredPosition = Vector2.Lerp(start, end, t);
            centerText.color = new Color(0.05f, 0.05f, 0.05f, Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        nextButton.gameObject.SetActive(true);
        nextButtonText.text = Localize("Next", "Next");
        SetRect(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.16f), new Vector2(180f, 50f));
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(() => nextRequested = true);

        while (!nextRequested && !leaveRequested)
        {
            yield return null;
        }

        centerText.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
    }

    private static List<int> CreateQuestionIndexList(bool[] wrongAnswers, bool onlyWrong)
    {
        List<int> result = new List<int>();
        for (int i = 0; i < PythonQuestions.Length; i++)
        {
            if (!onlyWrong || wrongAnswers[i])
            {
                result.Add(i);
            }
        }

        return result;
    }

    private static int CountWrongAnswers(bool[] wrongAnswers)
    {
        int wrongCount = 0;
        for (int i = 0; i < wrongAnswers.Length; i++)
        {
            if (wrongAnswers[i])
            {
                wrongCount++;
            }
        }

        return wrongCount;
    }

    private IEnumerator FinishPortalSequence(BeanController sphere, FirstPersonControllerSimple fps)
    {
        ForceSceneOnlyVisibility();

        SetPlayerLockState(sphere, fps, false, false);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(true);
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        yield return new WaitForSeconds(Mathf.Max(0f, reenterCooldown));
        if (portalTrigger != null)
        {
            portalTrigger.enabled = true;
        }
        leaveRequested = false;
        running = false;
    }

    private void SelectQuizOption(int selectedIndex, int correctIndex)
    {
        optionSelected = true;
        answerCorrect = selectedIndex == correctIndex;
    }

    private IEnumerator PromptQuizLanguage()
    {
        languageChosen = false;
        ShowLeaveButton(true);
        if (quizCardImage != null)
        {
            quizCardImage.gameObject.SetActive(true);
        }
        if (codeCardImage != null)
        {
            codeCardImage.gameObject.SetActive(false);
        }
        languageRoButton.gameObject.SetActive(true);
        languageEnButton.gameObject.SetActive(true);
        quizTitleText.gameObject.SetActive(true);
        quizPromptText.gameObject.SetActive(true);
        quizTitleText.text = Localize("Alege limba", "Choose language");
        quizPromptText.text = Localize("Selecteaza limba in care vrei sa vezi intrebarile.", "Select the language you want for the questions.");
        quizPromptText.alignment = TextAnchor.MiddleCenter;
        languageRoButton.onClick.RemoveAllListeners();
        languageEnButton.onClick.RemoveAllListeners();
        languageRoButton.onClick.AddListener(() => SelectQuizLanguage(QuizLanguage.Romanian));
        languageEnButton.onClick.AddListener(() => SelectQuizLanguage(QuizLanguage.English));

        while (!languageChosen && !leaveRequested)
        {
            yield return null;
        }

        languageRoButton.gameObject.SetActive(false);
        languageEnButton.gameObject.SetActive(false);
        ShowLeaveButton(false);
    }

    private void SelectQuizLanguage(QuizLanguage language)
    {
        selectedLanguage = language;
        languageChosen = true;
    }

    private void ApplyQuizButtonLabels()
    {
        if (nextButtonText != null) nextButtonText.text = Localize("Urmatoarea", "Next");
        if (previousButtonText != null) previousButtonText.text = Localize("Precedenta", "Previous");
        if (hintButtonText != null) hintButtonText.text = Localize("Indiciu", "Hint");
        if (hintBackButtonText != null) hintBackButtonText.text = Localize("Inapoi", "Back");
        if (verifyButtonText != null) verifyButtonText.text = Localize("Verifica", "Verify");
        if (leaveButtonText != null) leaveButtonText.text = Localize("Iesire", "Leave");
        if (languageRoButtonText != null) languageRoButtonText.text = "Romana";
        if (languageEnButtonText != null) languageEnButtonText.text = "English";
    }

    private string Localize(string romanian, string english)
    {
        return selectedLanguage == QuizLanguage.Romanian ? romanian : english;
    }

    private void HideOverlayImmediate()
    {
        ResetOverlay();
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

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void ShowLeaveButton(bool visible)
    {
        if (leaveButton == null)
        {
            return;
        }

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

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
        }

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button EnsureButton(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, string label, int fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
        }

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

        Transform textTransform = go.transform.Find("Text");
        GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("Text");
        if (textTransform == null)
        {
            textObject.transform.SetParent(go.transform, false);
        }

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;

        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        return button;
    }

    private static InputField EnsureInputField(Transform parent, string name, Vector2 anchor, Vector2 size, out Text inputText, out Text placeholderText)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
        }

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
        image.color = new Color(0.12f, 0.17f, 0.25f, 0.96f);

        InputField field = go.GetComponent<InputField>();
        if (field == null)
        {
            field = go.AddComponent<InputField>();
        }

        inputText = EnsureText(go.transform, "Text", string.Empty, 22, new Color(0.93f, 0.97f, 1f, 1f));
        inputText.alignment = TextAnchor.UpperLeft;
        inputText.supportRichText = false;
        RectTransform inputRect = inputText.GetComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(16f, 12f);
        inputRect.offsetMax = new Vector2(-16f, -12f);

        placeholderText = EnsureText(go.transform, "Placeholder", "Write code here...", 22, new Color(0.68f, 0.74f, 0.82f, 0.85f));
        placeholderText.alignment = TextAnchor.UpperLeft;
        RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(16f, 12f);
        placeholderRect.offsetMax = new Vector2(-16f, -12f);

        field.textComponent = inputText;
        field.placeholder = placeholderText;
        field.lineType = InputField.LineType.MultiLineNewline;
        field.text = string.Empty;
        return field;
    }

    private static Text EnsureText(Transform parent, string name, string value, int fontSize, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            go.transform.SetParent(parent, false);
        }

        Text text = go.GetComponent<Text>();
        if (text == null)
        {
            text = go.AddComponent<Text>();
        }

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static string NormalizeCode(string value)
    {
        return value.Replace("\r", string.Empty).Trim();
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(target);
            return;
        }
#endif

        Object.Destroy(target);
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
}

public static class SphereRiftPortalSequenceBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachIfNeeded()
    {
        GameObject target = GameObject.Find("Sphere");
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<SphereRiftPortalSequence>() == null)
        {
            target.AddComponent<SphereRiftPortalSequence>();
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void AttachInEditor()
    {
        EditorApplication.delayCall += AttachIfNeededInEditor;
    }

    private static void AttachIfNeededInEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        GameObject target = GameObject.Find("Sphere");
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<SphereRiftPortalSequence>() == null)
        {
            target.AddComponent<SphereRiftPortalSequence>();
        }
    }
#endif
}
