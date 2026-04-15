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
        // Prefer manufacturer/vendor detection over model-only matching.
        // Quest device models are not consistently reported as "Quest" across OS versions.
        string model = (SystemInfo.deviceModel ?? string.Empty).ToLowerInvariant();
        string deviceName = (SystemInfo.deviceName ?? string.Empty).ToLowerInvariant();
        string manufacturer = GetAndroidBuildField("MANUFACTURER");

        bool looksLikeQuest =
            model.Contains("quest") ||
            deviceName.Contains("quest") ||
            manufacturer.Contains("meta") ||
            manufacturer.Contains("oculus");

        bool looksLikePico =
            model.Contains("pico") ||
            deviceName.Contains("pico") ||
            manufacturer.Contains("pico");

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

#if UNITY_ANDROID
    private static string GetAndroidBuildField(string fieldName)
    {
        try
        {
            using (var buildClass = new AndroidJavaClass("android.os.Build"))
            {
                return (buildClass.GetStatic<string>(fieldName) ?? string.Empty).ToLowerInvariant();
            }
        }
        catch
        {
            return string.Empty;
        }
    }
#endif
}
