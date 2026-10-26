using Game.StatusEffects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One HUD status-effect entry (<c>StatusEffectIcon.prefab</c>): the effect icon (or its tint as a plain square),
    /// "×N" when stacked and the remaining game time. Painted by <see cref="StatusEffectBarUI"/>; labels are only
    /// rebuilt when their value changes.
    /// </summary>
    public class StatusEffectIconUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _timeLabel;
        [SerializeField] private TMP_Text _stackLabel;

        private StatusEffectSO _effect;
        private int _count = -1;
        private long _minutes = -1;

        public void Set(StatusEffectSO effect, int count, double remainingSeconds)
        {
            if (effect != _effect)
            {
                _effect = effect;
                if (_icon != null)
                {
                    _icon.sprite = effect != null ? effect.icon : null;
                    _icon.color = effect != null ? effect.iconTint : Color.white;
                }
            }
            if (count != _count)
            {
                _count = count;
                if (_stackLabel != null)
                {
                    _stackLabel.gameObject.SetActive(count > 1);
                    _stackLabel.text = "×" + count;
                }
            }
            long minutes = StatusEffectMath.RemainingMinutes(remainingSeconds);
            if (minutes != _minutes)
            {
                _minutes = minutes;
                if (_timeLabel != null) _timeLabel.text = StatusEffectMath.FormatRemaining(remainingSeconds);
            }
        }
    }
}
