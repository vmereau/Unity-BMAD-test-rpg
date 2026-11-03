using Game.Audio;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure helpers of MusicVolumeSettings (clamping and the Options slider label).
    /// </summary>
    public class MusicVolumeSettingsTests
    {
        private const float EPS = 0.0001f;

        [TestCase(-3f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(7f, 1f)]
        public void Clamp01Volume_ClampsIntoUnitRange(float input, float expected)
        {
            Assert.AreEqual(expected, MusicVolumeSettings.Clamp01Volume(input), EPS);
        }

        [Test]
        public void Clamp01Volume_NaNOrInfinity_FallsBackToDefault()
        {
            Assert.AreEqual(MusicVolumeSettings.DEFAULT_VOLUME, MusicVolumeSettings.Clamp01Volume(float.NaN), EPS);
            Assert.AreEqual(MusicVolumeSettings.DEFAULT_VOLUME,
                MusicVolumeSettings.Clamp01Volume(float.PositiveInfinity), EPS);
        }

        [Test]
        public void SetThenGet_RoundTripsClampedValue_AndRaisesChanged()
        {
            bool hadKey = UnityEngine.PlayerPrefs.HasKey(MusicVolumeSettings.PREF_KEY);
            float previous = UnityEngine.PlayerPrefs.GetFloat(MusicVolumeSettings.PREF_KEY);
            float raised = -1f;
            void OnChanged(float v) => raised = v;
            MusicVolumeSettings.Changed += OnChanged;
            try
            {
                UnityEngine.PlayerPrefs.DeleteKey(MusicVolumeSettings.PREF_KEY);
                Assert.AreEqual(MusicVolumeSettings.DEFAULT_VOLUME, MusicVolumeSettings.Get(), EPS);

                MusicVolumeSettings.Set(4f);
                Assert.AreEqual(1f, raised, EPS);
                Assert.AreEqual(1f, MusicVolumeSettings.Get(), EPS);

                MusicVolumeSettings.Set(0.25f);
                Assert.AreEqual(0.25f, MusicVolumeSettings.Get(), EPS);
            }
            finally
            {
                MusicVolumeSettings.Changed -= OnChanged;
                if (hadKey) UnityEngine.PlayerPrefs.SetFloat(MusicVolumeSettings.PREF_KEY, previous);
                else UnityEngine.PlayerPrefs.DeleteKey(MusicVolumeSettings.PREF_KEY);
            }
        }

        [TestCase(0.7f, "70 %")]
        [TestCase(0f, "0 %")]
        [TestCase(1f, "100 %")]
        [TestCase(0.333f, "33 %")]
        [TestCase(5f, "100 %")]
        public void FormatPercent_RoundsToWholePercent(float input, string expected)
        {
            Assert.AreEqual(expected, MusicVolumeSettings.FormatPercent(input));
        }
    }
}
