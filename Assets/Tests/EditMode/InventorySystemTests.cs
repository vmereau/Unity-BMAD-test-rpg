using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Game.Inventory;
using Game.Player;
using Game.UI;
using UnityEngine;

namespace Tests.EditMode
{
    public class InventorySystemTests
    {
        private InventorySystem _inventory;
        private readonly List<ItemSO> _createdItems = new List<ItemSO>();

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestInventory");
            _inventory = go.AddComponent<InventorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inventory.gameObject);
            foreach (var item in _createdItems)
                Object.DestroyImmediate(item);
            _createdItems.Clear();
        }

        private ItemSO CreateTestItem(string name = "Test Item")
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = name;
            _createdItems.Add(item);
            return item;
        }

        private PotionItemSO CreateTestPotion(string name = "Test Potion", int maxStacks = 10)
        {
            var item = ScriptableObject.CreateInstance<PotionItemSO>();
            item.itemName = name;
            item.maxStacks = maxStacks;
            item.consumable = true;
            _createdItems.Add(item);
            return item;
        }

        [Test]
        public void AddItem_ValidItem_ReturnsTrue()
        {
            var item = CreateTestItem();
            Assert.IsTrue(_inventory.AddItem(item));
        }

        [Test]
        public void AddItem_ValidItem_IncreasesCount()
        {
            var item = CreateTestItem();
            _inventory.AddItem(item);
            Assert.AreEqual(1, _inventory.Count);
        }

        [Test]
        public void AddItem_NullItem_ReturnsFalse()
        {
            Assert.IsFalse(_inventory.AddItem(null));
        }

        [Test]
        public void AddItem_NullItem_CountUnchanged()
        {
            _inventory.AddItem(null);
            Assert.AreEqual(0, _inventory.Count);
        }

        [Test]
        public void AddItem_MultipleItems_AllAdded()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));
            _inventory.AddItem(CreateTestItem("C"));
            Assert.AreEqual(3, _inventory.Count);
        }

        [Test]
        public void Items_ReflectsAddedItems()
        {
            var item1 = CreateTestItem("Sword");
            var item2 = CreateTestItem("Shield");
            _inventory.AddItem(item1);
            _inventory.AddItem(item2);

            Assert.AreEqual(2, _inventory.Items.Count);
            Assert.AreEqual(item1, _inventory.Items[0].Item);
            Assert.AreEqual(item2, _inventory.Items[1].Item);
        }

        [Test]
        public void ItemPickup_Configure_SetsInteractPrompt()
        {
            var go = new GameObject("TestPickup");
            var pickup = go.AddComponent<ItemPickup>();
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = "Magic Sword";

            pickup.Configure(item);

            Assert.AreEqual("Press E to pick up Magic Sword", pickup.InteractPrompt);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(item);
        }

        // ItemPickup null-guard: disabled component must not throw when Interact() is called
        // (InteractionSystem.GetComponentInParent returns disabled MonoBehaviours — HIGH-1 regression guard)
        // Note: Edit Mode tests do not run Awake() automatically, so we manually simulate the disabled state.
        [Test]
        public void ItemPickup_Interact_WhenDisabledDueToNullItem_DoesNotThrow()
        {
            var go = new GameObject("TestPickup");
            var pickup = go.AddComponent<ItemPickup>();
            // Manually disable to simulate Awake() disabling the component when _item is null.
            // In Edit Mode tests, Awake() lifecycle is not triggered by AddComponent.
            pickup.enabled = false;
            // _inventory is null (no InventorySystem in test scene) — Interact() must null-guard and not throw
            Assert.DoesNotThrow(() => pickup.Interact());
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MoveItem_ValidIndices_SwapsItems()
        {
            var itemA = CreateTestItem("A");
            var itemB = CreateTestItem("B");
            var itemC = CreateTestItem("C");
            _inventory.AddItem(itemA);
            _inventory.AddItem(itemB);
            _inventory.AddItem(itemC);

            _inventory.MoveItem(0, 2);

            Assert.AreEqual(itemC, _inventory.Items[0].Item);
            Assert.AreEqual(itemB, _inventory.Items[1].Item);
            Assert.AreEqual(itemA, _inventory.Items[2].Item);
        }

        [Test]
        public void MoveItem_SameIndex_NoChange()
        {
            var itemA = CreateTestItem("A");
            _inventory.AddItem(itemA);

            _inventory.MoveItem(0, 0);

            Assert.AreEqual(1, _inventory.Count);
            Assert.AreEqual(itemA, _inventory.Items[0].Item);
        }

        [Test]
        public void MoveItem_OutOfBoundsFrom_LogsWarn_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            Assert.DoesNotThrow(() => _inventory.MoveItem(-1, 0));
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void MoveItem_OutOfBoundsTo_LogsWarn_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            Assert.DoesNotThrow(() => _inventory.MoveItem(0, 5));
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void RemoveItem_ValidIndex_RemovesAndReturnsItem()
        {
            var itemA = CreateTestItem("A");
            var itemB = CreateTestItem("B");
            var itemC = CreateTestItem("C");
            _inventory.AddItem(itemA);
            _inventory.AddItem(itemB);
            _inventory.AddItem(itemC);

            var removed = _inventory.RemoveItem(1);

            Assert.AreEqual(itemB, removed);
            Assert.AreEqual(2, _inventory.Count);
            Assert.AreEqual(itemA, _inventory.Items[0].Item);
            Assert.AreEqual(itemC, _inventory.Items[1].Item);
        }

        [Test]
        public void RemoveItem_OutOfBoundsIndex_ReturnsNull_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));
            _inventory.AddItem(CreateTestItem("B"));

            var result = _inventory.RemoveItem(5);

            Assert.IsNull(result);
            Assert.AreEqual(2, _inventory.Count);
        }

        [Test]
        public void RemoveItem_NegativeIndex_ReturnsNull_NoThrow()
        {
            _inventory.AddItem(CreateTestItem("A"));

            var result = _inventory.RemoveItem(-1);

            Assert.IsNull(result);
            Assert.AreEqual(1, _inventory.Count);
        }

        // ── Stacking tests (Story 4.10) ────────────────────────────────────

        [Test]
        public void AddItem_Stackable_TwoUnits_MergesIntoOneSlot()
        {
            var potion = CreateTestPotion(maxStacks: 10);

            _inventory.AddItem(potion);
            _inventory.AddItem(potion);

            Assert.AreEqual(1, _inventory.Count, "Two stackable potions should occupy one slot");
            Assert.AreEqual(2, _inventory.Items[0].Count);
            Assert.AreEqual(potion, _inventory.Items[0].Item);
        }

        [Test]
        public void AddItem_Stackable_AtMaxStacks_CreatesNewSlot()
        {
            var potion = CreateTestPotion(maxStacks: 2);

            _inventory.AddItem(potion); // slot 0, count 1
            _inventory.AddItem(potion); // slot 0, count 2 (full)
            _inventory.AddItem(potion); // slot 1, count 1 (overflow)

            Assert.AreEqual(2, _inventory.Count, "Third potion should open a new slot when first is full");
            Assert.AreEqual(2, _inventory.Items[0].Count);
            Assert.AreEqual(1, _inventory.Items[1].Count);
        }

        [Test]
        public void AddItem_NonStackable_TwoIdenticalItems_CreatesTwoSlots()
        {
            var item = CreateTestItem("Sword"); // maxStacks defaults to 1 → IsStackable = false

            _inventory.AddItem(item);
            _inventory.AddItem(item);

            Assert.AreEqual(2, _inventory.Count, "Non-stackable items should each occupy their own slot");
            Assert.AreEqual(1, _inventory.Items[0].Count);
            Assert.AreEqual(1, _inventory.Items[1].Count);
        }

        [Test]
        public void AddItem_Stackable_SlotCountStartsAtOne()
        {
            var potion = CreateTestPotion();

            _inventory.AddItem(potion);

            Assert.AreEqual(1, _inventory.Items[0].Count);
        }

        [Test]
        public void DecrementStack_CountAboveOne_DecrementsCount()
        {
            var potion = CreateTestPotion();
            _inventory.AddItem(potion);
            _inventory.AddItem(potion); // count = 2

            var result = _inventory.DecrementStack(0);

            Assert.AreEqual(potion, result);
            Assert.AreEqual(1, _inventory.Count, "Slot should remain");
            Assert.AreEqual(1, _inventory.Items[0].Count, "Count should be 1 after decrement");
        }

        [Test]
        public void DecrementStack_CountEqualsOne_RemovesSlot()
        {
            var potion = CreateTestPotion();
            _inventory.AddItem(potion); // count = 1

            var result = _inventory.DecrementStack(0);

            Assert.AreEqual(potion, result);
            Assert.AreEqual(0, _inventory.Count, "Slot should be removed when last unit is consumed");
        }

        [Test]
        public void DecrementStack_OutOfRangeIndex_ReturnsNull_NoThrow()
        {
            _inventory.AddItem(CreateTestItem());

            ItemSO result = null;
            Assert.DoesNotThrow(() => result = _inventory.DecrementStack(5));
            Assert.IsNull(result);
            Assert.AreEqual(1, _inventory.Count);
        }

        // ── SkillItemSO null guard: OnUse with no _skill assigned logs warning and does not throw ──

        [Test]
        public void SkillItemSO_OnUse_NullSkill_DoesNotThrow()
        {
            var skillItem = ScriptableObject.CreateInstance<SkillItemSO>();
            var go = new GameObject("Player");

            Assert.DoesNotThrow(() => skillItem.OnUse(go));

            Object.DestroyImmediate(skillItem);
            Object.DestroyImmediate(go);
        }

        // SkillItemSO null guard: OnUse on a GO without PlayerSkills component logs warning and does not throw
        [Test]
        public void SkillItemSO_OnUse_NoPlayerSkillsComponent_DoesNotThrow()
        {
            var skillItem = ScriptableObject.CreateInstance<SkillItemSO>();
            // Assign a non-null SkillSO via reflection so we reach the PlayerSkills null check
            var skill = ScriptableObject.CreateInstance<Game.Progression.SkillSO>();
            typeof(SkillItemSO)
                .GetField("_skill", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(skillItem, skill);

            var go = new GameObject("PlayerWithoutSkills"); // no PlayerSkills component

            Assert.DoesNotThrow(() => skillItem.OnUse(go));

            Object.DestroyImmediate(skillItem);
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(go);
        }

        // ── PotionItemSO tests (Story 4.10) ────────────────────────────────

        // PotionItemSO null guard: OnUse on a GO without PlayerHealth component logs warning and returns false
        [Test]
        public void PotionItemSO_OnUse_NoPlayerHealthComponent_ReturnsFalse()
        {
            var potion = ScriptableObject.CreateInstance<PotionItemSO>();
            var go = new GameObject("PlayerWithoutHealth");

            bool result = false;
            Assert.DoesNotThrow(() => result = potion.OnUse(go));
            Assert.IsFalse(result);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(go);
        }

        // PotionItemSO dead guard: OnUse on a dead player returns false without calling Heal
        [Test]
        public void PotionItemSO_OnUse_DeadPlayer_ReturnsFalse()
        {
            var potion = ScriptableObject.CreateInstance<PotionItemSO>();
            var go = new GameObject("DeadPlayer");
            var health = go.AddComponent<PlayerHealth>();
            // PlayerHealth.IsDead is { get; private set; } — force via backing field
            typeof(PlayerHealth)
                .GetField("<IsDead>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(health, true);

            bool result = false;
            Assert.DoesNotThrow(() => result = potion.OnUse(go));
            Assert.IsFalse(result);

            Object.DestroyImmediate(potion);
            Object.DestroyImmediate(go);
        }

        // InventoryUI.UseItem null guard: out-of-range slot index logs warning and does not throw
        // Note: AddComponent in Edit Mode does not call Awake, so _input stays null.
        // UseItem does not touch _input, so this is safe. _inventorySystem is wired via reflection.
        [Test]
        public void InventoryUI_UseItem_OutOfRangeSlot_DoesNotThrow()
        {
            var uiGo = new GameObject("TestInventoryUI");
            var ui = uiGo.AddComponent<InventoryUI>();

            var invGo = new GameObject("TestInventory");
            var inv = invGo.AddComponent<InventorySystem>();
            typeof(InventoryUI)
                .GetField("_inventorySystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ui, inv);

            Assert.DoesNotThrow(() => ui.UseItem(5));

            Object.DestroyImmediate(uiGo);
            Object.DestroyImmediate(invGo);
        }

        // InventoryUI.UseItem null guard: non-usable item logs warning and does not remove it
        [Test]
        public void InventoryUI_UseItem_NonUsableItem_DoesNotRemoveItem()
        {
            var uiGo = new GameObject("TestInventoryUI");
            var ui = uiGo.AddComponent<InventoryUI>();

            var invGo = new GameObject("TestInventory");
            var inv = invGo.AddComponent<InventorySystem>();
            typeof(InventoryUI)
                .GetField("_inventorySystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ui, inv);

            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = "Not Usable";
            inv.AddItem(item);

            Assert.DoesNotThrow(() => ui.UseItem(0));
            Assert.AreEqual(1, inv.Count); // item not consumed

            Object.DestroyImmediate(uiGo);
            Object.DestroyImmediate(invGo);
            Object.DestroyImmediate(item);
        }
    }
}
