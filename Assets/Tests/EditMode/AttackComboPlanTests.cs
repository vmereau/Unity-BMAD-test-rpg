using Game.AI;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for AttackComboPlan: hit-count clamping, step advancing and reset.
/// </summary>
public class AttackComboPlanTests
{
    private AttackComboPlan _plan;

    [SetUp]
    public void SetUp()
    {
        _plan = new AttackComboPlan();
    }

    [Test]
    public void Begin_SetsStepOne()
    {
        _plan.Begin(2, 3);

        Assert.AreEqual(1, _plan.CurrentStep);
        Assert.AreEqual(2, _plan.TotalHits);
        Assert.IsTrue(_plan.IsActive);
    }

    [Test]
    public void Begin_ClampsHitsToMaxSteps()
    {
        _plan.Begin(5, 3);

        Assert.AreEqual(3, _plan.TotalHits);
    }

    [Test]
    public void Begin_ClampsHitsToAtLeastOne()
    {
        _plan.Begin(0, 3);

        Assert.AreEqual(1, _plan.TotalHits);
    }

    [Test]
    public void Begin_MaxStepsBelowOne_TreatedAsOne()
    {
        _plan.Begin(3, 0);

        Assert.AreEqual(1, _plan.TotalHits);
        Assert.AreEqual(1, _plan.CurrentStep);
    }

    [Test]
    public void TryAdvance_ThreeHits_AdvancesTwiceThenStops()
    {
        _plan.Begin(3, 3);

        Assert.IsTrue(_plan.TryAdvance(out int second));
        Assert.AreEqual(2, second);
        Assert.IsTrue(_plan.TryAdvance(out int third));
        Assert.AreEqual(3, third);
        Assert.IsFalse(_plan.TryAdvance(out int none));
        Assert.AreEqual(0, none);
        Assert.AreEqual(3, _plan.CurrentStep);
    }

    [Test]
    public void TryAdvance_SingleHit_ReturnsFalse()
    {
        _plan.Begin(1, 3);

        Assert.IsFalse(_plan.TryAdvance(out int next));
        Assert.AreEqual(0, next);
        Assert.AreEqual(1, _plan.CurrentStep);
    }

    [Test]
    public void TryAdvance_WhenIdle_ReturnsFalse()
    {
        Assert.IsFalse(_plan.TryAdvance(out int next));
        Assert.AreEqual(0, next);
        Assert.AreEqual(0, _plan.CurrentStep);
    }

    [Test]
    public void Reset_ClearsState()
    {
        _plan.Begin(3, 3);
        _plan.TryAdvance(out _);

        _plan.Reset();

        Assert.AreEqual(0, _plan.TotalHits);
        Assert.AreEqual(0, _plan.CurrentStep);
        Assert.IsFalse(_plan.IsActive);
    }
}
