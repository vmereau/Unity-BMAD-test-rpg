namespace Game.Animations
{
    /// <summary>
    /// Finger pose shown by Humanoid_Template's <c>LeftHand</c> / <c>RightHand</c> layers (int params <c>LeftHandPose</c> /
    /// <c>RightHandPose</c>). Values are the Animator ints: never renumber, append new poses at the end.
    /// </summary>
    public enum HandPose
    {
        Relaxed = 0,
        Fist = 1,
        OneHandGrip = 2,
        TwoHandGrip = 3,
        BowGrip = 4,
        ShieldHold = 5,
    }

    /// <summary>
    /// Pure rule picking each hand's finger pose (tested in HandPoseRuleTests). Same rule for the player and NPCs:
    /// a weapon in hand grips it (a left-hand weapon is a bow), a fight without one closes both fists, otherwise relaxed.
    /// Empty hands stay relaxed while a draw / sheathe plays (reaching for the hilt before the weapon reaches the hand) and
    /// while the body is down (knocked down, unconscious, dead, getting up).
    /// </summary>
    public static class HandPoseRule
    {
        /// <param name="inCombat">The Animator's <c>IsInCombat</c> (NPC brawls fight with the weapon sheathed).</param>
        /// <param name="weaponInHand">A weapon visual is drawn into a hand socket.</param>
        /// <param name="leftHandWeapon">The drawn weapon is held in the left hand (bow).</param>
        /// <param name="twoHanded">The drawn weapon is held with both hands.</param>
        /// <param name="shieldInHand">A shield is held in the offhand.</param>
        /// <param name="switchingWeapon">A draw / sheathe clip is playing (UpperBody <c>Draw_State</c> / <c>Sheathe_State</c>).</param>
        /// <param name="down">The Base Layer is in a knockdown / unconscious / death / get-up state.</param>
        public static void Resolve(bool inCombat, bool weaponInHand, bool leftHandWeapon, bool twoHanded, bool shieldInHand,
            bool switchingWeapon, bool down, out HandPose left, out HandPose right)
        {
            if (weaponInHand)
            {
                if (leftHandWeapon)
                {
                    left = HandPose.BowGrip;
                    right = HandPose.Relaxed;
                }
                else if (twoHanded)
                {
                    left = HandPose.TwoHandGrip;
                    right = HandPose.TwoHandGrip;
                }
                else
                {
                    left = shieldInHand ? HandPose.ShieldHold : HandPose.Relaxed;
                    right = HandPose.OneHandGrip;
                }
                return;
            }

            left = right = inCombat && !switchingWeapon && !down ? HandPose.Fist : HandPose.Relaxed;
        }
    }
}
