using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRInputDevice = UnityEngine.XR.InputDevice;

/// <summary>
/// Lightweight Quest/OpenXR hand-tracking support for existing controller-first VR flows.
/// Exposes tracked hand poses, pinch state, and simple debug-style hand visuals.
/// </summary>
public sealed class VrHandTracking : MonoBehaviour
{
    private const string BootstrapObjectName = "VrHandTracking";
    private const float PinchPressDistance = 0.028f;
    private const float PinchReleaseDistance = 0.04f;
    private const float DefaultJointScale = 0.045f;
    private static readonly Vector3 PalmVisualScale = new Vector3(0.11f, 0.02f, 0.11f);
    private const float DebugRayLength = 0.45f;
    private const float DebugRayThickness = 0.012f;
    private const float DebugOriginScale = 0.09f;

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();
    private static VrHandTracking instance;
    private HandVisual leftVisual;
    private HandVisual rightVisual;
    private HandRayVisual leftRayVisual;
    private HandRayVisual rightRayVisual;
    private Material leftMaterial;
    private Material rightMaterial;
    private FirstPersonControllerSimple cachedFps;
    private bool leftPinching;
    private bool rightPinching;
    private bool runtimeHandModeConfigured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject(BootstrapObjectName);
        instance = root.AddComponent<VrHandTracking>();
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
        leftMaterial = CreateHandMaterial(new Color(0.24f, 0.78f, 0.97f, 0.95f));
        rightMaterial = CreateHandMaterial(new Color(1f, 0.73f, 0.24f, 0.95f));
        leftVisual = new HandVisual(CreateHandRoot("LeftTrackedHand", leftMaterial));
        rightVisual = new HandVisual(CreateHandRoot("RightTrackedHand", rightMaterial));
        leftRayVisual = new HandRayVisual(CreateHandRayRoot("LeftTrackedHandRay", leftMaterial));
        rightRayVisual = new HandRayVisual(CreateHandRayRoot("RightTrackedHandRay", rightMaterial));
    }

    private void Update()
    {
        // Hand-tracking visuals are built in Awake. If they're missing, this
        // instance never finished initializing (e.g. running on desktop with no
        // VR runtime, where the OVR stack can fault during startup). Bail out so
        // we don't spam NullReferenceExceptions every frame. Real VR builds
        // complete Awake, so this guard never trips there.
        if (leftVisual?.Root == null || rightVisual?.Root == null ||
            leftRayVisual?.Root == null || rightRayVisual?.Root == null)
        {
            return;
        }

        TryConfigureRuntimeHandMode();

        cachedFps = PlayerCache.GetFps();
        ReparentVisual(leftVisual.Root, null);
        ReparentVisual(rightVisual.Root, null);
        ResetVisualRoot(leftVisual.Root);
        ResetVisualRoot(rightVisual.Root);

        bool leftTracked = UpdateHand(XRNode.LeftHand, ref leftPinching, leftVisual);
        bool rightTracked = UpdateHand(XRNode.RightHand, ref rightPinching, rightVisual);
        UpdateHandRay(XRNode.LeftHand, leftTracked, leftRayVisual);
        UpdateHandRay(XRNode.RightHand, rightTracked, rightRayVisual);
        cachedFps?.SetHandTrackingVisualsActive(leftTracked || rightTracked);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool IsHandTrackingActive()
    {
        return instance != null && (instance.leftVisual.Root.gameObject.activeSelf || instance.rightVisual.Root.gameObject.activeSelf);
    }

    public static bool IsPinching(XRNode node)
    {
        if (instance == null)
        {
            return false;
        }

        return node == XRNode.LeftHand ? instance.leftPinching : instance.rightPinching;
    }

    public static bool AreBothHandsPinching()
    {
        return IsPinching(XRNode.LeftHand) && IsPinching(XRNode.RightHand);
    }

    public static bool TryGetPointerRay(XRNode node, Transform reference, out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;

        if (TryGetOvrPointerRay(node, reference, out origin, out direction))
        {
            return true;
        }

        if (!TryGetJointPose(node, XRHandJointID.IndexTip, out Pose indexPose) ||
            !TryGetJointPose(node, XRHandJointID.Wrist, out Pose wristPose))
        {
            return false;
        }

        Vector3 pinchPosition = indexPose.position;
        if (TryGetJointPose(node, XRHandJointID.ThumbTip, out Pose thumbPose))
        {
            pinchPosition = (indexPose.position + thumbPose.position) * 0.5f;
        }

        Vector3 localDirection = (pinchPosition - wristPose.position).normalized;
        if (localDirection.sqrMagnitude < 0.0001f)
        {
            localDirection = indexPose.rotation * Vector3.forward;
        }

        if (reference != null)
        {
            origin = reference.TransformPoint(pinchPosition);
            direction = reference.TransformDirection(localDirection).normalized;
            return true;
        }

        origin = pinchPosition;
        direction = localDirection;
        return true;
    }

    private static bool TryGetJointPose(XRNode node, XRHandJointID jointId, out Pose pose)
    {
        pose = Pose.identity;
        if (!TryGetHand(node, out XRHand hand))
        {
            return false;
        }

        XRHandJoint joint = hand.GetJoint(jointId);
        return joint.TryGetPose(out pose);
    }

    private static bool TryGetHand(XRNode node, out XRHand hand)
    {
        hand = default;
        XRHandSubsystem subsystem = GetRunningSubsystem();
        if (subsystem == null)
        {
            return false;
        }

        hand = node == XRNode.LeftHand ? subsystem.leftHand : subsystem.rightHand;
        return hand.isTracked;
    }

    private static XRHandSubsystem GetRunningSubsystem()
    {
        SubsystemManager.GetSubsystems(HandSubsystems);
        for (int i = 0; i < HandSubsystems.Count; i++)
        {
            XRHandSubsystem subsystem = HandSubsystems[i];
            if (subsystem != null && subsystem.running)
            {
                return subsystem;
            }
        }

        return null;
    }

    private bool UpdateHand(XRNode node, ref bool pinchState, HandVisual visual)
    {
        if (TryUpdateOvrHand(node, ref pinchState, visual))
        {
            return true;
        }

        bool tracked = TryGetHand(node, out XRHand hand)
            && TryUpdateJoint(visual.Wrist, hand.GetJoint(XRHandJointID.Wrist), DefaultJointScale * 0.75f)
            && TryUpdatePalm(visual.Palm, hand.GetJoint(XRHandJointID.Palm))
            && TryUpdateJoint(visual.IndexTip, hand.GetJoint(XRHandJointID.IndexTip), DefaultJointScale)
            && TryUpdateJoint(visual.ThumbTip, hand.GetJoint(XRHandJointID.ThumbTip), DefaultJointScale);

        visual.Root.gameObject.SetActive(tracked);
        if (!tracked)
        {
            pinchState = false;
            return false;
        }

        Vector3 pinchOffset = (visual.IndexTip.position + visual.ThumbTip.position) * 0.5f;
        visual.PinchMarker.position = pinchOffset;
        visual.PinchMarker.rotation = Quaternion.identity;
        visual.PinchMarker.localScale = Vector3.one * DefaultJointScale * 0.95f;

        float pinchDistance = Vector3.Distance(visual.IndexTip.position, visual.ThumbTip.position);
        pinchState = pinchState ? pinchDistance <= PinchReleaseDistance : pinchDistance <= PinchPressDistance;
        visual.PinchMarker.gameObject.SetActive(pinchState);
        return true;
    }

    private bool TryUpdateOvrHand(XRNode node, ref bool pinchState, HandVisual visual)
    {
        if (!TryGetOvrHandState(node, out OVRPlugin.HandState handState))
        {
            visual.Root.gameObject.SetActive(false);
            pinchState = false;
            return false;
        }

        OVRPose rootPose = handState.RootPose.ToOVRPose();
        OVRPose pointerPose = handState.PointerPose.ToOVRPose();

        visual.Root.gameObject.SetActive(true);
        ApplyTrackingPose(visual.Wrist, rootPose.position, rootPose.orientation);
        visual.Wrist.localScale = Vector3.one * (DefaultJointScale * 0.75f);

        ApplyTrackingPose(
            visual.Palm,
            rootPose.position + rootPose.orientation * new Vector3(0f, 0.015f, 0.03f),
            rootPose.orientation);
        visual.Palm.localScale = PalmVisualScale;

        ApplyTrackingPose(visual.IndexTip, pointerPose.position, pointerPose.orientation);
        visual.IndexTip.localScale = Vector3.one * DefaultJointScale;

        ApplyTrackingPose(
            visual.ThumbTip,
            pointerPose.position + pointerPose.orientation * new Vector3(-0.025f, -0.015f, -0.01f),
            pointerPose.orientation);
        visual.ThumbTip.localScale = Vector3.one * DefaultJointScale;

        float pinchStrength = GetOvrPinchStrength(handState);
        float pressThreshold = pinchState ? 0.55f : 0.72f;
        pinchState = pinchStrength >= pressThreshold;

        visual.PinchMarker.position = (visual.IndexTip.position + visual.ThumbTip.position) * 0.5f;
        visual.PinchMarker.rotation = Quaternion.identity;
        visual.PinchMarker.localScale = Vector3.one * DefaultJointScale * 0.95f;
        visual.PinchMarker.gameObject.SetActive(pinchState);
        return true;
    }

    private void UpdateHandRay(XRNode node, bool handTracked, HandRayVisual rayVisual)
    {
        if (rayVisual == null)
        {
            return;
        }

        Transform reference = GetTrackingReference();
        bool hasRay = TryGetPointerRay(node, reference, out Vector3 origin, out Vector3 direction);
        bool visible = handTracked && hasRay;
        rayVisual.Root.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        rayVisual.Origin.position = origin;
        rayVisual.Origin.rotation = Quaternion.identity;
        rayVisual.Origin.localScale = Vector3.one * DebugOriginScale;

        Vector3 rayCenter = origin + direction.normalized * (DebugRayLength * 0.5f);
        rayVisual.Beam.position = rayCenter;
        rayVisual.Beam.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        rayVisual.Beam.localScale = new Vector3(DebugRayThickness, DebugRayLength * 0.5f, DebugRayThickness);
    }

    private void TryConfigureRuntimeHandMode()
    {
        if (runtimeHandModeConfigured)
        {
            return;
        }

        try
        {
            OVRPlugin.SetMultimodalHandsControllersSupported(false);
            OVRPlugin.SetSimultaneousHandsAndControllersEnabled(false);
            runtimeHandModeConfigured = true;
        }
        catch
        {
            // OVR runtime may not be initialized on the first frames. Retry on later updates.
        }
    }

    private static bool TryGetOvrPointerRay(XRNode node, Transform reference, out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;

        if (!TryGetOvrHandState(node, out OVRPlugin.HandState handState))
        {
            return false;
        }

        OVRPose pointerPose = handState.PointerPose.ToOVRPose();
        Vector3 localOrigin = pointerPose.position;
        Vector3 localDirection = pointerPose.orientation * Vector3.forward;

        if (reference != null)
        {
            origin = reference.TransformPoint(localOrigin);
            direction = reference.TransformDirection(localDirection).normalized;
            return true;
        }

        origin = localOrigin;
        direction = localDirection.normalized;
        return true;
    }

    private static bool TryGetOvrHandState(XRNode node, out OVRPlugin.HandState handState)
    {
        handState = new OVRPlugin.HandState();
        return TryReadOvrHandState(node, ref handState);
    }

    private static bool TryReadOvrHandState(XRNode node, ref OVRPlugin.HandState handState)
    {
        if (!OVRPlugin.GetHandTrackingEnabled())
        {
            return false;
        }

        OVRPlugin.Hand hand = node == XRNode.LeftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
        if (!OVRPlugin.GetHandState(OVRPlugin.Step.Render, hand, ref handState))
        {
            return false;
        }

        bool tracked = (handState.Status & OVRPlugin.HandStatus.HandTracked) != 0;
        bool inputValid = (handState.Status & OVRPlugin.HandStatus.InputStateValid) != 0;
        bool confidenceValid = !HasConnectedTouchControllers()
            || handState.HandConfidence == OVRPlugin.TrackingConfidence.High;
        return tracked && inputValid && confidenceValid;
    }

    private static bool HasConnectedTouchControllers()
    {
        OVRInput.Controller connectedControllers = OVRInput.GetConnectedControllers();
        return (connectedControllers & OVRInput.Controller.LTouch) != 0
            || (connectedControllers & OVRInput.Controller.RTouch) != 0;
    }

    private static float GetOvrPinchStrength(OVRPlugin.HandState handState)
    {
        if (handState.PinchStrength != null && handState.PinchStrength.Length > (int)OVRPlugin.HandFinger.Index)
        {
            return handState.PinchStrength[(int)OVRPlugin.HandFinger.Index];
        }

        bool indexPinch = (((int)handState.Pinches) & (1 << (int)OVRPlugin.HandFinger.Index)) != 0;
        return indexPinch ? 1f : 0f;
    }

    private static bool TryUpdatePalm(Transform target, XRHandJoint joint)
    {
        if (!joint.TryGetPose(out Pose pose))
        {
            return false;
        }

        ApplyTrackingPose(target, pose.position, pose.rotation);
        target.localScale = PalmVisualScale;
        return true;
    }

    private static bool TryUpdateJoint(Transform target, XRHandJoint joint, float fallbackScale)
    {
        if (!joint.TryGetPose(out Pose pose))
        {
            return false;
        }

        ApplyTrackingPose(target, pose.position, pose.rotation);

        float radius = fallbackScale * 0.5f;
        if (joint.TryGetRadius(out float jointRadius) && jointRadius > 0f)
        {
            radius = jointRadius;
        }

        target.localScale = Vector3.one * Mathf.Max(radius * 2f, fallbackScale);
        return true;
    }

    private static Transform GetTrackingReference()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera != null && activeCamera.transform.parent != null)
        {
            return activeCamera.transform.parent;
        }

        return instance != null && instance.cachedFps != null ? instance.cachedFps.transform : instance.transform;
    }

    private static void ResetVisualRoot(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.position = Vector3.zero;
        visualRoot.rotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private static void ApplyTrackingPose(Transform target, Vector3 trackingPosition, Quaternion trackingRotation)
    {
        if (target == null)
        {
            return;
        }

        Transform trackingReference = GetTrackingReference();
        if (trackingReference != null)
        {
            target.position = trackingReference.TransformPoint(trackingPosition);
            target.rotation = trackingReference.rotation * trackingRotation;
            return;
        }

        target.position = trackingPosition;
        target.rotation = trackingRotation;
    }

    private static void ReparentVisual(Transform visualRoot, Transform parent)
    {
        if (visualRoot == null || visualRoot.parent == parent)
        {
            return;
        }

        visualRoot.SetParent(parent, false);
    }

    private static Transform CreateHandRoot(string name, Material material)
    {
        GameObject root = new GameObject(name);
        DontDestroyOnLoad(root);

        CreateMarker("Wrist", PrimitiveType.Sphere, root.transform, material);
        CreateMarker("Palm", PrimitiveType.Cube, root.transform, material);
        CreateMarker("IndexTip", PrimitiveType.Sphere, root.transform, material);
        CreateMarker("ThumbTip", PrimitiveType.Sphere, root.transform, material);
        Transform pinch = CreateMarker("Pinch", PrimitiveType.Sphere, root.transform, material);
        pinch.gameObject.SetActive(false);
        return root.transform;
    }

    private static Transform CreateHandRayRoot(string name, Material material)
    {
        GameObject root = new GameObject(name);
        DontDestroyOnLoad(root);

        CreateMarker("Origin", PrimitiveType.Sphere, root.transform, material);
        CreateMarker("Beam", PrimitiveType.Cylinder, root.transform, material);
        root.SetActive(false);
        return root.transform;
    }

    private static Transform CreateMarker(string name, PrimitiveType primitiveType, Transform parent, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(primitiveType);
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.layer = parent.gameObject.layer;

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return marker.transform;
    }

    private static Material CreateHandMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 0.6f);
            material.EnableKeyword("_EMISSION");
        }

        return material;
    }

    private sealed class HandVisual
    {
        public HandVisual(Transform root)
        {
            Root = root;
            Wrist = root.Find("Wrist");
            Palm = root.Find("Palm");
            IndexTip = root.Find("IndexTip");
            ThumbTip = root.Find("ThumbTip");
            PinchMarker = root.Find("Pinch");
            Root.gameObject.SetActive(false);
        }

        public Transform Root { get; }
        public Transform Wrist { get; }
        public Transform Palm { get; }
        public Transform IndexTip { get; }
        public Transform ThumbTip { get; }
        public Transform PinchMarker { get; }
    }

    private sealed class HandRayVisual
    {
        public HandRayVisual(Transform root)
        {
            Root = root;
            Origin = root.Find("Origin");
            Beam = root.Find("Beam");
            Root.gameObject.SetActive(false);
        }

        public Transform Root { get; }
        public Transform Origin { get; }
        public Transform Beam { get; }
    }
}
