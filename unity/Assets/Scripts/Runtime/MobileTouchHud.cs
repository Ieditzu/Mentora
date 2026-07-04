using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileTouchHud : MonoBehaviour
{
    private const string PauseButtonObjectName = "PauseButton";

    private bool lastPausedState = false;
    private Button pauseButton;
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
        if (paused == lastPausedState)
        {
            return;
        }

        ApplyPausedState(paused);
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
    }

    private static void OnPauseButtonClicked()
    {
        PauseMenuManager.VrTogglePause();
    }
}
