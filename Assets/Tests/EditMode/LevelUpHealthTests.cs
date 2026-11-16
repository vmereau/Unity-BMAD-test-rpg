using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Level-up max-health gain (spec-level-up-max-health): PlayerHealth.HandleLevelUp adds
    /// ProgressionConfigSO.healthPerLevel to max and current health through the saved bonus.
    /// Edit Mode: Awake/OnEnable don't run on AddComponent, so dependencies are wired by reflection.
    /// </summary>
    public class LevelUpHealthTests
    {
        private GameObject _go;
        private PlayerHealth _health;
        private ProgressionConfigSO _progression;
        private int _changedCount;
        private readonly List<Object> _cleanup = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPlayer");
            _cleanup.Add(_go);
            _health = _go.AddComponent<PlayerHealth>();

            var combat = SO<CombatConfigSO>();
            combat.baseHealth = 100f;
            Set("_config", combat);

            _progression = SO<ProgressionConfigSO>();
            _progression.healthPerLevel = 10;
            Set("_progressionConfig", _progression);

            var changed = SO<GameEventSO_Float>();
            Set("_onPlayerHealthChanged", changed);
            changed.AddListener(_ => _changedCount++);

            _health.RestoreHealth(60f);
            _changedCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private T SO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private void Set(string field, object value) =>
            typeof(PlayerHealth).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_health, value);

        private void LevelUp(int level) =>
            typeof(PlayerHealth).GetMethod("HandleLevelUp", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_health, new object[] { level });

        [Test]
        public void LevelUp_RaisesMaxAndCurrentHealth_RaisesChanged()
        {
            LevelUp(2);

            Assert.AreEqual(110f, _health.MaxHealth);
            Assert.AreEqual(70f, _health.CurrentHealth);
            Assert.AreEqual(10, _health.BonusMaxHealth);
            Assert.AreEqual(1, _changedCount);
        }

        [Test]
        public void TwoLevelUps_Stack()
        {
            LevelUp(2);
            LevelUp(3);

            Assert.AreEqual(120f, _health.MaxHealth);
            Assert.AreEqual(80f, _health.CurrentHealth);
            Assert.AreEqual(20, _health.BonusMaxHealth);
        }

        [Test]
        public void OnLevelUpRaised_AfterOnEnable_RaisesMaxHealth()
        {
            var levelUp = SO<GameEventSO_Int>();
            Set("_onLevelUp", levelUp);
            var lifecycle = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(PlayerHealth).GetMethod("OnEnable", lifecycle).Invoke(_health, null);

            levelUp.Raise(2);
            Assert.AreEqual(110f, _health.MaxHealth);

            typeof(PlayerHealth).GetMethod("OnDisable", lifecycle).Invoke(_health, null);
            levelUp.Raise(3);
            Assert.AreEqual(110f, _health.MaxHealth);
        }

        [Test]
        public void ZeroHealthPerLevel_ChangesNothing()
        {
            _progression.healthPerLevel = 0;

            LevelUp(2);

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.AreEqual(60f, _health.CurrentHealth);
            Assert.AreEqual(0, _changedCount);
        }

        [Test]
        public void MissingProgressionConfig_ChangesNothing()
        {
            Set("_progressionConfig", null);

            LevelUp(2);

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.AreEqual(0, _changedCount);
        }
    }
}
