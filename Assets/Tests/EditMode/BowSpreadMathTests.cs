using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the bow aim spread math — BowSpreadMath.
/// </summary>
public class BowSpreadMathTests
{
    private const float EPS = 0.001f;

    [Test]
    public void StepProgress_Still_ShrinksLinearly()
        => Assert.That(BowSpreadMath.StepProgress(0f, 0.5f, 1.5f, false, 1f), Is.EqualTo(1f / 3f).Within(EPS));

    [Test]
    public void StepProgress_Still_ClampsAtOne()
        => Assert.That(BowSpreadMath.StepProgress(0.9f, 1f, 1.5f, false, 1f), Is.EqualTo(1f));

    [Test]
    public void StepProgress_Moving_WidensAtMultipliedRate()
        => Assert.That(BowSpreadMath.StepProgress(1f, 0.5f, 1.5f, true, 2f), Is.EqualTo(1f / 3f).Within(EPS));

    [Test]
    public void StepProgress_Moving_ClampsAtZero()
        => Assert.That(BowSpreadMath.StepProgress(0.1f, 1f, 1.5f, true, 1f), Is.EqualTo(0f));

    [Test]
    public void StepProgress_MovingWithZeroMultiplier_Holds()
        => Assert.That(BowSpreadMath.StepProgress(0.4f, 1f, 1.5f, true, 0f), Is.EqualTo(0.4f).Within(EPS));

    [Test]
    public void StepProgress_ZeroDt_Unchanged()
        => Assert.That(BowSpreadMath.StepProgress(0.4f, 0f, 1.5f, false, 1f), Is.EqualTo(0.4f).Within(EPS));

    [Test]
    public void StepProgress_ZeroDuration_StillIsPerfect_MovingWithoutWidenHolds()
    {
        Assert.That(BowSpreadMath.StepProgress(0.2f, 0.1f, 0f, false, 1f), Is.EqualTo(1f));
        Assert.That(BowSpreadMath.StepProgress(0.2f, 0.1f, 0f, true, 1f), Is.EqualTo(0f));
        Assert.That(BowSpreadMath.StepProgress(0.2f, 0.1f, 0f, true, 0f), Is.EqualTo(0.2f).Within(EPS));
    }

    [Test]
    public void StepProgress_ZeroDtWithZeroDuration_Unchanged()
        => Assert.That(BowSpreadMath.StepProgress(0.2f, 0f, 0f, false, 1f), Is.EqualTo(0.2f).Within(EPS));

    [Test]
    public void ClampToCone_InsideCone_Unchanged()
    {
        Vector3 dir = Quaternion.Euler(0f, 2f, 0f) * Vector3.forward * 10f;
        Vector3 result = BowSpreadMath.ClampToCone(Vector3.forward, dir, 5f);
        Assert.That(Vector3.Distance(dir, result), Is.LessThan(EPS));
    }

    [Test]
    public void ClampToCone_OutsideCone_PulledToEdge_KeepsLength()
    {
        Vector3 dir = Quaternion.Euler(0f, 40f, 0f) * Vector3.forward * 10f;
        Vector3 result = BowSpreadMath.ClampToCone(Vector3.forward, dir, 5f);
        Assert.That(Vector3.Angle(Vector3.forward, result), Is.EqualTo(5f).Within(0.01f));
        Assert.That(result.magnitude, Is.EqualTo(10f).Within(EPS));
        Assert.That(result.x, Is.GreaterThan(0f)); // same side as the input
    }

    [Test]
    public void SpreadAngle_LerpsFromMaxToZero()
    {
        Assert.That(BowSpreadMath.SpreadAngle(0f, 5f), Is.EqualTo(5f).Within(EPS));
        Assert.That(BowSpreadMath.SpreadAngle(0.5f, 5f), Is.EqualTo(2.5f).Within(EPS));
        Assert.That(BowSpreadMath.SpreadAngle(1f, 5f), Is.EqualTo(0f).Within(EPS));
    }

    [Test]
    public void SampleCone_ZeroRadius_IsCentreRay()
    {
        Vector3 dir = new Vector3(0.3f, -0.2f, 1f);
        Vector3 sample = BowSpreadMath.SampleCone(dir, 5f, 0f, 0.37f);
        Assert.That(Vector3.Angle(dir, sample), Is.LessThan(0.01f));
    }

    [Test]
    public void SampleCone_ZeroAngle_IsCentreRay()
    {
        Vector3 sample = BowSpreadMath.SampleCone(Vector3.forward, 0f, 1f, 0.5f);
        Assert.That(Vector3.Angle(Vector3.forward, sample), Is.LessThan(0.01f));
    }

    [Test]
    public void SampleCone_FullRadius_IsOnTheConeEdge()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 sample = BowSpreadMath.SampleCone(Vector3.forward, 5f, 1f, i / 8f);
            Assert.That(Vector3.Angle(Vector3.forward, sample), Is.EqualTo(5f).Within(0.01f));
        }
    }

    [Test]
    public void SampleCone_StaysInsideTheCone()
    {
        Vector3 dir = new Vector3(-0.5f, 0.4f, 0.2f);
        for (int i = 0; i <= 10; i++)
        for (int j = 0; j < 10; j++)
        {
            Vector3 sample = BowSpreadMath.SampleCone(dir, 4f, i / 10f, j / 10f);
            Assert.That(Vector3.Angle(dir, sample), Is.LessThanOrEqualTo(4f + 0.01f));
            Assert.That(sample.magnitude, Is.EqualTo(1f).Within(EPS));
        }
    }

    [Test]
    public void SampleCone_UniformDisk_HalfAreaAtSqrtHalfRadius()
    {
        // u = 0.5 → half the disk area → radius = angle × sqrt(0.5).
        Vector3 sample = BowSpreadMath.SampleCone(Vector3.forward, 4f, 0.5f, 0.2f);
        Assert.That(Vector3.Angle(Vector3.forward, sample), Is.EqualTo(4f * Mathf.Sqrt(0.5f)).Within(0.01f));
    }

    [Test]
    public void SampleCone_VerticalDirection_StaysValid()
    {
        Vector3 sample = BowSpreadMath.SampleCone(Vector3.up, 3f, 1f, 0.25f);
        Assert.That(Vector3.Angle(Vector3.up, sample), Is.EqualTo(3f).Within(0.01f));
    }

    [Test]
    public void ScreenRadius_HalfFovAngle_IsHalfScreenHeight()
        => Assert.That(BowSpreadMath.ScreenRadius(20f, 40f, 1080f), Is.EqualTo(540f).Within(0.1f));

    [Test]
    public void ScreenRadius_GrowsWhenFovNarrows()
    {
        float wide = BowSpreadMath.ScreenRadius(5f, 40f, 1080f);
        float zoomed = BowSpreadMath.ScreenRadius(5f, 30f, 1080f);
        Assert.That(zoomed, Is.GreaterThan(wide));
    }

    [Test]
    public void ScreenRadius_ZeroAngle_IsZero()
        => Assert.That(BowSpreadMath.ScreenRadius(0f, 40f, 1080f), Is.EqualTo(0f));
}
