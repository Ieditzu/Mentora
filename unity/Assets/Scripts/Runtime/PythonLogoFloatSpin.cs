using System.Collections;
using UnityEngine;

public class PythonLogoFloatSpin : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 32f;
    [SerializeField] private float bobAmplitude = 0.035f;
    [SerializeField] private float bobFrequency = 1.2f;

    private Vector3 startLocalPosition;
    private float phaseOffset;
    private Vector3 fixedWorldEulerAngles;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        fixedWorldEulerAngles = transform.rotation.eulerAngles;
    }

    private void OnEnable()
    {
        startLocalPosition = transform.localPosition;
        fixedWorldEulerAngles = transform.rotation.eulerAngles;
    }

    private void Update()
    {
        fixedWorldEulerAngles.y += rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(fixedWorldEulerAngles);

        float bobOffset = Mathf.Sin((Time.time * bobFrequency) + phaseOffset) * bobAmplitude;
        Vector3 localPosition = startLocalPosition;
        localPosition.y += bobOffset;
        transform.localPosition = localPosition;
    }
}

public static class PythonLogoFloatSpinBootstrap
{
    private static readonly string[] TargetNames =
    {
        "PythonLogoVisual",
        "PythonLogoMarker",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToPythonLogos()
    {
        GameObject bootstrap = new GameObject("PythonLogoFloatSpinBootstrap");
        Object.DontDestroyOnLoad(bootstrap);
        bootstrap.hideFlags = HideFlags.HideAndDontSave;
        bootstrap.AddComponent<PythonLogoFloatSpinBootstrapRunner>();
    }

    private sealed class PythonLogoFloatSpinBootstrapRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            for (int i = 0; i < 120; i++)
            {
                AttachToTargets();
                yield return null;
            }

            Destroy(gameObject);
        }

        private static void AttachToTargets()
        {
            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject candidate = allObjects[i];
                if (!IsTargetName(candidate.name))
                {
                    continue;
                }

                if (candidate.GetComponent<PythonLogoFloatSpin>() != null)
                {
                    continue;
                }

                candidate.AddComponent<PythonLogoFloatSpin>();
            }
        }

        private static bool IsTargetName(string objectName)
        {
            for (int i = 0; i < TargetNames.Length; i++)
            {
                if (string.Equals(objectName, TargetNames[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
