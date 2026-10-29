using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Crafting;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Recipe items (learn on use, rejected when known) and the per-recipe craft count stored in
    /// <see cref="WorldStateManager"/> (count, capture / restore for saves, display text).
    /// </summary>
    public class RecipeItemTests
    {
        // Same isolation trick as WorldStateManagerFactsTests: force Instance to the test component.
        private static readonly FieldInfo s_instanceField =
            typeof(WorldStateManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<Object> _cleanup = new List<Object>();
        private WorldStateManager _wsm;
        private GameObject _user;

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
        }

        [TearDown]
        public void TearDown()
        {
            s_instanceField.SetValue(null, null);
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _cleanup.Add(so);
            return so;
        }

        private RecipeFact Fact(string id)
        {
            var fact = Create<RecipeFact>();
            fact.Init(id);
            return fact;
        }

        private RecipeItemSO CreateRecipeItem(RecipeFact fact, bool withOutput = true)
        {
            var recipe = Create<RecipeSO>();
            recipe.knownFact = fact;
            recipe.ingredients = new[] { Create<ItemSO>() };
            if (withOutput)
            {
                recipe.output = Create<ItemSO>();
                recipe.output.itemName = "Sandwich";
            }
            var item = Create<RecipeItemSO>();
            item.consumable = true;
            var so = new SerializedObject(item);
            so.FindProperty("_recipe").objectReferenceValue = recipe;
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // ── Recipe item ───────────────────────────────────────────────────────

        [Test]
        public void Use_UnknownRecipe_LearnsItAndSucceeds()
        {
            var fact = Fact("Sandwich");
            var item = CreateRecipeItem(fact);

            Assert.IsTrue(item.CanUse());
            Assert.IsTrue(item.OnUse(_user));
            Assert.IsTrue(_wsm.IsRecipeKnown(fact));
        }

        [Test]
        public void Use_KnownRecipe_IsRejectedAndNotUsable()
        {
            var fact = Fact("Sandwich");
            _wsm.SetRecipeKnown(fact);
            var item = CreateRecipeItem(fact);

            Assert.IsFalse(item.CanUse());
            Assert.IsFalse(item.OnUse(_user));
        }

        [Test]
        public void Use_Misconfigured_IsRejected()
        {
            var noFact = CreateRecipeItem(null);
            var noOutput = CreateRecipeItem(Fact("Sandwich"), withOutput: false);

            Assert.IsFalse(noFact.CanUse());
            Assert.IsFalse(noFact.OnUse(_user));
            Assert.IsFalse(noOutput.OnUse(_user));
        }

        [Test]
        public void Use_EmptyRecipeId_IsRejected()
        {
            var item = CreateRecipeItem(Fact(""));

            Assert.IsFalse(item.CanUse());
            Assert.IsFalse(item.OnUse(_user));
        }

        [Test]
        public void Use_WithoutWorldStateManager_IsRejected()
        {
            var item = CreateRecipeItem(Fact("Sandwich"));
            s_instanceField.SetValue(null, null);

            Assert.IsFalse(item.CanUse());
            Assert.IsFalse(item.OnUse(_user));
        }

        // ── Craft count ───────────────────────────────────────────────────────

        [Test]
        public void CraftCount_CountsPerRecipe()
        {
            var sandwich = Fact("Sandwich");
            var potion = Fact("Potion");

            _wsm.AddRecipeCraft(sandwich);
            _wsm.AddRecipeCraft(sandwich);

            Assert.AreEqual(2, _wsm.GetRecipeCraftCount(sandwich));
            Assert.AreEqual(2, _wsm.GetRecipeCraftCount(Fact("Sandwich")), "keyed by recipe id, not by asset");
            Assert.AreEqual(0, _wsm.GetRecipeCraftCount(potion));
            Assert.AreEqual(0, _wsm.GetRecipeCraftCount(null));
        }

        [Test]
        public void CraftCount_CaptureRestore_RoundTrips()
        {
            var sandwich = Fact("Sandwich");
            _wsm.AddRecipeCraft(sandwich);
            _wsm.AddRecipeCraft(sandwich);
            var saved = _wsm.CaptureRecipeCrafts();

            _wsm.AddRecipeCraft(sandwich);
            _wsm.RestoreRecipeCrafts(saved);

            Assert.AreEqual(2, _wsm.GetRecipeCraftCount(sandwich));
        }

        [Test]
        public void CraftCount_RestoreNull_ClearsCounts()
        {
            var sandwich = Fact("Sandwich");
            _wsm.AddRecipeCraft(sandwich);

            _wsm.RestoreRecipeCrafts(null);

            Assert.AreEqual(0, _wsm.GetRecipeCraftCount(sandwich));
        }

        [TestCase(0, "Never cooked")]
        [TestCase(1, "Cooked once")]
        [TestCase(3, "Cooked 3 times")]
        public void FormatCraftCount_Text(int count, string expected)
        {
            Assert.AreEqual(expected, CookingUI.FormatCraftCount(count));
        }

        [TestCase(0, "Never forged")]
        [TestCase(1, "Forged once")]
        [TestCase(3, "Forged 3 times")]
        public void FormatCraftCount_Smithing_Text(int count, string expected)
        {
            Assert.AreEqual(expected, CookingUI.FormatCraftCount(count, CraftingKind.Smithing));
        }

        [Test]
        public void FormatSkillRequired_NamesSkill()
        {
            Assert.AreEqual("You need the Smithing skill", CookingSystem.FormatSkillRequired("Smithing"));
        }
    }
}
