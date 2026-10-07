using System.Collections.Generic;
using Game.Inventory;
using Game.Progression;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    public class ItemDetailFormatterTests
    {
        private readonly List<Object> _created = new();
        private readonly List<ItemStatLine> _lines = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
            _lines.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        private static void SetSerialized(Object target, string field, System.Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            Assert.IsNotNull(prop, $"Serialized field '{field}' not found on {target.GetType().Name}");
            assign(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private PotionItemSO CreatePotion(float heal)
        {
            var potion = Create<PotionItemSO>();
            SetSerialized(potion, "_healAmount", p => p.floatValue = heal);
            return potion;
        }

        private SkillSO CreateSkill(string name, int lpCost, string description)
        {
            var skill = Create<SkillSO>();
            SetSerialized(skill, "_displayName", p => p.stringValue = name);
            SetSerialized(skill, "_lpCost", p => p.intValue = lpCost);
            SetSerialized(skill, "_description", p => p.stringValue = description);
            return skill;
        }

        private SkillItemSO CreateTome(SkillSO skill)
        {
            var tome = Create<SkillItemSO>();
            if (skill != null)
                SetSerialized(tome, "_skill", p => p.objectReferenceValue = skill);
            return tome;
        }

        private static void AssertLine(ItemStatLine line, string label, string value, StatPolarity polarity)
        {
            Assert.AreEqual(label, line.Label);
            Assert.AreEqual(value, line.Value);
            Assert.AreEqual(polarity, line.Polarity);
        }

        // AC 1
        [Test]
        public void Weapon_ShowsDamageThenBonusesInOrder()
        {
            var sword = Create<SwordSO>();
            sword.damageBonus = 20f;
            sword.strengthBonus = 3;
            sword.dexterityBonus = 1;

            ItemDetailFormatter.BuildStatLines(sword, _lines);

            Assert.AreEqual(3, _lines.Count);
            AssertLine(_lines[0], "Damage", "+20", StatPolarity.Positive);
            AssertLine(_lines[1], "Strength", "+3", StatPolarity.Positive);
            AssertLine(_lines[2], "Dexterity", "+1", StatPolarity.Positive);
            Assert.AreEqual("Weapon", ItemDetailFormatter.GetCategory(sword));
        }

        // AC 2
        [Test]
        public void Armor_SlotInCategory_NegativeBonusIsNegative_ZeroBonusesOmitted()
        {
            var helmet = Create<ArmorSO>();
            helmet.slot = EquipmentSlot.Helmet;
            helmet.defenseBonus = 2;
            helmet.strengthBonus = -1;

            ItemDetailFormatter.BuildStatLines(helmet, _lines);

            Assert.AreEqual("Armor · Helmet", ItemDetailFormatter.GetCategory(helmet));
            Assert.AreEqual(2, _lines.Count);
            AssertLine(_lines[0], "Strength", "-1", StatPolarity.Negative);
            AssertLine(_lines[1], "Defense", "+2", StatPolarity.Positive);
        }

        [Test]
        public void Armor_BodySlot_CategoryReadsBodyArmor()
        {
            var armor = Create<ArmorSO>();
            armor.slot = EquipmentSlot.Armor;
            Assert.AreEqual("Armor · Body Armor", ItemDetailFormatter.GetCategory(armor));
        }

        [TestCase(EquipmentSlot.Ring1, "Armor · Ring")]
        [TestCase(EquipmentSlot.Ring2, "Armor · Ring")]
        [TestCase(EquipmentSlot.Necklace, "Armor · Necklace")]
        public void Armor_JewelrySlots_CategoryNames(EquipmentSlot slot, string expected)
        {
            var armor = Create<ArmorSO>();
            armor.slot = slot;
            Assert.AreEqual(expected, ItemDetailFormatter.GetCategory(armor));
        }

        [Test]
        public void Weapon_NegativeDamage_IsNegative()
        {
            var sword = Create<SwordSO>();
            sword.damageBonus = -4f;

            ItemDetailFormatter.BuildStatLines(sword, _lines);

            Assert.AreEqual(1, _lines.Count);
            AssertLine(_lines[0], "Damage", "-4", StatPolarity.Negative);
        }

        [Test]
        public void Weapon_DamageRoundingToZero_HasNoDamageRow()
        {
            var sword = Create<SwordSO>();
            sword.damageBonus = 0.4f;

            ItemDetailFormatter.BuildStatLines(sword, _lines);

            Assert.AreEqual(0, _lines.Count);
        }

        // AC 3
        [Test]
        public void Potion_ShowsHealConsumableAndStack()
        {
            var potion = CreatePotion(30f);
            potion.consumable = true;
            potion.maxStacks = 10;

            ItemDetailFormatter.BuildStatLines(potion, _lines);

            Assert.AreEqual("Potion", ItemDetailFormatter.GetCategory(potion));
            Assert.AreEqual(3, _lines.Count);
            AssertLine(_lines[0], "Restores Health", "+30", StatPolarity.Positive);
            AssertLine(_lines[1], "Consumable", "", StatPolarity.Neutral);
            AssertLine(_lines[2], "Stack", "up to 10", StatPolarity.Neutral);
        }

        [Test]
        public void Usable_NotConsumable_ShowsReusableTag()
        {
            var potion = CreatePotion(0f);
            potion.consumable = false;

            ItemDetailFormatter.BuildStatLines(potion, _lines);

            Assert.AreEqual(1, _lines.Count);
            AssertLine(_lines[0], "Reusable", "", StatPolarity.Neutral);
        }

        // AC 4
        [Test]
        public void SkillTome_ShowsTeachesAndLpCost_AndSkillDescription()
        {
            var tome = CreateTome(CreateSkill("Power Strike", 3, "X"));

            ItemDetailFormatter.BuildStatLines(tome, _lines);

            Assert.AreEqual("Skill Tome", ItemDetailFormatter.GetCategory(tome));
            Assert.GreaterOrEqual(_lines.Count, 2);
            AssertLine(_lines[0], "Teaches", "Power Strike", StatPolarity.Neutral);
            AssertLine(_lines[1], "LP Cost", "3", StatPolarity.Neutral);
            Assert.AreEqual("X", ItemDetailFormatter.GetSkillDescription(tome));
        }

        [Test]
        public void SkillTome_BlankSkillDescription_ReturnsNull()
        {
            var tome = CreateTome(CreateSkill("Power Strike", 3, "   "));
            Assert.IsNull(ItemDetailFormatter.GetSkillDescription(tome));
        }

        // AC 5
        [Test]
        public void SkillTome_NoSkill_NoSkillRowsAndNullDescription()
        {
            var tome = CreateTome(null);

            Assert.DoesNotThrow(() => ItemDetailFormatter.BuildStatLines(tome, _lines));

            foreach (var line in _lines)
            {
                Assert.AreNotEqual("Teaches", line.Label);
                Assert.AreNotEqual("LP Cost", line.Label);
            }
            Assert.IsNull(ItemDetailFormatter.GetSkillDescription(tome));
        }

        // AC 6
        [Test]
        public void PlainItem_NotStackable_HasNoRowsAndMiscCategory()
        {
            var item = Create<ItemSO>();
            item.maxStacks = 1;

            ItemDetailFormatter.BuildStatLines(item, _lines);

            Assert.AreEqual(0, _lines.Count);
            Assert.AreEqual("Miscellaneous", ItemDetailFormatter.GetCategory(item));
        }

        [Test]
        public void PlainItem_Stackable_HasSingleStackRow()
        {
            var item = Create<ItemSO>();
            item.maxStacks = 50;

            ItemDetailFormatter.BuildStatLines(item, _lines);

            Assert.AreEqual(1, _lines.Count);
            AssertLine(_lines[0], "Stack", "up to 50", StatPolarity.Neutral);
        }

        // AC 7
        [Test]
        public void Price_DependsOnContext()
        {
            var item = Create<ItemSO>();
            item.buyValue = 15;
            item.sellValue = 5;

            Assert.AreEqual("Value", ItemDetailFormatter.GetPriceLabel(ItemPriceContext.Value));
            Assert.AreEqual(5, ItemDetailFormatter.GetPrice(item, ItemPriceContext.Value));
            Assert.AreEqual("Price", ItemDetailFormatter.GetPriceLabel(ItemPriceContext.Buy));
            Assert.AreEqual(15, ItemDetailFormatter.GetPrice(item, ItemPriceContext.Buy));
            Assert.AreEqual("Sells for", ItemDetailFormatter.GetPriceLabel(ItemPriceContext.Sell));
            Assert.AreEqual(5, ItemDetailFormatter.GetPrice(item, ItemPriceContext.Sell));
        }

        // AC 8
        [Test]
        public void NullItem_IsSafe()
        {
            _lines.Add(new ItemStatLine("Stale", "1"));

            Assert.DoesNotThrow(() => ItemDetailFormatter.BuildStatLines(null, _lines));
            Assert.AreEqual(0, _lines.Count);
            Assert.AreEqual("", ItemDetailFormatter.GetCategory(null));
            Assert.IsNull(ItemDetailFormatter.GetSkillDescription(null));
            Assert.AreEqual(0, ItemDetailFormatter.GetPrice(null, ItemPriceContext.Buy));
        }

        // AC 9
        [Test]
        public void BuildStatLines_ReusedList_ContainsOnlyNewItemLines()
        {
            var sword = Create<SwordSO>();
            sword.damageBonus = 5f;
            sword.strengthBonus = 3;
            var leg = Create<ItemSO>();
            leg.maxStacks = 50;

            ItemDetailFormatter.BuildStatLines(sword, _lines);
            ItemDetailFormatter.BuildStatLines(leg, _lines);

            Assert.AreEqual(1, _lines.Count);
            AssertLine(_lines[0], "Stack", "up to 50", StatPolarity.Neutral);
        }
    }
}
