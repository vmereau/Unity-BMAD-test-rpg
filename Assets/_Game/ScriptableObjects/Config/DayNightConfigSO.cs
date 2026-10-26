using UnityEngine;

namespace Game.Core
{
    /// <summary>Tunables of the visual day / night cycle (<see cref="DayNightController"/>). Hours are game hours.</summary>
    [CreateAssetMenu(fileName = "DayNightConfig", menuName = "Game/Config/Day Night Config")]
    public class DayNightConfigSO : ScriptableObject
    {
        [Header("Day")]
        [Tooltip("Hour the sun rises (with sunset 22: day = 16 / 24 of TimeConfig.realMinutesPerGameDay, night = 8 / 24).")]
        [Range(0f, 23.99f)] public float sunriseHour = 6f;
        [Tooltip("Hour the sun sets. Must be after sunrise.")]
        [Range(0f, 23.99f)] public float sunsetHour = 22f;
        [Tooltip("Game hours over which light fades in after sunrise and out before sunset.")]
        [Min(0f)] public float twilightHours = 1f;

        [Header("Sun")]
        [Tooltip("Heading (degrees, 0 = +Z north, 90 = east) where the sun rises; it sets 180° later.")]
        public float sunriseAzimuth = 90f;
        [Tooltip("Sun elevation (degrees) at mid-day.")]
        [Range(0f, 90f)] public float sunMaxElevation = 60f;
        [Tooltip("Lowest pitch (degrees) a lit sun / moon shines from — avoids horizontal light and endless shadows near the horizon.")]
        [Range(0f, 30f)] public float minLightElevation = 5f;
        [Min(0f)] public float sunIntensity = 1.5f;
        [Tooltip("Sun color over the day (0 = sunrise, 1 = sunset).")]
        public Gradient sunColor = new Gradient();

        [Header("Moon")]
        [Tooltip("Moon light intensity in full night (0 = no moon).")]
        [Min(0f)] public float moonIntensity = 0.15f;
        public Color moonColor = new Color(0.55f, 0.65f, 1f);
        [Tooltip("Moon elevation (degrees) at mid-night.")]
        [Range(0f, 90f)] public float moonMaxElevation = 50f;

        [Header("Ambient (trilight)")]
        public Color daySkyAmbient = new Color(0.4f, 0.4f, 0.45f);
        public Color dayEquatorAmbient = new Color(0.3f, 0.3f, 0.3f);
        public Color dayGroundAmbient = new Color(0.12f, 0.11f, 0.1f);
        public Color nightSkyAmbient = new Color(0.06f, 0.07f, 0.12f);
        public Color nightEquatorAmbient = new Color(0.04f, 0.045f, 0.07f);
        public Color nightGroundAmbient = new Color(0.02f, 0.02f, 0.025f);

        [Header("Sky")]
        [Tooltip("Skybox _Exposure in full day.")]
        [Min(0f)] public float daySkyExposure = 1.3f;
        [Tooltip("Skybox _Exposure in full night.")]
        [Min(0f)] public float nightSkyExposure = 0.1f;
        [Tooltip("Environment reflection intensity in full day / full night (the reflection cubemap isn't re-rendered).")]
        [Range(0f, 1f)] public float dayReflectionIntensity = 1f;
        [Range(0f, 1f)] public float nightReflectionIntensity = 0.15f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!DayNightMath.IsValidDay(sunriseHour, sunsetHour))
                GameLog.Warn("[DayNight]", $"{name}: sunrise ({sunriseHour}) must be before sunset ({sunsetHour}) — the cycle stops");
        }
#endif
    }
}
