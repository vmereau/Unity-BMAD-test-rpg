using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Start-menu origin pick (MainMenu scene, inside <c>Window</c>): one button per <see cref="OriginCatalogSO"/>
    /// origin, the origin's artwork (hidden when it has none), a detail text (description, bonuses, skill, belongings
    /// "sent ahead with the Company baggage cart"), and Begin. Driven by <see cref="MainMenuUI"/>: <see cref="Show"/> / <see cref="Hide"/>.
    /// </summary>
    public class OriginPickUI : MonoBehaviour
    {
        private const string TAG = "[MainMenu]";
        public const string BELONGINGS_HEADER = "Sent ahead with the Company baggage cart:";
        public const string NO_BELONGINGS_TEXT = "You arrive with nothing.";

        [SerializeField] private OriginCatalogSO _catalog;
        [SerializeField] private Transform _originButtonRoot;
        [SerializeField] private Button _originButtonPrefab;
        [SerializeField] private TMP_Text _originNameText;
        [SerializeField] private TMP_Text _detailsText;
        [Tooltip("Origin illustration beside the details; hidden when the origin has no artwork.")]
        [SerializeField] private Image _artworkImage;
        [SerializeField] private Button _beginButton;
        [Tooltip("Tint of the selected origin's button.")]
        [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.55f);

        private readonly List<(Button button, OriginSO origin)> _buttons = new List<(Button, OriginSO)>();
        private OriginSO _selected;
        private Action<OriginSO> _onBegin;

        private void Awake()
        {
            if (_catalog == null) GameLog.Error(TAG, "OriginPickUI: OriginCatalog not assigned — no origin can be picked");
            if (_artworkImage == null) GameLog.Warn(TAG, "OriginPickUI: Artwork Image not assigned — origin art won't show");
            BuildButtons();
            GameMenuUI.Wire(_beginButton, HandleBegin);
        }

        private void BuildButtons()
        {
            if (_catalog == null || _originButtonRoot == null || _originButtonPrefab == null) return;
            foreach (var origin in _catalog.origins)
            {
                if (origin == null) continue;
                var button = Instantiate(_originButtonPrefab, _originButtonRoot);
                button.name = $"Origin_{origin.originId}";
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = origin.displayName;
                    // Three buttons share the row: shrink long names instead of clipping them.
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 16f;
                    label.fontSizeMax = label.fontSize;
                }
                var captured = origin;
                button.onClick.AddListener(() => Select(captured));
                _buttons.Add((button, origin));
            }
        }

        /// <summary>Opens the panel with the default origin selected; Begin calls <paramref name="onBegin"/>.</summary>
        public void Show(Action<OriginSO> onBegin)
        {
            _onBegin = onBegin;
            gameObject.SetActive(true);
            // The default (Wretch) when it has a button, else the first button: the selection always shows.
            OriginSO initial = _catalog != null ? _catalog.DefaultOrFirst : null;
            if (_buttons.Count > 0 && !_buttons.Exists(b => b.origin == initial)) initial = _buttons[0].origin;
            Select(initial);
        }

        public void Hide()
        {
            _onBegin = null;
            gameObject.SetActive(false);
        }

        private void Select(OriginSO origin)
        {
            _selected = origin;
            foreach (var (button, o) in _buttons)
                if (button.targetGraphic != null) button.targetGraphic.color = o == origin ? _selectedColor : Color.white;
            if (_originNameText != null) _originNameText.text = origin != null ? origin.displayName : string.Empty;
            if (_detailsText != null) _detailsText.text = FormatDetails(origin);
            ShowArtwork(origin != null ? origin.artwork : null);
            if (_beginButton != null) _beginButton.interactable = origin != null;
        }

        private void ShowArtwork(Sprite artwork)
        {
            if (_artworkImage == null) return;
            _artworkImage.sprite = artwork;
            _artworkImage.preserveAspect = true;
            _artworkImage.gameObject.SetActive(artwork != null);
        }

        private void HandleBegin()
        {
            if (_selected != null) _onBegin?.Invoke(_selected);
        }

        /// <summary>Detail text: description, stat bonuses, starting skill, then the belongings line.</summary>
        public static string FormatDetails(OriginSO origin)
        {
            if (origin == null) return string.Empty;
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(origin.description)) sb.AppendLine(origin.description).AppendLine();

            var bonuses = new List<string>();
            // Same filter as PlayerStats.AddBaseStat: never promise a bonus that isn't applied.
            foreach (var b in origin.statBonuses)
                if (b.points > 0 && b.stat != StatType.Defense) bonuses.Add($"{b.stat} +{b.points}");
            sb.AppendLine(bonuses.Count > 0 ? $"Bonus: {string.Join(", ", bonuses)}" : "Bonus: none");
            sb.AppendLine(origin.skill != null ? $"Skill: {origin.skill.displayName}" : "Skill: none");
            sb.AppendLine();

            if (!origin.hasBelongings)
            {
                sb.Append(NO_BELONGINGS_TEXT);
                return sb.ToString();
            }
            sb.AppendLine(BELONGINGS_HEADER);
            foreach (var item in origin.promisedItems)
                if (!string.IsNullOrEmpty(item)) sb.AppendLine($"- {item}");
            if (origin.promisedGold > 0) sb.AppendLine($"- {origin.promisedGold} gold");
            return sb.ToString().TrimEnd();
        }
    }
}
