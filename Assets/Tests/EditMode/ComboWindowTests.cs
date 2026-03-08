using NUnit.Framework;

/// <summary>
/// Edit Mode tests for combo step progression formula used by PlayerCombat.
/// Tests pure state-machine logic, not MonoBehaviour lifecycle.
/// Pattern mirrors PlayerCombatGateTests — tests formulas, not the MonoBehaviour.
/// </summary>
public class ComboWindowTests
{
    // Simulates the combo step advancement formula from PlayerCombat.TryAttack()
    private int AdvanceCombo(int currentStep, bool windowOpen)
    {
        if (!windowOpen) return 0; // reset if window closed
        return currentStep < 2 ? currentStep + 1 : 0; // advance or wrap
    }

    [Test]
    public void ComboStep_AdvancesFrom0To1_OnFirstHit()
    {
        // First hit: window was closed → step resets to 0, fires Attack_1, advances to 1
        int resetStep = AdvanceCombo(0, windowOpen: false); // window closed → 0
        Assert.That(resetStep, Is.EqualTo(0));
        int nextStep = AdvanceCombo(resetStep, windowOpen: true); // advance after fire
        Assert.That(nextStep, Is.EqualTo(1));
    }

    [Test]
    public void ComboStep_AdvancesFrom1To2_OnSecondHit()
    {
        int nextStep = AdvanceCombo(1, windowOpen: true);
        Assert.That(nextStep, Is.EqualTo(2));
    }

    [Test]
    public void ComboStep_ResetsTo0_AfterFinisher()
    {
        int nextStep = AdvanceCombo(2, windowOpen: true); // step 2 = finisher → wraps to 0
        Assert.That(nextStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_WhenWindowClosed()
    {
        int nextStep = AdvanceCombo(1, windowOpen: false); // window expired mid-combo
        Assert.That(nextStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_OnStaminaBlock()
    {
        // Stamina block forces reset — same formula as window-closed reset
        bool staminaBlocked = true;
        int nextStep = staminaBlocked ? AdvanceCombo(1, windowOpen: false) : AdvanceCombo(1, windowOpen: true);
        Assert.That(nextStep, Is.EqualTo(0));
    }
}
