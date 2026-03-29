using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class CleanMissingScripts
{
    [MenuItem("Bila/Clean Missing Scripts In Scene")]
    public static void CleanMissingScriptsInScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        var rootObjects = activeScene.GetRootGameObjects();
        int totalRemoved = 0;

        foreach (var root in rootObjects)
        {
            totalRemoved += CleanupMissingScripts(root);
        }

        if (totalRemoved > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Removed {totalRemoved} missing scripts from the scene.");
        }
        else
        {
            Debug.Log("No missing scripts found in the scene.");
        }
    }

    private static int CleanupMissingScripts(GameObject go)
    {
        int count = 0;
        
        // Count missing scripts on this GameObject
        count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        
        // Recursively check children
        for (int i = 0; i < go.transform.childCount; i++)
        {
            count += CleanupMissingScripts(go.transform.GetChild(i).gameObject);
        }

        return count;
    }
}
