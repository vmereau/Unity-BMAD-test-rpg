using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.AI
{
    /// <summary>What an NPC does during a routine block. Only <see cref="Stand"/> exists so far.</summary>
    public enum NpcActivity { Stand }

    /// <summary>
    /// One routine block: from <see cref="startHour"/> (inclusive) to <see cref="endHour"/> (exclusive), wrapping midnight.
    /// <see cref="spot"/> null = home behaviour (spawn spot / authored waypoints).
    /// </summary>
    [Serializable]
    public class NpcRoutineBlock
    {
        [Range(0, 23)] public int startHour;
        [Range(0, 23)] public int endHour;
        public NpcActivity activity;
        [Tooltip("Where the NPC stands (position + facing). Empty = home behaviour (spawn spot or patrol waypoints).")]
        public Transform spot;
    }

    /// <summary>Pure block lookup for <see cref="NpcRoutine"/>. Tested by <c>NpcRoutineScheduleTests</c>.</summary>
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
                if (b != null && Contains(b.startHour, b.endHour, hour)) return i;
            }
            return -1;
        }

        private static bool Contains(int start, int end, int hour)
        {
            if (start == end) return true;
            return start < end ? hour >= start && hour < end : hour >= start || hour < end;
        }
    }
}
