using System.Collections.Generic;
using RunGun.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityAction = UnityEngine.Events.UnityAction;

namespace RunGun.Settings
{
    /// <summary>
    /// Connects the manually authored Crosshair tab to the persistent runtime settings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Crosshair Settings Tab")]
    public sealed class CrosshairSettingsTab : MonoBehaviour
    {
        private const float SizeStep = 0.1f;
        private const float OpacityStep = 0.05f;

        private const float MinimumSize = 0.1f;
        private const float MaximumSize = 4f;
        private const float MinimumOpacity = 0f;
        private static readonly Color SelectedBorderColor = new Color32(255, 52, 52, 255);

        private sealed class Choice
        {
            public int Index;
            public Button Button;
            public Image Border;
            public Image Icon;
            public TMP_Text Label;
            public Color NormalBorderColor;
            public UnityAction ClickAction;
        }

        private readonly List<Choice> _styles = new();
        private readonly List<Choice> _colors = new();
        private Slider _sizeSlider;
        private Slider _opacitySlider;
        private TMP_Text _sizeValue;
        private TMP_Text _opacityValue;
        private Button _resetButton;
        private Image _previewCrosshair;
        private UiButtonAudioController _buttonFeedback;
        private bool _initialized;

        private void OnEnable()
        {
            if (!_initialized)
                InitializeHierarchy();

            RefreshAll();
            AddListeners();
        }

        private void OnDisable()
        {
            RemoveListeners();
        }

        private void InitializeHierarchy()
        {
            _sizeSlider = FindComponent<Slider>("SizeSlider");
            _opacitySlider = FindComponent<Slider>("OpacitySlider");
            _sizeValue = FindComponent<TMP_Text>("SizeValue");
            _opacityValue = FindComponent<TMP_Text>("OpacityValue");
            _resetButton = FindComponent<Button>("ResetButton");
            _previewCrosshair = FindComponent<Image>("PreviewCrosshair");
            _buttonFeedback = GetComponentInParent<UiButtonAudioController>();
            DisableContainerButton("SizeRow");
            DisableContainerButton("OpacityRow");

            string[] styleObjects =
                { "DotStyle", "ClassicStyle", "CircleStyle", "CrossStyle" };
            for (int i = 0; i < styleObjects.Length; i++)
                AddChoice(_styles, styleObjects[i], i, CrosshairSettings.StyleNames[i], true);

            string[] colorObjects =
                { "WhiteColor", "RedColor", "CyanColor", "GreenColor", "YellowColor", "MagentaColor" };
            for (int i = 0; i < colorObjects.Length; i++)
                AddChoice(_colors, colorObjects[i], i, CrosshairSettings.ColorNames[i], false);

            ConfigureSlider(_sizeSlider, MinimumSize, MaximumSize);
            ConfigureSlider(_opacitySlider, MinimumOpacity, 1f);

            // Newly added card buttons need to join the shared hover/audio system.
            if (_buttonFeedback != null)
                _buttonFeedback.BindButtons();

            _initialized = true;
        }

        private void DisableContainerButton(string objectName)
        {
            Transform row = FindChildRecursive(transform, objectName);
            Button button = row != null ? row.GetComponent<Button>() : null;
            if (button == null)
                return;

            button.transition = Selectable.Transition.None;
            button.interactable = false;
        }

        private void AddChoice(
            List<Choice> target,
            string objectName,
            int index,
            string labelText,
            bool style)
        {
            Transform root = FindChildRecursive(transform, objectName);
            if (root == null)
            {
                Debug.LogWarning($"Crosshair tab is missing '{objectName}'.", this);
                return;
            }

            Image border = FindChildComponent<Image>(root, "Border");
            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.None;
            button.targetGraphic = border;

            var choice = new Choice
            {
                Index = index,
                Button = button,
                Border = border,
                Icon = FindChildComponent<Image>(root, "Icon"),
                Label = FindChildComponent<TMP_Text>(root, "Name"),
                NormalBorderColor = border != null ? border.color : Color.white
            };

            GameLocalization.SetText(choice.Label, labelText);

            if (choice.Icon != null)
            {
                if (style)
                {
                    choice.Icon.sprite = CrosshairSettings.GetSprite(index);
                    choice.Icon.color = Color.white;
                    choice.Icon.preserveAspect = true;
                }
                else
                {
                    choice.Icon.color = CrosshairSettings.GetColor(index);
                }
            }

            choice.ClickAction = style
                ? () => SelectStyle(choice.Index)
                : () => SelectColor(choice.Index);
            target.Add(choice);
        }

        private void AddListeners()
        {
            if (_sizeSlider != null)
                _sizeSlider.onValueChanged.AddListener(SetSize);
            if (_opacitySlider != null)
                _opacitySlider.onValueChanged.AddListener(SetOpacity);
            if (_resetButton != null)
                _resetButton.onClick.AddListener(ResetToDefault);

            AddChoiceListeners(_styles);
            AddChoiceListeners(_colors);
        }

        private void RemoveListeners()
        {
            if (_sizeSlider != null)
                _sizeSlider.onValueChanged.RemoveListener(SetSize);
            if (_opacitySlider != null)
                _opacitySlider.onValueChanged.RemoveListener(SetOpacity);
            if (_resetButton != null)
                _resetButton.onClick.RemoveListener(ResetToDefault);

            RemoveChoiceListeners(_styles);
            RemoveChoiceListeners(_colors);
        }

        private static void AddChoiceListeners(List<Choice> choices)
        {
            for (int i = 0; i < choices.Count; i++)
                choices[i].Button.onClick.AddListener(choices[i].ClickAction);
        }

        private static void RemoveChoiceListeners(List<Choice> choices)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];
                if (choice.Button != null)
                    choice.Button.onClick.RemoveListener(choice.ClickAction);
            }
        }

        private void SelectStyle(int index)
        {
            GameSettings.CrosshairStyle = index;
            RefreshAll();
        }

        private void SelectColor(int index)
        {
            GameSettings.CrosshairColor = index;
            RefreshAll();
        }

        private void SetSize(float value)
        {
            value = SnapToStep(value, SizeStep, _sizeSlider.minValue, _sizeSlider.maxValue);
            _sizeSlider.SetValueWithoutNotify(value);
            GameSettings.CrosshairSize = value;
            RefreshPreview();
            UpdateValueLabels();
        }

        private void SetOpacity(float value)
        {
            value = SnapToStep(value, OpacityStep, _opacitySlider.minValue, _opacitySlider.maxValue);
            _opacitySlider.SetValueWithoutNotify(value);
            GameSettings.CrosshairOpacity = value;
            RefreshPreview();
            UpdateValueLabels();
        }

        private void ResetToDefault()
        {
            GameSettings.ResetCrosshair();
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_sizeSlider != null)
                _sizeSlider.SetValueWithoutNotify(GameSettings.CrosshairSize);
            if (_opacitySlider != null)
                _opacitySlider.SetValueWithoutNotify(GameSettings.CrosshairOpacity);

            RefreshChoiceBorders(_styles, GameSettings.CrosshairStyle);
            RefreshChoiceBorders(_colors, GameSettings.CrosshairColor);
            UpdateValueLabels();
            RefreshPreview();
        }

        private static void RefreshChoiceBorders(List<Choice> choices, int selected)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];
                if (choice.Border != null)
                {
                    choice.Border.color = choice.Index == selected
                        ? SelectedBorderColor
                        : choice.NormalBorderColor;
                }
            }
        }

        private void UpdateValueLabels()
        {
            if (_sizeValue != null)
                _sizeValue.text = GameSettings.CrosshairSize.ToString("0.0");
            if (_opacityValue != null)
                _opacityValue.text = $"{Mathf.RoundToInt(GameSettings.CrosshairOpacity * 100f)}%";
        }

        private void RefreshPreview()
        {
            if (_previewCrosshair == null)
                return;

            _previewCrosshair.sprite = CrosshairSettings.GetSprite(GameSettings.CrosshairStyle);
            _previewCrosshair.color = CrosshairSettings.GetColor(
                GameSettings.CrosshairColor, GameSettings.CrosshairOpacity);
            _previewCrosshair.preserveAspect = true;
            _previewCrosshair.rectTransform.localScale = Vector3.one;
            _previewCrosshair.rectTransform.sizeDelta =
                Vector2.one * CrosshairSettings.BaseSize * GameSettings.CrosshairSize;
        }

        private static void ConfigureSlider(Slider slider, float minimum, float maximum)
        {
            if (slider == null)
                return;

            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = false;
        }

        private static float SnapToStep(float value, float step, float minimum, float maximum)
        {
            return Mathf.Clamp(Mathf.Round(value / step) * step, minimum, maximum);
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            Transform target = FindChildRecursive(transform, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static T FindChildComponent<T>(Transform root, string objectName)
            where T : Component
        {
            Transform target = FindChildRecursive(root, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
