using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Plays the global exploration track (GO <c>AudioManager</c> in Core.unity, always loaded, so music survives
    /// region loads, save loads and death). 2D, looping, fades in with unscaled time so it also runs while paused.
    /// Effective volume = fade factor × <see cref="MusicVolumeSettings"/>; setting changes apply live.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        private const string TAG = "[MusicPlayer]";
        // Caps one frame's fade step so a long first frame (boot, region load) can't skip the fade-in.
        private const float MAX_FADE_STEP_SECONDS = 0.1f;

        [SerializeField] private AudioClip _explorationTrack;
        [SerializeField, Min(0f)] private float _fadeInSeconds = 3f;

        private AudioSource _source;
        private float _settingVolume;
        private float _fade;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
            _source.priority = 0; // never virtualised by SFX voices
            _source.volume = 0f;
            _settingVolume = MusicVolumeSettings.Get();
        }

        private void OnEnable()
        {
            MusicVolumeSettings.Changed += HandleVolumeChanged;
        }

        private void OnDisable()
        {
            MusicVolumeSettings.Changed -= HandleVolumeChanged;
        }

        private void Start()
        {
            if (_explorationTrack == null)
            {
                GameLog.Warn(TAG, "No exploration track assigned — music disabled");
                enabled = false;
                return;
            }

            _source.clip = _explorationTrack;
            _fade = _fadeInSeconds > 0f ? 0f : 1f;
            ApplyVolume();
            _source.Play();
        }

        private void Update()
        {
            if (_fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Mathf.Min(Time.unscaledDeltaTime, MAX_FADE_STEP_SECONDS) / _fadeInSeconds);
            ApplyVolume();
        }

        private void HandleVolumeChanged(float volume)
        {
            _settingVolume = volume;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (_source == null) return;
            _source.volume = _fade * _settingVolume;
        }
    }
}
