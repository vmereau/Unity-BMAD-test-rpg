using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure-math tests for lock-on movement direction helpers.
    /// No MonoBehaviour dependencies — static helpers only.
    /// Note: These tests validate the math in isolation. Integration with
    /// PlayerController.ApplyMovement() requires Play Mode testing (Task 4).
    /// </summary>
    public class LockOnMovementTests
    {
        // --- Static helpers (mirrors PlayerController lock-on movement logic) ---

        static Vector3 ComputeLockForward(Vector3 playerPos, Vector3 targetPos)
        {
            Vector3 toTarget = targetPos - playerPos;
            toTarget.y = 0f;
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
        }

        static Vector3 ComputeLockRight(Vector3 lockForward)
            => Vector3.Cross(Vector3.up, lockForward).normalized;

        static Vector3 ComputeLockMoveDir(Vector3 forward, Vector3 right, Vector2 input)
        {
            Vector3 dir = forward * input.y + right * input.x;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;
        }

        // --- Tests ---

        [Test]
        public void LockForward_PointsTowardTarget()
        {
            Vector3 forward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Assert.That(Vector3.Distance(Vector3.forward, forward), Is.LessThan(1e-5f));
        }

        [Test]
        public void LockRight_IsPerpendicularToForward()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            float dot = Vector3.Dot(lockForward, lockRight);
            Assert.AreEqual(0f, dot, 1e-5f);
        }

        [Test]
        public void MoveForward_ProducesTargetwardDirection()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            Vector3 moveDir     = ComputeLockMoveDir(lockForward, lockRight, new Vector2(0f, 1f));
            Assert.That(Vector3.Distance(lockForward, moveDir), Is.LessThan(1e-5f));
        }

        [Test]
        public void StrafeRight_ProducesPerpendicularDirection()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            Vector3 moveDir     = ComputeLockMoveDir(lockForward, lockRight, new Vector2(1f, 0f));
            Assert.That(Vector3.Distance(lockRight, moveDir), Is.LessThan(1e-5f));
        }

        [Test]
        public void CoincidentTarget_ReturnsZeroForward()
        {
            Vector3 forward = ComputeLockForward(Vector3.zero, Vector3.zero);
            Assert.AreEqual(Vector3.zero, forward);
        }

        [Test]
        public void MoveBackward_ProducesAwayFromTargetDirection()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            Vector3 moveDir     = ComputeLockMoveDir(lockForward, lockRight, new Vector2(0f, -1f));
            Assert.That(Vector3.Distance(-lockForward, moveDir), Is.LessThan(1e-5f));
        }

        [Test]
        public void StrafeLeft_ProducesOppositeRightDirection()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            Vector3 moveDir     = ComputeLockMoveDir(lockForward, lockRight, new Vector2(-1f, 0f));
            Assert.That(Vector3.Distance(-lockRight, moveDir), Is.LessThan(1e-5f));
        }

        [Test]
        public void SmallInput_BelowDeadzone_ReturnsZeroMoveDir()
        {
            Vector3 lockForward = ComputeLockForward(Vector3.zero, new Vector3(0f, 0f, 5f));
            Vector3 lockRight   = ComputeLockRight(lockForward);
            // sqrMagnitude of resulting dir ≈ 0.005 < 0.01 guard — must return zero
            Vector3 moveDir = ComputeLockMoveDir(lockForward, lockRight, new Vector2(0.05f, 0.05f));
            Assert.AreEqual(Vector3.zero, moveDir);
        }
    }
}
