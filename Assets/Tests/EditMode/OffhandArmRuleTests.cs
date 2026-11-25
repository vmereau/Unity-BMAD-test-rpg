using Game.Animations;
using Game.Inventory;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary><see cref="OffhandArmRule"/> — weight of the LeftArm layer and the torch fist (spec-sword-shield-take-combo).</summary>
    public class OffhandArmRuleTests
    {
        [Test]
        public void Target_NoPose_IsZero() =>
            Assert.That(OffhandArmRule.Target(OffhandArmPose.None, false, false, false), Is.EqualTo(0f));

        [Test]
        public void Target_TorchInIdleOrLocomotion_IsOne() =>
            Assert.That(OffhandArmRule.Target(OffhandArmPose.TorchHold, false, false, false), Is.EqualTo(1f));

        [Test]
        public void Target_TorchInAttackOrFullBodyState_IsZero() =>
            Assert.That(OffhandArmRule.Target(OffhandArmPose.TorchHold, true, false, false), Is.EqualTo(0f));

        [Test]
        public void Target_TorchInUpperBodyAction_IsZero() =>
            Assert.That(OffhandArmRule.Target(OffhandArmPose.TorchHold, false, true, false), Is.EqualTo(0f),
                "block / draw / sheathe / drink play the clip's left arm");

        [Test]
        public void Target_TorchWhileDown_IsZero() =>
            Assert.That(OffhandArmRule.Target(OffhandArmPose.TorchHold, false, false, true), Is.EqualTo(0f));

        [Test]
        public void LeftHand_Torch_IsFist() =>
            Assert.That(OffhandArmRule.LeftHand(OffhandArmPose.TorchHold, HandPose.ShieldHold), Is.EqualTo(HandPose.Fist));

        [Test]
        public void LeftHand_NoPose_KeepsResolvedPose() =>
            Assert.That(OffhandArmRule.LeftHand(OffhandArmPose.None, HandPose.ShieldHold), Is.EqualTo(HandPose.ShieldHold));
    }
}
