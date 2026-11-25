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
        public int intelligenceBonus;
        public int defenseBonus;

        [Header("Off-hand")]
        [Tooltip("Left-arm pose held in idle and locomotion while this item is in the Offhand slot (LeftArm layer; attacks, " +
                 "UpperBody actions and full-body states play the clips' own arm). None = always the clips' arm (shields).")]
        public OffhandArmPose offhandArmPose = OffhandArmPose.None;

        public abstract bool CanEquip();
    }
}
