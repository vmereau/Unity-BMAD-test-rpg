using System.Collections.Generic;
using Game.AI;
using Game.Animations;
using Game.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Edit Mode tests for EntityMeleeAttacker window routing: attack gating, per-id / all-hitbox
/// open, unknown ids, close-on-end/begin and runtime unregister. WeaponHitbox Enable/Disable and
/// IsWindowOpen depend only on its tracker, so no Awake/LateUpdate is needed.
/// </summary>
public class EntityMeleeAttackerTests
{
    private GameObject _attackerGO;
    private EntityMeleeAttacker _attacker;
    private WeaponHitbox _bite;
    private WeaponHitbox _tail;
    private readonly List<GameObject> _created = new();

    /// <summary>Records combo calls; MaxComboSteps 3 like the humanoid driver.</summary>
    private class FakeComboDriver : AIAnimationDriver
    {
        public readonly List<int> ComboSteps = new();
        public int CancelCount;
        public override int MaxComboSteps => 3;
        public override void TriggerComboStep(int step) => ComboSteps.Add(step);
        public override void CancelAttack() => CancelCount++;
    }

    // Awake does not run in EditMode — assign the driver through the serialized field.
    private FakeComboDriver AttachDriver()
    {
        var driver = _attackerGO.AddComponent<FakeComboDriver>();
        var so = new SerializedObject(_attacker);
        so.FindProperty("_animationDriver").objectReferenceValue = driver;
        so.ApplyModifiedPropertiesWithoutUndo();
        return driver;
    }

    [SetUp]
    public void SetUp()
    {
        // EditMode: Awake does not run on AddComponent outside play mode, but be tolerant of its warnings.
        LogAssert.ignoreFailingMessages = true;

        _attackerGO = new GameObject("Attacker");
        _created.Add(_attackerGO);
        _attacker = _attackerGO.AddComponent<EntityMeleeAttacker>();
        _bite = CreateHitbox("Hitbox_Bite");
        _tail = CreateHitbox("Hitbox_Tail");
        _attacker.RegisterHitbox("Bite", _bite);
        _attacker.RegisterHitbox("Tail", _tail);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _created)
            if (go != null) Object.DestroyImmediate(go);
        _created.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private WeaponHitbox CreateHitbox(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_attackerGO.transform, false);
        go.AddComponent<SphereCollider>();
        return go.AddComponent<WeaponHitbox>();
    }

    [Test]
    public void OpenWindow_WhenNotAttacking_DoesNotOpen()
    {
        _attacker.OpenWindow("Bite");

        Assert.IsFalse(_bite.IsWindowOpen);
    }

    [Test]
    public void OpenWindow_WithId_OpensOnlyThatHitbox()
    {
        _attacker.BeginAttack(10f);
        _attacker.OpenWindow("Bite");

        Assert.IsTrue(_bite.IsWindowOpen);
        Assert.IsFalse(_tail.IsWindowOpen);
    }

    [Test]
    public void OpenWindow_EmptyId_OpensAll()
    {
        _attacker.BeginAttack(10f);
        _attacker.OpenWindow("");

        Assert.IsTrue(_bite.IsWindowOpen);
        Assert.IsTrue(_tail.IsWindowOpen);
    }

    [Test]
    public void OpenWindow_UnknownId_DoesNotThrow()
    {
        _attacker.BeginAttack(10f);

        Assert.DoesNotThrow(() =>
        {
            _attacker.OpenWindow("Wing");
            _attacker.OpenWindow("Wing");
        });
        Assert.IsFalse(_bite.IsWindowOpen);
        Assert.IsFalse(_tail.IsWindowOpen);
    }

    [Test]
    public void EndAttack_ClosesAllWindows()
    {
        _attacker.BeginAttack(10f);
        _attacker.OpenWindow(null);

        _attacker.EndAttack();

        Assert.IsFalse(_bite.IsWindowOpen);
        Assert.IsFalse(_tail.IsWindowOpen);
        Assert.IsFalse(_attacker.IsAttacking);
    }

    [Test]
    public void BeginAttack_ClosesOpenWindows_AndStoresDamage()
    {
        _attacker.BeginAttack(10f);
        _attacker.OpenWindow("Bite");

        _attacker.BeginAttack(25f);

        Assert.IsFalse(_bite.IsWindowOpen);
        Assert.AreEqual(25f, _attacker.CurrentDamage);
        Assert.IsTrue(_attacker.IsAttacking);
    }

    [Test]
    public void UnregisterHitbox_ClosesAndRemoves()
    {
        _attacker.BeginAttack(10f);
        _attacker.OpenWindow("Bite");

        _attacker.UnregisterHitbox("Bite");

        Assert.IsFalse(_bite.IsWindowOpen);
        Assert.AreEqual(1, _attacker.Hitboxes.Count);
        Assert.AreEqual("Tail", _attacker.Hitboxes[0].Id);
    }

    [Test]
    public void NotifyAttackStateExited_LastState_EndsAttack()
    {
        _attacker.BeginAttack(10f);
        _attacker.NotifyAttackStateEntered();
        _attacker.OpenWindow("Bite");

        _attacker.NotifyAttackStateExited();

        Assert.IsFalse(_attacker.IsAttacking);
        Assert.IsFalse(_attacker.IsInAttackState);
        Assert.IsFalse(_bite.IsWindowOpen);
    }

    [Test]
    public void NotifyAttackStateExited_WithOverlappingNextState_KeepsAttacking()
    {
        // Combo crossfade: OnStateEnter(next) fires before OnStateExit(previous).
        _attacker.BeginAttack(10f);
        _attacker.NotifyAttackStateEntered();
        _attacker.NotifyAttackStateEntered();

        _attacker.NotifyAttackStateExited();
        _attacker.OpenWindow("Bite");

        Assert.IsTrue(_attacker.IsAttacking);
        Assert.IsTrue(_attacker.IsInAttackState);
        Assert.IsTrue(_bite.IsWindowOpen);
    }

    [Test]
    public void NotifyAttackStateExited_BelowZero_Clamps()
    {
        Assert.DoesNotThrow(() => _attacker.NotifyAttackStateExited());
        Assert.IsFalse(_attacker.IsInAttackState);

        _attacker.NotifyAttackStateEntered();
        Assert.IsTrue(_attacker.IsInAttackState);
    }

    [Test]
    public void EndAttack_DoesNotResetStateCounter()
    {
        _attacker.BeginAttack(10f);
        _attacker.NotifyAttackStateEntered();

        _attacker.EndAttack();

        Assert.IsFalse(_attacker.IsAttacking);
        Assert.IsTrue(_attacker.IsInAttackState);
    }

    [Test]
    public void BeginAttack_NoDriver_ClampsComboToOneHit()
    {
        _attacker.BeginAttack(10f, 3);

        Assert.AreEqual(1, _attacker.ComboHits);
        Assert.AreEqual(1, _attacker.ComboStep);
    }

    [Test]
    public void OnComboWindowOpen_WhenNotAttacking_DoesNotAdvance()
    {
        _attacker.OnComboWindowOpen();

        Assert.AreEqual(0, _attacker.ComboStep);
        Assert.AreEqual(0, _attacker.ComboHits);
    }

    [Test]
    public void OnComboWindowOpen_WithDriver_AdvancesThroughAllSteps()
    {
        FakeComboDriver driver = AttachDriver();
        _attacker.BeginAttack(10f, 3);

        _attacker.OnComboWindowOpen();
        _attacker.OnComboWindowOpen();
        _attacker.OnComboWindowOpen(); // third window (last step) must not request a step 4

        CollectionAssert.AreEqual(new[] { 2, 3 }, driver.ComboSteps);
        Assert.AreEqual(3, _attacker.ComboStep);
    }

    [Test]
    public void BeginAttack_WithDriver_ClampsToMaxComboSteps()
    {
        AttachDriver();

        _attacker.BeginAttack(10f, 5);

        Assert.AreEqual(3, _attacker.ComboHits);
    }

    [Test]
    public void BeginAttack_MidCombo_RestartsAtStepOne()
    {
        AttachDriver();
        _attacker.BeginAttack(10f, 3);
        _attacker.OnComboWindowOpen();

        _attacker.BeginAttack(10f, 2);

        Assert.AreEqual(1, _attacker.ComboStep);
        Assert.AreEqual(2, _attacker.ComboHits);
    }

    [Test]
    public void EndAttack_CancelsQueuedAttackTriggers()
    {
        FakeComboDriver driver = AttachDriver();
        _attacker.BeginAttack(10f, 3);

        _attacker.EndAttack();

        Assert.AreEqual(1, driver.CancelCount);
    }

    [Test]
    public void OnDisable_ResetsStateCounter()
    {
        _attacker.NotifyAttackStateEntered();
        _attacker.NotifyAttackStateEntered();

        // Lifecycle callbacks don't fire in EditMode — invoke OnDisable directly.
        typeof(EntityMeleeAttacker)
            .GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_attacker, null);

        Assert.IsFalse(_attacker.IsInAttackState);
    }
}
