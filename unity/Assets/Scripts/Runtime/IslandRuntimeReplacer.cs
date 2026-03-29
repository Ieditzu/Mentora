using System.Collections;
using UnityEngine;

public class IslandRuntimeReplacer : MonoBehaviour
{
    private static readonly string[] ReplacementTargets =
    {
        "insulac++",
        "insulapython",
        "insulapthon",
    };

    private static readonly string[] ReplacementNames =
    {
        "CppIslandReplacement",
        "PythonIslandReplacement",
        "PythonIslandReplacement",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject bootstrapObject = new GameObject("IslandRuntimeReplacer");
        Object.DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.hideFlags = HideFlags.HideAndDontSave;
        bootstrapObject.AddComponent<IslandRuntimeReplacer>();
    }

    private IEnumerator Start()
    {
        for (int i = 0; i < 24; i++)
        {
            ReplaceTargetIslands();
            yield return null;
        }
    }

    private static void ReplaceTargetIslands()
    {
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            int targetIndex = GetReplacementTargetIndex(candidate.name);
            if (targetIndex < 0)
            {
                continue;
            }

            BuildReplacementIsland(candidate, targetIndex);
        }
    }

    private static int GetReplacementTargetIndex(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return -1;
        }

        for (int i = 0; i < ReplacementTargets.Length; i++)
        {
            if (string.Equals(objectName, ReplacementTargets[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void BuildReplacementIsland(GameObject original, int targetIndex)
    {
        if (original == null)
        {
            return;
        }

        Transform existing = original.transform.parent != null
            ? original.transform.parent.Find(ReplacementNames[targetIndex])
            : null;

        Bounds? worldBounds = CalculateCombinedWorldBounds(original.transform);
        if (!worldBounds.HasValue)
        {
            return;
        }

        Bounds bounds = worldBounds.Value;
        DisableOriginalIsland(original);

        GameObject replacement = existing != null ? existing.gameObject : new GameObject(ReplacementNames[targetIndex]);
        replacement.layer = original.layer;

        if (original.transform.parent != null)
        {
            replacement.transform.SetParent(original.transform.parent, true);
        }

        replacement.transform.position = bounds.center;
        replacement.transform.rotation = Quaternion.identity;

        EnsureIslandVisuals(replacement, bounds, original.name);
    }

    private static void DisableOriginalIsland(GameObject original)
    {
        if (original.activeSelf)
        {
            original.SetActive(false);
        }
    }

    private static void EnsureIslandVisuals(GameObject replacement, Bounds bounds, string sourceName)
    {
        float width = Mathf.Max(2.4f, bounds.size.x);
        float depth = Mathf.Max(2.4f, bounds.size.z);
        float height = Mathf.Clamp(bounds.size.y, 0.8f, 2.2f);
        float topHeight = Mathf.Clamp(height * 0.45f, 0.35f, 0.8f);
        float baseHeight = Mathf.Clamp(height * 0.7f, 0.5f, 1.2f);

        replacement.transform.position = new Vector3(bounds.center.x, bounds.min.y + (height * 0.5f), bounds.center.z);

        GameObject basePart = GetOrCreatePrimitiveChild(replacement.transform, "Base", PrimitiveType.Cylinder);
        basePart.transform.localPosition = Vector3.zero;
        basePart.transform.localScale = new Vector3(width * 0.58f, baseHeight * 0.5f, depth * 0.58f);
        ConfigurePrimitive(basePart, false, new Color(0.36f, 0.26f, 0.18f, 1f));

        GameObject topPart = GetOrCreatePrimitiveChild(replacement.transform, "Top", PrimitiveType.Cylinder);
        topPart.transform.localPosition = new Vector3(0f, (baseHeight * 0.42f), 0f);
        topPart.transform.localScale = new Vector3(width * 0.50f, topHeight * 0.5f, depth * 0.50f);
        ConfigurePrimitive(topPart, true, GetTopColor(sourceName));
        EnsureTopWalkSurface(topPart.transform, width * 0.50f, depth * 0.50f, topHeight);

        GameObject rockA = GetOrCreatePrimitiveChild(replacement.transform, "RockA", PrimitiveType.Sphere);
        rockA.transform.localPosition = new Vector3(width * -0.16f, baseHeight * 0.62f, depth * 0.08f);
        rockA.transform.localScale = new Vector3(width * 0.18f, height * 0.22f, depth * 0.16f);
        ConfigurePrimitive(rockA, false, new Color(0.52f, 0.49f, 0.45f, 1f));

        GameObject rockB = GetOrCreatePrimitiveChild(replacement.transform, "RockB", PrimitiveType.Sphere);
        rockB.transform.localPosition = new Vector3(width * 0.14f, baseHeight * 0.58f, depth * -0.1f);
        rockB.transform.localScale = new Vector3(width * 0.14f, height * 0.18f, depth * 0.14f);
        ConfigurePrimitive(rockB, false, new Color(0.48f, 0.45f, 0.41f, 1f));
    }

    private static GameObject GetOrCreatePrimitiveChild(Transform parent, string childName, PrimitiveType primitiveType)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = childName;
        primitive.transform.SetParent(parent, false);
        return primitive;
    }

    private static void ConfigurePrimitive(GameObject primitive, bool keepCollider, Color color)
    {
        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = keepCollider;
        }

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.name.StartsWith("Default"))
            {
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            renderer.sharedMaterial.color = color;
        }
    }

    private static void EnsureTopWalkSurface(Transform topTransform, float width, float depth, float topHeight)
    {
        Transform existing = topTransform.Find("WalkSurface");
        if (existing == null)
        {
            GameObject walkSurface = new GameObject("WalkSurface");
            walkSurface.transform.SetParent(topTransform, false);
            existing = walkSurface.transform;
        }

        existing.localPosition = new Vector3(0f, Mathf.Max(0.08f, topHeight), 0f);
        existing.localRotation = Quaternion.identity;

        BoxCollider box = existing.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = existing.gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = false;
        box.size = new Vector3(
            Mathf.Max(1f, width * 1.65f),
            0.22f,
            Mathf.Max(1f, depth * 1.65f));
        box.center = Vector3.zero;
    }

    private static Color GetTopColor(string sourceName)
    {
        if (sourceName.ToLowerInvariant().Contains("python"))
        {
            return new Color(0.82f, 0.92f, 1f, 1f);
        }

        return new Color(0.88f, 1f, 0.9f, 1f);
    }

    private static Bounds? CalculateCombinedWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? combined : null;
    }
}
