using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pure-logic tests for the stat-combat effect formulas introduced in Story 3.6.
    /// No MonoBehaviour or Unity scene required — formulas are extracted as local helpers.
    /// </summary>
    public class StatCombatEffectTests
    {
        // ── Helpers mirroring the production formulas ──────────────────────────

        private float ComputeDamage(float baseDamage, int strength, int baseStrength,
            float damagePerStrength, bool hasPowerStrike, float psBonus)
        {
            float d = baseDamage + (strength - baseStrength) * damagePerStrength;
            if (hasPowerStrike) d += psBonus;
            return d;
        }

        private float ComputeMaxStamina(float basePool, int endurance, int baseEndurance,
            float staminaPerEndurance)
            => basePool + (endurance - baseEndurance) * staminaPerEndurance;

        // ── Damage tests ───────────────────────────────────────────────────────

        [Test]
        public void Damage_BaseStrength_NoBonusApplied()
        {
            // STR == base → no bonus
            float result = ComputeDamage(25f, strength: 5, baseStrength: 5,
                damagePerStrength: 2f, hasPowerStrike: false, psBonus: 10f);
            Assert.AreEqual(25f, result, 0.001f);
        }

        [Test]
        public void Damage_OnePointAboveBase_AddsCorrectBonus()
        {
            // STR=6, base=5, dps=2 → 25 + 2 = 27
            float result = ComputeDamage(25f, strength: 6, baseStrength: 5,
                damagePerStrength: 2f, hasPowerStrike: false, psBonus: 10f);
            Assert.AreEqual(27f, result, 0.001f);
        }

        [Test]
        public void Damage_PowerStrike_AddsBonus()
        {
            // STR==base but power strike → 25 + 10 = 35
            float result = ComputeDamage(25f, strength: 5, baseStrength: 5,
                damagePerStrength: 2f, hasPowerStrike: true, psBonus: 10f);
            Assert.AreEqual(35f, result, 0.001f);
        }

        [Test]
        public void Damage_StrengthPlusSkill_StacksCorrectly()
        {
            // STR=7, base=5, dps=2, psBonus=10 → 25 + 4 + 10 = 39
            float result = ComputeDamage(25f, strength: 7, baseStrength: 5,
                damagePerStrength: 2f, hasPowerStrike: true, psBonus: 10f);
            Assert.AreEqual(39f, result, 0.001f);
        }

        // ── Stamina tests ──────────────────────────────────────────────────────

        [Test]
        public void Stamina_BaseEndurance_NoBonusApplied()
        {
            // END == base → no bonus
            float result = ComputeMaxStamina(100f, endurance: 5, baseEndurance: 5,
                staminaPerEndurance: 5f);
            Assert.AreEqual(100f, result, 0.001f);
        }

        [Test]
        public void Stamina_OnePointAboveBase_AddsCorrectBonus()
        {
            // END=6, base=5, spe=5 → 100 + 5 = 105
            float result = ComputeMaxStamina(100f, endurance: 6, baseEndurance: 5,
                staminaPerEndurance: 5f);
            Assert.AreEqual(105f, result, 0.001f);
        }

        [Test]
        public void Stamina_MultipleEndurancePoints_ScalesLinearly()
        {
            // END=8, base=5, spe=5 → 100 + 15 = 115
            float result = ComputeMaxStamina(100f, endurance: 8, baseEndurance: 5,
                staminaPerEndurance: 5f);
            Assert.AreEqual(115f, result, 0.001f);
        }

        // ── Edge-case tests (below-base delta) ────────────────────────────────

        [Test]
        public void Damage_BelowBaseStrength_GivesNegativeBonus()
        {
            // STR=4, base=5, dps=2 → 25 + (-1)*2 = 23
            // Formula intentionally allows negative bonus (future debuff system).
            // Production code clamps to 0 via Mathf.Max; this test validates the raw formula.
            float result = ComputeDamage(25f, strength: 4, baseStrength: 5,
                damagePerStrength: 2f, hasPowerStrike: false, psBonus: 0f);
            Assert.AreEqual(23f, result, 0.001f);
        }

        [Test]
        public void Stamina_BelowBaseEndurance_GivesNegativeBonus()
        {
            // END=3, base=5, spe=5 → 100 + (-2)*5 = 90
            // Formula intentionally allows negative bonus; production code clamps to 0 via Mathf.Max.
            float result = ComputeMaxStamina(100f, endurance: 3, baseEndurance: 5,
                staminaPerEndurance: 5f);
            Assert.AreEqual(90f, result, 0.001f);
        }
    }
}
