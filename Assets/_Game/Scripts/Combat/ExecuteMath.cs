using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure execute-stomp geometry (spec-execute-unconscious-npc): which target is in reach, and where the player must
    /// stand so the clip's landing foot hits it. Tested by <c>ExecuteTests</c>.
    /// </summary>
    public static class ExecuteMath
    {
        /// <summary>
        /// True when <paramref name="target"/> is within <paramref name="range"/> on the ground plane and inside the
        /// forward cone (<paramref name="halfAngle"/> degrees). A target right under the player always counts.
        /// </summary>
        public static bool IsCandidate(Vector3 playerPos, Vector3 playerForward, Vector3 target, float range, float halfAngle)
        {
            Vector3 to = Flat(target - playerPos);
            float sqr = to.sqrMagnitude;
            if (sqr > range * range) return false;
            if (sqr < 0.0001f) return true;
            Vector3 fwd = Flat(playerForward);
            if (fwd.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(fwd, to) <= halfAngle;
        }

        /// <summary>
        /// Stance that puts the player-local <paramref name="footOffset"/> (landing point of the stomp) on
        /// <paramref name="target"/>, keeping the player's approach bearing (the player's facing when standing on the
        /// target). Returns the ground position (player Y kept) and the yaw in degrees.
        /// </summary>
        public static (Vector3 position, float yaw) AlignStance(Vector3 playerPos, Vector3 playerForward, Vector3 target,
            Vector3 footOffset)
        {
            Vector3 to = Flat(target - playerPos);
            if (to.sqrMagnitude < 0.0001f) to = Flat(playerForward);
            float approach = to.sqrMagnitude > 0.0001f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;
            Vector3 flatOffset = Flat(footOffset);
            float offsetBearing = flatOffset.sqrMagnitude > 0.0001f ? Mathf.Atan2(flatOffset.x, flatOffset.z) * Mathf.Rad2Deg : 0f;
            float yaw = approach - offsetBearing;
            Vector3 world = Quaternion.Euler(0f, yaw, 0f) * flatOffset;
            Vector3 pos = new Vector3(target.x - world.x, playerPos.y, target.z - world.z);
            return (pos, yaw);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
