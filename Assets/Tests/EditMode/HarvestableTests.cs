using Game.Inventory;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure Harvestable prompt and yield rules.
    /// </summary>
    public class HarvestableTests
    {
        [Test]
        public void ResolvePrompt_Legal_Harvest()
        {
            Assert.AreEqual("Harvest", Harvestable.ResolvePrompt(false));
        }

        [Test]
        public void ResolvePrompt_Illegal_Steal()
        {
            Assert.AreEqual("Steal", Harvestable.ResolvePrompt(true));
        }

        [Test]
        public void ResolveNameTag_UsesItemName()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = "Healing Plant";
            try { Assert.AreEqual("Healing Plant", Harvestable.ResolveNameTag(item)); }
            finally { Object.DestroyImmediate(item); }
        }

        [Test]
        public void ResolveNameTag_NullOrUnnamed_FallsBack()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = "";
            try
            {
                Assert.AreEqual("plant", Harvestable.ResolveNameTag(null));
                Assert.AreEqual("plant", Harvestable.ResolveNameTag(item));
            }
            finally { Object.DestroyImmediate(item); }
        }

        [TestCase(1, 1)]
        [TestCase(3, 3)]
        [TestCase(0, 1)]
        [TestCase(-2, 1)]
        public void ClampYield_NeverBelowOne(int authored, int expected)
        {
            Assert.AreEqual(expected, Harvestable.ClampYield(authored));
        }
    }
}
