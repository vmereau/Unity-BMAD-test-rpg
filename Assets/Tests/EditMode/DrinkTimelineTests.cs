using Game.Combat;
using Game.Inventory;
using Game.Player;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for DrinkTimeline (spec-potion-drink-animation): one sip at the heal time, done at the duration,
/// interrupt before / after the sip; and the player's drink gate.
/// </summary>
public class DrinkTimelineTests
{
    [Test]
    public void Tick_SipsOnceAtHealTime_ThenDone()
    {
        var t = new DrinkTimeline();
        t.Begin(1.5f, 2.5f);
        Assert.AreEqual(DrinkTick.None, t.Tick(1f));
        Assert.AreEqual(DrinkTick.Sip, t.Tick(0.6f));
        Assert.AreEqual(DrinkTick.None, t.Tick(0.5f));
        Assert.AreEqual(DrinkTick.Done, t.Tick(0.5f));
        Assert.IsFalse(t.IsActive);
        Assert.AreEqual(DrinkTick.None, t.Tick(1f));
    }

    [Test]
    public void Tick_HugeFrame_SipsBeforeDone()
    {
        var t = new DrinkTimeline();
        t.Begin(1.5f, 2.5f);
        Assert.AreEqual(DrinkTick.Sip, t.Tick(5f));
        Assert.AreEqual(DrinkTick.Done, t.Tick(0f));
    }

    [Test]
    public void Interrupt_BeforeSip_LosesHeal()
    {
        var t = new DrinkTimeline();
        t.Begin(1.5f, 2.5f);
        t.Tick(1f);
        Assert.IsTrue(t.Interrupt());
        Assert.IsFalse(t.IsActive);
        Assert.AreEqual(DrinkTick.None, t.Tick(1f));
    }

    [Test]
    public void Interrupt_AfterSip_KeepsHeal()
    {
        var t = new DrinkTimeline();
        t.Begin(1.5f, 2.5f);
        t.Tick(2f);
        Assert.IsFalse(t.Interrupt());
    }

    [Test]
    public void Interrupt_WhenIdle_IsNoOp()
    {
        var t = new DrinkTimeline();
        Assert.IsFalse(t.Interrupt());
    }

    [Test]
    public void Begin_DurationBelowHealTime_IsClamped()
    {
        var t = new DrinkTimeline();
        t.Begin(2f, 1f);
        Assert.AreEqual(DrinkTick.None, t.Tick(1.5f));
        Assert.AreEqual(DrinkTick.Sip, t.Tick(0.5f));
        Assert.AreEqual(DrinkTick.Done, t.Tick(0f));
    }

    [Test]
    public void CanDrink_Idle_True()
    {
        Assert.IsTrue(Gate());
    }

    [Test]
    public void CanDrink_BusyStates_False()
    {
        Assert.IsFalse(Gate(isDead: true));
        Assert.IsFalse(Gate(isIncapacitated: true));
        Assert.IsFalse(Gate(isDrinking: true));
        Assert.IsFalse(Gate(isAttacking: true));
        Assert.IsFalse(Gate(isBlocking: true));
        Assert.IsFalse(Gate(isDodging: true));
        Assert.IsFalse(Gate(isAirborne: true));
        Assert.IsFalse(Gate(isSwimming: true));
        Assert.IsFalse(Gate(isAiming: true));
        Assert.IsFalse(Gate(isInDialogue: true));
        Assert.IsFalse(Gate(isExecuting: true));
        Assert.IsFalse(Gate(isScriptedHold: true));
        Assert.IsFalse(Gate(isSwitchingWeapon: true));
    }

    [Test]
    public void IsDrunk_PotionOnly_NotRawFood()
    {
        var potion = ScriptableObject.CreateInstance<PotionItemSO>();
        Assert.IsTrue(potion.IsDrunk, "a plain potion plays the drink clip");
        potion.isIngredient = true;
        Assert.IsFalse(potion.IsDrunk, "raw food (kind Potion + ingredient) is eaten instantly");
        Object.DestroyImmediate(potion);
    }

    private static bool Gate(bool isDead = false, bool isIncapacitated = false, bool isDrinking = false, bool isAttacking = false,
        bool isBlocking = false, bool isDodging = false, bool isAirborne = false, bool isSwimming = false, bool isAiming = false,
        bool isInDialogue = false, bool isExecuting = false, bool isScriptedHold = false, bool isSwitchingWeapon = false) =>
        PlayerStateManager.EvaluateCanDrink(isDead, isIncapacitated, isExecuting, isScriptedHold, isInDialogue, isDrinking,
            isAttacking, isBlocking, isDodging, isAirborne, isSwimming, isSwitchingWeapon, isAiming);
}
