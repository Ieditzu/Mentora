using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ConfigureMentoraIcons
{
    private const string IconPath = "Assets/Branding/MentoraAppIcon.png";

    static ConfigureMentoraIcons()
    {
        EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Mentora/Apply Branding Icons")]
    public static void Apply()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            throw new System.InvalidOperationException($"Mentora icon was not found at {IconPath}.");
        }

        SetIcons(BuildTargetGroup.Standalone, icon);
        SetIcons(BuildTargetGroup.Android, icon);
        SetIcons(BuildTargetGroup.iOS, icon);
        AssetDatabase.SaveAssets();
    }

    private static void ApplyIfNeeded()
    {
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null)
        {
            Apply();
        }
    }

    private static void SetIcons(BuildTargetGroup target, Texture2D icon)
    {
        Texture2D[] existing = PlayerSettings.GetIconsForTargetGroup(target);
        int iconCount = Mathf.Max(existing?.Length ?? 0, 1);
        var icons = new Texture2D[iconCount];
        for (int index = 0; index < icons.Length; index++)
        {
            icons[index] = icon;
        }

        PlayerSettings.SetIconsForTargetGroup(target, icons);
    }
}
