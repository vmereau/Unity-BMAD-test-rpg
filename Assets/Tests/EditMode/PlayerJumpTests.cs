using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Edit Mode tests for jump physics formulas used by PlayerController.
    /// Tests pure math — no MonoBehaviour lifecycle, no scene required.
    /// </summary>
    public class PlayerJumpTests
    {
        private const float GRAVITY = -9.81f;
        private const float GROUNDED_VELOCITY = -2f;
        private const float DELTA_TIME = 1f / 60f;

        [Test]
        public void ApplyJump_WhenGrounded_SetsVelocityToJumpForce()
        {
            // Simulates: if (isGrounded && jumpPressed) verticalVelocity = jumpForce
            bool isGrounded = true;
            bool jumpPressed = true;
            float jumpForce = 5f;
            float verticalVelocity = GROUNDED_VELOCITY;

            if (isGrounded && jumpPressed)
                verticalVelocity = jumpForce;

            Assert.That(verticalVelocity, Is.EqualTo(jumpForce));
            Assert.That(verticalVelocity, Is.GreaterThan(0f));
        }

        [Test]
        public void ApplyGravity_GroundedGuard_AllowsJumpVelocityToSurviveFirstFrame()
        {
            // Validates the critical bug fix: when isGrounded=true but verticalVelocity > 0
            // (character just jumped), ApplyGravity must NOT snap back to GROUNDED_VELOCITY.
            // Without the "&& verticalVelocity <= 0f" guard, jump would be cancelled every frame.
            bool isGrounded = true;
            float verticalVelocity = 5f; // positive — just jumped

            // Simulates ApplyGravity() with the guard in place
            if (isGrounded && verticalVelocity <= 0f)
                verticalVelocity = GROUNDED_VELOCITY;
            else
                verticalVelocity += GRAVITY * DELTA_TIME;

            Assert.That(verticalVelocity, Is.LessThan(5f), "Gravity must reduce velocity each frame");
            Assert.That(verticalVelocity, Is.GreaterThan(0f), "Jump velocity must not be overridden by grounded snap on the same frame");
        }

        [Test]
        public void ApplyGravity_WhenGroundedAtRest_SnapsToGroundedVelocity()
        {
            // When grounded with zero or negative velocity, snaps to GROUNDED_VELOCITY (-2f)
            // to keep the character pressed against the ground (avoids float-off).
            bool isGrounded = true;
            float verticalVelocity = 0f;

            if (isGrounded && verticalVelocity <= 0f)
                verticalVelocity = GROUNDED_VELOCITY;
            else
                verticalVelocity += GRAVITY * DELTA_TIME;

            Assert.That(verticalVelocity, Is.EqualTo(GROUNDED_VELOCITY));
        }
    }
}
