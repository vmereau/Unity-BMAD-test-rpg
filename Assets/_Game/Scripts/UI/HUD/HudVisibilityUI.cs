using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Hides the whole HUD (<c>UICanvas/Game</c>) through its CanvasGroup while at least one world screen
    /// (trade, container, crafting) is open — see <see cref="HideHudWhileOpen"/>. HUD components keep running
    /// (they still receive their event channels); only alpha and raycasts are cut.
    /// Toasts stay visible because <c>NotificationContainer</c> has a CanvasGroup with <c>ignoreParentGroups</c>.
    /// Owns the <c>Game</c> CanvasGroup: no other system should write its alpha.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HudVisibilityUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        private readonly HudHiderSet _hiders = new HudHiderSet();
        private CanvasGroup _canvasGroup;

        public bool IsHidden => _hiders.IsHidden;

        private void Awake()
        {
            CacheCanvasGroup();
            Apply();
        }

        public void AddHider(Object hider)
        {
            if (!_hiders.Add(hider)) return;
            Apply();
            GameLog.Info(TAG, $"HudVisibilityUI: HUD hidden by {hider.name}");
        }

        public void RemoveHider(Object hider)
        {
            if (!_hiders.Remove(hider)) return;
            Apply();
            GameLog.Info(TAG, "HudVisibilityUI: HUD shown");
        }

        private void CacheCanvasGroup()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Apply()
        {
            CacheCanvasGroup(); // a hider can register before Awake
            if (_canvasGroup == null) return;
            bool visible = !_hiders.IsHidden;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
