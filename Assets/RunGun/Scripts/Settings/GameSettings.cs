using System;
using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using RunGun.Levels;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunGun.Settings
{
    public enum GameAction
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Jump,
        Sprint,
        Crouch,
        Reload
    }

    public static class GameSettings
    {
        internal const int SupportedResolutionWidth = 1920;
        internal const int SupportedResolutionHeight = 1080;

        private const string Prefix = "RunGun.Settings.";
        private const float BaseLookSensitivity = 0.08f;
        private const float MinimumSensitivity = 0.1f;
        private const float MaximumSensitivity = 5f;
        private const int DefaultCrosshairStyle = 1;
        private const float DefaultCrosshairSize = 1.2f;
        private const float DefaultCrosshairOpacity = 1f;
        private const int DefaultCrosshairColor = 3;
        private static readonly Dictionary<GameAction, Key> Defaults = new()
        {
            { GameAction.MoveForward, Key.W },
            { GameAction.MoveBackward, Key.S },
            { GameAction.MoveLeft, Key.A },
            { GameAction.MoveRight, Key.D },
            { GameAction.Jump, Key.Space },
            { GameAction.Sprint, Key.LeftShift },
            { GameAction.Crouch, Key.LeftCtrl },
            { GameAction.Reload, Key.R }
        };

        public static float Sensitivity
        {
            get => SnapToStep(
                PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f),
                0.1f,
                MinimumSensitivity,
                MaximumSensitivity);
            set
            {
                PlayerPrefs.SetFloat(
                    Prefix + "Sensitivity",
                    SnapToStep(value, 0.1f, MinimumSensitivity, MaximumSensitivity));
                PlayerPrefs.Save();
                ApplyToLoadedPlayers();
            }
        }

        /// <summary>
        /// Converts the user-facing 0.1-5.0 multiplier to the small value expected
        /// by PlayerController. A setting of 1.0 preserves the controller's original
        /// 0.08 sensitivity.
        /// </summary>
        public static float PlayerLookSensitivity => BaseLookSensitivity * Sensitivity;

        public static float MusicVolume
        {
            get => SnapToStep(PlayerPrefs.GetFloat(Prefix + "MusicVolume", 0.8f), 0.05f, 0f, 1f);
            set
            {
                PlayerPrefs.SetFloat(Prefix + "MusicVolume", SnapToStep(value, 0.05f, 0f, 1f));
                PlayerPrefs.Save();
                ApplyAudio();
            }
        }

        public static float SfxVolume
        {
            get => SnapToStep(PlayerPrefs.GetFloat(Prefix + "SfxVolume", 1f), 0.05f, 0f, 1f);
            set
            {
                PlayerPrefs.SetFloat(Prefix + "SfxVolume", SnapToStep(value, 0.05f, 0f, 1f));
                PlayerPrefs.Save();
                ApplyAudio();
            }
        }

        public static int CrosshairStyle
        {
            get => Mathf.Clamp(
                PlayerPrefs.GetInt(Prefix + "Crosshair", DefaultCrosshairStyle), 0, 3);
            set
            {
                PlayerPrefs.SetInt(Prefix + "Crosshair", Mathf.Clamp(value, 0, 3));
                PlayerPrefs.Save();
                CrosshairSettings.Apply();
            }
        }

        public static float CrosshairSize
        {
            get => SnapToStep(
                PlayerPrefs.GetFloat(Prefix + "CrosshairSize", DefaultCrosshairSize),
                0.1f, 0.1f, 4f);
            set
            {
                PlayerPrefs.SetFloat(
                    Prefix + "CrosshairSize", SnapToStep(value, 0.1f, 0.1f, 4f));
                PlayerPrefs.Save();
                CrosshairSettings.Apply();
            }
        }

        public static float CrosshairOpacity
        {
            get => SnapToStep(
                PlayerPrefs.GetFloat(Prefix + "CrosshairOpacity", DefaultCrosshairOpacity),
                0.05f, 0f, 1f);
            set
            {
                PlayerPrefs.SetFloat(
                    Prefix + "CrosshairOpacity", SnapToStep(value, 0.05f, 0f, 1f));
                PlayerPrefs.Save();
                CrosshairSettings.Apply();
            }
        }

        public static int CrosshairColor
        {
            get => Mathf.Clamp(
                PlayerPrefs.GetInt(Prefix + "CrosshairColor", DefaultCrosshairColor), 0, 5);
            set
            {
                PlayerPrefs.SetInt(Prefix + "CrosshairColor", Mathf.Clamp(value, 0, 5));
                PlayerPrefs.Save();
                CrosshairSettings.Apply();
            }
        }

        public static void ResetCrosshair()
        {
            PlayerPrefs.SetInt(Prefix + "Crosshair", DefaultCrosshairStyle);
            PlayerPrefs.SetFloat(Prefix + "CrosshairSize", DefaultCrosshairSize);
            PlayerPrefs.SetFloat(Prefix + "CrosshairOpacity", DefaultCrosshairOpacity);
            PlayerPrefs.SetInt(Prefix + "CrosshairColor", DefaultCrosshairColor);
            PlayerPrefs.Save();
            CrosshairSettings.Apply();
        }

        public static Key GetKey(GameAction action)
        {
            int raw = PlayerPrefs.GetInt(Prefix + "Key." + action, (int)Defaults[action]);
            return Enum.IsDefined(typeof(Key), raw) ? (Key)raw : Defaults[action];
        }

        public static void SetKey(GameAction action, Key key)
        {
            PlayerPrefs.SetInt(Prefix + "Key." + action, (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetControls()
        {
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", 1f);
            foreach (KeyValuePair<GameAction, Key> binding in Defaults)
                PlayerPrefs.SetInt(Prefix + "Key." + binding.Key, (int)binding.Value);

            PlayerPrefs.Save();
            ApplyToLoadedPlayers();
        }

        public static bool IsPressed(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[GetKey(action)].isPressed;
        }

        public static bool WasPressedThisFrame(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[GetKey(action)].wasPressedThisFrame;
        }

        public static void ApplyDisplay(int width, int height, FullScreenMode mode)
        {
            Screen.SetResolution(width, height, mode);
            PlayerPrefs.SetInt(Prefix + "Width", width);
            PlayerPrefs.SetInt(Prefix + "Height", height);
            PlayerPrefs.SetInt(Prefix + "WindowMode", (int)mode);
            PlayerPrefs.Save();
        }

        public static void ApplySavedDisplay()
        {
            FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt(
                Prefix + "WindowMode", (int)FullScreenMode.FullScreenWindow);
            ApplyDisplay(SupportedResolutionWidth, SupportedResolutionHeight, mode);
        }

        public static void ApplyToLoadedPlayers()
        {
            PlayerController[] players = UnityEngine.Object.FindObjectsByType<PlayerController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                players[i].sensitivity = PlayerLookSensitivity;
        }

        public static void ApplyAudio()
        {
            AudioSettingsRuntime.EnsureExists();
            AudioSettingsRuntime.ApplyNow();
        }

        public static void SetSfxSourceVolume(AudioSource source, float baseVolume)
        {
            if (source == null)
                return;

            AudioSettingsRuntime.EnsureExists();
            AudioSettingsRuntime.SetSourceBaseVolume(source, Mathf.Clamp01(baseVolume));
        }

        private static float SnapToStep(float value, float step, float minimum, float maximum)
        {
            return Mathf.Clamp(Mathf.Round(value / step) * step, minimum, maximum);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterSceneLoad()
        {
            ApplySavedDisplay();
            ApplyToLoadedPlayers();
            ApplyAudio();
            CrosshairSettings.Apply();
        }
    }

    internal sealed class AudioSettingsRuntime : MonoBehaviour
    {
        private static AudioSettingsRuntime _instance;
        private readonly Dictionary<AudioSource, float> _baseVolumes = new();
        private float _nextRefresh;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            var host = new GameObject("GameSettingsRuntime");
            _instance = host.AddComponent<AudioSettingsRuntime>();
            DontDestroyOnLoad(host);
        }

        public static void ApplyNow()
        {
            if (_instance == null)
                return;
            _instance.RefreshSources();
        }

        public static void SetSourceBaseVolume(AudioSource source, float baseVolume)
        {
            if (_instance == null || source == null)
                return;

            _instance._baseVolumes[source] = baseVolume;
            source.volume = baseVolume * GameSettings.SfxVolume;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _baseVolumes.Clear();
            RefreshSources();
            CrosshairSettings.Apply();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;

            _nextRefresh = Time.unscaledTime + 1f;
            RefreshSources();
        }

        private void RefreshSources()
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (!_baseVolumes.TryGetValue(source, out float baseVolume))
                {
                    baseVolume = source.volume;
                    _baseVolumes[source] = baseVolume;
                }

                LevelMusicController music = source.GetComponent<LevelMusicController>();
                if (music != null)
                {
                    music.ApplyUserVolume();
                    continue;
                }

                source.volume = baseVolume * GameSettings.SfxVolume;
            }
        }
    }

    internal static class CrosshairSettings
    {
        internal const float BaseSize = 32f;
        private static readonly Dictionary<int, Sprite> Sprites = new();

        internal static readonly string[] StyleNames = { "DOT", "CLASSIC", "CIRCLE", "CROSS" };
        internal static readonly string[] ColorNames =
            { "WHITE", "RED", "CYAN", "GREEN", "YELLOW", "MAGENTA" };

        private static readonly Color[] Colors =
        {
            Color.white,
            new Color32(255, 55, 55, 255),
            new Color32(49, 216, 255, 255),
            new Color32(65, 230, 105, 255),
            new Color32(255, 215, 40, 255),
            new Color32(235, 50, 255, 255)
        };

        public static void Apply()
        {
            Image[] images = UnityEngine.Object.FindObjectsByType<Image>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Sprite sprite = GetSprite(GameSettings.CrosshairStyle);
            Color color = GetColor(GameSettings.CrosshairColor, GameSettings.CrosshairOpacity);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name != "Crosshair")
                    continue;

                images[i].sprite = sprite;
                images[i].color = color;
                images[i].preserveAspect = true;

                RectTransform rect = images[i].rectTransform;
                rect.localScale = Vector3.one;
                rect.sizeDelta = Vector2.one * BaseSize * GameSettings.CrosshairSize;

            }
        }

        internal static Color GetColor(int index, float opacity = 1f)
        {
            Color color = Colors[Mathf.Clamp(index, 0, Colors.Length - 1)];
            color.a = Mathf.Clamp01(opacity);
            return color;
        }

        internal static Sprite GetSprite(int style)
        {
            if (Sprites.TryGetValue(style, out Sprite cached))
                return cached;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"CrosshairStyle{style}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[size * size];
            Color32 white = new(255, 255, 255, 255);
            int center = size / 2;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool draw = style switch
                {
                    0 => Mathf.Abs(x - center) <= 1 && Mathf.Abs(y - center) <= 1,
                    1 => (Mathf.Abs(x - center) <= 1 && Mathf.Abs(y - center) >= 4 && Mathf.Abs(y - center) <= 10)
                        || (Mathf.Abs(y - center) <= 1 && Mathf.Abs(x - center) >= 4 && Mathf.Abs(x - center) <= 10),
                    2 => Mathf.Abs(Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) - 8f) < 1.3f,
                    _ => Mathf.Abs(x - center) <= 1 || Mathf.Abs(y - center) <= 1
                };
                pixels[y * size + x] = draw ? white : default;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite result = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            Sprites[style] = result;
            return result;
        }
    }
}
