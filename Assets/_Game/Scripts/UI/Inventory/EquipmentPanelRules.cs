using System.Collections.Generic;
using Game.Inventory;

namespace Game.UI
{
    /// <summary>Frame state of an equipment slot well (DESIGN.md → Components → Equipment slot).</summary>
    public enum EquipmentSlotFrame { Normal, Hover, Selected, Valid, ValidHover }

    /// <summary>
    /// Pure display rules of the inventory paper doll (spec-inventory-equipment-paper-doll, EditMode-tested). It adds no equip
    /// rules: valid targets come from <see cref="EquipmentRules"/>, the same rules <see cref="EquipmentSystem"/> applies.
    /// </summary>
    public static class EquipmentPanelRules
    {
        /// <summary>A grid item can be dropped / equipped into <paramref name="slot"/> (valid-target highlight).</summary>
        public static bool IsValidTarget(ItemSO item, EquipmentSlot slot) =>
            item is EquipableItemSO && EquipmentRules.FitsSlot(item, slot);

        /// <summary>The item worn in <paramref name="from"/> can be dragged onto <paramref name="to"/> (Ring1 ↔ Ring2).</summary>
        public static bool IsValidSwapTarget(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, EquipmentSlot from, EquipmentSlot to) =>
            EquipmentRules.CanSwap(equipped, from, to);

        /// <summary>The Offhand slot shows the weapon faded: it is empty and the weapon takes both hands.</summary>
        public static bool ShowsOffhandGhost(ItemSO weapon, ItemSO offhand) =>
            offhand == null && weapon is WeaponSO { OccupiesOffhand: true };

        /// <summary>
        /// Frame precedence: a valid target under the cursor during a drag, then valid, then selected, then hover. Valid wins
        /// over selected so a drag started while a slot is selected still shows where it can land.
        /// </summary>
        public static EquipmentSlotFrame ResolveFrame(bool selected, bool valid, bool hovered, bool dragging)
        {
            if (valid) return hovered && dragging ? EquipmentSlotFrame.ValidHover : EquipmentSlotFrame.Valid;
            if (selected) return EquipmentSlotFrame.Selected;
            return hovered ? EquipmentSlotFrame.Hover : EquipmentSlotFrame.Normal;
        }

        /// <summary>Caption under the slot (EXPERIENCE.md → Voice and Tone).</summary>
        public static string Caption(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => "Weapon",
            EquipmentSlot.Offhand => "Offhand",
            EquipmentSlot.Helmet => "Helmet",
            EquipmentSlot.Armor => "Armor",
            EquipmentSlot.Ring1 => "Ring",
            EquipmentSlot.Ring2 => "Ring",
            EquipmentSlot.Necklace => "Necklace",
            _ => slot.ToString()
        };

        /// <summary>Label inside an empty well, uppercase.</summary>
        public static string EmptyLabel(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Necklace => "NECK",
            EquipmentSlot.Ring1 => "RING",
            EquipmentSlot.Ring2 => "RING",
            _ => Caption(slot).ToUpperInvariant()
        };

        /// <summary>
        /// Slot an action on <paramref name="clicked"/> applies to: the faded Offhand ghost stands for the Weapon slot.
        /// </summary>
        public static EquipmentSlot ActionSlot(EquipmentSlot clicked, bool offhandGhostShown) =>
            clicked == EquipmentSlot.Offhand && offhandGhostShown ? EquipmentSlot.Weapon : clicked;
    }
}
