using Game.Audio;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for SfxVolumeSettings clamping and AudioMixerVolumes.LinearToDecibels.</summary>
    public class SfxVolumeSettingsTests
    {
        private const float EPS = 0.001f;

        [TestCase(-3f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(7f, 1f)]
        public void Clamp01Volume_ClampsIntoUnitRange(float input, float expected)
        {
            Assert.AreEqual(expected, SfxVolumeSettings.Clamp01Volume(input), EPS);
        }

        [Test]
        public void Clamp01Volume_NaN_FallsBackToDefault()
        {
            Assert.AreEqual(SfxVolumeSettings.DEFAULT_VOLUME, SfxVolumeSettings.Clamp01Volume(float.NaN), EPS);
        }

        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0f, -80f)]
        [TestCase(-1f, -80f)]
        [TestCase(0.00001f, -80f)]
        [TestCase(2f, 0f)]
        public void LinearToDecibels(float linear, float expected)
        {
            Assert.AreEqual(expected, AudioMixerVolumes.LinearToDecibels(linear), EPS);
        }

        [Test]
        public void LinearToDecibels_NaN_IsFloor()
        {
            Assert.AreEqual(AudioMixerVolumes.MIN_DECIBELS, AudioMixerVolumes.LinearToDecibels(float.NaN), EPS);
        }
    }
}
