using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class CoinRotator : MonoBehaviour
{
    public enum CoinMode
    {
        JumpAndBox = 0,
        IslandReveal = 1,
        BridgeReveal = 2,
    }

    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private GameObject robotPrefab;
    [SerializeField] private Vector3 robotSpawnPosition = new Vector3(46f, 5f, 312f);
    [SerializeField] private Vector3 robotScale = Vector3.one * 0.2f;
    [SerializeField] private CoinMode mode = CoinMode.JumpAndBox;
    [SerializeField] private bool spawnNextCoinOnCollect = true;
    [SerializeField] private string nextCoinTargetObjectName = "boxcoins";
    [SerializeField] private string nextCoinName = "Coin2";
    [SerializeField] private float nextCoinVerticalOffset = 1.2f;
    [SerializeField] private float nextCoinScaleMultiplier = 1f;
    [SerializeField] private float nextCoinTriggerSizeMultiplier = 0.65f;

    private CoinRotator spawnedNextCoin;

    private void Awake()
    {
#if UNITY_EDITOR
        if (robotPrefab == null)
        {
            robotPrefab = LoadRobotFromAssets();
        }
#endif
        EnsureTrigger();
    }

    private void Reset()
    {
        EnsureTrigger();
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BeanController>() != null || other.GetComponent<CharacterController>() != null)
        {
            var ui = PickupUIController.Instance ?? FindObjectOfType<PickupUIController>();
            if (ui != null)
            {
                ui.Show(this, mode);
                if (string.Equals(gameObject.name, "CoinCatva", System.StringComparison.OrdinalIgnoreCase))
                {
                    PauseMenuManager.CompleteTaskByTitle("logic");
                    ui.HideOverlayOnly();
                }
            }

            SpawnNextCoinIfNeeded();
            SpawnRobot();
            gameObject.SetActive(false);
        }
    }

    private void SpawnRobot()
    {
        GameObject prefabToUse = robotPrefab ?? LoadRobotFromAssets();
        if (prefabToUse == null)
        {
            Debug.LogWarning("Robot prefab not found; assign it on CoinRotator or place one at Assets/robot-copernicus/source/low.obj");
            return;
        }

        var spawned = Instantiate(prefabToUse, robotSpawnPosition, Quaternion.identity);
        spawned.transform.localScale = robotScale;

        // Face the player horizontally and keep doing so.
        Transform player = ResolvePlayerTransform();
        if (player != null)
        {
            Vector3 dir = player.position - spawned.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                spawned.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
        if (spawned.GetComponent<RobotLookAt>() == null)
        {
            spawned.AddComponent<RobotLookAt>();
        }

    }

    public void ResetPickup()
    {
        gameObject.SetActive(true);
    }

    public void ConfigureRuntime(CoinMode newMode, bool canSpawnNextCoin)
    {
        mode = newMode;
        spawnNextCoinOnCollect = canSpawnNextCoin;
        spawnedNextCoin = null;
    }

    private void SpawnNextCoinIfNeeded()
    {
        if (!spawnNextCoinOnCollect || spawnedNextCoin != null || mode != CoinMode.JumpAndBox)
        {
            return;
        }

        Transform target = FindSpawnTarget();
        if (target == null)
        {
            Debug.LogWarning("Coin 2 spawn target not found.");
            return;
        }

        Vector3 spawnPosition = target.position + Vector3.up * nextCoinVerticalOffset;
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            spawnPosition = new Vector3(
                targetCollider.bounds.center.x,
                targetCollider.bounds.max.y + nextCoinVerticalOffset,
                targetCollider.bounds.center.z);
        }

        GameObject clone = Instantiate(gameObject, spawnPosition, Quaternion.identity);
        clone.name = string.IsNullOrWhiteSpace(nextCoinName)
            ? $"{gameObject.name}_IslandReveal"
            : nextCoinName;
        clone.SetActive(true);

        spawnedNextCoin = clone.GetComponent<CoinRotator>();
        if (spawnedNextCoin == null)
        {
            return;
        }

        spawnedNextCoin.mode = CoinMode.IslandReveal;
        spawnedNextCoin.spawnNextCoinOnCollect = false;
        spawnedNextCoin.spawnedNextCoin = null;
        spawnedNextCoin.transform.localScale = transform.localScale * nextCoinScaleMultiplier;
        spawnedNextCoin.ShrinkTrigger(nextCoinTriggerSizeMultiplier);

        Debug.Log($"Coin 2 spawned on {target.name} at {spawnPosition}");
    }

    private Transform FindSpawnTarget()
    {
        if (!string.IsNullOrWhiteSpace(nextCoinTargetObjectName))
        {
            foreach (Transform candidate in FindObjectsOfType<Transform>(true))
            {
                if (candidate == null)
                {
                    continue;
                }

                string candidateName = candidate.name.ToLowerInvariant();
                string targetName = nextCoinTargetObjectName.ToLowerInvariant();
                if (candidateName == targetName || candidateName.Contains(targetName))
                {
                    return candidate;
                }
            }
        }

        foreach (Transform candidate in FindObjectsOfType<Transform>())
        {
            if (candidate == null)
            {
                continue;
            }

            string lower = candidate.name.ToLowerInvariant();
            if (lower.Contains("boxcoin") ||
                lower.Contains("coinbox") ||
                lower.Contains("cube (3)") ||
                lower.Contains("box(3)") ||
                lower.Contains("box (3)") ||
                lower.Contains("box3"))
            {
                return candidate;
            }
        }

        return null;
    }

    private GameObject LoadRobotFromAssets()
    {
#if UNITY_EDITOR
        var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/robot-copernicus/source/low.obj");
        if (loaded != null)
        {
            return loaded;
        }
#endif
        return null;
    }

    private Transform ResolvePlayerTransform()
    {
        return PlayerCache.ResolvePlayerTransform();
    }

    private void EnsureTrigger()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
            col.enabled = true;
        }
    }

    private void ShrinkTrigger(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        if (TryGetComponent(out BoxCollider box))
        {
            box.size *= multiplier;
            box.center *= multiplier;
            return;
        }

        if (TryGetComponent(out SphereCollider sphere))
        {
            sphere.radius *= multiplier;
            sphere.center *= multiplier;
            return;
        }

        if (TryGetComponent(out CapsuleCollider capsule))
        {
            capsule.radius *= multiplier;
            capsule.height *= multiplier;
            capsule.center *= multiplier;
        }
    }
}
