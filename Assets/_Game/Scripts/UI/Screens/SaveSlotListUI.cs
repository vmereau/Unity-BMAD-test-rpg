using System;
using System.Globalization;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Game Menu sub-panel listing the save slots (one <see cref="SaveSlotEntryUI"/> row per slot, rebuilt on every
    /// <see cref="Show"/>). Save mode lists the manual slots only (quick / auto are written by F5 / autosave) and
    /// confirms overwrites; Load mode lists every slot, valid rows only are clickable and confirm before loading.
    /// Every existing row can be deleted after confirmation.
    /// </summary>
    public class SaveSlotListUI : MonoBehaviour
    {
        private const string TAG = "[SaveSlotList]";

        public enum Mode { Save, Load }

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private SaveSlotEntryUI _rowPrefab;
        [SerializeField] private ConfirmDialogUI _confirmDialog;

        private Mode _mode;

        public void Show(Mode mode)
        {
            _mode = mode;
            gameObject.SetActive(true);
            if (_titleText != null) _titleText.text = mode == Mode.Save ? "Save Game" : "Load Game";
            Refresh();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            if (_rowContainer == null || _rowPrefab == null)
            {
                GameLog.Error(TAG, "_rowContainer or _rowPrefab not assigned — slot list can't be built");
                return;
            }

            for (int i = _rowContainer.childCount - 1; i >= 0; i--)
                Destroy(_rowContainer.GetChild(i).gameObject);

            foreach (var slot in SaveFileStore.ListSlots())
            {
                bool isManual = slot.slotId.StartsWith(GameConstants.SAVE_MANUAL_SLOT_PREFIX, StringComparison.Ordinal);
                if (_mode == Mode.Save && !isManual) continue;

                var row = Instantiate(_rowPrefab, _rowContainer, false);
                bool clickable = _mode == Mode.Save || slot.isValid;
                string slotId = slot.slotId;
                bool exists = slot.exists;
                row.Bind(FormatSlotLabel(slotId), FormatDetails(slot), clickable, exists,
                    () => HandleRowClicked(slotId, exists),
                    () => HandleDeleteClicked(slotId));
            }
        }

        private void HandleRowClicked(string slotId, bool exists)
        {
            if (_mode == Mode.Save)
            {
                if (exists) Confirm($"Overwrite {FormatSlotLabel(slotId)}?", () => SaveTo(slotId));
                else SaveTo(slotId);
            }
            else
            {
                Confirm("Load this save? Unsaved progress will be lost.", () => SaveSystem.Instance?.Load(slotId));
            }
        }

        private void HandleDeleteClicked(string slotId)
        {
            Confirm($"Delete {FormatSlotLabel(slotId)}?", () =>
            {
                if (!SaveFileStore.TryDelete(slotId, out var error))
                    GameLog.Warn(TAG, $"Delete '{slotId}' failed: {error}");
                Refresh();
            });
        }

        private void SaveTo(string slotId)
        {
            if (SaveSystem.Instance == null)
            {
                GameLog.Error(TAG, "No SaveSystem instance — can't save");
                return;
            }
            SaveSystem.Instance.Save(slotId); // raises its own success / error notification
            Refresh();
        }

        private void Confirm(string message, Action onYes)
        {
            if (_confirmDialog != null) _confirmDialog.Show(message, onYes);
            else onYes();
        }

        // ── Formatting (static for tests) ─────────────────────────────────────

        /// <summary>"Quicksave", "Autosave", "Slot 3" (unknown ids are returned as-is).</summary>
        public static string FormatSlotLabel(string slotId)
        {
            if (slotId == GameConstants.SAVE_SLOT_QUICK) return "Quicksave";
            if (slotId == GameConstants.SAVE_SLOT_AUTO) return "Autosave";
            if (slotId != null && slotId.StartsWith(GameConstants.SAVE_MANUAL_SLOT_PREFIX, StringComparison.Ordinal))
                return "Slot " + slotId.Substring(GameConstants.SAVE_MANUAL_SLOT_PREFIX.Length);
            return slotId;
        }

        /// <summary>"{region} — Level {n} — {local date/time} — {h:mm}", "Empty" or "Corrupted — {error}".</summary>
        public static string FormatDetails(SlotInfo slot)
        {
            if (!slot.exists) return "Empty";
            if (!slot.isValid || slot.meta == null) return $"Corrupted — {slot.error}";

            var meta = slot.meta;
            return $"{meta.region} — Level {meta.playerLevel} — {FormatTimestamp(meta.savedAtUtc)} — {FormatPlaytime(meta.playtimeSeconds)}";
        }

        public static string FormatTimestamp(string savedAtUtc)
        {
            return DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc)
                ? utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : "?";
        }

        /// <summary>Playtime as h:mm (e.g. 3725 s → "1:02").</summary>
        public static string FormatPlaytime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 3600}:{total % 3600 / 60:00}";
        }
    }
}
