using UnityEngine;

/// <summary>Spawns a simple first-person capsule if none exists so you can explore the map in FPS mode.</summary>
public static class FpsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        // If a bean-based player already exists in the scene, keep using it and avoid spawning a duplicate FPS rig.
        if (Object.FindObjectOfType<BeanController>() != null)
        {
            return;
        }

        if (Object.FindObjectOfType<FirstPersonControllerSimple>() != null)
        {
            return;
        }

        Vector3 spawnPos = GuessSpawnPosition();
        GameObject player = new GameObject("FPS_Player");
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.42f;
        cc.radius = 0.18f;
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

        player.transform.position = spawnPos;
        player.AddComponent<FirstPersonControllerSimple>();
        AddBeanBody(player, cc);

        // Ensure the bean is not disabled when FPS is spawned!
    }

    private static void AddBeanBody(GameObject player, CharacterController cc)
    {
        var beanPrefab = Resources.Load<GameObject>("Characters/SM_Bean_Female_01");
        if (beanPrefab == null)
        {
            Debug.LogWarning("[FpsBootstrap] Bean prefab not found in Resources/Characters/");
            return;
        }

        var body = Object.Instantiate(beanPrefab, player.transform);
        body.name = "BeanBody";
        // eyeHeight = 4.0f in FirstPersonControllerSimple, bean natural height ≈ 1.72 units at scale 1
        // scale so head sits just below the camera: 4.0 / 1.72 ≈ 2.32, use 2.2 for a slight gap
        body.transform.localPosition = new Vector3(0f, 0f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale    = Vector3.one * 2.2f;

        // Remove any colliders on the body so they don't fight the CharacterController
        foreach (var col in body.GetComponentsInChildren<Collider>())
            Object.Destroy(col);

        // Put the bean body on the "PlayerBody" layer (layer 3) so the
        // first-person camera can cull it — player shouldn't see their own body
        SetLayerRecursively(body, 3);

        // Exclude layer 3 from the FP camera so the bean is invisible in first person
        var fpCam = player.GetComponentInChildren<Camera>();
        if (fpCam != null)
            fpCam.cullingMask &= ~(1 << 3);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static Vector3 GuessSpawnPosition()
    {
        // Prefer to start near the sphere's initial position if present.
        BeanController sphere = Object.FindObjectOfType<BeanController>();
        if (sphere != null)
        {
            Vector3 pos = sphere.transform.position + new Vector3(0f, 2.0f, -2.5f);
            return pos;
        }

        GameObject easyPath = GameObject.Find("PathEasy");
        if (easyPath != null)
        {
            return easyPath.transform.position + new Vector3(0f, 2.0f, -4f);
        }

        // Default spawn if no hints found.
        return new Vector3(75f, 1.0f, 222f);
    }
}
