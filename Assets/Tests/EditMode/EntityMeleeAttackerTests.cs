using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using NUnit.Framework;
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
}
