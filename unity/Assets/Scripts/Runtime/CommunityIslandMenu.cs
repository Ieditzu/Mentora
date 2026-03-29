using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[ExecuteAlways]
public class CommunityIslandMenu : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Vector3 triggerSize = new Vector3(1f, 1f, 2f);
    [SerializeField] private Vector3 triggerOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private float reenterCooldown = 0.2f;
    [SerializeField] private Vector3 iconLocalPosition = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Vector3 iconLocalScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private bool autoPlaceTriggerWhenMissing = true;

    [Header("Theme")]
    [SerializeField] private Color pageTint = new Color(0.98f, 0.98f, 0.96f, 1f);
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.96f);
    [SerializeField] private Color textColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.28f, 0.31f, 0.36f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.14f, 0.14f, 0.14f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.18f, 0.62f, 0.32f, 1f);

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

    private GameObject triggerObject;
    private BoxCollider triggerVolume;
    private bool running;
    private bool leaveRequested;
    private bool overlayInteractionActive;

    private void Awake()
    {
        EnsureTriggerVolume();
    }

    private void OnEnable()
    {
        EnsureTriggerVolume();
    }

    private void OnValidate()
    {
        EnsureTriggerVolume();
    }

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
        visualObject.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
    }

    public void HandleTriggerEnter(Collider other)
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

        yield return FadeImage(whiteImage, 0f, 1f, fadeToWhiteDuration, pageTint);
        yield return AnimatePanel(true);

        ShowLeaveButton(true);
        titleText.text = "Community";
        bodyText.text = string.Empty;

        while (!leaveRequested)
        {
            yield return null;
        }

        ShowLeaveButton(false);
        yield return AnimatePanel(false);
        yield return FadeImage(whiteImage, 1f, 0f, 0.2f, pageTint);

        RestorePlayerState(sphere, fps);
        StartCoroutine(ReenableTriggerAfterDelay());
    }

    private IEnumerator ReenableTriggerAfterDelay()
    {
        yield return new WaitForSeconds(reenterCooldown);
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
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(860f, 520f);

        Outline panelOutline = panelImage.GetComponent<Outline>() ?? panelImage.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.16f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        titleText = EnsureText(panelImage.transform, "TitleText", new Vector2(0.5f, 0.71f), new Vector2(720f, 84f), 42, FontStyle.Bold, textColor);
        bodyText = EnsureText(panelImage.transform, "BodyText", new Vector2(0.5f, 0.5f), new Vector2(720f, 120f), 24, FontStyle.Normal, secondaryTextColor);
        bodyText.alignment = TextAnchor.MiddleCenter;

        leaveButton = EnsureButton(canvasObject.transform, "LeaveButton", new Vector2(0.11f, 0.11f), new Vector2(180f, 56f), buttonColor, "Leave", 24);
        leaveButtonText = leaveButton.GetComponentInChildren<Text>(true);

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

    private void OnLeaveClicked()
    {
        leaveRequested = true;
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
}
