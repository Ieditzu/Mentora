using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SakuraAreaColliderApplier : MonoBehaviour
{
    [SerializeField] private bool autoApplyDisabled = true;

    private static readonly string[] WalkableSurfaceNames =
    {
        "plasnapython",
        "Plane.001",
        "shpere",
        "insulac++",
        "insulapython",
        "insulapthon",
        "insula3",
    };

    private bool applyQueued;

    private void OnEnable()
    {
        if (autoApplyDisabled)
        {
            return;
        }

        QueueApplyColliders();
    }

    private void OnValidate()
    {
        if (autoApplyDisabled)
        {
            return;
        }

        QueueApplyColliders();
    }

    [ContextMenu("Apply Sakura Area Colliders")]
    public void ApplyColliders()
    {
        if (autoApplyDisabled)
        {
            return;
        }

        applyQueued = false;
        ApplyToRootAndChildren(GameObject.Find("sakura2"), false);
        ApplyWalkableTopSurface(GameObject.Find("low_poly_treesNXT_5flat"));
        ApplySketchfabWalkableSurfaces();
        ApplyWalkableBridgeColliders();
        ApplyNamedWalkableSurfaces();
    }

    private void QueueApplyColliders()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (applyQueued)
            {
                return;
            }

            applyQueued = true;
            EditorApplication.delayCall += ApplyCollidersIfAlive;
            return;
        }
#endif

        ApplyColliders();
    }

#if UNITY_EDITOR
    private void ApplyCollidersIfAlive()
    {
        if (this == null)
        {
            return;
        }

        ApplyColliders();
    }
#endif

    private static void ApplyNamedWalkableSurfaces()
    {
        for (int i = 0; i < WalkableSurfaceNames.Length; i++)
        {
            ApplyWalkableTopSurface(WalkableSurfaceNames[i]);
        }
    }

    private static void ApplyWalkableTopSurface(string objectName)
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < allObjects.Length; i++)
        {
            if (string.Equals(allObjects[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyWalkableTopSurface(allObjects[i]);
            }
        }
    }

    private static void ApplyToRootAndChildren(GameObject root, bool preferWalkable)
    {
        if (root == null)
        {
            return;
        }

        AddBestCollider(root, preferWalkable);

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == null)
            {
                continue;
            }

            AddBestCollider(
                meshFilters[i].gameObject,
                preferWalkable || IsNamedWalkableSurface(meshFilters[i].name));
        }
    }

    private static bool IsNamedWalkableSurface(string objectName)
    {
        for (int i = 0; i < WalkableSurfaceNames.Length; i++)
        {
            if (string.Equals(WalkableSurfaceNames[i], objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyWalkableBridgeColliders()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < allObjects.Length; i++)
        {
            if (allObjects[i].name != "BranchBridge")
            {
                continue;
            }

            BuildSimpleWalkSurface(allObjects[i]);
        }
    }

    private static void ApplySketchfabWalkableSurfaces()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < allObjects.Length; i++)
        {
            string name = allObjects[i].name;
            if (!name.StartsWith("Sketchfab_"))
            {
                continue;
            }

            BuildSimpleWalkSurface(allObjects[i]);
        }
    }

    private static void ApplyWalkableTopSurface(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        if (string.Equals(root.name, "shpere", System.StringComparison.OrdinalIgnoreCase))
        {
            BuildFlatPlatformSurface(root);
            return;
        }

        BuildSimpleWalkSurface(root);
    }

    private static void BuildFlatPlatformSurface(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform existing = root.transform.Find("VisiblePlatformReplacement");
        if (existing != null)
        {
            DestroySafe(existing.gameObject);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                DestroySafe(colliders[i]);
            }
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        Bounds? localBounds = CalculateCombinedLocalBounds(root.transform);
        if (!localBounds.HasValue)
        {
            return;
        }

        Bounds bounds = localBounds.Value;
        float platformHeight = 0.18f;
        float visibleTopScale = 0.56f;
        float visualHeight = Mathf.Max(0.22f, bounds.size.y * 0.12f);
        float visibleWidth = Mathf.Max(0.35f, bounds.size.x * visibleTopScale);
        float visibleDepth = Mathf.Max(0.35f, bounds.size.z * visibleTopScale);

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platform.name = "VisiblePlatformReplacement";
        platform.transform.SetParent(root.transform, false);
        platform.transform.localPosition = new Vector3(
            bounds.center.x,
            bounds.max.y - (visualHeight * 0.15f),
            bounds.center.z);
        platform.transform.localRotation = Quaternion.identity;
        platform.transform.localScale = new Vector3(
            visibleWidth,
            visualHeight * 0.5f,
            visibleDepth);

        Collider primitiveCollider = platform.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
        }

        Renderer platformRenderer = platform.GetComponent<Renderer>();
        if (platformRenderer != null)
        {
            platformRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            platformRenderer.receiveShadows = true;
            if (platformRenderer.sharedMaterial == null || platformRenderer.sharedMaterial.name.StartsWith("Default"))
            {
                platformRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            platformRenderer.sharedMaterial.color = new Color(0.48f, 0.72f, 0.46f, 1f);
        }

        GameObject walkSurface = new GameObject("WalkSurfaceCollider");
        walkSurface.transform.SetParent(platform.transform, false);
        walkSurface.transform.localPosition = new Vector3(0f, visualHeight * 0.55f, 0f);
        walkSurface.transform.localRotation = Quaternion.identity;

        BoxCollider box = walkSurface.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = new Vector3(
            visibleWidth,
            platformHeight,
            visibleDepth);
        box.center = Vector3.zero;
    }

    private static void BuildSimpleWalkSurface(GameObject bridgeRoot)
    {
        if (bridgeRoot == null)
        {
            return;
        }

        Transform existing = bridgeRoot.transform.Find("WalkSurfaceCollider");
        if (existing != null)
        {
            DestroySafe(existing.gameObject);
        }

        Collider[] childColliders = bridgeRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            if (childColliders[i] == null)
            {
                continue;
            }

            if (childColliders[i].gameObject == bridgeRoot)
            {
                DestroySafe(childColliders[i]);
                continue;
            }

            DestroySafe(childColliders[i]);
        }

        Bounds? localBounds = CalculateCombinedLocalBounds(bridgeRoot.transform);
        if (!localBounds.HasValue)
        {
            return;
        }

        Bounds bounds = localBounds.Value;
        float skin = 0.08f;
        float surfaceHeight = Mathf.Clamp(bounds.size.y * 0.18f, 0.08f, 0.18f);

        GameObject walkSurface = new GameObject("WalkSurfaceCollider");
        walkSurface.transform.SetParent(bridgeRoot.transform, false);
        walkSurface.transform.localPosition = new Vector3(
            bounds.center.x,
            bounds.max.y - (surfaceHeight * 0.5f),
            bounds.center.z);
        walkSurface.transform.rotation = Quaternion.identity;

        BoxCollider box = walkSurface.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = new Vector3(
            Mathf.Max(0.2f, bounds.size.x - skin),
            surfaceHeight,
            Mathf.Max(0.2f, bounds.size.z - skin));
        box.center = Vector3.zero;
    }

    private static Bounds? CalculateCombinedLocalBounds(Transform root)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3[] corners = GetBoundsCorners(meshBounds);
            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 worldPoint = meshFilter.transform.TransformPoint(corners[cornerIndex]);
                Vector3 localPoint = root.InverseTransformPoint(worldPoint);

                if (!hasBounds)
                {
                    combined = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(localPoint);
                }
            }
        }

        return hasBounds ? combined : null;
    }

    private static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private static void AddBestCollider(GameObject target, bool preferWalkable)
    {
        if (target == null)
        {
            return;
        }

        if (target.GetComponent<Collider>() != null)
        {
            return;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        Renderer renderer = target.GetComponent<Renderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || renderer == null)
        {
            return;
        }

        Bounds bounds = renderer.bounds;
        Vector3 size = bounds.size;

        if (preferWalkable && size.x > 0.5f && size.z > 0.5f)
        {
            BoxCollider box = target.AddComponent<BoxCollider>();
            box.isTrigger = false;
            return;
        }

        MeshCollider meshCollider = target.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(obj);
        }
        else
        {
            Object.DestroyImmediate(obj);
        }
    }
}
