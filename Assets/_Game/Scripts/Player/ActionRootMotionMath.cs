using UnityEngine;

namespace Game.Player
{
    /// <summary>Pure filter of <see cref="ActionRootMotion"/> (EditMode tested).</summary>
    public static class ActionRootMotionMath
    {
        /// <summary>
        /// World displacement to apply for one frame of clip root motion: horizontal part only (height stays with gravity
        /// and the CharacterController), × <paramref name="scale"/>. A scale ≤ 0 means "not root-motion driven" → zero.
        /// </summary>
        public static Vector3 Filter(Vector3 animatorDelta, float scale)
        {
            if (scale <= 0f) return Vector3.zero;
            return new Vector3(animatorDelta.x * scale, 0f, animatorDelta.z * scale);
        }
    }
}
