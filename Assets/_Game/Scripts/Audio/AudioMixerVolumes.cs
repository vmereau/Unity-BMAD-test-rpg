using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// Applies the music and SFX volume settings to the main mixer's exposed parameters (GO <c>AudioManager</c> in
    /// Core.unity). Applied in Start (AudioMixer.SetFloat is ignored in Awake) and on every settings change.
    /// </summary>
    public class AudioMixerVolumes : MonoBehaviour
    {
        private const string TAG = "[AudioMixerVolumes]";
        public const string MUSIC_PARAM = "MusicVolume";
        public const string SFX_PARAM = "SfxVolume";
        public const float MIN_DECIBELS = -80f;

        [SerializeField] private AudioMixer _mixer;

        private void OnEnable()
        {
            MusicVolumeSettings.Changed += HandleMusicChanged;
            SfxVolumeSettings.Changed += HandleSfxChanged;
        }

        private void OnDisable()
        {
            MusicVolumeSettings.Changed -= HandleMusicChanged;
            SfxVolumeSettings.Changed -= HandleSfxChanged;
        }

        private void Start()
        {
            if (_mixer == null)
            {
                GameLog.Warn(TAG, "No AudioMixer assigned — volume settings are not applied");
                enabled = false;
                return;
            }
            HandleMusicChanged(MusicVolumeSettings.Get());
            HandleSfxChanged(SfxVolumeSettings.Get());
        }

        private void HandleMusicChanged(float volume) => Apply(MUSIC_PARAM, volume);

        private void HandleSfxChanged(float volume) => Apply(SFX_PARAM, volume);

        private void Apply(string parameter, float volume)
        {
            if (_mixer == null) return;
            if (!_mixer.SetFloat(parameter, LinearToDecibels(volume)))
                GameLog.Warn(TAG, $"Mixer has no exposed parameter '{parameter}'");
        }

        /// <summary>Linear 0–1 → dB (20·log10), floored at <see cref="MIN_DECIBELS"/>. NaN / ≤ 0 → floor.</summary>
        public static float LinearToDecibels(float linear)
        {
            if (float.IsNaN(linear) || linear <= 0f) return MIN_DECIBELS;
            return Mathf.Max(MIN_DECIBELS, 20f * Mathf.Log10(Mathf.Min(1f, linear)));
        }
    }
}
