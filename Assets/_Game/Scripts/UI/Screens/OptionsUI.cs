using Game.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Options sub-panel of the Game Menu. Music and Effects volume sliders (0–1) backed by
    /// <see cref="MusicVolumeSettings"/> / <see cref="SfxVolumeSettings"/>; changes apply live; PlayerPrefs are
    /// flushed when the panel closes.
    /// </summary>
    public class OptionsUI : MonoBehaviour, IScreenPanel
    {
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private TMP_Text _musicValueLabel;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private TMP_Text _sfxValueLabel;

        private void Awake()
        {
            ConfigureSlider(_musicSlider);
            ConfigureSlider(_sfxSlider);
        }

        private void OnEnable()
        {
            if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(HandleSfxSliderChanged);
        }

        private void OnDisable()
        {
            if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(HandleMusicSliderChanged);
            if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(HandleSfxSliderChanged);
        }

        public void OnScreenOpen()
        {
            float volume = MusicVolumeSettings.Get();
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(volume);
            UpdateLabel(volume);

            float sfx = SfxVolumeSettings.Get();
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(sfx);
            UpdateSfxLabel(sfx);
        }

        public void OnScreenClose()
        {
            MusicVolumeSettings.Save();
            SfxVolumeSettings.Save();
        }

        private void HandleMusicSliderChanged(float value)
        {
            MusicVolumeSettings.Set(value);
            UpdateLabel(value);
        }

        private void HandleSfxSliderChanged(float value)
        {
            SfxVolumeSettings.Set(value);
            UpdateSfxLabel(value);
        }

        private void UpdateLabel(float volume)
        {
            if (_musicValueLabel != null) _musicValueLabel.text = MusicVolumeSettings.FormatPercent(volume);
        }

        private void UpdateSfxLabel(float volume)
        {
            if (_sfxValueLabel != null) _sfxValueLabel.text = SfxVolumeSettings.FormatPercent(volume);
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }
}
