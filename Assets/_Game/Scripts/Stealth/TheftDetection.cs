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

        /// <summary>The thief is caught: close enough on the flat, on about the same level (a floor above / below doesn't
        /// count, a stair step does), in line of sight (not through a wall) and no menu / dialogue open (cursor locked).</summary>
        public static bool CanCatch(float flatDistance, float catchDistance, float heightGap, float maxHeightGap,
            bool hasLineOfSight, bool cursorLocked) =>
            cursorLocked && hasLineOfSight && flatDistance <= catchDistance &&
            (heightGap < 0f ? -heightGap : heightGap) <= maxHeightGap;

        /// <summary>Next value of a chasing witness's give-up timer (compared with the lose-sight time). Sensing the thief
        /// (seen or heard) while still making headway resets it; walking on without sensing them holds it; once the
        /// witness stands still — at the last sensed spot, at the end of a partial path, blocked, or stopped under a thief
        /// it can't catch (floor above, ledge) — it runs, sensed or not.</summary>
        public static float NextNoSightTime(float current, float deltaTime, bool sensed, bool progressing) =>
            progressing ? (sensed ? 0f : current) : current + deltaTime;

        /// <summary>A trespass chase ends once its area no longer restricts the target: the target left every box, or
        /// the area stopped being restricted (hours / grace). Theft chases and chases without an area never stop here.</summary>
        public static bool ShouldStopTrespassChase(bool isTrespass, bool hasArea, bool areaRestrictedNow, bool targetInside) =>
            isTrespass && hasArea && (!areaRestrictedNow || !targetInside);
    }
}
