using Game.Player;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class PlayerAnimationDriverTests
    {
        private const float TOLERANCE = 1e-5f;

        [Test]
        public void FullBodyWeight_RisesDuringFullBodyAttack()
            => Assert.That(PlayerAnimationDriver.StepFullBodyWeight(0f, true, true, 0.05f, 0.15f), Is.EqualTo(1f / 3f).Within(TOLERANCE));

        [Test]
        public void FullBodyWeight_ReachesOneAfterTheBlendTime()
        {
            float w = 0f;
            for (int i = 0; i < 4; i++) w = PlayerAnimationDriver.StepFullBodyWeight(w, true, true, 0.05f, 0.15f);
            Assert.That(w, Is.EqualTo(1f));
        }

        [Test]
        public void FullBodyWeight_FallsWhenTheAttackEnds()
            => Assert.That(PlayerAnimationDriver.StepFullBodyWeight(1f, false, true, 0.05f, 0.15f), Is.EqualTo(2f / 3f).Within(TOLERANCE));

        [Test]
        public void FullBodyWeight_StaysZeroForUpperBodyAttacks()
            => Assert.That(PlayerAnimationDriver.StepFullBodyWeight(0f, true, false, 0.05f, 0.15f), Is.EqualTo(0f));

        [Test]
        public void FullBodyWeight_ZeroBlendTime_SnapsToTarget()
            => Assert.That(PlayerAnimationDriver.StepFullBodyWeight(0f, true, true, 0.01f, 0f), Is.EqualTo(1f));
    }
}
