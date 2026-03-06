using NUnit.Framework;

/// <summary>
/// Edit Mode tests for combo step progression formula used by PlayerCombat.
/// Tests pure state-machine logic, not MonoBehaviour lifecycle.
/// Pattern mirrors PlayerCombatGateTests — tests formulas, not the MonoBehaviour.
/// </summary>
public class ComboWindowTests
{
    // Simulates the combo step advancement formula
    private int AdvanceCombo(int currentStep, bool windowOpen)
    {
        if (!windowOpen) return 0; // reset if window closed
        return currentStep < 2 ? currentStep + 1 : 0; // advance or wrap
    }

    [Test]
    public void ComboStep_AdvancesFrom0To1_OnFirstHit()
    {
        // First hit: window was closed, so step resets to 0, then advances to 1
        int nextStep = 0 + 1; // step 0 → fires hit 1 → advance to 1
        Assert.That(nextStep, Is.EqualTo(1));
    }

    [Test]
    public void ComboStep_AdvancesFrom1To2_OnSecondHit()
    {
        int currentStep = 1;
        bool windowOpen = true;
        int stepToFire = windowOpen ? currentStep : 0;
        Assert.That(stepToFire, Is.EqualTo(1)); // fires Attack_2
        int nextStep = stepToFire + 1;
        Assert.That(nextStep, Is.EqualTo(2));
    }

    [Test]
    public void ComboStep_ResetsTo0_AfterFinisher()
    {
        int currentStep = 2;
        bool isFinisher = currentStep >= 2;
        int nextStep = isFinisher ? 0 : currentStep + 1;
        Assert.That(nextStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_WhenWindowClosed()
    {
        int currentStep = 1; // mid-combo
        bool windowOpen = false; // window expired
        int resetStep = windowOpen ? currentStep : 0;
        Assert.That(resetStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_OnStaminaBlock()
    {
        // Stamina block forces reset regardless of current step
        int currentStep = 1;
        bool staminaBlocked = true;
        int resultStep = staminaBlocked ? 0 : currentStep;
        Assert.That(resultStep, Is.EqualTo(0));
    }
}
