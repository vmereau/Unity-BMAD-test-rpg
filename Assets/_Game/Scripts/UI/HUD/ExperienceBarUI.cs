using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Thin HUD bar below the StaminaBar showing progress toward the next level.
    /// Subscribes to OnPlayerXPProgressChanged (normalized 0–1, raised by LevelSystem).
    /// </summary>
    public class ExperienceBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private Image _fillImage;
        [SerializeField] private GameEventSO_Float _onPlayerXPProgressChanged;

        private void Awake()
        {
            if (_fillImage == null)
            {
                GameLog.Error(TAG, "ExperienceBarUI: _fillImage not assigned");
                enabled = false;
                return;
            }
            if (_onPlayerXPProgressChanged == null)
                GameLog.Warn(TAG, "ExperienceBarUI: _onPlayerXPProgressChanged not assigned — bar will not update");
        }

        private void OnEnable()
        {
            _onPlayerXPProgressChanged?.AddListener(HandleXPProgressChanged);
        }

        private void OnDisable()
        {
            _onPlayerXPProgressChanged?.RemoveListener(HandleXPProgressChanged);
        }

        private void HandleXPProgressChanged(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            _fillImage.transform.localScale = new Vector3(clamped, 1f, 1f);
        }
    }
}
