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

        /// <summary>
        /// Whether this frame's blended root delta may move the player. Outside an attack → attack crossfade: always.
        /// Inside one, <c>Animator.deltaPosition</c> mixes both clips: when the outgoing attack was also moved by its
        /// clip (<paramref name="outgoingClipDriven"/>, the unarmed combo) the mix is the continuous travel of the chain
        /// and is applied; when it was moved by the scripted step, its clip's own travel was never meant to reach the
        /// player (weapon clips, unbaked lunges) and the blend is waited out.
        /// </summary>
        public static bool AppliesDuringBlend(bool inAttackToAttackBlend, bool outgoingClipDriven)
            => !inAttackToAttackBlend || outgoingClipDriven;
    }
}
