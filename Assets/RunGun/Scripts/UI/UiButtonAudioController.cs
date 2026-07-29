using System;
using System.Collections.Generic;
using RunGun.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RunGun.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Button Audio Controller")]
    public sealed class UiButtonAudioController : MonoBehaviour
    {
        [Serializable]
        private sealed class SoundSettings
        {
            [SerializeField] private AudioClip clip;
            [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;
            [Range(0.1f, 3f)] [SerializeField] private float pitch = 1f;
            [Range(0f, 0.25f)] [SerializeField] private float pitchVariation;

            public AudioClip Clip => clip;
            public float Volume => volume;
            public float Pitch => pitch;
            public float PitchVariation => pitchVariation;
        }

        private sealed class ButtonBinding
        {
            private readonly UiButtonAudioController _owner;
            private readonly List<Graphic> _graphics = new();
            private readonly List<Color> _originalColors = new();
            private readonly UnityEngine.Events.UnityAction _clickAction;
            private readonly Transform _selectArrow;
            private readonly bool _selectArrowWasActive;
            private readonly Vector3 _originalScale;

            private float _pressVisualUntil;

            public ButtonBinding(UiButtonAudioController owner, Button button)
            {
                _owner = owner;
                Button = button;
                RectTransform = button.transform as RectTransform;
                _originalScale = button.transform.localScale;

                AddGraphic(button.GetComponentInChildren<TMP_Text>(true));
                Transform border = FindChildRecursive(button.transform, "Border");
                bool ownsPersistentSelectionBorder =
                    (button.GetComponentInParent<LevelSelectController>() != null &&
                     button.name.StartsWith("Line_1", StringComparison.Ordinal)) ||
                    (button.GetComponentInParent<CrosshairSettingsTab>() != null &&
                     (button.name.EndsWith("Style", StringComparison.Ordinal) ||
                      button.name.EndsWith("Color", StringComparison.Ordinal)));
                if (border != null && !ownsPersistentSelectionBorder)
                    AddGraphic(border.GetComponent<Graphic>());

                _selectArrow = FindChildRecursive(button.transform, "SelectArrow");
                _selectArrowWasActive = _selectArrow != null && _selectArrow.gameObject.activeSelf;

                _clickAction = HandleClick;
                button.onClick.AddListener(_clickAction);
            }

            public Button Button { get; }
            public RectTransform RectTransform { get; }

            public void Dispose()
            {
                if (Button != null)
                    Button.onClick.RemoveListener(_clickAction);

                Restore();
            }

            public void UpdateVisuals(bool hovered, bool selected, bool pointerPressed)
            {
                if (Button == null || !Button.gameObject.activeInHierarchy)
                    return;

                bool interactable = Button.IsInteractable();
                bool pressed = interactable && (pointerPressed || Time.unscaledTime < _pressVisualUntil);
                hovered &= interactable;
                selected &= interactable;
                float blend = 1f - Mathf.Exp(-_owner.TransitionSpeed * Time.unscaledDeltaTime);

                for (int i = 0; i < _graphics.Count; i++)
                {
                    Graphic graphic = _graphics[i];
                    if (graphic == null)
                        continue;

                    Color target = pressed
                        ? _owner.PressColor
                        : hovered
                            ? _owner.HoverColor
                            : selected ? _owner.SelectedColor : _originalColors[i];
                    graphic.color = Color.Lerp(graphic.color, target, blend);
                }

                float targetScale = pressed ? _owner.PressedScale : 1f;
                Vector3 scale = _originalScale * targetScale;
                Button.transform.localScale = Vector3.Lerp(Button.transform.localScale, scale, blend);

                if (_selectArrow != null)
                    _selectArrow.gameObject.SetActive(
                        _selectArrowWasActive || hovered || selected || pressed);
            }

            public void ShowPressFeedback()
            {
                _pressVisualUntil = Time.unscaledTime + _owner.PressFlashDuration;
            }

            private void HandleClick()
            {
                _owner.HandleButtonClick(this);
            }

            private void AddGraphic(Graphic graphic)
            {
                if (graphic == null || _graphics.Contains(graphic))
                    return;

                _graphics.Add(graphic);
                _originalColors.Add(graphic.color);
            }

            private void Restore()
            {
                for (int i = 0; i < _graphics.Count; i++)
                {
                    if (_graphics[i] != null)
                        _graphics[i].color = _originalColors[i];
                }

                if (Button != null)
                    Button.transform.localScale = _originalScale;

                if (_selectArrow != null)
                    _selectArrow.gameObject.SetActive(_selectArrowWasActive);
            }
        }

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private SoundSettings hoverSound = new();
        [SerializeField] private SoundSettings pressSound = new();
        [Min(0f)] [SerializeField] private float hoverCooldown = 0.03f;

        [Header("Visual Feedback")]
        [SerializeField] private Color hoverColor = new Color32(49, 216, 255, 255);
        [SerializeField] private Color selectedColor = new Color32(255, 52, 52, 255);
        [SerializeField] private Color pressColor = new Color32(255, 62, 70, 255);
        [Range(0.8f, 1f)] [SerializeField] private float pressedScale = 0.96f;
        [Min(0f)] [SerializeField] private float pressFlashDuration = 0.08f;
        [Min(0.1f)] [SerializeField] private float transitionSpeed = 18f;

        private readonly List<ButtonBinding> _bindings = new();
        private Canvas _canvas;
        private ButtonBinding _hoveredBinding;
        private ButtonBinding _selectedBinding;
        private ButtonBinding _persistentTabBinding;
        private ButtonBinding _lastPointerPressBinding;
        private int _lastPointerPressFrame = -10;
        private float _lastHoverTime = float.NegativeInfinity;

        private Color HoverColor => hoverColor.a > 0f ? hoverColor : new Color32(49, 216, 255, 255);
        private Color SelectedColor =>
            selectedColor.a > 0f ? selectedColor : new Color32(255, 52, 52, 255);
        private Color PressColor => pressColor.a > 0f ? pressColor : new Color32(255, 62, 70, 255);
        private float PressedScale => pressedScale > 0f ? pressedScale : 0.96f;
        private float PressFlashDuration => pressFlashDuration > 0f ? pressFlashDuration : 0.08f;
        private float TransitionSpeed => transitionSpeed > 0f ? transitionSpeed : 18f;

        private void OnEnable()
        {
            _canvas = GetComponentInParent<Canvas>();
            ConfigureAudioSource();
            BindButtons();
        }

        private void OnDisable()
        {
            ClearBindings();
        }

        private void Update()
        {
            UpdateMouseHover();

            Button selectedButton = null;
            if (EventSystem.current != null)
            {
                GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
                if (selectedObject != null)
                    selectedButton = selectedObject.GetComponent<Button>();
            }

            if (selectedButton != null)
            {
                _selectedBinding = FindBinding(selectedButton);
                if (IsSettingsTab(selectedButton))
                    _persistentTabBinding = _selectedBinding;
            }
            else if (_selectedBinding != null &&
                     (_selectedBinding.Button == null ||
                      !_selectedBinding.Button.gameObject.activeInHierarchy ||
                      !_selectedBinding.Button.IsInteractable()))
                _selectedBinding = null;

            if (_persistentTabBinding != null &&
                (_persistentTabBinding.Button == null ||
                 !_persistentTabBinding.Button.gameObject.activeInHierarchy ||
                 !_persistentTabBinding.Button.IsInteractable()))
            {
                _persistentTabBinding = null;
            }

            Mouse mouse = Mouse.current;
            bool mousePressed = mouse != null && mouse.leftButton.isPressed;
            if (_hoveredBinding != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _lastPointerPressBinding = _hoveredBinding;
                _lastPointerPressFrame = Time.frameCount;
                _hoveredBinding.ShowPressFeedback();
                PlayPress(_hoveredBinding.Button);
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                ButtonBinding binding = _bindings[i];
                bool hovered = binding == _hoveredBinding;
                bool selected = binding == _selectedBinding ||
                                binding == _persistentTabBinding;
                bool pointerPressed = binding == _hoveredBinding && mousePressed;
                binding.UpdateVisuals(hovered, selected, pointerPressed);
            }
        }

        public void ClearRetainedSelection(Button button)
        {
            if (button == null)
                return;

            if (_selectedBinding != null && _selectedBinding.Button == button)
                _selectedBinding = null;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == button.gameObject)
                eventSystem.SetSelectedGameObject(null);
        }

        private ButtonBinding FindBinding(Button button)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Button == button)
                    return _bindings[i];
            }

            return null;
        }

        private static bool IsSettingsTab(Button button)
        {
            return button.name is "DisplayBtn" or "AudioBtn" or "ControlsBtn" or "CrosshairBtn";
        }

        [ContextMenu("Refresh Button Bindings")]
        public void BindButtons()
        {
            ClearBindings();

            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                _bindings.Add(new ButtonBinding(this, buttons[i]));
        }

        private void UpdateMouseHover()
        {
            Mouse mouse = Mouse.current;
            ButtonBinding hovered = mouse != null ? FindButtonAt(mouse.position.ReadValue()) : null;
            if (hovered == _hoveredBinding)
                return;

            _hoveredBinding = hovered;
            if (hovered != null)
                PlayHover(hovered.Button);
        }

        private ButtonBinding FindButtonAt(Vector2 screenPosition)
        {
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                ButtonBinding binding = _bindings[i];
                if (binding.Button == null || !binding.Button.gameObject.activeInHierarchy ||
                    !binding.Button.IsInteractable() || binding.RectTransform == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(binding.RectTransform, screenPosition, eventCamera))
                    return binding;
            }

            return null;
        }

        private void PlayHover(Button button)
        {
            if (button == null || !button.IsInteractable())
                return;

            float now = Time.unscaledTime;
            if (now - _lastHoverTime < hoverCooldown)
                return;

            _lastHoverTime = now;
            Play(hoverSound);
        }

        private void PlayPress(Button button)
        {
            if (button != null && button.IsInteractable())
                Play(pressSound);
        }

        private void HandleButtonClick(ButtonBinding binding)
        {
            binding.ShowPressFeedback();

            bool alreadyPlayedForPointer = binding == _lastPointerPressBinding &&
                                           Time.frameCount - _lastPointerPressFrame <= 1;
            if (!alreadyPlayedForPointer)
                PlayPress(binding.Button);
        }

        private void ClearBindings()
        {
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].Dispose();

            _bindings.Clear();
            _hoveredBinding = null;
            _selectedBinding = null;
            _persistentTabBinding = null;
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;

            LoadClip(hoverSound);
            LoadClip(pressSound);
        }

        private static void LoadClip(SoundSettings sound)
        {
            if (sound?.Clip != null && sound.Clip.loadState == AudioDataLoadState.Unloaded)
                sound.Clip.LoadAudioData();
        }

        private void Play(SoundSettings sound)
        {
            if (audioSource == null || sound == null || sound.Clip == null)
                return;

            float variation = UnityEngine.Random.Range(-sound.PitchVariation, sound.PitchVariation);
            audioSource.pitch = Mathf.Clamp(sound.Pitch + variation, 0.1f, 3f);
            audioSource.PlayOneShot(sound.Clip, sound.Volume);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
