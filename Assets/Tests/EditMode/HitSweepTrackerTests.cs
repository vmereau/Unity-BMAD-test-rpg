using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for HitSweepTracker — the pure logic behind WeaponHitbox sweeps:
/// per-window dedupe, owner exclusion, dead/null rejection, sub-step count and shape inflation math.
/// </summary>
public class HitSweepTrackerTests
{
    private const float TOLERANCE = 1e-4f;

    private sealed class FakeDamageable : IDamageable
    {
        public bool IsDead { get; set; }
        public void TakeDamage(float amount) { }
        public HitResult TryReceiveHit(GameObject attacker) => HitResult.NotBlocked;
    }

    private HitSweepTracker _tracker;

    [SetUp]
    public void SetUp()
    {
        _tracker = new HitSweepTracker();
    }

    // --- TryRegisterHit ---

    [Test]
    public void TryRegisterHit_FirstHitInWindow_ReturnsTrue()
    {
        _tracker.BeginWindow();
        Assert.IsTrue(_tracker.TryRegisterHit(new FakeDamageable(), null));
    }

    [Test]
    public void TryRegisterHit_SameTargetTwiceInWindow_ReturnsFalseSecondTime()
    {
        var target = new FakeDamageable();
        _tracker.BeginWindow();
        Assert.IsTrue(_tracker.TryRegisterHit(target, null));
        Assert.IsFalse(_tracker.TryRegisterHit(target, null));
    }

    [Test]
    public void TryRegisterHit_AfterEndAndBeginWindow_ReturnsTrueAgain()
    {
        var target = new FakeDamageable();
        _tracker.BeginWindow();
        _tracker.TryRegisterHit(target, null);
        _tracker.EndWindow();
        _tracker.BeginWindow();
        Assert.IsTrue(_tracker.TryRegisterHit(target, null));
    }

    [Test]
    public void TryRegisterHit_WindowClosed_ReturnsFalse()
    {
        Assert.IsFalse(_tracker.TryRegisterHit(new FakeDamageable(), null));
        _tracker.BeginWindow();
        _tracker.EndWindow();
        Assert.IsFalse(_tracker.TryRegisterHit(new FakeDamageable(), null));
    }

    [Test]
    public void TryRegisterHit_TargetIsOwner_ReturnsFalse()
    {
        var owner = new FakeDamageable();
        _tracker.BeginWindow();
        Assert.IsFalse(_tracker.TryRegisterHit(owner, owner));
    }

    [Test]
    public void TryRegisterHit_DeadTarget_ReturnsFalse()
    {
        _tracker.BeginWindow();
        Assert.IsFalse(_tracker.TryRegisterHit(new FakeDamageable { IsDead = true }, null));
    }

    [Test]
    public void TryRegisterHit_NullTarget_ReturnsFalse()
    {
        _tracker.BeginWindow();
        Assert.IsFalse(_tracker.TryRegisterHit(null, new FakeDamageable()));
    }

    [Test]
    public void TryRegisterHit_TwoDistinctTargets_BothReturnTrue()
    {
        _tracker.BeginWindow();
        Assert.IsTrue(_tracker.TryRegisterHit(new FakeDamageable(), null));
        Assert.IsTrue(_tracker.TryRegisterHit(new FakeDamageable(), null));
    }

    // --- ComputeSubSteps ---

    [Test]
    public void ComputeSubSteps_ZeroDistance_ReturnsOne()
    {
        Assert.AreEqual(1, HitSweepTracker.ComputeSubSteps(0f, 0.1f, 8));
    }

    [Test]
    public void ComputeSubSteps_DistanceOverStep_ReturnsCeiling()
    {
        Assert.AreEqual(4, HitSweepTracker.ComputeSubSteps(0.35f, 0.1f, 8));
    }

    [Test]
    public void ComputeSubSteps_LargeDistance_ClampedToMax()
    {
        Assert.AreEqual(8, HitSweepTracker.ComputeSubSteps(10f, 0.1f, 8));
    }

    [Test]
    public void ComputeSubSteps_NonPositiveStep_ReturnsOne()
    {
        Assert.AreEqual(1, HitSweepTracker.ComputeSubSteps(1f, 0f, 8));
        Assert.AreEqual(1, HitSweepTracker.ComputeSubSteps(1f, -0.5f, 8));
    }

    // --- Shape inflation ---

    [Test]
    public void ComputeBoxHalfExtents_AppliesScaleAndPadding()
    {
        Vector3 result = HitSweepTracker.ComputeBoxHalfExtents(
            new Vector3(1f, 2f, 4f), new Vector3(2f, 1f, 0.5f), 0.1f);
        Assert.AreEqual(1.1f, result.x, TOLERANCE);
        Assert.AreEqual(1.1f, result.y, TOLERANCE);
        Assert.AreEqual(1.1f, result.z, TOLERANCE);
    }

    [Test]
    public void ComputeBoxHalfExtents_NegativeScale_UsesAbsolute()
    {
        Vector3 result = HitSweepTracker.ComputeBoxHalfExtents(
            new Vector3(1f, 1f, 1f), new Vector3(-2f, 1f, -1f), 0f);
        Assert.AreEqual(1f, result.x, TOLERANCE);
        Assert.AreEqual(0.5f, result.y, TOLERANCE);
        Assert.AreEqual(0.5f, result.z, TOLERANCE);
    }

    [Test]
    public void ComputeScaledRadius_UsesMaxAxisScalePlusPadding()
    {
        float result = HitSweepTracker.ComputeScaledRadius(0.25f, new Vector3(1f, 2f, 1f), 0.1f);
        Assert.AreEqual(0.6f, result, TOLERANCE);
    }
}
