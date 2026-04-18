using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeanController : MonoBehaviour
{
    private const float MaxJumpValue = 10f;
    private const string LeftTouchPlusResourcePath = "MetaQuestTouchPlus/MetaQuestTouchPlus_Left";
    private const string RightTouchPlusResourcePath = "MetaQuestTouchPlus/MetaQuestTouchPlus_Right";
    private static readonly Vector3 TouchPlusModelLocalPosition = new Vector3(0f, -0.03f, -0.04f);
    private static readonly Quaternion TouchPlusModelLocalRotation = Quaternion.Euler(30f, 0f, 0f);

    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float gravityMultiplier = 3f;
    [SerializeField] private float fallYThreshold = -6f;
    [SerializeField] private float jumpVelocity = 10f;
    [SerializeField] private float groundCheckDistance = 0.45f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float mouseSensitivity = 18f;
    [Header("Bean Visual")]
    [SerializeField] private GameObject beanVisualPrefab;
    [SerializeField] private float beanVisualScale = 4.4f;

    private Rigidbody rb;
    private Vector3 input;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private int timesFallen;
    private bool movementLocked;
    private bool hardFreeze;
    private bool isSprinting;
    private bool isGrounded;
    private bool jumpQueued;
    private float lastGroundedTime = -10f;

    private Transform camTransform;
    private Transform leftControllerVisual;
    private Transform rightControllerVisual;
    private float pitch;

    public static bool KeyboardInputEnabled { get; set; } = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Make it a bean, do not roll
        PlayerCache.Register(this);

        var sphereCol = GetComponent<SphereCollider>();
        if (sphereCol != null)
        {
            Destroy(sphereCol);
        }

        var capCol = GetComponent<CapsuleCollider>();
        if (capCol == null)
        {
            capCol = gameObject.AddComponent<CapsuleCollider>();
        }
        capCol.height = 4.4f;
        capCol.radius = 1.1f;
        capCol.center = new Vector3(0f, 2.2f, 0f);

        var filter = GetComponent<MeshFilter>();
        if (filter != null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            filter.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            // First-person: put the main camera inside the bean and drive it from this controller.
            var follow = mainCam.GetComponent<BeanCameraFollow>();
            if (follow != null)
            {
                Destroy(follow);
            }

            camTransform = mainCam.transform;
            camTransform.SetParent(transform, false);
            camTransform.localPosition = new Vector3(0f, 2.35f, 0f);
            camTransform.localRotation = Quaternion.identity;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        SpawnVisualBeanIfNeeded();
        EnsureControllerVisuals();

        startPosition = transform.position;
        startRotation = transform.rotation;
        timesFallen = 0;
        FallCounterDisplay.SetCount(timesFallen);
    }

    private void Update()
    {
        UpdateControllerVisuals();

        if (hardFreeze)
        {
            input = Vector3.zero;
            isSprinting = false;
            return;
        }

        if (PauseMenuManager.IsGamePaused || Cursor.lockState != CursorLockMode.Locked)
        {
            input = Vector3.zero;
            isSprinting = false;
            return;
        }

        if (!movementLocked && KeyboardInputEnabled && camTransform != null)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
            camTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Space) && !movementLocked && !hardFreeze && KeyboardInputEnabled)
        {
            jumpQueued = true;
        }

        Vector2 mobileMove = MobileTouchInput.Move;
        if (mobileMove.sqrMagnitude > 0.0001f)
        {
            input = new Vector3(mobileMove.x, 0f, mobileMove.y);
            return;
        }

        if (!KeyboardInputEnabled)
        {
            input = Vector3.zero;
            return;
        }

        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;

        input = new Vector3(x, 0f, z).normalized;
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

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (hardFreeze)
        {
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
            return;
        }

        if (transform.position.y < fallYThreshold)
        {
            rb.velocity = Vector3.zero;
            timesFallen++;
            FallCounterDisplay.SetCount(timesFallen);
            RespawnNow();
            return;
        }

        if (movementLocked)
        {
            return;
        }

        bool canJump = isGrounded || (Time.time - lastGroundedTime) <= coyoteTime;
        if (jumpQueued && canJump)
        {
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
            jumpQueued = false;
            isGrounded = false;
            lastGroundedTime = -10f;
        }

        if (jumpQueued && !canJump)
        {
            jumpQueued = false;
        }

        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        
        Vector3 moveDir;
        if (camTransform != null)
        {
            moveDir = transform.right * input.x + transform.forward * input.z;
        }
        else
        {
            // If there's no camera, fall back to simple world or relative transform space
            moveDir = transform.right * input.x + transform.forward * input.z;
        }

        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 targetVelocity = moveDir * currentSpeed;
        rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);

        if (gravityMultiplier > 1f)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    public void RespawnNow()
    {
        jumpQueued = false;
        rb.velocity = Vector3.zero;
        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.position = startPosition;
        transform.rotation = startRotation;
    }

    private bool CheckGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, groundCheckDistance);
        }

        float radius = Mathf.Max(0.05f, capsule.radius * 0.92f);
        float halfHeight = Mathf.Max(radius, capsule.height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 bottom = center - transform.up * halfHeight;
        Vector3 castStart = bottom + transform.up * 0.08f;
        return Physics.SphereCast(castStart, radius, Vector3.down, out _, groundCheckDistance);
    }

    private void SpawnVisualBeanIfNeeded()
    {
        if (beanVisualPrefab == null)
        {
            return;
        }

        // Disable the simple mesh so only the bean visual shows.
        var selfRenderer = GetComponent<Renderer>();
        if (selfRenderer != null)
        {
            selfRenderer.enabled = false;
        }

        var visual = Instantiate(beanVisualPrefab, transform);
        visual.name = "BeanVisual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * beanVisualScale;

        foreach (var col in visual.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }
    }

    private void EnsureControllerVisuals()
    {
        if (leftControllerVisual == null)
        {
            leftControllerVisual = CreateControllerVisual("BeanLeftController", true);
        }

        if (rightControllerVisual == null)
        {
            rightControllerVisual = CreateControllerVisual("BeanRightController", false);
        }
    }

    private Transform CreateControllerVisual(string name, bool isLeft)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);

        if (TryCreateTouchPlusControllerModel(root.transform, isLeft))
        {
            return root.transform;
        }

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = isLeft ? "LeftGrip" : "RightGrip";
        grip.transform.SetParent(root.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.015f, 0f);
        grip.transform.localScale = new Vector3(0.055f, 0.09f, 0.04f);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ring.name = isLeft ? "LeftRing" : "RightRing";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.02f, 0.025f);
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

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = isLeft ? new Color(0.22f, 0.72f, 0.95f) : new Color(0.95f, 0.78f, 0.22f);

        MeshRenderer gripRenderer = grip.GetComponent<MeshRenderer>();
        if (gripRenderer != null)
        {
            gripRenderer.sharedMaterial = material;
        }

        MeshRenderer ringRenderer = ring.GetComponent<MeshRenderer>();
        if (ringRenderer != null)
        {
            ringRenderer.sharedMaterial = material;
        }

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

    private void UpdateControllerVisuals()
    {
        EnsureControllerVisuals();
        UpdateControllerVisual(leftControllerVisual, OVRInput.Controller.LTouch, true);
        UpdateControllerVisual(rightControllerVisual, OVRInput.Controller.RTouch, false);
    }

    private void UpdateControllerVisual(Transform visual, OVRInput.Controller controllerMask, bool isLeft)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 defaultPosition = GetDefaultControllerPosition(isLeft);
        Quaternion defaultRotation = camTransform != null ? camTransform.localRotation : Quaternion.identity;

        if ((OVRInput.GetConnectedControllers() & controllerMask) != 0)
        {
            Vector3 trackedPosition = OVRInput.GetLocalControllerPosition(controllerMask);
            Quaternion trackedRotation = OVRInput.GetLocalControllerRotation(controllerMask);
            Vector3 cameraOffset = camTransform != null ? camTransform.localPosition : new Vector3(0f, 2.35f, 0f);

            visual.localPosition = cameraOffset + trackedPosition;
            visual.localRotation = trackedRotation;
            visual.gameObject.SetActive(true);
            return;
        }

        visual.localPosition = defaultPosition;
        visual.localRotation = defaultRotation;
        visual.gameObject.SetActive(true);
    }

    private Vector3 GetDefaultControllerPosition(bool isLeft)
    {
        float horizontalOffset = isLeft ? -0.22f : 0.22f;
        float cameraHeight = camTransform != null ? camTransform.localPosition.y : 2.35f;
        return new Vector3(horizontalOffset, cameraHeight - 0.18f, 0.35f);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (movementLocked)
        {
            input = Vector3.zero;
            isSprinting = false;
        }
    }

    public void SetHardFreeze(bool frozen)
    {
        hardFreeze = frozen;
        if (hardFreeze)
        {
            input = Vector3.zero;
            isSprinting = false;
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
        }
    }

    public void SetJumpForce(float newJump)
    {
        if (newJump > MaxJumpValue)
        {
            Debug.LogWarning("max value 10");
            return;
        }

        jumpVelocity = Mathf.Max(0f, newJump);
    }

    public float GetJumpForce()
    {
        return jumpVelocity;
    }
}
