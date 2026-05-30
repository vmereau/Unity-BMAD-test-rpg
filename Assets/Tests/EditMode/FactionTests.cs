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
    }
}
