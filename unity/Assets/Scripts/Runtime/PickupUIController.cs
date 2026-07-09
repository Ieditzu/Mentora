using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Minimal bottom-right overlay that appears after a coin is collected.
/// Lets you set the bean's jump power and toggle whether a target box can be pushed.
/// </summary>
public class PickupUIController : MonoBehaviour
{
    private const float MaxJumpValue = 10f;
    private const string IslandVisibleLabel = "!islandVisible =";
    private const string BridgeVisibleLabel = "viewPod =";

    public static PickupUIController Instance { get; private set; }
    public static bool IsBridgeRevealed => Instance != null && Instance.bridgeRevealActive;

    [SerializeField] private BeanController beanPlayer;
    [SerializeField] private FirstPersonControllerSimple fpsPlayer;
    [SerializeField] private Rigidbody targetBox;
    [SerializeField] private string revealIslandName = "islande";
    [SerializeField] private string bridgeRevealName = "podfull";
    [SerializeField] private string firstCoinName = "Coin1";
    [SerializeField] private string firstBridgeCoinName = "Coin3";
    [SerializeField] private string firstBridgeCoinTargetName = "boxcoin3";
    [SerializeField] private string coinCatvaName = "CoinCatva";
    [SerializeField] private float defaultJumpValue = 0f;
    [SerializeField] private float firstBridgeCoinVerticalOffset = 1.2f;

    [SerializeField] private bool showOnStart = false;
    [SerializeField] private float pushableBoxMass = 8f;
    [SerializeField] private float pushableBoxDrag = 3.5f;
    [SerializeField] private float pushableBoxAngularDrag = 2f;
    [SerializeField] private float boxRespawnY = -20f;

    private bool visible;
    private string jumpInput = "0";
    private bool boxPushable = false;
    private string boxInput = "false";
    // Default hidden: "!islandVisible = true"
    private string islandInput = "true";
    private string rocketEngineInput = "false";
    private string rocketFuelInput = "0";
    private string rocketThrustInput = "0";
    private string rocketDragInput = "0";
    private string rocketMassInput = "3";
    private string rocketStabilizerInput = "false";
    private string rocketLaunchInput = "false";
    private string jumpValidationMessage = string.Empty;
    private bool showHint;
    private CoinRotator activeCoin;
    private CoinRotator.CoinMode activeMode = CoinRotator.CoinMode.JumpAndBox;
    private Vector3 targetBoxSpawnPosition;
    private Quaternion targetBoxSpawnRotation;
    private float targetBoxOriginalMass;
    private float targetBoxOriginalDrag;
    private float targetBoxOriginalAngularDrag;
    private bool targetBoxStateCaptured;
    private float defaultBeanJumpValue;
    private float defaultFpsJumpValue;
    private bool defaultsCaptured;
    private Transform revealIslandRoot;
    private bool revealIslandActive;
    private bool revealIslandInitialized;
    private Transform bridgeRevealRoot;
    private bool bridgeRevealActive;
    private bool bridgeRevealInitialized;
    private bool bridgeRevealLocked;
    private string bridgeInput = "false";
    private CoinRotator hiddenFirstCoin;
    private CoinRotator runtimeFirstBridgeCoin;
    private CoinRotator coinCatva;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        visible = showOnStart;
        TryAutoAssign();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("PickupUIController");
            go.AddComponent<PickupUIController>();
        }
    }

    private void TryAutoAssign()
    {
        if (beanPlayer == null)
        {
            beanPlayer = PlayerCache.GetBean();
        }

        if (fpsPlayer == null)
        {
            fpsPlayer = PlayerCache.GetFps();
        }

        if (targetBox == null)
        {
            foreach (var rb in FindObjectsOfType<Rigidbody>())
            {
                string lower = rb.gameObject.name.ToLower();
                if (lower.Contains("box") || lower.Contains("movable"))
                {
                    targetBox = rb;
                    break;
                }
            }
        }

        if (targetBox != null)
        {
            CacheTargetBoxState();
            RestoreDefaults();
        }

        if (!defaultsCaptured)
        {
            defaultBeanJumpValue = Mathf.Clamp(defaultJumpValue, 0f, MaxJumpValue);
            defaultFpsJumpValue = Mathf.Clamp(defaultJumpValue, 0f, MaxJumpValue);
            defaultsCaptured = beanPlayer != null || fpsPlayer != null;
        }

        EnsureRevealIslandSetup();
        EnsureBridgeRevealSetup();
    }

    private void Update()
    {
        if (targetBox == null)
        {
            TryAutoAssign();
        }

        if (boxPushable && targetBox.position.y < boxRespawnY)
        {
            RespawnTargetBox();
        }

        if (visible && WasKeyPressedThisFrame(Key.L))
        {
            ExitMode();
        }

        if (visible && WasKeyPressedThisFrame(Key.H))
        {
            showHint = !showHint;
        }
    }

    private static bool WasKeyPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    private void CacheTargetBoxState()
    {
        if (targetBox == null || targetBoxStateCaptured)
        {
            return;
        }

        targetBoxSpawnPosition = targetBox.position;
        targetBoxSpawnRotation = targetBox.rotation;
        targetBoxOriginalMass = targetBox.mass;
        targetBoxOriginalDrag = targetBox.drag;
        targetBoxOriginalAngularDrag = targetBox.angularDrag;
        targetBoxStateCaptured = true;
    }

    private void ApplyTargetBoxPhysics()
    {
        if (targetBox == null)
        {
            return;
        }

        CacheTargetBoxState();

        targetBox.isKinematic = !boxPushable;
        if (boxPushable)
        {
            targetBox.mass = Mathf.Max(targetBoxOriginalMass, pushableBoxMass);
            targetBox.drag = Mathf.Max(targetBoxOriginalDrag, pushableBoxDrag);
            targetBox.angularDrag = Mathf.Max(targetBoxOriginalAngularDrag, pushableBoxAngularDrag);
        }
        else
        {
            targetBox.mass = targetBoxOriginalMass;
            targetBox.drag = targetBoxOriginalDrag;
            targetBox.angularDrag = targetBoxOriginalAngularDrag;
        }
    }

    private void RespawnTargetBox()
    {
        if (targetBox == null)
        {
            return;
        }

        CacheTargetBoxState();
        if (!targetBox.isKinematic)
        {
            targetBox.velocity = Vector3.zero;
            targetBox.angularVelocity = Vector3.zero;
        }

        targetBox.position = targetBoxSpawnPosition;
        targetBox.rotation = targetBoxSpawnRotation;
        targetBox.Sleep();
    }

    public void Show(CoinRotator coin, CoinRotator.CoinMode mode)
    {
        activeCoin = coin;
        activeMode = mode;
        showHint = false;
        RestoreDefaults();
        if (activeMode == CoinRotator.CoinMode.JumpAndBox)
        {
            float jumpDefault = Mathf.Clamp(defaultJumpValue, 0f, MaxJumpValue);
            jumpInput = jumpDefault.ToString("0.###");

            if (beanPlayer == null)
            {
                beanPlayer = PlayerCache.GetBean();
            }

            if (fpsPlayer == null)
            {
                fpsPlayer = PlayerCache.GetFps();
            }

            if (beanPlayer != null)
            {
                beanPlayer.SetJumpForce(jumpDefault);
            }

            if (fpsPlayer != null)
            {
                fpsPlayer.SetJumpVelocity(jumpDefault);
            }
        }
        else if (activeMode == CoinRotator.CoinMode.IslandReveal)
        {
            islandInput = "true"; // !islandVisible = true -> hidden by default
            SetRevealIslandState(false);
        }
        else if (activeMode == CoinRotator.CoinMode.RocketLanding)
        {
            ResetRocketCodeFields();
            RocketLandingPuzzle.EnsureInstance().BeginCoinSession();
        }

        visible = true;
    }

    public void ShowRocketExperiment()
    {
        activeCoin = null;
        activeMode = CoinRotator.CoinMode.RocketLanding;
        showHint = false;
        jumpValidationMessage = string.Empty;
        ResetRocketCodeFields();
        RocketLandingPuzzle.EnsureInstance().BeginCoinSession();
        visible = true;
    }

    private void ResetRocketCodeFields()
    {
        rocketEngineInput = "false";
        rocketFuelInput = "0";
        rocketThrustInput = "0";
        rocketDragInput = "0";
        rocketMassInput = "3";
        rocketStabilizerInput = "false";
        rocketLaunchInput = "false";
    }

    private void RestoreDefaults()
    {
        if (beanPlayer == null)
        {
            beanPlayer = PlayerCache.GetBean();
        }

        if (fpsPlayer == null)
        {
            fpsPlayer = PlayerCache.GetFps();
        }

        if (!defaultsCaptured)
        {
            if (beanPlayer != null)
            {
                defaultBeanJumpValue = beanPlayer.GetJumpForce();
            }

            if (fpsPlayer != null)
            {
                defaultFpsJumpValue = fpsPlayer.GetJumpVelocity();
            }

            defaultsCaptured = beanPlayer != null || fpsPlayer != null;
        }

        if (beanPlayer != null)
        {
            beanPlayer.SetJumpForce(defaultBeanJumpValue);
            jumpInput = defaultBeanJumpValue.ToString("0.###");
        }
        else if (fpsPlayer != null)
        {
            jumpInput = defaultFpsJumpValue.ToString("0.###");
        }

        if (fpsPlayer != null)
        {
            fpsPlayer.SetJumpVelocity(defaultFpsJumpValue);
            if (beanPlayer == null)
            {
                jumpInput = defaultFpsJumpValue.ToString("0.###");
            }
        }

        if (activeMode == CoinRotator.CoinMode.JumpAndBox)
        {
            boxPushable = false;
            boxInput = "false";
            ApplyTargetBoxPhysics();
            RespawnTargetBox();
        }
        else
        {
            boxInput = boxPushable ? "true" : "false";
        }

        islandInput = revealIslandActive ? "true" : "false";
        bridgeInput = bridgeRevealActive ? "true" : "false";
        if (activeMode == CoinRotator.CoinMode.RocketLanding)
        {
            ResetRocketCodeFields();
        }
        jumpValidationMessage = string.Empty;
    }

    private void ExitMode()
    {
        if (activeMode == CoinRotator.CoinMode.RocketLanding)
        {
            RocketLandingPuzzle existing = RocketLandingPuzzle.Instance;
            if (existing != null)
            {
                existing.EndCoinSession();
            }
        }

        RestoreDefaults();
        visible = false;
        showHint = false;

        if (activeCoin != null)
        {
            activeCoin.ResetPickup();
            activeCoin = null;
        }
    }

    public void HideOverlayOnly()
    {
        if (activeMode == CoinRotator.CoinMode.RocketLanding)
        {
            RocketLandingPuzzle existing = RocketLandingPuzzle.Instance;
            if (existing != null)
            {
                existing.EndCoinSession();
            }
        }

        RestoreDefaults();
        visible = false;
        showHint = false;
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        bool useMobileLayout = Input.touchSupported || Application.isMobilePlatform;
        float width = useMobileLayout ? 760f : 480f;
        float height = activeMode == CoinRotator.CoinMode.RocketLanding
            ? (useMobileLayout ? 740f : 520f)
            : (useMobileLayout ? 460f : 300f);
        Rect rect = new Rect(Screen.width - width - 16f, 16f, width, height);
        GUI.Box(rect, GUIContent.none);

        GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f));

        bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
        int labelFontSize = useMobileLayout ? 28 : 16;
        int textFieldFontSize = useMobileLayout ? 28 : 16;
        float labelWidth = useMobileLayout ? 240f : 140f;
        float compactLabelWidth = useMobileLayout ? 240f : 100f;

        int oldGlobalLabelSize = GUI.skin.label.fontSize;
        int oldGlobalTextSize = GUI.skin.textField.fontSize;
        GUI.skin.label.fontSize = labelFontSize;
        GUI.skin.textField.fontSize = textFieldFontSize;
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        if (useMobileLayout)
        {
            labelStyle.fixedHeight = 40f;
            labelStyle.padding = new RectOffset(0, 0, 6, 6);
            labelStyle.wordWrap = true;
        }
        if (useMobileLayout)
        {
            textFieldStyle.fixedHeight = 42f;
            textFieldStyle.padding = new RectOffset(8, 8, 6, 6);
        }

        if (activeMode == CoinRotator.CoinMode.JumpAndBox)
        {
            GUI.SetNextControlName("JumpField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("jumpVelocity =", labelStyle, GUILayout.Width(labelWidth));
            jumpInput = GUILayout.TextField(jumpInput, 12, textFieldStyle);
            GUILayout.EndHorizontal();

            if (float.TryParse(jumpInput, out float jp))
            {
                bool applyNow = enterPressed ? GUI.GetNameOfFocusedControl() == "JumpField" : true;
                if (applyNow)
                {
                    if (jp > MaxJumpValue)
                    {
                        jumpValidationMessage = "max value 10";
                    }
                    else
                    {
                        jumpValidationMessage = string.Empty;

                        if (beanPlayer == null) beanPlayer = PlayerCache.GetBean();
                        if (fpsPlayer == null) fpsPlayer = PlayerCache.GetFps();

                        if (beanPlayer != null) beanPlayer.SetJumpForce(jp);
                        if (fpsPlayer != null) fpsPlayer.SetJumpVelocity(jp);
                    }
                }
            }

            if (!string.IsNullOrEmpty(jumpValidationMessage))
            {
                GUILayout.Label(jumpValidationMessage);
            }
            GUI.SetNextControlName("BoxField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("boxRigidbody =", labelStyle, GUILayout.Width(compactLabelWidth));
            boxInput = GUILayout.TextField(boxInput, 8, textFieldStyle).ToLowerInvariant();
            GUILayout.EndHorizontal();
            if (enterPressed && GUI.GetNameOfFocusedControl() == "BoxField")
            {
                bool parsed = boxInput == "true";
                if (parsed != boxPushable)
                {
                    boxPushable = parsed;
                    ApplyTargetBoxPhysics();
                }
                if (boxPushable && float.TryParse(jumpInput, out float currentJp) && currentJp > 0f)
                {
                    PauseMenuManager.CompleteTaskByTitle("Set Jump Power");
                }
            }
        }
        else if (activeMode == CoinRotator.CoinMode.RocketLanding)
        {
            GUILayout.Label("// Broken rocket repair console", labelStyle);
            GUI.SetNextControlName("RocketEngineField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("engineEnabled =", labelStyle, GUILayout.Width(labelWidth));
            rocketEngineInput = GUILayout.TextField(rocketEngineInput, 8, textFieldStyle).ToLowerInvariant();
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketFuelField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("fuel =", labelStyle, GUILayout.Width(labelWidth));
            rocketFuelInput = GUILayout.TextField(rocketFuelInput, 12, textFieldStyle);
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketThrustField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("rocketThrust =", labelStyle, GUILayout.Width(labelWidth));
            rocketThrustInput = GUILayout.TextField(rocketThrustInput, 12, textFieldStyle);
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketDragField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("rocketDrag =", labelStyle, GUILayout.Width(labelWidth));
            rocketDragInput = GUILayout.TextField(rocketDragInput, 12, textFieldStyle);
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketMassField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("rocketMass =", labelStyle, GUILayout.Width(labelWidth));
            rocketMassInput = GUILayout.TextField(rocketMassInput, 12, textFieldStyle);
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketStabilizerField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("stabilizer =", labelStyle, GUILayout.Width(labelWidth));
            rocketStabilizerInput = GUILayout.TextField(rocketStabilizerInput, 8, textFieldStyle).ToLowerInvariant();
            GUILayout.EndHorizontal();

            GUI.SetNextControlName("RocketLaunchField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("launchReady =", labelStyle, GUILayout.Width(labelWidth));
            rocketLaunchInput = GUILayout.TextField(rocketLaunchInput, 8, textFieldStyle).ToLowerInvariant();
            GUILayout.EndHorizontal();

            if (enterPressed)
            {
                bool parsedFuel = float.TryParse(rocketFuelInput, out float fuel);
                bool parsedThrust = float.TryParse(rocketThrustInput, out float thrust);
                bool parsedDrag = float.TryParse(rocketDragInput, out float drag);
                bool parsedMass = float.TryParse(rocketMassInput, out float mass);

                if (!parsedFuel || !parsedThrust || !parsedDrag || !parsedMass)
                {
                    jumpValidationMessage = "fuel, thrust, drag, and mass must be numbers";
                }
                else
                {
                    bool engineEnabled = ParseBoolInput(rocketEngineInput);
                    bool stabilizerEnabled = ParseBoolInput(rocketStabilizerInput);
                    bool launchReady = ParseBoolInput(rocketLaunchInput);
                    RocketLandingPuzzle.EnsureInstance().TryApplyCode(
                        engineEnabled,
                        fuel,
                        thrust,
                        drag,
                        mass,
                        stabilizerEnabled,
                        launchReady,
                        out jumpValidationMessage);
                }
            }

            if (!string.IsNullOrEmpty(jumpValidationMessage))
            {
                GUILayout.Label(jumpValidationMessage, labelStyle);
            }
        }
        else
        {
            string fieldName = activeMode == CoinRotator.CoinMode.IslandReveal ? "IslandField" : "BridgeField";
            string label = activeMode == CoinRotator.CoinMode.IslandReveal ? IslandVisibleLabel : BridgeVisibleLabel;
            GUI.SetNextControlName(fieldName);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(compactLabelWidth));
            if (activeMode == CoinRotator.CoinMode.IslandReveal)
            {
                islandInput = GUILayout.TextField(islandInput, 8, textFieldStyle).ToLowerInvariant();
            }
            else
            {
                bridgeInput = GUILayout.TextField(bridgeInput, 8, textFieldStyle).ToLowerInvariant();
            }
            GUILayout.EndHorizontal();
            if (enterPressed && GUI.GetNameOfFocusedControl() == fieldName)
            {
                if (activeMode == CoinRotator.CoinMode.IslandReveal)
                {
                    bool hide = islandInput == "true";
                    SetRevealIslandState(!hide);
                }
                else
                {
                    SetBridgeRevealState(bridgeInput == "true");
                    if (bridgeRevealActive)
                    {
                        PauseMenuManager.CompleteTaskByTitle("Reveal Bridge Path");
                        RestoreDefaults();
                        visible = false;
                        activeCoin = null;
                    }
                }
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Press L to hide this", labelStyle);
        GUILayout.Label("Press H for hint", labelStyle);
        if (showHint)
        {
            GUILayout.Space(4f);
            string hint = activeMode switch
            {
                CoinRotator.CoinMode.JumpAndBox => "Hint: jumpVelocity 0-10; set boxRigidbody = true to push the box, false to lock it.",
                CoinRotator.CoinMode.IslandReveal => "Hint: !islandVisible uses inverse logic. true keeps the island hidden, false reveals it.",
                CoinRotator.CoinMode.BridgeReveal => "Hint: viewPod controls the bridge. true shows it, false hides it.",
                CoinRotator.CoinMode.RocketLanding => "Hint: set engineEnabled=true, fuel=70, rocketThrust=60, rocketDrag=0.8, rocketMass=3, stabilizer=true, launchReady=true.",
                _ => "Hint unavailable for this mode."
            };
            GUILayout.Label(hint, labelStyle);
        }

        GUILayout.EndArea();

        if (enterPressed)
        {
            GUI.FocusControl(null); // exit any focused field when Enter is pressed
        }

        GUI.skin.label.fontSize = oldGlobalLabelSize;
        GUI.skin.textField.fontSize = oldGlobalTextSize;
    }

    private void EnsureRevealIslandSetup()
    {
        if (!revealIslandInitialized)
        {
            revealIslandRoot = FindRevealIslandRoot();
            coinCatva = FindCoinByName(coinCatvaName);
            revealIslandInitialized = true;
        }

        if (revealIslandRoot != null)
        {
            SetRevealIslandState(false);
        }

        if (coinCatva != null)
        {
            coinCatva.gameObject.SetActive(false);
        }
    }

    private void EnsureBridgeRevealSetup()
    {
        if (bridgeRevealInitialized)
        {
            return;
        }

        hiddenFirstCoin = FindCoinByName(firstCoinName);
        if (hiddenFirstCoin == null)
        {
            bridgeRevealInitialized = true;
            return;
        }

        bridgeRevealRoot = FindBridgeRevealRoot(hiddenFirstCoin.transform);

        runtimeFirstBridgeCoin = FindCoinByName(firstBridgeCoinName);
        if (runtimeFirstBridgeCoin == null)
        {
            runtimeFirstBridgeCoin = SpawnFirstBridgeCoin(hiddenFirstCoin);
        }
        else
        {
            runtimeFirstBridgeCoin.ConfigureRuntime(CoinRotator.CoinMode.BridgeReveal, false);
        }

        hiddenFirstCoin.gameObject.SetActive(false);
        if (bridgeRevealRoot != null)
        {
            SetObjectTreeVisible(bridgeRevealRoot, false);
        }

        bridgeRevealActive = false;
        bridgeRevealLocked = false;
        bridgeRevealInitialized = true;
    }

    private Transform FindRevealIslandRoot()
    {
        if (!string.IsNullOrWhiteSpace(revealIslandName))
        {
            GameObject direct = GameObject.Find(revealIslandName);
            if (direct != null)
            {
                return direct.transform;
            }
        }

        string[] candidates = { "islande", "HardOuterIsland", "MainDifficultyIsland" };
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject direct = GameObject.Find(candidates[i]);
            if (direct != null)
            {
                return direct.transform;
            }
        }

        foreach (Transform candidate in FindObjectsOfType<Transform>(true))
        {
            if (candidate != null && candidate.name.ToLowerInvariant().Contains("islande"))
            {
                return candidate;
            }
        }

        return null;
    }

    private CoinRotator FindCoinByName(string coinName)
    {
        if (string.IsNullOrWhiteSpace(coinName))
        {
            return null;
        }

        CoinRotator[] coins = FindObjectsOfType<CoinRotator>(true);
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] != null && coins[i].name == coinName)
            {
                return coins[i];
            }
        }

        return null;
    }

    private CoinRotator SpawnFirstBridgeCoin(CoinRotator sourceCoin)
    {
        if (sourceCoin == null)
        {
            return null;
        }

        Transform target = FindFirstBridgeCoinTarget();
        if (target == null)
        {
            return null;
        }

        Vector3 spawnPosition = CalculateTargetTopCoinPosition(target);

        GameObject clone = Instantiate(sourceCoin.gameObject, spawnPosition, Quaternion.identity);
        clone.name = firstBridgeCoinName;
        clone.SetActive(true);

        CoinRotator cloneCoin = clone.GetComponent<CoinRotator>();
        if (cloneCoin != null)
        {
            cloneCoin.ConfigureRuntime(CoinRotator.CoinMode.BridgeReveal, false);
        }

        return cloneCoin;
    }

    private Transform FindFirstBridgeCoinTarget()
    {
        string[] candidates = { firstBridgeCoinTargetName, "boxcoin3", "boxcoins3", "boxcoin 3", "boxcoin_3" };
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);

        for (int i = 0; i < candidates.Length; i++)
        {
            string wanted = candidates[i];
            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            for (int j = 0; j < allTransforms.Length; j++)
            {
                Transform candidate = allTransforms[j];
                if (candidate == null)
                {
                    continue;
                }

                string lower = candidate.name.ToLowerInvariant();
                string wantedLower = wanted.ToLowerInvariant();
                if (lower == wantedLower || lower.Contains(wantedLower))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private Vector3 CalculateTargetTopCoinPosition(Transform target)
    {
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            return new Vector3(
                targetCollider.bounds.center.x,
                targetCollider.bounds.max.y + firstBridgeCoinVerticalOffset,
                targetCollider.bounds.center.z);
        }

        return target.position + Vector3.up * firstBridgeCoinVerticalOffset;
    }

    private Transform FindBridgeRevealRoot(Transform referenceCoin)
    {
        if (!string.IsNullOrWhiteSpace(bridgeRevealName))
        {
            Transform[] allTransformsByName = FindObjectsOfType<Transform>(true);
            string wanted = bridgeRevealName.ToLowerInvariant();
            for (int i = 0; i < allTransformsByName.Length; i++)
            {
                Transform candidate = allTransformsByName[i];
                if (candidate == null)
                {
                    continue;
                }

                string lower = candidate.name.ToLowerInvariant();
                if (lower == wanted || lower.Contains(wanted))
                {
                    return candidate;
                }
            }
        }

        if (referenceCoin == null)
        {
            return null;
        }

        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 referencePoint = referenceCoin.position;

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (!lowerName.Contains("bridge") ||
                lowerName.Contains("collider") ||
                lowerName.Contains("water") ||
                lowerName.Contains("join"))
            {
                continue;
            }

            if (candidate.GetComponentInChildren<Renderer>(true) == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(candidate.position - referencePoint);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void SetRevealIslandState(bool enabled)
    {
        revealIslandActive = enabled;
        // Field represents !islandVisible, so store inverted value.
        islandInput = enabled ? "false" : "true";

        if (revealIslandRoot == null)
        {
            revealIslandRoot = FindRevealIslandRoot();
            if (revealIslandRoot == null)
            {
                return;
            }
        }

        foreach (Renderer renderer in revealIslandRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = enabled;
        }

        foreach (Collider collider in revealIslandRoot.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = enabled;
        }

        if (coinCatva != null)
        {
            coinCatva.gameObject.SetActive(enabled);
        }
    }

    private void SetBridgeRevealState(bool enabled)
    {
        if (!enabled && bridgeRevealLocked)
        {
            bridgeRevealActive = true;
            bridgeInput = "true";
            return;
        }

        bridgeRevealActive = enabled;
        bridgeInput = enabled ? "true" : "false";
        bridgeRevealLocked |= enabled;

        if (bridgeRevealRoot != null)
        {
            SetObjectTreeVisible(bridgeRevealRoot, enabled);
        }

        if (hiddenFirstCoin != null)
        {
            hiddenFirstCoin.gameObject.SetActive(enabled);
        }
    }

    private void SetObjectTreeVisible(Transform root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }

    private static bool ParseBoolInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized == "true" || normalized == "1" || normalized == "yes";
    }
}
