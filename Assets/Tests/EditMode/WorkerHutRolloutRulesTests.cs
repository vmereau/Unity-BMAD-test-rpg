using Game.Editor.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class WorkerHutRolloutRulesTests
    {
        private static WorkerHutRolloutRules.Plot Plot(string name = "worker-hut-04", float rise = 0f, float drop = 0f,
            bool defaultDoor = true, bool mirroredDoor = true, bool crowded = false, bool longFits = false) =>
            new WorkerHutRolloutRules.Plot(name, rise, drop, defaultDoor, mirroredDoor, crowded, longFits);

        [Test]
        public void Hash_IsStableAcrossRuns()
        {
            // FNV-1a over the text's characters: fixed values, so a rerun on another machine makes the same choice.
            Assert.AreEqual(2166136261u, WorkerHutRolloutRules.Hash(""));
            Assert.AreEqual(0xE40C292Cu, WorkerHutRolloutRules.Hash("a"));
            Assert.AreNotEqual(WorkerHutRolloutRules.Hash("worker-hut-04"), WorkerHutRolloutRules.Hash("worker-hut-05"));
        }

        [Test]
        public void PlainPlot_OffersTheThreeHomeShells_InAnOrderSetByTheName()
        {
            string[] first = WorkerHutRolloutRules.Candidates(Plot(), out string reason);
            string[] again = WorkerHutRolloutRules.Candidates(Plot(), out _);
            Assert.IsNull(reason);
            CollectionAssert.AreEqual(first, again);
            CollectionAssert.AreEquivalent(
                new[] { WorkerHutRolloutRules.HOME, WorkerHutRolloutRules.HOME_B, WorkerHutRolloutRules.HOME_MIRRORED }, first);
        }

        [Test]
        public void PlainPlot_StartsFromTheShellItsNameHashPicks()
        {
            string[] pool = { WorkerHutRolloutRules.HOME, WorkerHutRolloutRules.HOME_B, WorkerHutRolloutRules.HOME_MIRRORED };
            var starts = new System.Collections.Generic.HashSet<string>();
            foreach (string name in new[] { "worker-hut-04", "worker-hut-05", "worker-hut-06", "worker-hut-08", "worker-hut-09", "worker-hut-10" })
            {
                int start = (int)(WorkerHutRolloutRules.Hash(name) % 3u);
                string[] candidates = WorkerHutRolloutRules.Candidates(Plot(name), out _);
                CollectionAssert.AreEqual(new[] { pool[start], pool[(start + 1) % 3], pool[(start + 2) % 3] }, candidates, name);
                starts.Add(candidates[0]);
            }
            Assert.Greater(starts.Count, 1, "six names should not all start from the same shell");
        }

        [Test]
        public void CrowdedPlot_OffersOnlyTightShells()
        {
            string[] candidates = WorkerHutRolloutRules.Candidates(Plot(crowded: true, longFits: true), out _);
            CollectionAssert.AreEquivalent(new[]
            {
                WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.TIGHT_MIRRORED,
                WorkerHutRolloutRules.TIGHT_C, WorkerHutRolloutRules.TIGHT_MIRRORED_D,
            }, candidates);
        }

        [Test]
        public void BlockedDefaultDoorway_OffersOnlyMirroredShells()
        {
            CollectionAssert.AreEqual(new[] { WorkerHutRolloutRules.HOME_MIRRORED },
                WorkerHutRolloutRules.Candidates(Plot(defaultDoor: false), out _));
            CollectionAssert.AreEquivalent(new[] { WorkerHutRolloutRules.TIGHT_MIRRORED, WorkerHutRolloutRules.TIGHT_MIRRORED_D },
                WorkerHutRolloutRules.Candidates(Plot(defaultDoor: false, crowded: true), out _));
        }

        [Test]
        public void BlockedMirroredDoorway_OffersOnlyDefaultShells()
        {
            CollectionAssert.AreEquivalent(new[] { WorkerHutRolloutRules.HOME, WorkerHutRolloutRules.HOME_B },
                WorkerHutRolloutRules.Candidates(Plot(mirroredDoor: false), out _));
        }

        [Test]
        public void BothDoorwaysBlocked_Skips()
        {
            string[] candidates = WorkerHutRolloutRules.Candidates(Plot(defaultDoor: false, mirroredDoor: false), out string reason);
            Assert.IsEmpty(candidates);
            StringAssert.Contains("doorway", reason);
        }

        [Test]
        public void Bank_SkipsOverTheLimits_AndPassesOnThem()
        {
            Assert.IsEmpty(WorkerHutRolloutRules.Candidates(Plot(rise: 0.16f), out string rise));
            StringAssert.Contains("bank", rise);
            Assert.IsEmpty(WorkerHutRolloutRules.Candidates(Plot(drop: 0.31f), out _));
            Assert.IsNotEmpty(WorkerHutRolloutRules.Candidates(
                Plot(rise: WorkerHutRolloutRules.MAX_RISE, drop: WorkerHutRolloutRules.MAX_DROP), out _));
        }

        [Test]
        public void LongHut_OnlyWhereItFits_WithTheDefaultDoorway()
        {
            CollectionAssert.AreEqual(new[] { WorkerHutRolloutRules.LONG },
                WorkerHutRolloutRules.Candidates(Plot(longFits: true), out _));
            CollectionAssert.AreEqual(new[] { WorkerHutRolloutRules.HOME_MIRRORED },
                WorkerHutRolloutRules.Candidates(Plot(longFits: true, defaultDoor: false), out _));
        }

        [Test]
        public void Pick_TakesTheShellTheFewestNeighboursShow()
        {
            string[] candidates = { WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.TIGHT_MIRRORED };
            Assert.AreEqual(WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.Pick(candidates, new string[0]));
            Assert.AreEqual(WorkerHutRolloutRules.TIGHT_MIRRORED,
                WorkerHutRolloutRules.Pick(candidates, new[] { WorkerHutRolloutRules.TIGHT }));
            Assert.AreEqual(WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.Pick(candidates,
                new[] { WorkerHutRolloutRules.TIGHT, WorkerHutRolloutRules.TIGHT_MIRRORED, WorkerHutRolloutRules.TIGHT_MIRRORED }));
            Assert.IsNull(WorkerHutRolloutRules.Pick(new string[0], new string[0]));
        }
    }
}
