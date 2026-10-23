using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.Player;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// First-meal permanent bonuses: granted once per food (claim stored in <see cref="WorldStateManager"/>),
    /// max-health bonus on <see cref="PlayerHealth"/> (+ save field), bonus text and the detail "First meal" row.
    /// </summary>
    public class MealBonusTests
    {
        // Same isolation trick as RecipeItemTests: force Instance to the test component.
        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<ItemStatLine> _lines = new List<ItemStatLine>();
        private WorldStateManager _wsm;
        private GameObject _user;
        private PlayerHealth _health;
        private GameEventSO_String _onBonus;
        private readonly List<string> _toasts = new List<string>();

        [SetUp]
        public void SetUp()
        {
            Assert.IsNotNull(s_instanceField, "WorldStateManager.Instance is no longer an auto-property");
            var go = new GameObject("WorldStateManager_Test");
            _wsm = go.AddComponent<WorldStateManager>();
            _cleanup.Add(go);
            s_instanceField.SetValue(null, _wsm);

            _user = new GameObject("Player_Test");
            _cleanup.Add(_user);
            _health = _user.AddComponent<PlayerHealth>();
            var config = Create<CombatConfigSO>();
            config.baseHealth = 100f;
            Set(_health, "_config", config);
            _health.RestoreHealth(80f);

            _onBonus = Create<GameEventSO_String>();
            _onBonus.AddListener(_toasts.Add);
        }

        [TearDown]
        public void TearDown()
        {
            s_instanceField.SetValue(null, null);
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
            _lines.Clear();
            _toasts.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, $"Serialized field '{field}' not found on {target.GetType().Name}");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private PotionItemSO CreateMeal(params (MealBonusStat stat, int amount)[] bonuses)
        {
            var meal = Create<PotionItemSO>();
            meal.itemId = "meal-" + System.Guid.NewGuid().ToString("N");
            meal.consumable = true;
            var so = new SerializedObject(meal);
            so.FindProperty("_healAmount").floatValue = 10f;
            var list = so.FindProperty("_permanentBonuses");
            list.arraySize = bonuses.Length;
            for (int i = 0; i < bonuses.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("stat").enumValueIndex = (int)bonuses[i].stat;
                e.FindPropertyRelative("amount").intValue = bonuses[i].amount;
            }
            so.FindProperty("_onMealBonusGained").objectReferenceValue = _onBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return meal;
        }

        // ── Use ───────────────────────────────────────────────────────────────

        [Test]
        public void FirstMeal_HealsThenRaisesMaxAndCurrent_ClaimsAndToasts()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));

            Assert.IsTrue(meal.OnUse(_user));

            Assert.AreEqual(105f, _health.MaxHealth);
            Assert.AreEqual(95f, _health.CurrentHealth); // 80 + 10 heal + 5 bonus
            Assert.IsTrue(_wsm.IsMealBonusClaimed(meal));
            CollectionAssert.AreEqual(new[] { "+5 Max HP" }, _toasts);
        }

        [Test]
        public void SecondMeal_OnlyHeals_NoToast()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));
            meal.OnUse(_user);
            _health.RestoreHealth(100f);

            Assert.IsTrue(meal.OnUse(_user));

            Assert.AreEqual(105f, _health.MaxHealth);
            Assert.AreEqual(105f, _health.CurrentHealth); // heal capped at the raised max
            Assert.AreEqual(1, _toasts.Count);
        }

        [Test]
        public void FirstMeal_StatBonus_UpgradesBaseStat()
        {
            var stats = _user.AddComponent<PlayerStats>();
            var meal = CreateMeal((MealBonusStat.Strength, 2), (MealBonusStat.MaxHealth, 5));

            meal.OnUse(_user);

            Assert.AreEqual(2, stats.GetBaseStat(StatType.Strength));
            Assert.AreEqual(105f, _health.MaxHealth);
            CollectionAssert.AreEqual(new[] { "+2 Strength, +5 Max HP" }, _toasts);
        }

        [Test]
        public void NoWorldStateManager_HealsWithoutBonus()
        {
            s_instanceField.SetValue(null, null);
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));

            Assert.IsTrue(meal.OnUse(_user));

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.AreEqual(90f, _health.CurrentHealth);
            Assert.IsEmpty(_toasts);
        }

        [Test]
        public void DeadPlayer_RejectedAndNothingClaimed()
        {
            _health.TakeDamage(1000f);
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));

            Assert.IsFalse(meal.OnUse(_user));

            Assert.IsFalse(_wsm.IsMealBonusClaimed(meal));
            Assert.AreEqual(100f, _health.MaxHealth);
        }

        [Test]
        public void EmptyItemId_NoBonusEveryTime()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));
            meal.itemId = "";

            meal.OnUse(_user);
            meal.OnUse(_user);

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.IsEmpty(_toasts);
        }

        [Test]
        public void StatBonusWithoutPlayerStats_NothingAppliedOrClaimed()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5), (MealBonusStat.Strength, 1));

            Assert.IsTrue(meal.OnUse(_user)); // still eaten for its heal

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.IsFalse(_wsm.IsMealBonusClaimed(meal));
            Assert.IsEmpty(_toasts);
        }

        [Test]
        public void MealWithoutBonus_ClaimsNothing()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 0));

            Assert.IsTrue(meal.OnUse(_user));

            Assert.IsFalse(meal.HasPermanentBonus);
            Assert.IsFalse(_wsm.IsMealBonusClaimed(meal));
            Assert.IsEmpty(_toasts);
        }

        [Test]
        public void Claim_IsCapturedWithFacts()
        {
            var meal = CreateMeal((MealBonusStat.MaxHealth, 5));
            _wsm.ClaimMealBonus(meal);

            var facts = _wsm.CaptureFacts();
            _wsm.RestoreFacts(null);
            Assert.IsFalse(_wsm.IsMealBonusClaimed(meal));
            _wsm.RestoreFacts(facts);

            Assert.IsTrue(_wsm.IsMealBonusClaimed(meal));
        }

        // ── Max health save / restore ─────────────────────────────────────────

        [Test]
        public void RestoreMaxHealthBonus_BeforeHealth_KeepsHealthAboveBase()
        {
            _health.RestoreMaxHealthBonus(5);
            _health.RestoreHealth(103f);

            Assert.AreEqual(5, _health.BonusMaxHealth);
            Assert.AreEqual(105f, _health.MaxHealth);
            Assert.AreEqual(103f, _health.CurrentHealth);
        }

        [Test]
        public void OlderSave_WithoutBonusMaxHealth_LoadsZero()
        {
            var obj = JObject.FromObject(new PlayerSaveData { health = 50f, bonusMaxHealth = 5 });
            Assert.IsTrue(obj.Remove("bonusMaxHealth"));

            var copy = JsonConvert.DeserializeObject<PlayerSaveData>(obj.ToString());

            Assert.AreEqual(0, copy.bonusMaxHealth);
            Assert.AreEqual(50f, copy.health);
        }

        [Test]
        public void OlderSaveRestore_ZeroBonus_MaxIsBase()
        {
            _health.RestoreMaxHealthBonus(5);
            _health.RestoreMaxHealthBonus(new PlayerSaveData().bonusMaxHealth);
            _health.RestoreHealth(150f);

            Assert.AreEqual(100f, _health.MaxHealth);
            Assert.AreEqual(100f, _health.CurrentHealth);
        }

        // ── Text + detail row ─────────────────────────────────────────────────

        [Test]
        public void Toast_Text()
        {
            Assert.AreEqual("Permanent bonus: +5 Max HP", NotificationToastUI.FormatMealBonus("+5 Max HP"));
        }

        [Test]
        public void Format_JoinsPositiveEntries()
        {
            var bonuses = new List<MealBonus>
            {
                new MealBonus { stat = MealBonusStat.MaxHealth, amount = 5 },
                new MealBonus { stat = MealBonusStat.Dexterity, amount = 0 },
                new MealBonus { stat = MealBonusStat.Strength, amount = 1 }
            };

            Assert.AreEqual("+5 Max HP, +1 Strength", MealBonusText.Format(bonuses));
            Assert.AreEqual("", MealBonusText.Format(null));
        }

        [Test]
        public void DetailRow_Unclaimed_PositiveAfterHeal()
        {
            ItemDetailFormatter.BuildStatLines(CreateMeal((MealBonusStat.MaxHealth, 5)), _lines, null, _ => false);

            Assert.AreEqual("Restores Health", _lines[0].Label);
            Assert.AreEqual("First meal", _lines[1].Label);
            Assert.AreEqual("+5 Max HP", _lines[1].Value);
            Assert.AreEqual(StatPolarity.Positive, _lines[1].Polarity);
        }

        [Test]
        public void DetailRow_Claimed_MutedGained()
        {
            ItemDetailFormatter.BuildStatLines(CreateMeal((MealBonusStat.MaxHealth, 5)), _lines, null, _ => true);

            Assert.AreEqual("+5 Max HP (gained)", _lines[1].Value);
            Assert.AreEqual(StatPolarity.Muted, _lines[1].Polarity);
        }

        [Test]
        public void DetailRow_NoClaimCheck_ShownUnclaimed()
        {
            ItemDetailFormatter.BuildStatLines(CreateMeal((MealBonusStat.MaxHealth, 5)), _lines);

            Assert.AreEqual(StatPolarity.Positive, _lines[1].Polarity);
        }

        [Test]
        public void DetailRow_NoBonus_NoRow()
        {
            ItemDetailFormatter.BuildStatLines(CreateMeal(), _lines, null, _ => false);

            Assert.IsFalse(_lines.Exists(l => l.Label == "First meal"));
        }
    }
}
