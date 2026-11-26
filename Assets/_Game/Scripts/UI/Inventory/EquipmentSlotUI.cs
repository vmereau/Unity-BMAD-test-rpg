using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One paper-doll equipment slot (spec-inventory-equipment-paper-doll): a <c>UI_SlotWell</c> with the item icon, the
    /// empty-well label, a caption, an outer ring and a dashed leader line. Paints the state <see cref="EquipmentUI"/> gives it
    /// and forwards pointer / drag / drop events to it; it holds no equip logic.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private const float DoubleClickThreshold = 0.3f;

        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _emptyLabel;
        [SerializeField] private TMP_Text _caption;
        [SerializeField] private Image _outerRing;
        [SerializeField] private Image _leaderLine;

        [Header("Frame tints (multiply UI_SlotWell)")]
        [SerializeField] private Color _normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.95f, 0.85f, 1f);
        [SerializeField] private Color _selectedColor = new Color(1f, 0.82f, 0.5f, 1f);
        [SerializeField] private Color _validColor = new Color(0.72f, 0.9f, 0.55f, 1f);
        [SerializeField] private Color _validHoverColor = new Color(0.9f, 1f, 0.72f, 1f);

        [Header("Outer ring")]
        [SerializeField] private Color _selectedRingColor = new Color32(0x4A, 0x3C, 0x22, 0xFF);
        [SerializeField] private Color _validRingColor = new Color32(0x3A, 0x42, 0x30, 0xFF);

        [Header("Leader line")]
        [SerializeField] private Color _lineColor = new Color32(0x6B, 0x54, 0x40, 0xFF);
        [SerializeField] private Color _lineSelectedColor = new Color32(0x8C, 0x73, 0x43, 0xFF);
        [SerializeField] private Color _lineValidColor = new Color32(0x6F, 0x7D, 0x50, 0xFF);
        [SerializeField] private float _lineWidth = 2f;
        [SerializeField] private float _lineHighlightWidth = 3f;

        [Header("Caption")]
        [SerializeField] private Color _captionColor = new Color32(0x80, 0x7C, 0x74, 0xFF);
        [SerializeField] private Color _captionHighlightColor = new Color32(0xD8, 0xCC, 0xB0, 0xFF);

        [Header("Offhand ghost")]
        [SerializeField] private Color _ghostIconColor = new Color(0.62f, 0.6f, 0.57f, 0.28f);

        public EquipmentSlot Slot { get; private set; }
        /// <summary>Item painted in the well: the equipped item, or the two-handed weapon when <see cref="IsGhost"/>.</summary>
        public ItemSO ShownItem { get; private set; }
        /// <summary>The Offhand slot shows the equipped weapon faded (it takes both hands).</summary>
        public bool IsGhost { get; private set; }

        private EquipmentUI _equipmentUI;
        private Canvas _rootCanvas;
        private GameObject _dragGhost;
        private float _lastClickTime = -1f;
        private bool _hovered, _selected, _valid, _dragging;

        public void Initialize(EquipmentSlot slot, EquipmentUI equipmentUI)
        {
            Slot = slot;
            _equipmentUI = equipmentUI;
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (_caption != null) _caption.text = EquipmentPanelRules.Caption(slot);
            if (_emptyLabel != null) _emptyLabel.text = EquipmentPanelRules.EmptyLabel(slot);
            Bind(null, false);
        }

        private void OnDisable()
        {
            // OnEndDrag never reaches a slot disabled mid-drag — drop the ghost and the hover here.
            RemoveDragGhost();
            _hovered = false;
            ApplyState();
        }

        public void Bind(ItemSO item, bool ghost)
        {
            ShownItem = item;
            IsGhost = item != null && ghost;
            if (_iconImage != null)
            {
                _iconImage.enabled = item != null;
                _iconImage.sprite = item != null ? item.icon : null;
                _iconImage.color = item == null ? Color.clear
                    : IsGhost ? _ghostIconColor
                    : item.icon != null ? Color.white : Color.gray;
            }
            if (_emptyLabel != null) _emptyLabel.gameObject.SetActive(item == null);
        }

        public void SetState(bool selected, bool valid, bool dragging)
        {
            _selected = selected;
            _valid = valid;
            _dragging = dragging;
            ApplyState();
        }

        private void ApplyState()
        {
            var frame = EquipmentPanelRules.ResolveFrame(_selected, _valid, _hovered, _dragging);
            if (_backgroundImage != null)
            {
                _backgroundImage.color = frame switch
                {
                    EquipmentSlotFrame.Hover => _hoverColor,
                    EquipmentSlotFrame.Selected => _selectedColor,
                    EquipmentSlotFrame.Valid => _validColor,
                    EquipmentSlotFrame.ValidHover => _validHoverColor,
                    _ => _normalColor
                };
            }

            bool isValid = frame is EquipmentSlotFrame.Valid or EquipmentSlotFrame.ValidHover;
            bool isSelected = frame == EquipmentSlotFrame.Selected;
            bool highlighted = isValid || isSelected;

            if (_outerRing != null)
            {
                _outerRing.enabled = highlighted;
                _outerRing.color = isValid ? _validRingColor : _selectedRingColor;
            }
            if (_leaderLine != null)
            {
                _leaderLine.color = isValid ? _lineValidColor : isSelected ? _lineSelectedColor : _lineColor;
                var rt = _leaderLine.rectTransform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, highlighted ? _lineHighlightWidth : _lineWidth);
            }
            if (_caption != null) _caption.color = highlighted ? _captionHighlightColor : _captionColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_equipmentUI == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _equipmentUI.OnSlotRightClicked(this, eventData.position);
                return;
            }
            if (eventData.button != PointerEventData.InputButton.Left) return;

            bool isDouble = Time.unscaledTime - _lastClickTime <= DoubleClickThreshold;
            _lastClickTime = isDouble ? -1f : Time.unscaledTime; // a third click starts a new pair
            if (isDouble) _equipmentUI.OnSlotDoubleClicked(this);
            else _equipmentUI.OnSlotClicked(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_equipmentUI == null || ShownItem == null || _rootCanvas == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return; // a jittery right-click must stay a context menu

            _dragGhost = new GameObject("DragGhost");
            _dragGhost.transform.SetParent(_rootCanvas.transform, false);
            _dragGhost.transform.SetAsLastSibling();
            var img = _dragGhost.AddComponent<Image>();
            img.sprite = ShownItem.icon;
            img.color = new Color(1f, 1f, 1f, 0.7f);
            img.raycastTarget = false; // must not block the drop target
            var rt = _dragGhost.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.position = eventData.position;

            _equipmentUI.OnSlotDragBegin(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragGhost != null) _dragGhost.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            bool wasDragging = _dragGhost != null;
            RemoveDragGhost();
            if (wasDragging) _equipmentUI?.OnSlotDragEnd(this, eventData.position);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_equipmentUI == null || eventData.pointerDrag == null) return;

            var gridSource = eventData.pointerDrag.GetComponent<ItemSlotUI>();
            if (gridSource != null)
            {
                gridSource.RemoveGhostImage();
                _equipmentUI.OnGridItemDropped(gridSource, this);
                return;
            }

            // Action bar → paper doll isn't a feature: claim the drop so the bar keeps its binding (its OnEndDrag clears
            // an unclaimed slot as if it had been dragged back to the inventory).
            var barSource = eventData.pointerDrag.GetComponent<ActionBarSlotUI>();
            if (barSource != null)
            {
                barSource.NotifyDropHandled();
                barSource.RemoveGhostImage();
                return;
            }

            var slotSource = eventData.pointerDrag.GetComponent<EquipmentSlotUI>();
            if (slotSource != null && slotSource != this)
                _equipmentUI.OnSlotDroppedOnSlot(slotSource, this);
        }

        private void RemoveDragGhost()
        {
            if (_dragGhost == null) return;
            Destroy(_dragGhost);
            _dragGhost = null;
        }
    }
}
