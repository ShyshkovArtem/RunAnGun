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
        private const string FinalPanelName = "TrialFinalPanel";
        private const string PausePanelName = "TrialPausePanel";
        private const string LevelTextName = "LevelTxt";
        private const string LegacyLevelTextName = "Leveltxt";
        private const string MenuButtonName = "MenuBtn";
        private const string RetryButtonName = "RetryBtn";
        private const string ContinueButtonName = "ContinueBtn";

        [Header("Level")]
        [SerializeField] private string levelDisplayName;
        [SerializeField] private string menuSceneName;
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

        private bool _completed;
        private bool _paused;

        private void Awake()
        {
            AutoBindUi();

            if (autoFindPlayer)
                FindPlayerReferences();

            if (trialFinalPanel != null)
                trialFinalPanel.SetActive(false);

            if (trialPausePanel != null)
                trialPausePanel.SetActive(false);

            Time.timeScale = 1f;
            BindButtons();
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

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                TogglePause();
        }

        public void CompleteTrial()
        {
            if (_completed)
                return;

            SetPaused(false);
            _completed = true;

            if (autoFindPlayer && (playerController == null || weaponController == null))
                FindPlayerReferences();

            SetPlayerLocked(true);
            UpdateLevelText();

            if (trialFinalPanel != null)
                trialFinalPanel.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void RetryLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            if (string.IsNullOrWhiteSpace(menuSceneName))
            {
                Debug.LogWarning($"{nameof(TrialLevelController)} on {name} has no menu scene configured yet.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        public void TogglePause()
        {
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
            if (levelText == null)
                return;

            levelText.text = string.IsNullOrWhiteSpace(levelDisplayName)
                ? SceneManager.GetActiveScene().name
                : levelDisplayName;
        }

        private void SetPlayerLocked(bool locked)
        {
            if (playerController != null)
                playerController.SetInputLocked(locked);

            if (weaponController != null)
                weaponController.SetInputLocked(locked);
        }

        private void SetPaused(bool paused)
        {
            if (_completed)
                paused = false;

            _paused = paused;

            if (autoFindPlayer && (playerController == null || weaponController == null))
                FindPlayerReferences();

            SetPlayerLocked(_paused);

            if (trialPausePanel != null)
                trialPausePanel.SetActive(_paused);

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
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                    continue;

                GameObject gameObject = candidate.gameObject;
                if (!gameObject.scene.IsValid())
                    continue;

                return gameObject;
            }

            return null;
        }
    }
}
