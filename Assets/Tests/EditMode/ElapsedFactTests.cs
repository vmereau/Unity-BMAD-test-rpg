using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="ElapsedFactRules"/> (pure delay rule) and the <see cref="WorldStateManager"/> "became true" times that
    /// feed <see cref="ElapsedFact"/>. The clock is an inactive GameClock (no Awake) forced into <c>GameClock.Active</c>.
    /// </summary>
    public class ElapsedFactTests
    {
        private const double HOUR = GameTime.SECONDS_PER_HOUR;

        private static readonly FieldInfo s_wsmInstance =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_clockActive =
            typeof(GameClock).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_factChanged =
            typeof(WorldStateManager).GetField("_onFactChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_raiseDue =
            typeof(WorldStateManager).GetMethod("RaiseDueElapsedFacts", BindingFlags.Instance | BindingFlags.NonPublic);

        private WorldStateManager _wsm;
        private GameClock _clock;
        private WorldFact _source;
        private ElapsedFact _elapsed;
        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<string> _raisedKeys = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(s_wsmInstance, "WorldStateManager.Instance backing field not found");
            Assert.IsNotNull(s_clockActive, "GameClock.Active backing field not found");
            Assert.IsNotNull(s_factChanged, "WorldStateManager._onFactChanged not found");
            Assert.IsNotNull(s_raiseDue, "WorldStateManager.RaiseDueElapsedFacts not found");

            var wsmGo = new GameObject("WSM_ElapsedTest");
            _wsm = wsmGo.AddComponent<WorldStateManager>();
            s_wsmInstance.SetValue(null, _wsm);

            var clockGo = new GameObject("Clock_ElapsedTest");
            clockGo.SetActive(false); // no Awake: it would need a TimeConfigSO
            _clock = clockGo.AddComponent<GameClock>();
            s_clockActive.SetValue(null, _clock);
            _clock.Restore(10 * HOUR);

            _source = ScriptableObject.CreateInstance<WorldFact>().Init("ElapsedTest_Source");
            _elapsed = ScriptableObject.CreateInstance<ElapsedFact>().Init(_source, 2f);
            var factChanged = ScriptableObject.CreateInstance<GameEventSO_Fact>();
            factChanged.AddListener(d => _raisedKeys.Add(d.key));
            s_factChanged.SetValue(_wsm, factChanged);
            _raisedKeys.Clear();
            _cleanup.AddRange(new Object[] { wsmGo, clockGo, _source, _elapsed, factChanged });
        }

        private void Tick() => s_raiseDue.Invoke(_wsm, null);
        private int ElapsedEvents() => _raisedKeys.FindAll(k => k == _elapsed.ToString()).Count;

        [TearDown]
        public void TearDown()
        {
            s_wsmInstance.SetValue(null, null);
            s_clockActive.SetValue(null, null);
            foreach (var obj in _cleanup) if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        // ── Pure rule ──────────────────────────────────────────────────────────

        [Test] public void Rule_SourceFalse_IsFalse() => Assert.IsFalse(ElapsedFactRules.IsElapsed(false, 0d, 100 * HOUR, 2d));
        [Test] public void Rule_BeforeDelay_IsFalse() => Assert.IsFalse(ElapsedFactRules.IsElapsed(true, 10 * HOUR, 11.5 * HOUR, 2d));
        [Test] public void Rule_AtDelay_IsTrue() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, 10 * HOUR, 12 * HOUR, 2d));
        [Test] public void Rule_DecimalHours() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, 0d, 0.5 * HOUR, 0.5d));
        [Test] public void Rule_NoTimestamp_IsTrue() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, null, 10 * HOUR, 2d));
        [Test] public void Rule_NoClock_IsTrue() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, 10 * HOUR, null, 2d));
        [Test] public void Rule_TimeAfterNow_IsTrue() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, 50 * HOUR, 10 * HOUR, 2d));
        [Test] public void Rule_ZeroHours_IsSourceOnly() => Assert.IsTrue(ElapsedFactRules.IsElapsed(true, 10 * HOUR, 10 * HOUR, 0d));

        // ── WorldStateManager timing ─────────────────────────────────────────

        [Test]
        public void SetTrue_RecordsClockTime()
        {
            _wsm.SetFact(_source, true);
            Assert.IsTrue(_wsm.TryGetFactSetTime(_source, out double t));
            Assert.AreEqual(10 * HOUR, t);
        }

        [Test]
        public void Elapsed_FollowsTheClock()
        {
            _wsm.SetFact(_source, true);
            _clock.Restore(11.5 * HOUR);
            Assert.IsFalse(_wsm.GetFact(_elapsed));
            _clock.Restore(12 * HOUR);
            Assert.IsTrue(_wsm.GetFact(_elapsed));
        }

        [Test]
        public void ReSetTrue_KeepsFirstTime()
        {
            _wsm.SetFact(_source, true);
            _clock.Restore(11 * HOUR);
            _wsm.SetFact(_source, true);
            _wsm.TryGetFactSetTime(_source, out double t);
            Assert.AreEqual(10 * HOUR, t);
        }

        [Test]
        public void SetFalse_ClearsTime_AndNextTrueRestarts()
        {
            _wsm.SetFact(_source, true);
            _wsm.SetFact(_source, false);
            Assert.IsFalse(_wsm.TryGetFactSetTime(_source, out _));
            Assert.IsFalse(_wsm.GetFact(_elapsed));

            _clock.Restore(20 * HOUR);
            _wsm.SetFact(_source, true);
            _wsm.TryGetFactSetTime(_source, out double t);
            Assert.AreEqual(20 * HOUR, t);
        }

        [Test]
        public void NoClockWhenSet_CountsAsElapsed()
        {
            s_clockActive.SetValue(null, null);
            _wsm.SetFact(_source, true);
            Assert.IsFalse(_wsm.TryGetFactSetTime(_source, out _));
            s_clockActive.SetValue(null, _clock);
            Assert.IsTrue(_wsm.GetFact(_elapsed));
        }

        [Test]
        public void CaptureRestore_RoundTrips()
        {
            _wsm.SetFact(_source, true);
            var times = _wsm.CaptureFactSetTimes();
            _wsm.RestoreFactSetTimes(null);
            Assert.IsFalse(_wsm.TryGetFactSetTime(_source, out _));
            _wsm.RestoreFactSetTimes(times);
            _clock.Restore(11 * HOUR);
            Assert.IsFalse(_wsm.GetFact(_elapsed));
        }

        [Test]
        public void OlderSave_NoTimes_TrueFactIsElapsed()
        {
            _wsm.RestoreFacts(new Dictionary<string, bool> { { _source.ToString(), true } });
            _wsm.RestoreFactSetTimes(null);
            Assert.IsTrue(_wsm.GetFact(_elapsed));
        }

        // ── Delay-end event ──────────────────────────────────────────────────

        [Test]
        public void PendingDelay_RaisesOnce_AtDeadline()
        {
            _wsm.SetFact(_source, true);
            Assert.IsFalse(_wsm.GetFact(_elapsed)); // registers the deadline (12:00)
            _clock.Restore(11.5 * HOUR);
            Tick();
            Assert.AreEqual(0, ElapsedEvents());
            _clock.Restore(12 * HOUR);
            Tick();
            Assert.AreEqual(1, ElapsedEvents());
            Tick();
            Assert.AreEqual(1, ElapsedEvents());
        }

        [Test]
        public void SourceTurnedFalse_NoEvent()
        {
            _wsm.SetFact(_source, true);
            _wsm.GetFact(_elapsed);
            _wsm.SetFact(_source, false);
            _clock.Restore(13 * HOUR);
            Tick();
            Assert.AreEqual(0, ElapsedEvents());
        }

        [Test]
        public void SourceReSetLater_FiresAtNewDeadline()
        {
            _wsm.SetFact(_source, true);
            _wsm.GetFact(_elapsed);               // deadline 12:00
            _wsm.SetFact(_source, false);
            _clock.Restore(11 * HOUR);
            _wsm.SetFact(_source, true);          // new time 11:00 → 13:00
            _clock.Restore(12 * HOUR);
            Tick();                               // old deadline: re-checked, still running → re-registered
            Assert.AreEqual(0, ElapsedEvents());
            _clock.Restore(13 * HOUR);
            Tick();
            Assert.AreEqual(1, ElapsedEvents());
        }

        [Test]
        public void Restore_ClearsPending()
        {
            _wsm.SetFact(_source, true);
            _wsm.GetFact(_elapsed);
            _wsm.RestoreFactSetTimes(_wsm.CaptureFactSetTimes());
            _clock.Restore(12 * HOUR);
            Tick();
            Assert.AreEqual(0, ElapsedEvents());
        }

        [Test]
        public void WithoutDelay_ReturnsSource()
        {
            Assert.AreSame(_source, ElapsedFact.WithoutDelay(_elapsed));
            Assert.AreSame(_source, ElapsedFact.WithoutDelay(_source));
        }
    }
}
