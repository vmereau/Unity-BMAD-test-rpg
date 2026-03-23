using NUnit.Framework;

namespace Tests.EditMode
{

/// <summary>
/// Edit Mode tests for combo step progression driven by comboSteps (Story 7.10).
/// Tests pure state-machine logic, not MonoBehaviour lifecycle.
///
/// Formula under test (from PlayerCombat.ManageComboStep):
///   int maxSteps = weaponComboSteps;          // 3 for unarmed, weapon value for armed
///   if (comboStep < maxSteps - 1) IncreaseAttackCombo()   → step++, window closed
///   else                           ResetAttackCombo()      → step=0, window closed
///
/// Guard under test (from PlayerCombat.OnComboWindowOpen):
///   if (!isAttacking) return;                 // ignore stale events after finisher reset
/// </summary>
public class ComboWindowTests
{
    // Simulates ManageComboStep — returns next comboStep value
    private int AdvanceCombo(int currentStep, int comboSteps)
    {
        // Mirrors: if (_comboStep < maxSteps - 1) IncreaseAttackCombo(); else ResetAttackCombo();
        return currentStep < comboSteps - 1 ? currentStep + 1 : 0;
    }

    // Simulates OnComboWindowOpen guard (Story 7.10 fix)
    private bool ShouldOpenComboWindow(bool isAttacking)
    {
        // Mirrors: if (!_stateManager.IsAttacking) return;
        return isAttacking;
    }

    // ── Unarmed 3-step combo (comboSteps = 3) ────────────────────────────────

    [Test]
    public void Unarmed_Step0_AdvancesTo1()
    {
        Assert.That(AdvanceCombo(0, comboSteps: 3), Is.EqualTo(1));
    }

    [Test]
    public void Unarmed_Step1_AdvancesTo2()
    {
        Assert.That(AdvanceCombo(1, comboSteps: 3), Is.EqualTo(2));
    }

    [Test]
    public void Unarmed_Step2_IsFinisher_ResetsTo0()
    {
        Assert.That(AdvanceCombo(2, comboSteps: 3), Is.EqualTo(0));
    }

    // ── Sword 2-step combo (comboSteps = 2) ──────────────────────────────────

    [Test]
    public void Sword_Step0_AdvancesTo1()
    {
        Assert.That(AdvanceCombo(0, comboSteps: 2), Is.EqualTo(1));
    }

    [Test]
    public void Sword_Step1_IsFinisher_ResetsTo0()
    {
        // With comboSteps=2: step 1 is the finisher (1 < 2-1 = false → reset)
        Assert.That(AdvanceCombo(1, comboSteps: 2), Is.EqualTo(0));
    }

    [Test]
    public void Sword_DoesNotReachStep2_MaxChainIsAttack1ThenAttack2()
    {
        // Full sword chain: Attack_1 (step 0 → 1), Attack_2 = finisher (step 1 → 0)
        int afterFirst  = AdvanceCombo(0, comboSteps: 2);
        int afterSecond = AdvanceCombo(afterFirst, comboSteps: 2);
        Assert.That(afterFirst,  Is.EqualTo(1), "Attack_1 should advance to step 1");
        Assert.That(afterSecond, Is.EqualTo(0), "Attack_2 is finisher — resets to 0");
    }

    // ── OnComboWindowOpen guard (Story 7.10 fix — prevents stale finisher events) ──

    [Test]
    public void ComboWindowOpen_OpensWindow_WhenIsAttackingTrue()
    {
        Assert.IsTrue(ShouldOpenComboWindow(isAttacking: true),
            "Combo window should open when an attack is in progress");
    }

    [Test]
    public void ComboWindowOpen_IgnoresEvent_WhenIsAttackingFalse()
    {
        // After finisher: ResetAttackCombo sets IsAttacking=false, but the animation's
        // ComboWindowOpen event fires at ~50% of the clip. Must be ignored.
        Assert.IsFalse(ShouldOpenComboWindow(isAttacking: false),
            "Stale ComboWindowOpen from finisher animation must not reopen the combo window");
    }

    // ── Legacy formula edge cases (verified unchanged) ────────────────────────

    [Test]
    public void ComboStep_ResetsTo0_WhenWindowClosed_FreshAttack()
    {
        // When the window was never open (fresh attack), TryAttack resets _comboStep = 0.
        // Step 0 then advances normally.
        Assert.That(AdvanceCombo(0, comboSteps: 3), Is.EqualTo(1),
            "Fresh attack at step 0 advances to 1");
    }
}

} // namespace Tests.EditMode
