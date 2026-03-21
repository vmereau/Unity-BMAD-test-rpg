using UnityEngine;

namespace Game.Inventory
{
    public abstract class EquipableItemSO : ItemSO
    {
        [Header("Visuals")]
        public GameObject equipVisualPrefab; // Prefab instantiated on socket when equipped. Null = placeholder primitive.

        [Header("Stat Bonuses (additive, applied while equipped)")]
        public int strengthBonus;
        public int dexterityBonus;
        public int enduranceBonus;
        public int manaBonus;
        public int defenseBonus;

        public abstract bool CanEquip();
    }
}
