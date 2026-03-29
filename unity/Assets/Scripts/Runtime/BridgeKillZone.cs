using UnityEngine;

/// <summary>
/// Disables the walkable collider for the bridge island until viewPod is revealed,
/// and enables a kill trigger while hidden for instant respawn.
/// </summary>
public class BridgeKillZone : MonoBehaviour
{
    private Collider solidCollider;
    private Collider triggerCollider;

    private void Awake()
    {
        CacheColliders();
        BuildTriggerIfMissing();
        SyncState();
    }

    private void OnValidate()
    {
        CacheColliders();
        BuildTriggerIfMissing();
        SyncState();
    }

    private void Update()
    {
        SyncState();
    }

    private void CacheColliders()
    {
        solidCollider = GetComponent<Collider>();
        triggerCollider = null;
        // If a BoxCollider child named BridgeKillTrigger exists, reuse it.
        Transform existing = transform.Find("BridgeKillTrigger");
        if (existing != null) triggerCollider = existing.GetComponent<Collider>();
    }

    private void BuildTriggerIfMissing()
    {
        if (triggerCollider != null) return;

        GameObject triggerObj = new GameObject("BridgeKillTrigger");
        triggerObj.transform.SetParent(transform, false);

        BoxCollider box = triggerObj.AddComponent<BoxCollider>();
        box.isTrigger = true;

        // Size the trigger to cover the visible bounds slightly inflated.
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
        }
        else if (solidCollider != null)
        {
            bounds = solidCollider.bounds;
        }

        box.center = triggerObj.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size * 1.05f;

        triggerCollider = box;
    }

    private void SyncState()
    {
        bool revealed = PickupUIController.IsBridgeRevealed;
        if (solidCollider != null) solidCollider.enabled = revealed;
        if (triggerCollider != null) triggerCollider.enabled = !revealed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PickupUIController.IsBridgeRevealed || triggerCollider == null || !triggerCollider.enabled)
        {
            return;
        }

        if (other.TryGetComponent(out BeanController bean))
        {
            bean.RespawnNow();
            return;
        }

        if (other.TryGetComponent(out FirstPersonControllerSimple fps))
        {
            fps.RespawnAtSpawnPoint();
            return;
        }

        var cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            var fpsController = cc.GetComponent<FirstPersonControllerSimple>() ?? cc.GetComponentInParent<FirstPersonControllerSimple>();
            if (fpsController != null)
            {
                fpsController.RespawnAtSpawnPoint();
            }
        }
    }
}

public static class BridgeKillZoneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        TryAttachTo("inslua3"); // only the designated bridge island
    }

    private static void TryAttachTo(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<BridgeKillZone>() == null)
        {
            target.AddComponent<BridgeKillZone>();
        }
    }
}
