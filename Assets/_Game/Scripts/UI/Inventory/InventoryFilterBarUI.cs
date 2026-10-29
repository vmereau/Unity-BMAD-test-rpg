using System;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Row of icon tabs filtering the inventory grid by <see cref="ItemFilter"/>, with one shared hover tooltip
    /// ("Weapons (3)"). View only: <see cref="InventoryUI"/> reads <see cref="Active"/> and pushes the counts.
    /// </summary>
    public class InventoryFilterBarUI : MonoBehaviour
    {
        private const string TAG = "[InventoryFilterBar]";

        [Serializable]
        private struct Entry
        {
            public ItemFilter filter;
            public Sprite icon;
        }

        [SerializeField] private InventoryFilterTabUI _tabPrefab;
        [SerializeField] private Transform _tabsRoot;
        [SerializeField] private List<Entry> _entries = new List<Entry>();
        [Tooltip("Lives under the grid panel (last sibling) so it draws over the slots. Not a raycast target.")]
        [SerializeField] private RectTransform _tooltipRoot;
        [SerializeField] private TMP_Text _tooltipText;
        [SerializeField] private Vector2 _tooltipOffset = new Vector2(0f, -6f);

        public ItemFilter Active { get; private set; } = ItemFilter.All;
        public event Action<ItemFilter> FilterChanged;

        private readonly List<InventoryFilterTabUI> _tabs = new List<InventoryFilterTabUI>();
        private readonly int[] _counts = new int[ItemFilterRules.COUNT];
        private readonly Vector3[] _corners = new Vector3[4];
        private InventoryFilterTabUI _hoveredTab;

        private void Awake()
        {
            BuildTabs();
            HideTooltip();
        }

        private void OnDisable() => HideTooltip();

        private void BuildTabs()
        {
            if (_tabs.Count > 0) return; // already built
            if (_tabPrefab == null || _tabsRoot == null)
            {
                GameLog.Warn(TAG, "_tabPrefab or _tabsRoot not assigned — no filter tabs");
                return;
            }
            foreach (var entry in _entries)
            {
                if ((int)entry.filter < 0 || (int)entry.filter >= ItemFilterRules.COUNT)
                {
                    GameLog.Warn(TAG, $"Entry with invalid filter value {(int)entry.filter} skipped");
                    continue;
                }
                if (entry.icon == null)
                    GameLog.Warn(TAG, $"No icon for '{entry.filter}' — the tab shows its label");
                var tab = Instantiate(_tabPrefab, _tabsRoot);
                tab.name = $"Tab_{entry.filter}";
                tab.Init(this, entry.filter, entry.icon);
                _tabs.Add(tab);
            }
            RepaintTabs();
        }

        /// <summary>Back to All without raising <see cref="FilterChanged"/> (the caller repaints anyway).</summary>
        public void ResetToAll()
        {
            Active = ItemFilter.All;
            RepaintTabs();
        }

        public void Select(ItemFilter filter)
        {
            if (filter == Active) return;
            Active = filter;
            RepaintTabs();
            FilterChanged?.Invoke(filter);
        }

        /// <summary>Slot count per filter, indexed by <c>(int)ItemFilter</c>.</summary>
        public void SetCounts(int[] counts)
        {
            if (counts == null) return;
            Array.Copy(counts, _counts, Mathf.Min(counts.Length, _counts.Length));
            foreach (var tab in _tabs)
                tab.SetCount(_counts[(int)tab.Filter]);
            // The inventory can change under the cursor (action-bar hotkey, loot) — keep the open tooltip current.
            if (_hoveredTab != null && _tooltipText != null)
                _tooltipText.text = ItemFilterRules.TooltipText(_hoveredTab.Filter, _counts[(int)_hoveredTab.Filter]);
        }

        public void ShowTooltip(InventoryFilterTabUI tab)
        {
            if (_tooltipRoot == null || _tooltipText == null || tab == null) return;
            _hoveredTab = tab;
            _tooltipText.text = ItemFilterRules.TooltipText(tab.Filter, _counts[(int)tab.Filter]);
            _tooltipRoot.gameObject.SetActive(true);
            _tooltipRoot.SetAsLastSibling();

            // Centred under the tab: tooltip pivot is expected at top-centre (0.5, 1).
            tab.Rect.GetWorldCorners(_corners); // 0 = bottom-left, 3 = bottom-right
            _tooltipRoot.position = (_corners[0] + _corners[3]) * 0.5f + (Vector3)_tooltipOffset;
        }

        public void HideTooltip()
        {
            _hoveredTab = null;
            if (_tooltipRoot != null) _tooltipRoot.gameObject.SetActive(false);
        }

        private void RepaintTabs()
        {
            foreach (var tab in _tabs)
                tab.SetActive(tab.Filter == Active);
        }
    }
}
