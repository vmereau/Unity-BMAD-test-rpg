using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class QuestButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string TAG = "[QuestButtonUI]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _button;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color _hoverColor  = new Color(0.30f, 0.30f, 0.30f, 0.9f);
        [SerializeField] private Color _selectedColor = new Color(0.35f, 0.30f, 0.18f, 0.95f);

        private QuestSO _quest;
        private QuestLogUI _parent;
        private UnityAction _selectAction;
        private bool _hovered;
        private bool _selected;

        public QuestSO Quest => _quest;

        private void Awake()
        {
            _parent = GetComponentInParent<QuestLogUI>(includeInactive: true);
            if (_parent == null)
                GameLog.Warn(TAG, "QuestButtonUI could not find a parent QuestLogUI — button clicks will be ignored.");
        }

        /// <summary>Returns false when the row could not be bound (missing references) — the caller discards it.</summary>
        public bool Bind(QuestSO quest)
        {
            if (_titleText == null) { GameLog.Warn(TAG, "_titleText is not assigned."); return false; }
            if (_button == null)    { GameLog.Warn(TAG, "_button is not assigned."); return false; }
            if (_parent == null)    { GameLog.Warn(TAG, "Cannot bind quest — no parent QuestLogUI."); return false; }

            _quest = quest;
            _titleText.text = quest.title;

            if (_selectAction != null) _button.onClick.RemoveListener(_selectAction);
            _selectAction = () => _parent.SelectQuest(_quest);
            _button.onClick.AddListener(_selectAction);

            _hovered  = false;
            _selected = false;
            UpdateColor();
            return true;
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            UpdateColor();
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true;  UpdateColor(); }
        public void OnPointerExit(PointerEventData eventData)  { _hovered = false; UpdateColor(); }

        private void UpdateColor()
        {
            if (_backgroundImage == null) return;
            _backgroundImage.color = _selected ? _selectedColor : _hovered ? _hoverColor : _normalColor;
        }
    }
}
