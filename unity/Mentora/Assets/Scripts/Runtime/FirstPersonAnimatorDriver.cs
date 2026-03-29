using UnityEngine;

/// <summary>
/// Drives a humanoid Animator on the FPS character using movement from FirstPersonControllerSimple.
/// Expects the Animator to have float "Speed" (0..1), bool "Grounded", and optional triggers "Jump" and "Land".
/// </summary>
[RequireComponent(typeof(FirstPersonControllerSimple))]
public class FirstPersonAnimatorDriver : MonoBehaviour
{
    [Tooltip("Animator on the visual character (can be on a child).")]
    public Animator characterAnimator;

    [Tooltip("Multiplier to convert meters/sec into Animator Speed parameter (0..1).")]
    public float speedScale = 0.2f;

    [Tooltip("Lerp smoothing for Speed parameter.")]
    public float speedLerp = 10f;

    [Tooltip("Name of Speed parameter.")]
    public string speedParam = "Speed";

    [Tooltip("Name of Grounded parameter.")]
    public string groundedParam = "Grounded";

    [Tooltip("Name of Jump trigger parameter (optional).")]
    public string jumpTrigger = "Jump";

    [Tooltip("Name of Land trigger parameter (optional).")]
    public string landTrigger = "Land";

    [Tooltip("Optional MotionSpeed parameter used by Starter Assets blend trees.")]
    public string motionSpeedParam = "MotionSpeed";

    private FirstPersonControllerSimple fps;
    private CharacterController cc;
    private float currentSpeed;
    private bool wasGrounded;

    private void Awake()
    {
        fps = GetComponent<FirstPersonControllerSimple>();
        cc = GetComponent<CharacterController>();
        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<Animator>();
        }
        if (characterAnimator != null)
        {
            characterAnimator.updateMode = AnimatorUpdateMode.Normal;
            characterAnimator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        if (characterAnimator == null || fps == null || cc == null)
        {
            return;
        }

        Vector3 horizontalVel = new Vector3(cc.velocity.x, 0f, cc.velocity.z);
        float targetSpeed = horizontalVel.magnitude * speedScale;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedLerp);
        characterAnimator.SetFloat(speedParam, currentSpeed);
        if (!string.IsNullOrEmpty(motionSpeedParam))
        {
            characterAnimator.SetFloat(motionSpeedParam, 1f);
        }

        bool grounded = cc.isGrounded;
        characterAnimator.SetBool(groundedParam, grounded);

        if (!wasGrounded && grounded && !string.IsNullOrEmpty(landTrigger))
        {
            characterAnimator.SetTrigger(landTrigger);
        }
        else if (wasGrounded && !grounded && !string.IsNullOrEmpty(jumpTrigger))
        {
            characterAnimator.SetTrigger(jumpTrigger);
        }

        wasGrounded = grounded;
    }
}
