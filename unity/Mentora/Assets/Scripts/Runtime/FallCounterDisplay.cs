using UnityEngine;

public static class FallCounterDisplay
{
    private const string CounterName = "FallCounterText";
    private static TextMesh textMesh;

    public static void CreateInSceneIfMissing()
    {
        GameObject counterObject = GameObject.Find(CounterName);
        if (counterObject == null)
        {
            counterObject = new GameObject(CounterName);
        }

        textMesh = counterObject.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = counterObject.AddComponent<TextMesh>();
        }

        textMesh.fontSize = 72;
        textMesh.characterSize = 0.08f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.text = "Times fallen: 0";

        counterObject.transform.position = new Vector3(0f, 0.06f, 6.5f);
        counterObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        counterObject.transform.localScale = Vector3.one;
    }

    public static void EnsureExists()
    {
        if (textMesh != null)
        {
            return;
        }

        GameObject counterObject = GameObject.Find(CounterName);
        if (counterObject == null)
        {
            return;
        }

        textMesh = counterObject.GetComponent<TextMesh>();
    }

    public static void SetCount(int count)
    {
        EnsureExists();
        if (textMesh != null)
        {
            textMesh.text = "Times fallen: " + count;
        }
    }
}
