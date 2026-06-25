using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Type "hitler" anywhere in-game to spawn history's most famous failed art school applicant.
/// </summary>
public class EasterEgg : MonoBehaviour
{
    private const string Code = "hitler";
    private string buffer = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene") return;
        new GameObject("EasterEgg").AddComponent<EasterEgg>();
    }

    // Only the keys that appear in "hitler"
    private static readonly (Key key, char ch)[] WatchedKeys =
    {
        (Key.H, 'h'), (Key.I, 'i'), (Key.T, 't'),
        (Key.L, 'l'), (Key.E, 'e'), (Key.R, 'r'),
    };

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        foreach (var (key, ch) in WatchedKeys)
        {
            if (!kb[key].wasPressedThisFrame) continue;

            buffer += ch;
            if (buffer.Length > Code.Length)
                buffer = buffer.Substring(buffer.Length - Code.Length);

            if (buffer == Code)
            {
                buffer = "";
                Spawn();
            }
        }
    }

    private void Spawn()
    {
        var player = PlayerCache.ResolvePlayerTransform();
        Vector3 spawnPos = player != null
            ? player.position + player.forward * 3f + Vector3.up * 0.1f
            : new Vector3(75f, 1f, 225f);

        // ── Build the painter out of primitives ──────────────────────────────

        var root = new GameObject("FamousPainter");
        root.transform.position = spawnPos;

        // Body
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale    = new Vector3(0.55f, 0.7f, 0.55f);
        SetColor(body, new Color(0.25f, 0.20f, 0.15f)); // dark coat

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        head.transform.localScale    = Vector3.one * 0.45f;
        SetColor(head, new Color(0.96f, 0.82f, 0.70f)); // skin

        // Little moustache
        var stache = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stache.transform.SetParent(root.transform, false);
        stache.transform.localPosition = new Vector3(0f, 1.83f, 0.22f);
        stache.transform.localScale    = new Vector3(0.10f, 0.05f, 0.04f);
        SetColor(stache, new Color(0.1f, 0.07f, 0.05f)); // dark brown

        // Painter's beret
        var beret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beret.transform.SetParent(root.transform, false);
        beret.transform.localPosition = new Vector3(0.06f, 2.10f, 0f);
        beret.transform.localScale    = new Vector3(0.38f, 0.07f, 0.38f);
        SetColor(beret, new Color(0.12f, 0.12f, 0.35f)); // dark beret

        // Painting canvas (flat cube)
        var canvas = GameObject.CreatePrimitive(PrimitiveType.Cube);
        canvas.transform.SetParent(root.transform, false);
        canvas.transform.localPosition = new Vector3(0.6f, 1.1f, 0f);
        canvas.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        canvas.transform.localScale    = new Vector3(0.06f, 0.55f, 0.45f);
        SetColor(canvas, new Color(0.95f, 0.92f, 0.80f)); // canvas beige

        // Easel leg
        var easel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        easel.transform.SetParent(root.transform, false);
        easel.transform.localPosition = new Vector3(0.55f, 0.5f, 0f);
        easel.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
        easel.transform.localScale    = new Vector3(0.05f, 0.55f, 0.05f);
        SetColor(easel, new Color(0.55f, 0.38f, 0.18f)); // wood

        // Paintbrush arm
        var brush = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brush.transform.SetParent(root.transform, false);
        brush.transform.localPosition = new Vector3(0.35f, 1.35f, 0f);
        brush.transform.localRotation = Quaternion.Euler(0f, 0f, 55f);
        brush.transform.localScale    = new Vector3(0.04f, 0.28f, 0.04f);
        SetColor(brush, new Color(0.6f, 0.4f, 0.2f));

        // ── Floating name label ───────────────────────────────────────────────
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 2.7f, 0f);
        labelGo.transform.localScale    = Vector3.one * 0.012f;

        var labelCanvas = labelGo.AddComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;
        var rt = labelGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320f, 80f);

        var bg = new GameObject("BG");
        bg.transform.SetParent(labelGo.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.04f, 0.12f, 0.92f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(labelGo.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text      = "Adolf Hitler\n<size=11>Watercolor Painter, 1889–1945</size>";
        txt.fontSize  = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.supportRichText = true;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(8, 4); txtRt.offsetMax = new Vector2(-8, -4);

        // Face the player on spawn
        if (player != null)
        {
            Vector3 dir = player.position - root.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
                root.transform.rotation = Quaternion.LookRotation(-dir.normalized);
        }

        // Make label always face camera
        root.AddComponent<LabelFacer>().label = labelGo.transform;

        // Self-destruct after 15 seconds
        Destroy(root, 15f);

        Debug.Log("[EasterEgg] His dream was to get into art school. Respect the hustle.");
    }

    private static void SetColor(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }
}

public class LabelFacer : MonoBehaviour
{
    public Transform label;

    private void LateUpdate()
    {
        if (label == null) return;
        var cam = PlayerCache.GetFps()?.GetComponentInChildren<Camera>();
        if (cam == null) return;
        Vector3 dir = label.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.001f)
            label.rotation = Quaternion.LookRotation(dir);
    }
}
