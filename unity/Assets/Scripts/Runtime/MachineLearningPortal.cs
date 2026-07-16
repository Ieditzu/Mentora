using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class MachineLearningPortal : MonoBehaviour
{
    [SerializeField] private MachineLearningIsland.Difficulty difficulty = MachineLearningIsland.Difficulty.Easy;
    [SerializeField] private float retriggerDelaySeconds = 1.5f;

    private float nextTriggerTime;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.unscaledTime < nextTriggerTime || !IsLocalPlayer(other))
        {
            return;
        }

        MachineLearningIsland island = GetComponentInParent<MachineLearningIsland>();
        if (island == null)
        {
            island = FindObjectOfType<MachineLearningIsland>();
        }

        if (island != null)
        {
            nextTriggerTime = Time.unscaledTime + Mathf.Max(0.25f, retriggerDelaySeconds);
            island.OpenDifficulty(ResolveDifficulty());
        }
    }

    private MachineLearningIsland.Difficulty ResolveDifficulty()
    {
        string lowerName = gameObject.name.ToLowerInvariant();
        if (lowerName.Contains("hard"))
        {
            return MachineLearningIsland.Difficulty.Hard;
        }

        if (lowerName.Contains("medium"))
        {
            return MachineLearningIsland.Difficulty.Medium;
        }

        return difficulty;
    }

    private static bool IsLocalPlayer(Collider other)
    {
        return other != null &&
               (other.GetComponent<FirstPersonControllerSimple>() != null ||
                other.GetComponentInParent<FirstPersonControllerSimple>() != null ||
                other.GetComponent<BeanController>() != null ||
                other.GetComponentInParent<BeanController>() != null);
    }
}
