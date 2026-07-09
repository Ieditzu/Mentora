using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RocketLandingPuzzle : MonoBehaviour
{
    private const string TaskTitle = "Rocket Landing";
    private const string CommunityIslandName = "inslua3";
    private const string MissileResourcePath = "Missile/AIM-120C AMRAAM";
    private const string MissileEditorAssetPath = "Assets/missle/AIM-120C AMRAAM/AIM-120C AMRAAM.obj";
    private static readonly Vector3 FallbackPosition = new Vector3(57.4f, 1.2f, 331.6f);
    private static readonly Vector3 CommunityIslandLocalOffset = new Vector3(0f, 1.2f, 0f);

    public static RocketLandingPuzzle Instance { get; private set; }

    private GameObject root;
    private GameObject rocket;
    private GameObject coinObject;
    private GameObject importedRocketModel;
    private GameObject bodyObject;
    private GameObject noseObject;
    private GameObject consoleObject;
    private GameObject landingPad;
    private Camera rocketCamera;
    private CapsuleCollider rocketCollider;
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
    private float launchClearanceHeight = 1.9f;
    private bool rocketViewActive;
    private bool crashed;
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

        if (thrustPower < 12f)
        {
            feedback = "rocketThrust is too low. Try a higher value.";
            return false;
        }

        if (!launchReady)
        {
            feedback = "Config applied. Set launchReady = true when ready.";
            return false;
        }

        rocketBody.mass = rocketMass;
        rocketBody.drag = 0.08f;
        rocketBody.angularDrag = stabilizerEnabled ? 1.2f : 0.2f;
        rocketBody.isKinematic = false;
        rocketBody.useGravity = true;
        rocketController.enabled = true;
        rocketController.zeroGravity = false;
        rocketController.thrustForce = thrustPower * 1.25f;
        rocketController.turnSpeed = torquePower * 4.4f;
        rocketController.steeringTorque = stabilizerEnabled ? 24f : 14f;
        rocketController.steeringDamping = stabilizerEnabled ? 6.5f : 2.5f;
        rocketController.maxAngularSpeed = stabilizerEnabled ? 5.5f : 8f;
        rocketController.boostAcceleration = thrustPower * 1.35f;
        rocketController.boostDuration = 0.9f;
        rocketController.boostCooldown = 1.6f;
        rocketController.SyncOrientationToTransform();
        rocketAerodynamics.enabledAero = true;
        rocketAerodynamics.lateralDrag = stabilizerEnabled ? lateralDrag * 1.35f : lateralDrag * 0.75f;
        rocketAerodynamics.axialDrag = stabilizerEnabled ? axialDrag * 1.1f : axialDrag * 0.7f;
        rocketAerodynamics.liftStrength = stabilizerEnabled ? 0.022f : 0.011f;
        rocketAerodynamics.maxLiftAccel = stabilizerEnabled ? 42f : 24f;
        rocketAerodynamics.alignmentTorque = stabilizerEnabled ? 4.8f : 1.8f;
        rocketAerodynamics.angularDamping = stabilizerEnabled ? 2.6f : 0.7f;
        rocketFuelUsage.thrustBurnRate = Mathf.Max(0.4f, thrustPower * 0.025f);
        rocketFuelUsage.boostBurnRate = Mathf.Max(1.2f, thrustPower * 0.04f);
        flightActive = true;
        completed = false;
            status = stabilizerEnabled
                ? "Launch active. Space thrusts, W/S pitch, A/D yaw, Shift boosts, C camera."
                : "Launch active, but stabilizer=false makes it unstable.";
        feedback = "Rocket repaired. Space thrusts, W/S pitch, A/D yaw, Shift boosts, C camera.";
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

        importedRocketModel = InstantiateRocketVisual();
        if (importedRocketModel != null)
        {
            importedRocketModel.transform.SetParent(rocket.transform, false);
            importedRocketModel.name = "RocketVisual";
            importedRocketModel.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            importedRocketModel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            importedRocketModel.transform.localScale = Vector3.one * 1.45f;
            RemoveVisualColliders(importedRocketModel);
            bodyObject = importedRocketModel;
            noseObject = importedRocketModel;
        }
        else
        {
            bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bodyObject.name = "RocketBody";
            bodyObject.transform.SetParent(rocket.transform, false);
            bodyObject.transform.localScale = new Vector3(0.72f, 1.6f, 0.72f);

            noseObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            noseObject.name = "RocketNose";
            noseObject.transform.SetParent(rocket.transform, false);
            noseObject.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            noseObject.transform.localScale = new Vector3(0.62f, 1.15f, 0.62f);

            GameObject finLeft = CreateFin("RocketFinLeft", new Vector3(-0.52f, -0.82f, 0f), new Vector3(0.12f, 0.5f, 0.7f));
            finLeft.transform.SetParent(rocket.transform, false);
            GameObject finRight = CreateFin("RocketFinRight", new Vector3(0.52f, -0.82f, 0f), new Vector3(0.12f, 0.5f, 0.7f));
            finRight.transform.SetParent(rocket.transform, false);

            RocketWinglets primitiveWinglets = rocket.AddComponent<RocketWinglets>();
            primitiveWinglets.finX = finLeft.transform;
            primitiveWinglets.finZ = finRight.transform;
        }

        GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nozzle.name = "RocketEngineNozzle";
        nozzle.transform.SetParent(rocket.transform, false);
        nozzle.transform.localPosition = new Vector3(0f, -1.95f, 0f);
        nozzle.transform.localScale = new Vector3(0.42f, 0.18f, 0.42f);
        ApplyColor(nozzle, new Color(0.18f, 0.18f, 0.2f, 1f));

        GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        flame.name = "RocketEngineFlame";
        flame.transform.SetParent(rocket.transform, false);
        flame.transform.localPosition = new Vector3(0f, -2.25f, 0f);
        flame.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        flame.transform.localScale = new Vector3(0.42f, 0.95f, 0.42f);
        ApplyColor(flame, new Color(1f, 0.42f, 0.08f, 0.85f));
        engineFlame = flame.transform;
        engineFlame.gameObject.SetActive(false);

        rocketBody = rocket.AddComponent<Rigidbody>();
        rocketBody.useGravity = true;
        rocketBody.isKinematic = true;
        rocketBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rocketBody.interpolation = RigidbodyInterpolation.Interpolate;

        rocketCollider = rocket.AddComponent<CapsuleCollider>();
        rocketCollider.radius = 0.42f;
        rocketCollider.height = 3.3f;
        rocketCollider.center = Vector3.zero;
        FitRocketCollider();

        rocketController = rocket.AddComponent<RocketController>();
        rocketController.enabled = false;
        rocketController.zeroGravity = false;
        rocketController.mouseFreeLook = false;
        rocketController.cameraFreeByDefault = false;
        rocketController.alignHeadingToCamera = false;
        rocketController.cameraRelativeYaw = false;
        rocketController.chainLatchSteering = true;
        rocketController.perAxisLatch = true;

        rocketAerodynamics = rocket.AddComponent<RocketAerodynamics>();
        rocketAerodynamics.body = rocketBody;
        rocketAerodynamics.enabledAero = false;

        rocketFuelUsage = rocket.AddComponent<FuelUsage>();
        rocketFuelUsage.rocket = rocketController;

        RocketCollisionRelay collisionRelay = rocket.AddComponent<RocketCollisionRelay>();
        collisionRelay.Initialize(this);
    }

    private void CreateBoosterPod(string name, Vector3 localPosition, Transform parent)
    {
        GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pod.name = name;
        pod.transform.SetParent(parent, false);
        pod.transform.localPosition = localPosition;
        pod.transform.localScale = new Vector3(0.28f, 1.1f, 0.28f);
        ApplyColor(pod, new Color(0.88f, 0.89f, 0.84f, 1f));

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = name + "Cap";
        cap.transform.SetParent(pod.transform, false);
        cap.transform.localPosition = new Vector3(0f, 1f, 0f);
        cap.transform.localScale = new Vector3(0.78f, 0.5f, 0.78f);
        ApplyColor(cap, new Color(0.82f, 0.16f, 0.13f, 1f));
    }

    private void CreateLandingStrut(string name, Vector3 localPosition, float zRotation, Transform parent)
    {
        GameObject strut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strut.name = name;
        strut.transform.SetParent(parent, false);
        strut.transform.localPosition = localPosition;
        strut.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        strut.transform.localScale = new Vector3(0.08f, 0.62f, 0.08f);
        ApplyColor(strut, new Color(0.72f, 0.74f, 0.77f, 1f));

        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foot.name = name + "Foot";
        foot.transform.SetParent(strut.transform, false);
        foot.transform.localPosition = new Vector3(0f, -0.58f, 0f);
        foot.transform.localRotation = Quaternion.identity;
        foot.transform.localScale = new Vector3(1.6f, 0.22f, 1.6f);
        ApplyColor(foot, new Color(0.2f, 0.21f, 0.24f, 1f));
    }

    private static GameObject InstantiateRocketVisual()
    {
        GameObject resourceModel = Resources.Load<GameObject>(MissileResourcePath);
        if (resourceModel != null)
        {
            return Instantiate(resourceModel);
        }

#if UNITY_EDITOR
        GameObject editorModel = AssetDatabase.LoadAssetAtPath<GameObject>(MissileEditorAssetPath);
        if (editorModel != null)
        {
            return Instantiate(editorModel);
        }
#endif

        return null;
    }

    private static void RemoveVisualColliders(GameObject rootObject)
    {
        Collider[] colliders = rootObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Destroy(colliders[i]);
        }
    }

    private void FitRocketCollider()
    {
        if (rocket == null || rocketCollider == null)
        {
            return;
        }

        Bounds? localBounds = CalculateLocalRendererBounds(rocket.transform);
        if (!localBounds.HasValue)
        {
            launchClearanceHeight = 1.9f;
            return;
        }

        Bounds bounds = localBounds.Value;
        float radius = Mathf.Max(0.22f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f);
        float height = Mathf.Max(radius * 2.2f, bounds.size.y);

        rocketCollider.center = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        rocketCollider.radius = radius;
        rocketCollider.height = height;

        // Spawn so the lowest visible point starts above the pad instead of intersecting it.
        launchClearanceHeight = -bounds.min.y + 0.18f;
    }

    private static Bounds? CalculateLocalRendererBounds(Transform rootTransform)
    {
        Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);
        Bounds? combined = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 localMin = rootTransform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = rootTransform.InverseTransformPoint(worldBounds.max);
            Bounds localBounds = new Bounds((localMin + localMax) * 0.5f, localMax - localMin);

            if (!combined.HasValue)
            {
                combined = localBounds;
            }
            else
            {
                Bounds current = combined.Value;
                current.Encapsulate(localBounds.min);
                current.Encapsulate(localBounds.max);
                combined = current;
            }
        }

        return combined;
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

        launchPosition = root.transform.TransformPoint(new Vector3(0f, launchClearanceHeight, 0f));
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

        SetRocketVisible(true);
        if (engineFlame != null)
        {
            engineFlame.gameObject.SetActive(false);
        }

        crashed = false;
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
        if (landingPad == null || completed || crashed)
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

        if (rocket.transform.position.y < root.transform.position.y - 24f || speed > 140f)
        {
            status = "Rocket lost control. Press R to reset.";
            flightActive = false;
        }
        else
        {
            status = $"Fuel {fuelRemaining:0} | Speed {speed:0.0} | Pad {horizontalDistance:0.0}m | Upright {(upright ? "yes" : "no")}";
        }
    }

    internal void HandleRocketCollision(Collision collision)
    {
        if (!flightActive || completed || crashed || collision == null || rocketBody == null)
        {
            return;
        }

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < 9.5f)
        {
            return;
        }

        bool groundLikeSurface = false;
        bool noseHit = false;
        Vector3 rocketUp = rocket.transform.up;
        float noseThreshold = rocketCollider != null
            ? rocketCollider.center.y + Mathf.Max(rocketCollider.height * 0.18f, rocketCollider.radius * 0.8f)
            : 0.9f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y > 0.2f)
            {
                groundLikeSurface = true;
            }

            Vector3 localPoint = rocket.transform.InverseTransformPoint(contact.point);
            if (localPoint.y >= noseThreshold)
            {
                noseHit = true;
            }
        }

        Vector3 velocity = rocketBody.velocity;
        bool divingIntoImpact = velocity.sqrMagnitude > 0.01f &&
                                Vector3.Dot(velocity.normalized, rocketUp) > 0.15f;
        bool severeGroundImpact = groundLikeSurface && impactSpeed >= 11f;
        bool noseCrash = noseHit && divingIntoImpact;
        if (severeGroundImpact || noseCrash)
        {
            ExplodeRocket(collision.GetContact(0).point, impactSpeed);
        }
    }

    private void ExplodeRocket(Vector3 impactPoint, float impactSpeed)
    {
        crashed = true;
        flightActive = false;

        if (rocketController != null)
        {
            rocketController.enabled = false;
        }

        if (rocketAerodynamics != null)
        {
            rocketAerodynamics.enabledAero = false;
        }

        if (rocketBody != null)
        {
            rocketBody.velocity = Vector3.zero;
            rocketBody.angularVelocity = Vector3.zero;
            rocketBody.isKinematic = true;
        }

        if (engineFlame != null)
        {
            engineFlame.gameObject.SetActive(false);
        }

        SetRocketVisible(false);
        CreateExplosionBurst(impactPoint, impactSpeed);
        AudioManager.Play(MenSfx.AnswerWrong);
        statusVisibleUntil = Time.time + 6f;
        status = "Rocket exploded on impact. Press R to reset.";
    }

    private void SetRocketVisible(bool visible)
    {
        if (rocket == null)
        {
            return;
        }

        Renderer[] renderers = rocket.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    private void CreateExplosionBurst(Vector3 impactPoint, float impactSpeed)
    {
        GameObject burstRoot = new GameObject("RocketExplosionBurst");
        burstRoot.transform.position = impactPoint;

        int burstCount = 10;
        float speedScale = Mathf.Clamp(impactSpeed * 0.12f, 1.1f, 2.6f);
        for (int i = 0; i < burstCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragment.transform.SetParent(burstRoot.transform, false);
            fragment.transform.localScale = Vector3.one * Random.Range(0.18f, 0.42f);
            fragment.transform.position = impactPoint + Random.insideUnitSphere * 0.35f;
            ApplyColor(fragment, i % 3 == 0
                ? new Color(1f, 0.45f, 0.08f, 1f)
                : new Color(0.18f, 0.18f, 0.2f, 1f));

            Collider fragmentCollider = fragment.GetComponent<Collider>();
            if (fragmentCollider != null)
            {
                Destroy(fragmentCollider);
            }

            Rigidbody fragmentBody = fragment.AddComponent<Rigidbody>();
            fragmentBody.mass = 0.08f;
            Vector3 burstDirection = (Random.onUnitSphere + Vector3.up * 0.8f).normalized;
            fragmentBody.AddForce(burstDirection * Random.Range(4.5f, 8.5f) * speedScale, ForceMode.Impulse);
            fragmentBody.AddTorque(Random.insideUnitSphere * 14f, ForceMode.Impulse);
            Destroy(fragment, 1.6f);
        }

        Destroy(burstRoot, 1.7f);
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
            rocketCamera.nearClipPlane = 0.01f;
            rocketCamera.tag = "Untagged";
            cameraObject.AddComponent<AudioListener>();
            rocketCameraController = cameraObject.AddComponent<RocketCameraController>();
            rocketCameraController.isOnLauncher = false;
            rocketCameraController.followHeading = true;
            rocketCameraController.noseViewActive = true;
            rocketCameraController.noseLocalOffset = new Vector3(0f, 1.78f, 0f);
            rocketCameraController.noseRotationOffset = Vector3.zero;
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

        ConfigureRocketCameraOffsets();

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

    private void ConfigureRocketCameraOffsets()
    {
        if (rocketCameraController == null)
        {
            return;
        }

        Vector3 noseOffset = new Vector3(0f, 1.78f, 0f);
        if (rocketCollider != null)
        {
            float noseY = rocketCollider.center.y + rocketCollider.height * 0.5f - rocketCollider.radius * 0.18f;
            noseOffset = new Vector3(rocketCollider.center.x, noseY, rocketCollider.center.z);
            rocketCameraController.lookOffset = new Vector3(rocketCollider.center.x, rocketCollider.center.y + 0.15f, rocketCollider.center.z);
        }

        rocketCameraController.noseLocalOffset = noseOffset;
        rocketCameraController.noseRotationOffset = Vector3.zero;
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
        GUILayout.Label("Collect coin to enter. Space thrusts, W/S pitch, A/D yaw, Shift boosts, C camera, R reset.");
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
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                renderers[i].material.color = color;
            }
        }
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

public class RocketCollisionRelay : MonoBehaviour
{
    private RocketLandingPuzzle owner;

    public void Initialize(RocketLandingPuzzle puzzleOwner)
    {
        owner = puzzleOwner;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null)
        {
            owner.HandleRocketCollision(collision);
        }
    }
}
