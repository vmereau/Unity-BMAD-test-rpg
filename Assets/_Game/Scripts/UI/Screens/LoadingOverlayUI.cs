using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Full-screen black "Loading…" panel shown while a save loads. The root has its own nested Canvas with a high
    /// sort order (draws above every menu) and stays active; <c>_panel</c> is toggled by the load started /
    /// finished events.
    /// </summary>
    public class LoadingOverlayUI : MonoBehaviour
    {
        private const string TAG = "[LoadingOverlay]";

        [SerializeField] private GameObject _panel;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Void _onLoadStarted;
        [SerializeField] private GameEventSO_Void _onLoadFinished;

        private void Awake()
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "_panel not assigned — loading overlay disabled");
                enabled = false;
                return;
            }
            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            _onLoadStarted?.AddListener(HandleLoadStarted);
            _onLoadFinished?.AddListener(HandleLoadFinished);
        }

        private void OnDisable()
        {
            _onLoadStarted?.RemoveListener(HandleLoadStarted);
            _onLoadFinished?.RemoveListener(HandleLoadFinished);
        }

        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void HandleLoadStarted(bool _) => Show();

        private void HandleLoadFinished(bool _) => Hide();
    }
}
