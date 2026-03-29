using UnityEngine;
using UnityEngine.EventSystems;

public class MobileTouchHud : MonoBehaviour
{
    private void Awake()
    {
        bool shouldShow = Input.touchSupported;
        gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            EnsureEventSystem();
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
}
