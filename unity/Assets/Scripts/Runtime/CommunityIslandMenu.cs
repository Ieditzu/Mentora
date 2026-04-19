using System.Collections;
using System.Collections.Generic;
using Mentora.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class CommunityIslandMenu : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Vector3 triggerSize = new Vector3(0.25f, 0.4f, 0.25f);
    [SerializeField] private Vector3 triggerOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private float reenterCooldown = 0.2f;
    [SerializeField] private Vector3 iconLocalPosition = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector3 iconLocalScale = new Vector3(2.4f, 2.4f, 2.4f);
    [SerializeField] private bool autoPlaceTriggerWhenMissing = true;

    [Header("Theme")]
    [SerializeField] private Color pageTint = new Color(0.98f, 0.98f, 0.96f, 1f);
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.96f);
    [SerializeField] private Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.28f, 0.31f, 0.36f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.14f, 0.14f, 0.14f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.18f, 0.62f, 0.32f, 1f);
    [SerializeField] private Color darkPageTint = new Color(0.05f, 0.07f, 0.10f, 1f);
    [SerializeField] private Color darkPanelColor = new Color(0.10f, 0.13f, 0.18f, 0.97f);
    [SerializeField] private Color darkTextColor = new Color(0.92f, 0.96f, 1f, 1f);
    [SerializeField] private Color darkSecondaryTextColor = new Color(0.74f, 0.80f, 0.88f, 1f);
    [SerializeField] private Color darkButtonColor = new Color(0.20f, 0.24f, 0.30f, 0.96f);
    [SerializeField] private Color darkAccentColor = new Color(0.33f, 0.74f, 1f, 1f);

    [Header("Animation")]
    [SerializeField] private float fadeToWhiteDuration = 0.34f;
    [SerializeField] private float menuFadeDuration = 0.22f;

    private static Canvas overlayCanvas;
    private static Image whiteImage;
    private static Image panelImage;
    private static Text titleText;
    private static Text bodyText;
    private static Button leaveButton;
    private static Text leaveButtonText;
    private static Button fetchButton;
    private static Text fetchButtonText;
    private static Button themeButton;
    private static Text themeButtonText;

    private GameObject triggerObject;
    private BoxCollider triggerVolume;
    private bool running;
    private bool leaveRequested;
    private bool overlayInteractionActive;
    private bool requireExitBeforeReopen;
    private float reenterBlockedUntil;
    private readonly HashSet<Collider> overlappingPlayerColliders = new HashSet<Collider>();
    private bool useDarkTheme;
    private bool packetSubscribed;
    private bool fetchInProgress;
    private string communityBodyMessage = "Press Fetch Courses to load community quizzes.";

    private static Button prevButton;
    private static Text prevButtonText;
    private static Button nextButton;
    private static Text nextButtonText;
    private static Button actionButton;
    private static Text actionButtonText;
    private static Button[] optionButtons = new Button[4];
    private static Text[] optionButtonTexts = new Text[4];

    private enum MenuState { List, Quiz, Result }
    private MenuState currentState = MenuState.List;
    private PublishedCourseSummary[] availableCourses;
    private int currentCourseIndex = 0;
    private CourseDetailDto currentCourseDetail;
    private int currentQuestionIndex = 0;
    private int currentScore = 0;

    private Color lightPageTint;
    private Color lightPanelColor;
    private Color lightTextColor;
    private Color lightSecondaryTextColor;
    private Color lightButtonColor;
    private Color lightAccentColor;
    private bool capturedLightTheme;
    private bool validateRefreshQueued;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            UnityMainThreadDispatcher.Initialize();
        }
        CacheLightTheme();
        EnsureTriggerVolume();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            UnityMainThreadDispatcher.Initialize();
        }
        CacheLightTheme();
        EnsureTriggerVolume();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            EnsureTriggerVolume();
            return;
        }

        if (validateRefreshQueued)
        {
            return;
        }

        validateRefreshQueued = true;
        EditorApplication.delayCall += RefreshAfterValidate;
#endif
    }

#if UNITY_EDITOR
    private void RefreshAfterValidate()
    {
        validateRefreshQueued = false;

        if (this == null)
        {
            return;
        }

        EnsureTriggerVolume();
    }
#endif

    private void LateUpdate()
    {
        if (overlayInteractionActive)
        {
            SetCursorVisible(true);
        }
    }

    private void EnsureTriggerVolume()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 localTopCenter = triggerOffset;
        if (hasBounds)
        {
            localTopCenter = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)) + triggerOffset;
        }

        Transform existing = transform.Find("CommunityMenuTrigger");
        triggerObject = existing != null ? existing.gameObject : new GameObject("CommunityMenuTrigger");
        triggerObject.transform.SetParent(transform, false);
        if (existing == null && autoPlaceTriggerWhenMissing)
        {
            triggerObject.transform.localPosition = localTopCenter;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
        }

        CommunityIslandMenuTriggerRelay relay = triggerObject.GetComponent<CommunityIslandMenuTriggerRelay>();
        if (relay == null)
        {
            relay = triggerObject.AddComponent<CommunityIslandMenuTriggerRelay>();
        }
        relay.Initialize(this);

        Rigidbody triggerBody = triggerObject.GetComponent<Rigidbody>();
        if (triggerBody == null)
        {
            triggerBody = triggerObject.AddComponent<Rigidbody>();
        }
        triggerBody.isKinematic = true;
        triggerBody.useGravity = false;
        triggerBody.constraints = RigidbodyConstraints.FreezeAll;

        triggerVolume = triggerObject.GetComponent<BoxCollider>();
        if (triggerVolume == null)
        {
            triggerVolume = triggerObject.AddComponent<BoxCollider>();
        }
        triggerVolume.isTrigger = true;
        triggerVolume.center = Vector3.zero;
        triggerVolume.size = triggerSize;

        EnsureTriggerIcon();
    }

    private void EnsureTriggerIcon()
    {
        Transform existing = triggerObject.transform.Find("Visual");
        if (existing != null && (existing.GetComponent<MeshFilter>() != null || existing.GetComponent<MeshRenderer>() != null))
        {
            Object.DestroyImmediate(existing.gameObject);
            existing = null;
        }

        GameObject visualObject = existing != null ? existing.gameObject : new GameObject("Visual");
        visualObject.name = "Visual";
        visualObject.transform.SetParent(triggerObject.transform, false);
        visualObject.transform.localPosition = iconLocalPosition;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = iconLocalScale;
        visualObject.hideFlags = HideFlags.None;

        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Object.DestroyImmediate(visualCollider);
        }

        CommunityIslandMenuIconBillboard billboard = visualObject.GetComponent<CommunityIslandMenuIconBillboard>();
        if (billboard == null)
        {
            billboard = visualObject.AddComponent<CommunityIslandMenuIconBillboard>();
        }
        billboard.SetOwner(triggerObject.transform);

        Texture2D iconTexture = Resources.Load<Texture2D>("Images/community");
        SpriteRenderer spriteRenderer = visualObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            return;
        }

        if (iconTexture != null)
        {
            Rect rect = new Rect(0f, 0f, iconTexture.width, iconTexture.height);
            float pixelsPerUnit = Mathf.Max(iconTexture.width, iconTexture.height);
            Sprite sprite = Sprite.Create(iconTexture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit);
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = accentColor;
        }

        spriteRenderer.sortingOrder = 20;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (!TryGetPlayer(other, out BeanController sphere, out FirstPersonControllerSimple fps))
        {
            return;
        }

        overlappingPlayerColliders.Add(other);

        if (running)
        {
            return;
        }

        if (requireExitBeforeReopen || Time.time < reenterBlockedUntil)
        {
            return;
        }

        StartCoroutine(PlaySequence(sphere, fps));
    }

    private void OnDisable()
    {
        UnsubscribeFromPackets();
    }

    private void CacheLightTheme()
    {
        if (capturedLightTheme)
        {
            return;
        }

        lightPageTint = pageTint;
        lightPanelColor = panelColor;
        lightTextColor = textColor;
        lightSecondaryTextColor = secondaryTextColor;
        lightButtonColor = buttonColor;
        lightAccentColor = accentColor;
        capturedLightTheme = true;
    }

    public void HandleTriggerExit(Collider other)
    {
        if (!TryGetPlayer(other, out _, out _))
        {
            return;
        }

        overlappingPlayerColliders.Remove(other);
        if (overlappingPlayerColliders.Count == 0)
        {
            requireExitBeforeReopen = false;
        }
    }

    private IEnumerator PlaySequence(BeanController sphere, FirstPersonControllerSimple fps)
    {
        running = true;
        leaveRequested = false;

        if (triggerVolume != null)
        {
            triggerVolume.enabled = false;
        }

        SetPlayerLockState(sphere, fps, true, true);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(false);
        }

        EnsureOverlay();
        ResetOverlay();
        SetOverlayVisible(true);
        SetCursorVisible(true);

        yield return FadeImage(whiteImage, 0f, 1f, fadeToWhiteDuration, pageTint);
        yield return AnimatePanel(true);

        ShowLeaveButton(true);
        ShowFetchButton(true);
        ShowThemeButton(true);
        ApplyTheme();
        titleText.text = "Community";
        UpdateBodyText();

        if (prevButton != null) {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() => {
                if (availableCourses != null && availableCourses.Length > 0) {
                    currentCourseIndex = (currentCourseIndex - 1 + availableCourses.Length) % availableCourses.Length;
                    UpdateUIState();
                }
            });
        }
        if (nextButton != null) {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => {
                if (availableCourses != null && availableCourses.Length > 0) {
                    currentCourseIndex = (currentCourseIndex + 1) % availableCourses.Length;
                    UpdateUIState();
                }
            });
        }
        if (actionButton != null) {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnActionClicked);
        }
        for (int i = 0; i < 4; i++) {
            int idx = i;
            if (optionButtons[idx] != null) {
                optionButtons[idx].onClick.RemoveAllListeners();
                optionButtons[idx].onClick.AddListener(() => OnOptionClicked(idx));
            }
        }


        while (!leaveRequested)
        {
            yield return null;
        }

        UnsubscribeFromPackets();
        ShowThemeButton(false);

        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (actionButton != null) actionButton.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++) {
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
        }
        currentState = MenuState.List;
        availableCourses = null;
        currentCourseIndex = 0;

        ShowFetchButton(false);
        ShowLeaveButton(false);
        yield return AnimatePanel(false);
        yield return FadeImage(whiteImage, 1f, 0f, 0.2f, pageTint);

        RestorePlayerState(sphere, fps);
        StartCoroutine(ReenableTriggerAfterDelay());
    }

    private IEnumerator ReenableTriggerAfterDelay()
    {
        yield return new WaitForSeconds(reenterCooldown);
        requireExitBeforeReopen = overlappingPlayerColliders.Count > 0;

        if (triggerVolume != null)
        {
            triggerVolume.enabled = true;
        }

        running = false;
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("CommunityIslandCanvas");
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 6000;
        canvasObject.AddComponent<GraphicRaycaster>();
        Object.DontDestroyOnLoad(canvasObject);

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        whiteImage = EnsureImage(canvasObject.transform, "WhiteFade", pageTint);
        StretchFullscreen(whiteImage.rectTransform);

        panelImage = EnsureImage(canvasObject.transform, "CommunityPanel", panelColor);
        RectTransform panelRect = panelImage.rectTransform;
        panelRect.sizeDelta = new Vector2(1200f, 850f);
        panelRect.anchoredPosition = Vector2.zero;

        Outline panelOutline = panelImage.GetComponent<Outline>() ?? panelImage.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.24f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        Shadow panelShadow = panelImage.GetComponent<Shadow>() ?? panelImage.gameObject.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0, 0, 0, 0.15f);
        panelShadow.effectDistance = new Vector2(10f, -10f);

        titleText = EnsureText(panelImage.transform, "TitleText", new Vector2(0.5f, 0.92f), new Vector2(900f, 80f), 48, FontStyle.Bold, textColor);
        bodyText = EnsureText(panelImage.transform, "BodyText", new Vector2(0.5f, 0.68f), new Vector2(1100f, 300f), 28, FontStyle.Normal, secondaryTextColor);
        bodyText.alignment = TextAnchor.UpperLeft;

        fetchButton = EnsureButton(panelImage.transform, "FetchButton", new Vector2(0.94f, 0.92f), new Vector2(80f, 80f), buttonColor, "↻", 32);
        fetchButtonText = fetchButton.GetComponentInChildren<Text>(true);
        themeButton = EnsureButton(panelImage.transform, "ThemeButton", new Vector2(0.06f, 0.92f), new Vector2(80f, 80f), buttonColor, "☾", 32);
        themeButtonText = themeButton.GetComponentInChildren<Text>(true);

        leaveButton = EnsureButton(panelImage.transform, "LeaveButton", new Vector2(0.5f, -0.08f), new Vector2(160f, 64f), buttonColor, "✕ Close", 24);
        leaveButtonText = leaveButton.GetComponentInChildren<Text>(true);

        prevButton = EnsureButton(panelImage.transform, "PrevButton", new Vector2(0.15f, 0.10f), new Vector2(120f, 64f), buttonColor, "<", 28);
        prevButtonText = prevButton.GetComponentInChildren<Text>(true);
        
        nextButton = EnsureButton(panelImage.transform, "NextButton", new Vector2(0.85f, 0.10f), new Vector2(120f, 64f), buttonColor, ">", 28);
        nextButtonText = nextButton.GetComponentInChildren<Text>(true);

        actionButton = EnsureButton(panelImage.transform, "ActionButton", new Vector2(0.5f, 0.10f), new Vector2(300f, 72f), accentColor, "Start", 28);
        actionButtonText = actionButton.GetComponentInChildren<Text>(true);

        for (int i = 0; i < 4; i++) {
            float yPos = 0.44f - (i * 0.10f);
            optionButtons[i] = EnsureButton(panelImage.transform, "OptionButton" + i, new Vector2(0.5f, yPos), new Vector2(900f, 64f), buttonColor, "Option " + i, 26);
            optionButtonTexts[i] = optionButtons[i].GetComponentInChildren<Text>(true);
        }


        EnsureEventSystem();
    }

    private void ResetOverlay()
    {
        overlayInteractionActive = false;

        if (whiteImage != null)
        {
            whiteImage.color = new Color(pageTint.r, pageTint.g, pageTint.b, 0f);
            whiteImage.enabled = false;
        }

        if (panelImage != null)
        {
            panelImage.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0f);
            panelImage.rectTransform.localScale = new Vector3(0.94f, 0.94f, 1f);
            panelImage.gameObject.SetActive(false);
        }

        if (titleText != null)
        {
            titleText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
        }

        if (bodyText != null)
        {
            bodyText.color = new Color(secondaryTextColor.r, secondaryTextColor.g, secondaryTextColor.b, 0f);
        }

        ShowLeaveButton(false);
        ShowFetchButton(false);
        ShowThemeButton(false);

        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (actionButton != null) actionButton.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++) {
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
        }
        currentState = MenuState.List;
        availableCourses = null;
        currentCourseIndex = 0;

    }

    private void SetOverlayVisible(bool visible)
    {
        overlayInteractionActive = visible;
        if (!visible)
        {
            SetCursorVisible(false);
        }
    }

    private IEnumerator AnimatePanel(bool show)
    {
        if (panelImage == null)
        {
            yield break;
        }

        if (show)
        {
            panelImage.gameObject.SetActive(true);
        }

        RectTransform rect = panelImage.rectTransform;
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
            SetPanelAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        rect.localScale = endScale;
        SetPanelAlpha(toAlpha);

        if (!show)
        {
            panelImage.gameObject.SetActive(false);
        }
    }

    private void SetPanelAlpha(float alpha)
    {
        if (panelImage != null)
        {
            panelImage.color = new Color(panelColor.r, panelColor.g, panelColor.b, alpha * panelColor.a);
        }

        if (titleText != null)
        {
            titleText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
        }

        if (bodyText != null)
        {
            bodyText.color = new Color(secondaryTextColor.r, secondaryTextColor.g, secondaryTextColor.b, alpha);
        }
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
            if (leaveButtonText != null)
            {
                leaveButtonText.text = "Leave";
            }
        }

        leaveButton.gameObject.SetActive(visible);
    }

    private void ShowFetchButton(bool visible)
    {
        if (fetchButton == null)
        {
            return;
        }

        fetchButton.onClick.RemoveAllListeners();
        if (visible)
        {
            fetchButton.onClick.AddListener(OnFetchCoursesClicked);
            if (fetchButtonText != null)
            {
                fetchButtonText.text = fetchInProgress ? "…" : "↻";
            }
        }

        fetchButton.interactable = !fetchInProgress;
        fetchButton.gameObject.SetActive(visible);
    }

    private void ShowThemeButton(bool visible)
    {
        if (themeButton == null)
        {
            return;
        }

        themeButton.onClick.RemoveAllListeners();
        if (visible)
        {
            themeButton.onClick.AddListener(ToggleTheme);
            UpdateThemeButtonLabel();
        }

        themeButton.gameObject.SetActive(visible);
    }

    private void OnLeaveClicked()
    {
        reenterBlockedUntil = Time.time + reenterCooldown;
        leaveRequested = true;
    }

    private async void OnFetchCoursesClicked()
    {
        if (fetchInProgress)
        {
            return;
        }

        fetchInProgress = true;
        communityBodyMessage = "Fetching community quizzes from the server...";
        UpdateBodyText();
        ShowFetchButton(true);

        try
        {
            EnsurePacketSubscription();

            if (GameClient.Instance == null)
            {
                communityBodyMessage = "Game client is not available in this scene.";
                return;
            }

            if (!GameClient.Instance.IsConnected)
            {
                await GameClient.Instance.Connect();
            }

            if (!GameClient.Instance.IsConnected)
            {
                communityBodyMessage = "Could not connect to the server.";
                return;
            }

            await GameClient.Instance.SendPacket(new FetchPublishedCoursesPacket());
        }
        catch (System.Exception ex)
        {
            communityBodyMessage = "Fetch failed: " + ex.Message;
            fetchInProgress = false;
            ShowFetchButton(true);
            UpdateBodyText();
        }
    }

    private void ToggleTheme()
    {
        useDarkTheme = !useDarkTheme;
        ApplyTheme();
        UpdateThemeButtonLabel();
    }

    private void UpdateThemeButtonLabel()
    {
        if (themeButtonText != null)
        {
            themeButtonText.text = useDarkTheme ? "☀" : "☾";
        }
    }

    private void ApplyTheme()
    {
        if (useDarkTheme)
        {
            pageTint = darkPageTint;
            panelColor = darkPanelColor;
            textColor = darkTextColor;
            secondaryTextColor = darkSecondaryTextColor;
            buttonColor = darkButtonColor;
            accentColor = darkAccentColor;
        }
        else
        {
            pageTint = lightPageTint;
            panelColor = lightPanelColor;
            textColor = lightTextColor;
            secondaryTextColor = lightSecondaryTextColor;
            buttonColor = lightButtonColor;
            accentColor = lightAccentColor;
        }

        if (whiteImage != null)
        {
            whiteImage.color = new Color(pageTint.r, pageTint.g, pageTint.b, whiteImage.color.a);
        }

        if (panelImage != null)
        {
            panelImage.color = new Color(panelColor.r, panelColor.g, panelColor.b, panelImage.color.a <= 0f ? 0f : panelColor.a * Mathf.Clamp01(panelImage.color.a));
            Outline panelOutline = panelImage.GetComponent<Outline>();
            if (panelOutline != null)
            {
                panelOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.16f);
            }
        }

        if (titleText != null)
        {
            titleText.color = new Color(textColor.r, textColor.g, textColor.b, titleText.color.a <= 0f ? 1f : titleText.color.a);
        }

        if (bodyText != null)
        {
            bodyText.color = new Color(secondaryTextColor.r, secondaryTextColor.g, secondaryTextColor.b, bodyText.color.a <= 0f ? 1f : bodyText.color.a);
        }

        ApplyButtonTheme(fetchButton, fetchButtonText, fetchInProgress ? "…" : "↻");
        ApplyButtonTheme(themeButton, themeButtonText, useDarkTheme ? "☀" : "☾");
        ApplyButtonTheme(leaveButton, leaveButtonText, "✕");

        ApplyButtonTheme(prevButton, prevButtonText, "<");
        ApplyButtonTheme(nextButton, nextButtonText, ">");
        ApplyButtonTheme(actionButton, actionButtonText, actionButtonText != null ? actionButtonText.text : "Start");
        for (int i = 0; i < 4; i++) {
            ApplyButtonTheme(optionButtons[i], optionButtonTexts[i], optionButtonTexts[i] != null ? optionButtonTexts[i].text : "");
        }

    }

    private void ApplyButtonTheme(Button button, Text label, string buttonLabel)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = buttonColor;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(Mathf.Clamp01(buttonColor.r + 0.08f), Mathf.Clamp01(buttonColor.g + 0.08f), Mathf.Clamp01(buttonColor.b + 0.08f), buttonColor.a);
        colors.pressedColor = new Color(Mathf.Clamp01(buttonColor.r - 0.08f), Mathf.Clamp01(buttonColor.g - 0.08f), Mathf.Clamp01(buttonColor.b - 0.08f), buttonColor.a);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, buttonColor.a * 0.35f);
        button.colors = colors;

        if (label != null)
        {
            label.text = buttonLabel;
            label.color = useDarkTheme ? darkTextColor : Color.white;
        }
    }

    private void UpdateBodyText()
    {
        if (bodyText != null)
        {
            bodyText.text = communityBodyMessage;
        }
    }

    private void EnsurePacketSubscription()
    {
        if (packetSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived += OnPacketReceived;
        packetSubscribed = true;
    }

    private void UnsubscribeFromPackets()
    {
        if (!packetSubscribed || GameClient.Instance == null)
        {
            return;
        }

        GameClient.Instance.OnPacketReceived -= OnPacketReceived;
        packetSubscribed = false;
    }

    
    
    private void OnPacketReceived(Packet packet)
    {
        if (packet is FetchPublishedCoursesResponsePacket coursesPacket)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                fetchInProgress = false;
                PublishedCourseSummaryList payload = ParsePublishedCourses(coursesPacket.CoursesJson);
                if (payload != null && payload.items != null && payload.items.Length > 0)
                {
                    availableCourses = payload.items;
                    currentCourseIndex = 0;
                    currentState = MenuState.List;
                }
                else
                {
                    availableCourses = null;
                    communityBodyMessage = "No published community quizzes were returned.";
                }
                UpdateUIState();
                ShowFetchButton(true);
            });
            return;
        }
        
        if (packet is FetchCourseDetailResponsePacket detailPacket)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                fetchInProgress = false;
                currentCourseDetail = ParseCourseDetail(detailPacket.CourseJson);
                if (currentCourseDetail != null && currentCourseDetail.questions != null && currentCourseDetail.questions.Length > 0)
                {
                    currentState = MenuState.Quiz;
                    currentQuestionIndex = 0;
                    currentScore = 0;
                }
                else
                {
                    currentState = MenuState.List;
                    communityBodyMessage = "Course details could not be loaded.";
                }
                UpdateUIState();
                ShowFetchButton(true);
            });
            return;
        }

        if (packet is ActionResponsePacket actionPacket)
        {
            if (actionPacket.RequestPacketId == 36 && !actionPacket.Success)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    fetchInProgress = false;
                    communityBodyMessage = "Fetch failed: " + (string.IsNullOrWhiteSpace(actionPacket.Message) ? "Unknown error." : actionPacket.Message);
                    UpdateUIState();
                    ShowFetchButton(true);
                });
            }
            else if (actionPacket.RequestPacketId == 40)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    fetchInProgress = false;
                    communityBodyMessage = actionPacket.Success ? "Course completed successfully!" : "Failed to submit course completion.";
                    currentState = MenuState.List;
                    UpdateUIState();
                    ShowFetchButton(true);
                });
            }
        }
    }

    private void UpdateUIState()
    {
        if (currentState == MenuState.List)
        {
            titleText.text = "Community Courses";
            for (int i = 0; i < 4; i++) if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
            
            if (availableCourses != null && availableCourses.Length > 0)
            {
                PublishedCourseSummary course = availableCourses[currentCourseIndex];
                string completedTag = course.completed ? "<color=#16a34a><b>[COMPLETED]</b></color> " : "";
                
                communityBodyMessage = $"<b><size=42>{course.title}</size></b>\n" +
                                     $"<color={GetHexColor(secondaryTextColor)}><size=24>{currentCourseIndex + 1} of {availableCourses.Length}</size></color>\n\n" +
                                     $"<b>Language:</b> {course.language}  |  <b>Difficulty:</b> {course.difficulty}\n" +
                                     $"<b>Questions:</b> {course.questionCount}  |  <b>Reward:</b> {course.pointReward} pts\n\n" +
                                     $"{completedTag}{course.summary}";
                
                if (prevButton != null) prevButton.gameObject.SetActive(availableCourses.Length > 1);
                if (nextButton != null) nextButton.gameObject.SetActive(availableCourses.Length > 1);
                if (actionButton != null)
                {
                    actionButton.gameObject.SetActive(true);
                    if (actionButtonText != null) actionButtonText.text = "Enroll Now";
                }
            }
            else
            {
                if (prevButton != null) prevButton.gameObject.SetActive(false);
                if (nextButton != null) nextButton.gameObject.SetActive(false);
                if (actionButton != null) actionButton.gameObject.SetActive(false);
            }
            
            bodyText.alignment = TextAnchor.UpperLeft;
        }
        else if (currentState == MenuState.Quiz)
        {
            titleText.text = currentCourseDetail.title;
            if (prevButton != null) prevButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (actionButton != null) actionButton.gameObject.SetActive(false);
            
            CourseQuestionDto q = currentCourseDetail.questions[currentQuestionIndex];
            communityBodyMessage = $"<color={GetHexColor(secondaryTextColor)}><size=24>Question {currentQuestionIndex + 1} of {currentCourseDetail.questions.Length}</size></color>\n\n" +
                                 $"<b><size=34>{q.prompt}</size></b>";
            
            bodyText.alignment = TextAnchor.MiddleCenter;
            
            for (int i = 0; i < 4; i++)
            {
                if (optionButtons[i] != null)
                {
                    if (i < q.options.Length)
                    {
                        optionButtons[i].gameObject.SetActive(true);
                        optionButtons[i].interactable = true;
                        optionButtonTexts[i].text = q.options[i];
                        ApplyButtonTheme(optionButtons[i], optionButtonTexts[i], q.options[i]);
                    }
                    else
                    {
                        optionButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }
        else if (currentState == MenuState.Result)
        {
            titleText.text = "Course Results";
            for (int i = 0; i < 4; i++) if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
            if (prevButton != null) prevButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            
            float percentage = (float)currentScore / currentCourseDetail.questions.Length;
            string colorHex = percentage >= 0.7f ? "#16a34a" : (percentage >= 0.4f ? "#ca8a04" : "#dc2626");
            
            communityBodyMessage = $"<size=48>Congratulations!</size>\n\n" +
                                 $"Your final score is\n" +
                                 $"<b><size=72><color={colorHex}>{currentScore}</color></size></b> / {currentCourseDetail.questions.Length}";
            
            bodyText.alignment = TextAnchor.MiddleCenter;
            
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(true);
                if (actionButtonText != null) actionButtonText.text = "Finish & Claim Reward";
            }
        }
        
        UpdateBodyText();
    }

    private string GetHexColor(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    private async void OnActionClicked()
    {
        if (currentState == MenuState.List && availableCourses != null && availableCourses.Length > 0)
        {
            long courseId = availableCourses[currentCourseIndex].id;
            fetchInProgress = true;
            communityBodyMessage = "Loading course details...";
            UpdateUIState();
            
            try
            {
                await GameClient.Instance.SendPacket(new FetchCourseDetailPacket(courseId));
            }
            catch (System.Exception ex)
            {
                communityBodyMessage = "Failed to load course: " + ex.Message;
                fetchInProgress = false;
                UpdateUIState();
            }
        }
        else if (currentState == MenuState.Result)
        {
            fetchInProgress = true;
            communityBodyMessage = "Submitting score...";
            UpdateUIState();
            
            try
            {
                await GameClient.Instance.SendPacket(new SubmitCourseCompletionPacket(currentCourseDetail.id, currentScore, currentCourseDetail.questions.Length));
            }
            catch (System.Exception ex)
            {
                communityBodyMessage = "Failed to submit score: " + ex.Message;
                fetchInProgress = false;
                currentState = MenuState.List;
                UpdateUIState();
            }
        }
    }

    private void OnOptionClicked(int optionIndex)
    {
        if (currentState != MenuState.Quiz) return;
        
        CourseQuestionDto q = currentCourseDetail.questions[currentQuestionIndex];
        
        if (optionIndex == q.correctIndex)
        {
            currentScore++;
        }
        
        currentQuestionIndex++;
        if (currentQuestionIndex >= currentCourseDetail.questions.Length)
        {
            currentState = MenuState.Result;
        }
        
        UpdateUIState();
    }

    private string BuildCourseListText(string coursesJson)
    {
        PublishedCourseSummaryList payload = ParsePublishedCourses(coursesJson);
        if (payload == null || payload.items == null || payload.items.Length == 0)
        {
            return "No published community quizzes were returned.";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Community quizzes:");
        builder.AppendLine();

        for (int i = 0; i < payload.items.Length; i++)
        {
            PublishedCourseSummary course = payload.items[i];
            builder.Append(i + 1);
            builder.Append(". ");
            builder.Append(string.IsNullOrWhiteSpace(course.title) ? "Untitled course" : course.title);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(course.language) ? "general" : course.language);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(course.difficulty) ? "beginner" : course.difficulty);
            builder.Append(" | ");
            builder.Append(course.questionCount);
            builder.Append(" questions");
            builder.Append(" | ");
            builder.Append(course.pointReward);
            builder.Append(" pts");

            if (course.completed)
            {
                builder.Append(" | completed");
            }

            if (!string.IsNullOrWhiteSpace(course.summary))
            {
                builder.AppendLine();
                builder.Append("   ");
                builder.Append(course.summary);
            }

            if (i < payload.items.Length - 1)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private PublishedCourseSummaryList ParsePublishedCourses(string coursesJson)
    {
        if (string.IsNullOrWhiteSpace(coursesJson))
        {
            return new PublishedCourseSummaryList { items = new PublishedCourseSummary[0] };
        }

        try
        {
            return JsonUtility.FromJson<PublishedCourseSummaryList>("{\"items\":" + coursesJson + "}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to parse published courses JSON: " + ex.Message);
            return new PublishedCourseSummaryList { items = new PublishedCourseSummary[0] };
        }
    }

    private IEnumerator FadeImage(Image image, float from, float to, float duration, Color color)
    {
        if (image == null)
        {
            yield break;
        }

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

    private void RestorePlayerState(BeanController sphere, FirstPersonControllerSimple fps)
    {
        SetPlayerLockState(sphere, fps, false, false);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(true);
        }
        SetOverlayVisible(false);
    }

    private static bool TryGetPlayer(Collider other, out BeanController sphere, out FirstPersonControllerSimple fps)
    {
        sphere = other.GetComponent<BeanController>() ?? other.GetComponentInParent<BeanController>();
        fps = other.GetComponent<FirstPersonControllerSimple>() ?? other.GetComponentInParent<FirstPersonControllerSimple>();
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

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        GameObject go = GetOrCreateUiObject(parent, name);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text EnsureText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize, FontStyle fontStyle, Color color)
    {
        GameObject go = GetOrCreateUiObject(parent, name);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Text text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        text.supportRichText = true;
        return text;
    }

    private static Button EnsureButton(Transform parent, string name, Vector2 anchor, Vector2 size, Color color, string label, int fontSize)
    {
        GameObject root = GetOrCreateUiObject(parent, name);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        image.color = color;

        Button button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = new Color(color.r + 0.08f, color.g + 0.08f, color.b + 0.08f, color.a);
        colors.pressedColor = new Color(color.r - 0.08f, color.g - 0.08f, color.b - 0.08f, color.a);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, color.a * 0.35f);
        button.colors = colors;

        Text text = EnsureText(root.transform, "Label", new Vector2(0.5f, 0.5f), size, fontSize, FontStyle.Bold, Color.white);
        text.text = label;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    private static GameObject GetOrCreateUiObject(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFullscreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
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

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
        Object.DontDestroyOnLoad(eventSystemObject);
    }

    private static void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
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


    [System.Serializable]
    private class CourseDetailDto
    {
        public long id;
        public string title;
        public CourseQuestionDto[] questions;
    }

    [System.Serializable]
    private class CourseQuestionDto
    {
        public int id;
        public string prompt;
        public string[] options;
        public int correctIndex;
        public string explanation;
    }

    private CourseDetailDto ParseCourseDetail(string json)
    {
        try
        {
            return JsonUtility.FromJson<CourseDetailDto>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to parse course detail JSON: " + ex.Message);
            return null;
        }
    }
    [System.Serializable]
    private class PublishedCourseSummaryList
    {
        public PublishedCourseSummary[] items;
    }

    [System.Serializable]
    private class PublishedCourseSummary
    {
        public long id;
        public string title;
        public string acronym;
        public string language;
        public string difficulty;
        public string summary;
        public int pointReward;
        public bool published;
        public int questionCount;
        public bool completed;
        public string updatedAt;
    }
}

public class CommunityIslandMenuIconBillboard : MonoBehaviour
{
    private Transform owner;

    public void SetOwner(Transform ownerTransform)
    {
        owner = ownerTransform;
    }

    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
        {
            return;
        }

        Vector3 direction = cam.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.LookAt(transform.position + direction.normalized, Vector3.up);
        transform.Rotate(0f, 180f, 0f, Space.Self);
    }

    private Camera ResolveCamera()
    {
        if (Application.isPlaying)
        {
            FirstPersonControllerSimple fps = Object.FindObjectOfType<FirstPersonControllerSimple>();
            if (fps != null)
            {
                Camera fpsCamera = fps.GetComponentInChildren<Camera>(true);
                if (fpsCamera != null)
                {
                    return fpsCamera;
                }
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }
        }

        Camera[] cameras = Camera.allCameras;
        if (cameras != null && cameras.Length > 0)
        {
            return cameras[0];
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
        {
            return UnityEditor.SceneView.lastActiveSceneView.camera;
        }
#endif

        return null;
    }
}

public static class CommunityIslandMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachIfNeeded()
    {
        AttachToIsland();
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void AttachInEditor()
    {
        UnityEditor.EditorApplication.delayCall += AttachToIsland;
    }
#endif

    private static void AttachToIsland()
    {
        GameObject island = GameObject.Find("SM_Generic_Ground_Flat_01 (1)");
        if (island == null)
        {
            return;
        }

        if (island.GetComponent<CommunityIslandMenu>() == null)
        {
            island.AddComponent<CommunityIslandMenu>();
        }
    }
}

public class CommunityIslandMenuTriggerRelay : MonoBehaviour
{
    private CommunityIslandMenu owner;

    public void Initialize(CommunityIslandMenu menu)
    {
        owner = menu;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null)
        {
            owner.HandleTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (owner != null)
        {
            owner.HandleTriggerExit(other);
        }
    }
}
