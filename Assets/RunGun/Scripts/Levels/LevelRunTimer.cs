using System;
using TMPro;
using UnityEngine;
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
        private sealed class RankDefinition
        {
            [SerializeField] private string displayName;
            [SerializeField] private Sprite icon;
            [Min(0f)] [SerializeField] private float maximumTime;

            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Rank" : displayName;
            public Sprite Icon => icon;
            public float MaximumTime => maximumTime;

            public RankDefinition(string name, float time)
            {
                displayName = name;
                maximumTime = time;
            }

            public void SetMinimumTime(float minimum)
            {
                maximumTime = Mathf.Max(minimum, maximumTime);
            }
        }

        [Serializable]
        private sealed class RequirementRow
        {
            [SerializeField] private TMP_Text text;
            [SerializeField] private Image icon;

            public void Show(RankDefinition rank, string requirement)
            {
                if (text != null)
                    text.text = $"{rank.DisplayName}  {requirement}";

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

                RankDefinition rank = timer.GetRank(timer._bestTime);
                SetText(bestTimeText, FormatTime(timer._bestTime));
                SetText(bestRankText, rank.DisplayName);
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

            public void Show(LevelRunTimer timer)
            {
                requirements.Show(timer);

                if (timer._hasFinishedAttempt)
                {
                    RankDefinition attemptRank = timer.GetRank(timer._elapsedTime);
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
                    RankDefinition bestRank = timer.GetRank(timer._bestTime);
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

        private const string BestTimeKeyPrefix = "RunGun.BestTime.";

        [Header("Save Identity")]
        [Tooltip("Unique and permanent ID used to save this level's best time, for example FinalTrial or Level01.")]
        [SerializeField] private string levelId;

        [Header("Ranks (Fastest To Slowest)")]
        [SerializeField] private RankDefinition impossibleRank = new("Impossible", 30f);
        [SerializeField] private RankDefinition goldRank = new("Gold", 45f);
        [SerializeField] private RankDefinition silverRank = new("Silver", 60f);
        [SerializeField] private RankDefinition bronzeRank = new("Bronze", 0f);

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

        private string BestTimeKey => BestTimeKeyPrefix + levelId.Trim();

        private void Awake()
        {
            LoadBestTime();
            ResetRun();
        }

        private void Update()
        {
            if (_state != RunState.Running)
                return;

            _elapsedTime += Time.deltaTime;
            UpdateGameplayTimer();
        }

        private void OnValidate()
        {
            impossibleRank.SetMinimumTime(0f);
            goldRank.SetMinimumTime(impossibleRank.MaximumTime);
            silverRank.SetMinimumTime(goldRank.MaximumTime);
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

        private RankDefinition GetRank(float time)
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
            if (!string.IsNullOrWhiteSpace(levelId))
                return true;

            Debug.LogWarning($"{nameof(LevelRunTimer)} on {name} needs a unique Level Id to save records.", this);
            return false;
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static void SetIcon(Image target, Sprite sprite)
        {
            if (target == null)
                return;

            target.sprite = sprite;
            target.enabled = sprite != null;
        }
    }
}
