using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RunGun.Settings
{
    /// <summary>
    /// Connects manually authored Audio-tab sliders and labels to the settings backend.
    /// It does not create or style UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Audio Settings Tab")]
    public sealed class AudioSettingsTab : MonoBehaviour
    {
        private const float VolumeStep = 0.05f;

        [SerializeField] private SettingsUiBridge settings;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text musicPercentText;
        [SerializeField] private TMP_Text sfxPercentText;

        private void OnEnable()
        {
            if (settings == null)
                settings = GetComponentInParent<SettingsUiBridge>();

            ConfigureSlider(musicSlider, GameSettings.MusicVolume);
            ConfigureSlider(sfxSlider, GameSettings.SfxVolume);
            UpdateMusic(GameSettings.MusicVolume, save: false);
            UpdateSfx(GameSettings.SfxVolume, save: false);

            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
        }

        private void OnDisable()
        {
            if (musicSlider != null)
                musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
        }

        private void HandleMusicChanged(float value)
        {
            value = SnapVolume(value);
            musicSlider?.SetValueWithoutNotify(value);
            UpdateMusic(value, save: true);
        }

        private void HandleSfxChanged(float value)
        {
            value = SnapVolume(value);
            sfxSlider?.SetValueWithoutNotify(value);
            UpdateSfx(value, save: true);
        }

        private void UpdateMusic(float value, bool save)
        {
            value = Mathf.Clamp01(value);
            if (musicPercentText != null)
                musicPercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";

            if (save)
            {
                if (settings != null)
                    settings.SetMusicVolume(value);
                else
                    GameSettings.MusicVolume = value;
            }
        }

        private void UpdateSfx(float value, bool save)
        {
            value = Mathf.Clamp01(value);
            if (sfxPercentText != null)
                sfxPercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";

            if (save)
            {
                if (settings != null)
                    settings.SetSfxVolume(value);
                else
                    GameSettings.SfxVolume = value;
            }
        }

        private static void ConfigureSlider(Slider slider, float value)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(SnapVolume(value));
        }

        private static float SnapVolume(float value)
        {
            return Mathf.Clamp01(Mathf.Round(value / VolumeStep) * VolumeStep);
        }
    }
}
