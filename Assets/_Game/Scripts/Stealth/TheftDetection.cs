namespace Game.Stealth
{
    /// <summary>
    /// Pure theft-witness and chase rules used by <c>EntityPerception.CanSeeTheft</c> and the brain's Pursuing state.
    /// No Unity object access, unit-tested in <c>TheftDetectionTests</c>. Line of sight is the caller's.
    /// </summary>
    public static class TheftDetection
    {
        /// <summary>Theft sight range, shortened like the hostile sight range while the thief sneaks.</summary>
        public static float EffectiveTheftRange(float theftSightRange, bool sneaking, float sneakSightMultiplier) =>
            StealthDetection.EffectiveSightRange(theftSightRange, sneaking, sneakSightMultiplier);

        /// <summary>Thief inside the view cone within <paramref name="sightRange"/>, or inside the proximity radius (geometry only).</summary>
        public static bool IsInView(float distance, float angleDeg, float sightRange, float viewAngle, float proximityRadius) =>
            StealthDetection.ComputeVisibility(distance, angleDeg, sightRange, viewAngle, proximityRadius, 1f, 1f) > 0f;

        /// <summary>A chasing witness gives up: out of sight too long, thief too far, or chase too long.</summary>
        public static bool ShouldGiveUp(float timeWithoutSight, float loseSightTime, float distance, float maxDistance,
            float elapsed, float maxDuration) =>
            timeWithoutSight >= loseSightTime || distance > maxDistance || elapsed >= maxDuration;

        /// <summary>The thief is caught: close enough and no menu / dialogue open (cursor locked).</summary>
        public static bool CanCatch(float distance, float catchDistance, bool cursorLocked) =>
            cursorLocked && distance <= catchDistance;
    }
}
