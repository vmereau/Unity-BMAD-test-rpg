using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Visual day / night cycle (GO <c>DayNightController</c> in Core.unity). Every frame it reads <see cref="GameClock.Now"/>
    /// and moves the <see cref="_sun"/> (east → west between sunrise and sunset, fading over the twilight) and the dim
    /// <see cref="_moon"/> (across the night — URP's main light while the sun is off), and blends the trilight ambient,
    /// the skybox exposure and the reflection intensity between day and night. Reading the clock (no events) means sleep
    /// skips and save loads are followed on the next frame; it runs after <see cref="GameClock"/> so it never lags one.
    /// The only writer of <see cref="RenderSettings"/>; region scenes must not add their own sun.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class DayNightController : MonoBehaviour
    {
        private const string TAG = "[DayNight]";
        private static readonly int EXPOSURE_ID = Shader.PropertyToID("_Exposure");

        [SerializeField] private GameClock _gameClock;
        [SerializeField] private DayNightConfigSO _config;
        [SerializeField] private Light _sun;
        [SerializeField] private Light _moon;

        // Skybox source asset → our runtime copy. Kept for the session (never destroyed early): a scene that becomes
        // active again still points at its copy, and the shared asset is never edited.
        private readonly Dictionary<Material, Material> _skyboxCopies = new Dictionary<Material, Material>();
        private Material _skybox;
        private bool _valid;
        private bool _warnedInvalidDay;

        private void Awake()
        {
            if (_gameClock == null || _config == null || _sun == null || _moon == null)
            {
                GameLog.Warn(TAG, "GameClock, config, sun or moon not assigned — day / night cycle disabled");
                enabled = false;
                return;
            }
            _valid = true;
            ApplyRenderSettings();
        }

        private void OnEnable()
        {
            if (!_valid) return;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            ApplyRenderSettings();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void OnDestroy()
        {
            foreach (Material copy in _skyboxCopies.Values)
                if (copy != null) Destroy(copy);
            _skyboxCopies.Clear();
        }

        // RenderSettings belong to the active scene: a new active scene brings its own — take them over again.
        private void HandleActiveSceneChanged(Scene previous, Scene next) => ApplyRenderSettings();

        private void ApplyRenderSettings()
        {
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;

            Material source = RenderSettings.skybox;
            if (source != null && _skyboxCopies.ContainsValue(source)) { _skybox = source; return; } // already ours
            _skybox = null;
            if (source == null || !source.HasProperty(EXPOSURE_ID))
            {
                GameLog.Warn(TAG, "Skybox missing or without _Exposure — the sky won't darken at night");
                return;
            }
            if (!_skyboxCopies.TryGetValue(source, out _skybox) || _skybox == null)
            {
                _skybox = new Material(source);
                _skyboxCopies[source] = _skybox;
            }
            RenderSettings.skybox = _skybox;
        }

        private void Update()
        {
            if (!_valid) return; // re-enabled after Awake rejected the setup
            float sunrise = _config.sunriseHour;
            float sunset = _config.sunsetHour;
            if (!DayNightMath.IsValidDay(sunrise, sunset))
            {
                if (!_warnedInvalidDay) GameLog.Warn(TAG, $"Sunrise {sunrise} must be before sunset {sunset} — lighting frozen");
                _warnedInvalidDay = true;
                return;
            }
            _warnedInvalidDay = false;

            float hour = DayNightMath.HourOfDay(_gameClock.Now);
            float daylight = DayNightMath.Daylight(hour, sunrise, sunset, _config.twilightHours);

            float dayProgress = DayNightMath.DayProgress(hour, sunrise, sunset);
            PlaceOnArc(_sun.transform, dayProgress, _config.sunMaxElevation);
            _sun.intensity = _config.sunIntensity * daylight;
            _sun.color = _config.sunColor.Evaluate(dayProgress);
            _sun.enabled = daylight > 0f;

            float nightProgress = DayNightMath.NightProgress(hour, sunrise, sunset);
            PlaceOnArc(_moon.transform, nightProgress, _config.moonMaxElevation);
            _moon.intensity = _config.moonIntensity * (1f - daylight);
            _moon.color = _config.moonColor;
            _moon.enabled = daylight < 1f && _config.moonIntensity > 0f;

            RenderSettings.ambientSkyColor = Color.Lerp(_config.nightSkyAmbient, _config.daySkyAmbient, daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(_config.nightEquatorAmbient, _config.dayEquatorAmbient, daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(_config.nightGroundAmbient, _config.dayGroundAmbient, daylight);
            RenderSettings.reflectionIntensity = Mathf.Lerp(_config.nightReflectionIntensity, _config.dayReflectionIntensity, daylight);
            if (_skybox != null)
                _skybox.SetFloat(EXPOSURE_ID, Mathf.Lerp(_config.nightSkyExposure, _config.daySkyExposure, daylight));
        }

        // A directional light shines along its forward: pitch down by the elevation (never below the minimum, so light
        // near the horizon isn't horizontal), facing away from where it rises.
        private void PlaceOnArc(Transform light, float progress, float maxElevation)
        {
            float elevation = Mathf.Max(DayNightMath.ArcElevation(progress, maxElevation), _config.minLightElevation);
            float azimuth = DayNightMath.ArcAzimuth(progress, _config.sunriseAzimuth);
            light.rotation = Quaternion.Euler(elevation, azimuth + 180f, 0f);
        }
    }
}
