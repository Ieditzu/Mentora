#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CodeWorldQuestIslandEditorBuilder
{
    [MenuItem("Mentora/Build Code Quest Island")]
    public static void BuildCodeQuestIsland()
    {
        CodeWorldQuestIsland[] existingIslands = Object.FindObjectsOfType<CodeWorldQuestIsland>(true);
        for (int index = 0; index < existingIslands.Length; index++)
        {
            if (existingIslands[index] != null)
            {
                Object.DestroyImmediate(existingIslands[index].gameObject);
            }
        }

        GameObject root = new GameObject("CodeWorldQuestIsland");
        CodeWorldQuestIsland island = root.AddComponent<CodeWorldQuestIsland>();
        island.Build();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Built Code Quest Island in the active scene.");
    }
}
#endif
