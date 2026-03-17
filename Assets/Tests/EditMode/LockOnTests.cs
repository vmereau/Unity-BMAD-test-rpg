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
    }
}
