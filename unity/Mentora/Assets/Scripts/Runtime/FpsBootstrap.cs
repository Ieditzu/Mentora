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
        // No visual proxy for the FPS player; skip spawning FPS_BeanBody.
        return;
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
