using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class ActionBarSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private TMP_Text _stackCountText;
        [SerializeField] private TMP_Text _keyLabelText;
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        [SerializeField] private Color _hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);

        public int SlotIndex { get; private set; }
        public ItemSO Item { get; private set; }
        public int InventoryIndex { get; private set; } = -1;

        private ActionBarUI _actionBarUI;
        private Canvas _canvas;
        private GameObject _ghostImage;
        private bool _dropHandled;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void Initialize(int slotIndex, ActionBarUI actionBarUI)
        {
            SlotIndex = slotIndex;
            _actionBarUI = actionBarUI;
            if (_keyLabelText != null)
                _keyLabelText.text = (slotIndex + 1).ToString();
        }

        public void Bind(int slotIndex, ItemSO item, int inventoryIndex, int stackCount)
        {
            SlotIndex = slotIndex;
            Item = item;
            InventoryIndex = inventoryIndex;

            if (_iconImage != null)
            {
                _iconImage.sprite = item.icon;
                _iconImage.color = item.icon != null ? Color.white : Color.gray;
            }

            if (_stackCountText != null)
            {
                _stackCountText.text = stackCount.ToString();
                _stackCountText.gameObject.SetActive(stackCount > 1);
            }
        }

        public void BindEmpty(int slotIndex)
        {
            SlotIndex = slotIndex;
            Item = null;
            InventoryIndex = -1;

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.color = Color.clear;
            }

            if (_stackCountText != null)
                _stackCountText.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = _hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = _normalColor;
        }

        public void NotifyDropHandled()
        {
            _dropHandled = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            _dropHandled = false;

            var rootCanvas = _canvas != null ? _canvas : GetComponentInParent<Canvas>();
            _ghostImage = new GameObject("ActionBarDragGhost");
            _ghostImage.transform.SetParent(rootCanvas.transform, false);
            _ghostImage.transform.SetAsLastSibling();

            var img = _ghostImage.AddComponent<Image>();
            img.sprite = _iconImage != null ? _iconImage.sprite : null;
            img.color = new Color(1f, 1f, 1f, 0.7f);
            img.raycastTarget = false;

            var rt = _ghostImage.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostImage != null)
                _ghostImage.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            RemoveGhostImage();
            if (!_dropHandled && Item != null)
                _actionBarUI.OnActionBarSlotClearedByDragToInventory(SlotIndex);
        }

        public void OnDrop(PointerEventData eventData)
        {
            // inventory → action bar
            var invSource = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
            if (invSource != null)
            {
                _actionBarUI.OnInventorySlotDroppedOnActionBar(invSource.SlotIndex, invSource.Item, SlotIndex);
                return;
            }

            // action bar → action bar (swap)
            var abSource = eventData.pointerDrag?.GetComponent<ActionBarSlotUI>();
            if (abSource != null && abSource != this)
            {
                abSource.NotifyDropHandled();
                _actionBarUI.OnActionBarSlotSwap(abSource.SlotIndex, SlotIndex);
            }
        }

        public void RemoveGhostImage()
        {
            if (_ghostImage != null)
            {
                Destroy(_ghostImage);
                _ghostImage = null;
            }
        }
    }
}
