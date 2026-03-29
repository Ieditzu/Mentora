using UnityEngine;

public static class MobileTouchInput
{
    private static Vector2 move;
    private static bool jumpRequested;
    private static bool joystickActive;
    private static int joystickPointerId = int.MinValue;

    public static Vector2 Move => move;
    public static bool JoystickActive => joystickActive;
    public static int JoystickPointerId => joystickPointerId;

    public static void SetMove(Vector2 value)
    {
        move = Vector2.ClampMagnitude(value, 1f);
    }

    public static void SetJoystickActive(bool active)
    {
        joystickActive = active;
    }

    public static void SetJoystickPointer(int pointerId)
    {
        joystickPointerId = pointerId;
    }

    public static void ClearJoystickPointer()
    {
        joystickPointerId = int.MinValue;
    }

    public static void RequestJump()
    {
        jumpRequested = true;
    }

    public static bool ConsumeJumpRequest()
    {
        if (!jumpRequested)
        {
            return false;
        }

        jumpRequested = false;
        return true;
    }

    public static void ResetMove()
    {
        move = Vector2.zero;
        joystickActive = false;
        joystickPointerId = int.MinValue;
    }
}
