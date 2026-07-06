using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileTouchHud : MonoBehaviour
{
    private const string PauseButtonObjectName = "PauseButton";
    private const string CodeWorldButtonObjectName = "CodeWorldButton";
    private const float CodeWorldButtonSpacing = 18f;

    private bool lastPausedState = false;
    private Button pauseButton;
    private Button codeWorldButton;
    private GameObject[] gameplayControls = System.Array.Empty<GameObject>();

    private void Awake()
    {
        bool shouldShow = Input.touchSupported;
        gameObject.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        EnsureEventSystem();
        BindPauseButton();
        EnsureCodeWorldButton();
        CacheGameplayControls();
        ApplyPausedState(PauseMenuManager.IsGamePaused);
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        bool paused = PauseMenuManager.IsGamePaused;
        if (paused != lastPausedState)
        {
            ApplyPausedState(paused);
        }

        UpdateCodeWorldButtonVisibility();
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

    private void BindPauseButton()
    {
        Transform existing = transform.Find(PauseButtonObjectName);
        if (existing == null)
        {
            Debug.LogWarning("[MobileTouchHud] PauseButton child was not found under MobileTouchControls.");
            return;
        }

        pauseButton = existing.GetComponent<Button>();
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
            pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }
    }

    private void EnsureCodeWorldButton()
    {
        if (pauseButton == null)
        {
            return;
        }

        Transform existing = transform.Find(CodeWorldButtonObjectName);
        if (existing != null)
        {
            codeWorldButton = existing.GetComponent<Button>();
        }
        else
        {
            GameObject clone = Instantiate(pauseButton.gameObject, transform);
            clone.name = CodeWorldButtonObjectName;
            codeWorldButton = clone.GetComponent<Button>();

            RectTransform pauseRect = pauseButton.GetComponent<RectTransform>();
            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            if (pauseRect != null && cloneRect != null)
            {
                cloneRect.anchorMin = pauseRect.anchorMin;
                cloneRect.anchorMax = pauseRect.anchorMax;
                cloneRect.pivot = pauseRect.pivot;
                cloneRect.sizeDelta = pauseRect.sizeDelta;
                cloneRect.anchoredPosition = pauseRect.anchoredPosition + new Vector2(pauseRect.sizeDelta.x + CodeWorldButtonSpacing, 0f);
                cloneRect.localScale = pauseRect.localScale;
                cloneRect.localRotation = pauseRect.localRotation;
            }
        }

        if (codeWorldButton == null)
        {
            return;
        }

        codeWorldButton.onClick.RemoveAllListeners();
        codeWorldButton.onClick.AddListener(OnCodeWorldButtonClicked);
        UpdateCodeWorldButtonVisuals();
        UpdateCodeWorldButtonVisibility();
    }

    private void CacheGameplayControls()
    {
        int childCount = transform.childCount;
        System.Collections.Generic.List<GameObject> controls = new System.Collections.Generic.List<GameObject>(childCount);
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name == PauseButtonObjectName)
            {
                continue;
            }

            controls.Add(child.gameObject);
        }

        gameplayControls = controls.ToArray();
    }

    private void ApplyPausedState(bool paused)
    {
        lastPausedState = paused;

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(!paused);
        }

        for (int i = 0; i < gameplayControls.Length; i++)
        {
            if (gameplayControls[i] != null)
            {
                gameplayControls[i].SetActive(!paused);
            }
        }

        if (paused)
        {
            MobileTouchInput.ResetMove();
        }

        UpdateCodeWorldButtonVisibility();
    }

    private void UpdateCodeWorldButtonVisuals()
    {
        if (codeWorldButton == null)
        {
            return;
        }

        Text[] texts = codeWorldButton.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
            {
                continue;
            }

            texts[i].text = "</>";
            texts[i].fontSize = Mathf.Max(texts[i].fontSize, 28);
            texts[i].alignment = TextAnchor.MiddleCenter;
            texts[i].resizeTextForBestFit = false;
        }
    }

    private void UpdateCodeWorldButtonVisibility()
    {
        if (codeWorldButton == null)
        {
            return;
        }

        bool shouldShow = !PauseMenuManager.IsGamePaused && CodeWorldRuntime.ShouldShowMobileToggle;
        codeWorldButton.gameObject.SetActive(shouldShow);
    }

    private static void OnPauseButtonClicked()
    {
        PauseMenuManager.VrTogglePause();
    }

    private static void OnCodeWorldButtonClicked()
    {
        CodeWorldRuntime.ToggleEditorFromMobile();
    }
}
