using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

/// <summary>
/// Lets the player re-zero VR tracking against the FPS rig during play.
/// Hold both grip buttons briefly to recalibrate the headset/controller origin.
/// </summary>
public class VrTrackingCalibrator : MonoBehaviour
{
    private const string BootstrapObjectName = "VrTrackingCalibrator";

    [SerializeField] private float holdDuration = 0.65f;

    private static VrTrackingCalibrator instance;
    private float holdTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject(BootstrapObjectName);
        instance = root.AddComponent<VrTrackingCalibrator>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!XRSettings.enabled || !XRSettings.isDeviceActive)
        {
            holdTimer = 0f;
            return;
        }

        if (!AreBothGripButtonsHeld())
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.unscaledDeltaTime;
        if (holdTimer < holdDuration)
        {
            return;
        }

        holdTimer = 0f;
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps != null)
        {
            fps.RecalibrateVrTracking();
            Debug.Log("VR tracking recalibrated");
        }
    }

    private static bool AreBothGripButtonsHeld()
    {
        if (VrHandTracking.AreBothHandsPinching())
        {
            return true;
        }

        return IsGripHeld(XRNode.LeftHand) && IsGripHeld(XRNode.RightHand);
    }

    private static bool IsGripHeld(XRNode node)
    {
        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripPressed))
        {
            return gripPressed;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryPressed))
        {
            return primaryPressed;
        }

        return false;
    }
}
