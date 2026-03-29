using UnityEngine;

public class TimeControlPipeTrigger : MonoBehaviour
{
    [SerializeField] private TimeOfDayController timeController;
    [SerializeField] private KeyCode modifierKey = KeyCode.LeftShift;

    [SerializeField] private float timeStep = 0.25f;
    [SerializeField] private float durationStepSeconds = 5f;
    [SerializeField] private float hourStep = 0.25f;
    [SerializeField] private float intensityStep = 0.05f;
    [SerializeField] private float yawStep = 5f;

    [SerializeField] private string title = "Time Control Pipe";

    private bool playerInside;
    private readonly Rect panelRect = new Rect(20f, 20f, 560f, 270f);

    private void Awake()
    {
        if (timeController == null)
        {
            timeController = FindObjectOfType<TimeOfDayController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BeanController>() == null)
        {
            return;
        }

        playerInside = true;
        if (timeController == null)
        {
            timeController = FindObjectOfType<TimeOfDayController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BeanController>() == null)
        {
            return;
        }

        playerInside = false;
    }

    private void Update()
    {
        if (!playerInside)
        {
            return;
        }

        if (timeController == null)
        {
            return;
        }

        float multiplier = Input.GetKey(modifierKey) ? 4f : 1f;

        if (Input.GetKeyDown(KeyCode.Alpha1)) timeController.SetTimeOfDay(timeController.TimeOfDay - (timeStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha2)) timeController.SetTimeOfDay(timeController.TimeOfDay + (timeStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha3)) timeController.SetAutoCycle(!timeController.AutoCycle);
        if (Input.GetKeyDown(KeyCode.Alpha4)) timeController.SetDayDurationSeconds(timeController.DayDurationSeconds - (durationStepSeconds * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha5)) timeController.SetDayDurationSeconds(timeController.DayDurationSeconds + (durationStepSeconds * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha6)) SetSunrise(timeController.SunriseHour - (hourStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha7)) SetSunrise(timeController.SunriseHour + (hourStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha8)) SetSunset(timeController.SunsetHour - (hourStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Alpha9)) SetSunset(timeController.SunsetHour + (hourStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Minus)) timeController.SetNightIntensity(timeController.NightIntensity - (intensityStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Equals)) timeController.SetNightIntensity(timeController.NightIntensity + (intensityStep * multiplier));
        if (Input.GetKeyDown(KeyCode.LeftBracket)) timeController.SetDayIntensity(timeController.DayIntensity - (intensityStep * multiplier));
        if (Input.GetKeyDown(KeyCode.RightBracket)) timeController.SetDayIntensity(timeController.DayIntensity + (intensityStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Semicolon)) timeController.SetSunYaw(timeController.SunYaw - (yawStep * multiplier));
        if (Input.GetKeyDown(KeyCode.Quote)) timeController.SetSunYaw(timeController.SunYaw + (yawStep * multiplier));
    }

    private void OnGUI()
    {
        if (!playerInside)
        {
            return;
        }

        GUI.Box(panelRect, GetStatusText());
    }

    private string GetStatusText()
    {
        if (timeController == null)
        {
            return $"{title}\n\nNo TimeOfDayController found in scene.";
        }

        return
            $"{title}\n" +
            "Hold LeftShift for larger steps\n\n" +
            $"Time: {timeController.TimeOfDay:0.00}h  |  Auto: {(timeController.AutoCycle ? "On" : "Off")}\n" +
            $"Day Duration: {timeController.DayDurationSeconds:0.0}s\n" +
            $"Sunrise: {timeController.SunriseHour:0.00}h  |  Sunset: {timeController.SunsetHour:0.00}h\n" +
            $"Night Intensity: {timeController.NightIntensity:0.00}  |  Day Intensity: {timeController.DayIntensity:0.00}\n" +
            $"Sun Yaw: {timeController.SunYaw:0.0}\n\n" +
            "1/2 time -/+ | 3 toggle auto | 4/5 duration -/+\n" +
            "6/7 sunrise -/+ | 8/9 sunset -/+\n" +
            "-/= night intensity -/+ | [/] day intensity -/+ | ;/' yaw -/+";
    }

    private void SetSunrise(float value)
    {
        if (timeController == null)
        {
            return;
        }

        float clamped = Mathf.Clamp(value, 0f, 23.5f);
        if (clamped >= timeController.SunsetHour)
        {
            clamped = timeController.SunsetHour - 0.25f;
        }

        timeController.SetSunriseHour(clamped);
    }

    private void SetSunset(float value)
    {
        if (timeController == null)
        {
            return;
        }

        float clamped = Mathf.Clamp(value, 0.5f, 24f);
        if (clamped <= timeController.SunriseHour)
        {
            clamped = timeController.SunriseHour + 0.25f;
        }

        timeController.SetSunsetHour(clamped);
    }
}
