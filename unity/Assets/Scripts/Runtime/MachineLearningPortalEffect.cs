using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MachineLearningPortalEffect : MonoBehaviour
{
    private const string GeneratedRootName = "__MachineLearningPortalEffect";
    private const int SurfaceAngularSegments = 48;
    private const int SurfaceRadialSegments = 5;

    [Header("Portal appearance")]
    [SerializeField] private Color difficultyColor = new Color(0.16f, 0.95f, 0.56f, 1f);
    [SerializeField] private Vector3 localCenter = new Vector3(0f, 3.05f, 0.06f);
    [SerializeField] private Vector2 portalSize = new Vector2(4.8f, 5.05f);
    [SerializeField, Range(2, 3)] private int energyRingCount = 3;
    [SerializeField, Range(24, 64)] private int ringSegments = 48;
    [SerializeField, Range(0.25f, 0.9f)] private float voidOpacity = 0.62f;
    [SerializeField, Range(0.03f, 0.35f)] private float surfaceOpacity = 0.18f;

    [Header("Motion")]
    [SerializeField, Range(12f, 30f)] private float animationFramesPerSecond = 24f;
    [SerializeField, Range(0f, 0.08f)] private float pulseAmount = 0.025f;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 1.65f;
    [SerializeField, Range(0f, 0.12f)] private float ringWobble = 0.035f;
    [SerializeField, Range(0f, 0.3f)] private float swirlDepth = 0.09f;

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

    private readonly List<EnergyRing> energyRings = new List<EnergyRing>(3);
    private readonly List<Material> runtimeMaterials = new List<Material>(4);
    private readonly List<Renderer> hiddenLegacyRenderers = new List<Renderer>();

    private Transform generatedRoot;
    private Transform voidTransform;
    private Transform surfaceTransform;
    private Material voidMaterial;
    private Material surfaceMaterial;
    private Material ringMaterial;
    private Material sparkMaterial;
    private Mesh surfaceMesh;
    private ParticleSystem sparkParticles;
    private float animationPhase;
    private float nextAnimationTime;
    private bool visualsBuilt;

    private sealed class EnergyRing
    {
        public LineRenderer lineRenderer;
        public Vector3[] positions;
        public float radiusScale;
        public float speed;
        public float phase;
        public float width;
        public int waveCount;
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
        if (isActiveAndEnabled)
        {
            EnsureVisualsBuilt();
        }
    }

    private void OnEnable()
    {
        EnsureVisualsBuilt();
        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(true);
        }

        HideLegacyRenderers();
        if (sparkParticles != null && !sparkParticles.isPlaying)
        {
            sparkParticles.Play(true);
        }
        nextAnimationTime = 0f;
    }

    private void OnDisable()
    {
        if (sparkParticles != null)
        {
            sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

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
        voidTransform = null;
        surfaceTransform = null;
        voidMaterial = null;
        surfaceMaterial = null;
        ringMaterial = null;
        sparkMaterial = null;
        energyRings.Clear();
        visualsBuilt = false;
    }

    private void OnValidate()
    {
        portalSize.x = Mathf.Max(0.5f, portalSize.x);
        portalSize.y = Mathf.Max(0.5f, portalSize.y);
        energyRingCount = Mathf.Clamp(energyRingCount, 2, 3);
        ringSegments = Mathf.Clamp(ringSegments, 24, 64);
        voidOpacity = Mathf.Clamp(voidOpacity, 0.25f, 0.9f);
        surfaceOpacity = Mathf.Clamp(surfaceOpacity, 0.03f, 0.35f);
        animationFramesPerSecond = Mathf.Clamp(animationFramesPerSecond, 12f, 30f);
        pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.08f);
        pulseSpeed = Mathf.Max(0.1f, pulseSpeed);
        ringWobble = Mathf.Clamp(ringWobble, 0f, 0.12f);
        swirlDepth = Mathf.Clamp(swirlDepth, 0f, 0.3f);
    }

    private void Update()
    {
        if (!visualsBuilt)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextAnimationTime)
        {
            return;
        }

        nextAnimationTime = now + 1f / Mathf.Max(12f, animationFramesPerSecond);
        float time = now + animationPhase;
        float pulse = 1f + Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;

        if (voidTransform != null)
        {
            float inversePulse = 1f - (pulse - 1f) * 0.35f;
            float voidScale = inversePulse * 0.985f;
            voidTransform.localScale = new Vector3(voidScale, voidScale, 1f);
        }

        if (surfaceTransform != null)
        {
            surfaceTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (voidMaterial != null)
        {
            float voidPulse = 0.96f + Mathf.Sin(time * pulseSpeed * 2.1f + 2.2f) * 0.04f;
            SetMaterialColor(voidMaterial, CreateVoidColor(voidOpacity * voidPulse), 0.16f);
        }

        if (surfaceMaterial != null)
        {
            float alphaPulse = 0.9f + Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f + 0.7f) * 0.1f;
            SetMaterialColor(surfaceMaterial, WithAlpha(difficultyColor, surfaceOpacity * alphaPulse), 1.4f);
        }

        for (int i = 0; i < energyRings.Count; i++)
        {
            AnimateRing(energyRings[i], time, i);
        }
    }

    public void ConfigureColor(Color color)
    {
        difficultyColor = color;
        difficultyColor.a = 1f;

        if (!visualsBuilt)
        {
            return;
        }

        if (voidMaterial != null)
        {
            SetMaterialColor(voidMaterial, CreateVoidColor(voidOpacity), 0.16f);
        }

        if (surfaceMaterial != null)
        {
            SetMaterialColor(surfaceMaterial, WithAlpha(difficultyColor, surfaceOpacity), 1.4f);
        }

        if (ringMaterial != null)
        {
            SetMaterialColor(ringMaterial, WithAlpha(difficultyColor, 0.88f), 1.7f);
        }

        if (sparkMaterial != null)
        {
            SetMaterialColor(sparkMaterial, WithAlpha(difficultyColor, 0.9f), 1.7f);
        }

        for (int i = 0; i < energyRings.Count; i++)
        {
            LineRenderer line = energyRings[i].lineRenderer;
            if (line != null)
            {
                line.colorGradient = CreateRingGradient(i);
            }
        }

        if (sparkParticles != null)
        {
            ParticleSystem.MainModule main = sparkParticles.main;
            Color bright = Color.Lerp(difficultyColor, Color.white, 0.65f);
            main.startColor = new ParticleSystem.MinMaxGradient(difficultyColor, bright);
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

        voidMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Void (Runtime)",
            CreateVoidColor(voidOpacity),
            false);

        surfaceMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Surface (Runtime)",
            WithAlpha(difficultyColor, surfaceOpacity),
            false);

        ringMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Rings (Runtime)",
            WithAlpha(difficultyColor, 0.88f),
            true);

        sparkMaterial = CreateRuntimeMaterial(
            transparentShader,
            "ML Portal Sparks (Runtime)",
            WithAlpha(difficultyColor, 0.9f),
            true);

        BuildEnergySurface();
        BuildEnergyRings(ringMaterial);
        BuildSparks(sparkMaterial);
        visualsBuilt = true;
    }

    private void BuildEnergySurface()
    {
        surfaceMesh = CreateEnergySurfaceMesh();

        GameObject voidObject = new GameObject("PortalVoid");
        voidObject.layer = gameObject.layer;
        voidObject.transform.SetParent(generatedRoot, false);
        voidObject.transform.localPosition = localCenter + Vector3.back * 0.018f;
        voidObject.transform.localScale = new Vector3(0.985f, 0.985f, 1f);
        voidTransform = voidObject.transform;

        MeshFilter voidFilter = voidObject.AddComponent<MeshFilter>();
        MeshRenderer voidRenderer = voidObject.AddComponent<MeshRenderer>();
        voidFilter.sharedMesh = surfaceMesh;
        ConfigureRenderer(voidRenderer, voidMaterial, -3);

        GameObject surfaceObject = new GameObject("EnergySurface");
        surfaceObject.layer = gameObject.layer;
        surfaceObject.transform.SetParent(generatedRoot, false);
        surfaceObject.transform.localPosition = localCenter;
        surfaceTransform = surfaceObject.transform;

        MeshFilter filter = surfaceObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = surfaceMesh;
        ConfigureRenderer(renderer, surfaceMaterial, -2);
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

    private Mesh CreateEnergySurfaceMesh()
    {
        int ringVertexCount = SurfaceAngularSegments;
        int vertexCount = 1 + SurfaceRadialSegments * ringVertexCount;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[SurfaceAngularSegments * 3 +
                                  (SurfaceRadialSegments - 1) * SurfaceAngularSegments * 6];

        float halfWidth = portalSize.x * 0.5f;
        float halfHeight = portalSize.y * 0.5f;
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);
        colors[0] = new Color(1f, 1f, 1f, 0.72f);

        for (int radialIndex = 1; radialIndex <= SurfaceRadialSegments; radialIndex++)
        {
            float radius = radialIndex / (float)SurfaceRadialSegments;
            float edgeFade = Mathf.Pow(1f - radius, 0.72f);
            float band = 0.78f + Mathf.Sin(radius * Mathf.PI * 4f) * 0.12f;
            Color vertexColor = new Color(1f, 1f, 1f, Mathf.Clamp01(edgeFade * band));

            for (int angularIndex = 0; angularIndex < SurfaceAngularSegments; angularIndex++)
            {
                float angle = angularIndex * Mathf.PI * 2f / SurfaceAngularSegments;
                int vertexIndex = 1 + (radialIndex - 1) * ringVertexCount + angularIndex;
                float x = Mathf.Cos(angle) * halfWidth * radius;
                float y = Mathf.Sin(angle) * halfHeight * radius;
                vertices[vertexIndex] = new Vector3(x, y, 0f);
                uv[vertexIndex] = new Vector2(
                    0.5f + Mathf.Cos(angle) * radius * 0.5f,
                    0.5f + Mathf.Sin(angle) * radius * 0.5f);
                colors[vertexIndex] = vertexColor;
            }
        }

        int triangleIndex = 0;
        for (int angularIndex = 0; angularIndex < SurfaceAngularSegments; angularIndex++)
        {
            int next = (angularIndex + 1) % SurfaceAngularSegments;
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = 1 + angularIndex;
            triangles[triangleIndex++] = 1 + next;
        }

        for (int radialIndex = 1; radialIndex < SurfaceRadialSegments; radialIndex++)
        {
            int innerStart = 1 + (radialIndex - 1) * ringVertexCount;
            int outerStart = innerStart + ringVertexCount;
            for (int angularIndex = 0; angularIndex < SurfaceAngularSegments; angularIndex++)
            {
                int next = (angularIndex + 1) % SurfaceAngularSegments;
                triangles[triangleIndex++] = innerStart + angularIndex;
                triangles[triangleIndex++] = outerStart + angularIndex;
                triangles[triangleIndex++] = outerStart + next;
                triangles[triangleIndex++] = innerStart + angularIndex;
                triangles[triangleIndex++] = outerStart + next;
                triangles[triangleIndex++] = innerStart + next;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "ML Portal Energy Surface (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            uv = uv,
            colors = colors,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void BuildEnergyRings(Material ringMaterial)
    {
        for (int i = 0; i < energyRingCount; i++)
        {
            GameObject ringObject = new GameObject("EnergyRing_" + (i + 1));
            ringObject.layer = gameObject.layer;
            ringObject.transform.SetParent(generatedRoot, false);
            ringObject.transform.localPosition = localCenter + Vector3.forward * (0.012f + i * 0.012f);

            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = ringSegments;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 0;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sortingOrder = i;
            line.sharedMaterial = ringMaterial;
            line.colorGradient = CreateRingGradient(i);

            EnergyRing ring = new EnergyRing
            {
                lineRenderer = line,
                positions = new Vector3[ringSegments],
                radiusScale = 1f - i * 0.105f,
                speed = (i % 2 == 0 ? 1f : -1f) * (1.05f + i * 0.31f),
                phase = i * 1.73f,
                width = Mathf.Max(0.035f, 0.09f - i * 0.022f),
                waveCount = 3 + i * 2
            };
            energyRings.Add(ring);
            AnimateRing(ring, animationPhase, i);
        }
    }

    private void AnimateRing(EnergyRing ring, float time, int ringIndex)
    {
        if (ring.lineRenderer == null)
        {
            return;
        }

        float halfWidth = portalSize.x * 0.5f * ring.radiusScale;
        float halfHeight = portalSize.y * 0.5f * ring.radiusScale;
        float travellingPhase = time * ring.speed + ring.phase;
        float gradientTravel = travellingPhase * (0.2f + ringIndex * 0.035f);
        int count = ring.positions.Length;

        for (int i = 0; i < count; i++)
        {
            // Offset the ellipse parameter rather than rotating its transform. This keeps the
            // opening upright while the LineRenderer gradient's bright arc travels around it.
            float angle = i * Mathf.PI * 2f / count + gradientTravel;
            float wave = 1f + Mathf.Sin(angle * ring.waveCount + travellingPhase * 2.1f) * ringWobble;
            float secondaryWave = Mathf.Sin(angle * (ring.waveCount + 2) - travellingPhase * 1.4f);
            float z = secondaryWave * swirlDepth * (0.55f + ringIndex * 0.18f);
            ring.positions[i] = new Vector3(
                Mathf.Cos(angle) * halfWidth * wave,
                Mathf.Sin(angle) * halfHeight * wave,
                z);
        }

        ring.lineRenderer.SetPositions(ring.positions);
        float widthPulse = 0.9f + Mathf.Sin(time * 2.4f + ring.phase) * 0.1f;
        ring.lineRenderer.startWidth = ring.width * widthPulse;
        ring.lineRenderer.endWidth = ring.width * widthPulse;
    }

    private Gradient CreateRingGradient(int ringIndex)
    {
        Color softWhite = Color.Lerp(Color.white, difficultyColor, 0.22f + ringIndex * 0.08f);
        Color coolEdge = Color.Lerp(Color.white, difficultyColor, 0.48f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(coolEdge, 0f),
                new GradientColorKey(Color.white, 0.38f),
                new GradientColorKey(softWhite, 0.72f),
                new GradientColorKey(coolEdge, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.24f, 0f),
                new GradientAlphaKey(0.95f, 0.3f),
                new GradientAlphaKey(0.42f, 0.68f),
                new GradientAlphaKey(0.24f, 1f)
            });
        return gradient;
    }

    private void BuildSparks(Material sparkMaterial)
    {
        GameObject sparksObject = new GameObject("PortalSparks");
        sparksObject.layer = gameObject.layer;
        sparksObject.transform.SetParent(generatedRoot, false);
        sparksObject.transform.localPosition = localCenter + Vector3.back * 0.025f;

        sparkParticles = sparksObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = sparkParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        Color bright = Color.Lerp(difficultyColor, Color.white, 0.65f);
        main.startColor = new ParticleSystem.MinMaxGradient(difficultyColor, bright);
        main.maxParticles = 28;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = sparkParticles.emission;
        emission.rateOverTime = 7f;

        ParticleSystem.ShapeModule shape = sparkParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;
        shape.radiusThickness = 0.08f;
        shape.scale = new Vector3(portalSize.x, portalSize.y, 0.12f);

        ParticleSystem.VelocityOverLifetimeModule velocity = sparkParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0.32f, 0.7f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-0.025f, 0.035f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparkParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient lifetimeGradient = new Gradient();
        lifetimeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.78f, 0.2f),
                new GradientAlphaKey(0.55f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = lifetimeGradient;

        ParticleSystemRenderer particleRenderer = sparksObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = sparkMaterial;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.sortingOrder = 3;
        particleRenderer.maxParticleSize = 0.08f;

        sparkParticles.Play(true);
    }

    private Material CreateRuntimeMaterial(
        Shader shader,
        string materialName,
        Color color,
        bool additive)
    {
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent,
            enableInstancing = true
        };

        material.SetOverrideTag("RenderType", "Transparent");
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", additive
            ? (float)BlendMode.One
            : (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        SetMaterialColor(material, color, additive ? 1.7f : 1.4f);

        runtimeMaterials.Add(material);
        return material;
    }

    private static Shader FindTransparentShader()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Sprites/Default",
            "Universal Render Pipeline/Unlit",
            "Particles/Standard Unlit",
            "Legacy Shaders/Particles/Additive",
            "Unlit/Transparent",
            "Universal Render Pipeline/Lit",
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
        if (material == null)
        {
            return;
        }

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
            Color emission = new Color(
                color.r * emissionIntensity,
                color.g * emissionIntensity,
                color.b * emissionIntensity,
                color.a);
            material.SetColor("_EmissionColor", emission);
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

    private Color CreateVoidColor(float alpha)
    {
        Color nearBlack = new Color(0.006f, 0.009f, 0.025f, 1f);
        Color tintedVoid = Color.Lerp(nearBlack, difficultyColor, 0.16f);
        return WithAlpha(tintedVoid, alpha);
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
