using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure-math tests for LockOnSystem cone detection and nearest-target selection.
    /// No MonoBehaviour dependencies — static helpers only.
    /// </summary>
    public class LockOnTests
    {
        // --- Static helpers (mirrors LockOnSystem internal logic) ---

        static bool IsInCone(Vector3 forward, Vector3 toTarget, float halfAngle)
            => Vector3.Angle(forward, toTarget) <= halfAngle;

        static int GetNearestIndex(float[] distances)
        {
            int nearest = 0;
            for (int i = 1; i < distances.Length; i++)
                if (distances[i] < distances[nearest]) nearest = i;
            return nearest;
        }

        // --- Cone tests ---

        [Test]
        public void IsInCone_ReturnsTrue_WhenTargetIsDirectlyAhead()
        {
            Assert.IsTrue(IsInCone(Vector3.forward, Vector3.forward, 60f));
        }

        [Test]
        public void IsInCone_ReturnsFalse_WhenTargetIsBehind()
        {
            // Vector3.back is 180° from Vector3.forward — well outside any 60° half-cone
            Assert.IsFalse(IsInCone(Vector3.forward, Vector3.back, 60f));
        }

        [Test]
        public void IsInCone_HandlesBoundaryInclusive()
        {
            // Rotate forward by exactly 60°; use the measured angle as halfAngle so the
            // boundary is tested as <= (not <) without floating-point imprecision in Vector3.Angle.
            Vector3 at60 = Quaternion.AngleAxis(60f, Vector3.up) * Vector3.forward;
            float exactAngle = Vector3.Angle(Vector3.forward, at60);
            Assert.IsTrue(IsInCone(Vector3.forward, at60, exactAngle),
                "A target at the exact boundary angle must be inside the cone (boundary inclusive)");
        }

        [Test]
        public void IsInCone_ReturnsFalse_WhenAngleJustExceedsFOV()
        {
            // Construct a vector 61° from forward — just outside the 60° half-cone
            float rad = 61f * Mathf.Deg2Rad;
            Vector3 at61 = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            Assert.IsFalse(IsInCone(Vector3.forward, at61, 60f));
        }

        // --- Nearest target test ---

        [Test]
        public void GetNearest_ReturnsClosestCandidate()
        {
            float[] distances = { 10f, 3f, 7f };
            Assert.AreEqual(1, GetNearestIndex(distances));
        }

        // --- LockOnSystem.IsInCone (real code) ---

        [Test]
        public void SystemIsInCone_IgnoresTargetHeight()
        {
            // A low spider root or a tall NPC ahead must both count — the cone is horizontal
            Assert.IsTrue(Game.Player.LockOnSystem.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, -3f, 5f), 60f));
            Assert.IsTrue(Game.Player.LockOnSystem.IsInCone(Vector3.zero, Vector3.forward, new Vector3(0f, 4f, 5f), 60f));
        }

        [Test]
        public void SystemIsInCone_RejectsTargetOutsideHalfAngle()
        {
            Vector3 at61 = Quaternion.AngleAxis(61f, Vector3.up) * Vector3.forward * 5f;
            Assert.IsFalse(Game.Player.LockOnSystem.IsInCone(Vector3.zero, Vector3.forward, at61, 60f));
        }

        [Test]
        public void SystemIsInCone_ZeroForward_RejectsEverything()
        {
            Assert.IsFalse(Game.Player.LockOnSystem.IsInCone(Vector3.zero, Vector3.up, Vector3.forward * 5f, 60f),
                "Camera looking straight down flattens to zero — must not accept every target");
        }

        [Test]
        public void SelectNearestInCone_PicksNearestInsideConeAndRange()
        {
            var candidates = new[]
            {
                new Vector3(0f, 0f, 8f),   // in cone, far
                new Vector3(0f, 0f, -2f),  // behind
                new Vector3(1f, -0.5f, 4f),// in cone, nearest (low spider root)
                new Vector3(0f, 0f, 30f),  // in cone, beyond range
            };
            Assert.AreEqual(2, Game.Player.LockOnSystem.SelectNearestInCone(
                Vector3.zero, Vector3.forward, candidates, 60f, 15f));
        }

        [Test]
        public void SelectNearestInCone_RejectsCandidateBeyondRange()
        {
            // A trigger collider can reach into the overlap sphere while its entity root is far away
            var candidates = new[] { new Vector3(0f, 0f, 20f) };
            Assert.AreEqual(-1, Game.Player.LockOnSystem.SelectNearestInCone(
                Vector3.zero, Vector3.forward, candidates, 60f, 15f));
        }

        [Test]
        public void SelectNearestInCone_NoCandidates_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Game.Player.LockOnSystem.SelectNearestInCone(
                Vector3.zero, Vector3.forward, new Vector3[0], 60f, 15f));
        }

        [Test]
        public void SystemIsInCone_RejectsTargetDirectlyAboveOrigin()
        {
            Assert.IsFalse(Game.Player.LockOnSystem.IsInCone(Vector3.zero, Vector3.forward, Vector3.up * 2f, 60f));
        }
    }
}
