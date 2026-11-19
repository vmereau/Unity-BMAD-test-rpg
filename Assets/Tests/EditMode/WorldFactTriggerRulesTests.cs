using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="WorldFactTriggerRules"/> (hour window, required-NPC gate).</summary>
    public class WorldFactTriggerRulesTests
    {
        // ── Fact gate ──

        [Test]
        public void FactGate_NoFact_Passes() => Assert.IsTrue(WorldFactTriggerRules.FactGatePasses(false, false));

        [Test]
        public void FactGate_FactFalse_Blocks() => Assert.IsFalse(WorldFactTriggerRules.FactGatePasses(true, false));

        [Test]
        public void FactGate_FactTrue_Passes() => Assert.IsTrue(WorldFactTriggerRules.FactGatePasses(true, true));

        // ── Hour gate ──

        [Test]
        public void HourGate_Off_PassesEvenWithoutClock() =>
            Assert.IsTrue(WorldFactTriggerRules.HourGatePasses(false, false, 12, 0, 3));

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(12, false)]
        public void HourGate_PlainWindow(int hour, bool expected) =>
            Assert.AreEqual(expected, WorldFactTriggerRules.HourGatePasses(true, true, hour, 0, 3));

        [TestCase(22, true)]
        [TestCase(23, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(21, false)]
        public void HourGate_WrapsMidnight(int hour, bool expected) =>
            Assert.AreEqual(expected, WorldFactTriggerRules.HourGatePasses(true, true, hour, 22, 3));

        [TestCase(0)]
        [TestCase(13)]
        public void HourGate_StartEqualsEnd_AllDay(int hour) =>
            Assert.IsTrue(WorldFactTriggerRules.HourGatePasses(true, true, hour, 5, 5));

        [Test]
        public void HourGate_NoClock_NeverPasses() =>
            Assert.IsFalse(WorldFactTriggerRules.HourGatePasses(true, false, 1, 0, 3));

        // ── NPC gate ──

        [Test]
        public void NpcGate_PresentAliveConsciousPlaced_Passes() =>
            Assert.IsTrue(WorldFactTriggerRules.NpcGatePasses(true, false, false, true));

        [Test]
        public void NpcGate_Absent_Fails() =>
            Assert.IsFalse(WorldFactTriggerRules.NpcGatePasses(false, false, false, true));

        [Test]
        public void NpcGate_Dead_Fails() =>
            Assert.IsFalse(WorldFactTriggerRules.NpcGatePasses(true, true, false, true));

        [Test]
        public void NpcGate_Unconscious_Fails() =>
            Assert.IsFalse(WorldFactTriggerRules.NpcGatePasses(true, false, true, true));

        [Test]
        public void NpcGate_NotPlaced_Fails() =>
            Assert.IsFalse(WorldFactTriggerRules.NpcGatePasses(true, false, false, false));

        // ── Placement ──

        [Test]
        public void InBlock_SameLabel_True() => Assert.IsTrue(WorldFactTriggerRules.IsInBlock("BarnDoor", "BarnDoor"));

        [TestCase("Barracks")]
        [TestCase("barndoor")]
        [TestCase(null)]
        public void InBlock_OtherOrNoBlock_False(string current) =>
            Assert.IsFalse(WorldFactTriggerRules.IsInBlock(current, "BarnDoor"));

        [Test]
        public void InBlock_EmptyRequired_False()
        {
            Assert.IsFalse(WorldFactTriggerRules.IsInBlock(null, null));
            Assert.IsFalse(WorldFactTriggerRules.IsInBlock("", ""));
        }

        [Test]
        public void NearSpot_WithinRadius_IgnoresHeight() =>
            Assert.IsTrue(WorldFactTriggerRules.IsNearSpot(new Vector3(1f, 5f, 1f), Vector3.zero, 1.5f));

        [Test]
        public void NearSpot_OnRadius_True() =>
            Assert.IsTrue(WorldFactTriggerRules.IsNearSpot(new Vector3(2f, 0f, 0f), Vector3.zero, 2f));

        [Test]
        public void NearSpot_OutsideRadius_False() =>
            Assert.IsFalse(WorldFactTriggerRules.IsNearSpot(new Vector3(2f, 0f, 1f), Vector3.zero, 2f));
    }
}
