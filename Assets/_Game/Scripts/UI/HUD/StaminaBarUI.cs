using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Displays the player's current stamina as a horizontal fill bar below the HealthBar.
    /// Subscribes to OnPlayerStaminaChanged (float = normalized ratio 0–1).
    /// Ratio is pre-normalized by StaminaSystem so this component needs no config reference.
    /// </summary>
    public class StaminaBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private GameEventSO_Float _onPlayerStaminaChanged;

        private void Awake()
        {
            if (_fillImage == null)
            {
                GameLog.Error(TAG, "StaminaBarUI: _fillImage not assigned");
                enabled = false;
                return;
            }
            if (_onPlayerStaminaChanged == null)
                GameLog.Warn(TAG, "StaminaBarUI: _onPlayerStaminaChanged not assigned — bar will not update");
        }

        private void OnEnable()
        {
            _onPlayerStaminaChanged?.AddListener(HandleStaminaChanged);
        }

        private void OnDisable()
        {
            _onPlayerStaminaChanged?.RemoveListener(HandleStaminaChanged);
        }

        private void HandleStaminaChanged(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            _fillImage.transform.localScale = new Vector3(clamped, 1f, 1f);
        }
    }
}
