using Game.Animations;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class UpperBodyWeightTests
    {
        private const float TOLERANCE = 1e-5f;

        [Test]
        public void Target_FullBodyBaseState_IsZero_EvenInCombatOrDuringAnUpperAction()
            => Assert.That(UpperBodyWeight.Target(baseIsFullBody: true, upperIsAction: true, isInCombat: true), Is.EqualTo(0f));

        [Test]
        public void Target_InCombat_IsOne()
            => Assert.That(UpperBodyWeight.Target(baseIsFullBody: false, upperIsAction: false, isInCombat: true), Is.EqualTo(1f));

        [Test]
        public void Target_UpperActionOutOfCombat_IsOne() // sheathe tail after IsInCombat cleared
            => Assert.That(UpperBodyWeight.Target(baseIsFullBody: false, upperIsAction: true, isInCombat: false), Is.EqualTo(1f));

        [Test]
        public void Target_OutOfCombat_IsZero()
            => Assert.That(UpperBodyWeight.Target(baseIsFullBody: false, upperIsAction: false, isInCombat: false), Is.EqualTo(0f));

        [Test]
        public void Step_MovesTowardTarget_ByDeltaOverBlendTime()
            => Assert.That(UpperBodyWeight.Step(0f, 1f, 0.05f, 0.15f), Is.EqualTo(1f / 3f).Within(TOLERANCE));

        [Test]
        public void Step_FallsTowardZero()
            => Assert.That(UpperBodyWeight.Step(1f, 0f, 0.05f, 0.15f), Is.EqualTo(2f / 3f).Within(TOLERANCE));

        [Test]
        public void Step_ReachesTargetAfterTheBlendTime()
        {
            float w = 0f;
            for (int i = 0; i < 4; i++) w = UpperBodyWeight.Step(w, 1f, 0.05f, 0.15f);
            Assert.That(w, Is.EqualTo(1f));
        }

        [Test]
        public void Step_ZeroBlendTime_SnapsToTarget()
            => Assert.That(UpperBodyWeight.Step(0f, 1f, 0.01f, 0f), Is.EqualTo(1f));
    }
}
