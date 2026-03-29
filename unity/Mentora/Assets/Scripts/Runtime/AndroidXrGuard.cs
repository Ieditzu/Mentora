using UnityEngine;
#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

/// <summary>
/// Disables XR loaders on Android phones (non-Quest) so the app won't crash when no headset is present.
/// </summary>
public static class AndroidXrGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void MaybeDisableXr()
    {
#if UNITY_ANDROID && UNITY_XR_MANAGEMENT
        // Heuristic: keep XR only on Quest/Meta devices.
        string model = SystemInfo.deviceModel.ToLowerInvariant();
        bool looksLikeQuest = model.Contains("quest") || model.Contains("meta quest");
        bool looksLikePico = model.Contains("pico");

        var settings = XRGeneralSettings.Instance;
        var manager = settings != null ? settings.Manager : null;
        if (manager == null)
        {
            return;
        }

        // Disable XR on phones; leave it on only for known HMDs.
        if (!looksLikeQuest && !looksLikePico)
        {
            settings.InitManagerOnStart = false;
            manager.automaticLoading = false;
            manager.automaticRunning = false;
            if (manager.isInitializationComplete)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }

            // Ensure no loaders remain active.
            manager.activeLoader = null;
            manager.loaders.Clear();
        }
#endif
    }
}
