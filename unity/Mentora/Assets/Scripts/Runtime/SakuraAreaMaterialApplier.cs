using UnityEngine;

[ExecuteAlways]
public class SakuraAreaMaterialApplier : MonoBehaviour
{
    [SerializeField] private Material sakuraLeafMaterial;
    [SerializeField] private Material sakuraBarkMaterial;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material bridgeMaterial;

    private void OnEnable()
    {
        ApplyMaterials();
    }

    private void OnValidate()
    {
        ApplyMaterials();
    }

    [ContextMenu("Apply Sakura Area Materials")]
    public void ApplyMaterials()
    {
        ApplyToSakura();
        ApplyToLowPolyFloor();
        ApplyToNearestBridge();
    }

    private void ApplyToSakura()
    {
        GameObject sakura = GameObject.Find("sakura2");
        if (sakura == null)
        {
            return;
        }

        Renderer[] renderers = sakura.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            bool changed = false;

            for (int m = 0; m < materials.Length; m++)
            {
                string materialName = materials[m] != null ? materials[m].name : string.Empty;
                if (sakuraLeafMaterial != null && materialName.Contains("sakura_branch_new01"))
                {
                    materials[m] = sakuraLeafMaterial;
                    changed = true;
                }
                else if (sakuraBarkMaterial != null && materialName.Contains("mossybark02"))
                {
                    materials[m] = sakuraBarkMaterial;
                    changed = true;
                }
            }

            if (changed)
            {
                renderers[i].sharedMaterials = materials;
            }
        }
    }

    private void ApplyToLowPolyFloor()
    {
        if (floorMaterial == null)
        {
            return;
        }

        GameObject floorRoot = GameObject.Find("low_poly_treesNXT_5flat");
        if (floorRoot == null)
        {
            return;
        }

        Renderer[] renderers = floorRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = floorMaterial;
        }
    }

    private void ApplyToNearestBridge()
    {
        if (bridgeMaterial == null)
        {
            return;
        }

        GameObject sakura = GameObject.Find("sakura2");
        if (sakura == null)
        {
            return;
        }

        GameObject[] bridges = GameObject.FindObjectsOfType<GameObject>(true);
        Transform nearestBridge = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < bridges.Length; i++)
        {
            if (bridges[i].name != "BranchBridge")
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(bridges[i].transform.position - sakura.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestBridge = bridges[i].transform;
            }
        }

        if (nearestBridge == null)
        {
            return;
        }

        Renderer[] renderers = nearestBridge.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = bridgeMaterial;
        }
    }
}
