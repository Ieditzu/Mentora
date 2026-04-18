using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LobbySkinSampleTrigger : MonoBehaviour
{
    [SerializeField] private Renderer sampleRenderer;
    [SerializeField] private bool applyToBeanPlayer = true;
    [SerializeField] private bool applyToFirstPersonPlayer = true;

    public void ResolveSampleRenderer()
    {
        if (sampleRenderer != null)
        {
            return;
        }

        string sampleObjectName = gameObject.name.EndsWith("Trigger")
            ? gameObject.name.Substring(0, gameObject.name.Length - "Trigger".Length)
            : gameObject.name;

        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        Transform sampleTransform = parent.Find(sampleObjectName);
        if (sampleTransform == null)
        {
            return;
        }

        sampleRenderer = sampleTransform.GetComponent<Renderer>();
        if (sampleRenderer == null)
        {
            sampleRenderer = sampleTransform.GetComponentInChildren<Renderer>(true);
        }
    }

    private void Awake()
    {
        ResolveSampleRenderer();
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        ResolveSampleRenderer();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sampleRenderer == null || sampleRenderer.sharedMaterial == null)
        {
            ResolveSampleRenderer();
        }

        if (sampleRenderer == null || sampleRenderer.sharedMaterial == null)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        Material runtimeMaterial = new Material(sampleRenderer.sharedMaterial);
        if (applyToBeanPlayer)
        {
            ApplyToBean(runtimeMaterial);
        }

        if (applyToFirstPersonPlayer)
        {
            ApplyToFirstPerson(runtimeMaterial);
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.GetComponentInParent<BeanController>() != null)
        {
            return true;
        }

        return other.GetComponentInParent<FirstPersonControllerSimple>() != null;
    }

    private static void ApplyToBean(Material material)
    {
        BeanController bean = PlayerCache.GetBean();
        if (bean == null)
        {
            return;
        }

        Renderer[] renderers = bean.GetComponentsInChildren<Renderer>(true);
        ApplyMaterialToRenderers(renderers, material);
    }

    private static void ApplyToFirstPerson(Material material)
    {
        FirstPersonControllerSimple fps = PlayerCache.GetFps();
        if (fps == null)
        {
            return;
        }

        Renderer[] renderers = fps.GetComponentsInChildren<Renderer>(true);
        ApplyMaterialToRenderers(renderers, material);
    }

    private static void ApplyMaterialToRenderers(Renderer[] renderers, Material material)
    {
        if (renderers == null || material == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !ShouldSkinRenderer(renderer))
            {
                continue;
            }

            renderer.sharedMaterial = new Material(material);
        }
    }

    private static bool ShouldSkinRenderer(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
        {
            return false;
        }

        string objectName = renderer.gameObject.name;
        if (objectName.Contains("VrController") || objectName.Contains("Hand"))
        {
            return false;
        }

        Transform current = renderer.transform;
        while (current != null)
        {
            if (current.name == "VrTrackedHandsRoot")
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }
}
