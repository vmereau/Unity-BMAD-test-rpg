using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Off-hand torch (spec-torch-offhand-item): a light source with no stats, never used up. Held lit in the left hand
    /// whenever it is equipped, except while a weapon that needs both hands (two-hander, bow) is drawn — then it is stowed,
    /// hidden for now (<see cref="EquipmentVisuals.ShouldHoldTorch"/>). The visual follows the shield convention:
    /// <c>_Visual</c> root → <c>Drawn</c> (held: mesh, flame, light) / <c>Sheathed</c> (hip: mesh only, unused while hidden).
    /// Set <see cref="EquipableItemSO.offhandArmPose"/> to <see cref="OffhandArmPose.TorchHold"/> on the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Torch", fileName = "Item_Torch")]
    public class TorchSO : EquipableItemSO
    {
        [Header("Poses")]
        [Tooltip("Pose of the Drawn child in the left-hand socket space (the torch gripped in the fist).")]
        public WeaponPose heldPose;
        [Tooltip("Pose of the Sheathed child in the hip socket space (stowed while a two-hander or bow is drawn).")]
        public WeaponPose stowPose;

        public override bool CanEquip() => true;
    }
}
