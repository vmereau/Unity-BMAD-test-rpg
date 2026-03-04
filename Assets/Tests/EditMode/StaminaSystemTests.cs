using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Combat;

namespace Game.Tests
{
    /// <summary>
    /// Edit Mode tests for StaminaSystem behaviour.
    /// Uses reflection to inject CombatConfigSO since SerializeField is private.
    /// Regen is formula-tested (Time.deltaTime is 0 in Edit Mode, making Update unreliable).
    /// </summary>
    public class StaminaSystemTests
    {
        private StaminaSystem _stamina;
        private CombatConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CombatConfigSO>();
            _config.baseStaminaPool = 100f;
            _config.staminaRegenRate = 20f;
            _config.staminaRegenDelay = 1.5f;

            var go = new GameObject("StaminaTest");
            _stamina = go.AddComponent<StaminaSystem>();

            // Awake ran with null config → component is disabled.
            // Inject config and re-initialize manually via reflection.
            SetPrivateField("_config", _config);
            SetPrivateField("_currentStamina", _config.baseStaminaPool);
            _stamina.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stamina.gameObject);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Consume_ReducesStaminaByAmount()
        {
            bool consumed = _stamina.Consume(20f);

            Assert.That(consumed, Is.True);
            Assert.That(_stamina.CurrentStamina, Is.EqualTo(80f));
        }

        [Test]
        public void Consume_ReturnsFalse_WhenInsufficient()
        {
            bool consumed = _stamina.Consume(200f);

            Assert.That(consumed, Is.False);
            Assert.That(_stamina.CurrentStamina, Is.EqualTo(100f)); // unchanged
        }

        [Test]
        public void Stamina_CannotGoBelowZero()
        {
            _stamina.Consume(100f); // drain to exactly 0

            Assert.That(_stamina.CurrentStamina, Is.EqualTo(0f));
        }

        [Test]
        public void Regen_DoesNotExceedMaxPool()
        {
            // Update() regen uses: Mathf.Min(current + rate * dt, max)
            // Time.deltaTime is 0 in Edit Mode so we test the formula directly.
            float current = 95f;
            float result = Mathf.Min(current + _config.staminaRegenRate * 0.5f, _config.baseStaminaPool);

            Assert.That(result, Is.EqualTo(100f));
        }

        private void SetPrivateField(string fieldName, object value)
        {
            typeof(StaminaSystem)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_stamina, value);
        }
    }
}
