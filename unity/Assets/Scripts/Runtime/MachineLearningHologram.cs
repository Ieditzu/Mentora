using UnityEngine;

public sealed class MachineLearningHologram : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 28f;
    [SerializeField] private float bobAmplitude = 0.18f;
    [SerializeField] private float bobSpeed = 1.4f;

    private Vector3 startLocalPosition;
    private float phase;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        phase = Mathf.Abs(gameObject.GetInstanceID() % 97) * 0.1f;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        Vector3 position = startLocalPosition;
        position.y += Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
        transform.localPosition = position;
    }
}
