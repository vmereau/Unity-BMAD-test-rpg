using Game.Combat;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests for <see cref="PlayerAttackTracker"/> — the player attack lifecycle that follows the
    /// Animator's attack-state enter/exit callbacks (combo lock-up fix).
    /// </summary>
    public class PlayerAttackTrackerTests
    {
        private const float Timeout = 1.5f;

        [Test]
        public void SingleAttack_EndsOnExit()
        {
            var t = new PlayerAttackTracker();
            t.Press(0f, isChainStep: false);
            t.Enter(0.05f);
            Assert.IsTrue(t.Exit(1f), "Only attack state exited — attack must end");
        }

        [Test]
        public void Chain_OutgoingExitDuringCrossfade_DoesNotEndAttack()
        {
            var t = new PlayerAttackTracker();
            t.Press(0f, false);
            t.Enter(0f);                 // Attack_1
            t.Press(0.5f, true);         // combo press
            t.Enter(0.5f);               // Attack_2 enters at transition start
            Assert.IsFalse(t.Exit(0.9f), "Attack_1 exit at transition end must not end the chain");
            Assert.IsTrue(t.Exit(2f), "Attack_2 exit ends the attack");
        }

        [Test]
        public void LatePress_NeverTaken_EndsOnOutgoingExit()
        {
            // Press lands during the uninterruptible blend-out to CombatIdle: the next state never enters
            var t = new PlayerAttackTracker();
            t.Press(0f, false);
            t.Enter(0f);
            t.Press(0.85f, true);
            Assert.IsTrue(t.ShouldIgnoreWindowClose, "Close event while a step is queued is ignored");
            Assert.IsTrue(t.Exit(1f), "No attack state left — attack must end despite the queued press");
        }

        [Test]
        public void WindowClose_IgnoredWhileTwoStatesActive()
        {
            var t = new PlayerAttackTracker();
            t.Enter(0f);
            t.Press(0.6f, true);
            t.Enter(0.6f);               // queue consumed, crossfade in progress
            Assert.IsFalse(t.IsStepQueued);
            Assert.IsTrue(t.ShouldIgnoreWindowClose, "Outgoing clip's close event during the crossfade is ignored");
            t.Exit(0.9f);
            Assert.IsFalse(t.ShouldIgnoreWindowClose, "Single state, nothing queued — close is a real expiry");
        }

        [Test]
        public void TriggerNeverTaken_StallsAfterTimeout()
        {
            var t = new PlayerAttackTracker();
            t.Press(10f, false);
            Assert.IsFalse(t.IsStalled(true, 10f + Timeout - 0.01f, Timeout));
            Assert.IsTrue(t.IsStalled(true, 10f + Timeout + 0.01f, Timeout));
        }

        [Test]
        public void ClipEvents_KeepAttackAlive()
        {
            var t = new PlayerAttackTracker();
            t.Press(0f, false);
            t.Enter(0.1f);
            t.Event(1.2f);
            Assert.IsFalse(t.IsStalled(true, 2.5f, Timeout));
        }

        [Test]
        public void NotAttacking_NeverStalls()
        {
            var t = new PlayerAttackTracker();
            Assert.IsFalse(t.IsStalled(false, 100f, Timeout));
        }

        [Test]
        public void Exit_WithoutEnter_ClampsAtZero()
        {
            var t = new PlayerAttackTracker();
            Assert.IsTrue(t.Exit(0f));
            Assert.AreEqual(0, t.ActiveStates);
        }

        [Test]
        public void ClearQueue_KeepsActiveStates()
        {
            // Dodge cancel: the Animator still owns the attack state until its exit
            var t = new PlayerAttackTracker();
            t.Enter(0f);
            t.Press(0.5f, true);
            t.ClearQueue();
            Assert.IsFalse(t.IsStepQueued);
            Assert.AreEqual(1, t.ActiveStates);
        }

        [Test]
        public void LostExit_FreshPressResyncsCount()
        {
            // An exit Unity never delivered leaves the count at 1; the next Attack_1 must still end on its exit
            var t = new PlayerAttackTracker();
            t.Enter(0f);                 // exit lost
            t.Press(5f, isChainStep: false);
            t.Enter(5f);
            Assert.IsFalse(t.ShouldIgnoreWindowClose, "Drifted count must not keep blocking the window close");
            Assert.IsTrue(t.Exit(6f), "Fresh attack ends on its own exit");
        }

        [Test]
        public void ChainPress_KeepsActiveCount()
        {
            var t = new PlayerAttackTracker();
            t.Press(0f, false);
            t.Enter(0f);
            t.Press(0.5f, isChainStep: true);
            Assert.AreEqual(1, t.ActiveStates, "A combo press must not resync — Attack_n is still playing");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var t = new PlayerAttackTracker();
            t.Enter(0f);
            t.Enter(0f);
            t.Press(0f, true);
            t.Reset();
            Assert.AreEqual(0, t.ActiveStates);
            Assert.IsFalse(t.IsStepQueued);
        }
    }
}
