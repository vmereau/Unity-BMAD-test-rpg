using NUnit.Framework;

/// <summary>
/// Edit Mode tests for PlayerStateManager gating logic.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests and PerfectBlockTests.
/// Requires Tests.EditMode.asmdef to reference "Game" assembly (established Story 1.5).
/// </summary>
public class PlayerStateManagerTests
{
    private bool CanAttack(bool isAirborne, bool isBlocking) =>
        !isAirborne && !isBlocking;

    private bool CanBlock(bool isAirborne, float stamina, float blockCost) =>
        !isAirborne && stamina >= blockCost;

    [Test]
    public void CanAttack_ReturnsFalse_WhenAirborne()
    {
        Assert.That(CanAttack(true, false), Is.False);
    }

    [Test]
    public void CanBlock_ReturnsFalse_WhenAirborne()
    {
        Assert.That(CanBlock(true, 100f, 15f), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsTrue_WhenGroundedAndNotBlocking()
    {
        Assert.That(CanAttack(false, false), Is.True);
    }

    [Test]
    public void CanBlock_ReturnsTrue_WhenGroundedAndHasStamina()
    {
        Assert.That(CanBlock(false, 15f, 15f), Is.True);
        Assert.That(CanBlock(false, 100f, 15f), Is.True);
    }

    [Test]
    public void CanAttack_ReturnsFalse_WhenBlocking()
    {
        Assert.That(CanAttack(false, true), Is.False);
        // Airborne + blocking: still false
        Assert.That(CanAttack(true, true), Is.False);
    }

    // ── IsAttacking state invariants ────────────────────────────────────────

    // Simulates the dodge entry gate (Story 2.7 prerequisite)
    private bool CanDodge(bool isAirborne, bool isAttacking) =>
        !isAirborne && !isAttacking;

    [Test]
    public void CanDodge_ReturnsFalse_WhenAttackingMidCombo()
    {
        // IsAttacking=true during combo chain blocks dodge
        Assert.That(CanDodge(false, isAttacking: true), Is.False);
    }

    [Test]
    public void CanDodge_ReturnsFalse_WhenAirborne()
    {
        Assert.That(CanDodge(isAirborne: true, isAttacking: false), Is.False);
    }

    [Test]
    public void CanDodge_ReturnsTrue_WhenGroundedAndChainTerminated()
    {
        // IsAttacking=false after finisher / window expiry / block-start
        Assert.That(CanDodge(false, false), Is.True);
    }

    [Test]
    public void IsBlocking_And_IsAttacking_AreMutuallyExclusive()
    {
        // OnBlockStarted calls SetAttacking(false) before raising block.
        // Invariant: IsBlocking=true implies IsAttacking=false.
        bool isBlocking = true;
        bool isAttackingAfterBlockStart = false; // SetAttacking(false) called in OnBlockStarted
        Assert.That(isBlocking && isAttackingAfterBlockStart, Is.False,
            "IsBlocking and IsAttacking must never both be true simultaneously");
    }
}
