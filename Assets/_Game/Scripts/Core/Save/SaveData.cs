using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    // Plain save model serialized by Newtonsoft Json only (no [Serializable]: Unity never serializes
    // these, and the attribute triggers Unity serialization-analyzer warnings). No UnityEngine types —
    // Newtonsoft loops on Vector3.normalized, so positions / rotations use SVector3 / SQuaternion.

    public struct SVector3
    {
        public float x, y, z;

        public static SVector3 From(Vector3 v) => new SVector3 { x = v.x, y = v.y, z = v.z };
        public Vector3 To() => new Vector3(x, y, z);
    }

    public struct SQuaternion
    {
        public float x, y, z, w;

        public static SQuaternion From(Quaternion q) => new SQuaternion { x = q.x, y = q.y, z = q.z, w = q.w };
        public Quaternion To() => new Quaternion(x, y, z, w);
    }

    public class ItemStackSaveData
    {
        public string itemId;
        public int count;
    }

    public class ActionBarSlotSaveData
    {
        public int slotIndex;
        public int inventoryIndex;
        public string itemId;
    }

    /// <summary>Header shown in the slot list without interpreting the rest of the file.</summary>
    public class SaveMeta
    {
        public string slotId;
        /// <summary>UTC, ISO 8601 round-trip format ("o").</summary>
        public string savedAtUtc;
        public string region;
        public int playerLevel;
        public float playtimeSeconds;
    }

    public class PlayerSaveData
    {
        public SVector3 position;
        public SQuaternion rotation;
        public float health;
        public int baseStrength, baseDexterity, baseEndurance, baseIntelligence;
        public int xp;
        public int totalKills;
        public int level;
        public int learningPoints;
        public List<string> skills = new List<string>();
        public List<ItemStackSaveData> inventory = new List<ItemStackSaveData>();
        /// <summary>EquipmentSlot name → itemId.</summary>
        public Dictionary<string, string> equipped = new Dictionary<string, string>();
        public List<ActionBarSlotSaveData> actionBar = new List<ActionBarSlotSaveData>();
        public int gold;
    }

    /// <summary>
    /// State of one saveable world object. Null fields mean "the object has no such component".
    /// Dead / alive is not stored here — it comes from the entity's KilledFact in worldFacts.
    /// </summary>
    public class ObjectSaveData
    {
        public bool hasTransform;
        public SVector3 position;
        public SQuaternion rotation;
        /// <summary>Null = the object has no InventorySystem.</summary>
        public List<ItemStackSaveData> inventory;
        public int? gold;
        public bool? isLocked;
        public bool? isOpen;
    }

    public class DroppedItemSaveData
    {
        public string region;
        public string itemId;
        public SVector3 position;
        public SQuaternion rotation;
    }

    public class SaveData
    {
        public int version;
        public SaveMeta meta;
        public string region;
        // worldFacts / player are left null by default so a file missing them is detected as corrupted.
        public Dictionary<string, bool> worldFacts;
        public PlayerSaveData player;
        public Dictionary<string, ObjectSaveData> objects = new Dictionary<string, ObjectSaveData>();
        /// <summary>Save keys of scene-authored objects removed from the world (e.g. picked-up items).</summary>
        public List<string> consumedObjects = new List<string>();
        public List<DroppedItemSaveData> droppedItems = new List<DroppedItemSaveData>();
    }
}
