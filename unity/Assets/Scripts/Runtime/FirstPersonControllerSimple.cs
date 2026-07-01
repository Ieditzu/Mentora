using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonControllerSimple : MonoBehaviour
{
    private const float MaxJumpValue = 10f;
    private const string LeftTouchPlusResourcePath = "MetaQuestTouchPlus/MetaQuestTouchPlus_Left";
    private const string RightTouchPlusResourcePath = "MetaQuestTouchPlus/MetaQuestTouchPlus_Right";
    private static readonly Vector3 TouchPlusModelLocalPosition = new Vector3(0f, -0.03f, -0.04f);
    private static readonly Quaternion TouchPlusModelLocalRotation = Quaternion.Euler(30f, 0f, 0f);

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 3.2f;
    [SerializeField] private float temporaryBoostMultiplier = 4f;
    [SerializeField] private float temporaryBoostDuration = 5f;
    [SerializeField] private float jumpVelocity = 7f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float gravity = 9.81f;
    [Header("Physics Interaction")]
    [SerializeField] private float pushForce = 8f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 12f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [Tooltip("Optional anchor (e.g., head bone) to position the camera. If null, a local offset is used.")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 3.8f, -5.4f);
    [Header("Visuals")]
    [Tooltip("If no renderers are found under this player, spawn a simple capsule proxy so the body is visible in third-person.")]
    [SerializeField] private bool spawnProxyBodyIfMissing = false;
    [Tooltip("Optional character prefab (e.g., PolygonStarter FBX) to auto-spawn as the visible body if none is present.")]
    [SerializeField] private GameObject characterPrefab;
    [Header("Camera")]
    [SerializeField] private float eyeHeight = 4.0f;
    [SerializeField] private float bodyVisualScale = 8f;
    [SerializeField] private float baseControllerHeight = 1.8f;
    [SerializeField] private float vrNearClipPlane = 0.01f;
    [SerializeField] private float vrHeightScale = 1.35f;
    [SerializeField] private float vrMoveDeadzone = 0.15f;
    [SerializeField] private float vrTurnSpeed = 180f; // degrees per second using right stick X
    [SerializeField] private float vrTurnDeadzone = 0.18f;
    [SerializeField] private bool showVrControllers = true;
    [SerializeField] private Vector3 leftControllerLocalOffset = new Vector3(-0.01f, -0.015f, 0.045f);
    [SerializeField] private Vector3 rightControllerLocalOffset = new Vector3(0.01f, -0.015f, 0.045f);

    [Header("Controller Size")]
    [SerializeField] private float controllerHeight = 2.0f;
    [SerializeField] private float controllerRadius = 0.3f;
    [SerializeField] private float stepOffset = 1.0f;
    [SerializeField] private float slopeLimit = 65f;

    [Header("Respawn")]
    [SerializeField] private float fallRespawnY = -1000f;
    [SerializeField] private float waterDrownY = 0f;
    [SerializeField] private float drownDelay = 1.2f;
    [SerializeField] private float waterPullDown = 8f;
    [SerializeField] private bool useOverrideSpawn = true;
    [SerializeField] private Vector3 overrideSpawnPosition = new Vector3(74.26f, 11.421f, 241.7f);
    [SerializeField] private Quaternion overrideSpawnRotation = Quaternion.identity;

    [Header("Bindings")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private float doubleTapSprintWindow = 0.28f;

    private CharacterController controller;
    private Transform camTransform;
    private float pitch;
    private Vector3 velocity;
    private bool movementLocked;
    private bool hardFreeze;
    private bool cameraControlEnabled = true;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector3 pendingExternalDisplacement;
    private float drownTimer;
    private float lastForwardTapTime = -10f;
    private bool doubleTapSprintActive;
    private float jumpPower;
    private float lastGroundedTime = -10f;
    private bool wasGrounded;
    private float speedBoostUntilTime = -1f;
    private Vector2 lastTouchPos;
    private bool touchLookActive;
    private bool xrHeadOriginCaptured;
    private Vector3 xrHeadOriginLocalPosition;
    private Transform leftVrControllerVisual;
    private Transform rightVrControllerVisual;
    private bool handTrackingVisualsActive;

    private bool ShouldDriveVrControllerVisuals()
    {
        return showVrControllers && (IsVrConfigured() || HasConnectedOvrControllers());
    }

    public void SetHeadAnchor(Transform anchor)
    {
        headAnchor = anchor;
        if (camTransform != null && headAnchor != null && !IsVrConfigured())
        {
            camTransform.position = headAnchor.position;
        }
    }

    public void SetJumpVelocity(float newVelocity)
    {
        if (newVelocity > MaxJumpValue)
        {
            Debug.LogWarning("max value 10");
            return;
        }

        jumpVelocity = Mathf.Max(0f, newVelocity);
    }

    public float GetJumpVelocity()
    {
        return jumpVelocity;
    }

    public void RecalibrateVrTracking()
    {
        xrHeadOriginCaptured = false;
    }

    public float GetVrEyeHeight()
    {
        return eyeHeight;
    }

    public void SetHandTrackingVisualsActive(bool handTrackingActive)
    {
        handTrackingVisualsActive = handTrackingActive;

        if (showVrControllers)
        {
            EnsureVrControllerVisuals();
        }

        ApplyVrControllerVisibility();
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        PlayerCache.Register(this);
        ApplyControllerSize();
        if (useOverrideSpawn)
        {
            spawnPosition = overrideSpawnPosition;
            spawnRotation = overrideSpawnRotation;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
        else
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }
        Camera cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            GameObject camObj = new GameObject("FP_Camera");
            cam = camObj.AddComponent<Camera>();
        }
        camTransform = cam.transform;
        camTransform.SetParent(transform, false);
        camTransform.localPosition = new Vector3(0f, eyeHeight, 0f);
        camTransform.localRotation = Quaternion.identity;
        // If running in XR, scale height to feel taller in VR without changing non-VR builds.
        if (IsVrActive())
        {
            cam.nearClipPlane = Mathf.Clamp(vrNearClipPlane, 0.001f, 0.1f);
            eyeHeight *= vrHeightScale;
            controllerHeight *= vrHeightScale;
            bodyVisualScale *= vrHeightScale;
            ApplyControllerSize();
        }

        EnsureBodyVisible();
        EnsureVrControllerVisuals();

        HideCursor();
    }

    private void OnEnable()
    {
        PlayerCache.Register(this);
    }

    private void OnDisable()
    {
        PlayerCache.Unregister(this);
    }

    private void OnDestroy()
    {
        PlayerCache.Unregister(this);
    }

    private void Update()
    {
        CheckVrPauseButton();
        if (GetKeyDownCompat(KeyCode.CapsLock))
        {
            ToggleCursor();
        }

        if (GetKeyDownCompat(KeyCode.P))
        {
            speedBoostUntilTime = Time.time + temporaryBoostDuration;
        }

        if (transform.position.y < fallRespawnY)
        {
            RespawnAtSpawnPoint();
            return;
        }
        HandleWaterSubmersion();

        ApplyExternalDisplacement();

        if (hardFreeze)
        {
            velocity = Vector3.zero;
            return;
        }

        if (PauseMenuManager.IsGamePaused)
        {
            if (IsVrConfigured()) TryApplyXrHeadPose();
            if (ShouldDriveVrControllerVisuals()) UpdateVrControllerVisuals();
            ApplyGravityOnly();
            return;
        }

        if (CommunityIslandMenu.IsVrMenuActive)
        {
            velocity = Vector3.zero;
            if (IsVrConfigured())
            {
                TryApplyXrHeadPose();
            }

            if (ShouldDriveVrControllerVisuals())
            {
                UpdateVrControllerVisuals();
            }
            return;
        }

        if (cameraControlEnabled && !PauseMenuManager.IsGamePaused)
        {
            Look();
        }
        if (movementLocked)
        {
            ApplyGravityOnly();
            return;
        }

        Move();
        if (ShouldDriveVrControllerVisuals())
        {
            UpdateVrControllerVisuals();
        }
    }

    private void ToggleCursor()
    {
        // Derive from actual cursor state so we stay in sync with pause menu or other systems.
        bool shouldShow = Cursor.lockState != CursorLockMode.None || !Cursor.visible;
        if (shouldShow)
        {
            ShowCursor();
        }
        else
        {
            HideCursor();
        }
    }

    private void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetCameraControlEnabled(false);
    }

    private void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        SetCameraControlEnabled(true);
    }

    private void Look()
    {
        // In VR, drive the camera directly from the HMD pose and skip mouse-look.
        if (IsVrConfigured() && TryApplyXrHeadPose())
        {
            ApplyVrStickTurn();
            return;
        }

        Vector2 lookDelta = GetLookDeltaCompat();
        float mouseX = lookDelta.x;
        float mouseY = lookDelta.y;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (camTransform != null)
        {
            camTransform.localPosition = new Vector3(0f, eyeHeight, 0f);
            camTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        transform.Rotate(Vector3.up * mouseX);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.rigidbody;
        if (rb == null || rb.isKinematic) return;
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDir.sqrMagnitude < 0.0001f) return;
        rb.AddForce(pushDir.normalized * pushForce, ForceMode.Impulse);
    }

    private void Move()
    {
        bool grounded = ApplyGroundStick();
        UpdateDoubleTapSprint();

        float h = GetAxisRawCompat("Horizontal");
        float v = GetAxisRawCompat("Vertical");
        Vector2 touchMove = MobileTouchInput.Move;
        if (touchMove.sqrMagnitude > 0.0001f)
        {
            h = touchMove.x;
            v = touchMove.y;
        }
        else if (IsVrActive() && TryGetVrMoveAxis(out Vector2 vrMove))
        {
            h = vrMove.x;
            v = vrMove.y;
        }

        Vector3 input = (transform.right * h + transform.forward * v).normalized;
        if (IsVrActive() && camTransform != null)
        {
            Vector3 camF = camTransform.forward;
            camF.y = 0f;
            camF = camF.sqrMagnitude > 0.0001f ? camF.normalized : transform.forward;
            Vector3 camR = new Vector3(camF.z, 0f, -camF.x).normalized;
            input = (camR * h + camF * v).normalized;
        }

        float speed = moveSpeed;
        bool sprintHeld = GetKeyCompat(sprintKey) || IsVrSprintHeld();
        bool movingForward = v > 0.01f;
        if (sprintHeld || (doubleTapSprintActive && movingForward))
        {
            speed *= sprintMultiplier;
        }

        if (Time.time < speedBoostUntilTime)
        {
            speed *= temporaryBoostMultiplier;
        }

        controller.Move(input * speed * Time.deltaTime);

        bool jumpRequested = GetKeyDownCompat(jumpKey) || MobileTouchInput.ConsumeJumpRequest() || TryGetVrJumpRequest();
        bool canJump = grounded || (Time.time - lastGroundedTime) <= coyoteTime;
        if (jumpRequested && canJump)
        {
            velocity.y = Mathf.Max(0.05f, jumpVelocity + jumpPower);
            lastGroundedTime = -10f;
            AudioManager.Play(MenSfx.Jump);
        }

        velocity.y -= gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateDoubleTapSprint()
    {
        if (GetKeyDownCompat(forwardKey))
        {
            if (Time.time - lastForwardTapTime <= doubleTapSprintWindow)
            {
                doubleTapSprintActive = true;
            }

            lastForwardTapTime = Time.time;
        }

        if (GetKeyUpCompat(forwardKey) || GetAxisRawCompat("Vertical") <= 0.01f)
        {
            doubleTapSprintActive = false;
        }
    }

    private Vector2 GetLookDeltaCompat()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int skipId = MobileTouchInput.JoystickPointerId;
            TouchControl touch = null;
            foreach (TouchControl t in touchscreen.touches)
            {
                if (t.press.isPressed && t.touchId.ReadValue() != skipId)
                {
                    touch = t;
                    break;
                }
            }

            if (touch != null && touch.press.isPressed)
            {
                Vector2 pos = touch.position.ReadValue();
                if (!touchLookActive)
                {
                    touchLookActive = true;
                    lastTouchPos = pos;
                    return Vector2.zero;
                }

                Vector2 touchDelta = pos - lastTouchPos;
                lastTouchPos = pos;
                return touchDelta * (mouseSensitivity * 0.002f);
            }

            touchLookActive = false;
        }

        if (Mouse.current == null)
        {
            // Legacy mouse fallback
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            return new Vector2(mouseX, mouseY) * (mouseSensitivity * 0.02f);
        }

        Vector2 delta = Mouse.current.delta.ReadValue();
        return delta * (mouseSensitivity * 0.02f);
    }

    private static float GetAxisRawCompat(string axisName)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            // Legacy axis fallback (e.g., Standalone with old input only)
            return Input.GetAxisRaw(axisName);
        }

        switch (axisName)
        {
            case "Horizontal":
                float horizontal = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    horizontal -= 1f;
                }
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    horizontal += 1f;
                }
                return horizontal;
            case "Vertical":
                float vertical = 0f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    vertical -= 1f;
                }
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    vertical += 1f;
                }
                return vertical;
            default:
                return 0f;
        }
    }

    private static bool GetKeyCompat(KeyCode key)
    {
        ButtonControl control = GetKeyboardControl(key);
        if (control != null)
        {
            return control.isPressed;
        }

        return Input.GetKey(key);
    }

    private static bool GetKeyDownCompat(KeyCode key)
    {
        ButtonControl control = GetKeyboardControl(key);
        if (control != null)
        {
            return control.wasPressedThisFrame;
        }

        return Input.GetKeyDown(key);
    }

    private static bool GetKeyUpCompat(KeyCode key)
    {
        ButtonControl control = GetKeyboardControl(key);
        if (control != null)
        {
            return control.wasReleasedThisFrame;
        }

        return Input.GetKeyUp(key);
    }

    private static ButtonControl GetKeyboardControl(KeyCode key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return null;
        }

        switch (key)
        {
            case KeyCode.CapsLock:
                return keyboard.capsLockKey;
            case KeyCode.LeftShift:
                return keyboard.leftShiftKey;
            case KeyCode.RightShift:
                return keyboard.rightShiftKey;
            case KeyCode.Space:
                return keyboard.spaceKey;
            case KeyCode.P:
                return keyboard.pKey;
            case KeyCode.W:
                return keyboard.wKey;
            case KeyCode.A:
                return keyboard.aKey;
            case KeyCode.S:
                return keyboard.sKey;
            case KeyCode.D:
                return keyboard.dKey;
            case KeyCode.Escape:
                return keyboard.escapeKey;
            default:
                return null;
        }
    }

    private void EnsureBodyVisible()
    {
        if (HasRenderers())
        {
            return;
        }

        if (characterPrefab != null)
        {
            GameObject body = Instantiate(characterPrefab, transform);
            body.name = "FPS_Character";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * bodyVisualScale;
            // Keep controller size unchanged to avoid spawning higher; only scale visuals.
            SinkBodyToGround(body.transform);
            var animDriver = GetComponent<FirstPersonAnimatorDriver>();
            if (animDriver == null)
            {
                animDriver = gameObject.AddComponent<FirstPersonAnimatorDriver>();
            }
            Animator bodyAnimator = body.GetComponentInChildren<Animator>();
            animDriver.characterAnimator = bodyAnimator;
            // attempt to set head anchor from newly spawned model
            FirstPersonHeadBinder binder = GetComponent<FirstPersonHeadBinder>();
            if (binder != null)
            {
                binder.characterRoot = body.transform;
                binder.TryBind();
            }
            return;
        }

        if (!spawnProxyBodyIfMissing)
        {
            return;
        }

        GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        proxy.name = "FPS_ProxyBody";
        proxy.transform.SetParent(transform, false);
        proxy.transform.localPosition = new Vector3(0f, controller != null ? controller.height * 0.5f : 0.9f, 0f);
        proxy.transform.localScale = Vector3.one;
    }

    private void EnsureVrControllerVisuals()
    {
        if (!showVrControllers)
        {
            SetVrControllerVisualState(false);
            return;
        }

        if (leftVrControllerVisual == null)
        {
            leftVrControllerVisual = CreateVrControllerVisual("LeftVrControllerVisual", true);
        }

        if (rightVrControllerVisual == null)
        {
            rightVrControllerVisual = CreateVrControllerVisual("RightVrControllerVisual", false);
        }

        ApplyVrControllerVisibility();
    }

    private Transform CreateVrControllerVisual(string name, bool isLeft)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);

        if (TryCreateTouchPlusControllerModel(root.transform, isLeft))
        {
            return root.transform;
        }

        CreateFallbackControllerGeometry(root.transform, isLeft);
        return root.transform;
    }

    private static bool TryCreateTouchPlusControllerModel(Transform parent, bool isLeft)
    {
        string resourcePath = isLeft ? LeftTouchPlusResourcePath : RightTouchPlusResourcePath;
        GameObject modelPrefab = Resources.Load<GameObject>(resourcePath);
        if (modelPrefab == null)
        {
            return false;
        }

        GameObject modelInstance = Instantiate(modelPrefab, parent);
        modelInstance.name = modelPrefab.name;
        modelInstance.transform.localPosition = TouchPlusModelLocalPosition;
        modelInstance.transform.localRotation = TouchPlusModelLocalRotation;
        modelInstance.transform.localScale = Vector3.one;
        return true;
    }

    private static void CreateFallbackControllerGeometry(Transform parent, bool isLeft)
    {
        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = isLeft ? "LeftControllerGrip" : "RightControllerGrip";
        grip.transform.SetParent(parent, false);
        grip.transform.localPosition = new Vector3(0f, -0.015f, 0f);
        grip.transform.localRotation = Quaternion.identity;
        grip.transform.localScale = new Vector3(0.055f, 0.09f, 0.04f);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ring.name = isLeft ? "LeftControllerRing" : "RightControllerRing";
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.02f, 0.025f);
        ring.transform.localRotation = Quaternion.identity;
        ring.transform.localScale = new Vector3(0.09f, 0.06f, 0.09f);

        Collider gripCollider = grip.GetComponent<Collider>();
        if (gripCollider != null)
        {
            Destroy(gripCollider);
        }

        Collider ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null)
        {
            Destroy(ringCollider);
        }

        Material material = CreateFallbackControllerMaterial(isLeft);
        ApplyMaterial(grip, material);
        ApplyMaterial(ring, material);
    }

    private static Material CreateFallbackControllerMaterial(bool isLeft)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        Color color = isLeft ? new Color(0.22f, 0.72f, 0.95f) : new Color(0.95f, 0.78f, 0.22f);
        material.color = color;
        return material;
    }

    private static void ApplyMaterial(GameObject target, Material material)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void SetVrControllerVisualState(bool visible)
    {
        if (leftVrControllerVisual != null)
        {
            leftVrControllerVisual.gameObject.SetActive(visible);
        }

        if (rightVrControllerVisual != null)
        {
            rightVrControllerVisual.gameObject.SetActive(visible);
        }
    }

    private void UpdateVrControllerVisuals()
    {
        EnsureVrControllerVisuals();
        if (!showVrControllers || handTrackingVisualsActive)
        {
            return;
        }

        UpdateVrControllerVisual(XRNode.LeftHand, OVRInput.Controller.LTouch, leftVrControllerVisual, leftControllerLocalOffset);
        UpdateVrControllerVisual(XRNode.RightHand, OVRInput.Controller.RTouch, rightVrControllerVisual, rightControllerLocalOffset);
    }

    private void UpdateVrControllerVisual(XRNode node, OVRInput.Controller ovrController, Transform visual, Vector3 localOffset)
    {
        if (visual == null)
        {
            return;
        }

        XRInputDevice device = InputDevices.GetDeviceAtXRNode(node);
        Vector3 localPosition = Vector3.zero;
        Quaternion localRotation = Quaternion.identity;
        bool hasPosition = device.isValid && device.TryGetFeatureValue(XRCommonUsages.devicePosition, out localPosition);
        bool hasRotation = device.isValid && device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out localRotation);

        if ((!hasPosition && !hasRotation) && TryGetOvrControllerPose(ovrController, out Vector3 ovrLocalPosition, out Quaternion ovrLocalRotation))
        {
            localPosition = ovrLocalPosition;
            localRotation = ovrLocalRotation;
            hasPosition = true;
            hasRotation = true;
        }

        Vector3 calibratedPosition = GetDefaultControllerRestPosition(ovrController);
        if (hasPosition)
        {
            calibratedPosition = localPosition;
            if (xrHeadOriginCaptured)
            {
                Vector3 relativeToHeadOrigin = localPosition - xrHeadOriginLocalPosition;
                calibratedPosition = new Vector3(relativeToHeadOrigin.x, eyeHeight + relativeToHeadOrigin.y, relativeToHeadOrigin.z);
            }
        }

        visual.localPosition = calibratedPosition + (hasRotation ? localRotation * localOffset : localOffset);
        visual.localRotation = hasRotation ? localRotation : Quaternion.identity;
    }

    private void ApplyVrControllerVisibility()
    {
        bool shouldShow = showVrControllers
            && !handTrackingVisualsActive
            && (IsVrConfigured() || HasConnectedOvrControllers());
        SetVrControllerVisualState(shouldShow);
    }

    private Vector3 GetDefaultControllerRestPosition(OVRInput.Controller controllerMask)
    {
        float horizontalOffset = controllerMask == OVRInput.Controller.LTouch ? -0.22f : 0.22f;
        return new Vector3(horizontalOffset, eyeHeight - 0.18f, 0.35f);
    }

    private static bool HasConnectedOvrControllers()
    {
        OVRInput.Controller connectedControllers = OVRInput.GetConnectedControllers();
        return (connectedControllers & OVRInput.Controller.LTouch) != 0
            || (connectedControllers & OVRInput.Controller.RTouch) != 0;
    }

    private static bool TryGetOvrControllerPose(OVRInput.Controller controllerMask, out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;

        if ((OVRInput.GetConnectedControllers() & controllerMask) == 0)
        {
            return false;
        }

        localPosition = OVRInput.GetLocalControllerPosition(controllerMask);
        localRotation = OVRInput.GetLocalControllerRotation(controllerMask);
        return true;
    }

    private void SinkBodyToGround(Transform body)
    {
        if (body == null)
        {
            return;
        }

        SkinnedMeshRenderer smr = body.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            Bounds b = smr.bounds; // world space after scale
            float offsetY = -b.min.y;
            body.position += new Vector3(0f, offsetY, 0f);
        }
        else
        {
            // Fallback: drop by half visual height estimate
            float h = baseControllerHeight * bodyVisualScale;
            body.localPosition = new Vector3(0f, -h * 0.5f + 0.05f, 0f);
        }
    }

    private bool HasRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        return renderers != null && renderers.Length > 0;
    }

    // Helper for editor utilities to set prefab.
    public void SetCharacterPrefab(GameObject prefab)
    {
        characterPrefab = prefab;
    }

    private void AlignToHead()
    {
        if (camTransform == null)
        {
            return;
        }

        if (headAnchor != null && !IsVrConfigured())
        {
            camTransform.SetParent(headAnchor, false);
            camTransform.localPosition = Vector3.zero;
            return;
        }

        camTransform.SetParent(transform, false);
        camTransform.localPosition = new Vector3(0f, GetDefaultEyeHeight(), 0f);
        camTransform.localRotation = Quaternion.identity;
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (movementLocked)
        {
            velocity = Vector3.zero;
        }
    }

    public void SetHardFreeze(bool frozen)
    {
        hardFreeze = frozen;
        if (hardFreeze)
        {
            velocity = Vector3.zero;
        }
    }

    public void AddExternalDisplacement(Vector3 worldDelta)
    {
        pendingExternalDisplacement += worldDelta;
    }

    private bool ApplyGroundStick()
    {
        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f)
        {
            velocity.y = -2f; // small stick force
        }

        if (grounded)
        {
            if (!wasGrounded)
            {
                AudioManager.Play(MenSfx.Land);
            }
            lastGroundedTime = Time.time;
        }

        wasGrounded = grounded;
        return grounded;
    }

    private void ApplyGravityOnly()
    {
        ApplyGroundStick();
        velocity.y -= gravity * Time.deltaTime;
        controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
    }

    public void RespawnAtSpawnPoint()
    {
        drownTimer = 0f;
        velocity = Vector3.zero;
        pendingExternalDisplacement = Vector3.zero;
        xrHeadOriginCaptured = false;
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        controller.enabled = wasEnabled;
        AudioManager.Play(MenSfx.Respawn);
    }

    private void HandleWaterSubmersion()
    {
        bool submerged = transform.position.y < waterDrownY;
        bool standingOnSurface = controller != null && controller.isGrounded;

        if (submerged && !standingOnSurface)
        {
            drownTimer += Time.deltaTime;
            velocity.y -= waterPullDown * Time.deltaTime;
            if (drownTimer >= drownDelay)
            {
                RespawnAtSpawnPoint();
                SetHardFreeze(false);
                SetMovementLocked(false);
            }
        }
        else
        {
            drownTimer = 0f;
        }
    }

    private void ApplyExternalDisplacement()
    {
        if (controller == null)
        {
            pendingExternalDisplacement = Vector3.zero;
            return;
        }

        if (pendingExternalDisplacement.sqrMagnitude < 0.0000001f)
        {
            return;
        }

        Vector3 delta = pendingExternalDisplacement;
        pendingExternalDisplacement = Vector3.zero;
        controller.Move(delta);
    }

    private void ApplyControllerSize()
    {
        if (controller == null)
        {
            return;
        }

        controller.height = controllerHeight;
        controller.radius = controllerRadius;
        controller.center = new Vector3(0f, controllerHeight * 0.5f, 0f);
        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;
    }

    private float GetDefaultEyeHeight()
    {
        float baseHeight = controller != null ? controller.height : controllerHeight;
        return baseHeight * 0.9f;
    }

    public float GetMouseSensitivity()
    {
        return mouseSensitivity;
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Max(0.05f, value);
    }

    public void SetJumpPower(float value)
    {
        jumpPower = Mathf.Max(0f, value);
    }

    public float GetJumpPower()
    {
        return jumpPower;
    }

    public void SetCameraControlEnabled(bool enabled)
    {
        cameraControlEnabled = enabled;
    }

    private bool TryApplyXrHeadPose()
    {
        if (camTransform == null)
        {
            return false;
        }

        var subsystem = GetActiveDisplay();
        if (subsystem == null || !subsystem.running)
        {
            return false;
        }

        if (camTransform.parent != transform)
        {
            camTransform.SetParent(transform, false);
        }

        if (InputDevices.GetDeviceAtXRNode(XRNode.Head)
            .TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 pos))
        {
            if (!xrHeadOriginCaptured)
            {
                xrHeadOriginLocalPosition = pos;
                xrHeadOriginCaptured = true;
            }

            Vector3 offset = pos - xrHeadOriginLocalPosition;
            camTransform.localPosition = new Vector3(offset.x, eyeHeight, offset.z);
        }
        else
        {
            xrHeadOriginCaptured = false;
            camTransform.localPosition = new Vector3(0f, eyeHeight, 0f);
        }

        if (InputDevices.GetDeviceAtXRNode(XRNode.Head)
            .TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rot))
        {
            camTransform.localRotation = rot;
        }
        else
        {
            camTransform.localRotation = Quaternion.identity;
        }

        return true;
    }

    private XRDisplaySubsystem GetActiveDisplay()
    {
        var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running)
            {
                return displays[i];
            }
        }
        return null;
    }

    private bool IsVrActive()
    {
        var display = GetActiveDisplay();
        return display != null && display.running;
    }

    private static bool IsVrConfigured()
    {
        return XRSettings.enabled;
    }

    private bool TryGetVrMoveAxis(out Vector2 axis)
    {
        axis = Vector2.zero;

        XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!leftHand.isValid)
        {
            return false;
        }

        if (!leftHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out axis))
        {
            return false;
        }

        if (axis.sqrMagnitude < vrMoveDeadzone * vrMoveDeadzone)
        {
            axis = Vector2.zero;
            return false;
        }

        return true;
    }

    private void ApplyVrStickTurn()
    {
        if (vrTurnSpeed <= 0f)
        {
            return;
        }

        XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!rightHand.isValid)
        {
            return;
        }

        if (rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 axis))
        {
            float x = Mathf.Abs(axis.x) > vrTurnDeadzone ? axis.x : 0f;
            if (Mathf.Abs(x) > 0f)
            {
                float yawDelta = vrTurnSpeed * x * Time.deltaTime;
                transform.Rotate(Vector3.up, yawDelta, Space.World);
            }
        }
    }

    private bool _vrJumpWasPressed = false;
    private bool _vrPauseWasPressed = false;

    private void CheckVrPauseButton()
    {
        bool pressedNow = false;

        XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid)
            leftHand.TryGetFeatureValue(XRCommonUsages.secondaryButton, out pressedNow);

        if (!pressedNow)
        {
            try { pressedNow = OVRInput.Get(OVRInput.RawButton.Y); } catch { }
        }

        if (!pressedNow)
        {
            pressedNow = VrHandTracking.IsPinching(XRNode.LeftHand);
        }

        if (pressedNow && !_vrPauseWasPressed)
            PauseMenuManager.VrTogglePause();

        _vrPauseWasPressed = pressedNow;
    }

    private bool TryGetVrJumpRequest()
    {
        bool pressedNow = false;

        // A button via XR InputDevices (primaryButton on right hand)
        XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid)
            rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out pressedNow);

        // OVRInput fallback
        if (!pressedNow)
        {
            try { pressedNow = OVRInput.Get(OVRInput.RawButton.A); } catch { }
        }

        if (!pressedNow)
        {
            pressedNow = VrHandTracking.IsPinching(XRNode.RightHand);
        }

        bool downThisFrame = pressedNow && !_vrJumpWasPressed;
        _vrJumpWasPressed = pressedNow;
        return downThisFrame;
    }

    private bool IsVrSprintHeld()
    {
        // Left index trigger > 0.5 = sprint
        float triggerValue = 0f;
        XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid)
            leftHand.TryGetFeatureValue(XRCommonUsages.trigger, out triggerValue);

        if (triggerValue < 0.01f)
        {
            try { triggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch); } catch { }
        }

        if (triggerValue > 0.5f) return true;

        // Left thumbstick click = sprint
        bool stickClick = false;
        if (leftHand.isValid)
            leftHand.TryGetFeatureValue(XRCommonUsages.primary2DAxisClick, out stickClick);

        if (!stickClick)
        {
            try { stickClick = OVRInput.Get(OVRInput.RawButton.LThumbstick); } catch { }
        }

        return stickClick;
    }
}
