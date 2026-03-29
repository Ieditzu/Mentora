using UnityEngine;

/// <summary>
/// Keeps the robot continuously facing the player (bean or FPS) on the horizontal plane.
/// </summary>
public class RobotLookAt : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 6f;

    private Transform target;

    private void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = ResolvePlayer();
            if (target == null) return;
        }

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotateSpeed * Time.deltaTime);
    }

    private Transform ResolvePlayer()
    {
        return PlayerCache.ResolvePlayerTransform();
    }
}
