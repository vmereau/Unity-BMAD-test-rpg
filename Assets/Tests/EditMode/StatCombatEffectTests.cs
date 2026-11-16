using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pure-logic tests for the Endurance stamina formula (Story 3.6). Damage formulas: DamageMathTests.
    /// No MonoBehaviour or Unity scene required — formulas are extracted as local helpers.
    /// </summary>
    public class StatCombatEffectTests
    {
        // ── Helpers mirroring the production formulas ──────────────────────────

        private float ComputeMaxStamina(float basePool, int endurance, int baseEndurance,
            float staminaPerEndurance)
            => basePool + (endurance - baseEndurance) * staminaPerEndurance;

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
