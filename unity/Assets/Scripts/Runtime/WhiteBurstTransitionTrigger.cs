using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class WhiteBurstTransitionTrigger : MonoBehaviour
{
    [Header("Rise")]
    [SerializeField] private Transform riseAnchor;
    [SerializeField] private bool alignToAnchorXZ;
    [SerializeField] private float riseHeight = 2.2f;
    [SerializeField] private float riseDuration = 0.62f;
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Explosion")]
    [SerializeField] private ParticleSystem whiteExplosionPrefab;
    [SerializeField] private float delayBeforeExplosion = 0.05f;
    [SerializeField] private bool hideSphereDuringFade = true;

    [Header("Fullscreen White Transition")]
    [SerializeField] private Image fullscreenWhiteImage;
    [SerializeField] private bool autoCreateFullscreenWhiteImage = true;
    [SerializeField] private float delayBeforeFade = 0.06f;
    [SerializeField] private float fadeToWhiteDuration = 0.55f;
    [SerializeField] private float holdWhiteDuration = 0.12f;
    [SerializeField] private float fadeFromWhiteDuration = 0.5f;

    [Header("Next Part")]
    [SerializeField] private GameObject[] objectsToEnable;
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private Transform postTransitionSpawnPoint;

    [Header("Events")]
    [SerializeField] private UnityEvent onReachedFullWhite;
    [SerializeField] private UnityEvent onTransitionComplete;

    private Collider cachedTrigger;
    private bool running;

    private void Awake()
    {
        cachedTrigger = GetComponent<Collider>();
        if (cachedTrigger != null)
        {
            cachedTrigger.isTrigger = true;
        }

        EnsureFullscreenFadeImage();
        SetFadeAlpha(0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (running)
        {
            return;
        }

        var sphere = other.GetComponent<BeanController>();
        if (sphere == null)
        {
            return;
        }

        StartCoroutine(PlaySequence(sphere));
    }

    public void SetObjectsToEnable(GameObject[] targets)
    {
        objectsToEnable = targets;
    }

    public void SetObjectsToDisable(GameObject[] targets)
    {
        objectsToDisable = targets;
    }

    public void SetPostTransitionSpawnPoint(Transform target)
    {
        postTransitionSpawnPoint = target;
    }

    private IEnumerator PlaySequence(BeanController sphere)
    {
        running = true;
        if (cachedTrigger != null)
        {
            cachedTrigger.enabled = false;
        }

        var rb = sphere.GetComponent<Rigidbody>();
        sphere.SetMovementLocked(true);
        sphere.SetHardFreeze(true);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 startPos = sphere.transform.position;
        Vector3 endPos = startPos + (Vector3.up * Mathf.Max(0.01f, riseHeight));

        if (alignToAnchorXZ && riseAnchor != null)
        {
            endPos.x = riseAnchor.position.x;
            endPos.z = riseAnchor.position.z;
        }

        float t = 0f;
        float safeRiseDuration = Mathf.Max(0.01f, riseDuration);
        while (t < safeRiseDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / safeRiseDuration);
            float eased = riseCurve != null ? riseCurve.Evaluate(normalized) : normalized;
            sphere.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        sphere.transform.position = endPos;

        if (delayBeforeExplosion > 0f)
        {
            yield return new WaitForSeconds(delayBeforeExplosion);
        }

        SpawnWhiteExplosion(endPos);

        if (hideSphereDuringFade)
        {
            SetSphereVisible(sphere, false);
        }

        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFade);
        }

        yield return FadeWhite(0f, 1f, fadeToWhiteDuration);

        onReachedFullWhite?.Invoke();
        ToggleObjects(objectsToDisable, false);
        ToggleObjects(objectsToEnable, true);

        if (postTransitionSpawnPoint != null)
        {
            sphere.transform.position = postTransitionSpawnPoint.position;
        }

        if (holdWhiteDuration > 0f)
        {
            yield return new WaitForSeconds(holdWhiteDuration);
        }

        if (hideSphereDuringFade)
        {
            SetSphereVisible(sphere, true);
        }

        sphere.SetHardFreeze(false);
        sphere.SetMovementLocked(false);

        yield return FadeWhite(1f, 0f, fadeFromWhiteDuration);

        onTransitionComplete?.Invoke();
        running = false;
    }

    private void EnsureFullscreenFadeImage()
    {
        if (fullscreenWhiteImage != null || !autoCreateFullscreenWhiteImage)
        {
            return;
        }

        var canvasObject = new GameObject("WhiteTransitionCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasObject.AddComponent<GraphicRaycaster>();

        var imageObject = new GameObject("WhiteTransitionImage");
        imageObject.transform.SetParent(canvasObject.transform, false);

        var rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fullscreenWhiteImage = imageObject.AddComponent<Image>();
        fullscreenWhiteImage.color = Color.white;
        fullscreenWhiteImage.raycastTarget = false;
    }

    private IEnumerator FadeWhite(float from, float to, float duration)
    {
        SetFadeAlpha(from);

        if (duration <= 0.001f)
        {
            SetFadeAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float smooth = k * k * (3f - (2f * k));
            SetFadeAlpha(Mathf.Lerp(from, to, smooth));
            yield return null;
        }

        SetFadeAlpha(to);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fullscreenWhiteImage == null)
        {
            return;
        }

        Color c = fullscreenWhiteImage.color;
        c.r = 1f;
        c.g = 1f;
        c.b = 1f;
        c.a = Mathf.Clamp01(alpha);
        fullscreenWhiteImage.color = c;
        fullscreenWhiteImage.enabled = c.a > 0.0001f;
    }

    private void SpawnWhiteExplosion(Vector3 position)
    {
        if (whiteExplosionPrefab != null)
        {
            ParticleSystem fx = Instantiate(whiteExplosionPrefab, position, Quaternion.identity);
            fx.Play();
            float destroyAfter = fx.main.duration + fx.main.startLifetime.constantMax + 0.2f;
            Destroy(fx.gameObject, destroyAfter);
            return;
        }

        var fxRoot = new GameObject("WhiteBurstFX");
        fxRoot.transform.position = position;
        var ps = fxRoot.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.45f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.25f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.95f));
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.96f, 0.98f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.1f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.05f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.color = Color.white;
                renderer.material = material;
            }
        }

        ps.Play();
        Destroy(fxRoot, 1.5f);
    }

    private static void ToggleObjects(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private static void SetSphereVisible(BeanController sphere, bool visible)
    {
        if (sphere == null)
        {
            return;
        }

        var renderers = sphere.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }
    }
}
