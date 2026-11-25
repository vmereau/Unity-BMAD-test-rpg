using Game.Inventory;

namespace Game.Animations
{
    /// <summary>
    /// Weight of the <c>LeftArm</c> layer (spec-sword-shield-take-combo): 1 while the off-hand item asks for an arm pose in
    /// idle and locomotion; 0 in attacks and full-body states (the Base <c>Attack</c> / <c>FullBody</c> tags), in UpperBody
    /// actions (block, draw / sheathe, drink, bow aim — the <c>UpperBodyAction</c> tag) and while down. Every animated action
    /// plays the clip's own left arm: the sword-and-shield attacks swing it out of the blade's path, which no fixed pose
    /// does (a held torch came within 2–6 cm of the blade), and the drink clip lifts the potion with it. Pure — tested.
    /// </summary>
    public static class OffhandArmRule
    {
        public const string LAYER = "LeftArm";
        public const float BLEND_TIME = 0.15f; // ≈ the hand layers' 0.12 s, the UpperBody layer's 0.15 s

        public static float Target(OffhandArmPose pose, bool baseIsFullBody, bool upperIsAction, bool baseIsDown) =>
            pose != OffhandArmPose.None && !baseIsFullBody && !upperIsAction && !baseIsDown ? 1f : 0f;

        /// <summary>Left-hand finger pose while the off-hand item is a held one: a torch is gripped in a fist, attacks included.</summary>
        public static HandPose LeftHand(OffhandArmPose pose, HandPose resolved) =>
            pose == OffhandArmPose.TorchHold ? HandPose.Fist : resolved;
    }
}
