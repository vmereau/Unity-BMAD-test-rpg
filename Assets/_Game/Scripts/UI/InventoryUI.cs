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
