using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Shared modal Yes / No dialog (Game Menu quit, slot overwrite / load / delete).
    /// Lives on its own GameObject under UICanvas, drawn above the Game Menu and death screen; inactive when hidden.
    /// Doesn't touch the cursor or timeScale — the screen that opens it already owns both.
    /// </summary>
    public class ConfirmDialogUI : MonoBehaviour
    {
        private const string TAG = "[ConfirmDialog]";

        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        private Action _onYes;
        private Action _onNo;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_yesButton != null) _yesButton.onClick.AddListener(HandleYes);
            else GameLog.Warn(TAG, "_yesButton not assigned");
            if (_noButton != null) _noButton.onClick.AddListener(Cancel);
            else GameLog.Warn(TAG, "_noButton not assigned");
        }

        /// <summary>Shows the dialog. <paramref name="onYes"/> runs after the dialog hides.</summary>
        public void Show(string message, Action onYes, Action onNo = null)
        {
            _onYes = onYes;
            _onNo = onNo;
            if (_messageText != null) _messageText.text = message;
            gameObject.SetActive(true);
        }

        /// <summary>Same as clicking No (also used by Esc).</summary>
        public void Cancel()
        {
            var onNo = _onNo;
            Hide();
            onNo?.Invoke();
        }

        /// <summary>Hides without invoking either callback.</summary>
        public void Hide()
        {
            _onYes = null;
            _onNo = null;
            gameObject.SetActive(false);
        }

        private void HandleYes()
        {
            var onYes = _onYes;
            Hide();
            onYes?.Invoke();
        }
    }
}
