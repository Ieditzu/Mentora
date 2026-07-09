using UnityEngine;
using UnityEngine.InputSystem;

public class RocketLandingPuzzle : MonoBehaviour
{
    private const string TaskTitle = "Rocket Landing";
    private const string CommunityIslandName = "inslua3";
    private static readonly Vector3 FallbackPosition = new Vector3(57.4f, 1.2f, 331.6f);
    private static readonly Vector3 CommunityIslandLocalOffset = new Vector3(0f, 1.2f, 0f);

    public static RocketLandingPuzzle Instance { get; private set; }

    private GameObject root;
    private GameObject rocket;
    private GameObject coinObject;
    private GameObject bodyObject;
    private GameObject noseObject;
    private GameObject consoleObject;
    private GameObject landingPad;
    private Camera rocketCamera;
    private Rigidbody rocketBody;
    private RocketController rocketController;
    private RocketAerodynamics rocketAerodynamics;
    private FuelUsage rocketFuelUsage;
    private RocketCameraController rocketCameraController;
    private Transform engineFlame;
    private Vector3 launchPosition;
    private Quaternion launchRotation;
    private bool engineEnabled;
    private bool stabilizerEnabled;
    private bool flightActive;
    private bool completed;
    private float thrustPower;
    private float axialDrag;
    private float lateralDrag;
    private float rocketMass = 3f;
    private float fuelCapacity;
    private float fuelRemaining;
    private float torquePower = 18f;
    private float statusVisibleUntil;
    private bool rocketViewActive;
    private Camera playerCamera;
    private FirstPersonControllerSimple activeFps;
    private BeanController activeBean;
    private string status = "Walk to the blue console and repair the rocket with code.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static RocketLandingPuzzle EnsureInstance()
    {
        if (Instance != null)
        {
            Instance.EnsurePuzzleObjects();
            return Instance;
        }

        RocketLandingPuzzle existing = FindObjectOfType<RocketLandingPuzzle>();
        if (existing != null)
        {
            Instance = existing;
            Instance.EnsurePuzzleObjects();
            return existing;
        }

        GameObject go = new GameObject("RocketLandingPuzzle");
        Instance = go.AddComponent<RocketLandingPuzzle>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsurePuzzleObjects();
    }

    private void Update()
    {
        if (rocket == null || rocketBody == null)
        {
            return;
        }

        if (WasPressed(Key.R))
        {
            ResetRocket();
        }

        if (flightActive)
        {
            CheckLandingState();
        }
    }

    public bool TryApplyCode(
        bool engineEnabledValue,
        float fuel,
        float thrust,
        float drag,
        float mass,
        bool stabilizerValue,
        bool launchReady,
        out string feedback)
    {
        EnsurePuzzleObjects();
        statusVisibleUntil = Time.time + 6f;

        engineEnabled = engineEnabledValue;
        stabilizerEnabled = stabilizerValue;
        fuelCapacity = Mathf.Clamp(fuel, 0f, 120f);
        fuelRemaining = fuelCapacity;
        thrustPower = Mathf.Clamp(thrust, 0f, 90f);
        rocketMass = Mathf.Clamp(mass, 0.5f, 12f);
        lateralDrag = Mathf.Clamp(drag, 0f, 6f);
        axialDrag = Mathf.Clamp(drag * 0.18f, 0f, 1.4f);
        torquePower = Mathf.Lerp(30f, 10f, Mathf.InverseLerp(0.5f, 12f, rocketMass));

        ResetRocket();
        ApplyConfigVisuals();

        if (!engineEnabled)
        {
            feedback = "engineEnabled is false: the rocket has no ignition.";
            return false;
        }

        if (fuelRemaining < 15f)
        {
            feedback = "fuel is too low. Give it enough fuel to fly.";
            return false;
        }

        if (thrustPower <= rocketMass * Physics.gravity.magnitude * 1.12f)
        {
            feedback = "rocketThrust is too low for rocketMass.";
            return false;
        }

        if (!launchReady)
        {
            feedback = "Config applied. Set launchReady = true when ready.";
            return false;
        }

        rocketBody.mass = rocketMass;
        rocketBody.drag = 0f;
        rocketBody.angularDrag = stabilizerEnabled ? 2.8f : 0.45f;
        rocketBody.isKinematic = false;
        rocketBody.useGravity = true;
        rocketController.enabled = true;
        rocketController.zeroGravity = false;
        rocketController.thrustForce = thrustPower;
        rocketController.turnSpeed = torquePower * 5.2f;
        rocketController.boostAcceleration = thrustPower * 1.55f;
        rocketController.boostDuration = 0.9f;
        rocketController.boostCooldown = 1.6f;
        rocketController.SyncOrientationToTransform();
        rocketAerodynamics.enabledAero = true;
        rocketAerodynamics.lateralDrag = lateralDrag;
        rocketAerodynamics.axialDrag = axialDrag;
        rocketAerodynamics.liftStrength = stabilizerEnabled ? 0.028f : 0.014f;
        rocketAerodynamics.maxLiftAccel = stabilizerEnabled ? 55f : 32f;
        rocketFuelUsage.thrustBurnRate = Mathf.Max(0.4f, thrustPower * 0.025f);
        rocketFuelUsage.boostBurnRate = Mathf.Max(1.2f, thrustPower * 0.04f);
        flightActive = true;
        completed = false;
        status = stabilizerEnabled
            ? "Launch active. Space thrusts, W/S pitch, A/D yaw, Shift boosts."
            : "Launch active, but stabilizer=false makes it unstable.";
        feedback = "Rocket repaired. Space thrusts, W/S pitch, A/D yaw, Shift boosts.";
        AudioManager.Play(MenSfx.ButtonClick);
        return true;
    }

    private void EnsurePuzzleObjects()
    {
        if (root != null)
        {
            return;
        }

        Vector3 basePosition = ResolveCommunityIslandTop();
        root = new GameObject("RocketLandingExperiment");
        root.transform.position = basePosition;

        CreateLaunchPad(root.transform);
        CreateLandingPad(root.transform);
        CreateRocket(root.transform);
        CreateCoin(root.transform);
        CreateConsole(root.transform);
        ResetRocket();
        ApplyConfigVisuals();
    }

    private Vector3 ResolveCommunityIslandTop()
    {
        Transform island = FindCommunityIsland();
        if (island == null)
        {
            return FallbackPosition;
        }

        Bounds? bounds = CalculateBounds(island);
        if (!bounds.HasValue || island.name == CommunityIslandName)
        {
            return island.position + CommunityIslandLocalOffset;
        }

        Bounds b = bounds.Value;
        return new Vector3(b.center.x, b.max.y + 0.2f, b.center.z);
    }

    private static Transform FindCommunityIsland()
    {
        Transform communityIsland = FindTransformByExactName(CommunityIslandName);
        if (communityIsland != null)
        {
            return communityIsland;
        }

        string[] names = { "CommunityIsland", "Community Island", "islande", "HardOuterIsland", "MainDifficultyIsland" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform direct = FindTransformByExactName(names[i]);
            if (direct != null)
            {
                return direct;
            }
        }

        Transform[] all = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate == null)
            {
                continue;
            }

            string lower = candidate.name.ToLowerInvariant();
            if (lower.Contains("island") || lower.Contains("insula"))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindTransformByExactName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject active = GameObject.Find(objectName);
        if (active != null)
        {
            return active.transform;
        }

        Transform[] all = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform candidate = all[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Bounds? CalculateBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds? bounds = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!bounds.HasValue)
            {
                bounds = renderers[i].bounds;
            }
            else
            {
                Bounds b = bounds.Value;
                b.Encapsulate(renderers[i].bounds);
                bounds = b;
            }
        }

        return bounds;
    }

    private void CreateLaunchPad(Transform parent)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "RocketLaunchPad";
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = Vector3.zero;
        pad.transform.localScale = new Vector3(2.4f, 0.18f, 2.4f);
        ApplyColor(pad, new Color(0.18f, 0.23f, 0.28f, 1f));
    }

    private void CreateLandingPad(Transform parent)
    {
        landingPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        landingPad.name = "RocketLandingPad";
        landingPad.transform.SetParent(parent, false);
        landingPad.transform.localPosition = new Vector3(13f, 0f, 7f);
        landingPad.transform.localScale = new Vector3(3.4f, 0.22f, 3.4f);
        ApplyColor(landingPad, new Color(0.16f, 0.55f, 0.34f, 1f));

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "RocketLandingPadMarker";
        marker.transform.SetParent(landingPad.transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        marker.transform.localScale = new Vector3(2.7f, 0.035f, 2.7f);
        ApplyColor(marker, new Color(0.92f, 0.95f, 0.55f, 1f));
    }

    private void CreateConsole(Transform parent)
    {
        consoleObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        consoleObject.name = "RocketRepairConsole";
        consoleObject.transform.SetParent(parent, false);
        consoleObject.transform.localPosition = new Vector3(-2.7f, 0.7f, -2.4f);
        consoleObject.transform.localScale = new Vector3(1.4f, 1.0f, 0.65f);
        ApplyColor(consoleObject, new Color(0.08f, 0.36f, 0.78f, 1f));

        Collider trigger = consoleObject.GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        consoleObject.AddComponent<RocketLandingConsoleTrigger>();

        GameObject label = new GameObject("RocketConsoleLabel");
        label.transform.SetParent(consoleObject.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = "ROCKET CODE";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.2f;
        text.fontSize = 42;
        text.color = Color.white;
    }

    private void CreateCoin(Transform parent)
    {
        coinObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coinObject.name = "RocketExperimentCoin";
        coinObject.transform.SetParent(parent, false);
        coinObject.transform.localPosition = new Vector3(-2.7f, 1.55f, -0.9f);
        coinObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        coinObject.transform.localScale = new Vector3(0.7f, 0.08f, 0.7f);
        ApplyColor(coinObject, new Color(1f, 0.72f, 0.16f, 1f));

        Collider coinCollider = coinObject.GetComponent<Collider>();
        if (coinCollider != null)
        {
            coinCollider.isTrigger = true;
        }

        CoinRotator coinRotator = coinObject.AddComponent<CoinRotator>();
        coinRotator.ConfigureRuntime(CoinRotator.CoinMode.RocketLanding, false);
    }

    private void CreateRocket(Transform parent)
    {
        rocket = new GameObject("StudentCodeRocket");
        rocket.transform.SetParent(parent, false);

        bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bodyObject.name = "RocketBody";
        bodyObject.transform.SetParent(rocket.transform, false);
        bodyObject.transform.localScale = new Vector3(0.55f, 1.35f, 0.55f);

        noseObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        noseObject.name = "RocketNose";
        noseObject.transform.SetParent(rocket.transform, false);
        noseObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        noseObject.transform.localScale = new Vector3(0.64f, 0.78f, 0.64f);

        GameObject finLeft = CreateFin("RocketFinLeft", new Vector3(-0.52f, -0.82f, 0f), new Vector3(0.12f, 0.5f, 0.7f));
        finLeft.transform.SetParent(rocket.transform, false);
        GameObject finRight = CreateFin("RocketFinRight", new Vector3(0.52f, -0.82f, 0f), new Vector3(0.12f, 0.5f, 0.7f));
        finRight.transform.SetParent(rocket.transform, false);

        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        flame.name = "RocketEngineFlame";
        flame.transform.SetParent(rocket.transform, false);
        flame.transform.localPosition = new Vector3(0f, -1.7f, 0f);
        flame.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        flame.transform.localScale = new Vector3(0.34f, 0.7f, 0.34f);
        ApplyColor(flame, new Color(1f, 0.42f, 0.08f, 0.85f));
        engineFlame = flame.transform;
        engineFlame.gameObject.SetActive(false);

        rocketBody = rocket.AddComponent<Rigidbody>();
        rocketBody.useGravity = true;
        rocketBody.isKinematic = true;
        rocketBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rocketBody.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider collider = rocket.AddComponent<CapsuleCollider>();
        collider.radius = 0.42f;
        collider.height = 3.3f;
        collider.center = Vector3.zero;

        rocketController = rocket.AddComponent<RocketController>();
        rocketController.enabled = false;
        rocketController.zeroGravity = false;
        rocketController.mouseFreeLook = false;
        rocketController.cameraFreeByDefault = false;
        rocketController.alignHeadingToCamera = false;
        rocketController.chainLatchSteering = true;
        rocketController.perAxisLatch = true;

        rocketAerodynamics = rocket.AddComponent<RocketAerodynamics>();
        rocketAerodynamics.body = rocketBody;
        rocketAerodynamics.enabledAero = false;

        rocketFuelUsage = rocket.AddComponent<FuelUsage>();
        rocketFuelUsage.rocket = rocketController;

        RocketWinglets winglets = rocket.AddComponent<RocketWinglets>();
        winglets.finX = finLeft.transform;
        winglets.finZ = finRight.transform;
    }

    private GameObject CreateFin(string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = name;
        fin.transform.localPosition = localPosition;
        fin.transform.localScale = localScale;
        ApplyColor(fin, new Color(0.82f, 0.16f, 0.13f, 1f));
        return fin;
    }

    private void ResetRocket()
    {
        if (rocket == null || rocketBody == null)
        {
            return;
        }

        launchPosition = root.transform.TransformPoint(new Vector3(0f, 1.9f, 0f));
        launchRotation = root.transform.rotation;
        rocketBody.velocity = Vector3.zero;
        rocketBody.angularVelocity = Vector3.zero;
        rocketBody.isKinematic = true;
        rocketBody.useGravity = true;
        rocketBody.mass = rocketMass;
        rocket.transform.SetPositionAndRotation(launchPosition, launchRotation);
        if (rocketController != null)
        {
            rocketController.enabled = false;
            rocketController.ResetTo(launchPosition, launchRotation);
        }

        if (rocketFuelUsage != null)
        {
            rocketFuelUsage.ResetFuel();
        }

        if (rocketAerodynamics != null)
        {
            rocketAerodynamics.enabledAero = false;
        }

        if (engineFlame != null)
        {
            engineFlame.gameObject.SetActive(false);
        }

        flightActive = false;
        if (!completed)
        {
            status = engineEnabled
                ? "Rocket configured. Set launchReady=true and press Enter."
                : "Rocket broken. Open the blue console and fix the variables.";
        }
    }

    public void BeginCoinSession()
    {
        EnsurePuzzleObjects();
        statusVisibleUntil = Time.time + 6f;
        CachePlayerReferences();
        SetPlayerLockState(true);
        SetRocketViewActive(true);
        status = "Rocket camera locked. Repair the rocket code, then launch.";
    }

    public void EndCoinSession()
    {
        SetRocketViewActive(false);
        SetPlayerLockState(false);
        flightActive = false;
        if (!completed)
        {
            status = "Rocket session closed.";
        }
    }

    private void CheckLandingState()
    {
        if (landingPad == null || completed)
        {
            return;
        }

        Vector3 localToPad = landingPad.transform.InverseTransformPoint(rocket.transform.position);
        float horizontalDistance = new Vector2(localToPad.x, localToPad.z).magnitude;
        float verticalDistance = Mathf.Abs(localToPad.y - 1.9f);
        float speed = rocketBody.velocity.magnitude;
        bool upright = Vector3.Dot(rocket.transform.up, Vector3.up) > 0.72f;
        bool onPad = horizontalDistance < 2.2f && verticalDistance < 1.1f;

        if (rocketFuelUsage != null)
        {
            fuelRemaining = Mathf.Max(0f, fuelCapacity - rocketFuelUsage.FuelUsed);
        }

        if (onPad && upright && speed < 4.2f)
        {
            completed = true;
            flightActive = false;
            rocketBody.velocity = Vector3.zero;
            rocketBody.angularVelocity = Vector3.zero;
            rocketBody.isKinematic = true;
            status = "Clean landing. Rocket experiment solved.";
            AudioManager.Play(MenSfx.ChallengeComplete);
            PauseMenuManager.CompleteTaskByTitle(TaskTitle);
            return;
        }

        if (fuelRemaining <= 0f && engineFlame != null)
        {
            engineFlame.gameObject.SetActive(false);
            if (rocketController != null)
            {
                rocketController.LockThrustUntilRelease();
            }
        }

        if (rocket.transform.position.y < root.transform.position.y - 24f || speed > 46f)
        {
            status = "Rocket lost control. Press R to reset.";
            flightActive = false;
        }
        else
        {
            status = $"Fuel {fuelRemaining:0} | Speed {speed:0.0} | Pad {horizontalDistance:0.0}m | Upright {(upright ? "yes" : "no")}";
        }
    }

    private void CachePlayerReferences()
    {
        activeFps = PlayerCache.GetFps();
        activeBean = FindObjectOfType<BeanController>();
        if (activeFps != null)
        {
            playerCamera = activeFps.GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void SetPlayerLockState(bool locked)
    {
        if (activeBean != null)
        {
            activeBean.SetMovementLocked(locked);
            activeBean.SetHardFreeze(locked);
        }

        if (activeFps != null)
        {
            activeFps.SetMovementLocked(locked);
            activeFps.SetHardFreeze(locked);
            activeFps.SetCameraControlEnabled(!locked);
        }
    }

    private void SetRocketViewActive(bool active)
    {
        rocketViewActive = active;

        if (playerCamera != null)
        {
            playerCamera.enabled = !active;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = !active;
            }
        }

        if (rocketCamera == null && active)
        {
            GameObject cameraObject = new GameObject("RocketFollowCamera");
            cameraObject.transform.SetParent(transform, false);
            rocketCamera = cameraObject.AddComponent<Camera>();
            rocketCamera.depth = 100f;
            rocketCamera.fieldOfView = 72f;
            rocketCamera.nearClipPlane = 0.03f;
            rocketCamera.tag = "Untagged";
            cameraObject.AddComponent<AudioListener>();
            rocketCameraController = cameraObject.AddComponent<RocketCameraController>();
            rocketCameraController.isOnLauncher = true;
            rocketCameraController.followHeading = true;
            rocketCameraController.distance = 7.2f;
            rocketCameraController.minDistance = 5f;
            rocketCameraController.maxDistance = 11f;
            rocketCameraController.lookOffset = new Vector3(0f, 1.2f, 0f);
            rocketCameraController.launcherPositionOffset = new Vector3(0f, 1.6f, 0f);
            rocketCameraController.launcherRotationOffset = new Vector3(18f, 0f, 0f);
            rocketCameraController.slowMoTimeScale = 1f;
            rocketCameraController.slowMoRampIn = 0.01f;
            rocketCameraController.slowMoRampOut = 0.01f;
        }

        if (rocketCamera != null)
        {
            rocketCamera.enabled = active;
            AudioListener listener = rocketCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = active;
            }
        }

        if (rocketCameraController != null)
        {
            rocketCameraController.enabled = active;
            rocketCameraController.target = rocket != null ? rocket.transform : null;
        }
    }

    private void ApplyConfigVisuals()
    {
        Color bodyColor = engineEnabled
            ? new Color(0.86f, 0.87f, 0.82f, 1f)
            : new Color(0.38f, 0.36f, 0.34f, 1f);
        Color noseColor = engineEnabled
            ? new Color(0.82f, 0.16f, 0.13f, 1f)
            : new Color(0.38f, 0.08f, 0.07f, 1f);

        if (bodyObject != null) ApplyColor(bodyObject, bodyColor);
        if (noseObject != null) ApplyColor(noseObject, noseColor);
        if (consoleObject != null)
        {
            ApplyColor(consoleObject, engineEnabled ? new Color(0.05f, 0.62f, 0.44f, 1f) : new Color(0.08f, 0.36f, 0.78f, 1f));
        }
    }

    private void OnGUI()
    {
        if (rocket == null || (!flightActive && !completed && Time.time > statusVisibleUntil))
        {
            return;
        }

        float width = Input.touchSupported || Application.isMobilePlatform ? 760f : 470f;
        Rect rect = new Rect(16f, Screen.height - 150f, width, 132f);
        GUI.Box(rect, GUIContent.none);
        GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f));
        GUILayout.Label("Rocket experiment");
        GUILayout.Label("Collect coin to enter. Space thrusts, W/S pitch, A/D yaw, Shift boosts, R reset.");
        GUILayout.Label(status);
        GUILayout.EndArea();
    }

    private static bool WasPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.material.color = color;
    }
}

public class RocketLandingConsoleTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        PickupUIController ui = PickupUIController.Instance;
        if (ui == null)
        {
            GameObject uiObject = new GameObject("PickupUIController");
            ui = uiObject.AddComponent<PickupUIController>();
        }

        ui.ShowRocketExperiment();
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        return other.GetComponentInParent<BeanController>() != null ||
               other.GetComponentInParent<FirstPersonControllerSimple>() != null ||
               other.GetComponentInParent<CharacterController>() != null ||
               other.CompareTag("Player");
    }
}
