using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

/// <summary>
/// Applies a conservative quality profile only when XR is actually running.
/// This avoids shipping the desktop-quality path into VR headsets, which is the main
/// cause of the current lag spikes and low framerate.
/// </summary>
public class VrPerformanceManager : MonoBehaviour
{
    private const string BootstrapObjectName = "VrPerformanceManager";
    private const int PerformantQualityIndex = 0;
    private const int AndroidVrTargetFps = 72;
    private const int DesktopVrTargetFps = 90;
    private const float AndroidEyeTextureScale = 0.95f;
    private const float DesktopEyeTextureScale = 1f;
    private const float AndroidViewportScale = 0.9f;
    private const float DesktopViewportScale = 1f;

    private static VrPerformanceManager instance;
    private bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject(BootstrapObjectName);
        instance = root.AddComponent<VrPerformanceManager>();
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

    private void Start()
    {
        StartCoroutine(WaitForXrAndApply());
    }

    private IEnumerator WaitForXrAndApply()
    {
        float timeoutAt = Time.realtimeSinceStartup + 12f;
        while (!applied && Time.realtimeSinceStartup < timeoutAt)
        {
            if (IsVrRunning())
            {
                ApplyVrSettings();
                yield break;
            }

            yield return null;
        }
    }

    private static bool IsVrRunning()
    {
        if (!XRSettings.enabled)
        {
            return false;
        }

        List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyVrSettings()
    {
        if (applied)
        {
            return;
        }

        applied = true;

        bool isAndroid = Application.platform == RuntimePlatform.Android;
        int targetFps = isAndroid ? AndroidVrTargetFps : DesktopVrTargetFps;

        if (QualitySettings.GetQualityLevel() != PerformantQualityIndex)
        {
            QualitySettings.SetQualityLevel(PerformantQualityIndex, true);
        }

        QualitySettings.vSyncCount = 0;
        QualitySettings.pixelLightCount = 0;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowDistance = 0f;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowCascades = 0;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.antiAliasing = 2;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.55f);
        QualitySettings.maximumLODLevel = Mathf.Max(QualitySettings.maximumLODLevel, 1);

        Application.targetFrameRate = targetFps;

        XRSettings.eyeTextureResolutionScale = isAndroid ? AndroidEyeTextureScale : DesktopEyeTextureScale;
        XRSettings.renderViewportScale = isAndroid ? AndroidViewportScale : DesktopViewportScale;

        DisableRealtimeSceneCosts();
        Debug.Log("VrPerformanceManager applied VR quality profile");
    }

    private static void DisableRealtimeSceneCosts()
    {
        Light[] lights = FindObjectsOfType<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
            {
                continue;
            }

            light.shadows = LightShadows.None;
            if (light.type != LightType.Directional)
            {
                light.renderMode = LightRenderMode.ForceVertex;
            }
        }

        ReflectionProbe[] probes = FindObjectsOfType<ReflectionProbe>(true);
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] == null)
            {
                continue;
            }

            probes[i].enabled = false;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                continue;
            }

            cameras[i].allowHDR = false;
            cameras[i].allowMSAA = true;
        }
    }
}
