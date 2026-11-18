using Game.Editor.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class GoatTrackDropCheckTests
    {
        // Drop from (0,0) towards -x: the ledge is at x >= 0, the pit beyond.
        private static readonly Vector2 Top = Vector2.zero, Forward = Vector2.left;

        [TestCase(0.5f, 0f, 0f, true)]     // behind the top: back on the ledge at any height
        [TestCase(-2f, 0f, 0f, false)]     // in the pit, low
        [TestCase(-2f, 0f, 45f, true)]     // just past the top but up at ledge height (on the Lip)
        [TestCase(-5f, 0f, 45f, false)]   // high but well past the ledge (the face beside the pit)
        [TestCase(-2f, 17f, 45f, false)]  // high, near the top's line but far to the side (the inner face north of the pit)
        public void IsBackOnLedge_Cases(float x, float z, float height, bool expected) =>
            Assert.AreEqual(expected, DropCheckRules.IsBackOnLedge(new Vector2(x, z), height, Top, Forward, 3f, 6f, 42f));

        // ── The CharacterController behaviour the drop blockers rely on ──────────────────────────────────────────────

        private static float ClimbAgainst(GameObject wall)
        {
            var host = new GameObject("ClimbTestController");
            try
            {
                var cc = host.AddComponent<CharacterController>();
                cc.height = 1.7f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.85f, 0f);
                cc.slopeLimit = 45f;
                cc.stepOffset = 0.3f;
                float best = 0f;
                for (int run = 0; run < 3; run++)
                {
                    GoatTrackDropCheck.Drive(cc, new Vector3(-1.5f + run * 0.2f, 0.05f, 0f), Vector3.right, Vector3.right, 5f,
                        new Vector2(100f, 0f), Vector2.left, 1000f, out _, out float peak);
                    best = Mathf.Max(best, peak);
                }
                return best;
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(wall);
            }
        }

        private static GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            return floor;
        }

        [Test]
        public void VerticalBoxFace_CannotBeJumpClimbed()
        {
            var floor = Floor();
            try
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0.5f, 5f, 0f);
                wall.transform.localScale = new Vector3(1f, 10f, 10f);
                Physics.SyncTransforms();
                Assert.Less(ClimbAgainst(wall), 2f, "a jump reaches ~1.3 m; a vertical box face gives no ground to jump from again");
            }
            finally { Object.DestroyImmediate(floor); }
        }

        [Test]
        public void SteepSlope_IsJumpClimbed()
        {
            // The reason the drop needs boxes: a 70 degree surface still counts as ground, so repeated jumps walk up it.
            var floor = Floor();
            try
            {
                var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.transform.position = new Vector3(2f, 4f, 0f);
                ramp.transform.rotation = Quaternion.Euler(0f, 0f, 70f);
                ramp.transform.localScale = new Vector3(10f, 0.5f, 10f);
                Physics.SyncTransforms();
                Assert.Greater(ClimbAgainst(ramp), 3f);
            }
            finally { Object.DestroyImmediate(floor); }
        }
    }
}
