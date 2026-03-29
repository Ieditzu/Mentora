using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR

public static class GameSceneBuilderEditor
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const float ArenaHalfWidth = 12f;
    private const float ArenaHalfDepth = 9f;
    private static readonly Color GrassTopColor = new Color(0.43f, 0.75f, 0.34f, 1f);
    private static readonly Color GrassHighlightColor = new Color(0.62f, 0.89f, 0.46f, 1f);
    private static readonly Color SoilColor = new Color(0.42f, 0.27f, 0.14f, 1f);
    private static readonly Color SoilShadowColor = new Color(0.28f, 0.18f, 0.1f, 1f);
    private static readonly Color WoodColor = new Color(0.49f, 0.31f, 0.16f, 1f);
    private static readonly Color WoodDarkColor = new Color(0.31f, 0.2f, 0.1f, 1f);
    private const string TreePrefabPath = "Assets/PolygonStarter/Prefabs/SM_Generic_Tree_02.prefab";
    private const string RockPrefabPath = "Assets/PolygonStarter/Prefabs/SM_Generic_Small_Rocks_03.prefab";
    private const bool PreserveExistingSceneLayout = true;

    [MenuItem("Bila/Build Scene Layout")]
    public static void BuildScene()
    {
        var sphere = Object.FindObjectOfType<BeanController>();
        if (sphere == null)
        {
            var sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereObject.name = "PlayerSphere";
            sphereObject.transform.position = new Vector3(0f, 0.5f, 0f);

            var rb = sphereObject.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.drag = 0.2f;
            rb.angularDrag = 0.35f;

            sphere = sphereObject.AddComponent<BeanController>();
        }

        EnsureSphereShape(sphere.gameObject);
        
        EnsureSphereVisual(sphere.gameObject);
        EnsureLowBouncePhysics(sphere.gameObject);
        EnsureSphereRigidbodyTuning(sphere.gameObject);
        RemoveLegacyDualPitFloor();
        EnsureBlackFloor();
        EnsureSpawnLobbyFloor();
        
        EnsureTopLeftCustomSkinPipe();
        EnsureBoundaryWallsAndDoor();
        EnsureOutsideGateFloor();
        EnsurePostGateApproach();
        EnsureDifficultyPaths();
        EnsureSpiralTubesAndPlatforms();
        EnsureMobileTouchControls();
        FallCounterDisplay.CreateInSceneIfMissing();
        FallCounterDisplay.SetCount(0);
        ConfigureTopFollowCamera(sphere.transform);
        MarkActiveSceneDirtyIfEditable();
    }

    private static void EnsureMobileTouchControls()
    {
        var root = GameObject.Find("MobileTouchControls");
        if (root == null)
        {
            root = new GameObject("MobileTouchControls");
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        ClearChildren(root.transform);

        var canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = root.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;

        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = root.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (root.GetComponent<GraphicRaycaster>() == null)
        {
            root.AddComponent<GraphicRaycaster>();
        }

        if (root.GetComponent<MobileTouchHud>() == null)
        {
            root.AddComponent<MobileTouchHud>();
        }

        var stickAreaObj = new GameObject("JoystickArea");
        stickAreaObj.transform.SetParent(root.transform, false);
        var areaRect = stickAreaObj.AddComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0f, 0f);
        areaRect.anchorMax = new Vector2(0f, 0f);
        areaRect.pivot = new Vector2(0.5f, 0.5f);
        areaRect.anchoredPosition = new Vector2(170f, 170f);
        areaRect.sizeDelta = new Vector2(260f, 260f);
        var areaImage = stickAreaObj.AddComponent<Image>();
        areaImage.color = new Color(0.08f, 0.08f, 0.08f, 0.5f);

        var handleObj = new GameObject("JoystickHandle");
        handleObj.transform.SetParent(stickAreaObj.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(120f, 120f);
        var handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.78f);

        var joystick = stickAreaObj.AddComponent<MobileJoystick>();
        var serializedObject = new SerializedObject(joystick);
        serializedObject.FindProperty("joystickArea").objectReferenceValue = areaRect;
        serializedObject.FindProperty("handle").objectReferenceValue = handleRect;
        serializedObject.FindProperty("handleRange").floatValue = 70f;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        var labelObj = new GameObject("JoystickLabel");
        labelObj.transform.SetParent(stickAreaObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -8f);
        labelRect.sizeDelta = new Vector2(180f, 26f);
        var label = labelObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "Move";
        label.color = Color.white;

        var jumpObj = new GameObject("JumpButton");
        jumpObj.transform.SetParent(root.transform, false);
        var jumpRect = jumpObj.AddComponent<RectTransform>();
        jumpRect.anchorMin = new Vector2(1f, 0f);
        jumpRect.anchorMax = new Vector2(1f, 0f);
        jumpRect.pivot = new Vector2(0.5f, 0.5f);
        jumpRect.anchoredPosition = new Vector2(-170f, 170f);
        jumpRect.sizeDelta = new Vector2(180f, 180f);
        var jumpImage = jumpObj.AddComponent<Image>();
        jumpImage.color = new Color(0.85f, 0.25f, 0.25f, 0.72f);

        var jumpButton = jumpObj.AddComponent<Button>();
        jumpButton.targetGraphic = jumpImage;
        jumpObj.AddComponent<MobileJumpButton>();

        var jumpLabelObj = new GameObject("JumpLabel");
        jumpLabelObj.transform.SetParent(jumpObj.transform, false);
        var jumpLabelRect = jumpLabelObj.AddComponent<RectTransform>();
        jumpLabelRect.anchorMin = Vector2.zero;
        jumpLabelRect.anchorMax = Vector2.one;
        jumpLabelRect.offsetMin = Vector2.zero;
        jumpLabelRect.offsetMax = Vector2.zero;
        var jumpLabel = jumpLabelObj.AddComponent<Text>();
        jumpLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        jumpLabel.fontSize = 34;
        jumpLabel.alignment = TextAnchor.MiddleCenter;
        jumpLabel.text = "JUMP";
        jumpLabel.color = Color.white;
    }

    private static void EnsureSphereShape(GameObject sphereObject)
    {
        var boxCollider = sphereObject.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Object.DestroyImmediate(boxCollider);
        }

        var sphereCollider = sphereObject.GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = sphereObject.AddComponent<SphereCollider>();
        }

        // Align the player's mesh with a built-in sphere mesh.
        var meshFilter = sphereObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = sphereObject.AddComponent<MeshFilter>();
        }

        var tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sphereMesh = tempSphere.GetComponent<MeshFilter>()?.sharedMesh;
        Object.DestroyImmediate(tempSphere);

        if (sphereMesh != null)
        {
            meshFilter.sharedMesh = sphereMesh;
        }

        var meshRenderer = sphereObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            sphereObject.AddComponent<MeshRenderer>();
        }
    }

    [MenuItem("Bila/Build Scene Layout And Save")]
    public static void BuildSceneAndSave()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Build Scene Layout And Save is unavailable during Play Mode.");
            return;
        }

        BuildScene();
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Bila/Inject New Questions Only")]
    public static void InjectNewQuestionsOnly()
    {
        var allTriggers = Object.FindObjectsOfType<CppQuestionTrigger>(true);
        CppQuestionTrigger fifth = null;
        foreach (var t in allTriggers) {
            if (t.gameObject.name == "FifthQuestionTrigger") {
                fifth = t;
                break;
            }
        }

        if (fifth == null) {
            Debug.LogError("Could not find FifthQuestionTrigger. Please ensure the scene is set up properly first.");
            return;
        }

        Transform fifthAnswersRoot = fifth.answersRoot;
        if (fifthAnswersRoot == null) {
            fifthAnswersRoot = fifth.transform.parent.Find("FifthQuestionAnswersRoot");
        }

        if (fifthAnswersRoot == null) {
            Debug.LogError("Could not find FifthQuestionAnswersRoot.");
            return;
        }

        Transform sixthAnswersRoot;
        CppQuestionTrigger sixthTrigger = EnsureQuestionStageArea(
            fifthAnswersRoot,
            "SixthQuestionArea",
            "SixthQuestionPlatform",
            "SixthQuestionTrigger",
            "SixthQuestionBridge",
            "SixthQuestionAnswersRoot",
            "What is the\nvalue of x?",
            "int x = 10;\nx += 5;\nx -= 2;",
            new[] { "13", "15", "10" },
            0,
            6,
            true,
            0.8f,
            false,
            out sixthAnswersRoot);

        Transform seventhAnswersRoot;
        CppQuestionTrigger seventhTrigger = EnsureQuestionStageArea(
            sixthAnswersRoot,
            "SeventhQuestionArea",
            "SeventhQuestionPlatform",
            "SeventhQuestionTrigger",
            "SeventhQuestionBridge",
            "SeventhQuestionAnswersRoot",
            "What will this\ncode print?",
            "for(int i=0; i<3; i++) {\n cout << i;\n}",
            new[] { "123", "012", "0123" },
            1,
            7,
            true,
            1.1f,
            false,
            out seventhAnswersRoot);

        Transform eighthAnswersRoot;
        CppQuestionTrigger eighthTrigger = EnsureQuestionStageArea(
            seventhAnswersRoot,
            "EighthQuestionArea",
            "EighthQuestionPlatform",
            "EighthQuestionTrigger",
            "EighthQuestionBridge",
            "EighthQuestionAnswersRoot",
            "What does this\nreturn?",
            "bool a = true;\nbool b = false;\nreturn a && b;",
            new[] { "true", "false", "1" },
            1,
            8,
            false,
            0.8f,
            false,
            out eighthAnswersRoot);

        fifth.SetNextQuestionRoot(sixthTrigger.transform.parent);
        fifth.SetSpawnNextQuestionOnCorrect(true);

        sixthTrigger.SetNextQuestionRoot(seventhTrigger.transform.parent);
        seventhTrigger.SetNextQuestionRoot(eighthTrigger.transform.parent);
        eighthTrigger.SetNextQuestionRoot(null);
        eighthTrigger.SetSpawnNextQuestionOnCorrect(false);

        MarkActiveSceneDirtyIfEditable();
        Debug.Log("Successfully injected questions 6, 7, and 8 without rebuilding the full scene!");
    }

    public static void BuildSampleSceneLayoutAndSave()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("BuildSampleSceneLayoutAndSave is unavailable during Play Mode.");
            return;
        }

        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        BuildScene();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void MarkActiveSceneDirtyIfEditable()
    {
        if (Application.isPlaying)
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void EnsureSphereVisual(GameObject sphereObject)
    {
        var renderer = sphereObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return;
        }

        var material = new Material(shader);
        var texture = CreateFuturisticSphereTexture();
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        material.mainTexture = texture;
        material.color = new Color(0.9f, 0.96f, 1f, 1f);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.9f, 0.96f, 1f, 1f));
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.78f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.92f);
        }
        else if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.92f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.1f, 0.85f, 1f, 1f) * 0.75f);
        }

        renderer.material = material;
    }

    private static Texture2D CreateFuturisticSphereTexture()
    {
        const int width = 512;
        const int height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                Color color = new Color(0.06f, 0.08f, 0.12f, 1f);

                // Neon longitudinal seams.
                float seamA = Mathf.Abs(u - 0.2f);
                float seamB = Mathf.Abs(u - 0.5f);
                float seamC = Mathf.Abs(u - 0.8f);
                bool longitudinal = seamA < 0.007f || seamB < 0.007f || seamC < 0.007f;

                // Equatorial ring.
                bool equator = Mathf.Abs(v - 0.5f) < 0.015f;

                // Small panel break lines for motion readability.
                bool panelLines = Mathf.Abs(Mathf.Repeat(u * 12f, 1f) - 0.5f) < 0.02f && Mathf.Abs(v - 0.5f) > 0.06f;

                if (longitudinal || equator || panelLines)
                {
                    color = new Color(0.2f, 0.9f, 1f, 1f);
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static void EnsureLowBouncePhysics(GameObject sphereObject)
    {
        var colliders = sphereObject.GetComponents<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return;
        }

        var noBounce = new PhysicMaterial("NoBounce")
        {
            bounciness = 0f,
            dynamicFriction = 0.8f,
            staticFriction = 0.8f,
            frictionCombine = PhysicMaterialCombine.Average,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        foreach (var col in colliders)
        {
            col.material = noBounce;
        }
    }

    private static void EnsureSphereRigidbodyTuning(GameObject sphereObject)
    {
        var rb = sphereObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.drag = 0.2f;
        rb.angularDrag = 0.35f;
    }

    private static void EnsureBlackFloor()
    {
        var floor = GameObject.Find("Floor");
        if (floor == null)
        {
            floor = new GameObject("Floor");
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
        }

        var meshFilter = floor.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = floor.AddComponent<MeshFilter>();
        }

        var meshCollider = floor.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = floor.AddComponent<MeshCollider>();
        }

        var meshRenderer = floor.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = floor.AddComponent<MeshRenderer>();
        }

        var floorMesh = CreateLobbyFloorMesh();
        meshFilter.sharedMesh = floorMesh;
        meshCollider.sharedMesh = floorMesh;

        var renderer = floor.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                var uniformFloorPathColor = new Color(0.24f, 0.24f, 0.24f, 1f);
                material.color = uniformFloorPathColor;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", uniformFloorPathColor);
                }
                renderer.material = material;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static Mesh CreateLobbyFloorMesh()
    {
        const int xSegments = 140;
        const int zSegments = 110;

        int verticesPerRow = xSegments + 1;
        Vector3[] vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xSegments * zSegments * 6];

        int vertexIndex = 0;
        for (int z = 0; z <= zSegments; z++)
        {
            float v = z / (float)zSegments;
            float worldZ = Mathf.Lerp(-ArenaHalfDepth, ArenaHalfDepth, v);

            for (int x = 0; x <= xSegments; x++)
            {
                float u = x / (float)xSegments;
                float worldX = Mathf.Lerp(-ArenaHalfWidth, ArenaHalfWidth, u);

                float y = FloorHeightAt();

                vertices[vertexIndex] = new Vector3(worldX, y, worldZ);
                uvs[vertexIndex] = new Vector2(u, v);
                vertexIndex++;
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int i = z * verticesPerRow + x;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = i + verticesPerRow;
                triangles[triangleIndex++] = i + 1;

                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + verticesPerRow;
                triangles[triangleIndex++] = i + verticesPerRow + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "LobbyFloor";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float FloorHeightAt()
    {
        return 0f;
    }

    private static void EnsureBoundaryWallsAndDoor()
    {
        const float wallHeight = 3.6f;
        const float wallThickness = 0.8f;
        const float gateOpeningWidth = 2.4f;

        var wallsRoot = GameObject.Find("ArenaWalls");
        if (wallsRoot == null)
        {
            wallsRoot = new GameObject("ArenaWalls");
        }
        wallsRoot.transform.position = Vector3.zero;

        for (int i = wallsRoot.transform.childCount - 1; i >= 0; i--)
        {
            DestroySafe(wallsRoot.transform.GetChild(i).gameObject);
        }

        float wallCenterY = (wallHeight * 0.5f) - 0.3f; // sink walls into the floor a bit to avoid edge leaks
        EnsureWall(wallsRoot.transform, "WallTop", new Vector3(0f, wallCenterY, ArenaHalfDepth + (wallThickness * 0.5f)), new Vector3((ArenaHalfWidth * 2f) + (wallThickness * 2f), wallHeight, wallThickness));
        EnsureWall(wallsRoot.transform, "WallBottom", new Vector3(0f, wallCenterY, -ArenaHalfDepth - (wallThickness * 0.5f)), new Vector3((ArenaHalfWidth * 2f) + (wallThickness * 2f), wallHeight, wallThickness));
        float leftSegmentDepth = ((ArenaHalfDepth * 2f) - gateOpeningWidth) * 0.5f;
        float leftSegmentOffset = (gateOpeningWidth * 0.5f) + (leftSegmentDepth * 0.5f);
        EnsureWall(wallsRoot.transform, "WallLeftTop", new Vector3(-ArenaHalfWidth - (wallThickness * 0.5f), wallCenterY, leftSegmentOffset), new Vector3(wallThickness, wallHeight, leftSegmentDepth));
        EnsureWall(wallsRoot.transform, "WallLeftBottom", new Vector3(-ArenaHalfWidth - (wallThickness * 0.5f), wallCenterY, -leftSegmentOffset), new Vector3(wallThickness, wallHeight, leftSegmentDepth));
        EnsureWall(wallsRoot.transform, "WallRight", new Vector3(ArenaHalfWidth + (wallThickness * 0.5f), wallCenterY, 0f), new Vector3(wallThickness, wallHeight, ArenaHalfDepth * 2f));
        EnsureContainmentCeiling(wallsRoot.transform, wallHeight);

        var oldDoor = GameObject.Find("LeftDoor");
        if (oldDoor != null)
        {
            DestroySafe(oldDoor);
        }

        EnsureLeftGate(wallThickness, gateOpeningWidth);
    }

    private static void EnsureSpawnLobbyFloor()
    {
        var spawnFloor = GameObject.Find("SpawnLobbyFloor");
        if (spawnFloor == null)
        {
            spawnFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawnFloor.name = "SpawnLobbyFloor";
        }

        // Flat, solid patch centered on spawn to guarantee no holes under the player.
        spawnFloor.transform.position = new Vector3(0f, -0.05f, 0f);
        spawnFloor.transform.rotation = Quaternion.identity;
        spawnFloor.transform.localScale = new Vector3(12f, 0.1f, 9f);

        var collider = spawnFloor.GetComponent<Collider>();
        if (collider != null)
        {
            collider.material = CreateSlipperyNoBounceMaterial();
        }

        var renderer = spawnFloor.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                var uniformFloorPathColor = new Color(0.24f, 0.24f, 0.24f, 1f);
                material.color = uniformFloorPathColor;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", uniformFloorPathColor);
                }
                renderer.material = material;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void EnsureTopLeftCustomSkinPipe()
    {
        var oldRoot = GameObject.Find("TopRightBlackoutPipe");
        if (oldRoot != null)
        {
            DestroySafe(oldRoot);
        }

        var root = GameObject.Find("CustomSkinPipe");
        if (root == null)
        {
            root = new GameObject("CustomSkinPipe");
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        ClearChildren(root.transform);

        Vector3 pipePosition = new Vector3(-ArenaHalfWidth + 1.1f, 0.03f, ArenaHalfDepth - 1.1f);
        Transform pipe = CreateSelectionTube(root.transform, "BlackoutPipeVisual", pipePosition);

        var triggerObject = new GameObject("BlackoutPipeTrigger");
        triggerObject.transform.SetParent(root.transform);
        triggerObject.transform.localPosition = pipe.localPosition;
        triggerObject.transform.localRotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;

        var triggerCollider = triggerObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(1.05f, 1.6f, 1.05f);
        triggerCollider.center = new Vector3(0f, 0.4f, 0f);

        var blackoutTrigger = triggerObject.GetComponent<BlackoutPipeTrigger>();
        if (blackoutTrigger == null)
        {
            blackoutTrigger = triggerObject.AddComponent<BlackoutPipeTrigger>();
        }

        if (blackoutTrigger != null)
        {
            blackoutTrigger.SetPipeRoot(pipe);
        }
    }

    private static Transform CreateSelectionTube(Transform parent, string name, Vector3 localPosition)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Rim";
        rim.transform.SetParent(root.transform);
        rim.transform.localPosition = Vector3.zero;
        rim.transform.localRotation = Quaternion.identity;
        rim.transform.localScale = new Vector3(0.62f, 0.08f, 0.62f);

        const int segments = 12;
        const float shaftRadius = 0.34f;
        const float shaftHeight = 0.85f;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ShaftWall_" + i;
            wall.transform.SetParent(root.transform);
            wall.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * shaftRadius,
                -(shaftHeight * 0.5f),
                Mathf.Sin(angle) * shaftRadius);
            wall.transform.localRotation = Quaternion.Euler(0f, -Mathf.Rad2Deg * angle, 0f);
            wall.transform.localScale = new Vector3(0.08f, shaftHeight, 0.24f);

            var wallCollider = wall.GetComponent<Collider>();
            if (wallCollider != null)
            {
                DestroySafe(wallCollider);
            }
        }

        var rimRenderer = rim.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader != null)
        {
            if (rimRenderer != null)
            {
                var rimMaterial = new Material(shader);
                rimMaterial.color = new Color(0.14f, 0.15f, 0.18f, 1f);
                if (rimMaterial.HasProperty("_BaseColor"))
                {
                    rimMaterial.SetColor("_BaseColor", new Color(0.14f, 0.15f, 0.18f, 1f));
                }
                if (rimMaterial.HasProperty("_EmissionColor"))
                {
                    rimMaterial.EnableKeyword("_EMISSION");
                    rimMaterial.SetColor("_EmissionColor", new Color(0.2f, 0.45f, 0.6f, 1f) * 0.3f);
                }
                rimRenderer.material = rimMaterial;
            }

            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (!child.name.StartsWith("ShaftWall_"))
                {
                    continue;
                }

                var renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                var wallMaterial = new Material(shader);
                wallMaterial.color = new Color(0.04f, 0.05f, 0.06f, 1f);
                if (wallMaterial.HasProperty("_BaseColor"))
                {
                    wallMaterial.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.06f, 1f));
                }
                if (wallMaterial.HasProperty("_EmissionColor"))
                {
                    wallMaterial.SetColor("_EmissionColor", Color.black);
                }
                renderer.material = wallMaterial;
            }
        }

        var rimCollider = rim.GetComponent<Collider>();
        if (rimCollider != null)
        {
            DestroySafe(rimCollider);
        }

        return root.transform;
    }

    private static Transform EnsureOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            child = obj.transform;
        }

        return child;
    }

    private static void EnsureSkinSelectionLabel(Transform parent)
    {
        var textObj = new GameObject("SkinSelectionLabelText");
        textObj.transform.SetParent(parent);
        textObj.transform.localPosition = new Vector3(8.95f, 0.04f, 5.35f);
        textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        textObj.transform.localScale = Vector3.one;

        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = "Change Skin";
        textMesh.fontSize = 88;
        textMesh.characterSize = 0.075f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
    }

    private static void RemoveLegacyDualPitFloor()
    {
        var legacyDualPitFloor = GameObject.Find("RectangularFloorWithDualPits");
        if (legacyDualPitFloor != null)
        {
            DestroySafe(legacyDualPitFloor);
        }

        // Remove common legacy pit/hole objects near spawn/lobby center.
        var allTransforms = Object.FindObjectsOfType<Transform>();
        foreach (var t in allTransforms)
        {
            if (t == null || t.gameObject == null)
            {
                continue;
            }

            string n = t.gameObject.name.ToLowerInvariant();
            bool isLegacyPit = n == "pita" || n == "pitb" || n == "pitleft" || n == "pitright";
            bool isHoleLike = n.Contains("hole") || n.Contains("pit") || n.Contains("crater");
            if (!isLegacyPit && !isHoleLike)
            {
                continue;
            }

            if (Mathf.Abs(t.position.x) <= ArenaHalfWidth + 1f && Mathf.Abs(t.position.z) <= ArenaHalfDepth + 1f)
            {
                DestroySafe(t.gameObject);
            }
        }
    }

    private static Material CreateFuturisticSkinMaterial(Color baseColor, Color emissionColor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader);
        material.color = baseColor;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.72f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.92f);
        }
        else if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", 0.92f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor * 0.55f);
        }

        return material;
    }

    private static Material CreateDetailedBallSkinMaterial(Color baseColor, Color lineColor, float phaseOffset, float stripeDensity, int patternMode)
    {
        var material = CreateFuturisticSkinMaterial(baseColor, lineColor);
        if (material == null)
        {
            return null;
        }

        var texture = CreateSphereLineTexture(baseColor, lineColor, phaseOffset, stripeDensity, patternMode);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", lineColor * 0.62f);
        }

        return material;
    }

    private static Material CreateDetailedCubeSkinMaterial(Color baseColor, Color lineColor)
    {
        var material = CreateFuturisticSkinMaterial(baseColor, lineColor);
        if (material == null)
        {
            return null;
        }

        var texture = CreateCubeLineTexture(baseColor, lineColor);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", lineColor * 0.58f);
        }

        return material;
    }

    private static Texture2D CreateSphereLineTexture(Color baseColor, Color lineColor, float phaseOffset, float stripeDensity, int patternMode)
    {
        const int width = 512;
        const int height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);

                float tone = 0.82f + (Mathf.Sin((u + phaseOffset) * 6.28318f) * 0.04f) + ((v - 0.5f) * 0.1f);
                Color color = baseColor * tone;
                color.a = 1f;

                float verticalBand = Mathf.Abs(Mathf.Repeat((u + phaseOffset) * stripeDensity, 1f) - 0.5f);
                float verticalLine = Mathf.Clamp01((0.042f - verticalBand) / 0.042f);
                float equatorBand = Mathf.Abs(v - 0.5f);
                float equatorLine = Mathf.Clamp01((0.035f - equatorBand) / 0.035f);
                float diagonalBand = Mathf.Abs(Mathf.Repeat((u * 0.85f) + (v * 0.45f) + phaseOffset, 1f) - 0.5f);
                float diagonalLine = Mathf.Clamp01((0.022f - diagonalBand) / 0.022f);
                float ringBand = Mathf.Abs(Mathf.Repeat((v + (phaseOffset * 0.2f)) * 8f, 1f) - 0.5f);
                float ringLine = Mathf.Clamp01((0.032f - ringBand) / 0.032f);
                float helixBand = Mathf.Abs(Mathf.Repeat((u * 1.35f) - (v * 0.9f) + phaseOffset, 1f) - 0.5f);
                float helixLine = Mathf.Clamp01((0.02f - helixBand) / 0.02f);

                float detailNoise = Mathf.PerlinNoise((u + phaseOffset) * 15f, v * 18f) * 0.08f;
                color *= 0.94f + detailNoise;

                float panelStepX = Mathf.Abs(Mathf.Repeat(u * 8f + phaseOffset, 1f) - 0.5f);
                float panelStepY = Mathf.Abs(Mathf.Repeat(v * 5f, 1f) - 0.5f);
                float panelMask = Mathf.Clamp01((0.11f - Mathf.Min(panelStepX, panelStepY)) / 0.11f) * 0.15f;

                float lineMask;
                switch (patternMode)
                {
                    case 1:
                        lineMask = Mathf.Clamp01((diagonalLine * 1.05f) + (verticalLine * 0.24f) + (ringLine * 0.18f) + panelMask);
                        break;
                    case 2:
                        lineMask = Mathf.Clamp01((ringLine * 1.05f) + (verticalLine * 0.42f) + (diagonalLine * 0.16f) + panelMask);
                        break;
                    case 3:
                        lineMask = Mathf.Clamp01((helixLine * 1.1f) + (equatorLine * 0.48f) + (verticalLine * 0.24f) + panelMask);
                        break;
                    default:
                        lineMask = Mathf.Clamp01((verticalLine * 0.82f) + (equatorLine * 0.95f) + (diagonalLine * 0.34f) + panelMask);
                        break;
                }

                color = Color.Lerp(color, lineColor, lineMask);

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateCubeLineTexture(Color baseColor, Color lineColor)
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);

                Color color = baseColor * (0.78f + (Mathf.Sin(u * 6.28318f) * 0.04f));
                color.a = 1f;

                float gridX = Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.5f);
                float gridY = Mathf.Abs(Mathf.Repeat(v * 6f, 1f) - 0.5f);
                float gridMask = Mathf.Clamp01((0.05f - Mathf.Min(gridX, gridY)) / 0.05f);

                float edgeX = Mathf.Min(u, 1f - u);
                float edgeY = Mathf.Min(v, 1f - v);
                float edgeMask = Mathf.Clamp01((0.08f - Mathf.Min(edgeX, edgeY)) / 0.08f);

                float ledPulse = Mathf.Abs(Mathf.Repeat((u * 8f) + (v * 3f), 1f) - 0.5f);
                float ledMask = Mathf.Clamp01((0.07f - ledPulse) / 0.07f) * 0.45f;

                float lineMask = Mathf.Clamp01((gridMask * 0.65f) + (edgeMask * 0.9f) + ledMask);
                color = Color.Lerp(color, lineColor, lineMask);

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static void EnsureWall(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = localScale;

        var collider = wall.GetComponent<Collider>();
        if (collider != null)
        {
            collider.material = CreateSlipperyNoBounceMaterial();
        }

        var wallRenderer = wall.GetComponent<Renderer>();
        if (wallRenderer != null)
        {
            var wallShader = Shader.Find("Universal Render Pipeline/Lit");
            if (wallShader == null)
            {
                wallShader = Shader.Find("Standard");
            }

            if (wallShader != null)
            {
                var wallMaterial = new Material(wallShader);
                wallMaterial.color = new Color(0.1f, 0.1f, 0.1f, 1f);
                wallRenderer.material = wallMaterial;
            }
        }
    }

    private static void EnsureContainmentCeiling(Transform parent, float wallHeight)
    {
        var ceiling = new GameObject("ContainmentCeiling");
        ceiling.transform.SetParent(parent);
        ceiling.transform.localPosition = new Vector3(0f, wallHeight + 0.35f, 0f);
        ceiling.transform.localRotation = Quaternion.identity;

        var collider = ceiling.AddComponent<BoxCollider>();
        collider.size = new Vector3((ArenaHalfWidth * 2f) + 1.6f, 0.2f, (ArenaHalfDepth * 2f) + 1.6f);
        collider.material = CreateSlipperyNoBounceMaterial();
    }

    private static void EnsureLeftGate(float wallThickness, float gateOpeningWidth)
    {
        var gateRoot = GameObject.Find("LeftGate");
        if (gateRoot == null)
        {
            gateRoot = new GameObject("LeftGate");
        }

        float gateX = -ArenaHalfWidth - (wallThickness * 0.5f) + 0.02f;
        gateRoot.transform.position = new Vector3(gateX, 0f, 0f);
        gateRoot.transform.rotation = Quaternion.identity;

        float panelHeight = 2.2f;
        float panelThickness = wallThickness * 0.95f;
        float leafLength = (gateOpeningWidth * 0.5f) - 0.05f;

        Transform leftPivot = EnsureGatePivot(gateRoot.transform, "GatePivotA", new Vector3(0f, 0f, -gateOpeningWidth * 0.5f));
        Transform rightPivot = EnsureGatePivot(gateRoot.transform, "GatePivotB", new Vector3(0f, 0f, gateOpeningWidth * 0.5f));

        var leftLeaf = EnsureGatePart(leftPivot, "GateLeafA", new Vector3(0f, panelHeight * 0.5f, leafLength * 0.5f), new Vector3(panelThickness, panelHeight, leafLength), Color.white);
        var rightLeaf = EnsureGatePart(rightPivot, "GateLeafB", new Vector3(0f, panelHeight * 0.5f, -leafLength * 0.5f), new Vector3(panelThickness, panelHeight, leafLength), Color.white);

        var triggerZone = gateRoot.GetComponent<BoxCollider>();
        if (triggerZone == null)
        {
            triggerZone = gateRoot.AddComponent<BoxCollider>();
        }
        triggerZone.isTrigger = true;
        triggerZone.center = new Vector3(0f, 1.1f, 0f);
        triggerZone.size = new Vector3(2.2f, 2.6f, gateOpeningWidth + 1.4f);

        var controller = gateRoot.GetComponent<GateController>();
        if (controller == null)
        {
            controller = gateRoot.AddComponent<GateController>();
        }
        controller.leftPivot = leftPivot;
        controller.rightPivot = rightPivot;
        controller.leftLeafCollider = leftLeaf.GetComponent<Collider>();
        controller.rightLeafCollider = rightLeaf.GetComponent<Collider>();
    }

    private static void EnsureOutsideGateFloor()
    {
        var outsideFloor = GameObject.Find("GateOutsideFloor");
        if (outsideFloor == null)
        {
            outsideFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outsideFloor.name = "GateOutsideFloor";
        }

        outsideFloor.transform.position = new Vector3(-ArenaHalfWidth - 3f, -0.1f, 0f);
        outsideFloor.transform.rotation = Quaternion.identity;
        outsideFloor.transform.localScale = new Vector3(6f, 0.2f, 5f);

        var renderer = outsideFloor.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = Color.black;
                renderer.material = material;
            }
        }
    }

    private static void EnsureDifficultyPaths()
    {
        var root = GameObject.Find("DifficultyPaths");
        if (root == null)
        {
            root = new GameObject("DifficultyPaths");
        }
        else if (PreserveExistingSceneLayout && root.transform.childCount > 0)
        {
            return;
        }
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        EnsureNaturalIsland(root.transform, "MainDifficultyIsland", new Vector3(-23.8f, -0.22f, 0f), new Vector3(16.5f, 0.55f, 20.5f), true);
        EnsureNaturalIsland(root.transform, "EasyOuterIsland", new Vector3(-31.6f, -0.16f, -8.6f), new Vector3(7.2f, 0.42f, 6.4f), true);
        EnsureNaturalIsland(root.transform, "MediumOuterIsland", new Vector3(-32.4f, -0.16f, 0f), new Vector3(7.8f, 0.42f, 6.9f), true);
        EnsureNaturalIsland(root.transform, "HardOuterIsland", new Vector3(-31.6f, -0.16f, 8.6f), new Vector3(7.2f, 0.42f, 6.4f), true);

        EnsureNaturalDifficultyPath(
            root.transform,
            "PathEasy",
            new Vector3(-18.2f, 0.06f, -5.4f),
            new Vector3(-24.6f, 0.09f, -8.6f),
            new Vector3(-30f, 0.06f, -8.6f),
            "Easy",
            new Color(0.63f, 0.95f, 0.76f, 1f));
        EnsureNaturalDifficultyPath(
            root.transform,
            "PathMedium",
            new Vector3(-17.8f, 0.06f, 0f),
            new Vector3(-24.8f, 0.09f, 0f),
            new Vector3(-30f, 0.06f, 0f),
            "Medium",
            new Color(0.58f, 0.84f, 0.97f, 1f));
        EnsureNaturalDifficultyPath(
            root.transform,
            "PathHard",
            new Vector3(-18.2f, 0.06f, 5.4f),
            new Vector3(-24.6f, 0.09f, 8.6f),
            new Vector3(-30f, 0.06f, 8.6f),
            "Hard",
            new Color(1f, 0.76f, 0.66f, 1f));

        RemoveIfExists(root.transform, "EasyLock");
        RemoveIfExists(root.transform, "MediumLock");
        RemoveIfExists(root.transform, "HardLock");

        EnsurePathLabel(root.transform, "LabelEasy", "Easy", new Vector3(-23.9f, 0.42f, -11.3f), new Color(0.85f, 1f, 0.9f, 1f));
        EnsurePathLabel(root.transform, "LabelMedium", "Medium", new Vector3(-24.2f, 0.42f, 0f), new Color(0.86f, 0.95f, 1f, 1f));
        EnsurePathLabel(root.transform, "LabelHard", "Hard", new Vector3(-23.9f, 0.42f, 11.3f), new Color(1f, 0.86f, 0.82f, 1f));
    }

    private static void EnsurePostGateApproach()
    {
        var root = GameObject.Find("PostGateApproach");
        if (root == null)
        {
            root = new GameObject("PostGateApproach");
        }
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        EnsureTrackPartWorld(
            root.transform,
            "BridgeWater",
            new Vector3(-17.8f, -0.38f, 0f),
            Quaternion.identity,
            new Vector3(16f, 0.08f, 18f),
            new Color(0.19f, 0.52f, 0.73f, 1f),
            null
        );
        EnsureNaturalIsland(root.transform, "ArrivalIsland", new Vector3(-12.8f, -0.18f, 0f), new Vector3(8.4f, 0.4f, 11f), true);
        EnsureWoodBridge(root.transform, "MainApproachBase", new Vector3(-12.8f, 0.08f, 0f), new Vector3(-18.4f, 0.08f, 0f), 3.4f);
        RemoveIfExists(root.transform, "MainApproachGlow");

        RemoveIfExists(root.transform, "AttachMediumBase");
        RemoveIfExists(root.transform, "AttachMediumGlow");
        RemoveIfExists(root.transform, "AttachEasyBase");
        RemoveIfExists(root.transform, "AttachEasyGlow");
        RemoveIfExists(root.transform, "AttachHardBase");
        RemoveIfExists(root.transform, "AttachHardGlow");
        RemoveIfExists(root.transform, "ApproachLeftBase");
        RemoveIfExists(root.transform, "ApproachMidBase");
        RemoveIfExists(root.transform, "ApproachRightBase");
        RemoveIfExists(root.transform, "ApproachLeftGlow");
        RemoveIfExists(root.transform, "ApproachMidGlow");
        RemoveIfExists(root.transform, "ApproachRightGlow");
        RemoveIfExists(root.transform, "LaneDividerLeft");
        RemoveIfExists(root.transform, "LaneDividerRight");

    }

    private static void EnsureSpiralTubesAndPlatforms()
    {
        var root = GameObject.Find("TubeSystem");
        if (root == null)
        {
            root = new GameObject("TubeSystem");
        }
        else if (PreserveExistingSceneLayout && root.transform.childCount > 0)
        {
            return;
        }

        var pathGray = new Color(0.24f, 0.24f, 0.24f, 1f);

        // Keep generous spacing between tubes and place them far from start area.
        EnsureTubeLane(
            root.transform,
            "EasyTubeLane",
            new Vector3(-35f, 0f, -11.2f),
            new Vector3(-30f, 0f, -8.6f),
            Vector3.back,
            pathGray,
            8.5f
        );
        EnsureTubeLane(
            root.transform,
            "MediumTubeLane",
            new Vector3(-35f, 0f, 0f),
            new Vector3(-30f, 0f, 0f),
            Vector3.left,
            pathGray,
            8.7f
        );
        EnsureTubeLane(
            root.transform,
            "HardTubeLane",
            new Vector3(-35f, 0f, 11.2f),
            new Vector3(-30f, 0f, 8.6f),
            Vector3.forward,
            pathGray,
            8.9f
        );
    }

    private static void EnsureNaturalDifficultyPath(
        Transform parent,
        string pathName,
        Vector3 branchStart,
        Vector3 padCenter,
        Vector3 tubeEntry,
        string label,
        Color accentColor)
    {
        Transform existing = parent.Find(pathName);
        GameObject pathRoot;
        if (existing == null)
        {
            pathRoot = new GameObject(pathName);
            pathRoot.transform.SetParent(parent);
        }
        else
        {
            pathRoot = existing.gameObject;
        }

        pathRoot.transform.localPosition = Vector3.zero;
        pathRoot.transform.localRotation = Quaternion.identity;
        pathRoot.transform.localScale = Vector3.one;
        ClearChildren(pathRoot.transform);

        EnsureWoodBridge(pathRoot.transform, "BranchBridge", branchStart, tubeEntry, 2.5f);
        EnsureTrackPartWorld(
            pathRoot.transform,
            "DifficultyPad",
            padCenter,
            Quaternion.identity,
            new Vector3(4.2f, 0.22f, 3f),
            new Color(
                Mathf.Clamp01(GrassHighlightColor.r * 0.95f),
                Mathf.Clamp01(GrassHighlightColor.g * 0.95f),
                Mathf.Clamp01(GrassHighlightColor.b * 0.95f),
                1f),
            null
        );
        EnsureTrackPartWorld(
            pathRoot.transform,
            "DifficultyPadTrim",
            padCenter + new Vector3(0f, -0.14f, 0f),
            Quaternion.identity,
            new Vector3(4.5f, 0.14f, 3.3f),
            SoilColor,
            null
        );
        EnsureTrackPartWorld(
            pathRoot.transform,
            "DifficultyPadAccent",
            padCenter + new Vector3(0f, 0.02f, 0f),
            Quaternion.identity,
            new Vector3(3.55f, 0.04f, 2.35f),
            accentColor,
            accentColor * 0.12f
        );
        EnsureTrackPartWorld(
            pathRoot.transform,
            "BridgeJoinPlank",
            Vector3.Lerp(padCenter, tubeEntry, 0.5f) + new Vector3(0f, 0.1f, 0f),
            Quaternion.LookRotation((tubeEntry - padCenter).normalized, Vector3.up),
            new Vector3(1.65f, 0.08f, Vector3.Distance(padCenter, tubeEntry) + 0.15f),
            WoodDarkColor,
            null
        );

        EnsurePathLabel(pathRoot.transform, "DifficultyLabel", label, padCenter + new Vector3(0f, 0.26f, 0f), accentColor);
    }

    private static void EnsureNaturalIsland(Transform parent, string name, Vector3 center, Vector3 topScale, bool decorate)
    {
        Transform existing = parent.Find(name);
        GameObject islandRoot;
        if (existing == null)
        {
            islandRoot = new GameObject(name);
            islandRoot.transform.SetParent(parent);
        }
        else
        {
            islandRoot = existing.gameObject;
        }

        islandRoot.transform.localPosition = Vector3.zero;
        islandRoot.transform.localRotation = Quaternion.identity;
        islandRoot.transform.localScale = Vector3.one;
        ClearChildren(islandRoot.transform);

        EnsureTrackPartWorld(
            islandRoot.transform,
            "SoilBase",
            center + new Vector3(0f, -0.36f, 0f),
            Quaternion.identity,
            new Vector3(topScale.x * 0.92f, topScale.y * 1.45f, topScale.z * 0.92f),
            SoilShadowColor,
            null
        );
        EnsureTrackPartWorld(
            islandRoot.transform,
            "SoilMid",
            center + new Vector3(0f, -0.18f, 0f),
            Quaternion.identity,
            new Vector3(topScale.x * 0.98f, topScale.y * 0.9f, topScale.z * 0.98f),
            SoilColor,
            null
        );
        EnsureTrackPartWorld(
            islandRoot.transform,
            "GrassTop",
            center + new Vector3(0f, 0.02f, 0f),
            Quaternion.identity,
            topScale,
            GrassTopColor,
            null
        );
        EnsureTrackPartWorld(
            islandRoot.transform,
            "GrassHighlight",
            center + new Vector3(0f, 0.17f, 0f),
            Quaternion.identity,
            new Vector3(topScale.x * 0.88f, 0.12f, topScale.z * 0.88f),
            GrassHighlightColor,
            null
        );

        if (!decorate)
        {
            return;
        }

        float radiusX = topScale.x * 0.34f;
        float radiusZ = topScale.z * 0.34f;
        EnsurePlacedPrefab(islandRoot.transform, "TreeA", TreePrefabPath, center + new Vector3(-radiusX, 0.22f, -radiusZ), Vector3.one * 0.72f, new Vector3(0f, 30f, 0f));
        EnsurePlacedPrefab(islandRoot.transform, "TreeB", TreePrefabPath, center + new Vector3(radiusX, 0.22f, radiusZ), Vector3.one * 0.64f, new Vector3(0f, -28f, 0f));
        EnsurePlacedPrefab(islandRoot.transform, "RockA", RockPrefabPath, center + new Vector3(radiusX * 0.82f, 0.08f, -radiusZ * 0.75f), Vector3.one * 0.8f, new Vector3(0f, 18f, 0f));
        EnsurePlacedPrefab(islandRoot.transform, "RockB", RockPrefabPath, center + new Vector3(-radiusX * 0.75f, 0.08f, radiusZ * 0.72f), Vector3.one * 0.65f, new Vector3(0f, -22f, 0f));
    }

    private static void EnsureWoodBridge(Transform parent, string name, Vector3 start, Vector3 end, float width)
    {
        Transform existing = parent.Find(name);
        GameObject bridgeRoot;
        if (existing == null)
        {
            bridgeRoot = new GameObject(name);
            bridgeRoot.transform.SetParent(parent);
        }
        else
        {
            bridgeRoot = existing.gameObject;
        }

        bridgeRoot.transform.localPosition = Vector3.zero;
        bridgeRoot.transform.localRotation = Quaternion.identity;
        bridgeRoot.transform.localScale = Vector3.one;
        ClearChildren(bridgeRoot.transform);

        Vector3 delta = end - start;
        float length = delta.magnitude;
        Vector3 direction = length > 0.001f ? delta.normalized : Vector3.left;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 mid = start + (delta * 0.5f);
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;

        EnsureTrackPartWorld(
            bridgeRoot.transform,
            "BridgeCollider",
            mid + new Vector3(0f, -0.05f, 0f),
            rotation,
            new Vector3(width, 0.2f, length + 0.25f),
            WoodDarkColor,
            null
        );

        int plankCount = Mathf.Max(4, Mathf.CeilToInt(length / 1.15f));
        float plankLength = length / plankCount;
        for (int i = 0; i < plankCount; i++)
        {
            float t = (i + 0.5f) / plankCount;
            Vector3 plankCenter = Vector3.Lerp(start, end, t);
            EnsureTrackPartWorld(
                bridgeRoot.transform,
                "Plank_" + i,
                plankCenter + new Vector3(0f, 0.06f, 0f),
                rotation,
                new Vector3(width * 0.92f, 0.1f, plankLength - 0.08f),
                i % 2 == 0 ? WoodColor : WoodDarkColor,
                null
            );

            if (i == 0 || i == plankCount - 1 || i % 2 == 0)
            {
                Vector3 postCenter = plankCenter + (right * ((width * 0.5f) - 0.18f));
                EnsureTrackPartWorld(
                    bridgeRoot.transform,
                    "PostL_" + i,
                    postCenter + new Vector3(0f, 0.42f, 0f),
                    Quaternion.identity,
                    new Vector3(0.12f, 0.7f, 0.12f),
                    WoodDarkColor,
                    null
                );
                EnsureTrackPartWorld(
                    bridgeRoot.transform,
                    "PostR_" + i,
                    postCenter - (right * ((width - 0.36f))) + new Vector3(0f, 0.42f, 0f),
                    Quaternion.identity,
                    new Vector3(0.12f, 0.7f, 0.12f),
                    WoodDarkColor,
                    null
                );
            }
        }
    }

    private static void EnsurePlacedPrefab(Transform parent, string name, string prefabPath, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles)
    {
        Transform existing = parent.Find(name);
        GameObject instance = existing != null ? existing.gameObject : null;

        if (instance == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            }

            if (instance == null)
            {
                instance = new GameObject(name);
            }

            instance.name = name;
            instance.transform.SetParent(parent);
        }

        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
        instance.transform.localScale = localScale;
    }

    private static void EnsureTubeLane(Transform parent, string laneName, Vector3 tubeBaseCenter, Vector3 pathExitPoint, Vector3 topContinuationDirection, Color accentColor, float platformHeight)
    {
        Transform existing = parent.Find(laneName);
        GameObject laneRoot;
        if (existing == null)
        {
            laneRoot = new GameObject(laneName);
            laneRoot.transform.SetParent(parent);
        }
        else
        {
            laneRoot = existing.gameObject;
        }

        laneRoot.transform.localPosition = Vector3.zero;
        laneRoot.transform.localRotation = Quaternion.identity;
        laneRoot.transform.localScale = Vector3.one;

        RemoveLiftEntranceRoom(laneRoot.transform);
        EnsureTubeConnector(laneRoot.transform, pathExitPoint, tubeBaseCenter, accentColor);
        Vector3 entranceDirection = (pathExitPoint - tubeBaseCenter).normalized; // from tube center to entrance
        Vector3 forwardDirection = topContinuationDirection.sqrMagnitude > 0.001f
            ? topContinuationDirection.normalized
            : (tubeBaseCenter - pathExitPoint).normalized;
        Vector3 topPlatformCenter = tubeBaseCenter + (forwardDirection * 4.9f) + (Vector3.up * platformHeight);

        EnsureSpiralTube(laneRoot.transform, "Tube", tubeBaseCenter, entranceDirection, forwardDirection, accentColor, platformHeight);
        EnsureUpperPlatform(laneRoot.transform, "UpperPlatform", topPlatformCenter, forwardDirection, accentColor);

        if (laneName == "EasyTubeLane")
        {
            EnsureEasyTopChallenge(laneRoot.transform, topPlatformCenter, forwardDirection, accentColor);
        }
        else if (laneName == "MediumTubeLane")
        {
            EnsureMediumTopChallenge(laneRoot.transform, topPlatformCenter, forwardDirection, accentColor);
        }
        else if (laneName == "HardTubeLane")
        {
            EnsureHardTopChallenge(laneRoot.transform, topPlatformCenter, forwardDirection, accentColor);
        }
    }

    private static void RemoveLiftEntranceRoom(Transform parent)
    {
        RemoveIfExists(parent, "EntranceFloor");
        RemoveIfExists(parent, "EntranceRoof");
        RemoveIfExists(parent, "EntranceBackWall");
        RemoveIfExists(parent, "EntranceLeftWall");
        RemoveIfExists(parent, "EntranceRightWall");
        RemoveIfExists(parent, "EntranceGlow");
    }

    private static void RemoveIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            DestroySafe(child.gameObject);
        }
    }

    private static void EnsureTubeConnector(Transform parent, Vector3 start, Vector3 end, Color accentColor)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        Vector3 mid = start + (delta * 0.5f);
        Quaternion rot = length > 0.001f ? Quaternion.LookRotation(delta.normalized, Vector3.up) : Quaternion.identity;

        EnsureTrackPartWorld(
            parent,
            "ConnectorBase",
            new Vector3(mid.x, -0.08f, mid.z),
            rot,
            new Vector3(2.6f, 0.2f, length + 0.15f),
            WoodColor,
            null
        );

        EnsureTrackPartWorld(
            parent,
            "ConnectorGlow",
            new Vector3(mid.x, 0.02f, mid.z),
            rot,
            new Vector3(0.12f, 0.02f, length + 0.1f),
            SoilColor,
            null
        );
    }

    private static void EnsureSpiralTube(Transform parent, string name, Vector3 center, Vector3 entranceDirection, Vector3 exitDirection, Color accentColor, float topHeight)
    {
        Transform existing = parent.Find(name);
        GameObject tubeRoot;
        if (existing == null)
        {
            tubeRoot = new GameObject(name);
            tubeRoot.transform.SetParent(parent);
        }
        else
        {
            tubeRoot = existing.gameObject;
        }

        tubeRoot.transform.localPosition = Vector3.zero;
        tubeRoot.transform.localRotation = Quaternion.identity;
        tubeRoot.transform.localScale = Vector3.one;
        ClearChildren(tubeRoot.transform);

        const float tubeRadius = 1.7f;
        const float wallThickness = 0.12f;
        const int wallSegments = 30;
        const float entranceOpeningHalfAngle = 88f; // bottom entrance opening
        const float entranceOpenHeight = 4.15f;
        float wallHeight = Mathf.Max(7.95f, topHeight - 0.18f);
        float entranceAngle = Mathf.Atan2(entranceDirection.z, entranceDirection.x) * Mathf.Rad2Deg;

        for (int i = 0; i < wallSegments; i++)
        {
            float a = (i / (float)wallSegments) * Mathf.PI * 2f;
            float segmentAngle = a * Mathf.Rad2Deg;
            float deltaToEntrance = Mathf.Abs(Mathf.DeltaAngle(segmentAngle, entranceAngle));
            float x = center.x + (Mathf.Cos(a) * tubeRadius);
            float z = center.z + (Mathf.Sin(a) * tubeRadius);
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Quaternion rot = Quaternion.LookRotation(radial, Vector3.up);

            bool inEntranceWindow = deltaToEntrance < entranceOpeningHalfAngle;

            if (inEntranceWindow)
            {
                float upperHeight = wallHeight - entranceOpenHeight;
                if (upperHeight > 0.05f)
                {
                    EnsureTrackPartWorld(
                        tubeRoot.transform,
                        "TubeWall_" + i + "_Upper",
                        new Vector3(x, entranceOpenHeight + (upperHeight * 0.5f), z),
                        rot,
                        new Vector3(wallThickness, upperHeight, 0.5f),
                        new Color(0.15f, 0.18f, 0.22f, 1f),
                        null
                    );
                }
                continue;
            }

            EnsureTrackPartWorld(
                tubeRoot.transform,
                "TubeWall_" + i,
                new Vector3(x, wallHeight * 0.5f, z),
                rot,
                new Vector3(wallThickness, wallHeight, 0.5f),
                new Color(0.15f, 0.18f, 0.22f, 1f),
                null
            );
        }

        ApplySlipperyMaterialToTubeWalls(tubeRoot.transform);

        Transform oldTopCap = tubeRoot.transform.Find("TubeTopCap");
        if (oldTopCap != null)
        {
            DestroySafe(oldTopCap.gameObject);
        }

        EnsureTubeLiftPlatform(tubeRoot.transform, center, topHeight, accentColor);
    }

    private static void ApplySlipperyMaterialToTubeWalls(Transform tubeRoot)
    {
        var slippery = CreateSlipperyNoBounceMaterial();
        for (int i = 0; i < tubeRoot.childCount; i++)
        {
            Transform child = tubeRoot.GetChild(i);
            if (!child.name.StartsWith("TubeWall_"))
            {
                continue;
            }

            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                collider.material = slippery;
            }
        }
    }

    private static void EnsureTubeLiftPlatform(Transform parent, Vector3 center, float topHeight, Color accentColor)
    {
        Transform existing = parent.Find("LiftPlatform");
        GameObject platform;
        if (existing == null)
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "LiftPlatform";
            platform.transform.SetParent(parent);
        }
        else
        {
            platform = existing.gameObject;
        }

        platform.transform.localPosition = new Vector3(center.x, 0.12f, center.z);
        platform.transform.localRotation = Quaternion.identity;
        platform.transform.localScale = new Vector3(3.6f, 0.14f, 3.6f);

        var renderer = platform.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = new Color(0.16f, 0.18f, 0.22f, 1f);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", accentColor * 1.25f);
                }
                renderer.material = material;
            }
        }

        var rb = platform.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = platform.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var mover = platform.GetComponent<TubeLiftPlatform>();
        if (mover == null)
        {
            mover = platform.AddComponent<TubeLiftPlatform>();
        }
        mover.bottomY = 0.12f;
        mover.topY = topHeight + 1.05f;
        mover.riseSpeed = 3.8f;
        mover.descendSpeed = 7.5f;
        mover.chargeSeconds = 1f;
        mover.topHoldSeconds = 2f;
    }

    private static void EnsureUpperPlatform(Transform parent, string name, Vector3 center, Color accentColor)
    {
        EnsureUpperPlatform(parent, name, center, Vector3.left, accentColor);
    }

    private static void EnsureUpperPlatform(Transform parent, string name, Vector3 center, Vector3 forwardDirection, Color accentColor)
    {
        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            forwardDirection = Vector3.left;
        }
        forwardDirection.Normalize();

        EnsureNaturalIsland(parent, name, center + new Vector3(0f, -0.12f, 0f), new Vector3(5.9f, 0.28f, 5.9f), false);
        EnsureWoodBridge(parent, name + "_ForwardPath", center + (forwardDirection * 2.25f) + new Vector3(0f, 0.06f, 0f), center + (forwardDirection * 6.9f) + new Vector3(0f, 0.06f, 0f), 2.4f);
        RemoveIfExists(parent, name + "_GlowA");
        RemoveIfExists(parent, name + "_GlowB");
        RemoveIfExists(parent, name + "_ForwardGlow");
        RemoveIfExists(parent, name + "_ForwardRailL");
        RemoveIfExists(parent, name + "_ForwardRailR");
    }

    private static void EnsureEasyTopChallenge(Transform parent, Vector3 topCenter, Vector3 forwardDirection, Color accentColor)
    {
        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            forwardDirection = Vector3.left;
        }
        forwardDirection.Normalize();
        Quaternion rot = Quaternion.LookRotation(forwardDirection, Vector3.up);

        // Extend Easy top path slightly.
        Vector3 extensionCenter = topCenter + (forwardDirection * 11.2f) + new Vector3(0f, -0.08f, 0f);
        EnsureTrackPartWorld(
            parent,
            "EasyTopExtension",
            extensionCenter,
            rot,
            new Vector3(2.6f, 0.2f, 4f),
            WoodColor,
            null
        );
        RemoveIfExists(parent, "EasyTopExtensionGlow");

        // End rectangle with distinct color for the question trigger.
        Vector3 padCenter = topCenter + (forwardDirection * 14.2f);
        EnsureTrackPartWorld(
            parent,
            "CppQuestionPad",
            padCenter + new Vector3(0f, -0.02f, 0f),
            rot,
            new Vector3(3.2f, 0.2f, 2.4f),
            new Color(0.93f, 0.82f, 0.46f, 1f),
            null
        );

        Transform triggerT = parent.Find("CppQuestionTrigger");
        GameObject triggerObject;
        if (triggerT == null)
        {
            triggerObject = new GameObject("CppQuestionTrigger");
            triggerObject.transform.SetParent(parent);
        }
        else
        {
            triggerObject = triggerT.gameObject;
        }

        triggerObject.transform.position = padCenter + new Vector3(0f, 0.5f, 0f);
        triggerObject.transform.rotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;

        var trigger = triggerObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = triggerObject.AddComponent<BoxCollider>();
        }
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.2f, 1.2f, 2.4f);
        trigger.center = Vector3.zero;

        var questionTrigger = triggerObject.GetComponent<CppQuestionTrigger>();
        if (questionTrigger == null)
        {
            questionTrigger = triggerObject.AddComponent<CppQuestionTrigger>();
        }
        questionTrigger.focusPoint = triggerObject.transform;
        questionTrigger.SetQuestionStage(1);
        questionTrigger.SetQuestionSideOffset(6.8f);
        questionTrigger.SetCodeVerticalOffset(0.5f);
        questionTrigger.SetLayoutQuestionBelowCode(false);
        questionTrigger.SetSpawnNextQuestionOnCorrect(true);
        questionTrigger.SetQuestionContent(
            "What is the\nvalue of c?",
            "int a = 4;\nint b = 2;\nint c = a * b;");
        EnsureQuestionTextObjects(triggerObject.transform);

        EnsureEasyAnswerPlatforms(parent, padCenter, forwardDirection, questionTrigger);
    }

    private static void EnsureMediumTopChallenge(Transform parent, Vector3 topCenter, Vector3 forwardDirection, Color accentColor)
    {
        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            forwardDirection = Vector3.left;
        }
        forwardDirection.Normalize();
        Quaternion rot = Quaternion.LookRotation(forwardDirection, Vector3.up);

        RemoveIfExists(parent, "MediumTopExtension");
        RemoveIfExists(parent, "MediumTopExtensionGlow");
        RemoveIfExists(parent, "MediumLiftArrivalTrigger");
        RemoveIfExists(parent, "MediumArrivalTarget");
        RemoveIfExists(parent, "MediumQuestionTrigger");
        RemoveIfExists(parent, "MediumAnswersRoot");
        RemoveIfExists(parent, "MediumQuestionToAnswersBridge");
        RemoveIfExists(parent, "MediumQuestionToAnswersBridgeGlow");
        RemoveIfExists(parent, "MediumSecondQuestionArea");
        RemoveIfExists(parent, "MediumThirdQuestionArea");
        RemoveIfExists(parent, "MediumFourthQuestionArea");
        RemoveIfExists(parent, "MediumQuestionPad");
    }

    private static void EnsureHardTopChallenge(Transform parent, Vector3 topCenter, Vector3 forwardDirection, Color accentColor)
    {
        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            forwardDirection = Vector3.left;
        }
        forwardDirection.Normalize();
        Quaternion rot = Quaternion.LookRotation(forwardDirection, Vector3.up);

        RemoveIfExists(parent, "HardTopExtension");
        RemoveIfExists(parent, "HardTopExtensionGlow");
        RemoveIfExists(parent, "HardLiftArrivalTrigger");
        RemoveIfExists(parent, "HardArrivalTarget");
        RemoveIfExists(parent, "HardQuestionTrigger");
        RemoveIfExists(parent, "HardAnswersRoot");
        RemoveIfExists(parent, "HardQuestionToAnswersBridge");
        RemoveIfExists(parent, "HardQuestionToAnswersBridgeGlow");
        RemoveIfExists(parent, "HardSecondQuestionArea");
        RemoveIfExists(parent, "HardThirdQuestionArea");
        RemoveIfExists(parent, "HardFourthQuestionArea");
        RemoveIfExists(parent, "HardQuestionPad");
    }

    private static void EnsureEasyAnswerPlatforms(Transform parent, Vector3 origin, Vector3 forwardDirection, CppQuestionTrigger trigger)
    {
        Transform rootT = parent.Find("CppAnswersRoot");
        GameObject root;
        if (rootT == null)
        {
            root = new GameObject("CppAnswersRoot");
            root.transform.SetParent(parent);
        }
        else
        {
            root = rootT.gameObject;
        }

        root.transform.position = origin + (forwardDirection.normalized * 4.6f);
        root.transform.rotation = Quaternion.LookRotation(forwardDirection.normalized, Vector3.up);
        root.transform.localScale = Vector3.one;
        ClearChildren(root.transform);

        // Simple connector floor so the ball rolls smoothly between question and answers.
        EnsureTrackPartWorld(
            root.transform,
            "AnswersBase",
            new Vector3(0f, -0.05f, 0f),
            Quaternion.identity,
            new Vector3(13.2f, 0.2f, 7f),
            GrassTopColor,
            null
        );
        RemoveIfExists(root.transform, "AnswersBaseGlow");

        EnsureTrackPartWorld(
            parent,
            "QuestionToAnswersBridge",
            origin + (forwardDirection.normalized * 2.3f) + new Vector3(0f, -0.05f, 0f),
            Quaternion.LookRotation(forwardDirection.normalized, Vector3.up),
            new Vector3(3.2f, 0.2f, 5.2f),
            WoodColor,
            null
        );
        RemoveIfExists(parent, "QuestionToAnswersBridgeGlow");

        EnsureAnswerPad(root.transform, "AnswerPadA", new Vector3(-5f, 0f, 0f), new Color(0.16f, 0.62f, 0.95f, 1f), "8", true, trigger);
        EnsureAnswerPad(root.transform, "AnswerPadB", Vector3.zero, new Color(0.95f, 0.36f, 0.26f, 1f), "6", false, trigger);
        EnsureAnswerPad(root.transform, "AnswerPadC", new Vector3(5f, 0f, 0f), new Color(0.58f, 0.46f, 0.92f, 1f), "10", false, trigger);
        EnsureTutorialPath(root.transform, trigger, 1);
        Transform firstPostPath = EnsurePostAnswerPath(root.transform);

        Transform secondAnswersRoot;
        CppQuestionTrigger secondTrigger = EnsureQuestionStageArea(
            root.transform,
            "SecondQuestionArea",
            "NextQuestionPlatform",
            "NextQuestionTrigger",
            "NextQuestionBridge",
            "NextQuestionAnswersRoot",
            "What is the value\nafter the code runs?",
            "int x = 5;\nif (x > 3) {\n    x = x + 2;\n}",
            new[] { "7", "5", "3" },
            0,
            2,
            true,
            0.5f,
            false,
            out secondAnswersRoot);

        Transform thirdAnswersRoot;
        CppQuestionTrigger thirdTrigger = EnsureQuestionStageArea(
            secondAnswersRoot,
            "ThirdQuestionArea",
            "ThirdQuestionPlatform",
            "ThirdQuestionTrigger",
            "ThirdQuestionBridge",
            "ThirdQuestionAnswersRoot",
            "What will this\ncode print?",
            "int a = 5;\nif (a > 3)\n{\ncout << 10;\n}\nelse\n{\ncout << 2;\n}",
            new[] { "5", "10", "2" },
            1,
            3,
            true,
            0.9f,
            false,
            out thirdAnswersRoot);

        Transform fourthAnswersRoot;
        CppQuestionTrigger fourthTrigger = EnsureQuestionStageArea(
            thirdAnswersRoot,
            "FourthQuestionArea",
            "FourthQuestionPlatform",
            "FourthQuestionTrigger",
            "FourthQuestionBridge",
            "FourthQuestionAnswersRoot",
            "What will be printed\nby this code?",
            "int x = 4;\nint y = 3;\n\nif (x + y > 6)\n{\n cout << x * y;\n}\nelse\n{\n cout << x - y;\n}",
            new[] { "12", "7", "1" },
            0,
            4,
            true,
            0.8f,
            false,
            out fourthAnswersRoot);

        Transform fifthAnswersRoot;
        CppQuestionTrigger fifthTrigger = EnsureQuestionStageArea(
            fourthAnswersRoot,
            "FifthQuestionArea",
            "FifthQuestionPlatform",
            "FifthQuestionTrigger",
            "FifthQuestionBridge",
            "FifthQuestionAnswersRoot",
            "What will this\ncode print?",
            "int a = 5;\nint b = 2;\n\nif (a % b == 1)\n{\n cout << 3;\n}\nelse\n{\n cout << 0;\n}",
            new[] { "3", "1", "0" },
            0,
            5,
            true,
            1.1f,
            false,
            out fifthAnswersRoot);

        Transform sixthAnswersRoot;
        CppQuestionTrigger sixthTrigger = EnsureQuestionStageArea(
            fifthAnswersRoot,
            "SixthQuestionArea",
            "SixthQuestionPlatform",
            "SixthQuestionTrigger",
            "SixthQuestionBridge",
            "SixthQuestionAnswersRoot",
            "What is the\nvalue of x?",
            "int x = 10;\nx += 5;\nx -= 2;",
            new[] { "13", "15", "10" },
            0,
            6,
            true,
            0.8f,
            false,
            out sixthAnswersRoot);

        Transform seventhAnswersRoot;
        CppQuestionTrigger seventhTrigger = EnsureQuestionStageArea(
            sixthAnswersRoot,
            "SeventhQuestionArea",
            "SeventhQuestionPlatform",
            "SeventhQuestionTrigger",
            "SeventhQuestionBridge",
            "SeventhQuestionAnswersRoot",
            "What will this\ncode print?",
            "for(int i=0; i<3; i++) {\n cout << i;\n}",
            new[] { "123", "012", "0123" },
            1,
            7,
            true,
            1.1f,
            false,
            out seventhAnswersRoot);

        Transform eighthAnswersRoot;
        CppQuestionTrigger eighthTrigger = EnsureQuestionStageArea(
            seventhAnswersRoot,
            "EighthQuestionArea",
            "EighthQuestionPlatform",
            "EighthQuestionTrigger",
            "EighthQuestionBridge",
            "EighthQuestionAnswersRoot",
            "What does this\nreturn?",
            "bool a = true;\nbool b = false;\nreturn a && b;",
            new[] { "true", "false", "1" },
            1,
            8,
            false,
            0.8f,
            false,
            out eighthAnswersRoot);

        trigger.SetPostAnswerPath(firstPostPath);
        trigger.SetNextQuestionRoot(secondTrigger.transform.parent);
        secondTrigger.SetNextQuestionRoot(thirdTrigger.transform.parent);
        thirdTrigger.SetNextQuestionRoot(fourthTrigger.transform.parent);
        fourthTrigger.SetNextQuestionRoot(fifthTrigger.transform.parent);
        fifthTrigger.SetNextQuestionRoot(sixthTrigger.transform.parent);
        sixthTrigger.SetNextQuestionRoot(seventhTrigger.transform.parent);
        seventhTrigger.SetNextQuestionRoot(eighthTrigger.transform.parent);
        eighthTrigger.SetNextQuestionRoot(null);
        eighthTrigger.SetSpawnNextQuestionOnCorrect(false);

        root.SetActive(false);
        trigger.answersRoot = root.transform;
    }

    private static CppQuestionTrigger EnsureQuestionStageArea(
        Transform parent,
        string areaName,
        string platformName,
        string triggerName,
        string bridgeName,
        string answersName,
        string questionText,
        string codeText,
        string[] answers,
        int correctIndex,
        int stage,
        bool spawnNext,
        float codeVerticalOffset,
        bool questionBelowCode,
        out Transform answersRootOut)
    {
        Transform areaTransform = parent.Find(areaName);
        GameObject areaObject;
        if (areaTransform == null)
        {
            areaObject = new GameObject(areaName);
            areaObject.transform.SetParent(parent);
        }
        else
        {
            areaObject = areaTransform.gameObject;
        }

        areaObject.transform.localPosition = Vector3.zero;
        areaObject.transform.localRotation = Quaternion.identity;
        areaObject.transform.localScale = Vector3.one;
        ClearChildren(areaObject.transform);

        EnsureTrackPartWorld(
            areaObject.transform,
            platformName,
            new Vector3(0f, -0.02f, 11.9f),
            Quaternion.identity,
            new Vector3(3.2f, 0.2f, 2.4f),
            new Color(0.93f, 0.82f, 0.46f, 1f),
            null
        );

        var triggerObject = new GameObject(triggerName);
        triggerObject.transform.SetParent(areaObject.transform);
        triggerObject.transform.localPosition = new Vector3(0f, 0.5f, 11.9f);
        triggerObject.transform.localRotation = Quaternion.identity;
        triggerObject.transform.localScale = Vector3.one;

        var triggerCollider = triggerObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(3.2f, 1.2f, 2.4f);
        triggerCollider.center = Vector3.zero;

        var trigger = triggerObject.AddComponent<CppQuestionTrigger>();
        trigger.focusPoint = triggerObject.transform;
        trigger.SetQuestionContent(questionText, codeText);
        trigger.SetQuestionSideOffset(7.2f);
        trigger.SetCodeVerticalOffset(codeVerticalOffset);
        trigger.SetQuestionStage(stage);
        trigger.SetLayoutQuestionBelowCode(questionBelowCode);
        trigger.SetSpawnNextQuestionOnCorrect(spawnNext);
        EnsureQuestionTextObjects(triggerObject.transform);

        EnsureTrackPartWorld(
            areaObject.transform,
            bridgeName,
            new Vector3(0f, -0.05f, 14.25f),
            Quaternion.identity,
            new Vector3(3.2f, 0.2f, 4.8f),
            WoodColor,
            null
        );

        var answersRootObject = new GameObject(answersName);
        answersRootObject.transform.SetParent(areaObject.transform);
        answersRootObject.transform.localPosition = new Vector3(0f, 0f, 16.5f);
        answersRootObject.transform.localRotation = Quaternion.identity;
        answersRootObject.transform.localScale = Vector3.one;

        EnsureTrackPartWorld(
            answersRootObject.transform,
            "AnswersBase",
            new Vector3(0f, -0.05f, 0f),
            Quaternion.identity,
            new Vector3(13.2f, 0.2f, 7f),
            GrassTopColor,
            null
        );

        int safeCorrectIndex = Mathf.Clamp(correctIndex, 0, 2);
        EnsureAnswerPad(answersRootObject.transform, "AnswerPadL", new Vector3(-5.2f, 0f, 0f), new Color(0.16f, 0.62f, 0.95f, 1f), answers[0], safeCorrectIndex == 0, trigger);
        EnsureAnswerPad(answersRootObject.transform, "AnswerPadM", Vector3.zero, new Color(0.95f, 0.36f, 0.26f, 1f), answers[1], safeCorrectIndex == 1, trigger);
        EnsureAnswerPad(answersRootObject.transform, "AnswerPadR", new Vector3(5.2f, 0f, 0f), new Color(0.58f, 0.46f, 0.92f, 1f), answers[2], safeCorrectIndex == 2, trigger);
        EnsureTutorialPath(answersRootObject.transform, trigger, stage);

        Transform postPath = EnsurePostAnswerPath(answersRootObject.transform);
        trigger.SetPostAnswerPath(postPath);
        trigger.answersRoot = answersRootObject.transform;

        answersRootObject.SetActive(false);
        areaObject.SetActive(false);
        answersRootOut = answersRootObject.transform;
        return trigger;
    }

    private static Transform EnsurePostAnswerPath(Transform answersRoot)
    {
        EnsureTrackPartWorld(
            answersRoot,
            "PostAnswerPath",
            new Vector3(0f, -0.05f, 6.1f),
            Quaternion.identity,
            new Vector3(3.2f, 0.2f, 9f),
            WoodColor,
            null
        );

        Transform path = answersRoot.Find("PostAnswerPath");
        if (path != null)
        {
            path.gameObject.SetActive(false);
        }

        return path;
    }

    private static void EnsureQuestionTextObjects(Transform triggerTransform)
    {
        EnsureQuestionTextObject(
            triggerTransform,
            "CppQuestionText",
            new Vector3(0f, 0.26f, 0f),
            92,
            0.12f,
            TextAnchor.MiddleCenter,
            TextAlignment.Center,
            Color.white
        );

        EnsureQuestionTextObject(
            triggerTransform,
            "CppCodeText",
            new Vector3(0f, 0.26f, 0f),
            68,
            0.09f,
            TextAnchor.UpperLeft,
            TextAlignment.Left,
            new Color(0.92f, 0.96f, 1f, 1f)
        );

        EnsureQuestionTextObject(
            triggerTransform,
            "FeedbackText",
            new Vector3(0f, 0.4f, 0f),
            118,
            0.15f,
            TextAnchor.MiddleCenter,
            TextAlignment.Center,
            Color.white
        );

        EnsureQuestionTextObject(
            triggerTransform,
            "ScoreText",
            new Vector3(0f, 0.52f, 0f),
            62,
            0.07f,
            TextAnchor.MiddleCenter,
            TextAlignment.Center,
            new Color(0.95f, 0.95f, 0.95f, 1f)
        );
    }

    private static void EnsureQuestionTextObject(
        Transform parent,
        string name,
        Vector3 localPosition,
        int fontSize,
        float characterSize,
        TextAnchor anchor,
        TextAlignment alignment,
        Color color)
    {
        Transform textTransform = parent.Find(name);
        GameObject textObject;
        if (textTransform == null)
        {
            textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
        }
        else
        {
            textObject = textTransform.gameObject;
        }

        var textMesh = textObject.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = textObject.AddComponent<TextMesh>();
        }

        textMesh.text = string.Empty;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.anchor = anchor;
        textMesh.alignment = alignment;
        textMesh.color = color;

        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        textObject.transform.localScale = Vector3.one;
    }

    private static void EnsureAnswerPad(Transform parent, string name, Vector3 localOffset, Color color, string label, bool isCorrect, CppQuestionTrigger trigger)
    {
        const float padThickness = 0.08f;
        const float padCenterY = 0.22f; // Raised so the pad hovers clearly above the AnswersBase.
        RemoveIfExists(parent, name);

        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = name;
        pad.transform.SetParent(parent);

        pad.transform.localPosition = localOffset + new Vector3(0f, padCenterY, 0f);
        pad.transform.localRotation = Quaternion.identity;
        pad.transform.localScale = new Vector3(2f, padThickness, 1.7f);

        var renderer = pad.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.35f);
                }
                renderer.material = material;
            }
        }

        Transform triggerT = pad.transform.Find("AnswerTrigger");
        GameObject triggerObj;
        if (triggerT == null)
        {
            triggerObj = new GameObject("AnswerTrigger");
            triggerObj.transform.SetParent(pad.transform);
        }
        else
        {
            triggerObj = triggerT.gameObject;
        }

        triggerObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        triggerObj.transform.localRotation = Quaternion.identity;
        triggerObj.transform.localScale = Vector3.one;

        var triggerCol = triggerObj.GetComponent<BoxCollider>();
        if (triggerCol == null)
        {
            triggerCol = triggerObj.AddComponent<BoxCollider>();
        }
        triggerCol.isTrigger = true;
        triggerCol.size = new Vector3(2f, 1.2f, 1.7f);
        triggerCol.center = Vector3.zero;

        var answerPad = triggerObj.GetComponent<CppAnswerPad>();
        if (answerPad == null)
        {
            answerPad = triggerObj.AddComponent<CppAnswerPad>();
        }
        answerPad.isCorrect = isCorrect;
        answerPad.questionTrigger = trigger;

        Transform labelT = pad.transform.Find("AnswerLabel");
        GameObject labelObj;
        if (labelT == null)
        {
            labelObj = new GameObject("AnswerLabel");
            labelObj.transform.SetParent(pad.transform);
        }
        else
        {
            labelObj = labelT.gameObject;
        }

        var textMesh = labelObj.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = labelObj.AddComponent<TextMesh>();
        }
        textMesh.text = label;
        textMesh.fontSize = 74;
        textMesh.characterSize = 0.085f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        labelObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        labelObj.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        labelObj.transform.localScale = Vector3.one;

    }

    private static void EnsureTutorialPath(Transform parent, CppQuestionTrigger trigger, int stage)
    {
        // Attach tutorial pipe platform directly to the left edge of the yellow question pad.
        // In answers-root local space, the question pad center is around z = -4.6.
        RemoveIfExists(parent, "TutorialPath");
        Vector3 platformCenter = new Vector3(-2.3f, -0.05f, -4.6f);
        CreateSimpleBlackFloorPiece(parent, "TutorialPlatform", platformCenter, new Vector3(1.4f, 0.2f, 1.4f));

        RemoveIfExists(parent, "TutorialPipeVisual");
        GameObject pipeVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipeVisual.name = "TutorialPipeVisual";
        pipeVisual.transform.SetParent(parent);
        pipeVisual.transform.localPosition = platformCenter + new Vector3(0f, 0.15f, 0f);
        pipeVisual.transform.localRotation = Quaternion.identity;
        pipeVisual.transform.localScale = new Vector3(0.58f, 0.16f, 0.58f);

        var pipeRenderer = pipeVisual.GetComponent<Renderer>();
        if (pipeRenderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = Color.white;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white * 0.15f);
                }
                pipeRenderer.material = material;
            }
        }

        RemoveIfExists(parent, "TutorialHoleTrigger");
        GameObject hole = new GameObject("TutorialHoleTrigger");
        hole.transform.SetParent(parent);
        hole.transform.localPosition = platformCenter + new Vector3(0f, 0.25f, 0f);
        hole.transform.localRotation = Quaternion.identity;
        hole.transform.localScale = Vector3.one;

        var collider = hole.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(0.96f, 0.6f, 0.96f);
        collider.center = new Vector3(0f, -0.25f, 0f);

        var tutorialTrigger = hole.AddComponent<TutorialHoleTrigger>();
        tutorialTrigger.SetQuestionTrigger(trigger);
        tutorialTrigger.SetTutorialText("Tutorial: read the code line by line, check which branch runs, then choose the value printed by cout.");
        ConfigureTutorialLearningContent(tutorialTrigger, stage);

        EnsureTutorialHintIndicator(parent, platformCenter + new Vector3(0f, 0.9f, 0f));
    }

    private static void ConfigureTutorialLearningContent(TutorialHoleTrigger tutorialTrigger, int stage)
    {
        if (tutorialTrigger == null)
        {
            return;
        }

        switch (stage)
        {
            case 2:
                tutorialTrigger.ConfigureLearningSequence(
                    "int x = 5;",
                    "if (x > 3) {",
                    " x = x + 2;",
                    "int x = 5; → Variable x starts with the value 5.",
                    "if (x > 3) → We check if x is greater than 3.",
                    "5 > 3 → This condition is true.",
                    "5 + 2 = 7",
                    "So the correct answer is 7.",
                    "}",
                    "x = x + 2 → We add 2 to x.");
                break;
            case 3:
                tutorialTrigger.ConfigureLearningSequence(
                    "int a = 5;",
                    "if (a > 3) cout << 10;",
                    "else cout << 2;",
                    "int a = 5; -> Variable a stores the value 5.",
                    "if (a > 3) cout << 10; -> Condition is true, so 10 is selected.",
                    "else cout << 2; -> Else branch is skipped because condition is true.",
                    "a > 3 is true -> output 10",
                    "So the correct answer is 10.");
                break;
            case 4:
                tutorialTrigger.ConfigureLearningSequence(
                    "int x = 4; int y = 3;",
                    "if (x + y > 6) cout << x * y;",
                    "else cout << x - y;",
                    "int x = 4; int y = 3; -> Variables x and y store 4 and 3.",
                    "if (x + y > 6) cout << x * y; -> 4 + 3 = 7, so condition is true.",
                    "else cout << x - y; -> Else branch is not used here.",
                    "4 + 3 = 7 > 6, so 4 × 3 = 12",
                    "So the correct answer is 12.");
                break;
            case 5:
                tutorialTrigger.ConfigureLearningSequence(
                    "int a = 5; int b = 2;",
                    "if (a % b == 1) cout << 3;",
                    "else cout << 0;",
                    "int a = 5; int b = 2; -> Variables a and b store 5 and 2.",
                    "if (a % b == 1) cout << 3; -> 5 % 2 equals 1, so condition is true.",
                    "else cout << 0; -> Else branch is skipped because condition is true.",
                    "5 % 2 = 1 -> output 3",
                    "So the correct answer is 3.");
                break;
            default:
                tutorialTrigger.ConfigureLearningSequence(
                    "int a = 4;",
                    "int b = 2;",
                    "int c = a * b;",
                    "int a = 4; -> Variable a stores the value 4.",
                    "int b = 2; -> Variable b stores the value 2.",
                    "int c = a * b; -> We multiply a and b.",
                    "4 × 2 = 8",
                    "So the correct answer is 8.");
                break;
        }
    }

    private static void CreateSimpleBlackFloorPiece(Transform parent, string name, Vector3 center, Vector3 size)
    {
        EnsureTrackPartWorld(
            parent,
            name,
            center,
            Quaternion.identity,
            size,
            Color.black,
            null
        );
    }

    private static void EnsureTutorialHintIndicator(Transform parent, Vector3 hintLocalPosition)
    {
        Transform existing = parent.Find("TutorialHintIndicator");
        GameObject hintRoot;
        if (existing == null)
        {
            hintRoot = new GameObject("TutorialHintIndicator");
            hintRoot.transform.SetParent(parent);
        }
        else
        {
            hintRoot = existing.gameObject;
        }

        hintRoot.transform.localPosition = hintLocalPosition;
        hintRoot.transform.localRotation = Quaternion.identity;
        hintRoot.transform.localScale = Vector3.one;

        Transform textTransform = hintRoot.transform.Find("HintText");
        GameObject textObject;
        if (textTransform == null)
        {
            textObject = new GameObject("HintText");
            textObject.transform.SetParent(hintRoot.transform);
        }
        else
        {
            textObject = textTransform.gameObject;
        }

        var hintText = textObject.GetComponent<TextMesh>();
        if (hintText == null)
        {
            hintText = textObject.AddComponent<TextMesh>();
        }
        hintText.text = "Need Help?";
        hintText.fontSize = 70;
        hintText.characterSize = 0.075f;
        hintText.anchor = TextAnchor.MiddleCenter;
        hintText.alignment = TextAlignment.Center;
        hintText.color = new Color(0.72f, 0.95f, 1f, 1f);
        textObject.transform.localPosition = new Vector3(0f, 0f, -2.48f);
        textObject.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        textObject.transform.localScale = Vector3.one;

        Transform arrowTransform = hintRoot.transform.Find("HintArrow");
        GameObject arrowObject;
        if (arrowTransform == null)
        {
            arrowObject = new GameObject("HintArrow");
            arrowObject.transform.SetParent(hintRoot.transform);
        }
        else
        {
            arrowObject = arrowTransform.gameObject;
        }

        var arrowText = arrowObject.GetComponent<TextMesh>();
        if (arrowText == null)
        {
            arrowText = arrowObject.AddComponent<TextMesh>();
        }
        arrowText.text = "▼";
        arrowText.fontSize = 92;
        arrowText.characterSize = 0.1f;
        arrowText.anchor = TextAnchor.MiddleCenter;
        arrowText.alignment = TextAlignment.Center;
        arrowText.color = Color.white;
        arrowObject.transform.localPosition = new Vector3(0f, 0f, -1.42f);
        arrowObject.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        arrowObject.transform.localScale = Vector3.one;
    }

    private static void EnsureFuturisticPath(Transform parent, string name, Vector3 center, float length, float width, Color accentColor, float curveAmount, float tiltAmount, float yawDegrees)
    {
        Transform existing = parent.Find(name);
        GameObject pathRoot;
        if (existing == null)
        {
            pathRoot = new GameObject(name);
            pathRoot.transform.SetParent(parent);
        }
        else
        {
            pathRoot = existing.gameObject;
        }

        pathRoot.transform.position = center;
        pathRoot.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        pathRoot.transform.localScale = Vector3.one;
        RemovePrimitiveVisuals(pathRoot);
        ClearChildren(pathRoot.transform);

        const int segmentCount = 26;
        float segmentLength = length / segmentCount;
        float railOffset = (width * 0.5f) + 0.12f;
        float glowOffset = (width * 0.5f) - 0.22f;

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1) / (float)segmentCount;
            float tm = (t0 + t1) * 0.5f;

            float x0 = (-length * 0.5f) + (t0 * length);
            float x1 = (-length * 0.5f) + (t1 * length);
            float xm = (x0 + x1) * 0.5f;

            float z0 = PathShapeOffset(t0, curveAmount, tiltAmount);
            float z1 = PathShapeOffset(t1, curveAmount, tiltAmount);
            float zm = PathShapeOffset(tm, curveAmount, tiltAmount);

            Vector3 p0 = new Vector3(x0, 0f, z0);
            Vector3 p1 = new Vector3(x1, 0f, z1);
            Vector3 dir = (p1 - p0).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            EnsureTrackPartWorld(
                pathRoot.transform,
                "RoadBase_" + i,
                new Vector3(xm, -0.08f, zm),
                rot,
                new Vector3(width, 0.2f, segmentLength + 0.04f),
                new Color(0.06f, 0.07f, 0.09f, 1f),
                null
            );

            EnsureTrackPartWorld(
                pathRoot.transform,
                "RoadTrim_" + i,
                new Vector3(xm, -0.02f, zm),
                rot,
                new Vector3(width, 0.03f, segmentLength + 0.04f),
                new Color(0.15f, 0.17f, 0.2f, 1f),
                null
            );

            EnsureTrackPartWorld(
                pathRoot.transform,
                "RailLeft_" + i,
                new Vector3(xm, 0.2f, zm) + (right * railOffset),
                rot,
                new Vector3(0.14f, 0.45f, segmentLength + 0.04f),
                new Color(0.18f, 0.2f, 0.24f, 1f),
                null
            );

            EnsureTrackPartWorld(
                pathRoot.transform,
                "RailRight_" + i,
                new Vector3(xm, 0.2f, zm) - (right * railOffset),
                rot,
                new Vector3(0.14f, 0.45f, segmentLength + 0.04f),
                new Color(0.18f, 0.2f, 0.24f, 1f),
                null
            );

            EnsureTrackPartWorld(
                pathRoot.transform,
                "GlowLeft_" + i,
                new Vector3(xm, 0.02f, zm) + (right * glowOffset),
                rot,
                new Vector3(0.08f, 0.02f, segmentLength + 0.04f),
                accentColor,
                accentColor * 1.5f
            );

            EnsureTrackPartWorld(
                pathRoot.transform,
                "GlowRight_" + i,
                new Vector3(xm, 0.02f, zm) - (right * glowOffset),
                rot,
                new Vector3(0.08f, 0.02f, segmentLength + 0.04f),
                accentColor,
                accentColor * 1.5f
            );

            if (i % 3 == 1)
            {
                EnsureTrackPartWorld(
                    pathRoot.transform,
                    "Dash_" + i,
                    new Vector3(xm, 0.015f, zm),
                    rot,
                    new Vector3(0.1f, 0.02f, 0.65f),
                    new Color(0.7f, 0.76f, 0.82f, 1f),
                    null
                );
            }
        }
    }

    private static float PathShapeOffset(float t, float curveAmount, float tiltAmount)
    {
        // Mixed profile: slight bow + slight directional lean (not fully straight or fully curved).
        float bow = Mathf.Sin(t * Mathf.PI) * curveAmount;
        float tilt = ((t - 0.5f) * 2f) * tiltAmount;
        return bow + tilt;
    }

    private static void EnsureTrackPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        EnsureTrackPart(parent, name, localPos, localScale, color, null);
    }

    private static void RemovePrimitiveVisuals(GameObject gameObject)
    {
        var collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroySafe(collider);
        }

        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            DestroySafe(renderer);
        }

        var filter = gameObject.GetComponent<MeshFilter>();
        if (filter != null)
        {
            DestroySafe(filter);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroySafe(parent.GetChild(i).gameObject);
        }
    }

    private static void EnsureTrackPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, Color? emission)
    {
        Transform existing = parent.Find(name);
        GameObject part;
        if (existing == null)
        {
            part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent);
        }
        else
        {
            part = existing.gameObject;
        }

        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        bool keepDetailedPathModel = IsDetailedPrimaryPathContext(parent);

        // Keep non-primary paths visually simple: flat black, no glow/emission effects.
        if (!keepDetailedPathModel && ShouldForceSimpleBlackPath(name))
        {
            color = Color.black;
            emission = null;
        }

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (emission.HasValue && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                }

                renderer.material = material;
            }
        }
    }

    private static void EnsureTrackPartWorld(Transform parent, string name, Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color, Color? emission)
    {
        bool keepDetailedPathModel = IsDetailedPrimaryPathContext(parent);

        // Only the first 3 difficulty paths keep detailed visuals.
        if (!keepDetailedPathModel && IsExtraPathDetail(name))
        {
            RemoveIfExists(parent, name);
            return;
        }

        EnsureTrackPart(parent, name, localPos, localScale, color, emission);
        Transform t = parent.Find(name);
        if (t != null)
        {
            t.localRotation = localRot;
        }
    }

    private static bool IsGlowPart(string name)
    {
        return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("glow");
    }

    private static bool IsDetailedPrimaryPathContext(Transform parent)
    {
        Transform current = parent;
        while (current != null)
        {
            string n = current.name;
            if (n == "PathEasy" || n == "PathMedium" || n == "PathHard")
            {
                return true;
            }

            // Keep original detailed/lit visuals for lift cylinder areas.
            if (n.Contains("TubeLane") || n == "Tube" || n.Contains("Lift"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsExtraPathDetail(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string n = name.ToLowerInvariant();
        return IsGlowPart(name)
            || n.Contains("rail")
            || n.Contains("trim")
            || n.Contains("band");
    }

    private static bool ShouldForceSimpleBlackPath(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        string n = name.ToLowerInvariant();
        return n.Contains("path")
            || n.Contains("track")
            || n.Contains("approach")
            || n.Contains("connector")
            || n.Contains("extension")
            || n.Contains("bridge")
            || n.Contains("lane")
            || n.Contains("rail");
    }

    private static void EnsurePathLabel(Transform parent, string name, string text, Vector3 position, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject labelObject;
        if (existing == null)
        {
            labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent);
        }
        else
        {
            labelObject = existing.gameObject;
        }

        ClearChildren(labelObject.transform);
        EnsureTrackPart(labelObject.transform, "SignPost", new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.46f, 0.16f), WoodDarkColor, null);
        EnsureTrackPart(labelObject.transform, "SignPanel", new Vector3(0f, 0.005f, 0f), new Vector3(1.9f, 0.08f, 0.66f), WoodColor, null);
        EnsureTrackPart(labelObject.transform, "SignFace", new Vector3(0f, 0.06f, 0f), new Vector3(1.62f, 0.04f, 0.5f), new Color(0.93f, 0.88f, 0.71f, 1f), null);

        var textMesh = labelObject.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = labelObject.AddComponent<TextMesh>();
        }

        textMesh.text = text;
        textMesh.fontSize = 72;
        textMesh.characterSize = 0.082f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(color.r * 0.7f, color.g * 0.65f, color.b * 0.62f, 1f);

        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        labelObject.transform.localScale = Vector3.one;
    }

    private static Transform EnsureGatePivot(Transform parent, string name, Vector3 localPosition)
    {
        Transform pivot = parent.Find(name);
        if (pivot == null)
        {
            var pivotObject = new GameObject(name);
            pivot = pivotObject.transform;
            pivot.SetParent(parent);
        }

        pivot.localPosition = localPosition;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        return pivot;
    }

    private static GameObject EnsureGatePart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, PrimitiveType primitiveType = PrimitiveType.Cube)
    {
        Transform existing = parent.Find(name);
        GameObject part;

        if (existing == null)
        {
            part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent);
        }
        else
        {
            part = existing.gameObject;
        }

        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = color;
                renderer.material = material;
            }
        }

        return part;
    }

    private static PhysicMaterial CreateSlipperyNoBounceMaterial()
    {
        return new PhysicMaterial("SlipperyNoBounce")
        {
            bounciness = 0f,
            dynamicFriction = 0.05f,
            staticFriction = 0.05f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    private static void ConfigureTopFollowCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        var cameraTransform = cam.transform;
        cameraTransform.position = target.position + new Vector3(0f, 15f, 0f);
        cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var follow = cam.GetComponent<BeanCameraFollow>();
        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<BeanCameraFollow>();
        }

        follow.target = target;

        var background = cam.GetComponent<BackgroundColorCycler>();
        if (background == null)
        {
            background = cam.gameObject.AddComponent<BackgroundColorCycler>();
        }
    }

    private static void DestroySafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}

#endif
