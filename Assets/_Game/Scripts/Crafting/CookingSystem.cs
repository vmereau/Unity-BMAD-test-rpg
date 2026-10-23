using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// Player-side listener for <see cref="CookingStation"/> open requests → opens the <see cref="CookingUI"/>.
    /// Mirrors <c>ContainerSystem</c>; future station gates (skills, tools) belong here, not on the station.
    /// </summary>
    public class CookingSystem : MonoBehaviour
    {
        private const string TAG = "[CookingSystem]";

        [SerializeField] private GameEventSO_CookingStationOpenRequest _onOpenRequested;
        [SerializeField] private CookingUI _cookingUI;

        private void OnEnable()
        {
            if (_onOpenRequested == null)
            {
                GameLog.Warn(TAG, "No cooking open event assigned — CookingSystem will not respond");
                return;
            }
            _onOpenRequested.AddListener(HandleOpenRequested);
        }

        private void OnDisable()
        {
            if (_onOpenRequested == null) return;
            _onOpenRequested.RemoveListener(HandleOpenRequested);
        }

        private void HandleOpenRequested(CookingStationOpenRequestData data)
        {
            if (_cookingUI == null)
            {
                GameLog.Warn(TAG, "CookingUI is not assigned — cannot open cooking station");
                return;
            }
            _cookingUI.Open(data.recipes, data.stationName);
        }
    }
}
