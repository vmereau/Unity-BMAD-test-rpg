using Game.Combat;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the shared stance meter — StanceMeter (spec-player-stance-stagger-knockdown).
/// Max 100, threshold 50 %, regen delay 2 s at 20/s, stagger 1 s, knockdown 4 s.
/// </summary>
public class StanceMeterTests
{
    private const float EPS = 0.001f;
    private static readonly StanceSettings Settings = new(100f, 0.5f, 2f, 20f, 1f, 4f);

    private static StanceMeter NewMeter()
    {
        var meter = new StanceMeter();
        meter.Reset(Settings);
        return meter;
    }

    [Test]
    public void Reset_FullStanceNoReaction()
    {
        StanceMeter m = NewMeter();
        Assert.That(m.Current, Is.EqualTo(100f));
        Assert.That(m.IsIncapacitated, Is.False);
    }

    [Test]
    public void Apply_CrossingThreshold_StaggersForDuration()
    {
        StanceMeter m = NewMeter();
        Assert.That(m.Apply(60f, false, Settings, out bool applied), Is.EqualTo(StanceReaction.Stagger));
        Assert.That(applied, Is.True);
        Assert.That(m.IsStaggered, Is.True);
        Assert.That(m.Tick(0.5f, Settings), Is.False);
        Assert.That(m.Tick(0.6f, Settings), Is.True);
        Assert.That(m.IsIncapacitated, Is.False);
        Assert.That(m.Current, Is.EqualTo(50f).Within(EPS), "a stagger does not refill");
    }

    [Test]
    public void Stagger_ReArmsOnlyAfterRegenAboveThreshold()
    {
        StanceMeter m = NewMeter();
        m.Apply(50f, false, Settings, out _);
        m.Tick(1.1f, Settings); // recover
        m.Apply(10f, true, Settings, out _);  // 40: still below the threshold
        Assert.That(m.IsStaggered, Is.False);
        m.Tick(1.9f, Settings); // delay not elapsed yet
        m.Tick(1f, Settings);  // +20 → 60
        Assert.That(m.Current, Is.EqualTo(60f).Within(EPS));
        Assert.That(m.Apply(10f, false, Settings, out _), Is.EqualTo(StanceReaction.Stagger));
    }

    [Test]
    public void NoRegenWhileStaggered()
    {
        StanceMeter m = NewMeter();
        m.Apply(50f, false, Settings, out _);
        m.Tick(0.9f, Settings);
        Assert.That(m.Current, Is.EqualTo(50f).Within(EPS));
    }

    [Test]
    public void KnockDown_IgnoresDamage_ThenRefills()
    {
        StanceMeter m = NewMeter();
        Assert.That(m.Apply(120f, true, Settings, out _), Is.EqualTo(StanceReaction.KnockDown));
        Assert.That(m.IsKnockedDown, Is.True);
        Assert.That(m.Apply(30f, true, Settings, out bool applied), Is.EqualTo(StanceReaction.None));
        Assert.That(applied, Is.False);
        Assert.That(m.Tick(4.1f, Settings), Is.True);
        Assert.That(m.Current, Is.EqualTo(100f));
        Assert.That(m.IsIncapacitated, Is.False);
    }

    [Test]
    public void NoKnockDownHit_AtThreshold_NotApplied()
    {
        StanceMeter m = NewMeter();
        m.Apply(50f, false, Settings, out _);
        m.Tick(1.1f, Settings);
        Assert.That(m.Apply(30f, false, Settings, out bool applied), Is.EqualTo(StanceReaction.None));
        Assert.That(applied, Is.False);
        Assert.That(m.Current, Is.EqualTo(50f).Within(EPS));
    }

    [Test]
    public void NoKnockDownHit_Overkill_StaggersAtThresholdNeverZero()
    {
        // Blocked player hits and unarmed NPC hits are applied with canKnockDown = false.
        StanceMeter m = NewMeter();
        Assert.That(m.Apply(500f, false, Settings, out _), Is.EqualTo(StanceReaction.Stagger));
        Assert.That(m.Current, Is.EqualTo(50f).Within(EPS));
        Assert.That(m.IsKnockedDown, Is.False);
    }

    [Test]
    public void Regen_WaitsForDelay()
    {
        StanceMeter m = NewMeter();
        m.Apply(20f, true, Settings, out _); // 80
        m.Tick(1.5f, Settings);
        Assert.That(m.Current, Is.EqualTo(80f).Within(EPS));
        m.Tick(1f, Settings); // delay passed during this tick → +20
        Assert.That(m.Current, Is.EqualTo(100f).Within(EPS));
    }

    [Test]
    public void ClearReaction_DropsWithoutRefill()
    {
        StanceMeter m = NewMeter();
        m.Apply(120f, true, Settings, out _);
        m.ClearReaction();
        Assert.That(m.IsIncapacitated, Is.False);
        Assert.That(m.Current, Is.EqualTo(0f));
    }
}
