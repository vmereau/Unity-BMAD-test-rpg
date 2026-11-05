using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Fades a light and its glow in at night, from the world clock. Region scenes can't reference Core, so it polls
    /// <see cref="GameClock.Active"/>, which also follows sleep skips and loads on the next frame. Stays off without a clock.
    /// </summary>
    public class ClockNightLight : MonoBehaviour
    {
        private const string TAG = "[ClockNightLight]";
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private DayNightConfigSO _config;
        [SerializeField] private Light _light;
        [SerializeField] private Renderer[] _glowRenderers;

        [Tooltip("Turned to face the camera while lit, so the glow reads from any side.")]
        [SerializeField] private Transform _billboard;

        [ColorUsage(true, true)]
        [SerializeField] private Color _glowColor = Color.white;

        private MaterialPropertyBlock _block;
        private Camera _view;
        private float _fullIntensity;
        private float _level = -1f;
        private bool _loggedNoClock;

        /// <summary>0 in full day, 1 at night, following the config's twilight ramps.</summary>
        public static float NightLevel(float hour, float sunrise, float sunset, float twilightHours) =>
            1f - DayNightMath.Daylight(hour, sunrise, sunset, twilightHours);

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_light != null) _fullIntensity = _light.intensity;
            Apply(0f);
            if (_config != null && _light != null && DayNightMath.IsValidDay(_config.sunriseHour, _config.sunsetHour)) return;
            GameLog.Error(TAG, $"{name}: needs a light and a valid day/night config — disabled.");
            enabled = false;
        }

        private void Update()
        {
            GameClock clock = GameClock.Active;
            if (clock == null)
            {
                if (!_loggedNoClock) GameLog.Info(TAG, $"{name}: no GameClock loaded — staying off");
                _loggedNoClock = true;
                Apply(0f);
                return;
            }

            Apply(NightLevel(DayNightMath.HourOfDay(clock.Now), _config.sunriseHour, _config.sunsetHour, _config.twilightHours));
        }

        private void LateUpdate()
        {
            if (_billboard == null || _level <= 0f) return;
            if (_view == null) _view = Camera.main; // the camera lives in Core, so it can load after this
            if (_view != null) _billboard.rotation = _view.transform.rotation;
        }

        private void Apply(float level)
        {
            if (Mathf.Approximately(level, _level)) return;
            _level = level;

            bool lit = level > 0f;
            if (_light != null)
            {
                _light.enabled = lit;
                _light.intensity = _fullIntensity * level;
            }

            if (_glowRenderers == null) return;
            Color faded = _glowColor * level;
            faded.a = _glowColor.a; // the material blends by alpha, so scaling it too would fade the glow twice
            _block.SetColor(BaseColor, faded);
            foreach (Renderer glow in _glowRenderers)
            {
                if (glow == null) continue;
                glow.enabled = lit;
                glow.SetPropertyBlock(_block);
            }
        }
    }
}
