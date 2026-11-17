using Game.AI;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for AttackTokenRegistry (spec-ai-combat-tactics-foundation): capacity per target, one token per
/// holder, release and reset.
/// </summary>
public class AttackTokenRegistryTests
{
    private readonly object _player = new object();
    private readonly object _other = new object();

    [SetUp]
    public void SetUp() => AttackTokenRegistry.Reset();

    [TearDown]
    public void TearDown() => AttackTokenRegistry.Reset();

    [Test]
    public void TryTake_RespectsCapacity()
    {
        object a = new object(), b = new object(), c = new object();
        Assert.IsTrue(AttackTokenRegistry.TryTake(_player, a, 2));
        Assert.IsTrue(AttackTokenRegistry.TryTake(_player, b, 2));
        Assert.IsFalse(AttackTokenRegistry.TryTake(_player, c, 2));
        Assert.AreEqual(2, AttackTokenRegistry.CountOn(_player));
        Assert.IsFalse(AttackTokenRegistry.IsAvailable(_player, c, 2));
    }

    [Test]
    public void TryTake_SameHolderTwice_KeepsOneToken()
    {
        object a = new object();
        Assert.IsTrue(AttackTokenRegistry.TryTake(_player, a, 1));
        Assert.IsTrue(AttackTokenRegistry.TryTake(_player, a, 1));
        Assert.AreEqual(1, AttackTokenRegistry.CountOn(_player));
        Assert.IsTrue(AttackTokenRegistry.IsAvailable(_player, a, 1)); // its own token
    }

    [Test]
    public void Release_FreesTheSlot()
    {
        object a = new object(), b = new object();
        AttackTokenRegistry.TryTake(_player, a, 1);
        AttackTokenRegistry.Release(a);
        Assert.IsFalse(AttackTokenRegistry.Holds(a, _player));
        Assert.IsTrue(AttackTokenRegistry.TryTake(_player, b, 1));
        AttackTokenRegistry.Release(a); // idempotent, doesn't touch b
        Assert.IsTrue(AttackTokenRegistry.Holds(b, _player));
    }

    [Test]
    public void TakingAnotherTarget_ReleasesThePrevious()
    {
        object a = new object();
        AttackTokenRegistry.TryTake(_player, a, 2);
        AttackTokenRegistry.TryTake(_other, a, 2);
        Assert.AreEqual(0, AttackTokenRegistry.CountOn(_player));
        Assert.IsTrue(AttackTokenRegistry.Holds(a, _other));
    }

    [Test]
    public void Reset_ClearsEverything()
    {
        object a = new object();
        AttackTokenRegistry.TryTake(_player, a, 2);
        AttackTokenRegistry.Reset();
        Assert.AreEqual(0, AttackTokenRegistry.CountOn(_player));
        Assert.IsFalse(AttackTokenRegistry.Holds(a, _player));
    }

    [Test]
    public void NullArguments_AreRejected()
    {
        Assert.IsFalse(AttackTokenRegistry.TryTake(null, new object(), 2));
        Assert.IsFalse(AttackTokenRegistry.TryTake(_player, null, 2));
        Assert.DoesNotThrow(() => AttackTokenRegistry.Release(null));
    }
}
