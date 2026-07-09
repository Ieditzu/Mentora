using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RocketController : MonoBehaviour
{
    public Transform cameraTransform;
    public ParticleSystem thrustParticles;
    public AudioSource boosterAudio;
    public float boosterFadeIn = 0.08f;
    public float boosterFadeOut = 0.35f;
    public float thrustForce = 25f;
    public float boosterMinPitch = 0.9f;
    public float boosterMaxPitch = 1.35f;
    public float pitchAtSpeed = 80f;
    public Key superBoostKey = Key.LeftShift;
    public ParticleSystem chargeParticles;
    public AudioSource chargeAudio;
    public float boostChargeTime = 0.5f;
    public ParticleSystem boostParticles;
    public AudioSource boostAudio;
    public float boostDuration = 1.5f;
    public float boostAcceleration = 80f;
    public float boostCooldown = 2f;
    public float turnSpeed = 90f;
    public bool invertPitch;
    public bool invertYaw;
    public bool zeroGravity = true;
    public bool alignHeadingToCamera;
    public bool alignAllAxes;
    public bool mouseFreeLook = true;
    public bool cameraFreeByDefault;
    public bool chainLatchSteering = true;
    public bool perAxisLatch;
    public bool cameraRelativeYaw = true;
    public bool tipThroughCenter = true;
    public bool latchYawAxisWhileHeld = true;
    public bool latchTurnAxis;

    private RocketCameraController camController;
    private Rigidbody rb;
    private Vector3 noseDir;
    private Vector3 latchedForward;
    private Vector3 latchedRight;
    private Vector3 latchedYawAxis;
    private float latchedYawSign = 1f;
    private Vector3 pressYawAxis;
    private bool yawWasHeldForAxis;
    private float prevCamYaw;
    private bool camYawInitialized;
    private Quaternion prevCamRot;
    private bool camRotInitialized;
    private bool wasSteering;
    private bool wasPitching;
    private bool wasYawing;
    private bool thrustLocked;
    private bool wasThrusting;
    private bool superBoosting;
    private float nextBoostReadyTime;
    private Coroutine chargeRoutine;
    private float boosterMaxVolume = 1f;

    public bool IsThrusting { get; private set; }

    public float CurrentSpeed => rb != null ? rb.velocity.magnitude : 0f;

    public bool IsBoosting => superBoosting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = !zeroGravity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            camController = cameraTransform.GetComponent<RocketCameraController>();
        }

        noseDir = transform.up;
        if (boosterAudio != null)
        {
            boosterMaxVolume = boosterAudio.volume;
            boosterAudio.volume = 0f;
        }
    }

    public void ResetTo(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        noseDir = rotation * Vector3.up;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void SyncOrientationToTransform()
    {
        noseDir = transform.up;
    }

    public void LockThrustUntilRelease()
    {
        thrustLocked = true;
    }

    private IEnumerator SuperBoostRoutine()
    {
        if (chargeParticles != null)
        {
            chargeParticles.Play();
        }

        if (chargeAudio != null)
        {
            chargeAudio.Play();
        }

        yield return new WaitForSeconds(boostChargeTime);

        if (chargeParticles != null)
        {
            chargeParticles.Stop();
        }

        if (boostParticles != null)
        {
            boostParticles.Play();
        }

        if (boostAudio != null)
        {
            if (boostAudio.clip != null)
            {
                boostAudio.PlayOneShot(boostAudio.clip);
            }
            else
            {
                boostAudio.Play();
            }
        }

        superBoosting = true;
        yield return new WaitForSeconds(boostDuration);
        superBoosting = false;

        if (boostParticles != null)
        {
            boostParticles.Stop();
        }

        nextBoostReadyTime = Time.time + boostCooldown;
        chargeRoutine = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool thrustPressed = keyboard?.spaceKey.isPressed ?? false;
        if (thrustLocked && !thrustPressed)
        {
            thrustLocked = false;
        }

        if (keyboard != null &&
            superBoostKey != Key.None &&
            keyboard[superBoostKey].wasPressedThisFrame &&
            !superBoosting &&
            chargeRoutine == null &&
            Time.time >= nextBoostReadyTime)
        {
            chargeRoutine = StartCoroutine(SuperBoostRoutine());
        }

        bool nowThrusting = (IsThrusting = (thrustPressed && !thrustLocked) || superBoosting);
        if (nowThrusting && !wasThrusting)
        {
            if (thrustParticles != null)
            {
                thrustParticles.Play();
            }
        }
        else if (!nowThrusting && wasThrusting && thrustParticles != null)
        {
            thrustParticles.Stop();
        }

        wasThrusting = nowThrusting;
        if (boosterAudio != null)
        {
            if (nowThrusting && !boosterAudio.isPlaying)
            {
                boosterAudio.Play();
            }

            float targetVolume = nowThrusting ? boosterMaxVolume : 0f;
            float fadeDuration = nowThrusting ? boosterFadeIn : boosterFadeOut;
            if (fadeDuration <= 0f)
            {
                boosterAudio.volume = targetVolume;
            }
            else
            {
                boosterAudio.volume = Mathf.MoveTowards(
                    boosterAudio.volume,
                    targetVolume,
                    boosterMaxVolume / fadeDuration * Time.unscaledDeltaTime);
            }

            if (!nowThrusting && boosterAudio.volume <= 0.001f && boosterAudio.isPlaying)
            {
                boosterAudio.Stop();
            }

            float speedT = Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, pitchAtSpeed));
            boosterAudio.pitch = Mathf.Lerp(boosterMinPitch, boosterMaxPitch, speedT);
        }
    }

    private void FixedUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        float yaw = 0f;
        float pitch = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) yaw -= 1f;
            if (keyboard.dKey.isPressed) yaw += 1f;
            if (keyboard.sKey.isPressed) pitch -= 1f;
            if (keyboard.wKey.isPressed) pitch += 1f;
        }

        if (invertPitch)
        {
            pitch = -pitch;
        }

        if (invertYaw)
        {
            yaw = -yaw;
        }

        bool pitching = Mathf.Abs(pitch) > 0.0001f;
        bool yawing = Mathf.Abs(yaw) > 0.0001f;
        bool steering = pitching || yawing;

        if (yawing && !wasYawing)
        {
            if (cameraRelativeYaw)
            {
                Vector3 flatNose = noseDir;
                flatNose.y = 0f;
                Vector3 flatCameraForward = cameraTransform.forward;
                flatCameraForward.y = 0f;
                latchedYawSign = Vector3.Dot(flatNose, flatCameraForward) >= 0f ? 1f : -1f;
            }
            else
            {
                latchedYawSign = 1f;
            }
        }

        if (!chainLatchSteering)
        {
            CaptureForward();
            CaptureRight();
        }
        else if (perAxisLatch)
        {
            if (pitching && !wasPitching)
            {
                CaptureRight();
            }

            if (yawing && !wasYawing)
            {
                CaptureForward();
                latchedYawAxis = ComputeYawAxis(noseDir, latchedForward);
            }
        }
        else if (steering && !wasSteering)
        {
            CaptureForward();
            CaptureRight();
            latchedYawAxis = ComputeYawAxis(noseDir, latchedForward);
        }

        wasSteering = steering;
        wasPitching = pitching;
        wasYawing = yawing;

        if (yawing && !yawWasHeldForAxis)
        {
            pressYawAxis = ComputeYawAxis(noseDir, latchedForward);
        }

        yawWasHeldForAxis = yawing;
        if (steering)
        {
            float turnAmount = turnSpeed * Time.fixedDeltaTime;
            if (Mathf.Abs(pitch) > 0.0001f)
            {
                noseDir = Quaternion.AngleAxis(pitch * turnAmount, latchedRight) * noseDir;
            }

            if (Mathf.Abs(yaw) > 0.0001f)
            {
                Vector3 yawAxis = latchYawAxisWhileHeld
                    ? pressYawAxis
                    : (!latchTurnAxis ? ComputeYawAxis(noseDir, latchedForward) : latchedYawAxis);
                if (yawAxis.sqrMagnitude > 1E-05f)
                {
                    noseDir = Quaternion.AngleAxis(yaw * turnAmount, yawAxis.normalized) * noseDir;
                }
            }

            noseDir.Normalize();
        }

        bool leftMouseHeld = Mouse.current != null &&
                             Mouse.current.leftButton.isPressed &&
                             Cursor.lockState == CursorLockMode.Locked;
        bool freeLook = mouseFreeLook && (cameraFreeByDefault ? !leftMouseHeld : leftMouseHeld);
        bool followHeading = camController != null && camController.followHeading;
        bool alignHeading = alignHeadingToCamera && !followHeading;

        if (alignHeading && alignAllAxes)
        {
            if (camRotInitialized && !freeLook)
            {
                Quaternion cameraDelta = cameraTransform.rotation * Quaternion.Inverse(prevCamRot);
                noseDir = (cameraDelta * noseDir).normalized;
            }

            prevCamRot = cameraTransform.rotation;
            camRotInitialized = true;
            camYawInitialized = false;
        }
        else if (alignHeading)
        {
            Vector3 flatCameraForward = cameraTransform.forward;
            flatCameraForward.y = 0f;
            if (flatCameraForward.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Mathf.Atan2(flatCameraForward.x, flatCameraForward.z) * Mathf.Rad2Deg;
                if (camYawInitialized && !freeLook)
                {
                    float angle = Mathf.DeltaAngle(prevCamYaw, targetYaw);
                    noseDir = (Quaternion.AngleAxis(angle, Vector3.up) * noseDir).normalized;
                }

                prevCamYaw = targetYaw;
                camYawInitialized = true;
            }

            camRotInitialized = false;
        }
        else
        {
            camYawInitialized = false;
            camRotInitialized = false;
        }

        rb.MoveRotation(Quaternion.FromToRotation(Vector3.up, noseDir));
        rb.angularVelocity = Vector3.zero;

        if (keyboard != null && keyboard.spaceKey.isPressed && !thrustLocked)
        {
            rb.AddForce(transform.up * thrustForce, ForceMode.Force);
        }

        if (superBoosting)
        {
            rb.AddForce(transform.up * boostAcceleration, ForceMode.Acceleration);
        }
    }

    private void CaptureForward()
    {
        latchedForward = cameraTransform.forward;
        latchedForward.y = 0f;
        latchedForward.Normalize();
    }

    private void CaptureRight()
    {
        latchedRight = cameraTransform.right;
        latchedRight.y = 0f;
        latchedRight.Normalize();
    }

    private Vector3 ComputeYawAxis(Vector3 nose, Vector3 forward)
    {
        float upDot = Vector3.Dot(nose, Vector3.up);
        float blend = Mathf.Abs(upDot);
        Vector3 throughCenter = -Mathf.Sign(upDot) * forward;
        Vector3 yawAxis = Vector3.Lerp(Vector3.up * latchedYawSign, throughCenter, blend);
        if (tipThroughCenter)
        {
            yawAxis -= Vector3.Dot(yawAxis, nose) * nose;
        }

        return yawAxis;
    }
}

[RequireComponent(typeof(Rigidbody))]
public class RocketAerodynamics : MonoBehaviour
{
    public Rigidbody body;
    public Vector3 noseAxisLocal = Vector3.up;
    public float liftStrength = 0.02f;
    public float minSpeed = 6f;
    [Range(1f, 2f)] public float speedPower = 2f;
    public float maxLiftAccel = 50f;
    public float lateralDrag = 1f;
    public float axialDrag;
    public bool bidirectional = true;
    public bool enabledAero = true;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
    }

    private void FixedUpdate()
    {
        if (!enabledAero || body == null)
        {
            return;
        }

        Vector3 velocity = body.velocity;
        float speed = velocity.magnitude;
        if (speed < 0.01f)
        {
            return;
        }

        Vector3 velocityDir = velocity / speed;
        Vector3 noseDir = transform.TransformDirection(noseAxisLocal).normalized;
        Vector3 effectiveNose = noseDir;
        if (bidirectional && Vector3.Dot(noseDir, velocity) < 0f)
        {
            effectiveNose = -noseDir;
        }

        Vector3 liftDir = effectiveNose - Vector3.Dot(effectiveNose, velocityDir) * velocityDir;
        float speedFactor = Mathf.Pow(Mathf.Max(0f, speed - minSpeed), speedPower);
        Vector3 liftAccel = Vector3.ClampMagnitude(liftDir * (liftStrength * speedFactor), maxLiftAccel);
        body.AddForce(liftAccel, ForceMode.Acceleration);

        Vector3 axialVelocity = Vector3.Dot(velocity, effectiveNose) * effectiveNose;
        Vector3 lateralVelocity = velocity - axialVelocity;
        if (lateralDrag > 0f)
        {
            body.AddForce(-lateralVelocity * lateralDrag, ForceMode.Acceleration);
        }

        if (axialDrag > 0f)
        {
            body.AddForce(-axialVelocity * axialDrag, ForceMode.Acceleration);
        }
    }
}

public class FuelUsage : MonoBehaviour
{
    public RocketController rocket;
    public float thrustBurnRate = 1f;
    public float boostBurnRate = 3f;
    public float FuelUsed { get; private set; }

    private void Awake()
    {
        if (rocket == null)
        {
            rocket = GetComponent<RocketController>();
        }
    }

    private void Update()
    {
        if (rocket == null)
        {
            return;
        }

        float delta = Time.deltaTime;
        if (rocket.IsThrusting)
        {
            FuelUsed += thrustBurnRate * delta;
        }

        if (rocket.IsBoosting)
        {
            FuelUsed += boostBurnRate * delta;
        }
    }

    public void ResetFuel()
    {
        FuelUsed = 0f;
    }
}

public class RocketCameraController : MonoBehaviour
{
    public bool isOnLauncher;
    public Vector3 launcherPositionOffset = Vector3.zero;
    public Vector3 launcherRotationOffset = Vector3.zero;
    public bool followHeading;
    public Key toggleKey = Key.C;
    public Transform target;
    public Vector3 lookOffset = new Vector3(0f, 1f, 0f);
    public float smoothTime = 0.2f;
    public float distance = 10f;
    public float minDistance = 4f;
    public float maxDistance = 25f;
    public float zoomSpeed = 0.01f;
    public float zoomSmoothTime = 0.08f;
    public float yawSpeed = 0.1f;
    public float pitchSpeed = 0.1f;
    public float minPitch = -20f;
    public float maxPitch = 80f;
    [Range(0.01f, 1f)] public float slowMoTimeScale = 0.25f;
    public float slowMoRampIn = 0.12f;
    public float slowMoRampOut = 0.2f;
    public float followPitchAngle = 40f;
    public float yawSmoothTime = 0.3f;
    public float headingDeadzone = 0.15f;
    public bool leadCamera;
    public Key leadToggleKey;
    public float leadDistance = 4f;
    public float turnLag = 0.35f;
    public float swayAmount = 1.2f;

    private float currentYaw;
    private float currentPitch = 20f;
    private float yawVel;
    private float targetDistance;
    private float zoomVel;
    private Vector3 currentSmoothedPosition;
    private Vector3 positionVelocity;
    private bool prevFollowHeading;
    private Vector3 smoothedForward;
    private Vector3 leadVel;
    private bool leadInitialized;
    private float defaultFixedDelta;
    private float currentTimeScale = 1f;

    private void Start()
    {
        if (target != null)
        {
            currentSmoothedPosition = target.position;
        }

        prevFollowHeading = followHeading;
        defaultFixedDelta = Time.fixedDeltaTime;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        distance = Mathf.Clamp(PlayerPrefs.GetFloat("settings.zoom", distance), minDistance, maxDistance);
        targetDistance = distance;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
        {
            followHeading = !followHeading;
        }

        if (keyboard != null && leadToggleKey != Key.None && keyboard[leadToggleKey].wasPressedThisFrame)
        {
            leadCamera = !leadCamera;
        }

        if (followHeading != prevFollowHeading)
        {
            prevFollowHeading = followHeading;
        }

        if (!isOnLauncher && mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            currentYaw += delta.x * yawSpeed;
            currentPitch -= delta.y * pitchSpeed;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                targetDistance = Mathf.Clamp(targetDistance - scrollY * zoomSpeed, minDistance, maxDistance);
                PlayerPrefs.SetFloat("settings.zoom", targetDistance);
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        distance = Mathf.SmoothDamp(distance, targetDistance, ref zoomVel, zoomSmoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
        HandleSlowMo(mouse);
    }

    private void HandleSlowMo(Mouse mouse)
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        bool slowPressed = mouse != null && mouse.rightButton.isPressed;
        float targetTimeScale = slowPressed ? slowMoTimeScale : 1f;
        float ramp = slowPressed ? slowMoRampIn : slowMoRampOut;
        currentTimeScale = Mathf.MoveTowards(currentTimeScale, targetTimeScale, 1f / Mathf.Max(ramp, 0.0001f) * Time.unscaledDeltaTime);
        Time.timeScale = currentTimeScale;
        Time.fixedDeltaTime = defaultFixedDelta * currentTimeScale;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (isOnLauncher)
        {
            Quaternion targetRotation = target.rotation * Quaternion.Euler(launcherRotationOffset);
            currentYaw = targetRotation.eulerAngles.y;
            float pitchAngle = targetRotation.eulerAngles.x;
            if (pitchAngle > 180f)
            {
                pitchAngle -= 360f;
            }

            currentPitch = Mathf.Clamp(pitchAngle, minPitch, maxPitch);
            currentSmoothedPosition = target.position;
            Vector3 lookPoint = target.position + lookOffset;
            Vector3 offset = targetRotation * launcherPositionOffset;
            transform.position = lookPoint - targetRotation * Vector3.forward * distance + offset;
            transform.rotation = targetRotation;
            return;
        }

        if (leadCamera)
        {
            HeavyLeadUpdate();
            return;
        }

        currentSmoothedPosition = Vector3.SmoothDamp(currentSmoothedPosition, target.position, ref positionVelocity, smoothTime);
        Vector3 focusPoint = currentSmoothedPosition + lookOffset;
        if (followHeading)
        {
            Vector3 flatUp = target.up;
            flatUp.y = 0f;
            if (flatUp.sqrMagnitude > headingDeadzone * headingDeadzone)
            {
                float headingYaw = Mathf.Atan2(flatUp.x, flatUp.z) * Mathf.Rad2Deg;
                currentYaw = Mathf.SmoothDampAngle(currentYaw, headingYaw, ref yawVel, yawSmoothTime);
            }

            Quaternion followRotation = Quaternion.Euler(followPitchAngle, currentYaw, 0f);
            transform.position = focusPoint - followRotation * Vector3.forward * distance;
            transform.rotation = followRotation;
        }
        else
        {
            Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            transform.position = focusPoint - orbitRotation * Vector3.forward * distance;
            transform.rotation = Quaternion.LookRotation(focusPoint - transform.position);
        }
    }

    private void HeavyLeadUpdate()
    {
        Vector3 targetForward = target.up;
        if (targetForward.sqrMagnitude < 1E-06f)
        {
            targetForward = transform.forward;
        }

        targetForward.Normalize();
        if (!leadInitialized)
        {
            smoothedForward = targetForward;
            leadInitialized = true;
        }

        smoothedForward = Vector3.SmoothDamp(smoothedForward, targetForward, ref leadVel, Mathf.Max(0.0001f, turnLag));
        if (smoothedForward.sqrMagnitude < 1E-06f)
        {
            smoothedForward = targetForward;
        }

        smoothedForward.Normalize();
        currentSmoothedPosition = Vector3.SmoothDamp(currentSmoothedPosition, target.position, ref positionVelocity, smoothTime);
        Vector3 focusPoint = currentSmoothedPosition + lookOffset;
        Vector3 flatHeading = smoothedForward;
        flatHeading.y = 0f;
        if (flatHeading.sqrMagnitude < 0.0001f)
        {
            flatHeading = new Vector3(transform.forward.x, 0f, transform.forward.z);
        }

        flatHeading.Normalize();
        float leadYaw = Mathf.Atan2(flatHeading.x, flatHeading.z) * Mathf.Rad2Deg;
        Quaternion cameraRotation = Quaternion.Euler(followPitchAngle, leadYaw, 0f);
        Vector3 cameraPosition = focusPoint - cameraRotation * Vector3.forward * distance;

        Vector3 targetFlatForward = targetForward;
        targetFlatForward.y = 0f;
        if (targetFlatForward.sqrMagnitude > 0.0001f)
        {
            targetFlatForward.Normalize();
            float trueYaw = Mathf.Atan2(targetFlatForward.x, targetFlatForward.z) * Mathf.Rad2Deg;
            float sway = Mathf.Clamp(Mathf.DeltaAngle(leadYaw, trueYaw) / 30f, -1f, 1f) * swayAmount;
            cameraPosition += cameraRotation * Vector3.right * sway;
        }

        Vector3 leadLookPoint = focusPoint + targetForward * leadDistance;
        transform.position = cameraPosition;
        transform.rotation = Quaternion.LookRotation(leadLookPoint - cameraPosition, Vector3.up);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;
    }
}

public class RocketWinglets : MonoBehaviour
{
    public Transform finX;
    public Transform finZ;
    public float maxDeflection = 25f;
    public float sensitivity = 0.3f;
    public float smoothSpeed = 12f;
    public bool invertX;
    public bool invertZ;

    private Quaternion finXRest;
    private Quaternion finZRest;
    private Quaternion prevRot;
    private float curX;
    private float curZ;

    private void Start()
    {
        if (finX != null)
        {
            finXRest = finX.localRotation;
        }

        if (finZ != null)
        {
            finZRest = finZ.localRotation;
        }

        prevRot = transform.rotation;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 1E-05f);
        Quaternion delta = transform.rotation * Quaternion.Inverse(prevRot);
        prevRot = transform.rotation;
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (float.IsInfinity(axis.x) || axis == Vector3.zero)
        {
            axis = Vector3.zero;
        }

        Vector3 localAngular = transform.InverseTransformDirection(axis.normalized * (angle / dt));
        float invertXFactor = invertX ? -1f : 1f;
        float invertZFactor = invertZ ? -1f : 1f;
        float targetX = Mathf.Clamp(localAngular.x * sensitivity * invertXFactor, -maxDeflection, maxDeflection);
        float targetZ = Mathf.Clamp(localAngular.z * sensitivity * invertZFactor, -maxDeflection, maxDeflection);
        float smoothT = 1f - Mathf.Exp(-smoothSpeed * dt);
        curX = Mathf.Lerp(curX, targetX, smoothT);
        curZ = Mathf.Lerp(curZ, targetZ, smoothT);

        if (finX != null)
        {
            finX.localRotation = Quaternion.AngleAxis(curX, Vector3.right) * finXRest;
        }

        if (finZ != null)
        {
            finZ.localRotation = Quaternion.AngleAxis(curZ, Vector3.forward) * finZRest;
        }
    }
}
