using _Game.ScriptableObjects.Entities;
using Game.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>spec-stat-scaling-balance: raw weapon damage, armor mitigation, level-derived XP.</summary>
    public class DamageMathTests
    {
        private const float FLOOR = 0.1f;

        [Test] public void Mitigate_ArmorBelowRaw_Subtracts() => Assert.AreEqual(10f, DamageMath.Mitigate(20f, 10f, FLOOR), 0.001f);
        [Test] public void Mitigate_ArmorAboveRaw_UsesFractionFloor() => Assert.AreEqual(1.8f, DamageMath.Mitigate(18f, 70f, FLOOR), 0.001f);
        [Test] public void Mitigate_TinyRaw_AbsoluteMinimumOne() => Assert.AreEqual(1f, DamageMath.Mitigate(3f, 50f, FLOOR), 0.001f);
        [Test] public void Mitigate_ZeroRaw_NoDamage() => Assert.AreEqual(0f, DamageMath.Mitigate(0f, 10f, FLOOR), 0.001f);
        [Test] public void Mitigate_NegativeRaw_NoHeal() => Assert.AreEqual(0f, DamageMath.Mitigate(-5f, 0f, FLOOR), 0.001f);
        [Test] public void Mitigate_NoArmor_FullDamage() => Assert.AreEqual(25f, DamageMath.Mitigate(25f, 0f, FLOOR), 0.001f);
        [Test] public void Mitigate_NegativeArmor_TreatedAsZero() => Assert.AreEqual(25f, DamageMath.Mitigate(25f, -10f, FLOOR), 0.001f);
        [Test] public void Mitigate_FractionAboveOne_Clamped() => Assert.AreEqual(25f, DamageMath.Mitigate(25f, 100f, 5f), 0.001f);

        [Test] public void WeaponRaw_StrengthSword() => Assert.AreEqual(27f, DamageMath.WeaponRaw(12f, 15), 0.001f);
        [Test] public void WeaponRaw_DexterityBow() => Assert.AreEqual(26f, DamageMath.WeaponRaw(12f, 14), 0.001f);
        [Test] public void WeaponRaw_Unarmed_FistsPlusStrengthTimesStep() => Assert.AreEqual(30f, DamageMath.WeaponRaw(10f, 10) * 1.5f, 0.001f);
        [Test] public void WeaponRaw_NegativeStat_IgnoredNeverNegative() => Assert.AreEqual(0f, DamageMath.WeaponRaw(-4f, -3), 0.001f);

        [Test]
        public void Entity_NegativeXpOnKill_DerivedFromLevel()
        {
            var entity = ScriptableObject.CreateInstance<Entity>();
            try
            {
                var so = new SerializedObject(entity);
                so.FindProperty("_level").intValue = 5;
                so.FindProperty("_xpOnKill").intValue = -1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(5 * Entity.XP_PER_LEVEL, entity.XpOnKill);

                so.FindProperty("_xpOnKill").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(0, entity.XpOnKill, "an explicit 0 overrides the level");
            }
            finally { Object.DestroyImmediate(entity); }
        }
    }
}
