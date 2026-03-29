using UnityEngine;

/// <summary>
/// Simple vertex-wave animator for the runtime water surface. Uses sine waves to displace y.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WaterWaveAnimator : MonoBehaviour
{
    [SerializeField] private float primaryAmplitude = 0.25f;
    [SerializeField] private float primaryFrequency = 0.35f;
    [SerializeField] private float primarySpeed = 0.8f;
    [SerializeField] private float secondaryAmplitude = 0.12f;
    [SerializeField] private float secondaryFrequency = 0.9f;
    [SerializeField] private float secondarySpeed = 1.5f;

    private Mesh mesh;
    private Vector3[] baseVertices;

    private void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        baseVertices = mesh.vertices;
    }

    private void Update()
    {
        if (mesh == null || baseVertices == null) return;

        float t = Time.time;
        Vector3[] verts = new Vector3[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 v = baseVertices[i];
            float wave1 = Mathf.Sin((v.x + t * primarySpeed) * primaryFrequency) * primaryAmplitude;
            float wave2 = Mathf.Sin((v.z + t * secondarySpeed) * secondaryFrequency) * secondaryAmplitude;
            v.y = wave1 + wave2;
            verts[i] = v;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
