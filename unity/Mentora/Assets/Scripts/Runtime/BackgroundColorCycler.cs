using UnityEngine;

public class BackgroundColorCycler : MonoBehaviour
{
    [SerializeField] private float cycleSpeed = 0.08f;
    [SerializeField] private float saturation = 0.35f;
    [SerializeField] private float value = 0.35f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            enabled = false;
            return;
        }

        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private void Update()
    {
        float h = Mathf.Repeat(Time.time * cycleSpeed, 1f);
        cam.backgroundColor = Color.HSVToRGB(h, saturation, value);
    }
}

