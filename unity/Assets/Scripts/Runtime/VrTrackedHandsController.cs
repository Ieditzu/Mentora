using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

[RequireComponent(typeof(FirstPersonControllerSimple))]
public class VrTrackedHandsController : MonoBehaviour
{
    private const string DefaultLeftPrefabPath = "XRHands/Prefabs/Left Hand Tracking";
    private const string DefaultRightPrefabPath = "XRHands/Prefabs/Right Hand Tracking";

    [Header("Tracked Hands")]
    [SerializeField] private bool enableTrackedHands = true;
    [SerializeField] private bool hidePlaceholderControllersWhenTracked = true;
    [SerializeField] private string leftHandPrefabPath = DefaultLeftPrefabPath;
    [SerializeField] private string rightHandPrefabPath = DefaultRightPrefabPath;

    private static readonly List<XRHandSubsystem> HandSubsystems = new List<XRHandSubsystem>();

    private FirstPersonControllerSimple controller;
    private Transform handsRoot;
    private GameObject leftHandInstance;
    private GameObject rightHandInstance;
    private XRHandSubsystem handSubsystem;
    private bool xrHeadOriginCaptured;
    private Vector3 xrHeadOriginLocalPosition;

    private void Awake()
    {
        controller = GetComponent<FirstPersonControllerSimple>();
        EnsureHandsRoot();
        EnsureHandInstances();
        SetHandsEnabled(false);
    }

    private void Update()
    {
        if (!enableTrackedHands || !XRSettings.enabled)
        {
            xrHeadOriginCaptured = false;
            SetHandsEnabled(false);
            SetPlaceholderControllersVisible(true);
            return;
        }

        EnsureHandsRoot();
        EnsureHandInstances();
        UpdateHandsRootAlignment();

        handSubsystem = GetRunningHandSubsystem();
        bool leftTracked = handSubsystem != null && handSubsystem.leftHand.isTracked;
        bool rightTracked = handSubsystem != null && handSubsystem.rightHand.isTracked;
        bool anyTracked = leftTracked || rightTracked;

        SetHandsEnabled(true);
        SetPlaceholderControllersVisible(!anyTracked);
    }

    private void EnsureHandsRoot()
    {
        if (handsRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("VrTrackedHandsRoot");
        if (existing != null)
        {
            handsRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("VrTrackedHandsRoot");
        handsRoot = rootObject.transform;
        handsRoot.SetParent(transform, false);
        handsRoot.localPosition = Vector3.zero;
        handsRoot.localRotation = Quaternion.identity;
    }

    private void EnsureHandInstances()
    {
        if (handsRoot == null)
        {
            return;
        }

        if (leftHandInstance == null)
        {
            GameObject leftPrefab = Resources.Load<GameObject>(leftHandPrefabPath);
            if (leftPrefab != null)
            {
                leftHandInstance = Instantiate(leftPrefab, handsRoot);
                leftHandInstance.name = leftPrefab.name;
            }
        }

        if (rightHandInstance == null)
        {
            GameObject rightPrefab = Resources.Load<GameObject>(rightHandPrefabPath);
            if (rightPrefab != null)
            {
                rightHandInstance = Instantiate(rightPrefab, handsRoot);
                rightHandInstance.name = rightPrefab.name;
            }
        }
    }

    private void UpdateHandsRootAlignment()
    {
        if (handsRoot == null)
        {
            return;
        }

        if (InputDevices.GetDeviceAtXRNode(XRNode.Head)
            .TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 headLocalPosition))
        {
            if (!xrHeadOriginCaptured)
            {
                xrHeadOriginLocalPosition = headLocalPosition;
                xrHeadOriginCaptured = true;
            }

            handsRoot.localPosition = new Vector3(
                -xrHeadOriginLocalPosition.x,
                controller.GetVrEyeHeight() - xrHeadOriginLocalPosition.y,
                -xrHeadOriginLocalPosition.z);
        }
        else
        {
            xrHeadOriginCaptured = false;
            handsRoot.localPosition = Vector3.zero;
        }

        handsRoot.localRotation = Quaternion.identity;
    }

    private XRHandSubsystem GetRunningHandSubsystem()
    {
        if (handSubsystem != null && handSubsystem.running)
        {
            return handSubsystem;
        }

        HandSubsystems.Clear();
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

    private void SetHandsEnabled(bool enabled)
    {
        if (leftHandInstance != null)
        {
            leftHandInstance.SetActive(enabled);
        }

        if (rightHandInstance != null)
        {
            rightHandInstance.SetActive(enabled);
        }
    }

    private void SetPlaceholderControllersVisible(bool visible)
    {
        if (!hidePlaceholderControllersWhenTracked || controller == null)
        {
            return;
        }

        controller.SetHandTrackingVisualsActive(!visible);
    }
}
