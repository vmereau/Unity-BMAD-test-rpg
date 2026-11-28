using Game.Inventory;
using Game.Progression;
using Game.World;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.World
{
    /// <summary>
    /// The locks of the Area 1 smuggling route's doors (spec-smuggling-route-locks-and-keys), set by the generated
    /// prefabs' builders so a rebuild keeps them: each door is locked, picked at Lockpicking 1, opened by its key.
    /// </summary>
    internal static class RouteDoorLocks
    {
        internal const string SKILL = "Assets/_Game/Data/Skills/Lockpicking/Skill_Begginer_Lockpicking.asset";
        internal const string KEY_WAREHOUSE_CELLAR = "Assets/_Game/Data/Items/Quest/Key_WarehouseCellar.asset";
        internal const string KEY_GANG_TUNNEL = "Assets/_Game/Data/Items/Quest/Key_GangTunnel.asset";
        internal const string KEY_GANG_BASEMENT = "Assets/_Game/Data/Items/Quest/Key_GangBasement.asset";

        /// <summary>Loads the skill and a key; adds what is missing to <paramref name="absent"/>.</summary>
        internal static (SkillSO skill, KeyItemSO key) Load(string keyPath, System.Collections.Generic.List<string> absent)
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(SKILL);
            var key = AssetDatabase.LoadAssetAtPath<KeyItemSO>(keyPath);
            if (skill == null && !absent.Contains(SKILL)) absent.Add(SKILL);
            if (key == null) absent.Add(keyPath + " (a KeyItemSO)");
            return (skill, key);
        }

        /// <summary>Locks the door's <see cref="Lockable"/>: the skill to pick it and the key that opens it.</summary>
        internal static void Lock(GameObject door, SkillSO skill, ItemSO key)
        {
            var lockSo = new SerializedObject(door.GetComponent<Lockable>());
            lockSo.FindProperty("_isLocked").boolValue = true;
            lockSo.FindProperty("_requiredSkill").objectReferenceValue = skill;
            lockSo.FindProperty("_keyItem").objectReferenceValue = key;
            lockSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
