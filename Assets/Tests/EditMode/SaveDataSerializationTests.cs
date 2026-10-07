using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SaveDataSerializationTests
    {
        private static SaveData BuildFull() => new SaveData
        {
            version = GameConstants.SAVE_FORMAT_VERSION,
            meta = new SaveMeta { slotId = "slot_1", savedAtUtc = "2026-10-07T12:34:56.0000000Z", region = "StartingTown", playerLevel = 3, playtimeSeconds = 123.5f },
            region = "StartingTown",
            worldFacts = new Dictionary<string, bool> { { "Killed.abc", true }, { "World.gate", false } },
            player = new PlayerSaveData
            {
                position = SVector3.From(new Vector3(1f, 2f, 3f)),
                rotation = SQuaternion.From(Quaternion.Euler(0f, 90f, 0f)),
                health = 42f,
                baseStrength = 11, baseDexterity = 12, baseEndurance = 13, baseIntelligence = 14,
                xp = 300, totalKills = 4, level = 3, learningPoints = 2,
                skills = new List<string> { "lockpick" },
                inventory = new List<ItemStackSaveData> { new ItemStackSaveData { itemId = "potion", count = 3 } },
                equipped = new Dictionary<string, string> { { "Weapon", "sword" } },
                actionBar = new List<ActionBarSlotSaveData> { new ActionBarSlotSaveData { slotIndex = 0, inventoryIndex = 0, itemId = "potion" } },
                gold = 77
            },
            objects = new Dictionary<string, ObjectSaveData>
            {
                { "chest", new ObjectSaveData { inventory = new List<ItemStackSaveData>(), isLocked = false } },
                { "door", new ObjectSaveData { isOpen = true } },
                { "wolf", new ObjectSaveData { hasTransform = true, position = SVector3.From(Vector3.one), gold = 5 } }
            },
            consumedObjects = new List<string> { "pickup1" },
            droppedItems = new List<DroppedItemSaveData> { new DroppedItemSaveData { region = "StartingTown", itemId = "potion", position = SVector3.From(Vector3.up) } }
        };

        [Test]
        public void RoundTrip_PreservesEveryField()
        {
            var original = BuildFull();
            string json = JsonConvert.SerializeObject(original, Formatting.Indented);
            var copy = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual(json, JsonConvert.SerializeObject(copy, Formatting.Indented));
            Assert.AreEqual(original.meta.savedAtUtc, copy.meta.savedAtUtc);
            Assert.AreEqual(true, copy.worldFacts["Killed.abc"]);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), copy.player.position.To());
            Assert.AreEqual(77, copy.player.gold);
            Assert.AreEqual("sword", copy.player.equipped["Weapon"]);
        }

        [Test]
        public void RoundTrip_NullableFieldsStayNullWhenAbsent()
        {
            var copy = JsonConvert.DeserializeObject<SaveData>(JsonConvert.SerializeObject(BuildFull()));

            var door = copy.objects["door"];
            Assert.IsNull(door.inventory);
            Assert.IsNull(door.gold);
            Assert.IsNull(door.isLocked);
            Assert.AreEqual(true, door.isOpen);
            Assert.AreEqual(false, copy.objects["chest"].isLocked);
            Assert.AreEqual(5, copy.objects["wolf"].gold);
        }

        [Test]
        public void Json_ContainsNoUnityTypes()
        {
            string json = JsonConvert.SerializeObject(BuildFull());
            StringAssert.DoesNotContain("normalized", json);
            StringAssert.DoesNotContain("magnitude", json);
            StringAssert.DoesNotContain("eulerAngles", json);
        }
    }
}
