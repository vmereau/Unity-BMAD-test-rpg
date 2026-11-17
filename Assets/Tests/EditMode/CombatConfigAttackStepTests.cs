using Game.Combat;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>CombatConfigSO.ResolveAttackStep — the step rule shared by the player and AI humanoids
    /// (spec-ai-attack-root-and-step).</summary>
    public class CombatConfigAttackStepTests
    {
        private static readonly AttackStep Default = new AttackStep(0.5f, 0.35f, 0.35f);
        private static readonly AttackStep Jab = new AttackStep(0.3f, 0.07f, 0.43f);
        private static readonly AttackStep Hook = new AttackStep(0.35f, 0.27f, 0.37f);
        private static readonly AttackStep Swing = new AttackStep(0.4f, 0.2f, 0.3f);

        private CombatConfigSO _config;
        private WeaponArchetypeSO _archetype;
        private WeaponSO _weapon;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CombatConfigSO>();
            _config.defaultAttackStep = Default;
            _config.unarmedAttacks = new[]
            {
                new UnarmedAttack(Jab, WeaponHand.Left),
                new UnarmedAttack(Hook, WeaponHand.Right),
            };
            _archetype = ScriptableObject.CreateInstance<WeaponArchetypeSO>();
            _archetype.attackSteps = new[] { Swing };
            _weapon = ScriptableObject.CreateInstance<SwordSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_archetype);
            Object.DestroyImmediate(_weapon);
        }

        [Test]
        public void Unarmed_ListedIndex_UsesThatEntry()
        {
            Assert.That(_config.ResolveAttackStep(null, 1), Is.EqualTo(Jab));
            Assert.That(_config.ResolveAttackStep(null, 2), Is.EqualTo(Hook));
        }

        [Test]
        public void Unarmed_IndexPastTheList_UsesDefault()
            => Assert.That(_config.ResolveAttackStep(null, 3), Is.EqualTo(Default));

        [Test]
        public void Unarmed_IndexZero_UsesDefault()
            => Assert.That(_config.ResolveAttackStep(null, 0), Is.EqualTo(Default));

        [Test]
        public void Weapon_ArchetypeStep_UsesIt()
        {
            _weapon.archetype = _archetype;
            Assert.That(_config.ResolveAttackStep(_weapon, 1), Is.EqualTo(Swing));
        }

        [Test]
        public void Weapon_IndexPastArchetypeSteps_UsesDefault()
        {
            _weapon.archetype = _archetype;
            Assert.That(_config.ResolveAttackStep(_weapon, 2), Is.EqualTo(Default));
        }

        [Test]
        public void Weapon_NoArchetype_UsesDefaultNotUnarmed()
            => Assert.That(_config.ResolveAttackStep(_weapon, 1), Is.EqualTo(Default));
    }
}
