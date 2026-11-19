using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="FactRemovedObjectRules"/> (blocker and NPC removal decisions).</summary>
    public class FactRemovedObjectRulesTests
    {
        private static FactRemovalAction Decide(bool factTrue, bool isEntity = false, bool isDead = false,
            bool isBusy = false, bool targetActive = true, bool hiddenByMe = false) =>
            FactRemovedObjectRules.Decide(factTrue, isEntity, isDead, isBusy, targetActive, hiddenByMe);

        // ── Blocker (non-entity target) ──

        [Test]
        public void Blocker_FactTrue_Active_Hides() =>
            Assert.AreEqual(FactRemovalAction.Hide, Decide(true));

        [Test]
        public void Blocker_FactTrue_AlreadyHidden_None() =>
            Assert.AreEqual(FactRemovalAction.None, Decide(true, targetActive: false));

        [Test]
        public void Blocker_FactFalse_HiddenByMe_Shows() =>
            Assert.AreEqual(FactRemovalAction.Show, Decide(false, targetActive: false, hiddenByMe: true));

        [Test]
        public void FactFalse_HiddenBySomethingElse_NeverShown() =>
            Assert.AreEqual(FactRemovalAction.None, Decide(false, targetActive: false));

        [Test]
        public void FactTrue_HiddenBySomethingElse_None() =>
            Assert.AreEqual(FactRemovalAction.None, Decide(true, isEntity: true, targetActive: false));

        [Test]
        public void Blocker_FactFalse_Active_None() =>
            Assert.AreEqual(FactRemovalAction.None, Decide(false));

        [Test]
        public void Blocker_IgnoresDeadAndBusyFlags() =>
            Assert.AreEqual(FactRemovalAction.Hide, Decide(true, isDead: true, isBusy: true));

        // ── NPC (entity target) ──

        [Test]
        public void Npc_FactTrue_Calm_Hides() =>
            Assert.AreEqual(FactRemovalAction.Hide, Decide(true, isEntity: true));

        [Test]
        public void Npc_FactTrue_Busy_Waits() =>
            Assert.AreEqual(FactRemovalAction.Wait, Decide(true, isEntity: true, isBusy: true));

        [Test]
        public void Npc_FactTrue_BusyButAlreadyHidden_None() =>
            Assert.AreEqual(FactRemovalAction.None, Decide(true, isEntity: true, isBusy: true, targetActive: false));

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Npc_Dead_NeverTouched(bool factTrue, bool targetActive) =>
            Assert.AreEqual(FactRemovalAction.None, Decide(factTrue, isEntity: true, isDead: true, targetActive: targetActive, hiddenByMe: true));

        [Test]
        public void Npc_FactFalse_Hidden_ComesBack() =>
            Assert.AreEqual(FactRemovalAction.Show, Decide(false, isEntity: true, targetActive: false, hiddenByMe: true));

        [Test]
        public void Npc_FactFalse_Busy_ShowNeverWaits() =>
            Assert.AreEqual(FactRemovalAction.Show, Decide(false, isEntity: true, isBusy: true, targetActive: false, hiddenByMe: true));
    }
}
