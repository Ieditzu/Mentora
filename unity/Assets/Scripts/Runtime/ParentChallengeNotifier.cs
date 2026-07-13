using System.Collections;
using Mentora.Network;
using UnityEngine;
using UnityEngine.UI;

public class ParentChallengeNotifier : MonoBehaviour
{
    private static ParentChallengeNotifier instance;

    private Canvas canvas;
    private Text titleText;
    private Text bodyText;
    private Coroutine hideRoutine;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("ParentChallengeNotifier");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ParentChallengeNotifier>();
    }

    private void Start()
    {
        if (!UnityMainThreadDispatcher.IsInitialized)
        {
            UnityMainThreadDispatcher.Initialize();
        }
        BuildUi();
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null && subscribed)
        {
            GameClient.Instance.OnPacketReceived -= OnPacketReceived;
        }
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (GameClient.Instance == null)
        {
            yield return null;
        }

        if (!subscribed)
        {
            GameClient.Instance.OnPacketReceived += OnPacketReceived;
            subscribed = true;
        }
    }

    private void OnPacketReceived(Packet packet)
    {
        if (packet is ParentChallengePacket challenge)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => ShowChallenge(challenge.Message));
        }
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("ParentChallengeCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        canvasGo.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -28f);
        panelRt.sizeDelta = new Vector2(760f, 170f);

        titleText = AddText(panel.transform, "Challenge from parent", 28, FontStyle.Bold, new Color(0.70f, 0.86f, 1f));
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.62f);
        titleRt.anchorMax = new Vector2(1f, 0.96f);
        titleRt.offsetMin = new Vector2(26f, 0f);
        titleRt.offsetMax = new Vector2(-26f, 0f);
        titleText.alignment = TextAnchor.MiddleLeft;

        bodyText = AddText(panel.transform, "", 26, FontStyle.Normal, Color.white);
        RectTransform bodyRt = bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0.08f);
        bodyRt.anchorMax = new Vector2(1f, 0.62f);
        bodyRt.offsetMin = new Vector2(26f, 0f);
        bodyRt.offsetMax = new Vector2(-26f, 0f);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;

        canvasGo.SetActive(false);
    }

    private Text AddText(Transform parent, string value, int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MentoraLocalization.Register(text, value);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        return text;
    }

    private void ShowChallenge(string message)
    {
        if (canvas == null) BuildUi();
        bodyText.text = string.IsNullOrWhiteSpace(message) ? MentoraLocalization.Localize("Try one more challenge tonight.") : message.Trim();
        canvas.gameObject.SetActive(true);

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(10f);
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
        hideRoutine = null;
    }
}
