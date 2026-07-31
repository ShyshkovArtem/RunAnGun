using System;
using System.Collections;
using System.Collections.Generic;
using RunGun.Levels;
using RunGun.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunGun.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/UI/Level Select Controller")]
    public sealed class LevelSelectController : MonoBehaviour
    {
        private sealed class LevelRow
        {
            private readonly LevelSelectController _owner;
            private readonly TMP_Text _numberText;
            private readonly TMP_Text _nameText;
            private readonly TMP_Text _timeText;
            private readonly Image _statusIcon;
            private readonly Image _border;
            private readonly List<Image> _selectionLines = new();
            private readonly Button _button;
            private LevelSelectCatalog.LevelDefinition _definition;

            public LevelRow(LevelSelectController owner, Transform root)
            {
                _owner = owner;
                Root = root.gameObject;
                _numberText = FindChildComponent<TMP_Text>(root, "LevelNumber");
                _nameText = FindChildComponent<TMP_Text>(root, "LevelName");
                _timeText = FindChildComponent<TMP_Text>(root, "CompleteTimeTxt");
                _statusIcon = FindChildComponent<Image>(root, "CompleteIcon");
                _border = FindChildComponent<Image>(root, "Border");

                _button = root.GetComponent<Button>();
                if (_button == null)
                    _button = root.gameObject.AddComponent<Button>();

                _button.transition = Selectable.Transition.None;
                _button.targetGraphic = root.GetComponent<Graphic>();
                _button.onClick.AddListener(Select);

                Image[] images = root.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];
                    if (image != null &&
                        image != _border &&
                        image != _statusIcon &&
                        image.gameObject != root.gameObject)
                    {
                        _selectionLines.Add(image);
                    }
                }
            }

            public GameObject Root { get; }
            public Button Button => _button;

            public void Show(LevelSelectCatalog.LevelDefinition definition)
            {
                _definition = definition;
                Root.SetActive(true);
                SetText(_numberText, definition.Number);
                SetText(_nameText, definition.DisplayName?.ToUpperInvariant());
                RefreshProgress();
            }

            public void Hide()
            {
                _definition = null;
                Root.SetActive(false);
            }

            public void SetSelected(bool selected)
            {
                Color color = selected ? _owner.selectedColor : _owner.normalColor;
                if (_border != null)
                    _border.color = color;

                for (int i = 0; i < _selectionLines.Count; i++)
                {
                    if (_selectionLines[i] != null)
                        _selectionLines[i].color = color;
                }
            }

            public void Dispose()
            {
                if (_button != null)
                    _button.onClick.RemoveListener(Select);
            }

            private void Select()
            {
                if (_definition != null)
                    _owner.SelectLevel(_definition);
            }

            private void RefreshProgress()
            {
                float bestTime = 0f;
                bool hasBestTime = _definition.TracksTime &&
                    LevelProgression.TryGetBestTime(_definition.BestTimeId, out bestTime);
                if (hasBestTime)
                {
                    if (_definition.UsesTimedRanks)
                    {
                        LevelTimingDefinition.RankDefinition rank = _owner.GetRank(_definition, bestTime);
                        SetStatus(rank?.Icon, Color.white);
                    }
                    else
                    {
                        bool hasCompletedLevel = LevelProgression.IsCompleted(_definition.SceneName);
                        SetStatus(hasCompletedLevel ? _owner.CheckSprite : _owner.CrossSprite,
                            hasCompletedLevel ? _owner.completeColor : _owner.incompleteColor);
                    }

                    SetText(_timeText, FormatTime(bestTime));
                    return;
                }

                bool completed = LevelProgression.IsCompleted(_definition.SceneName);
                if (_definition.TracksTime)
                {
                    SetStatus(_owner.CrossSprite, _owner.incompleteColor);
                    SetText(_timeText, "--:--.---");
                }
                else
                {
                    SetStatus(completed ? _owner.CheckSprite : _owner.CrossSprite,
                        completed ? _owner.completeColor : _owner.incompleteColor);
                    SetText(_timeText, string.Empty);
                }
            }

            private void SetStatus(Sprite sprite, Color color)
            {
                if (_statusIcon == null)
                    return;

                _statusIcon.sprite = sprite;
                _statusIcon.color = color;
                _statusIcon.enabled = sprite != null;
                _statusIcon.preserveAspect = true;
            }
        }

        private sealed class RequirementRow
        {
            private readonly GameObject _root;
            private readonly TMP_Text _name;
            private readonly TMP_Text _time;
            private readonly Image _icon;

            public RequirementRow(Transform root)
            {
                _root = root.gameObject;
                _name = FindChildComponent<TMP_Text>(root, "RankName");
                _time = FindChildComponent<TMP_Text>(root, "RankTime");
                _icon = FindChildComponent<Image>(root, "RankIcon");
            }

            public void Show(LevelTimingDefinition.RankDefinition rank, bool last)
            {
                _root.SetActive(true);
                SetText(_name, rank.DisplayName?.ToUpperInvariant());
                SetText(_time, last || rank.MaximumTime <= 0f ? "COMPLETE" : FormatTime(rank.MaximumTime));

                if (_icon != null)
                {
                    _icon.sprite = rank.Icon;
                    _icon.enabled = rank.Icon != null;
                    _icon.preserveAspect = true;
                }
            }

            public void Hide()
            {
                _root.SetActive(false);
            }
        }

        private const string CatalogResourceName = "LevelSelectCatalog";
        private const string MainMenuSceneName = "MainMenu";

        [Header("Selection Colors")]
        [SerializeField] private Color selectedColor = new Color32(255, 52, 52, 255);
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color completeColor = new Color32(66, 220, 120, 255);
        [SerializeField] private Color incompleteColor = new Color32(255, 75, 75, 255);

        private readonly List<LevelRow> _levelRows = new();
        private readonly List<RequirementRow> _requirementRows = new();
        private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _categoryBorders = new(StringComparer.OrdinalIgnoreCase);
        private LevelSelectCatalog _catalog;
        private LevelSelectCatalog.CategoryDefinition _selectedCategory;
        private LevelSelectCatalog.LevelDefinition _selectedLevel;
        private TMP_Text _categoryTitle;
        private TMP_Text _categoryProgress;
        private TMP_Text _detailName;
        private TMP_Text _detailDescription;
        private GameObject _requirementsPanel;
        private Button _startButton;
        private Button _backButton;
        private Button _openButton;
        private CanvasGroup _mainMenuButtonGroup;
        private float _mainMenuOriginalAlpha = 1f;
        private Sprite _checkSprite;
        private Sprite _crossSprite;
        private bool _initialized;

        private Sprite CheckSprite => _checkSprite ??= CreateStatusSprite(true);
        private Sprite CrossSprite => _crossSprite ??= CreateStatusSprite(false);

        public void Initialize(LevelSelectCatalog catalog)
        {
            if (_initialized)
                return;

            _catalog = catalog;
            if (_catalog == null)
            {
                Debug.LogError($"Missing Resources/{CatalogResourceName} asset.", this);
                return;
            }

            BindHierarchy();
            BindButtons();
            _initialized = true;

            SelectInitialCategory();
            RefreshButtonFeedbackBindings();
            SetMainMenuButtonsEnabled(true);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _levelRows.Count; i++)
                _levelRows[i].Dispose();

            foreach (KeyValuePair<string, Button> pair in _categoryButtons)
                pair.Value.onClick.RemoveAllListeners();

            _startButton?.onClick.RemoveListener(StartSelectedLevel);
            _backButton?.onClick.RemoveListener(Hide);
            _openButton?.onClick.RemoveListener(Show);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetMainMenuButtonsEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshSelection();
            StartCoroutine(SelectDefaultNextFrame());
        }

        public void Hide()
        {
            SetMainMenuButtonsEnabled(true);
            gameObject.SetActive(false);
            SelectGameObject(null);
        }

        private IEnumerator SelectDefaultNextFrame()
        {
            yield return null;

            if (!isActiveAndEnabled)
                yield break;

            Button firstLevelButton = GetFirstActiveLevelButton();

            if (firstLevelButton == null && _selectedCategory != null &&
                _categoryButtons.TryGetValue(_selectedCategory.Id, out Button categoryButton))
            {
                firstLevelButton = categoryButton;
            }

            SelectGameObject(firstLevelButton != null ? firstLevelButton.gameObject : null);
        }

        private IEnumerator SelectLevelAfterCategoryChange()
        {
            yield return null;

            if (!isActiveAndEnabled)
                yield break;

            Button firstLevelButton = GetFirstActiveLevelButton();
            if (firstLevelButton != null)
                SelectGameObject(firstLevelButton.gameObject);
        }

        private Button GetFirstActiveLevelButton()
        {
            for (int i = 0; i < _levelRows.Count; i++)
            {
                Button candidate = _levelRows[i].Button;
                if (candidate != null && candidate.IsActive() && candidate.IsInteractable())
                    return candidate;
            }

            return null;
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

        private void BindHierarchy()
        {
            Transform middlePanel = FindChildRecursive(transform, "MidlePanel");
            if (middlePanel != null)
            {
                for (int i = 0; i < middlePanel.childCount; i++)
                {
                    Transform child = middlePanel.GetChild(i);
                    if (child.name.StartsWith("Line_1", StringComparison.Ordinal))
                        _levelRows.Add(new LevelRow(this, child));
                }
            }

            Transform rightPanel = FindChildRecursive(transform, "RightPanel");
            _detailName = FindChildComponent<TMP_Text>(rightPanel, "LevelName");
            _detailDescription = FindChildComponent<TMP_Text>(rightPanel, "LevelDescription");
            _requirementsPanel = FindChildRecursive(rightPanel, "RequirementsPanel")?.gameObject;
            _startButton = FindChildComponent<Button>(rightPanel, "StartBtn");

            if (_requirementsPanel != null)
            {
                Transform requirements = _requirementsPanel.transform;
                for (int i = 0; i < requirements.childCount; i++)
                {
                    Transform child = requirements.GetChild(i);
                    if (child.name.StartsWith("Line_1", StringComparison.Ordinal))
                        _requirementRows.Add(new RequirementRow(child));
                }
            }

            Transform topPanel = FindChildRecursive(transform, "TopPanel");
            _categoryTitle = FindChildComponent<TMP_Text>(topPanel, "TrialsTxt");
            _categoryProgress = FindChildComponent<TMP_Text>(topPanel, "TrialsCountTxt");
            _backButton = FindChildComponent<Button>(topPanel, "BackButtom");

            Transform leftPanel = FindChildRecursive(transform, "LeftPanel");
            BindCategoryButton(leftPanel, "trials", "TrialsBtn");
            BindCategoryButton(leftPanel, "levels", "LevelsBtn");
            DisableComingSoonSelection(leftPanel);

            Scene scene = gameObject.scene;
            Transform open = FindSceneTransform(scene, "SelectBtn");
            _openButton = open != null ? open.GetComponent<Button>() : null;

            Transform mainButtonPanel = FindSceneTransform(scene, "ButtonPanel");
            if (mainButtonPanel != null)
            {
                _mainMenuButtonGroup = mainButtonPanel.GetComponent<CanvasGroup>();
                if (_mainMenuButtonGroup == null)
                    _mainMenuButtonGroup = mainButtonPanel.gameObject.AddComponent<CanvasGroup>();

                _mainMenuOriginalAlpha = _mainMenuButtonGroup.alpha;
            }
        }

        private void BindButtons()
        {
            foreach (KeyValuePair<string, Button> pair in _categoryButtons)
            {
                string categoryId = pair.Key;
                pair.Value.onClick.AddListener(() => SelectCategory(categoryId));
            }

            _startButton?.onClick.AddListener(StartSelectedLevel);
            _backButton?.onClick.AddListener(Hide);
            _openButton?.onClick.AddListener(Show);
        }

        private void BindCategoryButton(Transform root, string categoryId, string objectName)
        {
            Transform target = FindChildRecursive(root, objectName);
            if (target == null)
                return;

            Button button = target.GetComponent<Button>();
            if (button == null)
                button = target.gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.None;
            button.targetGraphic = target.GetComponent<Graphic>();
            _categoryButtons[categoryId] = button;

            Transform border = FindChildRecursiveStartingWith(target, "SelectedBorder");
            if (border != null)
                _categoryBorders[categoryId] = border.gameObject;
        }

        private static void DisableComingSoonSelection(Transform leftPanel)
        {
            Transform comingSoon = FindChildRecursive(leftPanel, "SoonNtm");
            if (comingSoon == null)
                return;

            Button button = comingSoon.GetComponent<Button>();
            if (button != null)
                button.interactable = false;

            Transform[] descendants = comingSoon.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == "SelectedBorder")
                    descendants[i].gameObject.SetActive(false);
            }
        }

        private void SetMainMenuButtonsEnabled(bool enabled)
        {
            if (_mainMenuButtonGroup == null)
                return;

            _mainMenuButtonGroup.interactable = enabled;
            _mainMenuButtonGroup.blocksRaycasts = enabled;
            _mainMenuButtonGroup.alpha = enabled ? _mainMenuOriginalAlpha : _mainMenuOriginalAlpha * 0.3f;
        }

        private void SelectInitialCategory()
        {
            if (!SelectCategory("trials") && _catalog.Categories.Count > 0)
                SelectCategory(_catalog.Categories[0].Id);
        }

        private bool SelectCategory(string categoryId)
        {
            LevelSelectCatalog.CategoryDefinition category = FindCategory(categoryId);
            if (category == null)
                return false;

            _selectedCategory = category;
            SetText(_categoryTitle, category.DisplayName?.ToUpperInvariant());

            foreach (KeyValuePair<string, Button> pair in _categoryButtons)
            {
                bool selected = string.Equals(
                    pair.Key, category.Id, StringComparison.OrdinalIgnoreCase);

                TMP_Text label = pair.Value.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.color = selected ? selectedColor : normalColor;

                if (_categoryBorders.TryGetValue(pair.Key, out GameObject border))
                {
                    border.SetActive(selected);
                    Image borderImage = border.GetComponent<Image>();
                    if (borderImage != null)
                        borderImage.color = selected ? selectedColor : normalColor;
                }
            }

            for (int i = 0; i < _levelRows.Count; i++)
            {
                if (i < category.Levels.Count)
                    _levelRows[i].Show(category.Levels[i]);
                else
                    _levelRows[i].Hide();
            }

            if (category.Levels.Count > 0)
                SelectLevel(category.Levels[0]);
            else
                ShowEmptyCategory();

            UpdateCategoryProgress();

            if (_initialized && isActiveAndEnabled)
                StartCoroutine(SelectLevelAfterCategoryChange());

            return true;
        }

        private void SelectLevel(LevelSelectCatalog.LevelDefinition level)
        {
            _selectedLevel = level;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selectedLevel == null)
                return;

            for (int i = 0; i < _levelRows.Count; i++)
            {
                bool selected = i < _selectedCategory.Levels.Count &&
                                ReferenceEquals(_selectedCategory.Levels[i], _selectedLevel);
                _levelRows[i].SetSelected(selected);
            }

            SetText(_detailName, _selectedLevel.DisplayName?.ToUpperInvariant());
            SetText(_detailDescription, _selectedLevel.Description);
            _startButton.interactable = !string.IsNullOrWhiteSpace(_selectedLevel.SceneName);
            ShowRequirements(_selectedLevel);
        }

        private void ShowRequirements(LevelSelectCatalog.LevelDefinition level)
        {
            bool show = level.UsesTimedRanks && level.Ranks.Count > 0;
            if (_requirementsPanel != null)
                _requirementsPanel.SetActive(show);

            for (int i = 0; i < _requirementRows.Count; i++)
            {
                if (show && i < level.Ranks.Count)
                    _requirementRows[i].Show(level.Ranks[i], i == level.Ranks.Count - 1);
                else
                    _requirementRows[i].Hide();
            }
        }

        private void ShowEmptyCategory()
        {
            _selectedLevel = null;
            SetText(_detailName, "NO LEVELS YET");
            SetText(_detailDescription, "COMING SOON");
            if (_requirementsPanel != null)
                _requirementsPanel.SetActive(false);
            if (_startButton != null)
                _startButton.interactable = false;
            SetText(_categoryProgress, "0 / 0");
        }

        private void UpdateCategoryProgress()
        {
            if (_selectedCategory == null)
                return;

            int completed = 0;
            for (int i = 0; i < _selectedCategory.Levels.Count; i++)
            {
                LevelSelectCatalog.LevelDefinition level = _selectedCategory.Levels[i];
                bool hasTimedResult = level.TracksTime &&
                                      LevelProgression.TryGetBestTime(level.BestTimeId, out _);
                if (hasTimedResult || LevelProgression.IsCompleted(level.SceneName))
                    completed++;
            }

            SetText(_categoryProgress, $"{completed} / {_selectedCategory.Levels.Count}");
        }

        private void StartSelectedLevel()
        {
            if (_selectedLevel == null || string.IsNullOrWhiteSpace(_selectedLevel.SceneName))
                return;

            if (!Application.CanStreamedLevelBeLoaded(_selectedLevel.SceneName))
            {
                Debug.LogError($"Level '{_selectedLevel.SceneName}' is not enabled in Build Settings.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(_selectedLevel.SceneName);
        }

        private LevelSelectCatalog.CategoryDefinition FindCategory(string id)
        {
            for (int i = 0; i < _catalog.Categories.Count; i++)
            {
                LevelSelectCatalog.CategoryDefinition category = _catalog.Categories[i];
                if (string.Equals(category.Id, id, StringComparison.OrdinalIgnoreCase))
                    return category;
            }

            return null;
        }

        private LevelTimingDefinition.RankDefinition GetRank(LevelSelectCatalog.LevelDefinition level, float time)
        {
            for (int i = 0; i < level.Ranks.Count; i++)
            {
                LevelTimingDefinition.RankDefinition rank = level.Ranks[i];
                if (rank.MaximumTime <= 0f || time <= rank.MaximumTime)
                    return rank;
            }

            return level.Ranks.Count > 0 ? level.Ranks[level.Ranks.Count - 1] : null;
        }

        private void RefreshButtonFeedbackBindings()
        {
            UiButtonAudioController feedback = GetComponentInParent<UiButtonAudioController>();
            feedback?.BindButtons();
        }

        private static Sprite CreateStatusSprite(bool check)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = check ? "RuntimeCheckIcon" : "RuntimeCrossIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 0);

            if (check)
            {
                DrawLine(pixels, size, 5, 15, 13, 7, 3);
                DrawLine(pixels, size, 13, 7, 28, 25, 3);
            }
            else
            {
                DrawLine(pixels, size, 7, 7, 25, 25, 3);
                DrawLine(pixels, size, 25, 7, 7, 25, 3);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 0f : step / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int oy = -thickness; oy <= thickness; oy++)
                {
                    for (int ox = -thickness; ox <= thickness; ox++)
                    {
                        int px = x + ox;
                        int py = y + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                            pixels[py * size + px] = new Color32(255, 255, 255, 255);
                    }
                }
            }
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }

        private static void SetText(TMP_Text text, string value)
        {
            GameLocalization.SetText(text, value ?? string.Empty);
        }

        private static T FindChildComponent<T>(Transform root, string childName) where T : Component
        {
            Transform child = FindChildRecursive(root, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

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

        private static Transform FindChildRecursiveStartingWith(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith(childName, StringComparison.Ordinal))
                    return child;

                Transform found = FindChildRecursiveStartingWith(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform root = roots[i].transform;
                if (root.name == objectName)
                    return root;

                Transform found = FindChildRecursive(root, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void SetupScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MainMenuSceneName)
                return;

            Transform panel = FindSceneTransform(scene, "LevelSelectPanel");
            if (panel == null)
                return;

            LevelSelectController controller = panel.GetComponent<LevelSelectController>();
            if (controller == null)
                controller = panel.gameObject.AddComponent<LevelSelectController>();

            controller.Initialize(Resources.Load<LevelSelectCatalog>(CatalogResourceName));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupActiveScene()
        {
            SetupScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupScene(scene);
        }
    }
}
