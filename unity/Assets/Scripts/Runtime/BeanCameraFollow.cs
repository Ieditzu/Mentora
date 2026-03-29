using UnityEngine;

public class BeanCameraFollow : MonoBehaviour
{
    public Transform target;
    [SerializeField] private float smoothTime = 0.12f;

    private Vector3 offset;
    private Vector3 velocity;
    private Quaternion fixedRotation;

    private void Start()
    {
        InitializeTarget(target, true);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        transform.rotation = fixedRotation;
    }

    public void InitializeTarget(Transform newTarget, bool snapToTarget = false)
    {
        target = newTarget;
        if (target == null)
        {
            return;
        }

        offset = transform.position - target.position;
        fixedRotation = transform.rotation;

        if (snapToTarget)
        {
            SnapToTarget();
        }
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        velocity = Vector3.zero;
        transform.position = target.position + offset;
        transform.rotation = fixedRotation;
    }
}
