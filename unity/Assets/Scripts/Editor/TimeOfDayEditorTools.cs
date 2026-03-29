using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
public static class TimeOfDayEditorTools
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MenuRoot = "Bila/Time Of Day/";

    [MenuItem(MenuRoot + "Current Scene/Morning (08:00)")]
    public static void SetMorningCurrentScene() => ApplyToCurrentScene(8f, false);

    [MenuItem(MenuRoot + "Current Scene/Noon (12:00)")]
    public static void SetNoonCurrentScene() => ApplyToCurrentScene(12f, false);

    [MenuItem(MenuRoot + "Current Scene/Sunset (18:00)")]
    public static void SetSunsetCurrentScene() => ApplyToCurrentScene(18f, false);

    [MenuItem(MenuRoot + "Current Scene/Night (22:00)")]
    public static void SetNightCurrentScene() => ApplyToCurrentScene(22f, false);

    [MenuItem(MenuRoot + "Sample Scene/Set Noon And Save")]
    public static void SetNoonSampleSceneAndSave() => ApplyAndSaveToScene(SampleScenePath, 12f, false);

    // Usage:
    // Unity -batchmode -quit -projectPath "<path>" -executeMethod TimeOfDayEditorTools.UpdateSampleSceneTimeFromCommandLine -- -timeOfDay 18 -autoCycle false -scene Assets/Scenes/SampleScene.unity
    public static void UpdateSampleSceneTimeFromCommandLine()
    {
        float hour = GetFloatArg("-timeOfDay", 12f);
        bool autoCycle = GetBoolArg("-autoCycle", false);
        string scenePath = GetStringArg("-scene", SampleScenePath);

        try
        {
            ApplyAndSaveToScene(scenePath, hour, autoCycle);
            Debug.Log($"[TimeOfDayEditorTools] Updated '{scenePath}' to {hour:0.##}:00 (autoCycle={autoCycle}).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TimeOfDayEditorTools] Failed to update time of day: {ex.Message}");
            throw;
        }
    }

    private static void ApplyToCurrentScene(float hour, bool autoCycle)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            throw new InvalidOperationException("No active scene is open.");
        }

        var controller = FindOrCreateControllerInOpenScene();
        ConfigureController(controller, hour, autoCycle);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ApplyAndSaveToScene(string scenePath, float hour, bool autoCycle)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            throw new InvalidOperationException($"Unable to open scene '{scenePath}'.");
        }

        var controller = FindOrCreateControllerInOpenScene();
        ConfigureController(controller, hour, autoCycle);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static TimeOfDayController FindOrCreateControllerInOpenScene()
    {
        var controller = UnityEngine.Object.FindObjectOfType<TimeOfDayController>();
        if (controller != null)
        {
            return controller;
        }

        var light = FindMainDirectionalLight();
        if (light == null)
        {
            var lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        controller = light.GetComponent<TimeOfDayController>();
        if (controller == null)
        {
            controller = light.gameObject.AddComponent<TimeOfDayController>();
        }

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("directionalLight").objectReferenceValue = light;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }

    private static Light FindMainDirectionalLight()
    {
        foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    private static void ConfigureController(TimeOfDayController controller, float hour, bool autoCycle)
    {
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("timeOfDay").floatValue = Mathf.Repeat(hour, 24f);
        serialized.FindProperty("autoCycle").boolValue = autoCycle;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        controller.SetTimeOfDay(hour);
        EditorUtility.SetDirty(controller);
        if (controller.TryGetComponent<Light>(out var light))
        {
            EditorUtility.SetDirty(light);
        }
    }

    private static string GetStringArg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private static float GetFloatArg(string name, float fallback)
    {
        string value = GetStringArg(name, null);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool GetBoolArg(string name, bool fallback)
    {
        string value = GetStringArg(name, null);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
#endif
