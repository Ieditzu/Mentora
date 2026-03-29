using UnityEngine;

[ExecuteAlways]
public class TimeOfDayController : MonoBehaviour
{
    [SerializeField] private Light directionalLight;
    [SerializeField, Range(0f, 24f)] private float timeOfDay = 12f;
    [SerializeField] private bool autoCycle;
    [SerializeField, Min(1f)] private float dayDurationSeconds = 120f;
    [SerializeField, Range(0f, 24f)] private float sunriseHour = 6f;
    [SerializeField, Range(0f, 24f)] private float sunsetHour = 18f;
    [SerializeField] private float nightIntensity = 0.1f;
    [SerializeField] private float dayIntensity = 2f;
    [SerializeField] private float sunYaw = -30f;

    public float TimeOfDay => timeOfDay;
    public bool AutoCycle => autoCycle;
    public float DayDurationSeconds => dayDurationSeconds;
    public float SunriseHour => sunriseHour;
    public float SunsetHour => sunsetHour;
    public float NightIntensity => nightIntensity;
    public float DayIntensity => dayIntensity;
    public float SunYaw => sunYaw;
    public float CurrentLightIntensity => directionalLight != null ? directionalLight.intensity : 0f;
    public Vector3 SunEulerAngles => transform.eulerAngles;

    private void Reset()
    {
        directionalLight = GetComponent<Light>();
        ApplyTime();
    }

    private void Awake()
    {
        if (directionalLight == null)
        {
            directionalLight = GetComponent<Light>();
        }

        ApplyTime();
    }

    private void OnValidate()
    {
        ApplyTime();
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoCycle)
        {
            return;
        }

        float hoursPerSecond = 24f / dayDurationSeconds;
        timeOfDay = Mathf.Repeat(timeOfDay + hoursPerSecond * Time.deltaTime, 24f);
        ApplyTime();
    }

    public void SetTimeOfDay(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        ApplyTime();
    }

    public void SetAutoCycle(bool value)
    {
        autoCycle = value;
    }

    public void SetDayDurationSeconds(float seconds)
    {
        dayDurationSeconds = Mathf.Max(1f, seconds);
    }

    public void SetSunriseHour(float hour)
    {
        sunriseHour = Mathf.Clamp(hour, 0f, 24f);
        ApplyTime();
    }

    public void SetSunsetHour(float hour)
    {
        sunsetHour = Mathf.Clamp(hour, 0f, 24f);
        ApplyTime();
    }

    public void SetNightIntensity(float intensity)
    {
        nightIntensity = Mathf.Max(0f, intensity);
        ApplyTime();
    }

    public void SetDayIntensity(float intensity)
    {
        dayIntensity = Mathf.Max(0f, intensity);
        ApplyTime();
    }

    public void SetSunYaw(float yaw)
    {
        sunYaw = yaw;
        ApplyTime();
    }

    public void Refresh()
    {
        ApplyTime();
    }

    private void ApplyTime()
    {
        if (directionalLight == null)
        {
            return;
        }

        float sunAngle = (timeOfDay / 24f) * 360f - 90f;
        transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);

        float daylight = EvaluateDaylight(timeOfDay);
        directionalLight.intensity = Mathf.Lerp(nightIntensity, dayIntensity, daylight);
    }

    private float EvaluateDaylight(float hour)
    {
        float sunrise = Mathf.Clamp(sunriseHour, 0f, 24f);
        float sunset = Mathf.Clamp(sunsetHour, 0f, 24f);
        if (sunset <= sunrise || hour <= sunrise || hour >= sunset)
        {
            return 0f;
        }

        float t = (hour - sunrise) / (sunset - sunrise);
        return Mathf.Sin(t * Mathf.PI);
    }
}
