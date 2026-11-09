using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>What an NPC does during a routine block.</summary>
    public enum NpcActivity
    {
        /// <summary>Stand at <see cref="NpcRoutineBlock.spot"/> (position + facing); empty spot = the spawn spot.</summary>
        Stand,
        /// <summary>Loop through <see cref="NpcRoutineBlock.waypoints"/>, waiting <c>PatrolWaitTime</c> at each.</summary>
        Patrol,
    }

    /// <summary>
    /// One routine block: from <see cref="startHour"/> (inclusive) to <see cref="endHour"/> (exclusive), wrapping midnight.
    /// </summary>
    [Serializable]
    public class NpcRoutineBlock
    {
        [Range(0, 23)] public int startHour;
        [Range(0, 23)] public int endHour;
        public NpcActivity activity;
        [Tooltip("Where the NPC stands (position + facing). Empty = its spawn spot. Patrol uses it only when it has no waypoints.")]
        public Transform spot;
        [Tooltip("Patrol: waypoints walked in order, looping. Empty = stands at the spot / spawn spot instead.")]
        public List<Transform> waypoints = new List<Transform>();
    }

    /// <summary>Pure helpers for <see cref="NpcRoutine"/>. Tested by <c>NpcRoutineScheduleTests</c>.</summary>
    public static class NpcRoutineSchedule
    {
        /// <summary>
        /// Index of the first block containing <paramref name="hour"/>, or -1. Start inclusive, end exclusive,
        /// wraps midnight (21 → 6); start == end covers the whole day.
        /// </summary>
        public static int FindBlock(IReadOnlyList<NpcRoutineBlock> blocks, int hour)
        {
            if (blocks == null) return -1;
            for (int i = 0; i < blocks.Count; i++)
            {
                NpcRoutineBlock b = blocks[i];
                if (b != null && Game.Core.GameTime.IsHourInWindow(b.startHour, b.endHour, hour)) return i;
            }
            return -1;
        }

        /// <summary>Index of the first all-day block (start == end), or -1. Used when no clock is loaded.</summary>
        public static int FindAllDayBlock(IReadOnlyList<NpcRoutineBlock> blocks)
        {
            if (blocks == null) return -1;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null && blocks[i].startHour == blocks[i].endHour) return i;
            return -1;
        }

        /// <summary>Index of the point closest to <paramref name="from"/> on the XZ plane, or -1 when empty.</summary>
        public static int NearestIndex(IReadOnlyList<Vector3> points, Vector3 from)
        {
            if (points == null) return -1;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].x - from.x;
                float dz = points[i].z - from.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }
    }
}
