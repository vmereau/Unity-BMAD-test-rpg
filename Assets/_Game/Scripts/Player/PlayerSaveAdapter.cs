using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.Progression;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Captures / restores every piece of player state for the save system. Lives on the Player prefab root.
    /// Restore writes state through each system's Restore* API — never through gameplay paths (AddItem,
    /// LearnSkill, GiveExperience, Equip) that would grant LP / XP / rewards or re-stack items.
    /// Unassigned references fall back to a component on this GameObject.
    /// </summary>
    public class PlayerSaveAdapter : MonoBehaviour
    {
        private const string TAG = "[Save]";
        private const int ACTION_BAR_SLOTS = 6;

        [SerializeField] private PlayerStats _stats;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private StaminaSystem _stamina;
        [SerializeField] private XPSystem _xp;
        [SerializeField] private LevelSystem _level;
        [SerializeField] private LearningPointSystem _learningPoints;
        [SerializeField] private PlayerSkills _skills;
        [SerializeField] private InventorySystem _inventory;
        [SerializeField] private EquipmentSystem _equipment;
        [SerializeField] private ActionBarSystem _actionBar;
        [SerializeField] private GoldSystem _gold;
        [SerializeField] private PlayerStateManager _stateManager;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private PlayerController _playerController;
        // Lives on a child of the Player rig — falls back to GetComponentInChildren.
        [SerializeField] private EquipmentVisuals _equipmentVisuals;

        private void Awake()
        {
            Fallback(ref _stats);
            Fallback(ref _health);
            Fallback(ref _stamina);
            Fallback(ref _xp);
            Fallback(ref _level);
            Fallback(ref _learningPoints);
            Fallback(ref _skills);
            Fallback(ref _inventory);
            Fallback(ref _equipment);
            Fallback(ref _actionBar);
            Fallback(ref _gold);
            Fallback(ref _stateManager);
            Fallback(ref _characterController);
            Fallback(ref _playerController);
            if (_equipmentVisuals == null) _equipmentVisuals = GetComponentInChildren<EquipmentVisuals>(true);
            if (_equipmentVisuals == null) GameLog.Warn(TAG, "PlayerSaveAdapter: EquipmentVisuals not found — weapon may stay drawn after a load");
        }

        private void Fallback<T>(ref T field) where T : Component
        {
            if (field == null) field = GetComponent<T>();
            if (field == null) GameLog.Warn(TAG, $"PlayerSaveAdapter: {typeof(T).Name} not found — it will not be saved / restored");
        }

        public PlayerSaveData Capture()
        {
            var d = new PlayerSaveData
            {
                position = SVector3.From(transform.position),
                rotation = SQuaternion.From(transform.rotation)
            };

            if (_health != null) d.health = _health.CurrentHealth;
            if (_stats != null)
            {
                d.baseStrength = _stats.GetBaseStat(StatType.Strength);
                d.baseDexterity = _stats.GetBaseStat(StatType.Dexterity);
                d.baseEndurance = _stats.GetBaseStat(StatType.Endurance);
                d.baseIntelligence = _stats.GetBaseStat(StatType.Intelligence);
            }
            if (_xp != null)
            {
                d.xp = _xp.CurrentXP;
                d.totalKills = _xp.TotalKills;
            }
            if (_level != null) d.level = _level.CurrentLevel;
            if (_learningPoints != null) d.learningPoints = _learningPoints.CurrentLP;
            if (_skills != null) d.skills = _skills.GetLearnedSkillIds();
            if (_inventory != null) d.inventory = ItemStackConverter.ToSave(_inventory.Items);
            if (_equipment != null)
            {
                foreach (var pair in _equipment.Equipped)
                {
                    if (pair.Value == null || string.IsNullOrEmpty(pair.Value.itemId)) continue;
                    d.equipped[pair.Key.ToString()] = pair.Value.itemId;
                }
            }
            if (_actionBar != null)
            {
                for (int i = 0; i < ACTION_BAR_SLOTS; i++)
                {
                    var slot = _actionBar.GetSlot(i);
                    if (slot == null || slot.Value.Item == null || string.IsNullOrEmpty(slot.Value.Item.itemId)) continue;
                    d.actionBar.Add(new ActionBarSlotSaveData
                    {
                        slotIndex = i,
                        inventoryIndex = slot.Value.InventoryIndex,
                        itemId = slot.Value.Item.itemId
                    });
                }
            }
            if (_gold != null) d.gold = _gold.Gold;
            return d;
        }

        /// <summary>
        /// Order matters: stats before stamina (max depends on Endurance), XP before level (progress bar),
        /// inventory before action bar (slots re-validate against it), health last so the revive happens
        /// once everything else is in place.
        /// </summary>
        public void Restore(PlayerSaveData d, ItemCatalogSO catalog)
        {
            if (d == null)
            {
                GameLog.Error(TAG, "PlayerSaveAdapter.Restore: no player data");
                return;
            }

            _stats?.RestoreBaseStats(d.baseStrength, d.baseDexterity, d.baseEndurance, d.baseIntelligence);
            _skills?.RestoreSkills(d.skills);
            _xp?.RestoreState(d.xp, d.totalKills);
            _level?.RestoreLevel(d.level);
            _learningPoints?.RestoreLP(d.learningPoints);
            _inventory?.RestoreSlots(ItemStackConverter.FromSave(d.inventory, catalog));
            _equipment?.RestoreEquipped(ResolveEquipped(d.equipped, catalog));
            _actionBar?.RestoreSlots(ResolveActionBar(d.actionBar, catalog));
            _gold?.RestoreGold(d.gold);
            _health?.RestoreHealth(d.health);
            _stamina?.RefillToMax();
            _stateManager?.SetDead(false);
            // Sneak isn't saved — every load starts standing.
            _stateManager?.SetSneaking(false);
            // The drawn / sheathed stance isn't saved — every load starts sheathed.
            if (_stateManager != null && _stateManager.IsInCombat)
            {
                _stateManager.SetInCombat(false);
                _equipmentVisuals?.SetCombatState(false);
            }
            Teleport(d.position.To(), d.rotation.To());
        }

        private static Dictionary<EquipmentSlot, ItemSO> ResolveEquipped(Dictionary<string, string> saved, ItemCatalogSO catalog)
        {
            var result = new Dictionary<EquipmentSlot, ItemSO>();
            if (saved == null || catalog == null) return result;
            foreach (var pair in saved)
            {
                if (!Enum.TryParse(pair.Key, out EquipmentSlot slot))
                {
                    GameLog.Warn(TAG, $"Unknown equipment slot '{pair.Key}' in save — skipped");
                    continue;
                }
                var item = catalog.FindById(pair.Value);
                if (item == null)
                {
                    GameLog.Warn(TAG, $"Unknown itemId '{pair.Value}' equipped in {slot} — skipped");
                    continue;
                }
                result[slot] = item;
            }
            return result;
        }

        private static List<(int slot, int invIndex, ItemSO item)> ResolveActionBar(List<ActionBarSlotSaveData> saved, ItemCatalogSO catalog)
        {
            var result = new List<(int slot, int invIndex, ItemSO item)>();
            if (saved == null || catalog == null) return result;
            foreach (var s in saved)
            {
                if (s == null) continue;
                var item = catalog.FindById(s.itemId);
                if (item == null)
                {
                    GameLog.Warn(TAG, $"Unknown itemId '{s.itemId}' on action bar slot {s.slotIndex + 1} — skipped");
                    continue;
                }
                result.Add((s.slotIndex, s.inventoryIndex, item));
            }
            return result;
        }

        // CharacterController overrides transform writes while enabled — disable around the move.
        private void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _characterController != null && _characterController.enabled;
            if (wasEnabled) _characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            if (wasEnabled) _characterController.enabled = true;
            _playerController?.ResetVerticalVelocity();
        }
    }
}
