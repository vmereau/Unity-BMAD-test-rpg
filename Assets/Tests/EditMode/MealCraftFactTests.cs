using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Crafting;
using Game.Inventory;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// <see cref="MealEatenFact"/> / <see cref="CraftedFact"/> evaluation and change events, the daily ration store in
    /// <see cref="WorldStateManager"/>, and <see cref="CookingRules.IsMealDiscovery"/>.
    /// </summary>
    public class MealCraftFactTests
    {
        private const double HOUR = GameTime.SECONDS_PER_HOUR;

        private static readonly FieldInfo s_wsmInstance =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_clockActive =
            typeof(GameClock).GetField("<Active>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_factChanged =
            typeof(WorldStateManager).GetField("_onFactChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        private WorldStateManager _wsm;
        private GameClock _clock;
        private readonly List<Object> _cleanup = new List<Object>();
        private readonly List<string> _raised = new List<string>();

        [SetUp]
        public void SetUp()
        {
            var wsmGo = new GameObject("WSM_MealCraftTest");
            _wsm = wsmGo.AddComponent<WorldStateManager>();
            s_wsmInstance.SetValue(null, _wsm);
            var clockGo = new GameObject("Clock_MealCraftTest");
            clockGo.SetActive(false); // no Awake: it would need a TimeConfigSO
            _clock = clockGo.AddComponent<GameClock>();
            s_clockActive.SetValue(null, _clock);
            _clock.Restore(10 * HOUR);
            var changed = ScriptableObject.CreateInstance<GameEventSO_Fact>();
            changed.AddListener(d => _raised.Add(d.key));
            s_factChanged.SetValue(_wsm, changed);
            _raised.Clear();
            _cleanup.AddRange(new Object[] { wsmGo, clockGo, changed });
        }

        [TearDown]
        public void TearDown()
        {
            s_wsmInstance.SetValue(null, null);
            s_clockActive.SetValue(null, null);
            foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(o);
            return o;
        }

        private PotionItemSO CreateFood(ConsumableKind kind, int maxHpBonus)
        {
            var food = Create<PotionItemSO>();
            food.itemId = "food-" + System.Guid.NewGuid().ToString("N");
            var so = new SerializedObject(food);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            var list = so.FindProperty("_permanentBonuses");
            list.arraySize = maxHpBonus > 0 ? 1 : 0;
            if (maxHpBonus > 0)
            {
                list.GetArrayElementAtIndex(0).FindPropertyRelative("stat").enumValueIndex = (int)MealBonusStat.MaxHealth;
                list.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = maxHpBonus;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return food;
        }

        // ── MealEatenFact ─────────────────────────────────────────────────────

        [Test]
        public void MealEaten_FalseUntilClaimed_ThenTrue_RaisesOnce()
        {
            var meal = CreateFood(ConsumableKind.Meal, 5);
            var fact = Create<MealEatenFact>().Init(meal);
            Assert.IsFalse(_wsm.GetFact(fact));

            _wsm.ClaimMealBonus(meal);
            _wsm.ClaimMealBonus(meal);

            Assert.IsTrue(_wsm.GetFact(fact));
            CollectionAssert.AreEqual(new[] { fact.ToString() }, _raised);
        }

        [Test] public void MealEaten_NoMeal_IsFalse() => Assert.IsFalse(_wsm.GetFact(Create<MealEatenFact>()));

        [Test] public void MealEaten_Problem_NoBonus() =>
            StringAssert.Contains("no first-meal bonus", MealEatenFact.Problem(CreateFood(ConsumableKind.Meal, 0)));
        [Test] public void MealEaten_Problem_NoMeal() => Assert.IsNotNull(MealEatenFact.Problem(null));
        [Test] public void MealEaten_Problem_Fine() => Assert.IsNull(MealEatenFact.Problem(CreateFood(ConsumableKind.Meal, 5)));

        // ── CraftedFact ───────────────────────────────────────────────────────

        [Test]
        public void Crafted_FirstCraftRaises_LaterCraftsDont()
        {
            var recipe = Create<RecipeFact>().Init("CraftTest");
            var fact = Create<CraftedFact>().Init(recipe);
            Assert.IsFalse(_wsm.GetFact(fact));

            _wsm.AddRecipeCraft(recipe);
            _wsm.AddRecipeCraft(recipe);

            Assert.IsTrue(_wsm.GetFact(fact));
            CollectionAssert.AreEqual(new[] { fact.ToString() }, _raised);
            Assert.AreEqual("Crafted.CraftTest", fact.ToString());
        }

        [Test]
        public void Crafted_AfterRestore_TrueWithoutEvent()
        {
            var recipe = Create<RecipeFact>().Init("CraftRestore");
            _wsm.RestoreRecipeCrafts(new Dictionary<string, int> { { recipe.ToString(), 3 } });
            Assert.IsTrue(_wsm.GetFact(Create<CraftedFact>().Init(recipe)));
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void Computed_SetFact_IsRefused()
        {
            var meal = CreateFood(ConsumableKind.Meal, 5);
            var fact = Create<MealEatenFact>().Init(meal);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            _wsm.SetFact(fact, true);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.IsFalse(_wsm.GetFact(fact));
            Assert.IsEmpty(_raised);
        }

        [Test]
        public void Crafted_EmptyRecipeId_RaisesNothing()
        {
            _wsm.AddRecipeCraft(Create<RecipeFact>().Init(""));
            Assert.IsEmpty(_raised);
        }

        [Test] public void Crafted_Problem_NoRecipe() => Assert.IsNotNull(CraftedFact.Problem(null));
        [Test] public void Crafted_Problem_EmptyId() => Assert.IsNotNull(CraftedFact.Problem(Create<RecipeFact>().Init("")));

        // ── Daily rations ─────────────────────────────────────────────────────

        [Test]
        public void Ration_TakeTwice_ThenResetsAtWakeHourNotMidnight()
        {
            const int wake = 5;
            Assert.AreEqual(2, _wsm.GetDailyRationRemaining("r1", 2, wake));
            _wsm.TakeDailyRation("r1", wake);
            _wsm.TakeDailyRation("r1", wake);
            Assert.AreEqual(0, _wsm.GetDailyRationRemaining("r1", 2, wake));

            _clock.Restore(24 * HOUR + 1 * HOUR); // 01:00 next day: same ration day
            Assert.AreEqual(0, _wsm.GetDailyRationRemaining("r1", 2, wake));

            _clock.Restore(24 * HOUR + 7 * HOUR); // 07:00: the NPC got up at 05:00
            Assert.AreEqual(2, _wsm.GetDailyRationRemaining("r1", 2, wake));
        }

        [Test]
        public void Ration_CaptureRestore_RoundTrips_NullIsFull()
        {
            _wsm.TakeDailyRation("r2", 5);
            var saved = _wsm.CaptureDailyRations();
            _wsm.RestoreDailyRations(null);
            Assert.AreEqual(2, _wsm.GetDailyRationRemaining("r2", 2, 5));
            _wsm.RestoreDailyRations(saved);
            Assert.AreEqual(1, _wsm.GetDailyRationRemaining("r2", 2, 5));
        }

        [Test] public void Ration_EmptyId_NoneLeft() => Assert.AreEqual(0, _wsm.GetDailyRationRemaining("", 2, 5));

        // ── Meal discovery ────────────────────────────────────────────────────

        [Test] public void Discovery_LearnedMeal_True() => Assert.IsTrue(CookingRules.IsMealDiscovery(true, CreateFood(ConsumableKind.Meal, 0)));
        [Test] public void Discovery_KnownRecipe_False() => Assert.IsFalse(CookingRules.IsMealDiscovery(false, CreateFood(ConsumableKind.Meal, 0)));
        [Test] public void Discovery_Potion_False() => Assert.IsFalse(CookingRules.IsMealDiscovery(true, CreateFood(ConsumableKind.Potion, 0)));
        [Test] public void Discovery_Null_False() => Assert.IsFalse(CookingRules.IsMealDiscovery(true, null));
    }
}
