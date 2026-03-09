using NUnit.Framework;
using Game.Combat;

/// <summary>
/// Edit Mode tests for perfect block logic used by PlayerCombat.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests and ComboWindowTests.
/// Requires Tests.EditMode.asmdef to reference "Game" assembly (established in Story 1.5).
/// </summary>
public class PerfectBlockTests
{
    // Simulates the TryReceiveHit formula
    private HitResult SimulateHit(bool isBlocking, bool isPerfectWindowOpen,
                                   float stamina, float blockCostPerHit)
    {
        if (!isBlocking) return HitResult.NotBlocked;
        if (isPerfectWindowOpen) return HitResult.PerfectBlock;
        if (stamina >= blockCostPerHit) return HitResult.Blocked;
        return HitResult.NotBlocked; // Block broken by stamina depletion
    }

    [Test]
    public void Hit_ReturnsPerfectBlock_WhenBlockingAndWindowOpen()
    {
        Assert.That(SimulateHit(true, true, 100f, 15f), Is.EqualTo(HitResult.PerfectBlock));
    }

    [Test]
    public void Hit_ReturnsPerfectBlock_EvenAtZeroStamina_WhenWindowOpen()
    {
        // Perfect block costs NO stamina — window open overrides stamina check
        Assert.That(SimulateHit(true, true, 0f, 15f), Is.EqualTo(HitResult.PerfectBlock));
    }

    [Test]
    public void Hit_ReturnsBlocked_WhenBlockingWindowClosedAndHasStamina()
    {
        Assert.That(SimulateHit(true, false, 100f, 15f), Is.EqualTo(HitResult.Blocked));
        Assert.That(SimulateHit(true, false, 15f, 15f), Is.EqualTo(HitResult.Blocked)); // exact cost
    }

    [Test]
    public void Hit_ReturnsNotBlocked_WhenBlockBrokenByStaminaDepletion()
    {
        Assert.That(SimulateHit(true, false, 5f, 15f), Is.EqualTo(HitResult.NotBlocked));
        Assert.That(SimulateHit(true, false, 0f, 15f), Is.EqualTo(HitResult.NotBlocked)); // zero stamina
    }

    [Test]
    public void Hit_ReturnsNotBlocked_WhenNotBlocking()
    {
        Assert.That(SimulateHit(false, false, 100f, 15f), Is.EqualTo(HitResult.NotBlocked));
        Assert.That(SimulateHit(false, true, 100f, 15f), Is.EqualTo(HitResult.NotBlocked)); // window open but not blocking
    }
}
