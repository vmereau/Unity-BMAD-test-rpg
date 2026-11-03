using System;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Music volume setting (0–1), stored in <see cref="PlayerPrefs"/> as a device setting (never in save files).
    /// Single owner of the value: <see cref="AudioMixerVolumes"/> applies it to the mixer's Music group,
    /// <c>OptionsUI</c> writes it through <see cref="Set"/>.
    /// </summary>
    public static class MusicVolumeSettings
    {
        public const string PREF_KEY = "settings.musicVolume";
        public const float DEFAULT_VOLUME = 0.7f;

        /// <summary>Raised by <see cref="Set"/> with the clamped volume. Subscribers must unsubscribe in OnDisable.</summary>
        public static event Action<float> Changed;

        // Domain reload off: static events survive play sessions — drop stale subscribers on every play start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
        }

        public static float Get()
        {
            return Clamp01Volume(PlayerPrefs.GetFloat(PREF_KEY, DEFAULT_VOLUME));
        }

        /// <summary>
        /// Stores and broadcasts the volume. Does not flush to disk (a slider drag calls this every frame) — call
        /// <see cref="Save"/> when the edit ends (Unity also flushes PlayerPrefs on quit).
        /// </summary>
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

        /// <summary>Clamps into [0, 1]; NaN or infinity fall back to <see cref="DEFAULT_VOLUME"/>.</summary>
        public static float Clamp01Volume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume)) return DEFAULT_VOLUME;
            return Mathf.Clamp01(volume);
        }

        /// <summary>Slider label, e.g. 0.7 → "70 %".</summary>
        public static string FormatPercent(float volume)
        {
            return $"{Mathf.RoundToInt(Clamp01Volume(volume) * 100f)} %";
        }
    }
}
