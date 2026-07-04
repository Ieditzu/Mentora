using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJumpButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        MobileTouchInput.RequestJump();
        AudioManager.Play(MenSfx.Jump);
    }
}
