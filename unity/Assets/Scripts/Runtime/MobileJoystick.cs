using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform joystickArea;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 70f;

    private Vector2 input;

    private void Awake()
    {
        if (joystickArea == null)
        {
            joystickArea = transform as RectTransform;
        }

        if (handle == null)
        {
            Transform child = transform.Find("JoystickHandle");
            if (child != null)
            {
                handle = child as RectTransform;
            }
        }
    }

    private void OnDisable()
    {
        input = Vector2.zero;
        UpdateVisual();
        MobileTouchInput.ResetMove();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        MobileTouchInput.SetJoystickActive(true);
        MobileTouchInput.SetJoystickPointer(eventData.pointerId);
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystickArea == null)
        {
            return;
        }

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickArea, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        float radius = Mathf.Min(joystickArea.rect.width, joystickArea.rect.height) * 0.5f;
        if (radius <= 0.001f)
        {
            return;
        }

        input = Vector2.ClampMagnitude(localPoint / radius, 1f);
        MobileTouchInput.SetMove(input);
        UpdateVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        input = Vector2.zero;
        MobileTouchInput.ResetMove();
        MobileTouchInput.SetJoystickActive(false);
        MobileTouchInput.ClearJoystickPointer();
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (handle == null)
        {
            return;
        }

        handle.anchoredPosition = input * handleRange;
    }
}
