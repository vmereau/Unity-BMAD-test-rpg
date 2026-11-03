using System;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Forward step of one melee attack (spec-full-body-attacks-root-motion): the Mixamo attack clips are in place, so
    /// <c>PlayerAttackStep</c> pushes the player <see cref="distance"/> metres along its facing, at constant speed, from
    /// <see cref="startTime"/> to <see cref="startTime"/> + <see cref="duration"/> seconds after the attack state entered.
    /// Distance 0 = rooted swing.
    /// </summary>
    [Serializable]
    public struct AttackStep
    {
        [Tooltip("Metres travelled forward during the step window. 0 = the swing stays in place.")]
        [Min(0f)] public float distance;
        [Tooltip("Seconds after the attack state entered when the step starts (usually just before HitboxEnable).")]
        [Min(0f)] public float startTime;
        [Tooltip("Seconds the step lasts.")]
        [Min(0f)] public float duration;

        public AttackStep(float distance, float startTime, float duration)
        {
            this.distance = distance;
            this.startTime = startTime;
            this.duration = duration;
        }

        /// <summary>
        /// Metres to travel between <paramref name="fromTime"/> and <paramref name="toTime"/> (seconds since the attack state
        /// entered): the overlap with the step window × constant speed. 0 for an empty step or window.
        /// </summary>
        public float DisplacementBetween(float fromTime, float toTime)
        {
            if (distance <= 0f || duration <= 0f || toTime <= fromTime) return 0f;
            float overlap = Mathf.Min(toTime, startTime + duration) - Mathf.Max(fromTime, startTime);
            return overlap > 0f ? overlap * distance / duration : 0f;
        }

        /// <summary>
        /// Step for attack state <paramref name="attackIndex"/> (1 = Attack_1): <paramref name="steps"/>[index − 1] when present,
        /// else <paramref name="fallback"/>.
        /// </summary>
        public static AttackStep Resolve(AttackStep[] steps, int attackIndex, AttackStep fallback) =>
            steps != null && attackIndex >= 1 && attackIndex <= steps.Length ? steps[attackIndex - 1] : fallback;
    }
}
