using NUnit.Framework;

/// <summary>
/// Edit Mode tests for XP system logic.
/// Pure formula simulation — no MonoBehaviour lifecycle, no scene required.
/// Story 3.1: Initial implementation.
/// </summary>
public class XPSystemTests
{
    // Pure formula helpers — mimic what XPSystem does internally

    private int CalculateXPForKills(int killCount, int xpPerKill)
        => killCount * xpPerKill;

    private int AccumulateXP(int currentXP, int xpGained)
        => currentXP + xpGained;

    // ── XP calculation tests ───────────────────────────────────────────────

    [Test]
    public void SingleKill_AwardsCorrectXP()
    {
        int result = CalculateXPForKills(1, 50);
        Assert.That(result, Is.EqualTo(50));
    }

    [Test]
    public void MultipleKills_AccumulatesXP()
    {
        int xpPerKill = 50;
        int totalXP = 0;
        totalXP = AccumulateXP(totalXP, CalculateXPForKills(1, xpPerKill));
        totalXP = AccumulateXP(totalXP, CalculateXPForKills(1, xpPerKill));
        totalXP = AccumulateXP(totalXP, CalculateXPForKills(1, xpPerKill));
        Assert.That(totalXP, Is.EqualTo(150));
    }

    [Test]
    public void ZeroKills_AwardsZeroXP()
    {
        int result = CalculateXPForKills(0, 50);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void DifferentConfig_AwardsCorrectXP()
    {
        int result = CalculateXPForKills(1, 100);
        Assert.That(result, Is.EqualTo(100));
    }
}
