using System;
using RunGun.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Level Run Timer")]
    public sealed class LevelRunTimer : MonoBehaviour
    {
        public enum RunState
        {
            Waiting,
            Running,
            Finished
        }

        [Serializable]
        private sealed class RequirementRow
        {
            [SerializeField] private TMP_Text nameText;
            [FormerlySerializedAs("text")]
            [SerializeField] private TMP_Text requirementText;
            [SerializeField] private Image icon;

            public Sprite Icon => icon != null ? icon.sprite : null;

            public void AutoBind(Transform root, string rankName)
            {
                BindIfMissing(ref nameText, root, rankName + "Txt");
                BindIfMissing(ref requirementText, root, rankName + "TimerTxt");
                BindIfMissing(ref icon, root, rankName + "Icon");
            }

            public void Show(LevelTimingDefinition.RankDefinition rank, string requirement)
            {
                SetText(nameText, rank.DisplayName);
                SetText(requirementText, requirement);

                if (rank.Icon != null)
                    SetIcon(icon, rank.Icon);
            }
        }

        [Serializable]
        private sealed class RequirementsView
        {
            [SerializeField] private RequirementRow impossible = new();
            [SerializeField] private RequirementRow gold = new();
            [SerializeField] private RequirementRow silver = new();
            [SerializeField] private RequirementRow bronze = new();

            public void AutoBind(Transform root)
            {
                impossible.AutoBind(root, "Impossible");
                gold.AutoBind(root, "Gold");
                silver.AutoBind(root, "Silver");
                bronze.AutoBind(root, "Bronze");
            }

            public void ApplyIcons(LevelRunTimer timer)
            {
                timer.impossibleRank.SetIconIfMissing(impossible.Icon);
                timer.goldRank.SetIconIfMissing(gold.Icon);
                timer.silverRank.SetIconIfMissing(silver.Icon);
                timer.bronzeRank.SetIconIfMissing(bronze.Icon);
            }

            public void Show(LevelRunTimer timer)
            {
                impossible.Show(timer.impossibleRank, FormatTime(timer.impossibleRank.MaximumTime));
                gold.Show(timer.goldRank, FormatTime(timer.goldRank.MaximumTime));
                silver.Show(timer.silverRank, FormatTime(timer.silverRank.MaximumTime));
                bronze.Show(timer.bronzeRank, "Complete");
            }
        }

        [Serializable]
        private sealed class PauseView
        {
            [SerializeField] private TMP_Text bestTimeText;
            [SerializeField] private TMP_Text bestRankText;
            [SerializeField] private Image bestRankIcon;
            [SerializeField] private RequirementsView requirements = new();

            public void AutoBind(Transform root)
            {
                BindIfMissing(ref bestTimeText, root, "BestTimerTxt");
                BindIfMissing(ref bestRankText, root, "BestRankTxt");
                BindIfMissing(ref bestRankIcon, root, "BestRankIcon");
                requirements.AutoBind(root);
            }

            public void ApplyIcons(LevelRunTimer timer)
            {
                requirements.ApplyIcons(timer);
            }

            public void Show(LevelRunTimer timer)
            {
                requirements.Show(timer);

                if (!timer._hasBestTime)
                {
                    SetText(bestTimeText, "No record yet");
                    SetText(bestRankText, string.Empty);
                    SetIcon(bestRankIcon, null);
                    return;
                }

                LevelTimingDefinition.RankDefinition rank = timer.GetRank(timer._bestTime);
                SetText(bestTimeText, FormatTime(timer._bestTime));
                SetText(bestRankText, rank.DisplayName);
                SetTextColor(bestRankText, rank.TextColor);
                SetIcon(bestRankIcon, rank.Icon);
            }
        }

        [Serializable]
        private sealed class FinalView
        {
            [SerializeField] private TMP_Text attemptTimeText;
            [SerializeField] private TMP_Text attemptRankText;
            [SerializeField] private Image attemptRankIcon;
            [SerializeField] private TMP_Text bestTimeText;
            [SerializeField] private TMP_Text bestRankText;
            [SerializeField] private Image bestRankIcon;
            [SerializeField] private GameObject newRecordIndicator;
            [SerializeField] private RequirementsView requirements = new();

            public void AutoBind(Transform root)
            {
                BindIfMissing(ref attemptTimeText, root, "YourTimerTxt");
                BindIfMissing(ref attemptRankText, root, "TimerRankTxt");
                BindIfMissing(ref attemptRankIcon, root, "TimerRankIcon");
                BindIfMissing(ref bestTimeText, root, "YourBestTimerTxt");
                BindIfMissing(ref bestRankText, root, "BestRankTxt");
                BindIfMissing(ref bestRankIcon, root, "BestRankIcon");

                if (newRecordIndicator == null)
                {
                    Transform indicator = FindChildRecursive(root, "NewRecordBorder");
                    if (indicator == null)
                        indicator = FindChildRecursive(root, "NewRecordTxt");

                    if (indicator != null)
                        newRecordIndicator = indicator.gameObject;
                }

                requirements.AutoBind(root);
            }

            public void ApplyIcons(LevelRunTimer timer)
            {
                requirements.ApplyIcons(timer);
            }

            public void Show(LevelRunTimer timer)
            {
                requirements.Show(timer);

                if (timer._hasFinishedAttempt)
                {
                    LevelTimingDefinition.RankDefinition attemptRank = timer.GetRank(timer._elapsedTime);
                    SetText(attemptTimeText, FormatTime(timer._elapsedTime));
                    SetText(attemptRankText, attemptRank.DisplayName);
                    SetIcon(attemptRankIcon, attemptRank.Icon);
                }
                else
                {
                    SetText(attemptTimeText, "--:--.---");
                    SetText(attemptRankText, string.Empty);
                    SetIcon(attemptRankIcon, null);
                }

                if (timer._hasBestTime)
                {
                    LevelTimingDefinition.RankDefinition bestRank = timer.GetRank(timer._bestTime);
                    SetText(bestTimeText, FormatTime(timer._bestTime));
                    SetText(bestRankText, bestRank.DisplayName);
                    SetIcon(bestRankIcon, bestRank.Icon);
                }
                else
                {
                    SetText(bestTimeText, "No record yet");
                    SetText(bestRankText, string.Empty);
                    SetIcon(bestRankIcon, null);
                }

                if (newRecordIndicator != null)
                    newRecordIndicator.SetActive(timer._isNewRecord);
            }
        }

        private const string PausePanelName = "TrialPausePanel";
        private const string FinalPanelName = "FinalPanel";
        private const string LegacyFinalPanelName = "TrialFinalPanel";

        [Header("Save Identity")]
        [Tooltip("Unique and permanent ID used to save this level's best time, for example FinalTrial or Level01.")]
        [SerializeField] private string levelId;
        [Tooltip("Shared source for the save ID and rank requirements. Overrides the legacy fields below when assigned.")]
        [SerializeField] private LevelTimingDefinition timingDefinition;

        [Header("Ranks (Fastest To Slowest)")]
        [SerializeField] private LevelTimingDefinition.RankDefinition impossibleRank = new("Impossible", 30f, new Color32(255, 52, 52, 255));
        [SerializeField] private LevelTimingDefinition.RankDefinition goldRank = new("Gold", 45f, new Color32(255, 214, 66, 255));
        [SerializeField] private LevelTimingDefinition.RankDefinition silverRank = new("Silver", 60f, new Color32(217, 224, 231, 255));
        [SerializeField] private LevelTimingDefinition.RankDefinition bronzeRank = new("Bronze", 0f, new Color32(215, 122, 50, 255));

        [Header("Live UI")]
        [SerializeField] private TMP_Text gameplayTimerText;

        [Header("Pause Panel UI")]
        [SerializeField] private PauseView pauseView = new();

        [Header("Final Panel UI")]
        [SerializeField] private FinalView finalView = new();

        private RunState _state;
        private float _elapsedTime;
        private float _bestTime;
        private bool _hasBestTime;
        private bool _hasFinishedAttempt;
        private bool _isNewRecord;

        public RunState State => _state;
        public float ElapsedTime => _elapsedTime;
        public float BestTime => _bestTime;
        public bool HasBestTime => _hasBestTime;
        public bool IsNewRecord => _isNewRecord;

        private string EffectiveLevelId => timingDefinition != null ? timingDefinition.LevelId : levelId?.Trim();
        private string BestTimeKey => LevelProgression.BestTimeKeyPrefix + EffectiveLevelId;

        private void Awake()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(levelId) && IsIntroTrial(sceneName))
                levelId = sceneName;

            ApplyTimingDefinition();
            EnsureRankColors();
            AutoBindUi();

            LoadBestTime();
            ResetRun();
        }

        private void Start()
        {
            if (gameplayTimerText == null)
            {
                AutoBindUi();
                RefreshAllUi();
            }
        }

        private void Update()
        {
            if (_state != RunState.Running || TutorialDialogueController.IsAnyDialoguePlaying)
                return;

            _elapsedTime += Time.deltaTime;
            UpdateGameplayTimer();
        }

        private void OnValidate()
        {
            impossibleRank.SetMinimumTime(0f);
            goldRank.SetMinimumTime(impossibleRank.MaximumTime);
            silverRank.SetMinimumTime(goldRank.MaximumTime);
            EnsureRankColors();
        }

        public void StartRun()
        {
            if (_state != RunState.Waiting)
                return;

            _state = RunState.Running;
            _isNewRecord = false;
            RefreshAllUi();
        }

        public void FinishRun()
        {
            if (_state != RunState.Running)
                return;

            _state = RunState.Finished;
            _hasFinishedAttempt = true;
            _isNewRecord = !_hasBestTime || _elapsedTime < _bestTime;

            if (_isNewRecord)
            {
                _bestTime = _elapsedTime;
                _hasBestTime = true;
                SaveBestTime();
            }

            RefreshAllUi();
        }

        public void ResetRun()
        {
            _state = RunState.Waiting;
            _elapsedTime = 0f;
            _hasFinishedAttempt = false;
            _isNewRecord = false;
            RefreshAllUi();
        }

        public void RefreshPauseUi()
        {
            pauseView.Show(this);
        }

        private void RefreshAllUi()
        {
            UpdateGameplayTimer();
            pauseView.Show(this);
            finalView.Show(this);
        }

        private void UpdateGameplayTimer()
        {
            SetText(gameplayTimerText, FormatTime(_elapsedTime));
        }

        private void EnsureRankColors()
        {
            impossibleRank.SetColorIfMissing(new Color32(255, 52, 52, 255));
            goldRank.SetColorIfMissing(new Color32(255, 214, 66, 255));
            silverRank.SetColorIfMissing(new Color32(217, 224, 231, 255));
            bronzeRank.SetColorIfMissing(new Color32(215, 122, 50, 255));
        }

        private void AutoBindUi()
        {
            if (gameplayTimerText == null)
            {
                Transform timer = FindSceneTransformByName("Timer");
                if (timer != null)
                {
                    gameplayTimerText = timer.GetComponent<TMP_Text>();
                    timer.gameObject.SetActive(true);
                }
            }

            Transform pausePanel = FindSceneTransformByName(PausePanelName);
            if (pausePanel != null)
                pauseView.AutoBind(pausePanel);

            Transform finalPanel = FindSceneTransformByName(FinalPanelName);
            if (finalPanel == null)
                finalPanel = FindSceneTransformByName(LegacyFinalPanelName);

            if (finalPanel != null)
                finalView.AutoBind(finalPanel);

            pauseView.ApplyIcons(this);
            finalView.ApplyIcons(this);
        }

        private LevelTimingDefinition.RankDefinition GetRank(float time)
        {
            if (time <= impossibleRank.MaximumTime)
                return impossibleRank;

            if (time <= goldRank.MaximumTime)
                return goldRank;

            if (time <= silverRank.MaximumTime)
                return silverRank;

            return bronzeRank;
        }

        private void LoadBestTime()
        {
            if (!HasValidLevelId())
                return;

            _hasBestTime = PlayerPrefs.HasKey(BestTimeKey);
            if (_hasBestTime)
                _bestTime = PlayerPrefs.GetFloat(BestTimeKey);
        }

        private void SaveBestTime()
        {
            if (!HasValidLevelId())
                return;

            PlayerPrefs.SetFloat(BestTimeKey, _bestTime);
            PlayerPrefs.Save();
        }

        private bool HasValidLevelId()
        {
            if (!string.IsNullOrWhiteSpace(EffectiveLevelId))
                return true;

            Debug.LogWarning($"{nameof(LevelRunTimer)} on {name} needs a unique Level Id to save records.", this);
            return false;
        }

        private void ApplyTimingDefinition()
        {
            if (timingDefinition == null || timingDefinition.Ranks.Count < 4)
                return;

            impossibleRank = timingDefinition.Ranks[0];
            goldRank = timingDefinition.Ranks[1];
            silverRank = timingDefinition.Ranks[2];
            bronzeRank = timingDefinition.Ranks[3];
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }

        private static void SetText(TMP_Text target, string value)
        {
            GameLocalization.SetText(target, value);
        }

        private static void SetTextColor(TMP_Text target, Color color)
        {
            if (target != null)
                target.color = color;
        }

        private static void SetIcon(Image target, Sprite sprite)
        {
            if (target == null)
                return;

            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private static void BindIfMissing<T>(ref T target, Transform root, string childName) where T : Component
        {
            if (target != null || root == null)
                return;

            Transform child = FindChildRecursive(root, childName);
            if (child != null)
                target = child.GetComponent<T>();
        }

        private static Transform FindSceneTransformByName(string objectName)
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                    continue;

                return candidate;
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool IsIntroTrial(string sceneName)
        {
            return sceneName is "Trial_1" or "Trial_2" or "Trial_3" or "Trial_4" or "Trial_5";
        }

        private static void EnsureTimerForScene(Scene scene)
        {
            if (!scene.IsValid() || !IsIntroTrial(scene.name))
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<LevelRunTimer>(true) != null)
                    return;
            }

            var timerObject = new GameObject(nameof(LevelRunTimer));
            SceneManager.MoveGameObjectToScene(timerObject, scene);
            timerObject.AddComponent<LevelRunTimer>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureTimerForActiveScene()
        {
            EnsureTimerForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureTimerForScene(scene);
        }
    }
}
