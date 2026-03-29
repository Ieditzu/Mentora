using UnityEngine;

/// <summary>
/// Creates a large water volume filling everything at y <= 0. Visual only; drowning handled by player y check.
/// </summary>
public static class WaterPlaneInitializer
{
    private static readonly Color SurfaceShallowColor = new Color(0.18f, 0.62f, 0.92f, 0.82f);
    private static readonly Color SurfaceDeepColor = new Color(0.03f, 0.24f, 0.52f, 0.94f);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Build()
    {
        // On Android/Quest stability first: skip runtime water creation (visual only) to avoid GPU driver crashes.
        if (Application.platform == RuntimePlatform.Android)
        {
            return;
        }

        if (GameObject.Find("InfiniteWater") != null)
        {
            return;
        }

        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "InfiniteWater";
        water.transform.position = new Vector3(0f, -500f, 0f); // fills everything below y=0
        water.transform.localScale = new Vector3(4000f, 1000f, 4000f);

        var collider = water.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true; // let player pass through
        }

        var renderer = water.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreateWaterSurfaceMaterial();
        }

        // Surface at y=0 for visible water top, with animated vertex waves.
        GameObject surface = new GameObject("WaterSurface");
        surface.transform.position = new Vector3(0f, 0.01f, 0f);
        surface.transform.localScale = Vector3.one;
        var mf = surface.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildGridMesh(800f, 180);
        var mr = surface.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = CreateWaterSurfaceMaterial();
        surface.AddComponent<WaterWaveAnimator>();
    }

    /// <summary>
    /// Builds a subdivided quad so the water shader's vertex waves look smooth even when very large.
    /// </summary>
    private static Mesh BuildGridMesh(float size, int subdivisions)
    {
        int vertCountPerSide = subdivisions + 1;
        int vertexCount = vertCountPerSide * vertCountPerSide;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];

        float half = size * 0.5f;
        float step = size / subdivisions;
        int v = 0;
        for (int z = 0; z <= subdivisions; z++)
        {
            float posZ = -half + z * step;
            for (int x = 0; x <= subdivisions; x++)
            {
                float posX = -half + x * step;
                vertices[v] = new Vector3(posX, 0f, posZ);
                normals[v] = Vector3.up;
                uvs[v] = new Vector2((float)x / subdivisions, (float)z / subdivisions);
                v++;
            }
        }

        int[] triangles = new int[subdivisions * subdivisions * 6];
        int t = 0;
        for (int z = 0; z < subdivisions; z++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                int i0 = z * vertCountPerSide + x;
                int i1 = i0 + 1;
                int i2 = i0 + vertCountPerSide;
                int i3 = i2 + 1;

                triangles[t++] = i0;
                triangles[t++] = i2;
                triangles[t++] = i1;

                triangles[t++] = i1;
                triangles[t++] = i2;
                triangles[t++] = i3;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "RuntimeWaterGrid"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateWaterSurfaceMaterial()
    {
        // Quest (Android) GPUs can crash on some custom vertex-warp shaders; use URP/Lit there.
        if (Application.platform == RuntimePlatform.Android)
        {
            return CreateFallbackWaterMaterial();
        }

        Shader shader = Shader.Find("Custom/BridgeWater");
        if (shader == null)
        {
            return CreateFallbackWaterMaterial();
        }

        Material material = new Material(shader);
        material.name = "RuntimeBridgeWaterSurface";
        material.SetColor("_ShallowColor", SurfaceShallowColor);
        material.SetColor("_DeepColor", SurfaceDeepColor);
        material.SetColor("_FoamColor", new Color(0.88f, 0.97f, 1f, 1f));
        material.SetFloat("_WaveAmplitude", 0.18f);
        material.SetFloat("_WaveFrequency", 0.95f);
        material.SetFloat("_WaveSpeed", 1.7f);
        material.SetFloat("_SecondaryWaveAmplitude", 0.09f);
        material.SetFloat("_SecondaryWaveFrequency", 2.4f);
        material.SetFloat("_SecondaryWaveSpeed", 2.25f);
        material.SetFloat("_Smoothness", 0.96f);
        material.SetFloat("_FresnelPower", 4.1f);
        material.SetFloat("_FoamStrength", 0.34f);
        material.renderQueue = 3000;
        return material;
    }

    private static Material CreateFallbackWaterMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material m = new Material(shader);
        m.name = "RuntimeWaterFallback";
        SetMainColor(m, SurfaceShallowColor);

        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f); // Transparent
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_Smoothness"))
            {
                m.SetFloat("_Smoothness", 0.9f);
            }
            if (m.HasProperty("_Metallic"))
            {
                m.SetFloat("_Metallic", 0f);
            }
        }
        else
        {
            m.SetFloat("_Mode", 3); // for Standard fallback
            m.EnableKeyword("_ALPHABLEND_ON");
        }

        m.renderQueue = 3000;
        return m;
    }

    private static void SetMainColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }
}
