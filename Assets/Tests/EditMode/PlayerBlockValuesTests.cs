using Game.Combat;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for PlayerCombat.ResolveBlockValues (spec-unshielded-blocking): a held shield sets the block stamina and
/// perfect-block window, no shield falls back to the CombatConfigSO unshielded values.
/// </summary>
public class PlayerBlockValuesTests
{
    private CombatConfigSO _config;
    private ShieldSO _shield;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<CombatConfigSO>();
        _config.unshieldedBlockStaminaCost = 25f;
        _config.unshieldedPerfectBlockWindow = 0.2f;
        _shield = ScriptableObject.CreateInstance<ShieldSO>();
        _shield.blockStaminaCost = 12f;
        _shield.perfectBlockWindow = 0.35f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
        Object.DestroyImmediate(_shield);
    }

    [Test]
    public void ShieldInHand_UsesShieldValues()
    {
        (float cost, float window) = PlayerCombat.ResolveBlockValues(_shield, _config);
        Assert.AreEqual(12f, cost, 1e-5f);
        Assert.AreEqual(0.35f, window, 1e-5f);
    }

    [Test]
    public void NoShield_UsesUnshieldedConfigValues()
    {
        (float cost, float window) = PlayerCombat.ResolveBlockValues(null, _config);
        Assert.AreEqual(25f, cost, 1e-5f);
        Assert.AreEqual(0.2f, window, 1e-5f);
    }

    [Test]
    public void NoShield_NoConfig_ReturnsZeroes()
    {
        (float cost, float window) = PlayerCombat.ResolveBlockValues(null, null);
        Assert.AreEqual(0f, cost);
        Assert.AreEqual(0f, window);
    }
}
