using System;
using Game.Core;
using Game.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One row of the Skills tab list: placeholder icon + name. Unlearned skills are grayed but stay clickable.
    /// Background shows normal / hover / selected (selected wins over hover).
    /// </summary>
    public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string TAG = "[SkillSlotUI]";

        [SerializeField] private Button _button;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color _hoverColor = new(0.30f, 0.30f, 0.30f, 0.9f);
        [SerializeField] private Color _selectedColor = new(0.35f, 0.30f, 0.18f, 0.95f);
        [SerializeField] private Color _learnedTextColor = new(0.88f, 0.88f, 0.88f);
        [SerializeField] private Color _unlearnedTextColor = new(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color _iconPlaceholderColor = new(0.5f, 0.5f, 0.5f);
        [SerializeField, Range(0f, 1f)] private float _unlearnedIconAlpha = 0.4f;

        public SkillSO Skill { get; private set; }

        private Action<SkillSlotUI> _onClicked;
        private UnityAction _clickAction;
        private bool _hovered;
        private bool _selected;
        private bool _warnedMissingRefs;

        public void Bind(SkillSO skill, Action<SkillSlotUI> onClicked)
        {
            Skill = skill;
            _onClicked = onClicked;

            if (_nameText != null) _nameText.text = skill != null ? skill.displayName : "";
            else WarnMissingRefs();

            if (_button == null) { WarnMissingRefs(); return; }
            if (_clickAction != null) _button.onClick.RemoveListener(_clickAction);
            _clickAction = HandleClick;
            _button.onClick.AddListener(_clickAction);

            RepaintBackground();
        }

        public void SetLearned(bool learned)
        {
            if (_nameText != null) _nameText.color = learned ? _learnedTextColor : _unlearnedTextColor;
            if (_icon != null)
            {
                Color c = _iconPlaceholderColor;
                c.a = learned ? 1f : _unlearnedIconAlpha;
                _icon.sprite = null; // placeholder until SkillSO gets an icon
                _icon.color = c;
            }
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RepaintBackground();
        }

        // Pointer exit isn't sent when the row is deactivated under the cursor (tab closed / switched) — clear hover here.
        private void OnDisable()
        {
            _hovered = false;
            if (_backgroundImage != null) RepaintBackground();
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; RepaintBackground(); }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; RepaintBackground(); }

        private void HandleClick() => _onClicked?.Invoke(this);

        private void RepaintBackground()
        {
            if (_backgroundImage == null) { WarnMissingRefs(); return; }
            _backgroundImage.color = _selected ? _selectedColor : _hovered ? _hoverColor : _normalColor;
        }

        private void WarnMissingRefs()
        {
            if (_warnedMissingRefs) return;
            _warnedMissingRefs = true;
            GameLog.Warn(TAG, $"{name}: _button, _backgroundImage or _nameText is not assigned");
        }
    }
}
