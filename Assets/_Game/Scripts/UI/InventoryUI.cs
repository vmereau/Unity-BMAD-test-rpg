using Game.Core;
using Game.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    public class InventoryUI : MonoBehaviour
    {
        private const string TAG = "[InventoryUI]";

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _itemSlotPrefab;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _playerTransform;

        private bool _isOpen = false;
        private InputSystem_Actions _input;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.UI.Enable();
        }

        private void OnEnable()
        {
            _input.Player.InventoryToggle.performed += HandleToggle;
            _input.UI.Cancel.performed += HandleClose;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.InventoryToggle.performed -= HandleToggle;
            _input.UI.Cancel.performed -= HandleClose;
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void HandleToggle(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        private void HandleClose(InputAction.CallbackContext ctx)
        {
            if (_isOpen)
                Close();
        }

        private void Open()
        {
            RefreshSlots();
            _panelRoot.SetActive(true);
            _isOpen = true;
            CursorManager.Unlock();
            GameLog.Info(TAG, "Inventory opened");
        }

        private void Close()
        {
            _panelRoot.SetActive(false);
            _isOpen = false;
            CursorManager.Lock();
            GameLog.Info(TAG, "Inventory closed");
        }

        public void DropItem(int slotIndex)
        {
            // Guard before removal — item would be lost if prefab is missing
            if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Drop skipped: slot {slotIndex} out of range");
                return;
            }
            var item = _inventorySystem.Items[slotIndex];
            if (item.worldItemPrefab == null)
            {
                GameLog.Warn(TAG, $"Drop skipped: {item.itemName} has no worldItemPrefab");
                return;
            }

            _inventorySystem.RemoveItem(slotIndex);

            // Item prefabs are active with _item pre-baked; Instantiate triggers Awake immediately
            var dropPos = _playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f;
            var go = Instantiate(item.worldItemPrefab, dropPos, Quaternion.identity);
            go.GetComponent<Rigidbody>().AddForce(_playerTransform.forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
            RefreshSlots();
            GameLog.Info(TAG, $"Dropped: {item.itemName}");
        }

        public void SwapSlots(int a, int b)
        {
            _inventorySystem.MoveItem(a, b);
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            var items = _inventorySystem.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var go = Instantiate(_itemSlotPrefab, _contentRoot);
                var slot = go.GetComponent<ItemSlotUI>();
                slot.Bind(items[i], i);
            }
        }
    }
}
