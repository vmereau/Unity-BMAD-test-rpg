using Game.Combat;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the arrow drop curve — ArrowFlightMath.
/// </summary>
public class ArrowFlightMathTests
{
    private const float EPS = 0.001f;

    [Test]
    public void GravityFactor_BeforeDelay_IsZero()
        => Assert.That(ArrowFlightMath.GravityFactor(0.2f, 0.25f, 0.5f), Is.EqualTo(0f));

    [Test]
    public void GravityFactor_AtDelay_IsZero()
        => Assert.That(ArrowFlightMath.GravityFactor(0.25f, 0.25f, 0.5f), Is.EqualTo(0f));

    [Test]
    public void GravityFactor_MidRamp_IsLinear()
        => Assert.That(ArrowFlightMath.GravityFactor(0.5f, 0.25f, 0.5f), Is.EqualTo(0.5f).Within(EPS));

    [Test]
    public void GravityFactor_AfterRamp_IsOne()
        => Assert.That(ArrowFlightMath.GravityFactor(2f, 0.25f, 0.5f), Is.EqualTo(1f));

    [Test]
    public void GravityFactor_ZeroRamp_IsInstantAfterDelay()
    {
        Assert.That(ArrowFlightMath.GravityFactor(0.24f, 0.25f, 0f), Is.EqualTo(0f));
        Assert.That(ArrowFlightMath.GravityFactor(0.26f, 0.25f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void GravityFactor_NegativeDelay_TreatedAsZero()
        => Assert.That(ArrowFlightMath.GravityFactor(0.25f, -1f, 0.5f), Is.EqualTo(0.5f).Within(EPS));
}
