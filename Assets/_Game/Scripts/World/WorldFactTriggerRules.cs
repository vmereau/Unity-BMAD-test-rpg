using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>Where a <see cref="WorldFactTrigger"/>'s required NPC must be for the NPC gate to pass.</summary>
    public enum NpcPlacementGate
    {
        /// <summary>Anywhere, as long as it is present, alive and conscious.</summary>
        Anywhere,
        /// <summary>Its <c>NpcRoutine</c>'s current block has the given label (passes while still walking there).</summary>
        InRoutineBlock,
        /// <summary>Within a flat (XZ) radius of a spot: the NPC is really there.</summary>
        NearSpot,
    }

    /// <summary>
    /// <b>Static, pure.</b> Gate decisions behind <see cref="WorldFactTrigger"/>'s optional hour window and required NPC.
    /// Tested by <c>WorldFactTriggerRulesTests</c>.
    /// </summary>
    public static class WorldFactTriggerRules
    {
        /// <summary>
        /// True when the hour gate is off, or a clock is loaded and <paramref name="hour"/> is in
        /// [<paramref name="startHour"/>, <paramref name="endHour"/>) (<see cref="GameTime.IsHourInWindow"/>: wraps midnight,
        /// start == end = all day). A gated trigger without a clock never passes.
        /// </summary>
        public static bool HourGatePasses(bool useHours, bool hasClock, int hour, int startHour, int endHour) =>
            !useHours || (hasClock && GameTime.IsHourInWindow(startHour, endHour, hour));

        /// <summary>True when the required NPC is present, neither dead nor unconscious, and where its placement gate wants it.</summary>
        public static bool NpcGatePasses(bool present, bool isDead, bool isUnconscious, bool placementOk) =>
            present && !isDead && !isUnconscious && placementOk;

        /// <summary>True when <paramref name="requiredLabel"/> is non-empty and equals <paramref name="currentLabel"/> (ordinal).</summary>
        public static bool IsInBlock(string currentLabel, string requiredLabel) =>
            !string.IsNullOrEmpty(requiredLabel) && string.Equals(currentLabel, requiredLabel, System.StringComparison.Ordinal);

        /// <summary>True when <paramref name="npcPosition"/> is within <paramref name="radius"/> of <paramref name="spotPosition"/> on the XZ plane (inclusive).</summary>
        public static bool IsNearSpot(Vector3 npcPosition, Vector3 spotPosition, float radius)
        {
            float dx = npcPosition.x - spotPosition.x;
            float dz = npcPosition.z - spotPosition.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
