using Game.Combat;
using NUnit.Framework;

public class KnockoutRulesTests
{
    [TestCase(true, true, false, true)]   // player vs neutral NPC
    [TestCase(true, true, true, false)]   // player vs bandit
    [TestCase(false, true, false, false)] // monster vs player
    [TestCase(true, false, false, false)] // player vs non-humanoid
    [TestCase(false, false, true, false)]
    public void IsNonLethal_TruthTable(bool attackerHumanoid, bool victimHumanoid, bool hostile, bool expected)
    {
        Assert.AreEqual(expected, KnockoutRules.IsNonLethal(attackerHumanoid, victimHumanoid, hostile));
    }

    [Test]
    public void RecoveryHealth_StartsAtZero()
    {
        Assert.AreEqual(0f, KnockoutRules.RecoveryHealth(100f, 0.15f, 0f, 8f), 1e-4f);
    }

    [Test]
    public void RecoveryHealth_IsLinear()
    {
        Assert.AreEqual(7.5f, KnockoutRules.RecoveryHealth(100f, 0.15f, 4f, 8f), 1e-4f);
    }

    [Test]
    public void RecoveryHealth_ClampsAfterDuration()
    {
        Assert.AreEqual(15f, KnockoutRules.RecoveryHealth(100f, 0.15f, 20f, 8f), 1e-4f);
    }

    [Test]
    public void RecoveryHealth_ClampsNegativeElapsedAndFraction()
    {
        Assert.AreEqual(0f, KnockoutRules.RecoveryHealth(100f, 0.15f, -1f, 8f), 1e-4f);
        Assert.AreEqual(100f, KnockoutRules.RecoveryHealth(100f, 2f, 8f, 8f), 1e-4f);
    }

    [Test]
    public void RecoveryHealth_ZeroDuration_ReturnsTarget()
    {
        Assert.AreEqual(15f, KnockoutRules.RecoveryHealth(100f, 0.15f, 0f, 0f), 1e-4f);
    }
}
