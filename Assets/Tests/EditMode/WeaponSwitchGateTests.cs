using Game.Player;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the draw / sheathe (R) gate — PlayerStateManager.EvaluateCanSwitchWeapon.
/// </summary>
public class WeaponSwitchGateTests
{
    private static bool Can(bool busy = false, bool attacking = false, bool blocking = false, bool dodging = false,
                            bool airborne = false, bool switching = false, bool aiming = false)
        => PlayerStateManager.EvaluateCanSwitchWeapon(busy, attacking, blocking, dodging, airborne, switching, aiming);

    [Test]
    public void CanSwitch_ReturnsTrue_WhenIdle() => Assert.That(Can(), Is.True);

    [Test]
    public void CanSwitch_ReturnsFalse_WhenBusy() => Assert.That(Can(busy: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhenAttacking() => Assert.That(Can(attacking: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhenBlocking() => Assert.That(Can(blocking: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhenDodging() => Assert.That(Can(dodging: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhenAirborne() => Assert.That(Can(airborne: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhileSwitchInFlight() => Assert.That(Can(switching: true), Is.False);

    [Test]
    public void CanSwitch_ReturnsFalse_WhileAiming() => Assert.That(Can(aiming: true), Is.False);
}
