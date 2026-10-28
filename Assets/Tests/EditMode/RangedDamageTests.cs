using Game.Combat;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for RangedDamage — bow damage = base + max(0, (Dex - baseDex) * perDex) + weapon bonus.
/// </summary>
public class RangedDamageTests
{
    private const float TOLERANCE = 1e-4f;

    [Test]
    public void Compute_DexAtBase_ReturnsBaseDamage()
    {
        Assert.AreEqual(25f, RangedDamage.Compute(25f, 5, 5, 2f, 0f), TOLERANCE);
    }

    [Test]
    public void Compute_DexAboveBase_AddsPerPointBonus()
    {
        Assert.AreEqual(31f, RangedDamage.Compute(25f, 8, 5, 2f, 0f), TOLERANCE);
    }

    [Test]
    public void Compute_DexBelowBase_NeverReducesDamage()
    {
        Assert.AreEqual(25f, RangedDamage.Compute(25f, 2, 5, 2f, 0f), TOLERANCE);
    }

    [Test]
    public void Compute_WeaponBonus_IsAddedFlat()
    {
        Assert.AreEqual(39f, RangedDamage.Compute(25f, 7, 5, 2f, 10f), TOLERANCE);
    }

    [Test]
    public void Compute_NegativeTotal_ClampsToZero()
    {
        Assert.AreEqual(0f, RangedDamage.Compute(5f, 5, 5, 2f, -20f), TOLERANCE);
    }
}
