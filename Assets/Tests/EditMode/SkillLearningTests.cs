using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure logic tests for skill learning mechanics. No MonoBehaviour or scene required.
    /// Story 3.5: Tome skill learning validation.
    /// </summary>
    public class SkillLearningTests
    {
        // --- Pure logic helpers (mirrors PlayerSkills / TomePickup logic) ---

        private bool CanLearnSkill(bool alreadyLearned, bool hasEnoughLP)
            => !alreadyLearned && hasEnoughLP;

        private bool IsSkillKnown(HashSet<string> known, string id)
            => known.Contains(id);

        private HashSet<string> SimulateLearnSkill(HashSet<string> known, string id)
        {
            known.Add(id);
            return known;
        }

        private int SimulateLPAfterLearn(int currentLP, int cost)
            => currentLP - cost;

        // --- Tests ---

        [Test]
        public void CanLearn_WhenNewSkillAndSufficientLP_ReturnsTrue()
        {
            Assert.IsTrue(CanLearnSkill(alreadyLearned: false, hasEnoughLP: true));
        }

        [Test]
        public void CannotLearn_WhenAlreadyLearned_ReturnsFalse()
        {
            Assert.IsFalse(CanLearnSkill(alreadyLearned: true, hasEnoughLP: true));
        }

        [Test]
        public void CannotLearn_WhenInsufficientLP_ReturnsFalse()
        {
            Assert.IsFalse(CanLearnSkill(alreadyLearned: false, hasEnoughLP: false));
        }

        [Test]
        public void CannotLearn_WhenAlreadyLearnedAndInsufficientLP_ReturnsFalse()
        {
            Assert.IsFalse(CanLearnSkill(alreadyLearned: true, hasEnoughLP: false));
        }

        [Test]
        public void SkillTracking_AfterLearn_ContainsSkillId()
        {
            var known = new HashSet<string>();
            SimulateLearnSkill(known, "power_strike");
            Assert.IsTrue(IsSkillKnown(known, "power_strike"));
        }

        [Test]
        public void SkillTracking_MultipleDistinctSkills_AllTracked()
        {
            var known = new HashSet<string>();
            SimulateLearnSkill(known, "power_strike");
            SimulateLearnSkill(known, "stealth_walk");
            Assert.IsTrue(IsSkillKnown(known, "power_strike"));
            Assert.IsTrue(IsSkillKnown(known, "stealth_walk"));
        }

        [Test]
        public void LPCost_DeductedCorrectly_AfterLearn()
        {
            int remaining = SimulateLPAfterLearn(currentLP: 5, cost: 2);
            Assert.AreEqual(3, remaining);
        }

        [Test]
        public void CanLearn_ExactLP_SameAsCost_PassesLPCheck()
        {
            // Boundary: LP exactly equals cost — hasEnoughLP should be true
            int currentLP = 2;
            int cost = 2;
            bool hasEnoughLP = currentLP >= cost;
            Assert.IsTrue(CanLearnSkill(alreadyLearned: false, hasEnoughLP: hasEnoughLP));
        }
    }
}
