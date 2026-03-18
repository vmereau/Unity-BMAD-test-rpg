using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for health system logic.
/// Pure formula simulation — no MonoBehaviour lifecycle, no scene required.
/// Pattern mirrors EnemyBrainStateTests, DodgeGateTests, BlockGateTests.
/// Story 2.9: Initial implementation.
/// Story 4.10: Added Heal formula tests.
/// </summary>
public class HealthSystemTests
{
    // Pure formula helpers — mimic what EnemyHealth and PlayerHealth do internally

    private float ApplyDamage(float currentHealth, float damage)
        => Mathf.Max(0f, currentHealth - damage);

    private bool IsDead(float currentHealth)
        => currentHealth <= 0f;

    private float ApplyHeal(float currentHealth, float healAmount, float maxHealth)
        => Mathf.Min(currentHealth + healAmount, maxHealth);

    // Mirrors PlayerHealth.Heal() dead guard: if dead, return health unchanged
    private float ApplyHeal_WithDeadGuard(bool isDead, float currentHealth, float healAmount, float maxHealth)
        => isDead ? currentHealth : Mathf.Min(currentHealth + healAmount, maxHealth);

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

    // ── ApplyHeal tests (Story 4.10) ───────────────────────────────────────

    [Test]
    public void ApplyHeal_RestoresHealth_ByAmount()
    {
        float result = ApplyHeal(50f, 30f, 100f);
        Assert.That(result, Is.EqualTo(80f));
    }

    [Test]
    public void ApplyHeal_ClampsToMaxHealth_WhenHealExceedsMax()
    {
        float result = ApplyHeal(80f, 30f, 100f);
        Assert.That(result, Is.EqualTo(100f), "Heal should not exceed maxHealth");
    }

    [Test]
    public void ApplyHeal_AtFullHealth_RemainsAtMax()
    {
        float result = ApplyHeal(100f, 30f, 100f);
        Assert.That(result, Is.EqualTo(100f), "Healing at full health should not overheal");
    }

    [Test]
    public void ApplyHeal_ExactlyFillsRemainingHealth()
    {
        float result = ApplyHeal(70f, 30f, 100f);
        Assert.That(result, Is.EqualTo(100f));
    }

    [Test]
    public void ApplyHeal_SmallHeal_PartialRecovery()
    {
        float result = ApplyHeal(10f, 5f, 100f);
        Assert.That(result, Is.EqualTo(15f));
    }

    [Test]
    public void ApplyHeal_DeadPlayer_HealthUnchanged()
    {
        float result = ApplyHeal_WithDeadGuard(isDead: true, 50f, 30f, 100f);
        Assert.That(result, Is.EqualTo(50f), "Dead player should not be healed");
    }

    [Test]
    public void HealAndDamage_Sequence_CorrectHealth()
    {
        float hp = 100f;
        hp = ApplyDamage(hp, 60f); // 40 HP
        hp = ApplyHeal(hp, 30f, 100f); // 70 HP
        hp = ApplyDamage(hp, 25f); // 45 HP
        Assert.That(hp, Is.EqualTo(45f));
        Assert.That(IsDead(hp), Is.False);
    }
}
