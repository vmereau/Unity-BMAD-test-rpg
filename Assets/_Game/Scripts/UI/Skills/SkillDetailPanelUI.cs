using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Display-only detail panel of the Skills tab: header (placeholder icon, name, status), description, effect and
    /// requirement rows. All display decisions come from <see cref="SkillDetailFormatter"/>; this class only paints.
    /// </summary>
    public class SkillDetailPanelUI : MonoBehaviour
    {
        private const string TAG = "[SkillDetailPanelUI]";

        [Header("Header")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _statusText;

        [Header("Description")]
        [SerializeField] private GameObject _descriptionSection;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Effect")]
        [SerializeField] private GameObject _effectSection;
        [SerializeField] private TMP_Text _effectText;

        [Header("Details")]
        [SerializeField] private GameObject _detailsSection;
        [SerializeField] private Transform _rowsRoot;
        [SerializeField] private ItemStatRowUI _rowPrefab;

        [Header("Colors")]
        [SerializeField] private Color _positiveColor = new(0.49f, 0.80f, 0.42f);
        [SerializeField] private Color _negativeColor = new(0.88f, 0.42f, 0.42f);
        [SerializeField] private Color _neutralColor = new(0.88f, 0.88f, 0.88f);
        [SerializeField] private Color _learnedStatusColor = new(0.49f, 0.80f, 0.42f);
        [SerializeField] private Color _notLearnedStatusColor = new(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color _iconPlaceholderColor = new(0.5f, 0.5f, 0.5f);

        private readonly List<ItemStatLine> _lines = new();
        private readonly List<ItemStatRowUI> _rows = new();
        private bool _warnedMissingRowRefs;

        public void Show(SkillSO skill, bool learned, Func<StatType, int> getStat, Func<string, bool> hasSkill)
        {
            if (skill == null)
            {
                GameLog.Warn(TAG, "Show: skill is null");
                Hide();
                return;
            }

            PaintHeader(skill, learned);
            PaintText(_descriptionSection, _descriptionText, SkillDetailFormatter.GetDescription(skill));
            PaintText(_effectSection, _effectText, SkillDetailFormatter.GetEffect(skill));
            PaintRows(skill, learned, getStat, hasSkill);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void PaintHeader(SkillSO skill, bool learned)
        {
            if (_icon != null)
            {
                _icon.sprite = null; // placeholder until SkillSO gets an icon
                _icon.color = _iconPlaceholderColor;
            }
            if (_nameText != null) _nameText.text = skill.displayName;
            if (_statusText != null)
            {
                _statusText.text = SkillDetailFormatter.GetStatus(learned);
                _statusText.color = learned ? _learnedStatusColor : _notLearnedStatusColor;
            }
        }

        private static void PaintText(GameObject section, TMP_Text label, string text)
        {
            bool hasText = text != null;
            if (section != null) section.SetActive(hasText);
            if (hasText && label != null) label.text = text;
        }

        private void PaintRows(SkillSO skill, bool learned, Func<StatType, int> getStat, Func<string, bool> hasSkill)
        {
            if (_rowPrefab == null || _rowsRoot == null)
            {
                if (!_warnedMissingRowRefs)
                {
                    GameLog.Warn(TAG, "PaintRows: _rowPrefab or _rowsRoot is not assigned; details hidden");
                    _warnedMissingRowRefs = true;
                }
                if (_detailsSection != null) _detailsSection.SetActive(false);
                return;
            }

            SkillDetailFormatter.BuildDetailLines(skill, learned, getStat, hasSkill, _lines);

            while (_rows.Count < _lines.Count)
                _rows.Add(Instantiate(_rowPrefab, _rowsRoot));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _lines.Count;
                _rows[i].gameObject.SetActive(used);
                if (used) _rows[i].Set(_lines[i].Label, _lines[i].Value, ColorFor(_lines[i].Polarity));
            }

            if (_detailsSection != null) _detailsSection.SetActive(_lines.Count > 0);
        }

        private Color ColorFor(StatPolarity polarity) => polarity switch
        {
            StatPolarity.Positive => _positiveColor,
            StatPolarity.Negative => _negativeColor,
            _ => _neutralColor
        };
    }
}
