using UnityEngine;

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

        if (visible && Input.GetKeyDown(KeyCode.L))
        {
            ExitMode();
        }

        if (visible && Input.GetKeyDown(KeyCode.H))
        {
            showHint = !showHint;
        }
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
        targetBox.velocity = Vector3.zero;
        targetBox.angularVelocity = Vector3.zero;
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

        visible = true;
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
        jumpValidationMessage = string.Empty;
    }

    private void ExitMode()
    {
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

        const float width = 480f;
        const float height = 300f;
        Rect rect = new Rect(Screen.width - width - 16f, 16f, width, height);
        GUI.Box(rect, GUIContent.none);

        GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f));

        bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

        if (activeMode == CoinRotator.CoinMode.JumpAndBox)
        {
            // Slightly larger font for readability.
            var oldLabelSize = GUI.skin.label.fontSize;
            var oldTextSize = GUI.skin.textField.fontSize;
            GUI.skin.label.fontSize = 16;
            GUI.skin.textField.fontSize = 16;

            GUI.SetNextControlName("JumpField");
            GUILayout.BeginHorizontal();
            GUILayout.Label("jumpVelocity =", GUILayout.Width(140f));
            jumpInput = GUILayout.TextField(jumpInput, 12);
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
            GUILayout.Label("boxRigidbody =", GUILayout.Width(100f));
            boxInput = GUILayout.TextField(boxInput, 8).ToLowerInvariant();
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

            GUI.skin.label.fontSize = oldLabelSize;
            GUI.skin.textField.fontSize = oldTextSize;
        }
        else
        {
            string fieldName = activeMode == CoinRotator.CoinMode.IslandReveal ? "IslandField" : "BridgeField";
            string label = activeMode == CoinRotator.CoinMode.IslandReveal ? IslandVisibleLabel : BridgeVisibleLabel;
            GUI.SetNextControlName(fieldName);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100f));
            if (activeMode == CoinRotator.CoinMode.IslandReveal)
            {
                islandInput = GUILayout.TextField(islandInput, 8).ToLowerInvariant();
            }
            else
            {
                bridgeInput = GUILayout.TextField(bridgeInput, 8).ToLowerInvariant();
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
        GUILayout.Label("Press L to hide this");
        GUILayout.Label("Press H for hint");
        if (showHint)
        {
            GUILayout.Space(4f);
            string hint = activeMode switch
            {
                CoinRotator.CoinMode.JumpAndBox => "Hint: jumpVelocity 0-10; set boxRigidbody = true to push the box, false to lock it.",
                CoinRotator.CoinMode.IslandReveal => "Hint: !islandVisible uses inverse logic. true keeps the island hidden, false reveals it.",
                CoinRotator.CoinMode.BridgeReveal => "Hint: viewPod controls the bridge. true shows it, false hides it.",
                _ => "Hint unavailable for this mode."
            };
            GUILayout.Label(hint);
        }

        GUILayout.EndArea();

        if (enterPressed)
        {
            GUI.FocusControl(null); // exit any focused field when Enter is pressed
        }
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
}
