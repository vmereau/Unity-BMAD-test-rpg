using System.Collections.Generic;
using Game.AI;
using NUnit.Framework;

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
    }
}
