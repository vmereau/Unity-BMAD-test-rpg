using System.Collections.Generic;

namespace Game.Inventory
{
    /// <summary>
    /// Pure slot rules of <see cref="EquipmentSystem"/> (EditMode-tested): which slot an item goes to, which equipped slot
    /// it pushes out, and whether a restored set is legal. Offhand rules: spec-shield-offhand-blocking.
    /// </summary>
    public static class EquipmentRules
    {
        /// <summary>
        /// Slot <paramref name="item"/> is equipped to, given what is already worn: weapons → Weapon, shields and torches → Offhand,
        /// rings → the free ring slot (Ring1 when both are taken), other armor → its slot. False for anything else.
        /// </summary>
        public static bool TryGetTargetSlot(ItemSO item, IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, out EquipmentSlot slot)
        {
            slot = default;
            switch (item)
            {
                case WeaponSO:
                    slot = EquipmentSlot.Weapon;
                    return true;
                case ShieldSO:
                case TorchSO:
                    slot = EquipmentSlot.Offhand;
                    return true;
                case ArmorSO armor when armor.slot is EquipmentSlot.Weapon or EquipmentSlot.Offhand:
                    return false; // body gear can't take a hand slot
                case ArmorSO { slot: EquipmentSlot.Ring1 }:
                    slot = !IsOccupied(equipped, EquipmentSlot.Ring1) ? EquipmentSlot.Ring1
                        : !IsOccupied(equipped, EquipmentSlot.Ring2) ? EquipmentSlot.Ring2
                        : EquipmentSlot.Ring1; // both taken: Ring1 is bumped back to the inventory
                    return true;
                case ArmorSO armor:
                    slot = armor.slot;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The other slot equipping <paramref name="item"/> frees up: a two-handed weapon / bow pushes out the offhand item,
        /// a shield pushes out a two-handed weapon / bow. A torch conflicts with neither: it is stowed (hidden) while one is drawn
        /// (spec-torch-offhand-item). False when nothing conflicts.
        /// </summary>
        public static bool TryGetConflictingSlot(ItemSO item, IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, out EquipmentSlot slot)
        {
            slot = default;
            if (item is WeaponSO { OccupiesOffhand: true } && IsOccupied(equipped, EquipmentSlot.Offhand)
                && !CoexistsWithTwoHands(Get(equipped, EquipmentSlot.Offhand)))
            {
                slot = EquipmentSlot.Offhand;
                return true;
            }
            if (item is ShieldSO && Get(equipped, EquipmentSlot.Weapon) is WeaponSO { OccupiesOffhand: true })
            {
                slot = EquipmentSlot.Weapon;
                return true;
            }
            return false;
        }

        /// <summary>Restore check: <paramref name="item"/> belongs in <paramref name="slot"/> (mirrors <see cref="TryGetTargetSlot"/>).</summary>
        public static bool FitsSlot(ItemSO item, EquipmentSlot slot) => item switch
        {
            WeaponSO => slot == EquipmentSlot.Weapon,
            ShieldSO => slot == EquipmentSlot.Offhand,
            TorchSO => slot == EquipmentSlot.Offhand,
            ArmorSO armor when armor.slot is EquipmentSlot.Weapon or EquipmentSlot.Offhand => false,
            ArmorSO armor when armor.slot == EquipmentSlot.Ring1 => slot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2,
            ArmorSO armor => slot == armor.slot,
            _ => false
        };

        /// <summary>An offhand item next to a weapon that needs both hands (a bad save): the offhand item must go.</summary>
        public static bool HasOffhandConflict(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped) =>
            IsOccupied(equipped, EquipmentSlot.Offhand) && !CoexistsWithTwoHands(Get(equipped, EquipmentSlot.Offhand))
            && Get(equipped, EquipmentSlot.Weapon) is WeaponSO { OccupiesOffhand: true };

        /// <summary>
        /// Two equipped slots can trade contents (inventory paper doll, Ring1 ↔ Ring2 drag): different slots, at least one
        /// occupied, and each item fits the other slot (an empty side always fits).
        /// </summary>
        public static bool CanSwap(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, EquipmentSlot a, EquipmentSlot b)
        {
            if (a == b) return false;
            ItemSO itemA = Get(equipped, a);
            ItemSO itemB = Get(equipped, b);
            if (itemA == null && itemB == null) return false;
            return (itemA == null || FitsSlot(itemA, b)) && (itemB == null || FitsSlot(itemB, a));
        }

        /// <summary>An offhand item that stays equipped next to a weapon needing both hands (stowed while it is drawn): torches.</summary>
        public static bool CoexistsWithTwoHands(ItemSO offhand) => offhand is TorchSO;

        private static bool IsOccupied(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, EquipmentSlot slot) => Get(equipped, slot) != null;

        private static ItemSO Get(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, EquipmentSlot slot) =>
            equipped != null && equipped.TryGetValue(slot, out ItemSO item) ? item : null;
    }
}
