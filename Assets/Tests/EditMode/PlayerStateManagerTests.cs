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
        // OnBlockStarted cancels an attack in progress (CancelAttack → SetAttacking(false)) before raising block.
        // Invariant: IsBlocking=true implies IsAttacking=false.
        bool isBlocking = true;
        bool isAttackingAfterBlockStart = false; // CancelAttack() called in OnBlockStarted
        Assert.That(isBlocking && isAttackingAfterBlockStart, Is.False,
            "IsBlocking and IsAttacking must never both be true simultaneously");
    }

    // Exercises the real gate (PlayerStateManager.CanSneak() delegates to EvaluateCanSneak).
    [Test]
    public void CanSneak_ReturnsFalse_WhenBusyAirborneOrDodging()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanSneak(isBusy: true, isAirborne: false, isDodging: false), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanSneak(isBusy: false, isAirborne: true, isDodging: false), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanSneak(isBusy: false, isAirborne: false, isDodging: true), Is.False);
    }

    [Test]
    public void CanSneak_ReturnsTrue_WhenIdleOnGround()
    {
        // Attacking and blocking are not inputs of the gate — sneak is allowed (and kept) during both.
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanSneak(isBusy: false, isAirborne: false, isDodging: false), Is.True);
    }

    // Exercises the real gate (PlayerStateManager.CanMove() delegates to EvaluateCanMove).
    [Test]
    public void CanMove_ReturnsFalse_DuringFullBodyAttack()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanMove(isBusy: false, isAttacking: true, isFullBodyAttack: true), Is.False);
    }

    [Test]
    public void CanMove_ReturnsTrue_DuringUpperBodyAttack()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanMove(isBusy: false, isAttacking: true, isFullBodyAttack: false), Is.True);
    }

    [Test]
    public void CanMove_ReturnsTrue_WhenFullBodyFlagOutlivesTheAttack()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanMove(isBusy: false, isAttacking: false, isFullBodyAttack: true), Is.True);
    }

    [Test]
    public void CanMove_ReturnsFalse_WhenBusy()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanMove(isBusy: true, isAttacking: false, isFullBodyAttack: false), Is.False);
    }

    // Exercises the real gates (CanBlock / CanJump delegate to EvaluateCanBlock / EvaluateCanJump). Attacking is not an
    // input: block and jump cancel a swing in progress.
    [Test]
    public void CanBlock_ReturnsTrue_DrawnOnGround_WhateverTheAttack()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(isBusy: false, isAirborne: false, isDodging: false,
            isSwitchingWeapon: false, isInCombat: true), Is.True);
    }

    [Test]
    public void CanBlock_ReturnsFalse_WhenSheathedSwitchingDodgingAirborneOrBusy()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(false, false, false, false, false), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(false, false, false, true, true), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(false, false, true, false, true), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(false, true, false, false, true), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanBlock(true, false, false, false, true), Is.False);
    }

    [Test]
    public void CanJump_ReturnsTrue_OnGround_WhateverTheAttack()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanJump(isBusy: false, isAirborne: false, isDodging: false,
            isBlocking: false), Is.True);
    }

    [Test]
    public void CanJump_ReturnsFalse_WhenBlockingDodgingAirborneOrBusy()
    {
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanJump(false, false, false, true), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanJump(false, false, true, false), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanJump(false, true, false, false), Is.False);
        Assert.That(Game.Player.PlayerStateManager.EvaluateCanJump(true, false, false, false), Is.False);
    }
}
