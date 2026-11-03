using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Pure weight rule for Humanoid_Template's masked <c>UpperBody</c> layer (tested in UpperBodyWeightTests).
    /// A full-body Base state (attack, dodge, jump, reaction, death) owns the whole body; otherwise an upper-body action
    /// (block, draw / sheathe, bow aim) or the combat carry pose (<c>CombatIdle</c>) shows over Base locomotion.
    /// </summary>
    public static class UpperBodyWeight
    {
        /// <summary>Base Layer state tags that play on the whole body (UpperBody weight 0).</summary>
        public const string ATTACK_TAG = "Attack";
        public const string FULL_BODY_TAG = "FullBody";
        /// <summary>UpperBody layer state tag for actions shown over locomotion even out of combat (sheathe tail).</summary>
        public const string UPPER_BODY_ACTION_TAG = "UpperBodyAction";

        public static float Target(bool baseIsFullBody, bool upperIsAction, bool isInCombat)
        {
            if (baseIsFullBody) return 0f;
            return upperIsAction || isInCombat ? 1f : 0f;
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/>; a full 0↔1 fade takes <paramref name="blendTime"/> s.</summary>
        public static float Step(float current, float target, float deltaTime, float blendTime)
        {
            if (blendTime <= 0f) return target;
            return Mathf.MoveTowards(current, target, deltaTime / blendTime);
        }
    }
}
