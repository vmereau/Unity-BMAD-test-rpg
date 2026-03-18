using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// A consumable potion that restores player health when used from the inventory.
    /// Story 4.10: First concrete UsableItemSO subclass that affects player stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Potion Item", fileName = "Item_")]
    public class PotionItemSO : UsableItemSO
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private float _healAmount = 30f;

        public float HealAmount => _healAmount;

        public override bool OnUse(GameObject user)
        {
            var health = user.GetComponent<PlayerHealth>();
            if (health == null)
            {
                GameLog.Warn(TAG, $"PotionItemSO.OnUse: no PlayerHealth on {user.name}");
                return false;
            }

            if (health.IsDead)
                return false;

            health.Heal(_healAmount);
            return true;
        }
    }
}
