using System;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Sound-effects volume setting (0–1), stored in <see cref="PlayerPrefs"/> as a device setting (never in save
    /// files). Same contract as <see cref="MusicVolumeSettings"/>: <see cref="AudioMixerVolumes"/> applies it to the
    /// mixer, <c>OptionsUI</c> writes it through <see cref="Set"/>.
    /// </summary>
    public static class SfxVolumeSettings
    {
        public const string PREF_KEY = "settings.sfxVolume";
        public const float DEFAULT_VOLUME = 0.8f;

        /// <summary>Raised by <see cref="Set"/> with the clamped volume. Subscribers must unsubscribe in OnDisable.</summary>
        public static event Action<float> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
        }

        public static float Get()
        {
            return Clamp01Volume(PlayerPrefs.GetFloat(PREF_KEY, DEFAULT_VOLUME));
        }

        /// <summary>Stores and broadcasts the volume. No disk flush (slider drags) — call <see cref="Save"/> when the edit ends.</summary>
        public static void Set(float volume)
        {
            float clamped = Clamp01Volume(volume);
            PlayerPrefs.SetFloat(PREF_KEY, clamped);
            Changed?.Invoke(clamped);
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Slider label, e.g. 0.8 -> "80 %".</summary>
        public static string FormatPercent(float volume)
        {
            return $"{Mathf.RoundToInt(Clamp01Volume(volume) * 100f)} %";
        }

        /// <summary>Clamps into [0, 1]; NaN or infinity fall back to <see cref="DEFAULT_VOLUME"/>.</summary>
        public static float Clamp01Volume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return DEFAULT_VOLUME;
            return Mathf.Clamp01(volume);
        }
    }
}
