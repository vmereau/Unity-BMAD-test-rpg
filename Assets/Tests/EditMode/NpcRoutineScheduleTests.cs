using System.Collections.Generic;
using Game.AI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure NpcRoutineSchedule block lookup used by NpcRoutine.
    /// </summary>
    public class NpcRoutineScheduleTests
    {
        private static NpcRoutineBlock Block(int start, int end) =>
            new NpcRoutineBlock { startHour = start, endHour = end };

        private static readonly List<NpcRoutineBlock> DayNight = new List<NpcRoutineBlock> { Block(6, 21), Block(21, 6) };

        [Test]
        public void NormalBlock_StartInclusive_EndExclusive()
        {
            Assert.AreEqual(0, NpcRoutineSchedule.FindBlock(DayNight, 6));
            Assert.AreEqual(0, NpcRoutineSchedule.FindBlock(DayNight, 14));
            Assert.AreEqual(0, NpcRoutineSchedule.FindBlock(DayNight, 20));
            Assert.AreEqual(1, NpcRoutineSchedule.FindBlock(DayNight, 21));
        }

        [Test]
        public void WrapBlock_CoversMidnight()
        {
            Assert.AreEqual(1, NpcRoutineSchedule.FindBlock(DayNight, 23));
            Assert.AreEqual(1, NpcRoutineSchedule.FindBlock(DayNight, 0));
            Assert.AreEqual(1, NpcRoutineSchedule.FindBlock(DayNight, 5));
        }

        [Test]
        public void Gap_ReturnsMinusOne()
        {
            var blocks = new List<NpcRoutineBlock> { Block(8, 12) };
            Assert.AreEqual(-1, NpcRoutineSchedule.FindBlock(blocks, 7));
            Assert.AreEqual(-1, NpcRoutineSchedule.FindBlock(blocks, 12));
        }

        [Test]
        public void Overlap_FirstListedWins()
        {
            var blocks = new List<NpcRoutineBlock> { Block(10, 14), Block(8, 20) };
            Assert.AreEqual(0, NpcRoutineSchedule.FindBlock(blocks, 12));
            Assert.AreEqual(1, NpcRoutineSchedule.FindBlock(blocks, 9));
        }

        [Test]
        public void StartEqualsEnd_CoversWholeDay()
        {
            var blocks = new List<NpcRoutineBlock> { Block(5, 5) };
            for (int h = 0; h < 24; h++)
                Assert.AreEqual(0, NpcRoutineSchedule.FindBlock(blocks, h), $"hour {h}");
        }

        [Test]
        public void EmptyOrNullList_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, NpcRoutineSchedule.FindBlock(new List<NpcRoutineBlock>(), 10));
            Assert.AreEqual(-1, NpcRoutineSchedule.FindBlock(null, 10));
        }

        [Test]
        public void FindAllDayBlock_ReturnsFirstStartEqualsEnd()
        {
            var blocks = new List<NpcRoutineBlock> { Block(6, 21), null, Block(0, 0), Block(3, 3) };
            Assert.AreEqual(2, NpcRoutineSchedule.FindAllDayBlock(blocks));
        }

        [Test]
        public void FindAllDayBlock_NoneOrNull_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, NpcRoutineSchedule.FindAllDayBlock(DayNight));
            Assert.AreEqual(-1, NpcRoutineSchedule.FindAllDayBlock(null));
        }

        [Test]
        public void NearestIndex_UsesFlatDistance()
        {
            var points = new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(1f, 50f, 1f), new Vector3(-3f, 0f, 0f) };
            // Point 1 is far vertically but closest on XZ.
            Assert.AreEqual(1, NpcRoutineSchedule.NearestIndex(points, Vector3.zero));
            Assert.AreEqual(0, NpcRoutineSchedule.NearestIndex(points, new Vector3(9f, 0f, 0f)));
        }

        [Test]
        public void NearestIndex_EmptyOrNull_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, NpcRoutineSchedule.NearestIndex(new List<Vector3>(), Vector3.zero));
            Assert.AreEqual(-1, NpcRoutineSchedule.NearestIndex(null, Vector3.zero));
        }
    }
}
