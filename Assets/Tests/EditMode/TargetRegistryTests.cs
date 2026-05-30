using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Game.Combat;
using Game.Factions;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for TargetRegistry.FindClosestHostile filtering logic.
    /// FactionMember.OnEnable/OnDisable are thin passthroughs to TargetRegistry.Register/Unregister;
    /// MonoBehaviour lifecycle callbacks are not reliably invoked in Edit Mode, so registration is
    /// driven explicitly via the public API here (project rule: do not test MonoBehaviour lifecycle
    /// in Edit Mode). The private _faction/_damageable fields are injected via reflection to build a
    /// fully-resolved member without relying on Awake (mirrors WorldStateManagerFactsTests).
    /// </summary>
    public class TargetRegistryTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo s_factionOverride = typeof(FactionMember).GetField("_factionOverride", Instance);
        private static readonly FieldInfo s_faction = typeof(FactionMember).GetField("_faction", Instance);
        private static readonly FieldInfo s_damageable = typeof(FactionMember).GetField("_damageable", Instance);
        private static readonly FieldInfo s_members =
            typeof(TargetRegistry).GetField("_members", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Object> _cleanup = new();

        private class StubDamageable : MonoBehaviour, IDamageable
        {
            public bool IsDead { get; set; }
            public void TakeDamage(float amount) { }
            public HitResult TryReceiveHit(GameObject attacker) => HitResult.NotBlocked;
        }

        private FactionMember MakeMember(string name, FactionSO faction, Vector3 position, bool dead = false)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var dmg = go.AddComponent<StubDamageable>();
            dmg.IsDead = dead;
            var m = go.AddComponent<FactionMember>();
            // Build a fully-resolved member without depending on Awake/OnEnable firing in Edit Mode.
            s_factionOverride.SetValue(m, faction);
            s_faction.SetValue(m, faction);
            s_damageable.SetValue(m, dmg);
            TargetRegistry.Register(m);
            _cleanup.Add(go);
            return m;
        }

        private FactionSO MakeFaction(string name, List<FactionSO> hostile = null)
        {
            var f = ScriptableObject.CreateInstance<FactionSO>();
            f.factionName = name;
            f.InitForTest(hostile ?? new List<FactionSO>());
            _cleanup.Add(f);
            return f;
        }

        [SetUp]
        public void SetUp()
        {
            // Guarantee isolation — the registry HashSet is static and survives between Edit Mode tests.
            ((HashSet<FactionMember>)s_members.GetValue(null)).Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
            ((HashSet<FactionMember>)s_members.GetValue(null)).Clear();
        }

        [Test]
        public void FindClosestHostile_ReturnsRegisteredHostileMember()
        {
            var fA = MakeFaction("A");
            var fB = MakeFaction("B");
            fA.InitForTest(new List<FactionSO> { fB });
            MakeMember("B1", fB, new Vector3(2, 0, 0));
            var result = TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Faction, Is.EqualTo(fB));
        }

        [Test]
        public void FindClosestHostile_SkipsNonHostileFactions()
        {
            var fA = MakeFaction("A");
            var fNeutral = MakeFaction("Neutral");
            MakeMember("N1", fNeutral, new Vector3(2, 0, 0));
            Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void FindClosestHostile_ReturnsNearestWhenMultipleHostiles()
        {
            var fA = MakeFaction("A");
            var fB = MakeFaction("B");
            fA.InitForTest(new List<FactionSO> { fB });
            var far = MakeMember("Far", fB, new Vector3(8, 0, 0));
            var near = MakeMember("Near", fB, new Vector3(2, 0, 0));
            var result = TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f);
            Assert.That(result, Is.EqualTo(near));
        }

        [Test]
        public void FindClosestHostile_SkipsDeadMembers()
        {
            var fA = MakeFaction("A");
            var fB = MakeFaction("B");
            fA.InitForTest(new List<FactionSO> { fB });
            MakeMember("Dead", fB, new Vector3(2, 0, 0), dead: true);
            Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void FindClosestHostile_SkipsOutOfRange()
        {
            var fA = MakeFaction("A");
            var fB = MakeFaction("B");
            fA.InitForTest(new List<FactionSO> { fB });
            MakeMember("Far", fB, new Vector3(20, 0, 0));
            Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void FindClosestHostile_ReturnsNull_WhenMyFactionIsNull()
        {
            Assert.That(TargetRegistry.FindClosestHostile(null, Vector3.zero, 10f), Is.Null);
        }

        [Test]
        public void Unregister_RemovesMemberFromRegistry()
        {
            var fA = MakeFaction("A");
            var fB = MakeFaction("B");
            fA.InitForTest(new List<FactionSO> { fB });
            var m = MakeMember("B1", fB, new Vector3(2, 0, 0));
            Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Not.Null); // registered
            TargetRegistry.Unregister(m);
            Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
        }
    }
}
