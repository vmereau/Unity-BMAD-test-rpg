using System.Collections.Generic;
using Game.AI;
using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The owned huts' table (spec-area-1-home-yard-huts-and-yard): beds, boxes, doorways, routines.</summary>
    public class HomeYardHomesTests
    {
        [Test]
        public void EveryHome_PassesItsOwnCheck()
        {
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
                Assert.IsNull(HomeYardHomes.Problem(home), home.Key);
        }

        [Test]
        public void EveryOwner_HasOneBed_AndEveryHutIsUsedOnce()
        {
            var huts = new HashSet<string>();
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
            {
                Assert.IsTrue(huts.Add(home.Hut), $"{home.Hut} is given twice");
                var owners = new HashSet<string>(home.Owners);
                foreach (HomeYardHomes.BedSpot bed in home.Beds) Assert.IsTrue(owners.Remove(bed.Owner), $"{home.Key}: {bed.NameTag}");
                Assert.IsEmpty(owners, $"{home.Key}: an owner without a bed");
            }
        }

        [Test]
        public void Beds_LieAlongASideWall_OffTheDoorwaysHalf()
        {
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
                foreach (HomeYardHomes.BedSpot bed in home.Beds)
                {
                    Rect r = HomeYardHomes.BedRect(bed);
                    Assert.Greater(Mathf.Max(Mathf.Abs(r.xMin), Mathf.Abs(r.xMax)), HomeYardHomes.ROOM_HALF - 0.05f,
                        $"{bed.NameTag} stands away from the wall");
                    Assert.IsFalse(r.Overlaps(HomeYardHomes.EntryRect(home)), $"{bed.NameTag} reaches the doorway");
                }
        }

        [Test]
        public void Problem_ReportsABedInTheDoorway_WithoutThrowing()
        {
            var home = new HomeYardHomes.Home("Test", "worker-hut-99", false, new[] { "a" },
                new[] { new HomeYardHomes.BedSpot("Bed", null, "a", "Bed", new Vector3(0.5f, 0f, 0.3f)) },
                "Box", "Box", new Vector3(-0.8f, 0f, -0.8f), "x", new (string, int)[0]);
            StringAssert.Contains("doorway", HomeYardHomes.Problem(home));
        }

        [Test]
        public void Problem_ReportsABoxThroughTheWall_AndOverlaps()
        {
            var outside = new HomeYardHomes.Home("Test", "h", true, new[] { "a" },
                new[] { new HomeYardHomes.BedSpot("Bed", null, "a", "Bed", new Vector3(0.89f, 0f, -0.26f)) },
                "Box", "Box", new Vector3(-1.4f, 0f, 0f), "x", new (string, int)[0]);
            StringAssert.Contains("walls", HomeYardHomes.Problem(outside));
            var overlap = new HomeYardHomes.Home("Test", "h", true, new[] { "a" },
                new[] { new HomeYardHomes.BedSpot("Bed", null, "a", "Bed", new Vector3(0.89f, 0f, -0.26f)) },
                "Box", "Box", new Vector3(0.89f, 0f, 0f), "x", new (string, int)[0]);
            StringAssert.Contains("overlaps", HomeYardHomes.Problem(overlap));
        }

        [Test]
        public void Problem_RefusesAHomeWithoutOwners_OrABedNamedTwice()
        {
            Assert.IsNotNull(HomeYardHomes.Problem(null));
            var noOwner = new HomeYardHomes.Home("Test", "h", false, new string[0], new HomeYardHomes.BedSpot[0],
                "Box", "Box", Vector3.zero, "x", new (string, int)[0]);
            Assert.IsNotNull(HomeYardHomes.Problem(noOwner));
            var both = new HomeYardHomes.Home("Test", "h", false, new[] { "a" },
                new[] { new HomeYardHomes.BedSpot("Bed", "Some/Path", "a", "Bed", new Vector3(-0.89f, 0f, -0.26f)) },
                "Box", "Box", new Vector3(-0.55f, 0f, 1.02f), "x", new (string, int)[0]);
            Assert.IsNotNull(HomeYardHomes.Problem(both));
        }

        [Test]
        public void Routines_CoverEveryHour_AndSleepInATableBed()
        {
            var beds = new HashSet<string>();
            foreach (HomeYardHomes.Home home in HomeYardHomes.All)
                foreach (HomeYardHomes.BedSpot bed in home.Beds)
                    if (bed.Name != null) beds.Add(bed.Name);
            foreach ((string npc, HomeYardHomes.Block[] blocks) in HomeYardHomes.Routines)
            {
                Assert.IsTrue(HomeYardHomes.CoversTheDay(blocks), npc);
                bool sleeps = false;
                foreach (HomeYardHomes.Block block in blocks)
                    if (block.Activity == NpcActivity.Sleep)
                    {
                        sleeps = true;
                        Assert.IsTrue(beds.Contains(block.Bed), $"{npc} sleeps in {block.Bed}");
                    }
                Assert.IsTrue(sleeps, $"{npc} never sleeps");
            }
        }

        [Test]
        public void CoversTheDay_FindsAGap()
        {
            Assert.IsFalse(HomeYardHomes.CoversTheDay(new[]
            {
                new HomeYardHomes.Block("Work", 6, 21, NpcActivity.Stand), new HomeYardHomes.Block("Sleep", 23, 5, NpcActivity.Sleep),
            }));
        }

        [Test]
        public void BoxSaveKeys_AreUnique()
        {
            var keys = new HashSet<string>();
            foreach (HomeYardHomes.Home home in HomeYardHomes.All) Assert.IsTrue(keys.Add(home.BoxSaveKey), home.BoxSaveKey);
        }
    }
}
