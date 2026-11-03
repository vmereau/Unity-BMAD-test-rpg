using System.Collections.Generic;
using Game.Factions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class FactionTests
    {
        private readonly List<Object> _cleanup = new();

        private FactionSO MakeFaction(string name) =>
            Track(ScriptableObject.CreateInstance<FactionSO>(), name);

        private FactionSO Track(FactionSO f, string name)
        {
            f.factionName = name;
            _cleanup.Add(f);
            return f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        [Test]
        public void IsHostileTo_ReturnsTrue_WhenOtherInHostileList()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(new List<FactionSO> { b });
            Assert.That(a.IsHostileTo(b), Is.True);
        }

        [Test]
        public void IsHostileTo_ReturnsFalse_WhenOtherNotInList()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(new List<FactionSO>());
            Assert.That(a.IsHostileTo(b), Is.False);
        }

        [Test]
        public void IsHostileTo_ReturnsFalse_WhenOtherIsNull()
        {
            var a = MakeFaction("A");
            a.InitForTest(new List<FactionSO>());
            Assert.That(a.IsHostileTo(null), Is.False);
        }

        [Test]
        public void IsAlliedWith_ReturnsTrue_WhenOtherInAlliedList()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(null, new List<FactionSO> { b });
            Assert.That(a.IsAlliedWith(b), Is.True);
        }

        [Test]
        public void IsHostileTo_IsAsymmetric_ByDesign()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(new List<FactionSO> { b });
            b.InitForTest(new List<FactionSO>());
            Assert.That(a.IsHostileTo(b), Is.True);
            Assert.That(b.IsHostileTo(a), Is.False); // documents symmetric-by-convention contract
        }

        [Test]
        public void AreHostile_OneWayList_IsHostileBothWays()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(new List<FactionSO> { b });
            Assert.IsTrue(FactionSO.AreHostile(a, b));
            Assert.IsTrue(FactionSO.AreHostile(b, a));
        }

        [Test]
        public void AreHostile_BothListEachOther_IsTrue()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            a.InitForTest(new List<FactionSO> { b });
            b.InitForTest(new List<FactionSO> { a });
            Assert.IsTrue(FactionSO.AreHostile(a, b));
        }

        [Test]
        public void AreHostile_NoListing_IsFalse()
        {
            var a = MakeFaction("A");
            var b = MakeFaction("B");
            Assert.IsFalse(FactionSO.AreHostile(a, b));
        }

        [Test]
        public void AreHostile_NullFaction_IsFalse()
        {
            var a = MakeFaction("A");
            a.InitForTest(new List<FactionSO> { a });
            Assert.IsFalse(FactionSO.AreHostile(a, null));
            Assert.IsFalse(FactionSO.AreHostile(null, a));
            Assert.IsFalse(FactionSO.AreHostile(null, null));
        }
    }
}
