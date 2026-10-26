using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One of the cooking screen's Experiment ingredient slots (<see cref="_slotIndex"/>). Holds a <b>reference</b> to an inventory item (like an action bar slot):
    /// dropping an inventory <see cref="ItemSlotUI"/> sets it; right-click, double-click or dragging it out clears it. Nothing is
    /// moved out of the inventory — <see cref="CookingUI"/> consumes on Cook.
    /// </summary>
    public class CookingIngredientSlotUI : MonoBehaviour,
        IDropHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _countText;
        [Tooltip("Index in CookingUI's experiment slots (0-based).")]
        [SerializeField] private int _slotIndex;

        private CookingUI _owner;
        private Canvas _canvas;
        private GameObject _ghostImage;

        public ItemSO Item { get; private set; }

        private void Awake()
        {
            _owner = GetComponentInParent<CookingUI>();
            var canvas = GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas : null;
        }

        /// <summary>Shows the icon only: a cook uses one unit per slot, so the stack count would mislead.</summary>
        public void Bind(ItemSO item)
        {
            Item = item;
            if (_iconImage != null)
            {
                _iconImage.sprite = item != null ? item.icon : null;
                _iconImage.color = item != null && item.icon == null ? Color.gray : Color.white; // like ItemSlotUI
                _iconImage.enabled = item != null;
            }
            if (_countText != null) _countText.gameObject.SetActive(false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            // action bar → ingredient slot: mark handled, or its OnEndDrag clears the hotkey binding
            var abSource = eventData.pointerDrag.GetComponent<ActionBarSlotUI>();
            if (abSource != null)
            {
                abSource.NotifyDropHandled();
                abSource.RemoveGhostImage();
                if (abSource.Item != null) _owner?.SetIngredient(_slotIndex, abSource.Item);
                return;
            }

            // ingredient slot → ingredient slot: copy here; the source's OnEndDrag (pointer not over itself) clears it → a move
            var slotSource = eventData.pointerDrag.GetComponent<CookingIngredientSlotUI>();
            if (slotSource != null)
            {
                if (slotSource != this && slotSource.Item != null) _owner?.SetIngredient(_slotIndex, slotSource.Item);
                return;
            }

            // inventory → ingredient slot
            var invSource = eventData.pointerDrag.GetComponent<ItemSlotUI>();
            if (invSource == null || invSource.Item == null) return;
            invSource.RemoveGhostImage();
            _owner?.SetIngredient(_slotIndex, invSource.Item);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Item == null) return;
            bool rightClick = eventData.button == PointerEventData.InputButton.Right;
            bool doubleClick = eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2;
            if (rightClick || doubleClick)
                _owner?.ClearIngredient(_slotIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null || _canvas == null)
            {
                eventData.pointerDrag = null;
                return;
            }

            _ghostImage = new GameObject("IngredientDragGhost");
            _ghostImage.transform.SetParent(_canvas.transform, false);
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
            // Dragged out of the slot (anywhere else) → clear the reference.
            GameObject over = eventData.pointerCurrentRaycast.gameObject;
            bool overSelf = over != null && over.transform.IsChildOf(transform);
            if (!overSelf && Item != null)
                _owner?.ClearIngredient(_slotIndex);
        }

        private void OnDisable()
        {
            RemoveGhostImage();
        }

        private void RemoveGhostImage()
        {
            if (_ghostImage != null)
            {
                Destroy(_ghostImage);
                _ghostImage = null;
            }
        }
    }
}
