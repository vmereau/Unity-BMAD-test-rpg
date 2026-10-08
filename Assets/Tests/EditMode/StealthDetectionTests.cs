using Game.Stealth;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure vision-cone / awareness math in <see cref="StealthDetection"/>
    /// (used by EntityPerception). Values mirror the spec's tuning start points.
    /// </summary>
    public class StealthDetectionTests
    {
        private const float EPS = 0.0001f;
        private const float SIGHT = 8f;
        private const float VIEW = 110f;
        private const float PROX = 2f;
        private const float EDGE = 0.2f;
        private const float PERIPH = 0.5f;

        private static float Vis(float distance, float angle, float sight = SIGHT, float view = VIEW, float prox = PROX) =>
            StealthDetection.ComputeVisibility(distance, angle, sight, view, prox, EDGE, PERIPH);

        // --- AngleToTarget ---

        [Test]
        public void AngleToTarget_StraightAhead_IsZero() =>
            Assert.That(StealthDetection.AngleToTarget(Vector3.forward, new Vector3(0, 0, 5)), Is.EqualTo(0f).Within(EPS));

        [Test]
        public void AngleToTarget_DirectlyBehind_Is180() =>
            Assert.That(StealthDetection.AngleToTarget(Vector3.forward, new Vector3(0, 0, -5)), Is.EqualTo(180f).Within(EPS));

        [Test]
        public void AngleToTarget_IgnoresY() =>
            Assert.That(StealthDetection.AngleToTarget(Vector3.forward, new Vector3(0, 10, 5)), Is.EqualTo(0f).Within(EPS));

        [Test]
        public void AngleToTarget_ZeroVector_IsZero() =>
            Assert.That(StealthDetection.AngleToTarget(Vector3.forward, Vector3.zero), Is.EqualTo(0f));

        // --- ComputeVisibility ---

        [Test]
        public void ComputeVisibility_InsideProximity_IsOne_AtAnyAngle() =>
            Assert.That(Vis(1.5f, 180f), Is.EqualTo(1f));

        [Test]
        public void ComputeVisibility_OutsideRange_IsZero() =>
            Assert.That(Vis(8.5f, 0f), Is.EqualTo(0f));

        [Test]
        public void ComputeVisibility_OutsideHalfAngle_IsZero() =>
            Assert.That(Vis(4f, 56f), Is.EqualTo(0f));

        [Test]
        public void ComputeVisibility_CentreAtDistanceZero_IsOne() =>
            Assert.That(Vis(0f, 0f, prox: 0f), Is.EqualTo(1f).Within(EPS));

        [Test]
        public void ComputeVisibility_FarEdgeOnAxis_IsEdgeFactor() =>
            Assert.That(Vis(SIGHT, 0f), Is.EqualTo(EDGE).Within(EPS));

        [Test]
        public void ComputeVisibility_SideEdgeAtDistanceZero_IsPeripheralFactor() =>
            Assert.That(Vis(0f, VIEW / 2f, prox: 0f), Is.EqualTo(PERIPH).Within(EPS));

        [Test]
        public void ComputeVisibility_DecreasesWithDistance()
        {
            float previous = float.MaxValue;
            for (float d = 2.5f; d <= SIGHT; d += 0.5f)
            {
                float v = Vis(d, 10f);
                Assert.That(v, Is.LessThan(previous));
                previous = v;
            }
        }

        [Test]
        public void ComputeVisibility_DecreasesWithAngle()
        {
            float previous = float.MaxValue;
            for (float a = 0f; a <= VIEW / 2f; a += 5f)
            {
                float v = Vis(4f, a);
                Assert.That(v, Is.LessThan(previous));
                previous = v;
            }
        }

        [Test]
        public void ComputeVisibility_FullCircleView_SeesBehind() =>
            Assert.That(Vis(4f, 180f, view: 360f), Is.GreaterThan(0f));

        // --- Sneak modifiers ---

        [Test]
        public void EffectiveSightRange_StandingUnchanged_SneakingMultiplied()
        {
            Assert.That(StealthDetection.EffectiveSightRange(8f, false, 0.6f), Is.EqualTo(8f));
            Assert.That(StealthDetection.EffectiveSightRange(8f, true, 0.6f), Is.EqualTo(4.8f).Within(EPS));
        }

        [Test]
        public void EffectiveProximityRadius_StandingUnchanged_SneakingMultiplied()
        {
            Assert.That(StealthDetection.EffectiveProximityRadius(2f, false, 0.35f), Is.EqualTo(2f));
            Assert.That(StealthDetection.EffectiveProximityRadius(2f, true, 0.35f), Is.EqualTo(0.7f).Within(EPS));
        }

        // --- FillPerSecond ---

        [Test]
        public void FillPerSecond_StandingFullVisibility_IsOnePerSecond() =>
            Assert.That(StealthDetection.FillPerSecond(1f, 1f, false, 0.5f), Is.EqualTo(1f).Within(EPS));

        [Test]
        public void FillPerSecond_Sneaking_IsHalved() =>
            Assert.That(StealthDetection.FillPerSecond(1f, 1f, true, 0.5f), Is.EqualTo(0.5f).Within(EPS));

        [Test]
        public void FillPerSecond_ZeroFillTime_IsZero() =>
            Assert.That(StealthDetection.FillPerSecond(1f, 0f, false, 0.5f), Is.EqualTo(0f));

        // --- StepAwareness ---

        [Test]
        public void StepAwareness_FillsWhenVisible() =>
            Assert.That(StealthDetection.StepAwareness(0.2f, 1f, 0.25f, true, 0.1f), Is.EqualTo(0.3f).Within(EPS));

        [Test]
        public void StepAwareness_DrainsWhenNotVisible() =>
            Assert.That(StealthDetection.StepAwareness(0.5f, 1f, 0.25f, false, 1f), Is.EqualTo(0.25f).Within(EPS));

        [Test]
        public void StepAwareness_ClampsToZeroAndOne()
        {
            Assert.That(StealthDetection.StepAwareness(0.95f, 1f, 0.25f, true, 1f), Is.EqualTo(1f));
            Assert.That(StealthDetection.StepAwareness(0.1f, 1f, 0.25f, false, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void StepAwareness_VisibleWithZeroFill_Drains() =>
            Assert.That(StealthDetection.StepAwareness(0.5f, 0f, 0.25f, true, 1f), Is.EqualTo(0.25f).Within(EPS));

        // --- Scenario (AC 13) ---

        [Test]
        public void Scenario_SneakingAtSixMetres_Invisible_StandingVisible()
        {
            float sneakSight = StealthDetection.EffectiveSightRange(SIGHT, true, 0.6f);
            float sneakProx = StealthDetection.EffectiveProximityRadius(PROX, true, 0.35f);
            Assert.That(Vis(6f, 0f, sight: sneakSight, prox: sneakProx), Is.EqualTo(0f));
            Assert.That(Vis(6f, 0f), Is.GreaterThan(0f));
        }

        // --- ComputeWitnessVisibility ---

        private const float WITNESS = 6f;

        private static float WitnessVis(bool sneaking, float distance, float angle) =>
            StealthDetection.ComputeWitnessVisibility(sneaking, distance, angle, WITNESS, VIEW, PROX, EDGE, PERIPH);

        [Test]
        public void WitnessVisibility_NotSneaking_IsZero()
        {
            Assert.That(WitnessVis(false, 3f, 0f), Is.EqualTo(0f));
            Assert.That(WitnessVis(false, 0.5f, 180f), Is.EqualTo(0f)); // even inside the proximity radius
        }

        [Test]
        public void WitnessVisibility_Sneaking_EqualsComputeVisibility()
        {
            foreach (var (d, a) in new[] { (0.5f, 180f), (3f, 0f), (5f, 40f), (7f, 0f), (3f, 70f) })
                Assert.That(WitnessVis(true, d, a), Is.EqualTo(Vis(d, a, sight: WITNESS)).Within(EPS), $"d {d}, a {a}");
        }
    }
}
