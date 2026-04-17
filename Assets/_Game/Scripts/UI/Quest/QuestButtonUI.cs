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

        private QuestSO _quest;
        private QuestLogUI _parent;
        private UnityAction _selectAction;

        private void Awake()
        {
            _parent = GetComponentInParent<QuestLogUI>(includeInactive: true);
            if (_parent == null)
                GameLog.Warn(TAG, "QuestButtonUI could not find a parent QuestLogUI — button clicks will be ignored.");
        }

        public void Bind(QuestSO quest)
        {
            if (_titleText == null) { GameLog.Warn(TAG, "_titleText is not assigned."); return; }
            if (_parent == null)    { GameLog.Warn(TAG, "Cannot bind quest — no parent QuestLogUI."); return; }

            _quest = quest;
            _titleText.text = quest.title;

            if (_selectAction != null) _button.onClick.RemoveListener(_selectAction);
            _selectAction = () => _parent.SelectQuest(_quest);
            _button.onClick.AddListener(_selectAction);

            SetHover(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHover(true);
        public void OnPointerExit(PointerEventData eventData)  => SetHover(false);

        private void SetHover(bool hovered)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = hovered ? _hoverColor : _normalColor;
        }
    }
}
