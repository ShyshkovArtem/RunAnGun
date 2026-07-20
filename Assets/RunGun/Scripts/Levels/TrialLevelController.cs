using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using RunGun.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Trial Level Controller")]
    public sealed class TrialLevelController : MonoBehaviour
    {
        private const string FinalPanelName = "FinalPanel";
        private const string LegacyFinalPanelName = "TrialFinalPanel";
        private const string PausePanelName = "TrialPausePanel";
        private const string LevelTextName = "LevelTxt";
        private const string LegacyLevelTextName = "Leveltxt";
        private const string MenuButtonName = "MenuBtn";
        private const string RetryButtonName = "RetryBtn";
        private const string ContinueButtonName = "ContinueBtn";
        private const string NextLevelButtonName = "NextLvlBtn";
        private const string MainMenuSceneName = "MainMenu";

        [Header("Level")]
        [SerializeField] private string levelDisplayName;
        [SerializeField] private string nextTrialSceneName;

        [Header("UI")]
        [SerializeField] private GameObject trialFinalPanel;
        [SerializeField] private GameObject trialPausePanel;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private Button pauseRetryButton;
        [SerializeField] private Button pauseContinueButton;

        [Header("Player Lock")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private LevelPlayerSpawner playerSpawner;
        [SerializeField] private LevelRunTimer runTimer;

        private bool _completed;
        private bool _paused;
        private readonly List<TMP_Text> _levelTexts = new();

        private void Awake()
        {
            AutoBindUi();
            CacheLevelTexts();

            if (autoFindPlayer)
                FindPlayerReferences();

            if (trialFinalPanel != null)
                trialFinalPanel.SetActive(false);

            if (trialPausePanel != null)
                trialPausePanel.SetActive(false);

            Time.timeScale = 1f;
            BindButtons();
            UpdateLevelText();
        }

        private void OnDestroy()
        {
            if (_paused)
                Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_completed)
                return;

            if (TutorialDialogueController.IsAnyDialoguePlaying)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        public void CompleteTrial()
        {
            TryCompleteTrial();
        }

        public bool TryCompleteTrial()
        {
            if (_completed || TutorialDialogueController.IsAnyDialoguePlaying)
                return false;

            SetPaused(false);
            LevelProgression.MarkCompleted(SceneManager.GetActiveScene().name);
            FindRunTimer();
            runTimer?.FinishRun();
            _completed = true;

            if (autoFindPlayer && (playerController == null || weaponController == null))
                FindPlayerReferences();

            SetPlayerLocked(true);
            UpdateLevelText();

            if (trialFinalPanel != null)
                trialFinalPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void RetryLevel()
        {
            Time.timeScale = 1f;
            _paused = false;
            _completed = false;

            if (autoFindPlayer && (playerController == null || weaponController == null || playerSpawner == null))
                FindPlayerReferences();

            if (trialFinalPanel != null)
                trialFinalPanel.SetActive(false);

            if (trialPausePanel != null)
                trialPausePanel.SetActive(false);

            SetPlayerLocked(false);
            ResetLevelSystems();

            if (playerSpawner != null)
            {
                playerSpawner.ResetRespawnPoint();
                playerSpawner.RespawnPlayer();
            }
            else if (playerController != null)
            {
                playerController.TeleportTo(playerController.transform.position, playerController.transform.rotation);
            }

            weaponController?.ResetForLevelRetry();
            UpdateLevelText();
            RestartPlayOnStartDialogues();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ContinueToNextLevel()
        {
            if (!string.IsNullOrWhiteSpace(nextTrialSceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(nextTrialSceneName);
                return;
            }

            int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
            if (nextBuildIndex >= 0 && nextBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(nextBuildIndex);
                return;
            }

            Debug.LogWarning($"{nameof(TrialLevelController)} on {name} has no next trial scene configured.", this);
        }

        public void GoToMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                Debug.LogError($"Scene '{MainMenuSceneName}' is not enabled in Build Settings.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuSceneName);
        }

        public void TogglePause()
        {
            if (TutorialDialogueController.IsAnyDialoguePlaying)
                return;

            SetPaused(!_paused);
        }

        public void ResumeLevel()
        {
            SetPaused(false);
        }

        private void AutoBindUi()
        {
            if (trialFinalPanel == null)
                trialFinalPanel = FindSceneObjectByName(FinalPanelName);

            if (trialFinalPanel == null)
                trialFinalPanel = FindSceneObjectByName(LegacyFinalPanelName);

            if (trialPausePanel == null)
                trialPausePanel = FindSceneObjectByName(PausePanelName);

            Transform panelTransform = trialFinalPanel != null ? trialFinalPanel.transform : transform;
            if (levelText == null)
                levelText = FindChildComponent<TMP_Text>(panelTransform, LevelTextName);

            if (levelText == null)
                levelText = FindChildComponent<TMP_Text>(panelTransform, LegacyLevelTextName);

            if (menuButton == null)
                menuButton = FindChildComponent<Button>(panelTransform, MenuButtonName);

            if (retryButton == null)
                retryButton = FindChildComponent<Button>(panelTransform, RetryButtonName);

            if (continueButton == null)
                continueButton = FindChildComponent<Button>(panelTransform, ContinueButtonName);

            if (continueButton == null)
                continueButton = FindChildComponent<Button>(panelTransform, NextLevelButtonName);

            Transform pausePanelTransform = trialPausePanel != null ? trialPausePanel.transform : null;
            if (pauseMenuButton == null)
                pauseMenuButton = FindChildComponent<Button>(pausePanelTransform, MenuButtonName);

            if (pauseRetryButton == null)
                pauseRetryButton = FindChildComponent<Button>(pausePanelTransform, RetryButtonName);

            if (pauseContinueButton == null)
                pauseContinueButton = FindChildComponent<Button>(pausePanelTransform, ContinueButtonName);
        }

        private void BindButtons()
        {
            BindButton(menuButton, GoToMenu);
            BindButton(retryButton, RetryLevel);
            BindButton(continueButton, ContinueToNextLevel);
            BindButton(pauseMenuButton, GoToMenu);
            BindButton(pauseRetryButton, RetryLevel);
            BindButton(pauseContinueButton, ResumeLevel);
        }

        private void UpdateLevelText()
        {
            if (_levelTexts.Count == 0)
                CacheLevelTexts();

            if (_levelTexts.Count == 0)
                return;

            string displayName = string.IsNullOrWhiteSpace(levelDisplayName)
                ? SceneManager.GetActiveScene().name
                : levelDisplayName;

            for (int i = 0; i < _levelTexts.Count; i++)
            {
                if (_levelTexts[i] != null)
                    _levelTexts[i].text = displayName;
            }
        }

        private void SetPlayerLocked(bool locked)
        {
            if (playerController != null)
                playerController.SetInputLocked(locked);

            if (weaponController != null)
                weaponController.SetInputLocked(locked);
        }

        private void ResetLevelSystems()
        {
            FindRunTimer();
            runTimer?.ResetRun();

            var runStartTriggers = FindSceneComponents<LevelRunStartTrigger>();
            for (int i = 0; i < runStartTriggers.Count; i++)
                runStartTriggers[i].ResetTrigger();

            var checkpoints = FindSceneComponents<LevelCheckpoint>();
            for (int i = 0; i < checkpoints.Count; i++)
                checkpoints[i].ResetCheckpoint();

            var finishTriggers = FindSceneComponents<TrialFinishTrigger>();
            for (int i = 0; i < finishTriggers.Count; i++)
                finishTriggers[i].ResetTrigger();

            var targetGroups = FindSceneComponents<AimTargetGroupController>();
            for (int i = 0; i < targetGroups.Count; i++)
                targetGroups[i].ResetForLevelRetry();

            var targetTriggers = FindSceneComponents<AimTargetGroupTrigger>();
            for (int i = 0; i < targetTriggers.Count; i++)
                targetTriggers[i].ResetTrigger();

            var movingPlatforms = FindSceneComponents<MovingPlatform>();
            for (int i = 0; i < movingPlatforms.Count; i++)
                movingPlatforms[i].ResetForLevelRetry();

            var breakables = FindSceneComponents<WeaponBreakable>();
            for (int i = 0; i < breakables.Count; i++)
                breakables[i].ResetBreakable();

            var dialogueControllers = FindSceneComponents<TutorialDialogueController>();
            for (int i = 0; i < dialogueControllers.Count; i++)
                dialogueControllers[i].ResetForLevelRetry();

            var dialogueTriggers = FindSceneComponents<TutorialDialogueTrigger>();
            for (int i = 0; i < dialogueTriggers.Count; i++)
                dialogueTriggers[i].ResetTrigger();
        }

        private void RestartPlayOnStartDialogues()
        {
            var dialogueControllers = FindSceneComponents<TutorialDialogueController>();
            for (int i = 0; i < dialogueControllers.Count; i++)
            {
                TutorialDialogueController controller = dialogueControllers[i];
                if (controller != null && controller.PlayOnStart)
                    controller.StartDialogue();
            }
        }

        private void SetPaused(bool paused)
        {
            if (_completed)
                paused = false;

            _paused = paused;

            if (autoFindPlayer && (playerController == null || weaponController == null))
                FindPlayerReferences();

            SetPlayerLocked(_paused || TutorialDialogueController.IsAnyDialoguePlaying);

            if (trialPausePanel != null)
                trialPausePanel.SetActive(_paused);

            if (_paused)
            {
                FindRunTimer();
                runTimer?.RefreshPauseUi();
            }

            Time.timeScale = _paused ? 0f : 1f;
            Cursor.lockState = _paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _paused;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void FindPlayerReferences()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (weaponController == null)
                weaponController = FindFirstObjectByType<PlayerWeaponController>();

            if (playerSpawner == null)
                playerSpawner = FindFirstObjectByType<LevelPlayerSpawner>();
        }

        private void FindRunTimer()
        {
            if (runTimer == null)
                runTimer = FindFirstObjectByType<LevelRunTimer>();
        }

        private void CacheLevelTexts()
        {
            _levelTexts.Clear();
            AddLevelText(levelText);

            var texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null)
                    continue;

                if (candidate.name != LevelTextName && candidate.name != LegacyLevelTextName)
                    continue;

                AddLevelText(candidate);
            }
        }

        private void AddLevelText(TMP_Text text)
        {
            if (text == null || _levelTexts.Contains(text))
                return;

            _levelTexts.Add(text);
        }

        private static T FindChildComponent<T>(Transform root, string childName) where T : Component
        {
            if (root == null)
                return null;

            Transform child = FindChildRecursive(root, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
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

        private static GameObject FindSceneObjectByName(string objectName)
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                    continue;

                return candidate.gameObject;
            }

            return null;
        }

        private static List<T> FindSceneComponents<T>() where T : Component
        {
            var results = new List<T>();
            var components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                    continue;

                results.Add(component);
            }

            return results;
        }
    }
}
