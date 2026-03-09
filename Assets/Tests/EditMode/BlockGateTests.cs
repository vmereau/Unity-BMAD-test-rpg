using NUnit.Framework;

/// <summary>
/// Edit Mode tests for block gate logic used by PlayerCombat.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors ComboWindowTests and PlayerCombatGateTests.
/// </summary>
public class BlockGateTests
{
    // Simulates the block entry gate formula
    private bool CanEnterBlock(float currentStamina, float blockCostPerHit)
    {
        return currentStamina >= blockCostPerHit;
    }

    // Simulates the attack-while-blocking gate
    private bool CanAttack(bool isBlocking)
    {
        return !isBlocking;
    }

    [Test]
    public void CanEnterBlock_ReturnsFalse_WhenStaminaIsZero()
    {
        Assert.That(CanEnterBlock(0f, 15f), Is.False);
    }

    [Test]
    public void CanEnterBlock_ReturnsTrue_WhenStaminaSufficient()
    {
        Assert.That(CanEnterBlock(15f, 15f), Is.True);
        Assert.That(CanEnterBlock(100f, 15f), Is.True);
    }

    [Test]
    public void CanEnterBlock_ReturnsFalse_WhenStaminaBelowCost()
    {
        Assert.That(CanEnterBlock(14f, 15f), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsFalse_WhenBlocking()
    {
        Assert.That(CanAttack(true), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsTrue_WhenNotBlocking()
    {
        Assert.That(CanAttack(false), Is.True);
    }
}
