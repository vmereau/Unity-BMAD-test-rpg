using System.Reflection;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the stamina gate logic, calling StaminaSystem.HasEnough() directly.
/// Uses reflection to set private _currentStamina without requiring a full MonoBehaviour lifecycle.
/// </summary>
public class PlayerCombatGateTests
{
    private GameObject _testGO;

    [SetUp]
    public void SetUp()
    {
        _testGO = new GameObject("TestStaminaSystem");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_testGO);
    }

    private StaminaSystem CreateStaminaSystem(float currentStamina)
    {
        var system = _testGO.AddComponent<StaminaSystem>();
        typeof(StaminaSystem)
            .GetField("_currentStamina", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(system, currentStamina);
        return system;
    }

    [Test]
    public void Gate_AllowsAttack_WhenStaminaSufficient()
    {
        var system = CreateStaminaSystem(50f);
        Assert.That(system.HasEnough(20f), Is.True);
    }

    [Test]
    public void Gate_BlocksAttack_WhenStaminaInsufficient()
    {
        var system = CreateStaminaSystem(10f);
        Assert.That(system.HasEnough(20f), Is.False);
    }

    [Test]
    public void Gate_BlocksAttack_WhenStaminaExactlyZero()
    {
        var system = CreateStaminaSystem(0f);
        Assert.That(system.HasEnough(20f), Is.False);
    }

    [Test]
    public void Gate_AllowsAttack_WhenStaminaExactlyEqualsToCost()
    {
        var system = CreateStaminaSystem(20f);
        Assert.That(system.HasEnough(20f), Is.True);
    }
}
