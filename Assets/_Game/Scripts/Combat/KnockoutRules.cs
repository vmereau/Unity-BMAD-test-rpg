using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure rules of the unconscious state (spec-unconscious-knockout). A blow that would kill is a knockout when the
    /// attacker and the victim are both humanoids (the player or an NPC) and their factions are not hostile. The victim
    /// then regains health linearly up to a fraction of its max while it lies on the ground.
    /// </summary>
    public static class KnockoutRules
    {
        /// <summary>True when a lethal blow knocks the victim out instead of killing it.</summary>
        public static bool IsNonLethal(bool attackerHumanoid, bool victimHumanoid, bool hostile) =>
            attackerHumanoid && victimHumanoid && !hostile;

        /// <summary>
        /// Health of an unconscious character <paramref name="elapsed"/> seconds after the knockout: 0 → max × fraction
        /// over <paramref name="duration"/>, clamped. A non-positive duration returns the full recovery at once.
        /// </summary>
        public static float RecoveryHealth(float maxHealth, float fraction, float elapsed, float duration)
        {
            float target = Mathf.Max(0f, maxHealth) * Mathf.Clamp01(fraction);
            if (duration <= 0f) return target;
            return target * Mathf.Clamp01(elapsed / duration);
        }
    }
}
