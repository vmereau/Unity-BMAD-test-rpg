using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Editor.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The broken stretch's placement (BrokenStretchLayout) and the parts file its Blender script writes: parts land on
    /// their ground, a wrongly shaped file is refused, the walks through the work yard stay clear of every collider,
    /// and the strip closed to agents spans the gap.
    /// </summary>
    public class BrokenStretchTests
    {
        private static readonly StretchFrame Frame = new StretchFrame(new Vector2(3f, 52.4f), new Vector2(1f, 0f), Vector2.zero);
        private static readonly Func<Vector2, float> Flat = _ => 2f;
        private static readonly Func<Vector2, float> Rocks = _ => 3f;

        // In the stretch's own frame (x along the wall, z out of the camp), as in build_broken_stretch.py:
        // the 2.5 m walk from the gap to the warehouse ramp's foot, the walk from that foot west to the dock, and the
        // ground east of the ramp that the docks' other entrance crosses (the new logs were moved off it by hand).
        private static readonly Rect GapWalk = Rect.MinMaxRect(-0.45f, 0.6f, 2.05f, 4.8f);
        private static readonly Rect DockWalk = Rect.MinMaxRect(-4f, 4.8f, 1.6f, 8f);
        private static readonly Rect DockEntrance = Rect.MinMaxRect(0f, 7.6f, 7f, 11.6f);
        private const float GAP_HALF_WIDTH = 3.7f;   // the wall's ends stand this far each side of the break point
        private const float HEAD_HEIGHT = 1.9f;      // a pole's end above this is over a walker's head
        private const float REACH = 20f;             // no part belongs further than this from the break point (the shore group is 17 m away)

        private static StretchPart[] Parse(string json)
        {
            Assert.IsTrue(BrokenStretchLayout.TryParse(JObject.Parse(json), out StretchPart[] parts, out string problem), problem);
            return parts;
        }

        private static bool Refused(string json) => !BrokenStretchLayout.TryParse(JObject.Parse(json), out _, out _);

        private static JObject PartsFile()
        {
            string path = Path.GetFullPath(BrokenStretchBuilder.PARTS);
            Assert.IsTrue(File.Exists(path), $"{BrokenStretchBuilder.PARTS} is missing: run build_broken_stretch.py");
            return JObject.Parse(File.ReadAllText(path));
        }

        [Test]
        public void Frame_PointsOutOfTheCamp_WhicheverWayTheLineIsListed()
        {
            Assert.AreEqual(new Vector2(0f, 1f), Frame.Outward, "The camp is south of this wall, so out is north");
            Assert.AreEqual(new Vector2(5f, 53.4f), Frame.ToWorld(2f, 1f));

            var reversed = new StretchFrame(new Vector2(3f, 52.4f), new Vector2(-1f, 0f), Vector2.zero);
            Assert.AreEqual(Frame.Outward, reversed.Outward);
            Assert.AreEqual(Frame.Along, reversed.Along, "A line listed the other way round must not mirror the layout");
            Assert.AreEqual(Frame.ToWorld(2f, 1f), reversed.ToWorld(2f, 1f));
        }

        [Test]
        public void Ground_StandsItsHeightAboveTheTerrain()
        {
            StretchPart part = Parse("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[1,0.5,2]}]}")[0];
            StretchPose pose = BrokenStretchLayout.Place(part, Frame, Flat, Rocks);
            Assert.AreEqual(new Vector3(4f, 2.5f, 54.4f), pose.Position);
            Assert.AreEqual(Vector3.one, pose.Scale);
        }

        [Test]
        public void Surface_StandsOnTheRocks()
        {
            StretchPart part = Parse("{parts:[{name:'A',mesh:'M',fit:'surface',pos:[0,0,0]}]}")[0];
            Assert.AreEqual(3f, BrokenStretchLayout.Place(part, Frame, Flat, Rocks).Position.y, 1e-4f);
        }

        [Test]
        public void Span_RestsBothEndsAndPointsFromAToB()
        {
            StretchPart part = Parse("{parts:[{name:'A',mesh:'M',fit:'span',on:'ground',a:[0,0],b:[4,0],lift:0.15}]}")[0];
            Func<Vector2, float> slope = p => p.x * 0.5f; // rises along the wall
            StretchPose pose = BrokenStretchLayout.Place(part, Frame, slope, Rocks);
            Assert.AreEqual(3f * 0.5f + 0.15f, pose.Position.y, 1e-4f, "End a rests its lift above its own ground");
            Vector3 along = pose.Rotation * Vector3.forward;
            Vector3 expected = new Vector3(4f, 2f, 0f).normalized;
            Assert.Less(Vector3.Angle(along, expected), 0.1f, "The log's axis runs from a up to b");
        }

        [Test]
        public void Strut_IsStretchedToItsLength_AndCanRestOnRocks()
        {
            StretchPart part = Parse("{parts:[{name:'A',mesh:'M',fit:'strut',a:[0,0,0],b:[0,3,4]}]}")[0];
            StretchPose pose = BrokenStretchLayout.Place(part, Frame, Flat, Rocks);
            Assert.AreEqual(5f, pose.Scale.z, 1e-4f);
            Assert.AreEqual(1f, pose.Scale.x, 1e-6f);
            Assert.AreEqual(2f, pose.Position.y, 1e-4f, "On the terrain unless it says otherwise");
            Assert.Less(Vector3.Angle(pose.Rotation * Vector3.forward, new Vector3(0f, 3f, 4f)), 0.1f);

            StretchPart onRocks = Parse("{parts:[{name:'A',mesh:'M',fit:'strut',on:'surface',a:[0,0,0],b:[0,3,4]}]}")[0];
            Assert.AreEqual(3f, BrokenStretchLayout.Place(onRocks, Frame, Flat, Rocks).Position.y, 1e-4f);
        }

        [Test]
        public void UprightForward_KeepsThePartStanding()
        {
            StretchPart part = Parse("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],forward:[0,1,0],roll:30}]}")[0];
            StretchPose pose = BrokenStretchLayout.Place(part, Frame, Flat, Rocks);
            Assert.Less(Vector3.Angle(pose.Rotation * Vector3.forward, Vector3.up), 0.1f, "A log's axis can point straight up");
        }

        [Test]
        public void BadFile_IsRefused_NotThrown()
        {
            Assert.IsTrue(Refused("{}"), "No parts");
            Assert.IsTrue(Refused("{parts:[1]}"), "A part that is not an object");
            Assert.IsTrue(Refused("{parts:[{name:['A'],mesh:'M',fit:'ground',pos:[0,0,0]}]}"), "A name that is not text");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'float',pos:[0,0,0]}]}"), "Unknown fit");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'span',a:[1,1],b:[1,1]}]}"), "A span with no length");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'span',on:'water',a:[0,0],b:[1,1]}]}"), "Resting on something unknown");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0]},{name:'A',mesh:'M',fit:'ground',pos:[1,0,0]}]}"), "A name used twice");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:{t:'box'}}]}"), "Colliders that are not a list");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:[7]}]}"), "A collider that is not an object");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:[{t:'ball',c:[0,0,0],s:[1,1,1]}]}]}"), "An unknown collider shape");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:[{t:'capsule',a:[0,0,0],b:[0,1,0]}]}]}"), "A capsule with no radius");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:[{t:'capsule',a:[0,1,0],b:[0,1,0],r:0.1}]}]}"), "A capsule with no length");
            Assert.IsTrue(Refused("{parts:[{name:'A',mesh:'M',fit:'ground',pos:[0,0,0],col:[{t:'box',c:[0,0,0],s:[1,0,1]}]}]}"), "A box with no height");
        }

        [Test]
        public void PartsFile_PlacesEveryPartNearTheBreak_OnItsGround()
        {
            JObject file = PartsFile();
            StretchPart[] parts = Parse(file.ToString());
            Assert.Greater(parts.Length, 30);
            Assert.AreEqual(JTokenType.Integer, file["version"].Type);
            Assert.AreEqual(BrokenStretchBuilder.VERSION, (int)file["version"], "The script's VERSION and the builder's must move together");

            Func<Vector2, float> bumpy = p => 1.8f + Mathf.Sin(p.x * 0.7f) * 0.3f;
            foreach (StretchPart part in parts)
            {
                Assert.IsNotNull(file["meshes"][part.Mesh], $"{part.Name}: the file lists no mesh '{part.Mesh}'");
                StretchPose pose = BrokenStretchLayout.Place(part, Frame, bumpy, Rocks);
                Assert.IsFalse(float.IsNaN(pose.Position.x + pose.Position.y + pose.Position.z + pose.Rotation.w + pose.Scale.z), part.Name);
                Assert.Less(Vector2.Distance(new Vector2(pose.Position.x, pose.Position.z), Frame.Origin), REACH, $"{part.Name} is far from the break");
                float below = Mathf.Min(bumpy(new Vector2(pose.Position.x, pose.Position.z)), 3f) - 0.5f;
                Assert.Greater(pose.Position.y, below, $"{part.Name}'s pivot is under the ground");
            }
        }

        [Test]
        public void PartsFile_KeepsTheWalksClear()
        {
            foreach (StretchPart part in Parse(PartsFile().ToString()))
            {
                foreach (Vector2 foot in Footprint(part))
                {
                    Assert.IsFalse(GapWalk.Contains(foot), $"{part.Name} reaches into the walk from the gap to the warehouse ramp at {foot}");
                    Assert.IsFalse(DockWalk.Contains(foot), $"{part.Name} reaches into the walk from the ramp to the dock at {foot}");
                    Assert.IsFalse(DockEntrance.Contains(foot), $"{part.Name} reaches into the docks' other entrance at {foot}");
                }
            }
        }

        [Test]
        public void PartsFile_ClosesTheGapToAgents()
        {
            JToken strip = PartsFile()["notWalkable"];
            float centre = (float)strip["centre"][0], width = (float)strip["size"][0];
            Assert.LessOrEqual(centre - width * 0.5f, -GAP_HALF_WIDTH, "The strip reaches the west end of the wall");
            Assert.GreaterOrEqual(centre + width * 0.5f, GAP_HALF_WIDTH, "The strip reaches the east end of the wall");
            float z = (float)strip["centre"][1], depth = (float)strip["size"][1];
            Assert.Less(z - depth * 0.5f, -1.5f, "It reaches back onto the rock bank inside the camp");
            Assert.Greater(z + depth * 0.5f, 0.3f, "It covers the old wall line");
            Assert.Less(z + depth * 0.5f, GapWalk.yMin + 0.5f, "It leaves the shelf open: agents walk from the ramp to the dock there");
        }

        [Test]
        public void Scar_IsBareEarthInsideAndUntouchedOutside()
        {
            Vector2 centre = BrokenStretchBuilder.ScarCentre;
            Assert.AreEqual(1f, BrokenStretchBuilder.ScarWeight(centre, centre), 1e-5f);
            Assert.AreEqual(0f, BrokenStretchBuilder.ScarWeight(centre + new Vector2(0f, 6f), centre), 1e-5f);
            Assert.Greater(BrokenStretchBuilder.HollowDepth(centre, centre), 0.25f);
            Assert.AreEqual(0f, BrokenStretchBuilder.HollowDepth(centre + new Vector2(0f, 5f), centre), 1e-5f, "The walk to the dock keeps its ground");
        }

        /// <summary>
        /// Where a part's colliders reach at walking height, in the stretch's frame: box corners, capsule ends out to
        /// their radius. A part with no collider stops nobody and has no footprint.
        /// </summary>
        private static IEnumerable<Vector2> Footprint(StretchPart part)
        {
            if (part.Colliders.Length == 0) yield break;

            if (part.Fit == StretchFit.Strut)
            {
                // A stretched pole: its ends, where they are low enough to walk into.
                foreach (Vector3 end in new[] { part.A, part.B })
                    if (end.y < HEAD_HEIGHT)
                        foreach (Vector2 p in Around(new Vector2(end.x, end.z), part.Colliders[0].Radius)) yield return p;
                yield break;
            }

            Vector2 origin, forward;
            if (part.Fit == StretchFit.Span)
            {
                origin = new Vector2(part.A.x, part.A.z);
                forward = (new Vector2(part.B.x, part.B.z) - origin).normalized;
            }
            else
            {
                origin = new Vector2(part.Pos.x, part.Pos.z);
                bool standing = Mathf.Abs(part.Forward.y) > 0.5f; // a leaning log: only its foot is at walking height
                if (standing)
                {
                    foreach (Vector2 p in Around(origin, part.Colliders[0].Radius)) yield return p;
                    yield break;
                }
                forward = new Vector2(part.Forward.x, part.Forward.z).normalized;
            }

            var right = new Vector2(forward.y, -forward.x); // the mesh's +x, with +z along forward
            foreach (StretchCollider c in part.Colliders)
            {
                if (c.Capsule)
                {
                    // Along the capsule, as far up as a walker reaches: a hoist's leg leans over the walk above head height.
                    for (int i = 0; i <= 8; i++)
                    {
                        Vector3 on = Vector3.Lerp(c.A, c.B, i / 8f);
                        if (on.y >= HEAD_HEIGHT) continue;
                        foreach (Vector2 p in Around(origin + right * on.x + forward * on.z, c.Radius)) yield return p;
                    }
                }
                else
                {
                    float yaw = c.Yaw * Mathf.Deg2Rad;
                    foreach (float sx in new[] { -0.5f, 0f, 0.5f })
                    {
                        foreach (float sz in new[] { -0.5f, 0f, 0.5f })
                        {
                            float x = sx * c.B.x, z = sz * c.B.z;
                            var local = new Vector2(c.A.x + x * Mathf.Cos(yaw) + z * Mathf.Sin(yaw), c.A.z - x * Mathf.Sin(yaw) + z * Mathf.Cos(yaw));
                            yield return origin + right * local.x + forward * local.y;
                        }
                    }
                }
            }
        }

        private static IEnumerable<Vector2> Around(Vector2 centre, float radius)
        {
            yield return centre;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                yield return centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
        }
    }
}
