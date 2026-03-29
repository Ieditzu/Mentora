using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class TubeLiftPlatform : MonoBehaviour
{
    private const float RuntimeSpeedMultiplier = 2.5f;

    public float bottomY = 0.12f;
    public float topY = 8f;
    public float riseSpeed = 6.1f;
    public float descendSpeed = 7.5f;
    [FormerlySerializedAs("chargeSeconds")]
    public float activationHoldSeconds = 1f;
    public float chargeSeconds
    {
        get => activationHoldSeconds;
        set => activationHoldSeconds = Mathf.Max(0f, value);
    }
    public float topHoldSeconds = 1.5f;
    public Vector3 passengerDetectionPadding = new Vector3(0.55f, 0.9f, 0.55f);

    private enum LiftState
    {
        BottomIdle,
        MovingUp,
        AtTopHold,
        MovingDown
    }

    private LiftState state = LiftState.BottomIdle;
    private Collider platformCollider;
    private Material runtimeMaterial;
    private float holdTimer;
    private float topHoldTimer;
    private bool hasPassenger;

    private readonly Color baseColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    private readonly Color effectColor = new Color(0.5f, 0.95f, 1f, 1f);
    private readonly Collider[] overlapResults = new Collider[32];
    private readonly HashSet<BeanController> spherePassengers = new HashSet<BeanController>();
    private readonly HashSet<FirstPersonControllerSimple> fpsPassengers = new HashSet<FirstPersonControllerSimple>();

    private void Awake()
    {
        platformCollider = GetComponent<Collider>();
        activationHoldSeconds = Mathf.Max(0f, activationHoldSeconds);
        bottomY = Mathf.Max(bottomY, transform.position.y);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            runtimeMaterial = renderer.material;
            if (runtimeMaterial != null && runtimeMaterial.HasProperty("_EmissionColor"))
            {
                runtimeMaterial.EnableKeyword("_EMISSION");
                runtimeMaterial.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void Update()
    {
        UpdatePassengers();
        float dt = Time.deltaTime * RuntimeSpeedMultiplier;
        float previousY = transform.position.y;

        switch (state)
        {
            case LiftState.BottomIdle:
                SetEffectVisual(0f);
                if (hasPassenger)
                {
                    holdTimer += dt;
                    if (holdTimer >= activationHoldSeconds)
                    {
                        holdTimer = 0f;
                        state = LiftState.MovingUp;
                    }
                }
                else
                {
                    holdTimer = 0f;
                }
                break;

            case LiftState.MovingUp:
                MoveToY(topY, riseSpeed * dt);
                if (Mathf.Abs(transform.position.y - topY) < 0.001f)
                {
                    topHoldTimer = topHoldSeconds;
                    state = LiftState.AtTopHold;
                }
                break;

            case LiftState.AtTopHold:
                topHoldTimer -= dt;
                if (topHoldTimer <= 0f)
                {
                    state = LiftState.MovingDown;
                }
                break;

            case LiftState.MovingDown:
                MoveToY(bottomY, descendSpeed * dt);
                if (Mathf.Abs(transform.position.y - bottomY) < 0.001f)
                {
                    state = LiftState.BottomIdle;
                }
                break;
        }

        float deltaY = transform.position.y - previousY;
        if (Mathf.Abs(deltaY) > 0.00001f)
        {
            MovePassengersVertical(deltaY);
        }
    }

    private void UpdatePassengers()
    {
        spherePassengers.Clear();
        fpsPassengers.Clear();
        hasPassenger = false;

        if (platformCollider == null)
        {
            return;
        }

        Bounds bounds = platformCollider.bounds;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.05f, bounds.extents.x + passengerDetectionPadding.x),
            Mathf.Max(0.05f, passengerDetectionPadding.y * 0.5f),
            Mathf.Max(0.05f, bounds.extents.z + passengerDetectionPadding.z));
        Vector3 center = new Vector3(bounds.center.x, bounds.max.y + halfExtents.y, bounds.center.z);

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapResults,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            BeanController sphere = hit.GetComponentInParent<BeanController>();
            if (sphere != null)
            {
                RegisterSpherePassenger(sphere);
                continue;
            }

            FirstPersonControllerSimple fps = hit.GetComponentInParent<FirstPersonControllerSimple>();
            if (fps != null)
            {
                RegisterFpsPassenger(fps);
            }
        }

        // Fallback for cases where overlap misses CharacterController intermittently.
        if (!hasPassenger)
        {
            TryRegisterFallbackPassenger(bounds);
        }
    }

    private void RegisterSpherePassenger(BeanController sphere)
    {
        if (sphere == null)
        {
            return;
        }

        hasPassenger = true;
        spherePassengers.Add(sphere);
    }

    private void RegisterFpsPassenger(FirstPersonControllerSimple fps)
    {
        if (fps == null)
        {
            return;
        }

        hasPassenger = true;
        fpsPassengers.Add(fps);
    }

    private void TryRegisterFallbackPassenger(Bounds platformBounds)
    {
        FirstPersonControllerSimple fps = Object.FindObjectOfType<FirstPersonControllerSimple>();
        if (fps != null && IsTransformOverPlatform(fps.transform, platformBounds, 0.9f))
        {
            RegisterFpsPassenger(fps);
            return;
        }

        BeanController sphere = Object.FindObjectOfType<BeanController>();
        if (sphere != null && IsTransformOverPlatform(sphere.transform, platformBounds, 1.0f))
        {
            RegisterSpherePassenger(sphere);
        }
    }

    private void MovePassengersVertical(float deltaY)
    {
        Vector3 delta = new Vector3(0f, deltaY, 0f);

        foreach (var fps in fpsPassengers)
        {
            if (fps == null)
            {
                continue;
            }

            fps.AddExternalDisplacement(delta);
        }

        foreach (var sphere in spherePassengers)
        {
            if (sphere == null)
            {
                continue;
            }

            Rigidbody sphereRb = sphere.GetComponent<Rigidbody>();
            if (sphereRb != null)
            {
                sphereRb.position += delta;
            }
            else
            {
                sphere.transform.position += delta;
            }
        }
    }

    private static bool IsTransformOverPlatform(Transform target, Bounds platformBounds, float maxHeightAboveTop)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 p = target.position;
        float minX = platformBounds.min.x - 0.35f;
        float maxX = platformBounds.max.x + 0.35f;
        float minZ = platformBounds.min.z - 0.35f;
        float maxZ = platformBounds.max.z + 0.35f;
        if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ)
        {
            return false;
        }

        float topY = platformBounds.max.y;
        return p.y >= topY - 0.35f && p.y <= topY + maxHeightAboveTop;
    }

    private void MoveToY(float targetY, float maxStep)
    {
        Vector3 current = transform.position;
        float clampedTargetY = Mathf.Max(bottomY, targetY);
        float nextY = Mathf.MoveTowards(current.y, clampedTargetY, maxStep);
        nextY = Mathf.Max(bottomY, nextY);
        transform.position = new Vector3(current.x, nextY, current.z);
    }

    private void SetEffectVisual(float normalized)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            Color tint = Color.Lerp(baseColor, new Color(0.2f, 0.28f, 0.34f, 1f), normalized * 0.6f);
            runtimeMaterial.SetColor("_BaseColor", tint);
        }

        if (runtimeMaterial.HasProperty("_EmissionColor"))
        {
            float pulse = 0.55f + (Mathf.Sin(Time.time * 10f) * 0.45f);
            runtimeMaterial.SetColor("_EmissionColor", effectColor * normalized * pulse * 1.35f);
        }
    }

    private void OnDisable() {}
}
