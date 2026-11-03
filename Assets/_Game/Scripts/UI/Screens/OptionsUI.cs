using Game.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Options sub-panel of the Game Menu. Music volume slider (0–1) backed by <see cref="MusicVolumeSettings"/>;
    /// changes apply live; PlayerPrefs are flushed when the panel closes.
    /// </summary>
    public class OptionsUI : MonoBehaviour, IScreenPanel
    {
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private TMP_Text _musicValueLabel;

        private void Awake()
        {
            if (_musicSlider == null) return;
            _musicSlider.minValue = 0f;
            _musicSlider.maxValue = 1f;
            _musicSlider.wholeNumbers = false;
        }

        private void OnEnable()
        {
            if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);
        }

        private void OnDisable()
        {
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(HandleMusicSliderChanged);
        }

        public void OnScreenOpen()
        {
            float volume = MusicVolumeSettings.Get();
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(volume);
            UpdateLabel(volume);
        }

        public void OnScreenClose()
        {
            MusicVolumeSettings.Save();
        }

        private void HandleMusicSliderChanged(float value)
        {
            MusicVolumeSettings.Set(value);
            UpdateLabel(value);
        }

        private void UpdateLabel(float volume)
        {
            if (_musicValueLabel != null) _musicValueLabel.text = MusicVolumeSettings.FormatPercent(volume);
        }
    }
}
