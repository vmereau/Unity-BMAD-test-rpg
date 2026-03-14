using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class ItemSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _nameText;

        public int SlotIndex { get; set; }
        public ItemSO Item { get; private set; }

        private GameObject _ghostImage;

        public void Bind(ItemSO item, int index)
        {
            Item = item;
            SlotIndex = index;

            if (item != null)
            {
                if (item.icon != null)
                {
                    _iconImage.sprite = item.icon;
                    _iconImage.color = Color.white;
                }
                else
                {
                    _iconImage.sprite = null;
                    _iconImage.color = Color.gray;
                }
                _nameText.text = item.itemName;
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.color = Color.gray;
                _nameText.text = string.Empty;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            _ghostImage = new GameObject("DragGhost");
            _ghostImage.transform.SetParent(canvas.transform, false);
            _ghostImage.transform.SetAsLastSibling();

            var img = _ghostImage.AddComponent<Image>();
            img.sprite = _iconImage.sprite;
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
            if (_ghostImage != null)
            {
                Destroy(_ghostImage);
                _ghostImage = null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
            if (source == null || source == this) return;
            GetComponentInParent<InventoryUI>().SwapSlots(source.SlotIndex, SlotIndex);
        }
    }
}
