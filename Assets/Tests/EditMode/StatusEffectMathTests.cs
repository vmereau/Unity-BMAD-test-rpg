using System.Collections.Generic;
using Game.StatusEffects;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure StatusEffectMath payout / stacking / display math used by StatusEffectHost.
    /// </summary>
    public class StatusEffectMathTests
    {
        private const double MIN = 60d;
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private StatusEffectSO CreateEffect(StatusEffectStacking stacking = StatusEffectStacking.Stack, int maxStacks = 0, float tick = 60f)
        {
            var effect = ScriptableObject.CreateInstance<StatusEffectSO>();
            effect.stacking = stacking;
            effect.maxStacks = maxStacks;
            effect.tickIntervalSeconds = tick;
            _created.Add(effect);
            return effect;
        }

        [Test]
        public void PaidBy_IsQuantizedToWholeTicks()
        {
            // 8 HP over 20 min, 1 min ticks → 0.4 HP per tick.
            Assert.AreEqual(0f, StatusEffectMath.PaidBy(0, 20 * MIN, 8f, MIN, 59), 1e-5);
            Assert.AreEqual(0.4f, StatusEffectMath.PaidBy(0, 20 * MIN, 8f, MIN, 60), 1e-5);
            Assert.AreEqual(0.4f, StatusEffectMath.PaidBy(0, 20 * MIN, 8f, MIN, 119), 1e-5);
            Assert.AreEqual(4f, StatusEffectMath.PaidBy(0, 20 * MIN, 8f, MIN, 10 * MIN), 1e-5);
        }

        [Test]
        public void PaidBy_IsExactTotalAtAndAfterExpiry()
        {
            // 7 min with 2 min ticks: the last partial tick still lands on expiry.
            Assert.AreEqual(5f, StatusEffectMath.PaidBy(0, 7 * MIN, 5f, 2 * MIN, 7 * MIN));
            Assert.AreEqual(5f, StatusEffectMath.PaidBy(0, 7 * MIN, 5f, 2 * MIN, 1000 * MIN));
            Assert.Less(StatusEffectMath.PaidBy(0, 7 * MIN, 5f, 2 * MIN, 7 * MIN - 1), 5f);
        }

        [Test]
        public void PaidBy_BeforeApply_IsZero()
        {
            Assert.AreEqual(0f, StatusEffectMath.PaidBy(100, 200, 5f, 10, 50));
        }

        [Test]
        public void DueNow_SkipPastExpiry_PaysAllRemainingOnce()
        {
            var instance = new StatusEffectInstance(CreateEffect(), 0, 20 * MIN, 8f);
            instance.PaidAmount += StatusEffectMath.DueNow(instance, 5 * MIN);
            Assert.AreEqual(2f, instance.PaidAmount, 1e-5);

            float due = StatusEffectMath.DueNow(instance, 8 * 60 * MIN); // slept 8 h
            Assert.AreEqual(6f, due, 1e-5);
            instance.PaidAmount += due;
            Assert.IsTrue(StatusEffectMath.IsExpired(instance, 8 * 60 * MIN));
            Assert.AreEqual(0f, StatusEffectMath.DueNow(instance, 9 * 60 * MIN));
        }

        [Test]
        public void DueNow_ClockBehindPaid_IsZero()
        {
            var instance = new StatusEffectInstance(CreateEffect(), 0, 20 * MIN, 8f, 4f);
            Assert.AreEqual(0f, StatusEffectMath.DueNow(instance, MIN));
        }

        [Test]
        public void AddInstance_Stack_KeepsBothAndPendingAddsUp()
        {
            var effect = CreateEffect();
            var active = new List<StatusEffectInstance>();
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(effect, 0, 20 * MIN, 8f));
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(effect, 10, 30 * MIN, 10f, 1f));

            Assert.AreEqual(2, StatusEffectMath.CountOf(active, effect));
            Assert.AreEqual(17f, StatusEffectMath.PendingOf(active, StatusEffectKind.Heal), 1e-5);
        }

        [Test]
        public void AddInstance_StackCap_DropsOldest()
        {
            var effect = CreateEffect(maxStacks: 2);
            var active = new List<StatusEffectInstance>();
            var first = new StatusEffectInstance(effect, 0, 100, 1f);
            StatusEffectMath.AddInstance(active, first);
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(effect, 10, 100, 1f));
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(effect, 20, 100, 1f));

            Assert.AreEqual(2, active.Count);
            CollectionAssert.DoesNotContain(active, first);
        }

        [Test]
        public void AddInstance_Refresh_ReplacesSameEffectOnly()
        {
            var refresh = CreateEffect(StatusEffectStacking.Refresh);
            var other = CreateEffect();
            var active = new List<StatusEffectInstance>();
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(other, 0, 100, 1f));
            StatusEffectMath.AddInstance(active, new StatusEffectInstance(refresh, 0, 100, 1f));
            var latest = new StatusEffectInstance(refresh, 50, 150, 1f);
            StatusEffectMath.AddInstance(active, latest);

            Assert.AreEqual(1, StatusEffectMath.CountOf(active, refresh));
            Assert.AreEqual(1, StatusEffectMath.CountOf(active, other));
            CollectionAssert.Contains(active, latest);
        }

        [TestCase(0d, "0m")]
        [TestCase(1d, "1m")]
        [TestCase(20 * MIN, "20m")]
        [TestCase(59 * MIN + 1, "1h00")]
        [TestCase(65 * MIN, "1h05")]
        public void FormatRemaining_RoundsUpToMinute(double seconds, string expected)
        {
            Assert.AreEqual(expected, StatusEffectMath.FormatRemaining(seconds));
        }

        [TestCase(0.2f, "1 min")]
        [TestCase(20f, "20 min")]
        [TestCase(60f, "1 h")]
        [TestCase(90f, "1 h 30 min")]
        public void FormatDuration_ReadsMinutesAndHours(float minutes, string expected)
        {
            Assert.AreEqual(expected, StatusEffectMath.FormatDuration(minutes));
        }
    }
}
