using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RunGun.Settings
{
    /// <summary>
    /// Connects manually authored Display-tab dropdowns to the settings backend.
    /// It does not create or style UI.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Display Settings Tab")]
    public sealed class DisplaySettingsTab : MonoBehaviour
    {
        [SerializeField] private SettingsUiBridge settings;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown windowModeDropdown;

        private void OnEnable()
        {
            if (settings == null)
                settings = GetComponentInParent<SettingsUiBridge>();

            if (settings == null)
            {
                Debug.LogError("DisplaySettingsTab requires a SettingsUiBridge reference.", this);
                return;
            }

            PopulateResolutionDropdown();
            PopulateWindowModeDropdown();

            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(settings.SetResolution);
            if (windowModeDropdown != null)
                windowModeDropdown.onValueChanged.AddListener(settings.SetWindowMode);
        }

        private void OnDisable()
        {
            if (settings != null && resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(settings.SetResolution);
            if (settings != null && windowModeDropdown != null)
                windowModeDropdown.onValueChanged.RemoveListener(settings.SetWindowMode);
        }

        private void PopulateResolutionDropdown()
        {
            if (resolutionDropdown == null)
                return;

            settings.RefreshResolutions();
            var options = new List<string>(settings.Resolutions.Count);
            for (int i = 0; i < settings.Resolutions.Count; i++)
                options.Add(settings.GetResolutionLabel(i));

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(0);
            resolutionDropdown.interactable = false;
            resolutionDropdown.RefreshShownValue();
        }

        private void PopulateWindowModeDropdown()
        {
            if (windowModeDropdown == null)
                return;

            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new List<string>
            {
                "Borderless",
                "Exclusive Fullscreen",
                "Windowed"
            });
            windowModeDropdown.SetValueWithoutNotify(settings.GetCurrentWindowModeIndex());
            windowModeDropdown.RefreshShownValue();
        }
    }
}
