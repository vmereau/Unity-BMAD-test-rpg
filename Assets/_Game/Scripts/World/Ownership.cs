using Game.Core;
using Game.Inventory;
using Game.NPC;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Marks an interactable (world item, container, door) as owned by an NPC. Taking / opening it while the owner
    /// lives is a theft: <see cref="InteractionSystem"/> draws the prompt and outline in red and raises
    /// <c>OnTheftCommitted</c> before the interaction. Free again once the owner's <see cref="NPCEntity.KilledFact"/>
    /// is set in <see cref="WorldStateManager"/>. Closing an owned open door is always free, and so is a locked door whose
    /// inner latch is usable (<see cref="DoorInteractable.DefersToLatch"/>): leaving from inside isn't a theft.
    /// </summary>
    public class Ownership : MonoBehaviour
    {
        private const string TAG = "[Ownership]";

        [Tooltip("NPC that owns this object. Taking / opening it while the owner lives is a theft. Null = not owned.")]
        [SerializeField] private NPCEntity _owner;

        private DoorInteractable _door;

        public NPCEntity Owner => _owner;
        public TheftKind Kind { get; private set; }

        /// <summary>Interacting with this object now is a theft (owner set and alive; open doors and usable latches excluded).</summary>
        public bool IsIllegal =>
            IsIllegalInteraction(_owner != null, IsOwnerKilled(), Kind == TheftKind.Door, _door != null && _door.IsOpen,
                _door != null && _door.DefersToLatch);

        private void Awake()
        {
            // Harvesting an owned plant takes an item → item-theft scolds.
            if (TryGetComponent(out ItemPickup _) || TryGetComponent(out Harvestable _)) Kind = TheftKind.Item;
            else if (TryGetComponent(out ContainerInteractable _)) Kind = TheftKind.Container;
            else if (TryGetComponent(out _door)) Kind = TheftKind.Door;
            else Kind = TheftKind.Other;

            if (_owner != null && _owner.KilledFact == null)
                GameLog.Warn(TAG, $"{gameObject.name}: owner '{_owner.name}' has no KilledFact — stays owned even after the owner dies");
        }

        private bool IsOwnerKilled() =>
            _owner != null && _owner.KilledFact != null && WorldStateManager.Instance != null &&
            WorldStateManager.Instance.IsKilled(_owner.KilledFact);

        /// <summary>Pure legality rule: owned, owner alive, not closing an open door, and not a door its inner latch opens.</summary>
        public static bool IsIllegalInteraction(bool hasOwner, bool ownerKilled, bool isDoor, bool doorOpen, bool latchUsable = false) =>
            hasOwner && !ownerKilled && !(isDoor && (doorOpen || latchUsable));

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (GetComponent<IInteractable>() == null)
                GameLog.Warn(TAG, $"{gameObject.name}: Ownership has no sibling IInteractable — it will never be checked");
        }
#endif
    }
}
