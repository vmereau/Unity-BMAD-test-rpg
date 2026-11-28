using System.Collections.Generic;
using Game.Inventory;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Keys (<see cref="KeyItemSO"/>, spec-smuggling-route-locks-and-keys): category "Key", Misc tab, never parted with
    /// (no drop, sale, container or action bar), worth nothing, and every key asset in the project is one.
    /// </summary>
    public class KeyItemTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private KeyItemSO Key()
        {
            var key = ScriptableObject.CreateInstance<KeyItemSO>();
            key.itemName = "Tunnel Key";
            _created.Add(key);
            return key;
        }

        [Test]
        public void Category_Key_IsKey() => Assert.AreEqual("Key", ItemDetailFormatter.GetCategory(Key()));

        [Test]
        public void Filter_Key_IsMisc() => Assert.AreEqual(ItemFilter.Misc, ItemFilterRules.Classify(Key()));

        [Test]
        public void CanPlayerPart_Key_False() => Assert.IsFalse(ItemSO.CanPlayerPart(Key()));

        [Test]
        public void ActionBar_Assign_RefusesKey()
        {
            var go = new GameObject("ActionBarKeyTestGO");
            try
            {
                var bar = go.AddComponent<ActionBarSystem>();
                bar.Assign(0, 0, Key());
                Assert.IsFalse(bar.GetSlot(0).HasValue);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void KeyAssets_AreKeys_WorthNothing()
        {
            string[] guids = AssetDatabase.FindAssets("t:KeyItemSO", new[] { "Assets/_Game/Data" });
            var names = new List<string>();
            foreach (string guid in guids)
            {
                var key = AssetDatabase.LoadAssetAtPath<KeyItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(key, AssetDatabase.GUIDToAssetPath(guid));
                names.Add(key.name);
                Assert.AreEqual(0, key.buyValue, key.name);
                Assert.AreEqual(0, key.sellValue, key.name);
                Assert.IsFalse(key.isQuestItem, $"{key.name}: a key is not also a quest item (its category is Key)");
            }
            CollectionAssert.IsSubsetOf(new[] { "Item_Key_QuarrySouthDoor", "Key_WarehouseCellar", "Key_GangTunnel", "Key_GangBasement" }, names);
            // no key left behind as a plain item
            foreach (string guid in AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets/_Game/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("Key_") || file.StartsWith("Item_Key_"))
                    Assert.IsInstanceOf<KeyItemSO>(AssetDatabase.LoadAssetAtPath<ItemSO>(path), path);
            }
        }
    }
}
