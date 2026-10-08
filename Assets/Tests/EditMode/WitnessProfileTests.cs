using Game.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="WitnessProfileSO"/> (watch range resolution, enabled check).</summary>
    public class WitnessProfileTests
    {
        [Test] public void ResolveWatchRange_AboveRange_IsKept() => Assert.That(WitnessProfileSO.ResolveWatchRange(6f, 9f), Is.EqualTo(9f));
        [Test] public void ResolveWatchRange_BelowRange_ClampedToRange() => Assert.That(WitnessProfileSO.ResolveWatchRange(6f, 4f), Is.EqualTo(6f));
        [Test] public void ResolveWatchRange_NegativeRange_TreatedAsZero() => Assert.That(WitnessProfileSO.ResolveWatchRange(-1f, -2f), Is.EqualTo(0f));

        [Test] public void IsEnabled_Null_IsFalse() => Assert.That(WitnessProfileSO.IsEnabled(null), Is.False);

        [Test]
        public void IsEnabled_DefaultProfile_IsTrue()
        {
            var profile = ScriptableObject.CreateInstance<WitnessProfileSO>(); // defaults: 6 m / 9 m
            try
            {
                Assert.That(WitnessProfileSO.IsEnabled(profile), Is.True);
                Assert.That(profile.WatchRange, Is.EqualTo(9f));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
