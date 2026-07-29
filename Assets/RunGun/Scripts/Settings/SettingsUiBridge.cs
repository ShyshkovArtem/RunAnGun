using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RunGun.Settings
{
    /// <summary>
    /// UnityEvent-friendly entry points for a manually authored settings UI.
    /// This component never creates, positions, or styles UI objects.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Settings UI Bridge")]
    public sealed class SettingsUiBridge : MonoBehaviour
    {
        public readonly struct DisplayResolution
        {
            public readonly int Width;
            public readonly int Height;

            public DisplayResolution(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        [SerializeField] private GameObject settingsPanel;
        [Tooltip("Optional override. When empty, DisplayBtn is selected.")]
        [SerializeField] private Selectable defaultSelection;

        private readonly List<DisplayResolution> _resolutions = new();
        private readonly List<(Button Button, UnityAction Action)> _tabListeners = new();
        private readonly Dictionary<string, GameObject> _tabPanels = new();
        private readonly Dictionary<string, Button> _tabButtons = new();
        private CanvasGroup _mainMenuButtonGroup;
        private float _mainMenuOriginalAlpha = 1f;

        public IReadOnlyList<DisplayResolution> Resolutions => _resolutions;

        private void Awake()
        {
            RefreshResolutions();
            BindMainMenuButtons();
            CacheSettingsUi();
            BindTabButtons();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _tabListeners.Count; i++)
            {
                var listener = _tabListeners[i];
                if (listener.Button != null)
                    listener.Button.onClick.RemoveListener(listener.Action);
            }
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
                return;

            settingsPanel.SetActive(true);
            SetMainMenuButtonsEnabled(false);
            ActivateDefaultPanel();
            StartCoroutine(SelectDefaultNextFrame());
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            SetMainMenuButtonsEnabled(true);
            SelectGameObject(null);
        }

        private void BindMainMenuButtons()
        {
            GameObject buttonPanel = GameObject.Find("ButtonPanel");
            if (buttonPanel == null)
                return;

            _mainMenuButtonGroup = buttonPanel.GetComponent<CanvasGroup>();
            if (_mainMenuButtonGroup == null)
                _mainMenuButtonGroup = buttonPanel.AddComponent<CanvasGroup>();

            _mainMenuOriginalAlpha = _mainMenuButtonGroup.alpha;
        }

        private void SetMainMenuButtonsEnabled(bool enabled)
        {
            if (_mainMenuButtonGroup == null)
                BindMainMenuButtons();
            if (_mainMenuButtonGroup == null)
                return;

            _mainMenuButtonGroup.interactable = enabled;
            _mainMenuButtonGroup.blocksRaycasts = enabled;
            _mainMenuButtonGroup.alpha = enabled
                ? _mainMenuOriginalAlpha
                : _mainMenuOriginalAlpha * 0.3f;
        }

        private void ActivateDefaultPanel()
        {
            ShowDisplayTab();
        }

        public void ShowDisplayTab()
        {
            ShowTab("DisplayPanel");
        }

        public void ShowAudioTab()
        {
            ShowTab("AudioPanel");
        }

        public void ShowControlsTab()
        {
            ShowTab("ControlsPanel");
        }

        public void ShowCrosshairTab()
        {
            ShowTab("CrosshairPanel");
        }

        private void ShowTab(string activePanelName)
        {
            if (_tabPanels.Count == 0)
                CacheSettingsUi();

            foreach (KeyValuePair<string, GameObject> entry in _tabPanels)
            {
                if (entry.Value != null)
                    entry.Value.SetActive(entry.Key == activePanelName);
            }
        }

        private void CacheSettingsUi()
        {
            _tabPanels.Clear();
            _tabButtons.Clear();
            if (settingsPanel == null)
                return;

            Transform[] descendants = settingsPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                switch (descendant.name)
                {
                    case "DisplayPanel":
                    case "AudioPanel":
                    case "ControlsPanel":
                    case "CrosshairPanel":
                        _tabPanels[descendant.name] = descendant.gameObject;
                        break;
                    case "DisplayBtn":
                    case "AudioBtn":
                    case "ControlsBtn":
                    case "CrosshairBtn":
                        Button button = descendant.GetComponent<Button>();
                        if (button != null)
                            _tabButtons[descendant.name] = button;
                        break;
                }
            }
        }

        private void BindTabButtons()
        {
            BindTabButton("DisplayBtn", ShowDisplayTab);
            BindTabButton("AudioBtn", ShowAudioTab);
            BindTabButton("ControlsBtn", ShowControlsTab);
            BindTabButton("CrosshairBtn", ShowCrosshairTab);
        }

        private void BindTabButton(string objectName, UnityAction action)
        {
            if (!_tabButtons.TryGetValue(objectName, out Button button) || button == null)
                return;

            button.onClick.AddListener(action);
            _tabListeners.Add((button, action));
        }

        private IEnumerator SelectDefaultNextFrame()
        {
            yield return null;

            if (settingsPanel == null || !settingsPanel.activeInHierarchy)
                yield break;

            Selectable selection = defaultSelection;
            if (selection == null)
            {
                Selectable[] selectables = settingsPanel.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < selectables.Length; i++)
                {
                    if (selectables[i].name == "DisplayBtn")
                    {
                        selection = selectables[i];
                        break;
                    }
                }

                if (selection == null)
                {
                    for (int i = 0; i < selectables.Length; i++)
                    {
                        if (selectables[i].IsActive() && selectables[i].IsInteractable())
                        {
                            selection = selectables[i];
                            break;
                        }
                    }
                }
            }

            SelectGameObject(selection != null ? selection.gameObject : null);
        }

        private static void SelectGameObject(GameObject target)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            eventSystem.SetSelectedGameObject(null);
            if (target != null && target.activeInHierarchy)
                eventSystem.SetSelectedGameObject(target);
        }

        public void RefreshResolutions()
        {
            _resolutions.Clear();
            _resolutions.Add(new DisplayResolution(
                GameSettings.SupportedResolutionWidth,
                GameSettings.SupportedResolutionHeight));
        }

        public string GetResolutionLabel(int index)
        {
            if (_resolutions.Count == 0)
                RefreshResolutions();

            index = Mathf.Clamp(index, 0, _resolutions.Count - 1);
            DisplayResolution resolution = _resolutions[index];
            return $"{resolution.Width} x {resolution.Height}";
        }

        public int GetCurrentResolutionIndex()
        {
            if (_resolutions.Count == 0)
                RefreshResolutions();

            int index = _resolutions.FindIndex(resolution =>
                resolution.Width == Screen.width && resolution.Height == Screen.height);

            if (index >= 0)
                return index;

            return 0;
        }

        public void SetResolution(int index)
        {
            if (_resolutions.Count == 0)
                RefreshResolutions();

            index = Mathf.Clamp(index, 0, _resolutions.Count - 1);
            DisplayResolution resolution = _resolutions[index];
            GameSettings.ApplyDisplay(resolution.Width, resolution.Height, Screen.fullScreenMode);
        }

        // Dropdown order: Borderless, Exclusive Fullscreen, Windowed.
        public void SetWindowMode(int index)
        {
            FullScreenMode mode = index switch
            {
                1 => FullScreenMode.ExclusiveFullScreen,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            GameSettings.ApplyDisplay(
                GameSettings.SupportedResolutionWidth,
                GameSettings.SupportedResolutionHeight,
                mode);
        }

        public int GetCurrentWindowModeIndex()
        {
            return Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => 1,
                FullScreenMode.Windowed => 2,
                _ => 0
            };
        }

        public void SetSensitivity(float value)
        {
            GameSettings.Sensitivity = value;
        }

        public void SetCrosshairStyle(int index)
        {
            GameSettings.CrosshairStyle = index;
        }

        public void SetMusicVolume(float value)
        {
            GameSettings.MusicVolume = value;
        }

        public void SetSfxVolume(float value)
        {
            GameSettings.SfxVolume = value;
        }
    }
}
