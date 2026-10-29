using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Put on the root of a world screen that toggles its own GameObject in Open / Close
    /// (ContainerUI, NPCTradeUI, CookingUI): the HUD is hidden while this GameObject is active.
    /// </summary>
    public class HideHudWhileOpen : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private HudVisibilityUI _hud;

        private void Awake()
        {
            if (_hud == null)
                GameLog.Warn(TAG, $"HideHudWhileOpen on {name}: _hud not assigned — HUD will stay visible over this screen");
        }

        private void OnEnable()
        {
            if (_hud != null) _hud.AddHider(this);
        }

        private void OnDisable()
        {
            if (_hud != null) _hud.RemoveHider(this);
        }
    }
}
