using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Low-cost portal visual for the three machine-learning stations. Geometry is generated once;
/// animation only changes transforms, so LineRenderer meshes are never rebuilt every frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class MachineLearningPortalEffect : MonoBehaviour
{
    private const string GeneratedRootName = "__MachineLearningPortalEffect";
    private const int SurfaceSegments = 28;

    [Header("Portal appearance")]
    [SerializeField] private Color difficultyColor = new Color(0.16f, 0.95f, 0.56f, 1f);
    [SerializeField] private Vector3 localCenter = new Vector3(0f, 3.05f, 0.06f);
    [SerializeField] private Vector2 portalSize = new Vector2(4.8f, 5.05f);
    [SerializeField, Range(1, 2)] private int energyRingCount = 1;
    [SerializeField, Range(16, 32)] private int ringSegments = 24;
    [SerializeField, Range(0.35f, 0.85f)] private float voidOpacity = 0.68f;
    [SerializeField, Range(0.03f, 0.22f)] private float surfaceOpacity = 0.12f;

    [Header("Cheap transform animation")]
    [SerializeField, Range(8f, 15f)] private float animationFramesPerSecond = 12f;
    [SerializeField, Range(0f, 0.04f)] private float pulseAmount = 0.018f;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 0.8f;
    [SerializeField, Range(0f, 0.08f)] private float ringWobble = 0.025f;
    [SerializeField, Range(0f, 0.12f)] private float swirlDepth = 0.035f;

    [Header("Performance culling")]
    [SerializeField, Min(10f)] private float activationDistance = 55f;
    [SerializeField, Range(0.15f, 0.75f)] private float cullingCheckInterval = 0.3f;

    [Header("Legacy focal point")]
    [SerializeField] private bool hideLegacyFocalPoint = true;
    [SerializeField] private string[] legacyDecorationNameFragments =
    {
        "ball",
        "orb",
        "sphere",
        "plate",
        "pedestal",
        "spinning"
    };

    private readonly List<RingVisual> rings = new List<RingVisual>(2);
    private readonly List<Material> runtimeMaterials = new List<Material>(2);
    private readonly List<Renderer> hiddenLegacyRenderers = new List<Renderer>();

    private Transform generatedRoot;
    private Transform surfaceTransform;
    private Material surfaceMaterial;
    private Material ringMaterial;
    private Mesh surfaceMesh;
    private Camera cachedCamera;
    private float animationPhase;
    private float nextAnimationTime;
    private float nextCullingCheckTime;
    private bool visualsBuilt;
    private bool animateForCamera = true;

    private sealed class RingVisual
    {
        public Transform transform;
        public float rotationSpeed;
        public float phase;
    }

    private void Reset()
    {
        string lowerName = gameObject.name.ToLowerInvariant();
        if (lowerName.Contains("hard"))
        {
            difficultyColor = new Color(0.96f, 0.2f, 0.72f, 1f);
        }
        else if (lowerName.Contains("medium"))
        {
            difficultyColor = new Color(1f, 0.66f, 0.14f, 1f);
        }
        else
        {
            difficultyColor = new Color(0.16f, 0.95f, 0.56f, 1f);
        }
    }

    private void Awake()
    {
        animationPhase = Mathf.Abs(gameObject.GetInstanceID() % 997) * 0.013f;
        EnsureVisualsBuilt();
    }

    private void OnEnable()
    {
        // Do not create runtime renderers while merely editing the scene.
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureVisualsBuilt();
        HideLegacyRenderers();
        nextAnimationTime = 0f;
        nextCullingCheckTime = Time.unscaledTime + cullingCheckInterval;
        animateForCamera = ShouldAnimateForCamera();
        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(false);
        }

        RestoreLegacyRenderers();
    }

    private void OnDestroy()
    {
        RestoreLegacyRenderers();

        if (generatedRoot != null)
        {
            DestroyRuntimeObject(generatedRoot.gameObject);
            generatedRoot = null;
        }

        if (surfaceMesh != null)
        {
            DestroyRuntimeObject(surfaceMesh);
            surfaceMesh = null;
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                DestroyRuntimeObject(runtimeMaterials[i]);
            }
        }

        runtimeMaterials.Clear();
        rings.Clear();
        surfaceTransform = null;
        surfaceMaterial = null;
        ringMaterial = null;
        visualsBuilt = false;
    }

    private void OnValidate()
    {
        portalSize.x = Mathf.Max(0.5f, portalSize.x);
        portalSize.y = Mathf.Max(0.5f, portalSize.y);
        energyRingCount = 1;
        ringSegments = Mathf.Clamp(ringSegments, 16, 32);
        voidOpacity = Mathf.Clamp(voidOpacity, 0.35f, 0.85f);
        surfaceOpacity = Mathf.Clamp(surfaceOpacity, 0.03f, 0.22f);
        animationFramesPerSecond = Mathf.Clamp(animationFramesPerSecond, 8f, 15f);
        pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.04f);
        pulseSpeed = Mathf.Max(0.1f, pulseSpeed);
        ringWobble = Mathf.Clamp(ringWobble, 0f, 0.08f);
        swirlDepth = Mathf.Clamp(swirlDepth, 0f, 0.12f);
        activationDistance = Mathf.Max(10f, activationDistance);
        cullingCheckInterval = Mathf.Clamp(cullingCheckInterval, 0.15f, 0.75f);
    }

    private void Update()
    {
        if (!visualsBuilt)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now >= nextCullingCheckTime)
        {
            nextCullingCheckTime = now + cullingCheckInterval;
            animateForCamera = ShouldAnimateForCamera();
        }

        if (!animateForCamera || now < nextAnimationTime)
        {
            return;
        }

        float cappedFrameRate = Mathf.Clamp(animationFramesPerSecond, 8f, 15f);
        nextAnimationTime = now + 1f / cappedFrameRate;
        AnimateTransforms(now + animationPhase);
    }

    public void ConfigureColor(Color color)
    {
        difficultyColor = color;
        difficultyColor.a = 1f;

        if (surfaceMaterial != null)
        {
            SetMaterialColor(surfaceMaterial, CreateSurfaceColor(), 0.45f);
        }
        if (ringMaterial != null)
        {
            SetMaterialColor(ringMaterial, WithAlpha(difficultyColor, 0.92f), 1.45f);
        }
    }

    private void EnsureVisualsBuilt()
    {
        if (visualsBuilt)
        {
            return;
        }

        Shader transparentShader = FindTransparentShader();
        if (transparentShader == null)
        {
            Debug.LogWarning("[MachineLearningPortalEffect] No compatible transparent shader was found.", this);
            return;
        }

        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot != null)
        {
            DestroyRuntimeObject(existingRoot.gameObject);
        }

        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.layer = gameObject.layer;
        rootObject.hideFlags = HideFlags.DontSave;
        generatedRoot = rootObject.transform;
        generatedRoot.SetParent(transform, false);

        surfaceMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Surface (Runtime)",
            CreateSurfaceColor(),
            false);
        ringMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Ring (Runtime)",
            WithAlpha(difficultyColor, 0.92f),
            true);

        BuildSurface();
        BuildRings();
        visualsBuilt = true;
    }

    private void BuildSurface()
    {
        surfaceMesh = CreateEllipseMesh();

        GameObject surfaceObject = new GameObject("PortalSurface");
        surfaceObject.layer = gameObject.layer;
        surfaceObject.transform.SetParent(generatedRoot, false);
        // The station fronts face local -Z, so keep the opaque aperture behind the glowing rim.
        surfaceObject.transform.localPosition = localCenter + Vector3.forward * 0.02f;
        surfaceTransform = surfaceObject.transform;

        MeshFilter filter = surfaceObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = surfaceMesh;
        ConfigureRenderer(renderer, surfaceMaterial, -1);
    }

    private void BuildRings()
    {
        int count = 1;
        int segmentCount = Mathf.Clamp(ringSegments, 16, 32);
        for (int ringIndex = 0; ringIndex < count; ringIndex++)
        {
            GameObject ringObject = new GameObject("EnergyRing_" + (ringIndex + 1));
            ringObject.layer = gameObject.layer;
            ringObject.transform.SetParent(generatedRoot, false);
            ringObject.transform.localPosition = localCenter + Vector3.back * (0.012f + ringIndex * 0.01f);

            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segmentCount;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sortingOrder = ringIndex;
            line.sharedMaterial = ringMaterial;
            line.startWidth = ringIndex == 0 ? 0.075f : 0.045f;
            line.endWidth = line.startWidth;
            line.colorGradient = CreateRingGradient(ringIndex);

            Vector3[] positions = new Vector3[segmentCount];
            float radiusScale = 1f - ringIndex * 0.12f;
            float halfWidth = portalSize.x * 0.5f * radiusScale;
            float halfHeight = portalSize.y * 0.5f * radiusScale;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / segmentCount;
                float staticWave = 1f + Mathf.Sin(angle * (3 + ringIndex * 2) + ringIndex * 1.7f) * ringWobble;
                positions[i] = new Vector3(
                    Mathf.Cos(angle) * halfWidth * staticWave,
                    Mathf.Sin(angle) * halfHeight * staticWave,
                    Mathf.Sin(angle * (4 + ringIndex)) * swirlDepth);
            }
            line.SetPositions(positions);

            rings.Add(new RingVisual
            {
                transform = ringObject.transform,
                rotationSpeed = ringIndex == 0 ? 9f : -6f,
                phase = ringIndex * 2.1f
            });
        }
    }

    private void AnimateTransforms(float time)
    {
        float pulse = 1f + Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        if (surfaceTransform != null)
        {
            surfaceTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        for (int i = 0; i < rings.Count; i++)
        {
            RingVisual ring = rings[i];
            if (ring.transform == null)
            {
                continue;
            }

            float rotation = Mathf.Repeat(time * ring.rotationSpeed + ring.phase * Mathf.Rad2Deg, 360f);
            float ringPulse = 1f + Mathf.Sin(time * (0.72f + i * 0.08f) + ring.phase) * pulseAmount * 0.65f;
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            ring.transform.localScale = new Vector3(ringPulse, ringPulse, 1f);
        }
    }

    private bool ShouldAnimateForCamera()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }
        if (cachedCamera == null)
        {
            return true;
        }

        Vector3 worldCenter = transform.TransformPoint(localCenter);
        float maximumDistance = Mathf.Max(10f, activationDistance);
        if ((cachedCamera.transform.position - worldCenter).sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        Vector3 viewport = cachedCamera.WorldToViewportPoint(worldCenter);
        return viewport.z > 0f && viewport.x > -0.2f && viewport.x < 1.2f && viewport.y > -0.2f && viewport.y < 1.2f;
    }

    private Mesh CreateEllipseMesh()
    {
        Vector3[] vertices = new Vector3[SurfaceSegments + 1];
        Vector2[] uv = new Vector2[SurfaceSegments + 1];
        int[] triangles = new int[SurfaceSegments * 3];
        float halfWidth = portalSize.x * 0.5f;
        float halfHeight = portalSize.y * 0.5f;

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < SurfaceSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / SurfaceSegments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * halfWidth, Mathf.Sin(angle) * halfHeight, 0f);
            uv[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);

            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = (i + 1) % SurfaceSegments + 1;
        }

        Mesh mesh = new Mesh
        {
            name = "ML Portal Surface (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            uv = uv,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Gradient CreateRingGradient(int ringIndex)
    {
        Color bright = Color.Lerp(difficultyColor, Color.white, ringIndex == 0 ? 0.62f : 0.42f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(difficultyColor, 0f),
                new GradientColorKey(bright, 0.48f),
                new GradientColorKey(difficultyColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.48f, 0f),
                new GradientAlphaKey(0.95f, 0.48f),
                new GradientAlphaKey(0.48f, 1f)
            });
        return gradient;
    }

    private static void ConfigureRenderer(Renderer renderer, Material material, int sortingOrder)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = sortingOrder;
    }

    private Material CreateRuntimeMaterial(Shader shader, string materialName, Color color, bool additive)
    {
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = additive ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry,
            enableInstancing = true
        };

        material.SetOverrideTag("RenderType", additive ? "Transparent" : "Opaque");
        SetFloatIfPresent(material, "_Surface", additive ? 1f : 0f);
        SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_ZWrite", additive ? 0f : 1f);
        SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
        SetFloatIfPresent(material, "_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.Zero);
        if (additive)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        SetMaterialColor(material, color, additive ? 1.45f : 0.45f);
        runtimeMaterials.Add(material);
        return material;
    }

    private static Shader FindTransparentShader()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Unlit/Transparent",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Standard"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader != null)
            {
                return shader;
            }
        }
        return null;
    }

    private static void SetMaterialColor(Material material, Color color, float emissionIntensity)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(
                color.r * emissionIntensity,
                color.g * emissionIntensity,
                color.b * emissionIntensity,
                color.a));
            material.EnableKeyword("_EMISSION");
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private Color CreateSurfaceColor()
    {
        Color nearBlack = new Color(0.006f, 0.009f, 0.025f, 1f);
        float tintAmount = Mathf.Clamp01(0.16f + surfaceOpacity * 0.55f);
        float darkness = Mathf.Lerp(0.12f, 0.24f, voidOpacity);
        return WithAlpha(Color.Lerp(nearBlack, difficultyColor, tintAmount * darkness), 1f);
    }

    private void HideLegacyRenderers()
    {
        if (!hideLegacyFocalPoint || hiddenLegacyRenderers.Count > 0)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null || !candidate.enabled || IsGeneratedVisual(candidate.transform))
            {
                continue;
            }
            if (!HasLegacyDecorationName(candidate.transform))
            {
                continue;
            }

            candidate.enabled = false;
            hiddenLegacyRenderers.Add(candidate);
        }
    }

    private bool HasLegacyDecorationName(Transform candidate)
    {
        if (legacyDecorationNameFragments == null)
        {
            return false;
        }

        Transform current = candidate;
        while (current != null && current != transform)
        {
            string objectName = current.gameObject.name;
            for (int i = 0; i < legacyDecorationNameFragments.Length; i++)
            {
                string fragment = legacyDecorationNameFragments[i];
                if (!string.IsNullOrWhiteSpace(fragment) &&
                    objectName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            current = current.parent;
        }
        return false;
    }

    private bool IsGeneratedVisual(Transform candidate)
    {
        return generatedRoot != null && (candidate == generatedRoot || candidate.IsChildOf(generatedRoot));
    }

    private void RestoreLegacyRenderers()
    {
        for (int i = 0; i < hiddenLegacyRenderers.Count; i++)
        {
            if (hiddenLegacyRenderers[i] != null)
            {
                hiddenLegacyRenderers[i].enabled = true;
            }
        }
        hiddenLegacyRenderers.Clear();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
