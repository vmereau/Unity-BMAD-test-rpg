using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.World
{
    /// <summary>Records taking an authored item from this container, never opening or restoring it.</summary>
    [RequireComponent(typeof(InventorySystem))]
    public class ContainerTakeFact : MonoBehaviour
    {
        private const string TAG = "[ContainerTakeFact]";
        [SerializeField] private GameEventSO_ContainerItemTaken _onItemTaken;
        [SerializeField] private WorldFact _fact;
        [SerializeField] private ItemSO[] _items;
        private InventorySystem _inventory;

        private void Awake()
        {
            _inventory = GetComponent<InventorySystem>();
            if (_onItemTaken != null && _fact != null && _items != null && _items.Length > 0) return;
            GameLog.Error(TAG, $"{name}: needs a take event, fact and item allowlist — disabled.");
            enabled = false;
        }

        private void OnEnable() => _onItemTaken?.AddListener(HandleItemTaken);
        private void OnDisable() => _onItemTaken?.RemoveListener(HandleItemTaken);

        private void HandleItemTaken(ContainerItemTakenData data)
        {
            var state = WorldStateManager.Instance;
            if (_fact == null || state == null || state.GetFact(_fact) ||
                !Matches(_inventory, _items, data)) return;
            state.SetWorldEvent(_fact, true);
        }

        public static bool Matches(InventorySystem source, ItemSO[] allowed, ContainerItemTakenData data)
        {
            if (source == null || source != data.sourceInventory || data.item == null || allowed == null) return false;
            for (int i = 0; i < allowed.Length; i++)
                if (allowed[i] == data.item) return true;
            return false;
        }
    }
}
