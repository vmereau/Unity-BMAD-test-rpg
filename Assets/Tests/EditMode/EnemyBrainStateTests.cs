using NUnit.Framework;

/// <summary>
/// Edit Mode tests for EnemyBrain state transition logic.
/// Pure formula simulation — no MonoBehaviour lifecycle, no NavMesh.
/// Pattern mirrors DodgeGateTests, BlockGateTests.
/// </summary>
public class EnemyBrainStateTests
{
    private bool ShouldEngage(float distToPlayer, float detectionRange)
        => distToPlayer <= detectionRange;

    private bool ShouldDisengage(float distToPlayer, float disengageRange)
        => distToPlayer > disengageRange;

    [Test]
    public void ShouldEngage_ReturnsTrue_WhenWithinRange()
        => Assert.That(ShouldEngage(5f, 8f), Is.True);

    [Test]
    public void ShouldEngage_ReturnsFalse_WhenOutOfRange()
        => Assert.That(ShouldEngage(10f, 8f), Is.False);

    [Test]
    public void ShouldEngage_ReturnsTrue_WhenExactlyAtRange()
        => Assert.That(ShouldEngage(8f, 8f), Is.True);

    [Test]
    public void ShouldDisengage_ReturnsTrue_WhenTooFar()
        => Assert.That(ShouldDisengage(15f, 12f), Is.True);

    [Test]
    public void ShouldDisengage_ReturnsFalse_WhenWithinRange()
        => Assert.That(ShouldDisengage(10f, 12f), Is.False);

    [Test]
    public void ShouldDisengage_ReturnsFalse_WhenExactlyAtRange()
        => Assert.That(ShouldDisengage(12f, 12f), Is.False);
}
