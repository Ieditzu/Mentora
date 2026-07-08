using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Mentora.Network;
using UnityEngine;

public enum CodeWorldQuestPortalKind
{
    Sandbox,
    Easy,
    Medium,
    Hard,
    AiProfile
}

public sealed class CodeWorldQuestIsland : MonoBehaviour
{
    private static readonly Vector3 HubCenter = new Vector3(220f, 30f, 430f);
    private static readonly Color GroundColor = new Color(0.09f, 0.14f, 0.18f);
    private static readonly Color EdgeColor = new Color(0.08f, 0.55f, 0.76f);
    public static Vector3 SpawnPosition => HubCenter + new Vector3(0f, 2.4f, -8f);
    public static Quaternion SpawnRotation => Quaternion.Euler(0f, 0f, 0f);

    public void Build()
    {
        if (transform.Find("CodeQuestIslandPlatform") != null)
        {
            return;
        }

        transform.position = Vector3.zero;
        BuildPlatform();
        BuildHeaderSign();
        BuildPortal(CodeWorldQuestPortalKind.Easy, "EASY\nBuild", new Vector3(-13f, 1.4f, 4f), new Color(0.22f, 0.9f, 0.42f));
        BuildPortal(CodeWorldQuestPortalKind.Medium, "MEDIUM\nFix", new Vector3(-6.5f, 1.4f, 10f), new Color(0.95f, 0.72f, 0.2f));
        BuildPortal(CodeWorldQuestPortalKind.Hard, "HARD\nSystems", new Vector3(0f, 1.4f, 13f), new Color(0.95f, 0.24f, 0.24f));
        BuildPortal(CodeWorldQuestPortalKind.AiProfile, "AI\nProfile Quest", new Vector3(6.5f, 1.4f, 10f), new Color(0.6f, 0.35f, 1f));
        BuildPortal(CodeWorldQuestPortalKind.Sandbox, "FREE\nSandbox", new Vector3(13f, 1.4f, 4f), new Color(0.2f, 0.68f, 1f));
        BuildDecor();
    }

    private void BuildPlatform()
    {
        GameObject basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePlatform.name = "CodeQuestIslandPlatform";
        basePlatform.transform.SetParent(transform, false);
        basePlatform.transform.position = HubCenter;
        basePlatform.transform.localScale = new Vector3(24f, 1.25f, 24f);
        RemoveGeneratedCollider(basePlatform);
        ApplyMaterial(basePlatform, GroundColor, 0f);

        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "CodeQuestIslandGlowRim";
        rim.transform.SetParent(transform, false);
        rim.transform.position = HubCenter + Vector3.up * 0.08f;
        rim.transform.localScale = new Vector3(25.2f, 0.12f, 25.2f);
        RemoveGeneratedCollider(rim);
        ApplyMaterial(rim, EdgeColor, 1.8f);

        BuildWalkColliders();
    }

    private void BuildWalkColliders()
    {
        GameObject root = new GameObject("CodeQuestIslandWalkCollider");
        root.transform.SetParent(transform, false);
        root.transform.position = HubCenter + new Vector3(0f, 1.14f, 0f);

        AddWalkColliderStrip(root.transform, "WalkColliderStrip_Center", 0f, 24f, 4f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_Near", -4f, 23f, 4f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_Far", 4f, 23f, 4f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_NearOuter", -8f, 18f, 4f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_FarOuter", 8f, 18f, 4f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_NearTip", -11f, 10f, 2f);
        AddWalkColliderStrip(root.transform, "WalkColliderStrip_FarTip", 11f, 10f, 2f);
    }

    private static void AddWalkColliderStrip(Transform parent, string name, float localZ, float width, float depth)
    {
        GameObject strip = new GameObject(name);
        strip.transform.SetParent(parent, false);
        strip.transform.localPosition = new Vector3(0f, 0f, localZ);

        BoxCollider collider = strip.AddComponent<BoxCollider>();
        collider.size = new Vector3(width, 0.22f, depth);
    }

    private void BuildHeaderSign()
    {
        GameObject signRoot = new GameObject("CodeQuestIslandSign");
        signRoot.transform.SetParent(transform, false);
        signRoot.transform.position = HubCenter + new Vector3(0f, 5.3f, 18f);
        signRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backing.name = "SignBacking";
        backing.transform.SetParent(signRoot.transform, false);
        backing.transform.localScale = new Vector3(13f, 3f, 0.4f);
        ApplyMaterial(backing, new Color(0.04f, 0.07f, 0.12f), 0f);

        TextMesh text = CreateWorldText(signRoot.transform, "CODE QUEST ISLAND\nPick a portal, solve it on Code Island", new Vector3(0f, 0.08f, -0.26f), 0.105f, TextAnchor.MiddleCenter, Color.white);
        text.name = "SignText";
        text.fontStyle = FontStyle.Bold;
        text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private void BuildPortal(CodeWorldQuestPortalKind kind, string label, Vector3 localPosition, Color color)
    {
        GameObject portal = new GameObject("CodeQuestPortal_" + kind);
        portal.transform.SetParent(transform, false);
        portal.transform.position = HubCenter + localPosition;
        portal.transform.rotation = Quaternion.LookRotation((HubCenter - portal.transform.position).normalized, Vector3.up);

        BoxCollider trigger = portal.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.2f, 4.2f, 2.2f);
        trigger.center = new Vector3(0f, 1.2f, 0f);

        CodeWorldQuestPortal portalComponent = portal.AddComponent<CodeWorldQuestPortal>();
        portalComponent.Configure(kind, label.Replace("\n", " "), color);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(portal.transform, false);
        core.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        core.transform.localScale = new Vector3(1.45f, 2.05f, 0.16f);
        RemoveGeneratedCollider(core);
        ApplyMaterial(core, color, 2.4f);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "OuterRing";
        ring.transform.SetParent(portal.transform, false);
        ring.transform.localPosition = new Vector3(0f, 1.2f, -0.03f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(1.55f, 0.055f, 1.55f);
        RemoveGeneratedCollider(ring);
        ApplyMaterial(ring, Color.Lerp(color, Color.white, 0.35f), 3.5f);

        GameObject innerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        innerRing.name = "InnerRing";
        innerRing.transform.SetParent(portal.transform, false);
        innerRing.transform.localPosition = new Vector3(0f, 1.2f, -0.06f);
        innerRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        innerRing.transform.localScale = new Vector3(1.2f, 0.035f, 1.2f);
        RemoveGeneratedCollider(innerRing);
        ApplyMaterial(innerRing, Color.Lerp(color, Color.white, 0.55f), 3f);

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "PortalPedestal";
        pedestal.transform.SetParent(portal.transform, false);
        pedestal.transform.localPosition = new Vector3(0f, -0.68f, 0f);
        pedestal.transform.localScale = new Vector3(1.75f, 0.22f, 1.75f);
        RemoveGeneratedCollider(pedestal);
        ApplyMaterial(pedestal, new Color(0.07f, 0.1f, 0.14f), 0f);

        Light portalLight = portal.AddComponent<Light>();
        portalLight.type = LightType.Point;
        portalLight.color = color;
        portalLight.range = 8f;
        portalLight.intensity = 2.6f;

        TextMesh text = CreateWorldText(portal.transform, label, new Vector3(0f, 0.95f, -0.42f), 0.045f, TextAnchor.MiddleCenter, Color.white);
        text.name = "PortalLabel";
        text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        PortalLoopAnimator animator = portal.AddComponent<PortalLoopAnimator>();
        animator.Initialize(color);
    }

    private void BuildDecor()
    {
        for (int index = 0; index < 10; index++)
        {
            float angle = index * Mathf.PI * 2f / 10f;
            Vector3 position = HubCenter + new Vector3(Mathf.Cos(angle) * 18.2f, 1.18f, Mathf.Sin(angle) * 18.2f);
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = "CodeQuestAquaNode_" + index;
            node.transform.SetParent(transform, false);
            node.transform.position = position;
            node.transform.localScale = Vector3.one * 0.58f;
            RemoveGeneratedCollider(node);
            ApplyMaterial(node, EdgeColor, 2.2f);
        }
    }

    private static TextMesh CreateWorldText(Transform parent, string value, Vector3 localPosition, float size, TextAnchor anchor, Color color)
    {
        GameObject obj = new GameObject("WorldText");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localRotation = Quaternion.identity;
        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 64;
        text.characterSize = size;
        text.anchor = anchor;
        text.alignment = TextAlignment.Center;
        text.lineSpacing = 0.82f;
        text.color = color;
        return text;
    }

    private static void RemoveGeneratedCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(collider);
        }
        else
        {
            DestroyImmediate(collider);
        }
    }

    private static void ApplyMaterial(GameObject target, Color color, float emission)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_EmissionColor") && emission > 0f)
        {
            material.SetColor("_EmissionColor", color * emission);
            material.EnableKeyword("_EMISSION");
        }
        renderer.sharedMaterial = material;
    }
}

public sealed class CodeWorldQuestPortal : MonoBehaviour
{
    [SerializeField]
    private CodeWorldQuestPortalKind portalKind;
    [SerializeField]
    private string portalLabel;
    [SerializeField]
    private Color portalColor = Color.cyan;
    private bool activationBusy;
    private GenerateAiTaskResponsePacket generatedTask;
    private string generationError;

    public void Configure(CodeWorldQuestPortalKind kind, string label, Color color)
    {
        portalKind = kind;
        portalLabel = label;
        portalColor = color;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activationBusy || !IsLocalPlayer(other))
        {
            return;
        }

        StartCoroutine(ActivatePortalRoutine());
    }

    private IEnumerator ActivatePortalRoutine()
    {
        activationBusy = true;
        CodeWorldQuestPortalKind resolvedKind = ResolvePortalKind();

        Debug.Log("[CodeQuest] Entered " + resolvedKind + " portal" + (string.IsNullOrWhiteSpace(portalLabel) ? string.Empty : ": " + portalLabel));

        if (resolvedKind == CodeWorldQuestPortalKind.Sandbox)
        {
            CodeWorldRuntime.StartSandboxFromPortal();
            yield return new WaitForSeconds(1f);
            activationBusy = false;
            yield break;
        }

        CodeWorldChallengeDefinition definition;
        if (resolvedKind == CodeWorldQuestPortalKind.AiProfile)
        {
            yield return RequestAiChallengeRoutine();
            definition = CodeWorldQuestDefinitions.FromAiTask(generatedTask, generationError);
        }
        else
        {
            definition = CodeWorldQuestDefinitions.Create(resolvedKind);
        }

        CodeWorldRuntime.StartChallengeFromPortal(definition);
        yield return new WaitForSeconds(1f);
        activationBusy = false;
    }

    private IEnumerator RequestAiChallengeRoutine()
    {
        generatedTask = null;
        generationError = string.Empty;

        if (GameClient.Instance == null)
        {
            generationError = "AI server client is not available.";
            yield break;
        }

        GameClient.Instance.OnPacketReceived += OnPacket;
        Task<bool> sendTask = SendPacketWithConnect(new GenerateAiTaskPacket("codeworld"));
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (!sendTask.Result)
        {
            generationError = "Could not reach AI task server.";
            GameClient.Instance.OnPacketReceived -= OnPacket;
            yield break;
        }

        float elapsed = 0f;
        while (generatedTask == null && string.IsNullOrWhiteSpace(generationError) && elapsed < 14f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (generatedTask == null && string.IsNullOrWhiteSpace(generationError))
        {
            generationError = "AI task generation timed out.";
        }

        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPacketReceived -= OnPacket;
        }
    }

    private async Task<bool> SendPacketWithConnect(Packet packet)
    {
        if (GameClient.Instance == null)
        {
            return false;
        }

        if (!GameClient.Instance.IsConnected)
        {
            await GameClient.Instance.Connect();
        }

        if (!GameClient.Instance.IsConnected)
        {
            return false;
        }

        await GameClient.Instance.SendPacket(packet);
        return true;
    }

    private void OnPacket(Packet packet)
    {
        if (packet is GenerateAiTaskResponsePacket response)
        {
            generatedTask = response;
            return;
        }

        if (packet is ActionResponsePacket actionResponse && actionResponse.RequestPacketId == 45 && !actionResponse.Success)
        {
            generationError = string.IsNullOrWhiteSpace(actionResponse.Message) ? "AI task generation failed." : actionResponse.Message;
        }
    }

    private CodeWorldQuestPortalKind ResolvePortalKind()
    {
        if (portalKind != CodeWorldQuestPortalKind.Sandbox || NameContains("Sandbox") || NameContains("Free"))
        {
            return portalKind;
        }

        if (NameContains("Easy"))
        {
            return CodeWorldQuestPortalKind.Easy;
        }

        if (NameContains("Medium"))
        {
            return CodeWorldQuestPortalKind.Medium;
        }

        if (NameContains("Hard"))
        {
            return CodeWorldQuestPortalKind.Hard;
        }

        if (NameContains("AiProfile") || NameContains("AI") || NameContains("Profile"))
        {
            return CodeWorldQuestPortalKind.AiProfile;
        }

        return portalKind;
    }

    private bool NameContains(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               name.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLocalPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        return other.GetComponent<BeanController>() != null ||
               other.GetComponentInParent<BeanController>() != null ||
               other.GetComponent<FirstPersonControllerSimple>() != null ||
               other.GetComponentInParent<FirstPersonControllerSimple>() != null;
    }
}

public static class CodeWorldQuestDefinitions
{
    public static CodeWorldChallengeDefinition Create(CodeWorldQuestPortalKind kind)
    {
        switch (kind)
        {
            case CodeWorldQuestPortalKind.Easy:
                return CreateEasy();
            case CodeWorldQuestPortalKind.Medium:
                return CreateMedium();
            case CodeWorldQuestPortalKind.Hard:
                return CreateHard();
            default:
                return CreateEasy();
        }
    }

    public static CodeWorldChallengeDefinition FromAiTask(GenerateAiTaskResponsePacket response, string error)
    {
        CodeWorldChallengeDefinition definition = new CodeWorldChallengeDefinition
        {
            Id = "ai_profile",
            GeneratedByAi = true,
            Title = response != null && !string.IsNullOrWhiteSpace(response.Title) ? response.Title.Trim() : "AI Profile Build",
            Description = BuildAiDescription(response, error),
            StarterCode = BuildAiStarterCode(response)
        };

        definition.Requirements.Add(CodeWorldChallengeRequirement.Exists("profile_goal", "Create an object named profile_goal"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.CountAtLeast(3, "Build at least 3 objects total"));
        return definition;
    }

    private static CodeWorldChallengeDefinition CreateEasy()
    {
        Vector3 beaconPosition = CodeWorldRuntime.SpawnPosition + new Vector3(0f, 1f, 6f);
        CodeWorldChallengeDefinition definition = new CodeWorldChallengeDefinition
        {
            Id = "easy_beacon",
            Title = "Easy: Red Beacon",
            Description = "Build a red beacon near the marker. It needs a tall object named beacon.",
            StarterCode =
                "from mentora_world import *\n\n" +
                "# Easy goal: fix this beacon so it passes the checklist.\n" +
                "# Hint: it is in the right place, but two values are wrong.\n" +
                "cube(\"beacon\", vector(220, 33, 526), scale=vector(1.5, 1.2, 1.5))\n" +
                "color(\"beacon\", \"blue\")\n"
        };
        definition.SetupCommands = new[] { "cylinder marker 220 32.6 526 2.5 0.2 2.5", "color marker yellow" };
        definition.Requirements.Add(CodeWorldChallengeRequirement.Exists("beacon", "Create an object named beacon"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.Near("beacon", beaconPosition, 1.75f, "Place beacon on the yellow marker"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.ScaleAtLeast("beacon", new Vector3(1f, 2.5f, 1f), "Make beacon tall enough"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.ColorNear("beacon", new Color(0.92f, 0.28f, 0.28f), 0.35f, "Color beacon red"));
        return definition;
    }

    private static CodeWorldChallengeDefinition CreateMedium()
    {
        CodeWorldChallengeDefinition definition = new CodeWorldChallengeDefinition
        {
            Id = "medium_bridge_repair",
            Title = "Medium: Repair Bridge",
            Description = "Clear the rubble and build at least three planks named plank_1, plank_2, etc. Add a green finish_flag.",
            StarterCode =
                "from mentora_world import *\n\n" +
                "# Medium goal: repair this unfinished bridge script.\n" +
                "# Hint: the rubble is handled, but the bridge is incomplete and the flag color is wrong.\n" +
                "delete(\"rubble\")\n" +
                "for i in range(2):\n" +
                "    cube(f\"plank_{i + 1}\", vector(216 + i * 4, 33, 530), scale=vector(3.2, 0.35, 1.4))\n" +
                "    color(f\"plank_{i + 1}\", \"orange\")\n" +
                "cube(\"finish_flag\", vector(228, 34, 530), scale=vector(0.7, 2.5, 0.7))\n" +
                "color(\"finish_flag\", \"yellow\")\n"
        };
        definition.SetupCommands = new[] { "cube rubble 220 33 530 4 0.75 3", "color rubble gray" };
        definition.Requirements.Add(CodeWorldChallengeRequirement.Missing("rubble", "Delete the rubble"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.PrefixCountAtLeast("plank_", 3, "Build at least 3 plank_ objects"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.Exists("finish_flag", "Create a finish_flag"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.ColorNear("finish_flag", new Color(0.2f, 0.82f, 0.34f), 0.35f, "Color finish_flag green"));
        return definition;
    }

    private static CodeWorldChallengeDefinition CreateHard()
    {
        CodeWorldChallengeDefinition definition = new CodeWorldChallengeDefinition
        {
            Id = "hard_power_core",
            Title = "Hard: Restore Core",
            Description = "Remove the broken core, build a cyan power_core, and create four shield_ objects around it.",
            StarterCode =
                "from mentora_world import *\n\n" +
                "# Hard goal: write the restore script.\n" +
                "# Requirements:\n" +
                "# - delete broken_core\n" +
                "# - create a cyan sphere named power_core near core_position\n" +
                "# - create four shield_ objects using shield_positions\n\n" +
                "core_position = vector(220, 35, 536)\n" +
                "shield_positions = [\n" +
                "    vector(216, 33, 536),\n" +
                "    vector(224, 33, 536),\n" +
                "    vector(220, 33, 532),\n" +
                "    vector(220, 33, 540),\n" +
                "]\n\n" +
                "# Write your solution below this line.\n"
        };
        definition.SetupCommands = new[] { "sphere broken_core 220 34 536 2 2 2", "color broken_core red" };
        definition.Requirements.Add(CodeWorldChallengeRequirement.Missing("broken_core", "Delete broken_core"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.Exists("power_core", "Create power_core"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.Near("power_core", CodeWorldRuntime.SpawnPosition + new Vector3(0f, 3f, 16f), 2.5f, "Place power_core in the repair zone"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.ColorNear("power_core", new Color(0.18f, 0.84f, 0.88f), 0.35f, "Color power_core cyan"));
        definition.Requirements.Add(CodeWorldChallengeRequirement.PrefixCountAtLeast("shield_", 4, "Build four shield_ objects"));
        return definition;
    }

    private static string BuildAiDescription(GenerateAiTaskResponsePacket response, string error)
    {
        StringBuilder builder = new StringBuilder();
        if (response != null && !string.IsNullOrWhiteSpace(response.Description))
        {
            builder.Append(response.Description.Trim());
        }
        else
        {
            builder.Append("Profile AI was unavailable, so this fallback asks you to build a small scene that represents your current coding goal.");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.Append("\nAI note: ").Append(error.Trim());
        }

        builder.Append("\nFor verification, create at least three objects and one object named profile_goal.");
        return builder.ToString();
    }

    private static string BuildAiStarterCode(GenerateAiTaskResponsePacket response)
    {
        string generated = response != null ? response.CodeTemplate ?? string.Empty : string.Empty;
        if (!string.IsNullOrWhiteSpace(generated))
        {
            return "from mentora_world import *\n\n" +
                   "# AI profile quest. Adapt this starter and make sure you create profile_goal plus at least 3 objects.\n" +
                   generated.Trim() + "\n";
        }

        return
            "from mentora_world import *\n\n" +
            "# Fallback AI profile quest: finish this mini scene about what you are learning.\n" +
            "# Requirements: create profile_goal and at least 3 objects total.\n\n" +
            "center = vector(220, 33, 526)\n" +
            "cube(\"profile_goal\", center, scale=vector(2, 2, 2))\n" +
            "color(\"profile_goal\", \"purple\")\n\n" +
            "# Add at least two more objects below.\n";
    }
}
