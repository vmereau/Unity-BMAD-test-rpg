using Game.Combat;
using Game.Inventory;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the unarmed combo data (spec-unarmed-combo-rework): UnarmedAttack.Resolve, the per-step stance
/// multiplier, and the authored CombatConfig.asset (lead jab L → hook R → uppercut L finisher).
/// </summary>
public class UnarmedAttackTests
{
    private const float EPS = 0.001f;
    private const string CONFIG_PATH = "Assets/_Game/Data/Config/CombatConfig.asset";
    private const string PLAYER_PATH = "Assets/_Game/Prefabs/Player/Player.prefab";

    private static readonly UnarmedAttack Fallback = UnarmedAttack.Fallback(new AttackStep(0.5f, 0.35f, 0.35f));

    private static UnarmedAttack[] Steps() => new[]
    {
        new UnarmedAttack(new AttackStep(0.3f, 0.1f, 0.4f), WeaponHand.Left),
        new UnarmedAttack(new AttackStep(0.4f, 0.2f, 0.3f), WeaponHand.Right),
        new UnarmedAttack(new AttackStep(0.6f, 0.2f, 0.3f), WeaponHand.Left, 1.5f, 2f),
    };

    [Test]
    public void Resolve_IndexIsOneBased()
    {
        UnarmedAttack second = UnarmedAttack.Resolve(Steps(), 2, Fallback);
        Assert.That(second.hand, Is.EqualTo(WeaponHand.Right));
        Assert.That(second.step.distance, Is.EqualTo(0.4f).Within(EPS));
    }

    [Test]
    public void Resolve_Finisher_CarriesItsMultipliers()
    {
        UnarmedAttack finisher = UnarmedAttack.Resolve(Steps(), 3, Fallback);
        Assert.That(finisher.damageMultiplier, Is.EqualTo(1.5f).Within(EPS));
        Assert.That(finisher.stanceMultiplier, Is.EqualTo(2f).Within(EPS));
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(-1)]
    public void Resolve_OutOfRange_ReturnsFallback(int attackIndex)
    {
        UnarmedAttack attack = UnarmedAttack.Resolve(Steps(), attackIndex, Fallback);
        Assert.That(attack.hand, Is.EqualTo(WeaponHand.Right));
        Assert.That(attack.damageMultiplier, Is.EqualTo(1f).Within(EPS));
        Assert.That(attack.stanceMultiplier, Is.EqualTo(1f).Within(EPS));
        Assert.That(attack.step.distance, Is.EqualTo(0.5f).Within(EPS));
    }

    [Test]
    public void Resolve_NullArray_ReturnsFallback()
        => Assert.That(UnarmedAttack.Resolve(null, 1, Fallback).step.distance, Is.EqualTo(0.5f).Within(EPS));

    [Test]
    public void StepStanceDamage_MultipliesBaseAndStrength()
        => Assert.That(StanceMath.ComputeMeleeStanceDamage(15f, 10f, 0.5f, 2f), Is.EqualTo(40f).Within(EPS));

    [Test]
    public void StepStanceDamage_MultiplierOne_IsPlainHit()
        => Assert.That(StanceMath.ComputeMeleeStanceDamage(15f, -3f, 0.5f, 1f), Is.EqualTo(15f).Within(EPS));

    [Test]
    public void StepStanceDamage_NegativeMultiplier_ClampsToZero()
        => Assert.That(StanceMath.ComputeMeleeStanceDamage(15f, 0f, 0.5f, -1f), Is.EqualTo(0f).Within(EPS));

    // ── Authored data ────────────────────────────────────────────────────────

    private static CombatConfigSO LoadConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<CombatConfigSO>(CONFIG_PATH);
        Assert.That(config, Is.Not.Null, $"{CONFIG_PATH} missing");
        return config;
    }

    [Test]
    public void Config_HasThreeSteps_LeftRightLeft()
    {
        CombatConfigSO config = LoadConfig();
        Assert.That(config.unarmedComboSteps, Is.EqualTo(3));
        Assert.That(config.unarmedAttacks.Length, Is.EqualTo(3));
        Assert.That(config.unarmedAttacks[0].hand, Is.EqualTo(WeaponHand.Left), "lead jab");
        Assert.That(config.unarmedAttacks[1].hand, Is.EqualTo(WeaponHand.Right), "hook");
        Assert.That(config.unarmedAttacks[2].hand, Is.EqualTo(WeaponHand.Left), "uppercut");
    }

    [Test]
    public void Config_EveryStepMovesForward_FinisherFarthest()
    {
        UnarmedAttack[] attacks = LoadConfig().unarmedAttacks;
        foreach (UnarmedAttack attack in attacks)
        {
            Assert.That(attack.step.distance, Is.GreaterThan(0f));
            Assert.That(attack.step.duration, Is.GreaterThan(0f));
        }
        Assert.That(attacks[2].step.distance, Is.GreaterThan(attacks[0].step.distance));
        Assert.That(attacks[2].step.distance, Is.GreaterThan(attacks[1].step.distance));
    }

    [Test]
    public void Config_EveryStepMovesThePlayerByItsClip()
    {
        // spec-action-root-motion-unarmed-combo: a scripted step in the middle of the chain would cut the travel at its
        // crossfade (ActionRootMotion only carries the blend between two clip-driven attacks).
        UnarmedAttack[] attacks = LoadConfig().unarmedAttacks;
        Assert.That(attacks, Is.Not.Empty);
        foreach (UnarmedAttack attack in attacks)
            Assert.That(attack.step.UsesRootMotion, Is.True);
    }

    [Test]
    public void Config_OnlyTheFinisherHitsHarder()
    {
        UnarmedAttack[] attacks = LoadConfig().unarmedAttacks;
        for (int i = 0; i < 2; i++)
        {
            Assert.That(attacks[i].damageMultiplier, Is.EqualTo(1f).Within(EPS));
            Assert.That(attacks[i].stanceMultiplier, Is.EqualTo(1f).Within(EPS));
        }
        Assert.That(attacks[2].damageMultiplier, Is.GreaterThan(1f));
        Assert.That(attacks[2].stanceMultiplier, Is.GreaterThan(1f));
    }

    // ── Player prefab wiring ─────────────────────────────────────────────────

    [TestCase("_unarmedHitbox", "WeaponSocket")]
    [TestCase("_unarmedHitboxLeft", "LeftWeaponSocket")]
    public void PlayerPrefab_HasAFistHitboxPerHand(string field, string socket)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PATH);
        Assert.That(prefab, Is.Not.Null, $"{PLAYER_PATH} missing");
        var combat = prefab.GetComponent<PlayerCombat>();
        Assert.That(combat, Is.Not.Null);

        var hitboxGO = new SerializedObject(combat).FindProperty(field).objectReferenceValue as GameObject;
        Assert.That(hitboxGO, Is.Not.Null, $"PlayerCombat.{field} not wired");
        Assert.That(hitboxGO.transform.parent.name, Is.EqualTo(socket));
        Assert.That(hitboxGO.GetComponent<WeaponHitbox>(), Is.Not.Null);
        Assert.That(hitboxGO.GetComponent<Collider>().enabled, Is.False, "the shape collider stays disabled");
    }

    // ── Weapon overrides keep their clips ────────────────────────────────────
    // Overrides are keyed on the base clip: a base state whose clip is not mapped plays the unarmed motion with a
    // weapon in hand.

    private const string COMBAT = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/";

    [TestCase("one handed/Sword_AnimatorOverride.overrideController", true)]
    [TestCase("two handed/TwoHanded_AnimatorOverride.overrideController", true)]
    [TestCase("Bow/Bow_AnimatorOverride.overrideController", false)]
    public void WeaponOverride_MapsTheUnarmedBaseClips(string file, bool melee)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(COMBAT + file);
        Assert.That(controller, Is.Not.Null, file + " missing");
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(pairs);
        var mapped = new HashSet<string>();
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
            if (pair.Value != null && pair.Value != pair.Key) mapped.Add(pair.Key.name);

        Assert.That(mapped, Does.Contain("Draw unarmed"));
        Assert.That(mapped, Does.Contain("sheath unarmed"));
        if (!melee) return;
        Assert.That(mapped, Does.Contain("lead jab step")); // Attack_1 base clip (spec-action-root-motion-jab-trial)
        Assert.That(mapped, Does.Contain("hook step"));     // Attack_2 base clip (spec-action-root-motion-unarmed-combo)
        Assert.That(mapped, Does.Contain("uppercut step")); // Attack_3 base clip
        // Stale keys: GetOverrides only lists the base controller's clips, so an entry left on a replaced base clip
        // (the old Hook / Uppercut) is only visible in the serialized list.
        var baseClips = new HashSet<AnimationClip>(controller.runtimeAnimatorController.animationClips);
        SerializedProperty entries = new SerializedObject(controller).FindProperty("m_Clips");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var original = entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_OriginalClip").objectReferenceValue as AnimationClip;
            Assert.That(original, Is.Not.Null, file + ": override entry " + i + " has no original clip");
            Assert.That(baseClips, Does.Contain(original), file + ": override keyed on '" + original.name + "', not a base clip");
        }
        Assert.That(mapped, Does.Contain("Unarmed Idle Guard"), "CombatIdle carry pose (base clip = baked unarmed guard)");
        Assert.That(mapped, Does.Contain("Unarmed Block Guard"), "Block_State (base clip = baked unarmed high guard)");
        string blockTarget = null;
        foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
            if (pair.Key.name == "Unarmed Block Guard" && pair.Value != null) blockTarget = pair.Value.name;
        // Weapon guard without a shield (spec-one-hand-braced-block); a held shield plays the base Block Idle.
        Assert.That(blockTarget, Is.EqualTo(file.StartsWith("two handed") ? "2hand blocking" : "Sword Braced Guard"),
            "each melee family keeps its own block");
    }
}
