using NUnit.Framework;

/// <summary>
/// Edit Mode tests for DodgeController gate logic.
/// Pure formula simulation — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests, PerfectBlockTests, PlayerStateManagerTests.
/// </summary>
public class DodgeGateTests
{
    private bool CanDodge(bool isAirborne, bool isBlocking,
                          bool isDodging, float stamina, float dodgeCost)
        => !isAirborne && !isBlocking && !isDodging && stamina >= dodgeCost;

    [Test]
    public void CanDodge_ReturnsFalse_WhenAirborne()
        => Assert.That(CanDodge(true, false, false, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsTrue_WhenAttacking()
        => Assert.That(CanDodge(false, false, false, 100f, 25f), Is.True);

    [Test]
    public void CanDodge_ReturnsFalse_WhenBlocking()
        => Assert.That(CanDodge(false, true, false, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenAlreadyDodging()
        => Assert.That(CanDodge(false, false, true, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenInsufficientStamina()
        => Assert.That(CanDodge(false, false, false, 24f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsTrue_WhenAllConditionsMet()
    {
        Assert.That(CanDodge(false, false, false, 25f, 25f), Is.True);
        Assert.That(CanDodge(false, false, false, 100f, 25f), Is.True);
    }
}
