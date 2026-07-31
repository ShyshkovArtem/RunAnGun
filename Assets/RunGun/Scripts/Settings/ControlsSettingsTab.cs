using System;
using System.Collections.Generic;
using RunGun.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityAction = UnityEngine.Events.UnityAction;

namespace RunGun.Settings
{
    /// <summary>
    /// Connects the manually authored Controls tab to sensitivity and key-binding settings.
    /// UI objects are discovered once by their normalized hierarchy names.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Controls Settings Tab")]
    public sealed class ControlsSettingsTab : MonoBehaviour
    {
        private const float MinimumSensitivity = 0.1f;
        private const float MaximumSensitivity = 5f;
        private const float SensitivityStep = 0.1f;

        private static readonly (GameAction Action, string RowName, string Label)[] BindingDefinitions =
        {
            (GameAction.MoveForward, "MoveForwardBinding", "MOVE FORWARD"),
            (GameAction.MoveBackward, "MoveBackwardBinding", "MOVE BACKWARD"),
            (GameAction.MoveLeft, "MoveLeftBinding", "MOVE LEFT"),
            (GameAction.MoveRight, "MoveRightBinding", "MOVE RIGHT"),
            (GameAction.Jump, "JumpBinding", "JUMP"),
            (GameAction.Sprint, "SprintBinding", "SPRINT"),
            (GameAction.Crouch, "CrouchBinding", "CROUCH / SLIDE"),
            (GameAction.Reload, "ReloadBinding", "RELOAD")
        };

        private sealed class Binding
        {
            public GameAction Action;
            public Button Button;
            public TMP_Text KeyText;
            public UnityAction ClickAction;
        }

        private readonly List<Binding> _bindings = new();
        private Slider _sensitivitySlider;
        private TMP_Text _sensitivityValue;
        private Button _resetButton;
        private UiButtonAudioController _buttonFeedback;
        private Binding _pendingBinding;
        private int _rebindStartedFrame;
        private bool _initialized;

        private void OnEnable()
        {
            if (!_initialized)
                InitializeHierarchy();

            ConfigureSensitivity();
            RefreshBindingLabels();
            AddListeners();
        }

        private void OnDisable()
        {
            RemoveListeners();
            CancelRebind();
        }

        private void Update()
        {
            if (_pendingBinding == null || Time.frameCount <= _rebindStartedFrame)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.anyKey.wasPressedThisFrame)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelRebind();
                return;
            }

            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (!keyControl.wasPressedThisFrame)
                    continue;

                ApplyBinding(_pendingBinding, keyControl.keyCode);
                return;
            }
        }

        private void InitializeHierarchy()
        {
            _sensitivitySlider = FindComponent<Slider>("SensitivitySlider");
            _sensitivityValue = FindComponent<TMP_Text>("SensitivityValue");
            _resetButton = FindComponent<Button>("ResetButton");
            _buttonFeedback = GetComponentInParent<UiButtonAudioController>();

            for (int i = 0; i < BindingDefinitions.Length; i++)
            {
                var definition = BindingDefinitions[i];
                Transform row = FindChildRecursive(transform, definition.RowName);
                if (row == null)
                {
                    Debug.LogWarning($"Controls tab is missing '{definition.RowName}'.", this);
                    continue;
                }

                // The row is only a visual container. Only its KeyButton should
                // receive pointer, navigation, and submit events.
                Button rowButton = row.GetComponent<Button>();
                if (rowButton != null)
                {
                    rowButton.transition = Selectable.Transition.None;
                    rowButton.interactable = false;
                }

                Transform labelObject = FindChildRecursive(row, "ActionLabel");
                TMP_Text label = labelObject != null ? labelObject.GetComponent<TMP_Text>() : null;
                GameLocalization.SetText(label, definition.Label);

                Transform keyObject = FindChildRecursive(row, "KeyButton");
                Button button = keyObject != null ? keyObject.GetComponent<Button>() : null;
                TMP_Text keyText = keyObject != null ? keyObject.GetComponent<TMP_Text>() : null;
                if (button == null || keyText == null)
                {
                    Debug.LogWarning($"Controls binding '{definition.RowName}' needs a KeyButton.", row);
                    continue;
                }

                var binding = new Binding
                {
                    Action = definition.Action,
                    Button = button,
                    KeyText = keyText
                };
                binding.ClickAction = () => BeginRebind(binding);
                _bindings.Add(binding);
            }

            _initialized = true;
        }

        private void ConfigureSensitivity()
        {
            if (_sensitivitySlider == null)
                return;

            _sensitivitySlider.minValue = MinimumSensitivity;
            _sensitivitySlider.maxValue = MaximumSensitivity;
            _sensitivitySlider.wholeNumbers = false;
            _sensitivitySlider.SetValueWithoutNotify(GameSettings.Sensitivity);
            UpdateSensitivityLabel(GameSettings.Sensitivity);
        }

        private void AddListeners()
        {
            if (_sensitivitySlider != null)
                _sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
            if (_resetButton != null)
                _resetButton.onClick.AddListener(ResetToDefaults);

            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Button.onClick.AddListener(_bindings[i].ClickAction);
        }

        private void RemoveListeners()
        {
            if (_sensitivitySlider != null)
                _sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
            if (_resetButton != null)
                _resetButton.onClick.RemoveListener(ResetToDefaults);

            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding binding = _bindings[i];
                if (binding.Button != null)
                    binding.Button.onClick.RemoveListener(binding.ClickAction);
            }
        }

        private void HandleSensitivityChanged(float value)
        {
            value = Mathf.Clamp(
                Mathf.Round(value / SensitivityStep) * SensitivityStep,
                MinimumSensitivity,
                MaximumSensitivity);
            _sensitivitySlider.SetValueWithoutNotify(value);
            GameSettings.Sensitivity = value;
            UpdateSensitivityLabel(value);
        }

        private void UpdateSensitivityLabel(float value)
        {
            if (_sensitivityValue == null)
                return;

            _sensitivityValue.text = value.ToString("0.0");
        }

        private void ResetToDefaults()
        {
            CancelRebind();
            GameSettings.ResetControls();
            ConfigureSensitivity();
            RefreshBindingLabels();
        }

        private void BeginRebind(Binding binding)
        {
            if (_pendingBinding != null)
                RefreshBindingLabel(_pendingBinding);

            _pendingBinding = binding;
            _rebindStartedFrame = Time.frameCount;
            binding.KeyText.text = "PRESS A KEY";

            // Prevent Space or Enter from also submitting the focused UI button
            // while those keys are being captured as the new binding.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void ApplyBinding(Binding binding, Key key)
        {
            if (key == Key.None)
                return;

            Key previousKey = GameSettings.GetKey(binding.Action);
            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding other = _bindings[i];
                if (other.Action != binding.Action && GameSettings.GetKey(other.Action) == key)
                {
                    GameSettings.SetKey(other.Action, previousKey);
                    break;
                }
            }

            GameSettings.SetKey(binding.Action, key);
            _pendingBinding = null;
            ClearBindingSelection(binding);
            RefreshBindingLabels();
        }

        private void CancelRebind()
        {
            if (_pendingBinding == null)
                return;

            Binding cancelled = _pendingBinding;
            _pendingBinding = null;
            ClearBindingSelection(cancelled);
            RefreshBindingLabel(cancelled);
        }

        private void ClearBindingSelection(Binding binding)
        {
            if (_buttonFeedback != null && binding?.Button != null)
                _buttonFeedback.ClearRetainedSelection(binding.Button);
        }

        private void RefreshBindingLabels()
        {
            for (int i = 0; i < _bindings.Count; i++)
                RefreshBindingLabel(_bindings[i]);
        }

        private static void RefreshBindingLabel(Binding binding)
        {
            if (binding.KeyText != null)
                binding.KeyText.text = FormatKey(GameSettings.GetKey(binding.Action));
        }

        private static string FormatKey(Key key)
        {
            return key switch
            {
                Key.LeftCtrl => "LEFT CTRL",
                Key.RightCtrl => "RIGHT CTRL",
                Key.LeftShift => "LEFT SHIFT",
                Key.RightShift => "RIGHT SHIFT",
                Key.LeftAlt => "LEFT ALT",
                Key.RightAlt => "RIGHT ALT",
                Key.Space => "SPACE",
                Key.Enter => "ENTER",
                Key.Backspace => "BACKSPACE",
                _ => SplitPascalCase(key.ToString()).ToUpperInvariant()
            };
        }

        private static string SplitPascalCase(string value)
        {
            for (int i = value.Length - 1; i > 0; i--)
            {
                if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                    value = value.Insert(i, " ");
            }

            return value;
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            Transform child = FindChildRecursive(transform, objectName);
            return child != null ? child.GetComponent<T>() : null;
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
