using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class HardQuestionPadTestSequence : MonoBehaviour
{
    [SerializeField] private float burstDuration = 0.32f;
    [SerializeField] private float fadeDuration = 0.42f;

    private static Canvas overlayCanvas;
    private static Image burstA;
    private static Image burstB;
    private static Image whiteImage;
    private static Text centerText;

    private Collider triggerCollider;
    private bool running;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = true;
        }

        EnsureOverlay();
        ResetOverlay();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (running)
        {
            return;
        }

        if (!TryGetPlayer(other, out FirstPersonControllerSimple fps))
        {
            return;
        }

        StartCoroutine(PlaySequence(fps));
    }

    private IEnumerator PlaySequence(FirstPersonControllerSimple fps)
    {
        running = true;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        EnsureOverlay();
        ResetOverlay();
        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(true);
        }

        SetPlayerLockState(fps, true, true);
        if (fps != null)
        {
            fps.SetCameraControlEnabled(false);
        }

        SetCursorVisible(false);
        yield return PlayBurst();
        yield return FadeWhite(0f, 1f, fadeDuration);

        if (centerText != null)
        {
            centerText.text = "tets";
            centerText.gameObject.SetActive(true);
        }
    }

    private IEnumerator PlayBurst()
    {
        if (burstA == null || burstB == null)
        {
            yield break;
        }

        burstA.gameObject.SetActive(true);
        burstB.gameObject.SetActive(true);
        float time = 0f;

        while (time < burstDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, burstDuration));
            float alpha = 1f - t;

            SetBurst(burstA, Mathf.Lerp(25f, 460f, t), Mathf.Lerp(25f, 180f, t), 0f, alpha);
            SetBurst(burstB, Mathf.Lerp(25f, 360f, t), Mathf.Lerp(25f, 140f, t), 90f, alpha * 0.9f);
            yield return null;
        }

        burstA.gameObject.SetActive(false);
        burstB.gameObject.SetActive(false);
    }

    private static void SetBurst(Image image, float width, float height, float rotationZ, float alpha)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(width, height);
        rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static IEnumerator FadeWhite(float from, float to, float duration)
    {
        if (whiteImage == null)
        {
            yield break;
        }

        Color color = whiteImage.color;
        color.a = from;
        whiteImage.color = color;
        whiteImage.gameObject.SetActive(true);

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.01f, duration));
            float smooth = t * t * (3f - (2f * t));
            color.a = Mathf.Lerp(from, to, smooth);
            whiteImage.color = color;
            yield return null;
        }

        color.a = to;
        whiteImage.color = color;
    }

    private static bool TryGetPlayer(Collider other, out FirstPersonControllerSimple fps)
    {
        fps = other.GetComponent<FirstPersonControllerSimple>() ?? other.GetComponentInParent<FirstPersonControllerSimple>();
        return fps != null;
    }

    private static void SetPlayerLockState(FirstPersonControllerSimple fps, bool movementLocked, bool hardFreeze)
    {
        if (fps != null)
        {
            fps.SetMovementLocked(movementLocked);
            fps.SetHardFreeze(hardFreeze);
        }
    }

    private static void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        if (!visible && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private static void EnsureOverlay()
    {
        if (overlayCanvas != null && whiteImage != null && burstA != null && burstB != null && centerText != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("HardQuestionPadOverlay");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("HardQuestionPadOverlay");
        }

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        if (overlayCanvas == null)
        {
            overlayCanvas = canvasObject.AddComponent<Canvas>();
        }
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 5500;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        whiteImage = EnsureImage("White", canvasObject.transform, Color.white);
        Stretch(whiteImage.rectTransform);

        burstA = EnsureImage("BurstA", canvasObject.transform, Color.white);
        burstB = EnsureImage("BurstB", canvasObject.transform, Color.white);
        CenterRect(burstA.rectTransform);
        CenterRect(burstB.rectTransform);

        centerText = EnsureText("CenterText", canvasObject.transform, "tets", 62);
        Stretch(centerText.rectTransform);
    }

    private static void ResetOverlay()
    {
        if (overlayCanvas == null || whiteImage == null || burstA == null || burstB == null || centerText == null)
        {
            return;
        }

        overlayCanvas.gameObject.SetActive(false);
        whiteImage.gameObject.SetActive(false);
        burstA.gameObject.SetActive(false);
        burstB.gameObject.SetActive(false);
        centerText.gameObject.SetActive(false);

        Color clear = Color.white;
        clear.a = 0f;
        whiteImage.color = clear;
        burstA.color = clear;
        burstB.color = clear;
    }

    private static Image EnsureImage(string name, Transform parent, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            imageObject.transform.SetParent(parent, false);
        }

        Image image = imageObject.GetComponent<Image>();
        if (image == null)
        {
            image = imageObject.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text EnsureText(string name, Transform parent, string value, int size)
    {
        Transform existing = parent.Find(name);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            textObject.transform.SetParent(parent, false);
        }

        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CenterRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(20f, 20f);
    }
}
