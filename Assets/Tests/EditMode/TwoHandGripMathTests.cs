using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class TwoHandGripMathTests
    {
        private const float TOLERANCE = 1e-4f;
        private const float ANGLE_TOLERANCE = 0.01f;

        private static readonly Vector3 Right = new(0.2f, 1.2f, 0.4f);
        private static readonly Vector3 Left = new(0f, 1.0f, 0.3f);

        private static void Solve(Vector3 edgeRef, float roll, float slide, out Vector3 pos, out Quaternion rot) =>
            Assert.That(TwoHandGripMath.TrySolve(Right, Left, edgeRef, roll, slide, out pos, out rot, out _), Is.True);

        [Test]
        public void Blade_PointsFromLeftHandThroughRightHand()
        {
            Solve(Vector3.forward, 0f, 0f, out _, out Quaternion rot);
            Assert.That(Vector3.Angle(rot * Vector3.up, (Right - Left).normalized), Is.LessThan(ANGLE_TOLERANCE));
        }

        [Test]
        public void Grip_SitsOnTheRightHand_WithoutSlide()
        {
            Solve(Vector3.forward, 0f, 0f, out Vector3 pos, out _);
            Assert.That(Vector3.Distance(pos, Right), Is.LessThan(TOLERANCE));
        }

        [Test]
        public void Edge_IsTheReferenceMadePerpendicularToTheBlade()
        {
            Vector3 edgeRef = new(0.3f, 0.4f, 1f);
            Solve(edgeRef, 0f, 0f, out _, out Quaternion rot);
            Vector3 blade = (Right - Left).normalized;
            Vector3 expected = Vector3.ProjectOnPlane(edgeRef, blade).normalized;
            Assert.That(Vector3.Angle(rot * Vector3.forward, expected), Is.LessThan(ANGLE_TOLERANCE));
            Assert.That(Mathf.Abs(Vector3.Dot(rot * Vector3.forward, blade)), Is.LessThan(TOLERANCE));
        }

        [Test]
        public void Roll_RotatesTheEdgeAboutTheBlade()
        {
            Solve(Vector3.forward, 0f, 0f, out _, out Quaternion unrolled);
            Solve(Vector3.forward, 90f, 0f, out _, out Quaternion rolled);
            Vector3 blade = (Right - Left).normalized;
            Assert.That(Vector3.SignedAngle(unrolled * Vector3.forward, rolled * Vector3.forward, blade), Is.EqualTo(90f).Within(ANGLE_TOLERANCE));
            Assert.That(Vector3.Angle(rolled * Vector3.up, blade), Is.LessThan(ANGLE_TOLERANCE));
        }

        [Test]
        public void Slide_MovesTheGripAlongTheBlade()
        {
            Solve(Vector3.forward, 0f, 0.05f, out Vector3 pos, out _);
            Vector3 expected = Right + (Right - Left).normalized * 0.05f;
            Assert.That(Vector3.Distance(pos, expected), Is.LessThan(TOLERANCE));
        }

        [Test]
        public void Weight_IsFull_ForTheMeasuredGripRange()
        {
            Assert.That(TwoHandGripMath.GripWeight(0.19f, 1f), Is.EqualTo(1f));
            Assert.That(TwoHandGripMath.GripWeight(0.35f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void Weight_EasesOut_AsTheLeftHandLetsGo()
        {
            float half = (TwoHandGripMath.FULL_GRIP_MAX_DISTANCE + TwoHandGripMath.RELEASE_HAND_DISTANCE) * 0.5f;
            Assert.That(TwoHandGripMath.GripWeight(half, 1f), Is.EqualTo(0.5f).Within(TOLERANCE));
            Assert.That(TwoHandGripMath.GripWeight(TwoHandGripMath.RELEASE_HAND_DISTANCE, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void Weight_EasesOut_NearTheDegenerateThresholds()
        {
            Assert.That(TwoHandGripMath.GripWeight(TwoHandGripMath.MIN_HAND_DISTANCE, 1f), Is.EqualTo(0f));
            Assert.That(TwoHandGripMath.GripWeight(0.3f, TwoHandGripMath.MIN_EDGE_SIN), Is.EqualTo(0f));
            float midSin = (TwoHandGripMath.MIN_EDGE_SIN + TwoHandGripMath.FULL_EDGE_SIN) * 0.5f;
            Assert.That(TwoHandGripMath.GripWeight(0.3f, midSin), Is.EqualTo(0.5f).Within(TOLERANCE));
        }

        [Test]
        public void ReversedGrip_StillRunsTheBladeThroughBothHands()
        {
            // 2hand blocking: the left hand on the other side of the right hand — the blade simply follows the line.
            Vector3 left = Right + new Vector3(-0.25f, 0.05f, 0f);
            Assert.That(TwoHandGripMath.TrySolve(Right, left, Vector3.forward, 0f, 0f, out _, out Quaternion rot, out float weight), Is.True);
            Assert.That(Vector3.Angle(rot * Vector3.up, (Right - left).normalized), Is.LessThan(ANGLE_TOLERANCE));
            Assert.That(weight, Is.EqualTo(1f));
        }

        [Test]
        public void HandsTooClose_Fails()
        {
            Vector3 left = Right - new Vector3(0f, TwoHandGripMath.MIN_HAND_DISTANCE * 0.5f, 0f);
            Assert.That(TwoHandGripMath.TrySolve(Right, left, Vector3.forward, 0f, 0f, out _, out _, out _), Is.False);
        }

        [Test]
        public void EdgeReferenceParallelToBlade_Fails()
        {
            Vector3 blade = (Right - Left).normalized;
            Assert.That(TwoHandGripMath.TrySolve(Right, Left, blade, 0f, 0f, out _, out _, out _), Is.False);
            Assert.That(TwoHandGripMath.TrySolve(Right, Left, Vector3.zero, 0f, 0f, out _, out _, out _), Is.False);
        }
    }
}
