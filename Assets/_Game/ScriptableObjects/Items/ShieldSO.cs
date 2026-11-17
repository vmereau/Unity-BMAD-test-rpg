using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Offhand shield (spec-shield-offhand-blocking). Blocking needs one held in hand: a drawn one-handed melee weapon plus
    /// this in <see cref="EquipmentSlot.Offhand"/>. Armor = <see cref="EquipableItemSO.defenseBonus"/> (player Defense, NPC
    /// armor). Stamina and the perfect-block window are player-only (NPCs roll their style's perfectBlockChance).
    /// The visual follows the weapon convention: <c>_Visual</c> root → <c>Drawn</c> (held) / <c>Sheathed</c> (back) → <c>Mesh</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Shield", fileName = "Shield_")]
    public class ShieldSO : EquipableItemSO
    {
        [Header("Block")]
        [Tooltip("Stamina the player spends per hit absorbed by a regular block (also the minimum stamina to raise it).")]
        [Min(0f)] public float blockStaminaCost = 15f;
        [Tooltip("Seconds after raising the block during which a hit counts as a perfect block (player only).")]
        [Min(0f)] public float perfectBlockWindow = 0.25f;

        [Header("Poses")]
        [Tooltip("Pose of the Drawn child in the offhand socket space (the hand opposite the weapon).")]
        public WeaponPose heldPose;
        [Tooltip("Pose of the Sheathed child in BackWeaponSocket space.")]
        public WeaponPose backPose;

        public override bool CanEquip() => true;
    }
}
