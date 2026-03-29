//test
using UnityEngine;

public class TubeLiftZone : MonoBehaviour
{
    public float liftAcceleration = 16f;
    public float centerPull = 4.5f;
    public float travelAcceleration = 10f;
    public float orbitSpeed = 9f;
    public float verticalSpeed = 5.8f;
    public float exitSpeed = 12f;
    public float maxHeight = 5.5f;
    public Vector3 exitTarget;

    private int sphereInsideCount;

    private void OnTriggerEnter(Collider other)
    {
        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        sphereInsideCount++;
        sphere.SetMovementLocked(true);
    }

    private void OnTriggerStay(Collider other)
    {
        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        var rb = sphere.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        Vector3 center = transform.position;
        Vector3 radial = new Vector3(rb.position.x - center.x, 0f, rb.position.z - center.z);
        Vector3 radialDir = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.right;
        Vector3 tangentDir = Vector3.Cross(Vector3.up, radialDir).normalized;

        Vector3 desiredVelocity;
        if (rb.position.y < maxHeight)
        {
            // Helical auto movement while climbing inside the tube.
            desiredVelocity = (tangentDir * orbitSpeed) + (Vector3.up * verticalSpeed);
        }
        else
        {
            // At top, automatically transport to the far platform.
            Vector3 toExit = exitTarget - rb.position;
            Vector3 horizontalToExit = new Vector3(toExit.x, 0f, toExit.z);
            Vector3 exitDir = horizontalToExit.sqrMagnitude > 0.01f ? horizontalToExit.normalized : Vector3.zero;
            desiredVelocity = (exitDir * exitSpeed) + Vector3.up * 0.25f;
        }

        rb.velocity = Vector3.Lerp(rb.velocity, desiredVelocity, 0.28f);
        rb.angularVelocity = Vector3.zero;

        // Keep centered in tube.
        Vector3 horizontalOffset = new Vector3(center.x - rb.position.x, 0f, center.z - rb.position.z);
        rb.AddForce(horizontalOffset * centerPull, ForceMode.Acceleration);
    }

    private void OnTriggerExit(Collider other)
    {
        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        sphereInsideCount = Mathf.Max(0, sphereInsideCount - 1);
        if (sphereInsideCount == 0)
        {
            sphere.SetMovementLocked(false);
        }
    }
}

