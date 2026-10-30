using Game.Combat;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the AI stance rules — StanceMath (spec-ai-stance-stagger-knockdown).
/// Max 100, threshold 50 % unless stated.
/// </summary>
public class StanceMathTests
{
    private const float EPS = 0.001f;

    [Test]
    public void Apply_CrossingThreshold_Staggers()
    {
        StanceResult r = StanceMath.Apply(60f, 100f, 0.5f, 20f, false);
        Assert.That(r.Stance, Is.EqualTo(50f).Within(EPS));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.Stagger));
    }

    [Test]
    public void Apply_AboveThreshold_NoReaction()
    {
        StanceResult r = StanceMath.Apply(100f, 100f, 0.5f, 20f, true);
        Assert.That(r.Stance, Is.EqualTo(80f).Within(EPS));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.None));
    }

    [Test]
    public void Apply_KnockDownWeaponReachingZero_KnocksDownWithoutStagger()
    {
        StanceResult r = StanceMath.Apply(60f, 100f, 0.5f, 70f, true);
        Assert.That(r.Stance, Is.EqualTo(0f));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.KnockDown));
    }

    [Test]
    public void Apply_NoKnockDown_ClampsAtThreshold()
    {
        StanceResult r = StanceMath.Apply(100f, 100f, 0.5f, 80f, false);
        Assert.That(r.Stance, Is.EqualTo(50f).Within(EPS));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.Stagger));
    }

    [Test]
    public void Apply_NoKnockDown_AlreadyBelowThreshold_DealsNothing()
    {
        StanceResult r = StanceMath.Apply(40f, 100f, 0.5f, 30f, false);
        Assert.That(r.Stance, Is.EqualTo(40f).Within(EPS));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.None));
    }

    [Test]
    public void Apply_BelowThreshold_KnockDownWeapon_DrainsWithoutSecondStagger()
    {
        StanceResult r = StanceMath.Apply(40f, 100f, 0.5f, 30f, true);
        Assert.That(r.Stance, Is.EqualTo(10f).Within(EPS));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.None));
    }

    [Test]
    public void Apply_ReArmsAfterRegenAboveThreshold()
    {
        float regen = StanceMath.Regen(45f, 100f, 10f, 1f); // 55 — back above 50
        StanceResult r = StanceMath.Apply(regen, 100f, 0.5f, 10f, false);
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.Stagger));
    }

    [Test]
    public void Apply_ZeroDamage_NoChange()
    {
        StanceResult r = StanceMath.Apply(60f, 100f, 0.5f, 0f, true);
        Assert.That(r.Stance, Is.EqualTo(60f));
        Assert.That(r.Reaction, Is.EqualTo(StanceReaction.None));
    }

    [Test]
    public void Apply_AlreadyZero_NoSecondKnockDown()
        => Assert.That(StanceMath.Apply(0f, 100f, 0.5f, 30f, true).Reaction, Is.EqualTo(StanceReaction.None));

    [Test]
    public void Regen_ClampsAtMax()
        => Assert.That(StanceMath.Regen(95f, 100f, 20f, 1f), Is.EqualTo(100f));

    [Test]
    public void ApplyResistance_HalvesDamage()
        => Assert.That(StanceMath.ApplyResistance(20f, 0.5f), Is.EqualTo(10f).Within(EPS));

    [Test]
    public void ApplyResistance_ClampsAboveOne()
        => Assert.That(StanceMath.ApplyResistance(20f, 2f), Is.EqualTo(0f).Within(EPS));

    [Test]
    public void ComputeMeleeStanceDamage_AllBonuses()
        => Assert.That(StanceMath.ComputeMeleeStanceDamage(20f, 10f, 1f, true, 1.5f, 5f), Is.EqualTo(50f).Within(EPS));

    [Test]
    public void ComputeMeleeStanceDamage_NegativeStrength_ClampedToBase()
        => Assert.That(StanceMath.ComputeMeleeStanceDamage(20f, -5f, 1f, false, 1.5f, 0f), Is.EqualTo(20f).Within(EPS));
}
