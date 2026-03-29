using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class BlackoutPipeTrigger : MonoBehaviour
{
    [SerializeField] private Transform pipeRoot;
    [SerializeField] private float alignToHoleDuration = 0.22f;
    [SerializeField] private float descendIntoHoleDuration = 0.5f;
    [SerializeField] private float riseFromHoleDuration = 0.34f;
    [SerializeField] private float holeMouthHeight = 0.18f;
    [SerializeField] private float shaftBottomY = -0.72f;
    [SerializeField] private float exitSpeed = 8.6f;
    [SerializeField] private float displayDistance = 3.4f;

    private bool transitionRunning;
    private bool inCustomRoom;
    private BeanController activeSphere;
    private Camera cam;
    private BeanCameraFollow camFollow;
    private BackgroundColorCycler camCycler;
    private Vector3 previousCamPosition;
    private Quaternion previousCamRotation;
    private int previousCullingMask;
    private CameraClearFlags previousClearFlags;
    private Color previousBackgroundColor;
    private GameObject previewRig;
    private MeshRenderer previewRenderer;
    private Canvas returnCanvas;
    private Button returnButton;
    private Slider alphaSlider;
    private Image alphaBackgroundImage;
    private TimeOfDayController timeController;
    private Slider timeOfDaySlider;
    private Toggle autoCycleToggle;
    private Slider dayDurationSlider;
    private Slider sunriseSlider;
    private Slider sunsetSlider;
    private Slider nightIntensitySlider;
    private Slider dayIntensitySlider;
    private Slider sunYawSlider;
    private Text timeOfDayValueText;
    private Text autoCycleValueText;
    private Text dayDurationValueText;
    private Text sunriseValueText;
    private Text sunsetValueText;
    private Text nightIntensityValueText;
    private Text dayIntensityValueText;
    private Text sunYawValueText;
    private GameObject debugHoverPanel;
    private Text debugHoverText;
    private bool showDebugHover;
    private RectTransform colorSquareRect;
    private RectTransform colorCursorRect;
    private Image colorSquareImage;
    private Image colorSwatch;
    private Material activeRuntimeMaterial;
    private bool suppressSliderCallbacks;
    private bool suppressTimeCallbacks;
    private float currentHue;
    private float currentValue = 1f;
    private float currentAlpha = 1f;

    public void SetPipeRoot(Transform root)
    {
        pipeRoot = root;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (transitionRunning || inCustomRoom)
        {
            return;
        }

        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        StartCoroutine(EnterCustomRoomFlow(sphere));
    }

    private void Update()
    {
        if (!inCustomRoom)
        {
            return;
        }

        EnsureTimeController();
        if (timeController == null)
        {
            return;
        }

        if (timeController.AutoCycle)
        {
            SyncTimeUiFromController();
        }
        else
        {
            UpdateTimeValueTexts();
            if (showDebugHover)
            {
                UpdateDebugHoverText();
            }
        }
    }

    private IEnumerator EnterCustomRoomFlow(BeanController sphere)
    {
        transitionRunning = true;
        activeSphere = sphere;

        var rb = sphere.GetComponent<Rigidbody>();
        sphere.SetMovementLocked(true);
        sphere.SetHardFreeze(true);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Transform root = pipeRoot != null ? pipeRoot : transform;
        Vector3 start = sphere.transform.position;
        Vector3 mouth = root.position + (Vector3.up * holeMouthHeight);
        Vector3 bottom = new Vector3(root.position.x, shaftBottomY, root.position.z);

        float t = 0f;
        while (t < alignToHoleDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.001f, alignToHoleDuration));
            float smooth = k * k * (3f - (2f * k));
            sphere.transform.position = Vector3.Lerp(start, mouth, smooth);
            yield return null;
        }

        t = 0f;
        while (t < descendIntoHoleDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.001f, descendIntoHoleDuration));
            float smooth = k * k * (3f - (2f * k));
            sphere.transform.position = Vector3.Lerp(mouth, bottom, smooth);
            yield return null;
        }

        EnterCustomRoomView(sphere);
        inCustomRoom = true;
        transitionRunning = false;
    }

    private void EnterCustomRoomView(BeanController sphere)
    {
        cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        camFollow = cam.GetComponent<BeanCameraFollow>();
        camCycler = cam.GetComponent<BackgroundColorCycler>();

        previousCamPosition = cam.transform.position;
        previousCamRotation = cam.transform.rotation;
        previousCullingMask = cam.cullingMask;
        previousClearFlags = cam.clearFlags;
        previousBackgroundColor = cam.backgroundColor;

        if (camFollow != null)
        {
            camFollow.enabled = false;
        }
        if (camCycler != null)
        {
            camCycler.enabled = false;
        }

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = ~0;

        EnsurePreviewRig();
        SyncPreviewSkin(sphere);
        EnsureEditablePlayerMaterial(sphere);
        EnsureTimeController();

        Vector3 rigPos = previewRig.transform.position;
        cam.transform.position = rigPos + new Vector3(0f, 0.15f, -displayDistance);
        cam.transform.LookAt(rigPos + new Vector3(0f, 0.05f, 0f));

        EnsureReturnUi();
        SyncTimeUiFromController();
        if (returnCanvas != null)
        {
            returnCanvas.gameObject.SetActive(true);
        }
    }

    private void EnsurePreviewRig()
    {
        if (previewRig != null && previewRenderer != null)
        {
            return;
        }

        previewRig = new GameObject("CustomSkinPreviewRig");
        previewRig.transform.position = new Vector3(2200f, 2200f, 2200f);
        previewRig.transform.rotation = Quaternion.identity;

        var previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewSphere.name = "PreviewBall";
        previewSphere.transform.SetParent(previewRig.transform);
        previewSphere.transform.localPosition = Vector3.zero;
        previewSphere.transform.localRotation = Quaternion.identity;
        previewSphere.transform.localScale = Vector3.one * 0.72f;

        var col = previewSphere.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        previewRenderer = previewSphere.GetComponent<MeshRenderer>();

        var lightObj = new GameObject("PreviewLight");
        lightObj.transform.SetParent(previewRig.transform);
        lightObj.transform.localPosition = new Vector3(-1.5f, 2.3f, -2f);
        lightObj.transform.localRotation = Quaternion.Euler(38f, 35f, 0f);
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1.35f;
    }

    private void SyncPreviewSkin(BeanController sphere)
    {
        if (previewRenderer == null || sphere == null)
        {
            return;
        }

        var sphereRenderer = sphere.GetComponent<MeshRenderer>();
        if (sphereRenderer == null)
        {
            return;
        }

        previewRenderer.sharedMaterial = sphereRenderer.sharedMaterial;
    }

    private void EnsureTimeController()
    {
        if (timeController == null)
        {
            timeController = FindObjectOfType<TimeOfDayController>();
        }
    }

    private void EnsureReturnUi()
    {
        if (returnCanvas != null && returnButton != null)
        {
            return;
        }

        var root = new GameObject("CustomSkinReturnUI");
        returnCanvas = root.AddComponent<Canvas>();
        returnCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        returnCanvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        var barObj = new GameObject("BottomBar");
        barObj.transform.SetParent(root.transform, false);
        var barRect = barObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 150f);
        var barImage = barObj.AddComponent<Image>();
        barImage.color = new Color(0.22f, 0f, 0f, 0.9f);

        CreateSpectrumUi(root.transform);
        CreateTimeUi(root.transform);
        CreateDebugHoverUi(root.transform);

        var buttonObj = new GameObject("ReturnButton");
        buttonObj.transform.SetParent(root.transform, false);
        var rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 70f);
        rect.sizeDelta = new Vector2(520f, 88f);

        var image = buttonObj.AddComponent<Image>();
        image.color = new Color(0.82f, 0.08f, 0.08f, 1f);

        returnButton = buttonObj.AddComponent<Button>();
        returnButton.targetGraphic = image;
        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(OnReturnButtonClicked);

        var textObj = new GameObject("Label");
        textObj.transform.SetParent(buttonObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.AddComponent<Text>();
        text.text = "Return to freedom";
        text.alignment = TextAnchor.MiddleCenter;
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.font = uiFont;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 30;
        text.color = Color.white;

        root.SetActive(false);
    }

    private void CreateSpectrumUi(Transform parent)
    {
        var panelObj = new GameObject("SpectrumPanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-36f, 0f);
        panelRect.sizeDelta = new Vector2(620f, 320f);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        var squareObj = new GameObject("ColorSquare");
        squareObj.transform.SetParent(panelObj.transform, false);
        colorSquareRect = squareObj.AddComponent<RectTransform>();
        colorSquareRect.anchorMin = new Vector2(0f, 0f);
        colorSquareRect.anchorMax = new Vector2(0f, 0f);
        colorSquareRect.pivot = new Vector2(0f, 0f);
        colorSquareRect.anchoredPosition = new Vector2(16f, 66f);
        colorSquareRect.sizeDelta = new Vector2(360f, 220f);
        colorSquareImage = squareObj.AddComponent<Image>();
        colorSquareImage.sprite = CreateColorSquareSprite();
        colorSquareImage.type = Image.Type.Sliced;
        AddColorSquareEvents(squareObj);

        var cursorObj = new GameObject("Cursor");
        cursorObj.transform.SetParent(squareObj.transform, false);
        colorCursorRect = cursorObj.AddComponent<RectTransform>();
        colorCursorRect.anchorMin = new Vector2(0f, 0f);
        colorCursorRect.anchorMax = new Vector2(0f, 0f);
        colorCursorRect.pivot = new Vector2(0.5f, 0.5f);
        colorCursorRect.anchoredPosition = new Vector2(210f, 110f);
        colorCursorRect.sizeDelta = new Vector2(18f, 18f);
        var cursorImage = cursorObj.AddComponent<Image>();
        cursorImage.color = Color.white;

        alphaSlider = CreateAlphaSlider(panelObj.transform, "Transparency", new Vector2(16f, 20f));

        if (alphaSlider != null) alphaSlider.onValueChanged.AddListener(_ => OnSpectrumChanged());

        var swatchObj = new GameObject("ColorSwatch");
        swatchObj.transform.SetParent(panelObj.transform, false);
        var swatchRect = swatchObj.AddComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(1f, 0.5f);
        swatchRect.anchorMax = new Vector2(1f, 0.5f);
        swatchRect.pivot = new Vector2(1f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(-16f, 0f);
        swatchRect.sizeDelta = new Vector2(90f, 90f);
        colorSwatch = swatchObj.AddComponent<Image>();
        colorSwatch.color = Color.white;
    }

    private void CreateTimeUi(Transform parent)
    {
        var panelObj = new GameObject("TimePanel");
        panelObj.transform.SetParent(parent, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(30f, 0f);
        panelRect.sizeDelta = new Vector2(780f, 620f);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.085f, 0.12f, 0.9f);

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(0f, 34f);
        var titleText = titleObj.AddComponent<Text>();
        titleText.text = "Time Of Day Controls";
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontStyle = FontStyle.Bold;
        titleText.fontSize = 23;
        titleText.color = new Color(0.92f, 0.98f, 1f, 1f);

        var subtitleObj = new GameObject("Subtitle");
        subtitleObj.transform.SetParent(panelObj.transform, false);
        var subtitleRect = subtitleObj.AddComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -40f);
        subtitleRect.sizeDelta = new Vector2(0f, 24f);
        var subtitleText = subtitleObj.AddComponent<Text>();
        subtitleText.text = "Adjust daylight behavior directly from this pipe";
        subtitleText.alignment = TextAnchor.MiddleLeft;
        subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subtitleText.fontSize = 15;
        subtitleText.color = new Color(0.73f, 0.83f, 0.9f, 1f);

        float y = -72f;
        const float step = 40f;

        CreateSectionHeader(panelObj.transform, "Cycle", new Vector2(16f, y));
        y -= 30f;
        timeOfDaySlider = CreateValueSliderRow(panelObj.transform, "Time", new Vector2(16f, y), 0f, 24f, false, OnTimeOfDayChanged, out timeOfDayValueText);
        y -= step;
        autoCycleToggle = CreateToggleRow(panelObj.transform, "Auto Cycle", new Vector2(16f, y), OnAutoCycleChanged, out autoCycleValueText);
        y -= step;
        dayDurationSlider = CreateValueSliderRow(panelObj.transform, "Day Duration (s)", new Vector2(16f, y), 1f, 600f, false, OnDayDurationChanged, out dayDurationValueText);

        y -= 48f;
        CreateSectionHeader(panelObj.transform, "Sun Window", new Vector2(16f, y));
        y -= 30f;
        sunriseSlider = CreateValueSliderRow(panelObj.transform, "Sunrise", new Vector2(16f, y), 0f, 23.75f, false, OnSunriseChanged, out sunriseValueText);
        y -= step;
        sunsetSlider = CreateValueSliderRow(panelObj.transform, "Sunset", new Vector2(16f, y), 0.25f, 24f, false, OnSunsetChanged, out sunsetValueText);

        y -= 48f;
        CreateSectionHeader(panelObj.transform, "Light", new Vector2(16f, y));
        y -= 30f;
        nightIntensitySlider = CreateValueSliderRow(panelObj.transform, "Night Intensity", new Vector2(16f, y), 0f, 4f, false, OnNightIntensityChanged, out nightIntensityValueText);
        y -= step;
        dayIntensitySlider = CreateValueSliderRow(panelObj.transform, "Day Intensity", new Vector2(16f, y), 0f, 8f, false, OnDayIntensityChanged, out dayIntensityValueText);
        y -= step;
        sunYawSlider = CreateValueSliderRow(panelObj.transform, "Sun Yaw", new Vector2(16f, y), -180f, 180f, false, OnSunYawChanged, out sunYawValueText);
    }

    private void CreateDebugHoverUi(Transform parent)
    {
        var anchorObj = new GameObject("DebugHoverAnchor");
        anchorObj.transform.SetParent(parent, false);
        var anchorRect = anchorObj.AddComponent<RectTransform>();
        anchorRect.anchorMin = new Vector2(0f, 1f);
        anchorRect.anchorMax = new Vector2(0f, 1f);
        anchorRect.pivot = new Vector2(0f, 1f);
        anchorRect.anchoredPosition = new Vector2(36f, -18f);
        anchorRect.sizeDelta = new Vector2(220f, 34f);

        var labelImage = anchorObj.AddComponent<Image>();
        labelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.65f);
        AddHoverEvents(anchorObj, true);

        var labelTextObj = new GameObject("Label");
        labelTextObj.transform.SetParent(anchorObj.transform, false);
        var labelTextRect = labelTextObj.AddComponent<RectTransform>();
        labelTextRect.anchorMin = Vector2.zero;
        labelTextRect.anchorMax = Vector2.one;
        labelTextRect.offsetMin = Vector2.zero;
        labelTextRect.offsetMax = Vector2.zero;
        var labelText = labelTextObj.AddComponent<Text>();
        labelText.text = "Hover For Debug Data";
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontStyle = FontStyle.Bold;
        labelText.fontSize = 18;
        labelText.color = Color.white;

        debugHoverPanel = new GameObject("DebugHoverPanel");
        debugHoverPanel.transform.SetParent(parent, false);
        var panelRect = debugHoverPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(36f, -58f);
        panelRect.sizeDelta = new Vector2(430f, 188f);
        var panelImage = debugHoverPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.82f);

        var textObj = new GameObject("DebugText");
        textObj.transform.SetParent(debugHoverPanel.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);
        debugHoverText = textObj.AddComponent<Text>();
        debugHoverText.alignment = TextAnchor.UpperLeft;
        debugHoverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        debugHoverText.fontSize = 16;
        debugHoverText.color = Color.white;
        debugHoverPanel.SetActive(false);
    }

    private Slider CreateValueSliderRow(
        Transform parent,
        string label,
        Vector2 anchoredPos,
        float min,
        float max,
        bool wholeNumbers,
        UnityAction<float> onChanged,
        out Text valueText)
    {
        var row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(730f, 36f);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(170f, 0f);
        var labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontStyle = FontStyle.Bold;
        labelText.fontSize = 18;
        labelText.color = Color.white;

        var valueObj = new GameObject("Value");
        valueObj.transform.SetParent(row.transform, false);
        var valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(130f, 0f);
        valueText = valueObj.AddComponent<Text>();
        valueText.text = "-";
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontStyle = FontStyle.Bold;
        valueText.fontSize = 18;
        valueText.color = new Color(0.9f, 0.95f, 1f, 1f);

        var sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.offsetMin = new Vector2(180f, 5f);
        sliderRect.offsetMax = new Vector2(-136f, -5f);

        var bg = sliderObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var slider = sliderObj.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.targetGraphic = bg;

        var fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5f, 5f);
        fillAreaRect.offsetMax = new Vector2(-16f, -5f);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.26f, 0.73f, 0.92f, 0.42f);

        var handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(sliderObj.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 28f);
        var handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    private Toggle CreateToggleRow(
        Transform parent,
        string label,
        Vector2 anchoredPos,
        UnityAction<bool> onChanged,
        out Text valueText)
    {
        var row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(730f, 36f);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(170f, 0f);
        var labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontStyle = FontStyle.Bold;
        labelText.fontSize = 18;
        labelText.color = Color.white;

        var valueObj = new GameObject("Value");
        valueObj.transform.SetParent(row.transform, false);
        var valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(130f, 0f);
        valueText = valueObj.AddComponent<Text>();
        valueText.text = "Off";
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        valueText.fontStyle = FontStyle.Bold;
        valueText.fontSize = 18;
        valueText.color = new Color(0.9f, 0.95f, 1f, 1f);

        var toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        var toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 0.5f);
        toggleRect.anchorMax = new Vector2(0f, 0.5f);
        toggleRect.pivot = new Vector2(0f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(190f, 0f);
        toggleRect.sizeDelta = new Vector2(64f, 28f);

        var toggle = toggleObj.AddComponent<Toggle>();

        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.18f, 0.25f, 0.3f, 1f);

        var checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        var checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0f);
        checkRect.anchorMax = new Vector2(0f, 1f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.sizeDelta = new Vector2(28f, 0f);
        checkRect.anchoredPosition = Vector2.zero;
        var checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.3f, 0.85f, 1f, 1f);

        toggle.graphic = checkImage;
        toggle.targetGraphic = bgImage;
        toggle.onValueChanged.AddListener(onChanged);
        return toggle;
    }

    private void CreateSectionHeader(Transform parent, string title, Vector2 anchoredPos)
    {
        var sectionObj = new GameObject("Section_" + title);
        sectionObj.transform.SetParent(parent, false);
        var sectionRect = sectionObj.AddComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0f, 1f);
        sectionRect.anchorMax = new Vector2(0f, 1f);
        sectionRect.pivot = new Vector2(0f, 1f);
        sectionRect.anchoredPosition = anchoredPos;
        sectionRect.sizeDelta = new Vector2(740f, 24f);

        var lineObj = new GameObject("Line");
        lineObj.transform.SetParent(sectionObj.transform, false);
        var lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchoredPosition = new Vector2(0f, 3f);
        lineRect.sizeDelta = new Vector2(0f, 1f);
        var line = lineObj.AddComponent<Image>();
        line.color = new Color(0.2f, 0.31f, 0.4f, 1f);

        var textObj = new GameObject("Label");
        textObj.transform.SetParent(sectionObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(180f, 0f);
        var text = textObj.AddComponent<Text>();
        text.text = title;
        text.alignment = TextAnchor.MiddleLeft;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = FontStyle.Bold;
        text.fontSize = 15;
        text.color = new Color(0.64f, 0.82f, 0.95f, 1f);
    }

    private void AddHoverEvents(GameObject target, bool showOnHover)
    {
        var trigger = target.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => SetDebugHoverVisible(showOnHover));
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => SetDebugHoverVisible(!showOnHover));
        trigger.triggers.Add(exit);
    }

    private void AddColorSquareEvents(GameObject target)
    {
        var trigger = target.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((evt) => OnColorSquarePointer((PointerEventData)evt));
        trigger.triggers.Add(down);
        var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener((evt) => OnColorSquarePointer((PointerEventData)evt));
        trigger.triggers.Add(drag);
    }

    private Slider CreateAlphaSlider(Transform parent, string label, Vector2 anchoredPos)
    {
        var row = new GameObject("Row_" + label);
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(0f, 0f);
        rowRect.pivot = new Vector2(0f, 0f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(470f, 28f);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(28f, 0f);
        var labelText = labelObj.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontStyle = FontStyle.Bold;
        labelText.fontSize = 22;
        labelText.color = Color.white;

        var sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.offsetMin = new Vector2(120f, 0f);
        sliderRect.offsetMax = new Vector2(0f, 0f);

        var bg = sliderObj.AddComponent<Image>();
        bg.sprite = CreateAlphaSpectrumSprite(Color.white);
        bg.type = Image.Type.Sliced;
        var slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.targetGraphic = bg;
        alphaBackgroundImage = bg;

        var fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(6f, 5f);
        fillAreaRect.offsetMax = new Vector2(-18f, -5f);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0.05f);

        var handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(sliderObj.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 28f);
        var handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Sprite CreateColorSquareSprite()
    {
        const int width = 256;
        const int height = 256;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float h = x / (float)(width - 1);
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                Color c = Color.HSVToRGB(h, 1f, v);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateAlphaSpectrumSprite(Color baseColor)
    {
        const int width = 256;
        const int height = 8;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.05f, 1f, t));
            for (int y = 0; y < height; y++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
    }

    private void EnsureEditablePlayerMaterial(BeanController sphere)
    {
        if (sphere == null)
        {
            return;
        }

        var sphereRenderer = sphere.GetComponent<MeshRenderer>();
        if (sphereRenderer == null || sphereRenderer.sharedMaterial == null)
        {
            return;
        }

        activeRuntimeMaterial = new Material(sphereRenderer.sharedMaterial);
        sphereRenderer.material = activeRuntimeMaterial;
        if (previewRenderer != null)
        {
            previewRenderer.sharedMaterial = activeRuntimeMaterial;
        }

        Color initial = ExtractMainColor(activeRuntimeMaterial);
        Color.RGBToHSV(initial, out currentHue, out _, out currentValue);
        currentAlpha = initial.a;
        SetSpectrumSliders();
        ApplyMaterialColor(initial);
    }

    private Color ExtractMainColor(Material mat)
    {
        if (mat == null)
        {
            return Color.white;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            return mat.GetColor("_BaseColor");
        }

        return mat.color;
    }

    private void SetSpectrumSliders()
    {
        suppressSliderCallbacks = true;
        if (alphaSlider != null) alphaSlider.value = currentAlpha;
        UpdateColorCursor();
        suppressSliderCallbacks = false;
        UpdateAlphaBarColor();
    }

    private void OnColorSquarePointer(PointerEventData data)
    {
        if (colorSquareRect == null)
        {
            return;
        }

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(colorSquareRect, data.position, data.pressEventCamera, out local))
        {
            return;
        }

        float width = colorSquareRect.rect.width;
        float height = colorSquareRect.rect.height;
        float x = Mathf.Clamp(local.x - colorSquareRect.rect.xMin, 0f, width);
        float y = Mathf.Clamp(local.y - colorSquareRect.rect.yMin, 0f, height);
        currentHue = Mathf.Clamp01(x / Mathf.Max(1f, width));
        currentValue = Mathf.Clamp01(y / Mathf.Max(1f, height));
        UpdateColorCursor();
        OnSpectrumChanged();
    }

    private void UpdateColorCursor()
    {
        if (colorCursorRect == null || colorSquareRect == null)
        {
            return;
        }

        float x = currentHue * colorSquareRect.rect.width;
        float y = currentValue * colorSquareRect.rect.height;
        colorCursorRect.anchoredPosition = new Vector2(x, y);
    }

    private void OnSpectrumChanged()
    {
        if (suppressSliderCallbacks)
        {
            return;
        }

        currentAlpha = alphaSlider != null ? alphaSlider.value : currentAlpha;
        Color c = Color.HSVToRGB(currentHue, 1f, currentValue);
        c.a = currentAlpha;
        UpdateAlphaBarColor();
        ApplyMaterialColor(c);
    }

    private void UpdateAlphaBarColor()
    {
        if (alphaBackgroundImage == null)
        {
            return;
        }

        Color hueColor = Color.HSVToRGB(currentHue, 1f, 1f);
        alphaBackgroundImage.sprite = CreateAlphaSpectrumSprite(hueColor);
    }

    private void ApplyMaterialColor(Color c)
    {
        if (activeRuntimeMaterial == null)
        {
            return;
        }

        ConfigureMaterialTransparency(activeRuntimeMaterial, c.a);

        activeRuntimeMaterial.color = c;
        if (activeRuntimeMaterial.HasProperty("_BaseColor"))
        {
            activeRuntimeMaterial.SetColor("_BaseColor", c);
        }
        if (activeRuntimeMaterial.HasProperty("_EmissionColor"))
        {
            activeRuntimeMaterial.EnableKeyword("_EMISSION");
            activeRuntimeMaterial.SetColor("_EmissionColor", c * 0.28f);
        }
        if (colorSwatch != null)
        {
            colorSwatch.color = c;
        }
    }

    private void OnTimeOfDayChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetTimeOfDay(value);
        UpdateTimeValueTexts();
    }

    private void OnAutoCycleChanged(bool value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetAutoCycle(value);
        UpdateTimeValueTexts();
    }

    private void OnDayDurationChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetDayDurationSeconds(value);
        UpdateTimeValueTexts();
    }

    private void OnSunriseChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        float adjusted = Mathf.Clamp(value, 0f, timeController.SunsetHour - 0.25f);
        timeController.SetSunriseHour(adjusted);
        if (!Mathf.Approximately(value, adjusted) && sunriseSlider != null)
        {
            suppressTimeCallbacks = true;
            sunriseSlider.value = adjusted;
            suppressTimeCallbacks = false;
        }

        UpdateTimeValueTexts();
    }

    private void OnSunsetChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        float adjusted = Mathf.Clamp(value, timeController.SunriseHour + 0.25f, 24f);
        timeController.SetSunsetHour(adjusted);
        if (!Mathf.Approximately(value, adjusted) && sunsetSlider != null)
        {
            suppressTimeCallbacks = true;
            sunsetSlider.value = adjusted;
            suppressTimeCallbacks = false;
        }

        UpdateTimeValueTexts();
    }

    private void OnNightIntensityChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetNightIntensity(value);
        UpdateTimeValueTexts();
    }

    private void OnDayIntensityChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetDayIntensity(value);
        UpdateTimeValueTexts();
    }

    private void OnSunYawChanged(float value)
    {
        if (suppressTimeCallbacks || timeController == null)
        {
            return;
        }

        timeController.SetSunYaw(value);
        UpdateTimeValueTexts();
    }

    private void SyncTimeUiFromController()
    {
        EnsureTimeController();
        if (timeController == null)
        {
            return;
        }

        suppressTimeCallbacks = true;
        if (timeOfDaySlider != null) timeOfDaySlider.value = timeController.TimeOfDay;
        if (autoCycleToggle != null) autoCycleToggle.isOn = timeController.AutoCycle;
        if (dayDurationSlider != null) dayDurationSlider.value = timeController.DayDurationSeconds;
        if (sunriseSlider != null) sunriseSlider.value = timeController.SunriseHour;
        if (sunsetSlider != null) sunsetSlider.value = timeController.SunsetHour;
        if (nightIntensitySlider != null) nightIntensitySlider.value = timeController.NightIntensity;
        if (dayIntensitySlider != null) dayIntensitySlider.value = timeController.DayIntensity;
        if (sunYawSlider != null) sunYawSlider.value = timeController.SunYaw;
        suppressTimeCallbacks = false;

        UpdateTimeValueTexts();
        UpdateDebugHoverText();
    }

    private void UpdateTimeValueTexts()
    {
        if (timeController == null)
        {
            if (timeOfDayValueText != null) timeOfDayValueText.text = "-";
            if (autoCycleValueText != null) autoCycleValueText.text = "-";
            if (dayDurationValueText != null) dayDurationValueText.text = "-";
            if (sunriseValueText != null) sunriseValueText.text = "-";
            if (sunsetValueText != null) sunsetValueText.text = "-";
            if (nightIntensityValueText != null) nightIntensityValueText.text = "-";
            if (dayIntensityValueText != null) dayIntensityValueText.text = "-";
            if (sunYawValueText != null) sunYawValueText.text = "-";
            return;
        }

        if (timeOfDayValueText != null) timeOfDayValueText.text = $"{timeController.TimeOfDay:0.00}h";
        if (autoCycleValueText != null) autoCycleValueText.text = timeController.AutoCycle ? "On" : "Off";
        if (dayDurationValueText != null) dayDurationValueText.text = $"{timeController.DayDurationSeconds:0.0}";
        if (sunriseValueText != null) sunriseValueText.text = $"{timeController.SunriseHour:0.00}h";
        if (sunsetValueText != null) sunsetValueText.text = $"{timeController.SunsetHour:0.00}h";
        if (nightIntensityValueText != null) nightIntensityValueText.text = $"{timeController.NightIntensity:0.00}";
        if (dayIntensityValueText != null) dayIntensityValueText.text = $"{timeController.DayIntensity:0.00}";
        if (sunYawValueText != null) sunYawValueText.text = $"{timeController.SunYaw:0.0}";
    }

    private void SetDebugHoverVisible(bool visible)
    {
        showDebugHover = visible;
        if (debugHoverPanel != null)
        {
            debugHoverPanel.SetActive(visible);
        }

        if (visible)
        {
            UpdateDebugHoverText();
        }
    }

    private void UpdateDebugHoverText()
    {
        if (debugHoverText == null)
        {
            return;
        }

        if (timeController == null)
        {
            debugHoverText.text =
                "Debug Data\n" +
                "No TimeOfDayController in scene.";
            return;
        }

        Vector3 sunEuler = timeController.SunEulerAngles;
        debugHoverText.text =
            "Debug Data\n" +
            $"TimeOfDay: {timeController.TimeOfDay:0.000}\n" +
            $"AutoCycle: {timeController.AutoCycle}\n" +
            $"DayDurationSeconds: {timeController.DayDurationSeconds:0.000}\n" +
            $"SunriseHour: {timeController.SunriseHour:0.000}\n" +
            $"SunsetHour: {timeController.SunsetHour:0.000}\n" +
            $"NightIntensity: {timeController.NightIntensity:0.000}\n" +
            $"DayIntensity: {timeController.DayIntensity:0.000}\n" +
            $"CurrentLightIntensity: {timeController.CurrentLightIntensity:0.000}\n" +
            $"SunYaw: {timeController.SunYaw:0.000}\n" +
            $"SunEuler XYZ: ({sunEuler.x:0.00}, {sunEuler.y:0.00}, {sunEuler.z:0.00})";
    }

    private static void ConfigureMaterialTransparency(Material mat, float alpha)
    {
        if (mat == null)
        {
            return;
        }

        bool transparent = alpha < 0.999f;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
        }
        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0f);
        }
        if (mat.HasProperty("_SrcBlend"))
        {
            mat.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
        }
        if (mat.HasProperty("_DstBlend"))
        {
            mat.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);
        }
        if (mat.HasProperty("_ZWrite"))
        {
            mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
        }
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", transparent ? 3f : 0f);
        }

        if (transparent)
        {
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var evObj = new GameObject("EventSystem");
        evObj.AddComponent<EventSystem>();
        evObj.AddComponent<StandaloneInputModule>();
    }

    private void OnReturnButtonClicked()
    {
        StartCoroutine(ReturnToFreedomFlow());
    }

    private IEnumerator ReturnToFreedomFlow()
    {
        if (transitionRunning || activeSphere == null)
        {
            yield break;
        }

        transitionRunning = true;
        inCustomRoom = false;
        showDebugHover = false;
        if (returnCanvas != null)
        {
            returnCanvas.gameObject.SetActive(false);
        }

        RestoreGameplayCamera();

        var rb = activeSphere.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Transform root = pipeRoot != null ? pipeRoot : transform;
        Vector3 mouth = root.position + (Vector3.up * holeMouthHeight);
        Vector3 bottom = new Vector3(root.position.x, shaftBottomY, root.position.z);
        activeSphere.transform.position = bottom;

        float t = 0f;
        while (t < riseFromHoleDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.001f, riseFromHoleDuration));
            float smooth = k * k * (3f - (2f * k));
            activeSphere.transform.position = Vector3.Lerp(bottom, mouth, smooth);
            yield return null;
        }

        activeSphere.transform.position = mouth;
        activeSphere.SetHardFreeze(false);
        activeSphere.SetMovementLocked(false);

        if (rb != null)
        {
            Vector3 escapeDir = (Vector3.zero - mouth);
            escapeDir.y = 0f;
            escapeDir = escapeDir.sqrMagnitude > 0.001f ? escapeDir.normalized : Vector3.right;
            rb.velocity = (escapeDir * exitSpeed) + (Vector3.up * 3.2f);
        }

        transitionRunning = false;
    }

    private void OnGUI()
    {
        if (!inCustomRoom || transitionRunning)
        {
            return;
        }

        if (returnCanvas != null && returnCanvas.gameObject.activeInHierarchy)
        {
            return;
        }

        float w = 520f;
        float h = 88f;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - 120f, w, h);
        var style = new GUIStyle(GUI.skin.button);
        style.fontSize = 32;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.normal.background = Texture2D.whiteTexture;

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.82f, 0.08f, 0.08f, 1f);
        if (GUI.Button(r, "Return to freedom", style))
        {
            StartCoroutine(ReturnToFreedomFlow());
        }
        GUI.backgroundColor = old;
    }

    private void RestoreGameplayCamera()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            return;
        }

        cam.clearFlags = previousClearFlags;
        cam.backgroundColor = previousBackgroundColor;
        cam.cullingMask = previousCullingMask;
        cam.transform.position = previousCamPosition;
        cam.transform.rotation = previousCamRotation;

        if (camCycler != null)
        {
            camCycler.enabled = true;
        }
        if (camFollow != null)
        {
            camFollow.enabled = true;
        }
    }
}
