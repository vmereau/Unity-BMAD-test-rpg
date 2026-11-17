using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for RangedAimMath (spec-ai-ranged-combat-style): lead point and velocity smoothing.</summary>
public class RangedAimMathTests
{
    private const float EPS = 1e-4f;

    [Test]
    public void StillTarget_AimsAtIt()
    {
        Vector3 p = RangedAimMath.LeadPoint(Vector3.zero, new Vector3(0, 1, 10), Vector3.zero, 40f, 1f);
        Assert.AreEqual(new Vector3(0, 1, 10), p);
    }

    [Test]
    public void FullLead_IsVelocityTimesFlightTime()
    {
        // 10 m at 40 m/s = 0.25 s; 4 m/s sideways → 1 m ahead.
        Vector3 p = RangedAimMath.LeadPoint(Vector3.zero, new Vector3(0, 0, 10), new Vector3(4, 0, 0), 40f, 1f);
        Assert.AreEqual(1f, p.x, EPS);
        Assert.AreEqual(10f, p.z, EPS);
    }

    [Test]
    public void LeadFactor_ScalesTheLead()
    {
        Vector3 p = RangedAimMath.LeadPoint(Vector3.zero, new Vector3(0, 0, 10), new Vector3(4, 0, 0), 40f, 0.5f);
        Assert.AreEqual(0.5f, p.x, EPS);
    }

    [Test]
    public void VerticalVelocity_Ignored()
    {
        Vector3 p = RangedAimMath.LeadPoint(Vector3.zero, new Vector3(0, 1, 10), new Vector3(0, 5, 0), 40f, 1f);
        Assert.AreEqual(1f, p.y, EPS);
    }

    [Test]
    public void NoSpeedOrNoFactor_NoLead()
    {
        Vector3 target = new Vector3(0, 0, 10);
        Assert.AreEqual(target, RangedAimMath.LeadPoint(Vector3.zero, target, Vector3.right * 4f, 0f, 1f));
        Assert.AreEqual(target, RangedAimMath.LeadPoint(Vector3.zero, target, Vector3.right * 4f, 40f, 0f));
    }

    [Test]
    public void SmoothVelocity_NoSmoothing_IsMeasured()
    {
        Vector3 v = RangedAimMath.SmoothVelocity(Vector3.zero, Vector3.zero, new Vector3(1, 0, 0), 0.5f, 0f);
        Assert.AreEqual(2f, v.x, EPS);
    }

    [Test]
    public void SmoothVelocity_OneTimeConstant_ReachesAboutTwoThirds()
    {
        // dt = smoothTime → 1 − e⁻¹ ≈ 0.632 of the way.
        Vector3 v = RangedAimMath.SmoothVelocity(Vector3.zero, Vector3.zero, new Vector3(1, 0, 0), 0.5f, 0.5f);
        Assert.AreEqual(2f * (1f - Mathf.Exp(-1f)), v.x, EPS);
    }

    [Test]
    public void SmoothVelocity_FrameRateIndependent()
    {
        // 1 s of a steady 2 m/s target in 10 × 0.1 s or 100 × 0.01 s steps lands on the same estimate.
        Vector3 coarse = Vector3.zero, fine = Vector3.zero;
        for (int i = 0; i < 10; i++) coarse = RangedAimMath.SmoothVelocity(coarse, Vector3.zero, new Vector3(0.2f, 0, 0), 0.1f, 0.3f);
        for (int i = 0; i < 100; i++) fine = RangedAimMath.SmoothVelocity(fine, Vector3.zero, new Vector3(0.02f, 0, 0), 0.01f, 0.3f);
        Assert.AreEqual(coarse.x, fine.x, 1e-3f);
    }

    [Test]
    public void SmoothVelocity_ZeroDt_Unchanged()
    {
        Vector3 v = RangedAimMath.SmoothVelocity(Vector3.one, Vector3.zero, Vector3.right, 0f, 0.5f);
        Assert.AreEqual(Vector3.one, v);
    }
}
