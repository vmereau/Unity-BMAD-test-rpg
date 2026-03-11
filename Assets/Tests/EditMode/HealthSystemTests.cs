using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for health system logic.
/// Pure formula simulation — no MonoBehaviour lifecycle, no scene required.
/// Pattern mirrors EnemyBrainStateTests, DodgeGateTests, BlockGateTests.
/// Story 2.9: Initial implementation.
/// </summary>
public class HealthSystemTests
{
    // Pure formula helpers — mimic what EnemyHealth and PlayerHealth do internally

    private float ApplyDamage(float currentHealth, float damage)
        => Mathf.Max(0f, currentHealth - damage);

    private bool IsDead(float currentHealth)
        => currentHealth <= 0f;

    // ── ApplyDamage tests ──────────────────────────────────────────────────

    [Test]
    public void ApplyDamage_ReducesHealth_ByAmount()
    {
        float result = ApplyDamage(100f, 25f);
        Assert.That(result, Is.EqualTo(75f));
    }

    [Test]
    public void ApplyDamage_ClampsToZero_WhenDamageExceedsHealth()
    {
        float result = ApplyDamage(20f, 50f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void ApplyDamage_ReturnsZero_WhenDamageExactlyEqualsHealth()
    {
        float result = ApplyDamage(50f, 50f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void ApplyDamage_DoesNotGoNegative_WithMassiveDamage()
    {
        float result = ApplyDamage(1f, 9999f);
        Assert.That(result, Is.EqualTo(0f));
    }

    // ── IsDead tests ───────────────────────────────────────────────────────

    [Test]
    public void IsDead_ReturnsFalse_WhenHealthAboveZero()
        => Assert.That(IsDead(1f), Is.False);

    [Test]
    public void IsDead_ReturnsTrue_WhenHealthAtZero()
        => Assert.That(IsDead(0f), Is.True);

    [Test]
    public void IsDead_ReturnsTrue_WhenHealthBelowZero_Hypothetical()
        => Assert.That(IsDead(-1f), Is.True);

    [Test]
    public void IsDead_ReturnsFalse_WhenHealthIsFullPool()
        => Assert.That(IsDead(100f), Is.False);

    // ── Combined scenario tests ────────────────────────────────────────────

    [Test]
    public void TwoHits_KillEnemyWith50HP_Using25DamageEach()
    {
        float hp = 50f;
        hp = ApplyDamage(hp, 25f);
        Assert.That(IsDead(hp), Is.False, "Should not be dead after first hit");

        hp = ApplyDamage(hp, 25f);
        Assert.That(IsDead(hp), Is.True, "Should be dead after second hit");
    }

    [Test]
    public void TenHits_KillPlayerWith100HP_Using10DamageEach()
    {
        float hp = 100f;
        for (int i = 0; i < 9; i++)
        {
            hp = ApplyDamage(hp, 10f);
            Assert.That(IsDead(hp), Is.False, $"Should not be dead after hit {i + 1}");
        }
        hp = ApplyDamage(hp, 10f);
        Assert.That(IsDead(hp), Is.True, "Should be dead after 10th hit");
    }
}
