using Game.Inventory;
using Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class ActionRootMotionMathTests
    {
        private const float TOLERANCE = 1e-6f;

        [Test]
        public void Filter_StripsTheVerticalPart()
        {
            Vector3 result = ActionRootMotionMath.Filter(new Vector3(0.1f, 0.5f, 0.2f), 1f);
            Assert.That(result.x, Is.EqualTo(0.1f).Within(TOLERANCE));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0.2f).Within(TOLERANCE));
        }

        [Test]
        public void Filter_AppliesTheScale()
        {
            Vector3 result = ActionRootMotionMath.Filter(new Vector3(0.1f, 0f, -0.2f), 0.5f);
            Assert.That(result.x, Is.EqualTo(0.05f).Within(TOLERANCE));
            Assert.That(result.z, Is.EqualTo(-0.1f).Within(TOLERANCE));
        }

        [Test]
        public void Filter_ZeroScale_DoesNotMove()
            => Assert.That(ActionRootMotionMath.Filter(new Vector3(0.1f, 0.2f, 0.3f), 0f), Is.EqualTo(Vector3.zero));

        [Test]
        public void Filter_NegativeScale_DoesNotMove()
            => Assert.That(ActionRootMotionMath.Filter(new Vector3(0.1f, 0.2f, 0.3f), -1f), Is.EqualTo(Vector3.zero));

        [Test]
        public void AttackStep_DefaultsToTheScriptedStep()
        {
            var step = new AttackStep(0.3f, 0.1f, 0.4f);
            Assert.That(step.UsesRootMotion, Is.False);
        }

        [Test]
        public void AttackStep_WithAScale_UsesRootMotion()
        {
            var step = new AttackStep(0.4f, 0.1f, 0.4f, 1f);
            Assert.That(step.UsesRootMotion, Is.True);
        }
    }
}
