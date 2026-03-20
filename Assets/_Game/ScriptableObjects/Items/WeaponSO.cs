namespace Game.Inventory
{
    [UnityEngine.CreateAssetMenu(menuName = "Items/Weapon", fileName = "Weapon_")]
    public class WeaponSO : EquipableItemSO
    {
        public override bool CanEquip() => true;
    }
}
