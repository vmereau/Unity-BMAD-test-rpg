using System.Collections.Generic;
using Game.Factions;
using Game.NPC;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="RestrictedAreaRules"/> (box containment, hours window, owner matching).</summary>
    public class RestrictedAreaRulesTests
    {
        private static readonly Vector3 Center = new(1f, 2f, 0f);
        private static readonly Vector3 Size = new(4f, 2f, 6f);

        [Test]
        public void InsideBox_True() => Assert.IsTrue(RestrictedAreaRules.IsInsideBox(new Vector3(2f, 2.5f, -2f), Center, Size));

        [Test]
        public void OnFace_True() => Assert.IsTrue(RestrictedAreaRules.IsInsideBox(new Vector3(3f, 2f, 3f), Center, Size));

        [TestCase(3.1f, 2f, 0f)]
        [TestCase(1f, 3.1f, 0f)]
        [TestCase(1f, 2f, -3.1f)]
        public void OutsideOnOneAxis_False(float x, float y, float z) =>
            Assert.IsFalse(RestrictedAreaRules.IsInsideBox(new Vector3(x, y, z), Center, Size));

        [Test]
        public void RotatedBox_UsesLocalPoint()
        {
            // A box rotated 90° around Y: its local +Z points to world +X, so a world point along +X lies on local +Z.
            var go = new GameObject("box");
            try
            {
                go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 local = go.transform.InverseTransformPoint(new Vector3(2.5f, 0f, 0f));
                Assert.AreEqual(2.5f, local.z, 1e-4f);
                // Off-centre box along local +Z only: catches a sign error in the local point.
                Assert.IsTrue(RestrictedAreaRules.IsInsideBox(local, new Vector3(0f, 0f, 2f), new Vector3(1f, 1f, 2f)));
                Assert.IsFalse(RestrictedAreaRules.IsInsideBox(local, new Vector3(0f, 0f, -2f), new Vector3(1f, 1f, 2f)));
                Assert.IsFalse(RestrictedAreaRules.IsInsideBox(local, Vector3.zero, new Vector3(6f, 1f, 1f)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NoHoursWindow_AlwaysRestricted() =>
            Assert.IsTrue(RestrictedAreaRules.IsRestrictedHour(true, false, 12, 21, 6));

        [Test]
        public void NoClock_AlwaysRestricted() =>
            Assert.IsTrue(RestrictedAreaRules.IsRestrictedHour(false, true, 12, 21, 6));

        [TestCase(21, true)]
        [TestCase(23, true)]
        [TestCase(0, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(12, false)]
        [TestCase(20, false)]
        public void NightWindow_WrapsMidnight(int hour, bool expected) =>
            Assert.AreEqual(expected, RestrictedAreaRules.IsRestrictedHour(true, true, hour, 21, 6));

        [TestCase(8, true)]
        [TestCase(17, false)]
        [TestCase(7, false)]
        public void DayWindow(int hour, bool expected) =>
            Assert.AreEqual(expected, RestrictedAreaRules.IsRestrictedHour(true, true, hour, 8, 17));

        [Test]
        public void StartEqualsEnd_AllDay() =>
            Assert.IsTrue(RestrictedAreaRules.IsRestrictedHour(true, true, 3, 10, 10));

        [Test]
        public void Owner_ByEntityOrFaction()
        {
            var guard = ScriptableObject.CreateInstance<NPCEntity>();
            var other = ScriptableObject.CreateInstance<NPCEntity>();
            var company = ScriptableObject.CreateInstance<FactionSO>();
            var neutral = ScriptableObject.CreateInstance<FactionSO>();
            try
            {
                var entities = new List<NPCEntity> { guard };
                var factions = new List<FactionSO> { company };
                Assert.IsTrue(RestrictedAreaRules.IsOwner(entities, factions, guard, neutral), "listed entity");
                Assert.IsTrue(RestrictedAreaRules.IsOwner(entities, factions, other, company), "listed faction");
                Assert.IsFalse(RestrictedAreaRules.IsOwner(entities, factions, other, neutral), "neither");
                Assert.IsFalse(RestrictedAreaRules.IsOwner(entities, factions, null, null), "nulls");
                Assert.IsFalse(RestrictedAreaRules.IsOwner(null, null, guard, company), "no owners");
            }
            finally
            {
                Object.DestroyImmediate(guard);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(company);
                Object.DestroyImmediate(neutral);
            }
        }
    }
}
