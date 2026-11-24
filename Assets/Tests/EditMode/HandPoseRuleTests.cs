using Game.Animations;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>spec-hand-pose-layer I/O matrix: which finger pose each hand shows.</summary>
    public class HandPoseRuleTests
    {
        private static (HandPose left, HandPose right) Resolve(bool inCombat, bool weaponInHand, bool leftHandWeapon = false,
            bool twoHanded = false, bool shieldInHand = false, bool switchingWeapon = false, bool down = false)
        {
            HandPoseRule.Resolve(inCombat, weaponInHand, leftHandWeapon, twoHanded, shieldInHand, switchingWeapon, down,
                out var left, out var right);
            return (left, right);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OutOfCombat_WeaponSheathed_BothRelaxed(bool twoHandedEquipped)
        {
            Assert.That(Resolve(false, false, twoHanded: twoHandedEquipped), Is.EqualTo((HandPose.Relaxed, HandPose.Relaxed)));
        }

        [Test]
        public void UnarmedFight_OrBrawlWithWeaponSheathed_BothFists()
        {
            Assert.That(Resolve(true, false), Is.EqualTo((HandPose.Fist, HandPose.Fist)));
        }

        [Test]
        public void OneHandWeaponDrawn_NoShield_RightGripLeftRelaxed()
        {
            Assert.That(Resolve(true, true), Is.EqualTo((HandPose.Relaxed, HandPose.OneHandGrip)));
        }

        [Test]
        public void SwordAndShieldDrawn_LeftShieldHold()
        {
            Assert.That(Resolve(true, true, shieldInHand: true), Is.EqualTo((HandPose.ShieldHold, HandPose.OneHandGrip)));
        }

        [Test]
        public void TwoHanderDrawn_BothTwoHandGrip()
        {
            Assert.That(Resolve(true, true, twoHanded: true), Is.EqualTo((HandPose.TwoHandGrip, HandPose.TwoHandGrip)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BowDrawn_LeftBowGripRightRelaxed(bool inCombat)
        {
            Assert.That(Resolve(inCombat, true, leftHandWeapon: true), Is.EqualTo((HandPose.BowGrip, HandPose.Relaxed)));
        }

        [Test]
        public void WeaponInHand_OutOfCombatFlag_StillGrips()
        {
            // The draw puts the weapon in the hand at WeaponSwap; the grip follows the weapon, not the combat flag.
            Assert.That(Resolve(false, true), Is.EqualTo((HandPose.Relaxed, HandPose.OneHandGrip)));
        }

        [Test]
        public void TwoHanderWithShieldFlag_ShieldIgnored()
        {
            // EquipmentRules keeps a two-hander / bow and a shield apart; the grip wins if both ever arrive.
            Assert.That(Resolve(true, true, twoHanded: true, shieldInHand: true), Is.EqualTo((HandPose.TwoHandGrip, HandPose.TwoHandGrip)));
            Assert.That(Resolve(true, true, leftHandWeapon: true, shieldInHand: true), Is.EqualTo((HandPose.BowGrip, HandPose.Relaxed)));
        }

        [Test]
        public void DrawOrSheathePlaying_EmptyHandsRelaxed_NotFists()
        {
            // IsInCombat flips at the draw press; the weapon reaches the hand only at WeaponSwap.
            Assert.That(Resolve(true, false, switchingWeapon: true), Is.EqualTo((HandPose.Relaxed, HandPose.Relaxed)));
            Assert.That(Resolve(true, true, twoHanded: true, switchingWeapon: true), Is.EqualTo((HandPose.TwoHandGrip, HandPose.TwoHandGrip)),
                "a weapon in hand keeps its grip through the sheathe");
        }

        [Test]
        public void Down_EmptyHandsRelaxed_WeaponStillGripped()
        {
            Assert.That(Resolve(true, false, down: true), Is.EqualTo((HandPose.Relaxed, HandPose.Relaxed)));
            Assert.That(Resolve(true, true, down: true), Is.EqualTo((HandPose.Relaxed, HandPose.OneHandGrip)));
        }

        [Test]
        public void HandPoseValues_AreTheAnimatorInts()
        {
            // The controller's AnyState conditions hold these ints (HandPoseBuilder): never renumber.
            Assert.That(new[] { (int)HandPose.Relaxed, (int)HandPose.Fist, (int)HandPose.OneHandGrip, (int)HandPose.TwoHandGrip,
                (int)HandPose.BowGrip, (int)HandPose.ShieldHold }, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
        }
    }
}
