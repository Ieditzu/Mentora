using System.IO;
using UnityEngine;

/// <summary>
/// Writes Unity logs to a file on Android so we can read crashes on devices that show no logcat output.
/// File path: Application.persistentDataPath + "/unity_device_log.txt"
/// </summary>
public static class AndroidCrashLogger
{
    private static StreamWriter _writer;
    private static string _logPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if UNITY_ANDROID
        _logPath = Path.Combine(Application.persistentDataPath, "unity_device_log.txt");
        try
        {
            _writer = new StreamWriter(_logPath, append: false);
            _writer.AutoFlush = true;
            Application.logMessageReceived += HandleLog;
            WriteHeader();
        }
        catch
        {
            // If we can't write, just skip to avoid further errors.
            _writer = null;
        }
#endif
    }

    private static void WriteHeader()
    {
        if (_writer == null) return;
        _writer.WriteLine("===== Unity Device Log =====");
        _writer.WriteLine($"Time: {System.DateTime.UtcNow:o}");
        _writer.WriteLine($"App: {Application.identifier} v{Application.version} ({Application.buildGUID})");
        _writer.WriteLine($"Device: {SystemInfo.deviceModel} / {SystemInfo.operatingSystem}");
        _writer.WriteLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} cores) ARM64:{DetectArm64()}");
        _writer.WriteLine($"GPU: {SystemInfo.graphicsDeviceName} API:{SystemInfo.graphicsDeviceVersion}");
        _writer.WriteLine($"XR enabled: {UnityEngine.XR.XRSettings.enabled}");
        _writer.WriteLine("============================");
        _writer.Flush();
    }

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (_writer == null) return;
        _writer.WriteLine($"[{type}] {condition}");
        if (!string.IsNullOrEmpty(stackTrace))
        {
            _writer.WriteLine(stackTrace);
        }
    }

    private static bool DetectArm64()
    {
        // Unity 2022: best-effort—assume ARM64 on device builds; we don't rely on this for logic.
        return Application.platform == RuntimePlatform.Android && !Application.isEditor;
    }
}
