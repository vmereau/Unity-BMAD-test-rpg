using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure-math tests for directional locomotion velocity normalization.
    /// No MonoBehaviour dependencies — static helpers only.
    /// Validates the VelocityX/VelocityZ normalization used in PlayerAnimator
    /// to drive the 2D Freeform Cartesian blend tree during lock-on locomotion.
    /// </summary>
    public class DirectionalLocomotionTests
    {
        // --- Static helper (mirrors PlayerAnimator lock-on normalization logic) ---

        static float NormalizeVelocity(float localAxisSpeed, float runSpeed)
            => Mathf.Clamp(localAxisSpeed / runSpeed, -1f, 1f);

        // --- Tests ---

        [Test]
        public void NormalizeVelocity_ForwardAtRunSpeed_ReturnsOne()
        {
            const float runSpeed = 6f;
            float normZ = NormalizeVelocity(runSpeed, runSpeed);
            Assert.AreEqual(1f, normZ, 1e-5f,
                "Forward velocity at runSpeed should normalize to 1.0");
        }

        [Test]
        public void NormalizeVelocity_BackwardAtWalkSpeed_ReturnsNegativeHalf()
        {
            const float walkSpeed = 3f;
            const float runSpeed = 6f;
            float normZ = NormalizeVelocity(-walkSpeed, runSpeed);
            Assert.AreEqual(-0.5f, normZ, 1e-5f,
                "Backward velocity at walkSpeed should normalize to -0.5 (walkSpeed/runSpeed = 3/6)");
        }

        [Test]
        public void NormalizeVelocity_Clamped_DoesNotExceedOne()
        {
            const float runSpeed = 6f;
            float normX = NormalizeVelocity(runSpeed * 2f, runSpeed);
            Assert.AreEqual(1f, normX, 1e-5f,
                "Velocity exceeding runSpeed should be clamped to 1.0");
        }
    }
}
