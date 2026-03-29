using UnityEngine;

/// <summary>
/// Lightweight procedural walker to make a static rig move arms/legs based on character velocity.
/// Best-effort bone auto-detect by name; works even if no Animator/animation clips exist.
/// </summary>
public class ProceduralWalkController : MonoBehaviour
{
    [Header("Bones")]
    public Transform leftThigh;
    public Transform rightThigh;
    public Transform leftArm;
    public Transform rightArm;
    public Transform hips;

    [Header("Motion")]
    public float stepFrequency = 3.2f;
    public float legSwingDeg = 55f;
    public float armSwingDeg = 65f;
    public float hipBobHeight = 0.09f;
    public float speedToStepScale = 0.65f;

    private Quaternion lThighStart, rThighStart, lArmStart, rArmStart;
    private Vector3 hipsStartLocalPos;
    private float phase;
    private CharacterController cc;
    private Animator anim;

    public void SetKnownBones(Transform root)
    {
        if (root == null) return;
        leftThigh = FindChildByName(root, "thigh.L");
        rightThigh = FindChildByName(root, "thigh.R");
        leftArm = FindChildByName(root, "upper_arm.L");
        rightArm = FindChildByName(root, "upper_arm.R");
        hips = FindChildByName(root, "spine.002") ?? FindChildByName(root, "spine.001");
        CacheStarts();
    }

    private Transform FindChildByName(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private void Awake()
    {
        cc = GetComponentInParent<CharacterController>();
        anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.enabled = false; // ensure procedural control
        }
        AutoFindBones();
        CacheStarts();
    }

    private void CacheStarts()
    {
        if (leftThigh != null) lThighStart = leftThigh.localRotation;
        if (rightThigh != null) rThighStart = rightThigh.localRotation;
        if (leftArm != null) lArmStart = leftArm.localRotation;
        if (rightArm != null) rArmStart = rightArm.localRotation;
        if (hips != null) hipsStartLocalPos = hips.localPosition;
    }

    private void AutoFindBones()
    {
        Transform[] all = GetComponentsInChildren<Transform>();
        if (leftThigh == null) leftThigh = FindBone(all, side: -1, axisHint: Axis.YDown, keywords: new[] { "thigh.l", "thigh_l", "thigh", "leg" });
        if (rightThigh == null) rightThigh = FindBone(all, side: 1, axisHint: Axis.YDown, keywords: new[] { "thigh.r", "thigh_r", "thigh", "leg" });
        if (leftArm == null) leftArm = FindBone(all, side: -1, axisHint: Axis.YUp, keywords: new[] { "upper_arm.l", "upperarm.l", "arm.l", "shoulder.l", "upper_arm", "arm" });
        if (rightArm == null) rightArm = FindBone(all, side: 1, axisHint: Axis.YUp, keywords: new[] { "upper_arm.r", "upperarm.r", "arm.r", "shoulder.r", "upper_arm", "arm" });
        if (hips == null) hips = FindBone(all, side: 0, axisHint: Axis.Center, keywords: new[] { "spine.002", "spine.001", "spine", "hips", "pelvis", "root", "torso" });
    }

    private enum Axis { YUp, YDown, Center }

    private Transform FindBone(Transform[] all, int side, Axis axisHint, string[] keywords)
    {
        Transform best = null;
        float bestScore = -1f;
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            float score = 0f;
            foreach (var k in keywords)
            {
                if (n.Contains(k))
                {
                    score += 5f;
                    break;
                }
            }

            if (score <= 0f)
            {
                continue; // skip non-matching transforms
            }

            if (side < 0) score += t.localPosition.x < 0 ? 2f : 0f;
            if (side > 0) score += t.localPosition.x > 0 ? 2f : 0f;
            if (axisHint == Axis.YUp) score += Mathf.Max(0f, t.localPosition.y);
            if (axisHint == Axis.YDown) score += Mathf.Max(0f, -t.localPosition.y);
            if (axisHint == Axis.Center) score += 1f - Mathf.Abs(t.localPosition.x);

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        return best;
    }

    private void LateUpdate()
    {
        float speed = 0f;
        if (cc != null)
        {
            Vector3 v = cc.velocity;
            speed = new Vector3(v.x, 0f, v.z).magnitude;
        }

        float stepSpeed = speed * speedToStepScale;
        phase += Time.deltaTime * stepFrequency * Mathf.Clamp01(stepSpeed * 1.2f + 0.2f);

        float legSwing = Mathf.Sin(phase) * legSwingDeg * Mathf.Clamp01(stepSpeed);
        float armSwing = Mathf.Sin(phase + Mathf.PI) * armSwingDeg * Mathf.Clamp01(stepSpeed);
        float hipBob = Mathf.Sin(phase * 2f) * hipBobHeight * Mathf.Clamp01(stepSpeed);

        if (leftThigh != null) leftThigh.localRotation = lThighStart * Quaternion.AngleAxis(legSwing, leftThigh.right);
        if (rightThigh != null) rightThigh.localRotation = rThighStart * Quaternion.AngleAxis(-legSwing, rightThigh.right);
        if (leftArm != null) leftArm.localRotation = lArmStart * Quaternion.AngleAxis(-armSwing, leftArm.right);
        if (rightArm != null) rightArm.localRotation = rArmStart * Quaternion.AngleAxis(armSwing, rightArm.right);
        if (hips != null) hips.localPosition = hipsStartLocalPos + new Vector3(0f, hipBob, 0f);
    }
}
