using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    public class Area1HandEditsTests
    {
        private static Area1Pose Pose(float x = 0f, float y = 0f, float z = 0f) => new Area1Pose(new Vector3(x, y, z), Quaternion.identity, Vector3.one);

        private static Dictionary<string, Area1Pose> Scene(params (string path, Area1Pose pose)[] objects) => objects.ToDictionary(o => o.path, o => o.pose);

        [Test]
        public void Diff_MovedChild_IsSavableAtItsLivePose()
        {
            Area1EditRow row = Area1HandEdits.Diff(Scene(("GuardPost/Lockup", Pose(1f))), Scene(("GuardPost/Lockup", Pose(2f, 0f, 3f)))).Single();

            Assert.AreEqual(Area1EditKind.Moved, row.Kind);
            Assert.IsFalse(row.HeightOnly);
            Assert.AreEqual(new Vector3(2f, 0f, 3f), row.Pose.Position);
        }

        [Test]
        public void Diff_HeightChange_IsFlaggedHeightOnly()
        {
            Area1EditRow row = Area1HandEdits.Diff(Scene(("GuardPost", Pose(1f, 6f))), Scene(("GuardPost", Pose(1f, 3f)))).Single();

            Assert.IsTrue(row.HeightOnly);
        }

        [Test]
        public void Diff_HeightAndRotation_IsNotHeightOnly()
        {
            var turned = new Area1Pose(new Vector3(0f, 3f, 0f), Quaternion.Euler(0f, 40f, 0f), Vector3.one);

            Area1EditRow row = Area1HandEdits.Diff(Scene(("A", Pose())), Scene(("A", turned))).Single();

            Assert.IsFalse(row.HeightOnly);
        }

        [Test]
        public void Diff_BelowEpsilon_IsNoEdit()
        {
            var nudged = new Area1Pose(new Vector3(0.0005f, 0f, 0f), Quaternion.Euler(0f, 0.005f, 0f), Vector3.one * 1.0005f);

            Assert.IsEmpty(Area1HandEdits.Diff(Scene(("A", Pose())), Scene(("A", nudged))));
        }

        [Test]
        public void Diff_RemovedParent_StandsForItsChildren()
        {
            List<Area1EditRow> rows = Area1HandEdits.Diff(Scene(("Warehouse", Pose()), ("Warehouse/Crate_0", Pose()), ("Warehouse/Crate_0/Lid", Pose())), Scene(("Warehouse", Pose())));

            Assert.AreEqual("Warehouse/Crate_0", rows.Single().Path);
            Assert.AreEqual(Area1EditKind.Removed, rows.Single().Kind);
        }

        [Test]
        public void Diff_AddedParent_StandsForItsChildren()
        {
            List<Area1EditRow> rows = Area1HandEdits.Diff(Scene(("Docks", Pose())), Scene(("Docks", Pose()), ("Docks/New", Pose()), ("Docks/New/Part", Pose())));

            Assert.AreEqual("Docks/New", rows.Single().Path);
            Assert.AreEqual(Area1EditKind.Added, rows.Single().Kind);
        }

        [Test]
        public void Snapshot_SameNamedSiblings_GetOrdinals()
        {
            var root = new GameObject("root").transform;
            try
            {
                Child("Top", root);
                Transform top = root.GetChild(0);
                Child("Post", top);
                Transform second = Child("Post", top);

                Dictionary<string, Area1Pose> poses = Area1HandEdits.Snapshot(root, new[] { top });

                CollectionAssert.AreEquivalent(new[] { "Top", "Top/Post", "Top/Post[1]" }, poses.Keys);
                Assert.AreSame(second, Area1HandEdits.Resolve(root, "Top/Post[1]"));
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Apply_MovesRemovesAndWarnsOnlyForOwnedPaths()
        {
            var root = new GameObject("root").transform;
            try
            {
                Transform post = Child("Post", root);
                Transform first = Child("Crate", root);
                Transform second = Child("Crate", root);
                Transform edge = Child("CampEdge", root);
                var edits = new Area1HandEdits();
                edits.Moved.Add(Edit("Post", Pose(4f, 5f, 6f)));
                edits.Moved.Add(Edit("Gone", Pose()));
                edits.Moved.Add(Edit("CampEdge", Pose(9f)));
                edits.Removed.Add("Crate");
                edits.Removed.Add("Crate[1]"); // resolved up front: a lazy lookup finds nothing once the first is destroyed
                LogAssert.Expect(LogType.Warning, new Regex("Gone"));
                LogAssert.Expect(LogType.Warning, new Regex("no longer match"));

                int missing = edits.Apply(root, path => !Area1HandEdits.IsCampEdge(path));

                Assert.AreEqual(1, missing);
                Assert.AreEqual(new Vector3(4f, 5f, 6f), post.localPosition);
                Assert.AreEqual(Vector3.zero, edge.localPosition);
                Assert.IsTrue(first == null && second == null);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Json_RoundTrips()
        {
            var edits = new Area1HandEdits();
            edits.Moved.Add(Edit("GuardPost/Lockup", new Area1Pose(new Vector3(-4.38f, -0.1029f, 6f), Quaternion.Euler(0f, 40f, 0f), new Vector3(1f, 2f, 1f)), new Vector3(1f, 2f, 3f)));
            edits.Removed.Add("Warehouse/Crate_0");

            Assert.IsTrue(Area1HandEdits.TryParse(edits.ToJson(), out Area1HandEdits read, out string problem), problem);

            Area1Pose pose = read.Moved.Single().Pose;
            Assert.AreEqual("GuardPost/Lockup", read.Moved.Single().Path);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), read.Moved.Single().Built);
            Assert.AreEqual(-4.38f, pose.Position.x, 0.0001f);
            Assert.AreEqual(2f, pose.Scale.y, 0.0001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 40f, 0f), pose.Rotation), 0.02f);
            CollectionAssert.AreEqual(new[] { "Warehouse/Crate_0" }, read.Removed);
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"moved\":[],\"removed\":[1]}")]
        [TestCase("{\"moved\":[3],\"removed\":[]}")]
        [TestCase("{\"moved\":[{\"path\":\"A\",\"pos\":[0,0],\"rot\":[0,0,0,1],\"scale\":[1,1,1]}],\"removed\":[]}")]
        [TestCase("{\"moved\":[{\"path\":\"A\",\"pos\":[0,0,0],\"rot\":[0,0,0,0],\"scale\":[1,1,1],\"built\":[0,0,0]}],\"removed\":[]}")]
        [TestCase("{\"moved\":[{\"path\":\"A\",\"pos\":[0,0,0],\"rot\":[0,0,0,1],\"scale\":[1,1,1]}],\"removed\":[]}")]
        [TestCase("{\"moved\":[{\"path\":\"A\",\"pos\":[1e300,0,0],\"rot\":[0,0,0,1],\"scale\":[1,1,1],\"built\":[0,0,0]}],\"removed\":[]}")]
        public void TryParse_BrokenFile_Fails(string json)
        {
            Assert.IsFalse(Area1HandEdits.TryParse(json, out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void Diff_RotationJustOverEpsilon_IsAnEdit()
        {
            var turned = new Area1Pose(Vector3.zero, Quaternion.Euler(0f, 0.02f, 0f), Vector3.one);

            Assert.AreEqual(1, Area1HandEdits.Diff(Scene(("A", Pose())), Scene(("A", turned))).Count);
        }

        [Test]
        public void Diff_PanelFollowingTheGround_IsHeightOnly()
        {
            var built = new Area1Pose(new Vector3(1f, 2f, 3f), Quaternion.identity, new Vector3(3f, 6f, 0.6f));
            var stale = new Area1Pose(new Vector3(1f, 3f, 3f), Quaternion.identity, new Vector3(3f, 8f, 0.6f));

            Assert.IsTrue(Area1HandEdits.Diff(Scene(("Panel", built)), Scene(("Panel", stale))).Single().HeightOnly);
        }

        [Test]
        public void Merge_SetsDefaultTicks()
        {
            var saved = new Area1HandEdits();
            saved.Moved.Add(Edit("Known", Pose()));
            var rows = new List<Area1EditRow>
            {
                new Area1EditRow { Kind = Area1EditKind.Moved, Path = "Flat" },
                new Area1EditRow { Kind = Area1EditKind.Moved, Path = "Height", HeightOnly = true },
                new Area1EditRow { Kind = Area1EditKind.Moved, Path = "Known", HeightOnly = true },
                new Area1EditRow { Kind = Area1EditKind.Removed, Path = "Gone" },
                new Area1EditRow { Kind = Area1EditKind.Added, Path = "New" },
            };

            List<Area1EditRow> merged = Area1HandEdits.Merge(rows, saved);

            CollectionAssert.AreEqual(new[] { true, false, true, true, false }, merged.Select(r => r.Ticked));
        }

        [Test]
        public void Merge_SavedEditWithNoDifference_StaysListedAndTicked()
        {
            var saved = new Area1HandEdits();
            saved.Moved.Add(Edit("Reverted", Pose(1f)));
            saved.Removed.Add("Warehouse/Crate_0");

            List<Area1EditRow> merged = Area1HandEdits.Merge(new List<Area1EditRow>(), saved);

            Assert.AreEqual(2, merged.Count);
            Assert.IsTrue(merged.All(r => r.InFileOnly && r.Ticked));
        }

        [Test]
        public void Apply_ToolBuildsElsewhereNow_WarnsAndStillApplies()
        {
            var root = new GameObject("root").transform;
            try
            {
                Transform panel = Child("Panel_140", root);
                var edits = new Area1HandEdits();
                edits.Moved.Add(Edit("Panel_140", Pose(4f), new Vector3(9f, 0f, 0f)));
                LogAssert.Expect(LogType.Warning, new Regex("Panel_140"));

                edits.Apply(root, _ => true);

                Assert.AreEqual(new Vector3(4f, 0f, 0f), panel.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void Resolve_OversizedOrdinal_IsNoMatchAndNoOverflow()
        {
            var root = new GameObject("root").transform;
            try
            {
                Assert.IsNull(Area1HandEdits.Resolve(root, "A[99999999999]"));
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void TryLoad_NoFile_IsEmpty()
        {
            Assert.IsTrue(Area1HandEdits.TryLoad(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), out Area1HandEdits edits, out _));
            Assert.IsEmpty(edits.Moved);
            Assert.IsEmpty(edits.Removed);
        }

        [Test]
        public void TryLoad_BrokenFile_FailsAndSaysNothingChanged()
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "{");

                Assert.IsFalse(Area1HandEdits.TryLoad(file, out _, out string problem));
                StringAssert.Contains("nothing changed", problem);
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static Area1MovedEdit Edit(string path, Area1Pose pose, Vector3 built = default) => new Area1MovedEdit { Path = path, Pose = pose, Built = built };

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
