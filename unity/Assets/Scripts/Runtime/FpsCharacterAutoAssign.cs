#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-time helper that auto-assigns a Floreswa human prefab to the FPS controller if none is set.
/// Keeps runtime simple (no Resources move needed) while ensuring a visible body in Play Mode.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FpsCharacterAutoAssign : MonoBehaviour
{
    [Tooltip("Path to a Floreswa prefab to use for the FPS body.")]
    [SerializeField] private string prefabPath = "Assets/Floreswa/Prefabs/male01_1.prefab";

    private void OnEnable() => Assign();
    private void OnValidate() => Assign();

    private void Assign()
    {
        var fps = GetComponent<FirstPersonControllerSimple>();
        if (fps == null)
        {
            return;
        }

        if (fpsCharacterAlreadySet(fps))
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            fps.SetCharacterPrefab(prefab);
            EditorUtility.SetDirty(fps);
        }
    }

    private bool fpsCharacterAlreadySet(FirstPersonControllerSimple fps)
    {
        var so = new SerializedObject(fps);
        var prop = so.FindProperty("characterPrefab");
        return prop != null && prop.objectReferenceValue != null;
    }
}
#endif
