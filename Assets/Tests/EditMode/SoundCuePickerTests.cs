using Game.Audio;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for SoundCuePicker (variation pick, null slots, jitter, cooldown) and CombatAudioSO.ForHit.</summary>
    public class SoundCuePickerTests
    {
        private const float EPS = 0.0001f;

        [TestCase(0, -1, 0.5f, -1)]
        [TestCase(-2, -1, 0.5f, -1)]
        [TestCase(1, 0, 0.9f, 0)]
        [TestCase(1, -1, 0.0f, 0)]
        public void PickIndex_EmptyOrSingle(int count, int last, float r, int expected)
        {
            Assert.AreEqual(expected, SoundCuePicker.PickIndex(count, last, r));
        }

        [Test]
        public void PickIndex_NeverRepeatsLast()
        {
            for (int last = 0; last < 4; last++)
                for (int step = 0; step <= 20; step++)
                {
                    int index = SoundCuePicker.PickIndex(4, last, step / 20f);
                    Assert.AreNotEqual(last, index);
                    Assert.That(index, Is.InRange(0, 3));
                }
        }

        [Test]
        public void PickIndex_NoLast_CoversWholeRange()
        {
            Assert.AreEqual(0, SoundCuePicker.PickIndex(4, -1, 0f));
            Assert.AreEqual(3, SoundCuePicker.PickIndex(4, -1, 1f));
            Assert.AreEqual(3, SoundCuePicker.PickIndex(4, 7, 1f)); // out-of-range last = no last
        }

        [Test]
        public void PickIndex_TwoClips_Alternates()
        {
            Assert.AreEqual(1, SoundCuePicker.PickIndex(2, 0, 0.3f));
            Assert.AreEqual(0, SoundCuePicker.PickIndex(2, 1, 0.9f));
        }

        [TestCase(1f, 0.1f, 0f, 1f)]
        [TestCase(1f, 0.1f, 1f, 0.9f)]
        [TestCase(0.5f, 0.2f, 0.5f, 0.45f)]
        [TestCase(2f, 0f, 0.5f, 1f)]
        [TestCase(-1f, 0.5f, 0.5f, 0f)]
        public void JitterVolume_ReducesWithinRange(float volume, float jitter, float r, float expected)
        {
            Assert.AreEqual(expected, SoundCuePicker.JitterVolume(volume, jitter, r), EPS);
        }

        [Test]
        public void PickPitch_LerpsAndHandlesInvertedRange()
        {
            Assert.AreEqual(0.95f, SoundCuePicker.PickPitch(0.95f, 1.05f, 0f), EPS);
            Assert.AreEqual(1.05f, SoundCuePicker.PickPitch(0.95f, 1.05f, 1f), EPS);
            Assert.AreEqual(1.0f, SoundCuePicker.PickPitch(1.05f, 0.95f, 0.5f), EPS);
        }

        [TestCase(10f, -1f, 0.5f, false)]
        [TestCase(10f, 9.8f, 0.5f, true)]
        [TestCase(10f, 9.5f, 0.5f, false)]
        [TestCase(10f, 10f, 0f, false)]
        public void IsOnCooldown(float now, float last, float cooldown, bool expected)
        {
            Assert.AreEqual(expected, SoundCuePicker.IsOnCooldown(now, last, cooldown));
        }

        [Test]
        public void FirstNonNull_SkipsEmptySlotsAndWraps()
        {
            var a = ScriptableObject.CreateInstance<SoundCueSO>();
            try
            {
                var items = new SoundCueSO[] { null, a, null };
                Assert.AreEqual(1, SoundCuePicker.FirstNonNull(items, 1));
                Assert.AreEqual(1, SoundCuePicker.FirstNonNull(items, 2)); // wraps
                Assert.AreEqual(-1, SoundCuePicker.FirstNonNull(new SoundCueSO[] { null, null }, 0));
                Assert.AreEqual(-1, SoundCuePicker.FirstNonNull(new SoundCueSO[0], 0));
            }
            finally { Object.DestroyImmediate(a); }
        }

        [Test]
        public void CombatAudio_ForHit_MapsHitResults()
        {
            var audio = ScriptableObject.CreateInstance<CombatAudioSO>();
            audio.hitFlesh = ScriptableObject.CreateInstance<SoundCueSO>();
            audio.block = ScriptableObject.CreateInstance<SoundCueSO>();
            audio.perfectBlock = ScriptableObject.CreateInstance<SoundCueSO>();
            try
            {
                Assert.AreSame(audio.hitFlesh, audio.ForHit(HitResult.NotBlocked));
                Assert.AreSame(audio.block, audio.ForHit(HitResult.Blocked));
                Assert.AreSame(audio.perfectBlock, audio.ForHit(HitResult.PerfectBlock));
                Assert.IsNull(audio.ForHit(HitResult.Dodged));
            }
            finally
            {
                Object.DestroyImmediate(audio.hitFlesh);
                Object.DestroyImmediate(audio.block);
                Object.DestroyImmediate(audio.perfectBlock);
                Object.DestroyImmediate(audio);
            }
        }
    }
}
