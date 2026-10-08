using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One row of the save / load slot list (SaveSlotEntry.prefab): slot label, details line, the row button
    /// (save to / load this slot) and a Delete button. Built and bound by <see cref="SaveSlotListUI"/>.
    /// </summary>
    public class SaveSlotEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private TMP_Text _detailsText;
        [SerializeField] private Button _rowButton;
        [SerializeField] private Button _deleteButton;

        public void Bind(string label, string details, bool clickable, bool showDelete, Action onClick, Action onDelete)
        {
            if (_labelText != null) _labelText.text = label;
            if (_detailsText != null) _detailsText.text = details;

            if (_rowButton != null)
            {
                _rowButton.onClick.RemoveAllListeners();
                _rowButton.interactable = clickable;
                if (clickable && onClick != null) _rowButton.onClick.AddListener(() => onClick());
            }

            if (_deleteButton != null)
            {
                _deleteButton.onClick.RemoveAllListeners();
                _deleteButton.gameObject.SetActive(showDelete);
                if (showDelete && onDelete != null) _deleteButton.onClick.AddListener(() => onDelete());
            }
        }
    }
}
