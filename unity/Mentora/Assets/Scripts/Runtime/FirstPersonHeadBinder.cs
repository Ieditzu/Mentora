using UnityEngine;

/// <summary>
/// Optional helper: attach this to your FPS player and assign a character model.
/// It finds a head bone and wires it to the FirstPersonControllerSimple headAnchor so the camera sits at the model's head.
/// </summary>
[RequireComponent(typeof(FirstPersonControllerSimple))]
public class FirstPersonHeadBinder : MonoBehaviour
{
    [Tooltip("Root of the skinned mesh/rig for the character.")]
    public Transform characterRoot;

    [Tooltip("Override head transform. If empty, binder will try to auto-find a head bone by name.")]
    public Transform headOverride;

    [Tooltip("Re-run binding in play mode after a character prefab was spawned.")]
    public bool allowRuntimeRebind = true;

    private void Awake()
    {
        FirstPersonControllerSimple fps = GetComponent<FirstPersonControllerSimple>();
        if (fps == null)
        {
            enabled = false;
            return;
        }

        TryBind(fps);
    }

    public void TryBind(FirstPersonControllerSimple fpsOverride = null)
    {
        FirstPersonControllerSimple fps = fpsOverride != null ? fpsOverride : GetComponent<FirstPersonControllerSimple>();
        if (fps == null || (!allowRuntimeRebind && Application.isPlaying && headOverride == null))
        {
            return;
        }

        Transform searchRoot = characterRoot != null ? characterRoot : transform;
        Transform head = headOverride != null ? headOverride : FindHead(searchRoot);
        if (head != null)
        {
            fps.SetHeadAnchor(head);
        }
    }

    private Transform FindHead(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform best = null;
        float bestScore = -1f;
        Traverse(root, ref best, ref bestScore);
        return best;
    }

    private void Traverse(Transform t, ref Transform best, ref float bestScore)
    {
        float score = ScoreName(t.name);
        if (score > bestScore)
        {
            bestScore = score;
            best = t;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            Traverse(t.GetChild(i), ref best, ref bestScore);
        }
    }

    private float ScoreName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1f;
        }

        string lower = name.ToLowerInvariant();
        if (lower.Contains("head"))
        {
            return 10f;
        }
        if (lower.Contains("neck"))
        {
            return 6f;
        }
        if (lower.Contains("helmet") || lower.Contains("hat"))
        {
            return 4f;
        }
        return 0f;
    }
}
