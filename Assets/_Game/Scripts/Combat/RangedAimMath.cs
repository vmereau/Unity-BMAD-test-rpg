using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure NPC bow aiming (spec-ai-ranged-combat-style): where to aim at a moving target. Gravity is ignored — at NPC
    /// ranges (≤ 15 m) the arrow flies straight for its gravity delay.
    /// </summary>
    public static class RangedAimMath
    {
        /// <summary>
        /// Aim point ahead of a moving target: <paramref name="targetPoint"/> + the target's flat velocity × the arrow's flight
        /// time to it × <paramref name="leadFactor"/> (0 = no lead, 1 = full intercept estimate). Vertical velocity is
        /// ignored (jumps, stairs). Speed ≤ 0 → no lead.
        /// </summary>
        public static Vector3 LeadPoint(Vector3 origin, Vector3 targetPoint, Vector3 targetVelocity, float projectileSpeed,
            float leadFactor)
        {
            if (projectileSpeed <= 0f || leadFactor <= 0f) return targetPoint;
            Vector3 flat = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
            float flightTime = Vector3.Distance(origin, targetPoint) / projectileSpeed;
            return targetPoint + flat * (flightTime * Mathf.Clamp01(leadFactor));
        }

        /// <summary>
        /// Velocity from two positions <paramref name="dt"/> apart, exponentially smoothed with time constant
        /// <paramref name="smoothTime"/> (frame-rate independent; ≤ 0 = the raw measurement). dt ≤ 0 → unchanged.
        /// </summary>
        public static Vector3 SmoothVelocity(Vector3 current, Vector3 previousPosition, Vector3 position, float dt,
            float smoothTime)
        {
            if (dt <= 0f) return current;
            Vector3 measured = (position - previousPosition) / dt;
            if (smoothTime <= 0f) return measured;
            return Vector3.Lerp(current, measured, 1f - Mathf.Exp(-dt / smoothTime));
        }
    }
}
