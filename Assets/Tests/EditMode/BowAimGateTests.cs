using Game.Player;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the bow aim gate — PlayerStateManager.EvaluateCanAim.
/// </summary>
public class BowAimGateTests
{
    private static bool Can(bool busy = false, bool airborne = false, bool dodging = false, bool attacking = false,
                            bool blocking = false, bool switching = false, bool inCombat = true)
        => PlayerStateManager.EvaluateCanAim(busy, airborne, dodging, attacking, blocking, switching, inCombat);

    [Test]
    public void CanAim_ReturnsFalse_WhileBlockStillRaised() => Assert.That(Can(blocking: true), Is.False);

    [Test]
    public void CanAim_ReturnsTrue_WhenDrawnAndIdle() => Assert.That(Can(), Is.True);

    [Test]
    public void CanAim_ReturnsFalse_WhenSheathed() => Assert.That(Can(inCombat: false), Is.False);

    [Test]
    public void CanAim_ReturnsFalse_WhenBusy() => Assert.That(Can(busy: true), Is.False);

    [Test]
    public void CanAim_ReturnsFalse_WhenAirborne() => Assert.That(Can(airborne: true), Is.False);

    [Test]
    public void CanAim_ReturnsFalse_WhenDodging() => Assert.That(Can(dodging: true), Is.False);

    [Test]
    public void CanAim_ReturnsFalse_WhenAttacking() => Assert.That(Can(attacking: true), Is.False);

    [Test]
    public void CanAim_ReturnsFalse_WhileSwitchingWeapon() => Assert.That(Can(switching: true), Is.False);
}
