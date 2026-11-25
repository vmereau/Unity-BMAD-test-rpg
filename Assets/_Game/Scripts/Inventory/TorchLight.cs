using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Flame and light of a held torch (spec-torch-offhand-item), on the torch visual's <c>Drawn</c> child. Switched by
    /// <see cref="EquipmentVisuals"/> (<see cref="SetLit"/>: held and not doused); flickers its point light while lit. The
    /// stowed (<c>Sheathed</c>) torch has no TorchLight: it never burns.
    /// </summary>
    public class TorchLight : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private Light _light;
        [Tooltip("Flame / ember / smoke particle systems: played while lit, stopped and cleared when doused.")]
        [SerializeField] private ParticleSystem[] _flames;

        [Header("Flicker")]
        [Tooltip("Intensity swing around the light's authored intensity (0.15 = ±15 %).")]
        [Range(0f, 0.5f)] [SerializeField] private float _flickerAmount = 0.15f;
        [Tooltip("Noise speed (cycles per second, roughly).")]
        [Min(0f)] [SerializeField] private float _flickerSpeed = 6f;
        [Tooltip("Light position jitter (m) — moves the shadows a little, like a living flame.")]
        [Min(0f)] [SerializeField] private float _jitter = 0.01f;

        [Header("Shadows")]
        [Tooltip("Soft shadows from the torch light. Off = no shadow cost (6 cube faces per frame).")]
        [SerializeField] private bool _castShadows = true;

        private bool _initialized;
        private bool _lit;
        private float _baseIntensity;
        private Vector3 _basePosition;
        private float _seed;

        /// <summary>True while the flame burns.</summary>
        public bool IsLit => _lit;

        private void Awake() => Init();

        // Re-enabled by a parent (visual hidden / shown, Drawn child toggled): re-apply the state, or a play-on-awake flame
        // would burn while doused, and a lit torch's particles could stay stopped.
        private void OnEnable()
        {
            if (_initialized) SetLit(_lit);
        }

        // Lazy: EquipmentVisuals may call SetLit before Awake when the Drawn child starts inactive.
        private void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _seed = Random.value * 100f;
            if (_light == null)
            {
                GameLog.Warn(TAG, $"TorchLight ({name}): no Light assigned — flame only");
                return;
            }
            _baseIntensity = _light.intensity;
            _basePosition = _light.transform.localPosition;
            _light.shadows = _castShadows ? LightShadows.Soft : LightShadows.None; // resolution: URP additional-light atlas
            _light.renderMode = LightRenderMode.ForcePixel;
            _light.lightShadowCasterMode = LightShadowCasterMode.Default;
        }

        /// <summary>Shadows on or off (NPC torches: off). Applied now if the light is already set up, else at its setup.</summary>
        public void SetCastShadows(bool cast)
        {
            _castShadows = cast;
            if (_initialized && _light != null) _light.shadows = cast ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>Lights or douses the torch. Idempotent.</summary>
        public void SetLit(bool lit)
        {
            Init();
            _lit = lit;
            if (_light != null)
            {
                _light.enabled = lit;
                if (!lit)
                {
                    _light.intensity = _baseIntensity; // drop the last flicker sample
                    _light.transform.localPosition = _basePosition;
                }
            }
            if (_flames == null) return;
            foreach (var flame in _flames)
            {
                if (flame == null) continue;
                if (lit)
                {
                    if (!flame.isPlaying) flame.Play(true);
                }
                else
                {
                    flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void Update()
        {
            if (!_lit || _light == null) return;
            float t = Time.time * _flickerSpeed;
            _light.intensity = _baseIntensity * Flicker(Mathf.PerlinNoise(_seed, t), _flickerAmount);
            if (_jitter > 0f)
            {
                var offset = new Vector3(Mathf.PerlinNoise(_seed + 10f, t) - 0.5f, Mathf.PerlinNoise(_seed + 20f, t) - 0.5f,
                    Mathf.PerlinNoise(_seed + 30f, t) - 0.5f);
                _light.transform.localPosition = _basePosition + offset * (2f * _jitter);
            }
        }

        /// <summary>Pure (tested): intensity multiplier for a noise sample in [0, 1] — 1 ± <paramref name="amount"/>.</summary>
        public static float Flicker(float noise, float amount) => 1f + (Mathf.Clamp01(noise) - 0.5f) * 2f * amount;
    }
}
