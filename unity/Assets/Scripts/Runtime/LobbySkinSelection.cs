using UnityEngine;

[ExecuteAlways]
public class LobbySkinSelection : MonoBehaviour
{
    private void Awake()
    {
        RefreshTriggers();
    }

    private void OnEnable()
    {
        RefreshTriggers();
    }

    private void OnValidate()
    {
        RefreshTriggers();
    }

    [ContextMenu("Refresh Skin Triggers")]
    public void RefreshTriggers()
    {
        LobbySkinSampleTrigger[] triggers = GetComponentsInChildren<LobbySkinSampleTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null)
            {
                triggers[i].ResolveSampleRenderer();
            }
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform || !child.name.EndsWith("Trigger"))
            {
                continue;
            }

            LobbySkinSampleTrigger trigger = child.GetComponent<LobbySkinSampleTrigger>();
            if (trigger == null)
            {
                trigger = child.gameObject.AddComponent<LobbySkinSampleTrigger>();
            }

            trigger.ResolveSampleRenderer();
        }
    }
}
