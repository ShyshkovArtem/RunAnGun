using System;
using ElmanGameDevTools.PlayerSystem;
using RunGun.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Tutorial Dialogue Controller")]
    public sealed class TutorialDialogueController : MonoBehaviour
    {
        [Serializable]
        public sealed class DialogueLine
        {
            public string speakerName;
            [TextArea(2, 5)] public string text;
            public GameObject speakerPosePrefab;
        }

        [Header("Dialogue")]
        [SerializeField] private DialogueLine[] lines;
        [SerializeField] private bool playOnStart;
        [SerializeField] private bool hidePoseOnEnd = true;

        [Header("Text Reveal")]
        [SerializeField] private bool useTypewriter = true;
        [SerializeField] private float charactersPerSecond = 45f;
        [SerializeField] private bool enterCompletesCurrentLine = true;

        [Header("UI")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueText;

        [Header("3D Speaker")]
        [SerializeField] private Transform poseSpawnAnchor;
        [SerializeField] private bool parentPoseToAnchor = true;
        [SerializeField] private Vector3 poseLocalPosition;
        [SerializeField] private Vector3 poseLocalRotation;
        [SerializeField] private Vector3 poseLocalScale = Vector3.one;

        [Header("Player Lock")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerWeaponController weaponController;

        private int _lineIndex;
        private bool _isPlaying;
        private float _nextAdvanceTime;
        private GameObject _spawnedPose;
        private int _visibleCharacters;
        private float _characterRevealProgress;
        private bool _lineFullyVisible = true;
        private static TutorialDialogueController _activeDialogue;

        public bool PlayOnStart => playOnStart;
        public bool IsPlaying => _isPlaying;
        public static bool IsAnyDialoguePlaying => _activeDialogue != null && _activeDialogue._isPlaying;

        private void Awake()
        {
            if (autoFindPlayer)
                FindPlayerReferences();

            SetDialogueVisible(false);
            ClearSpawnedPose();
        }

        private void Start()
        {
            if (playOnStart)
                StartDialogue();
        }

        private void OnDisable()
        {
            if (_activeDialogue == this)
            {
                SetPlayerLocked(false);
                _activeDialogue = null;
            }

            ClearSpawnedPose();
            SetDialogueVisible(false);
            _isPlaying = false;
        }

        private void Update()
        {
            if (!_isPlaying || Time.unscaledTime < _nextAdvanceTime)
                return;

            UpdateTextReveal();

            if (WasAdvancePressed())
                HandleAdvancePressed();
        }

        public void StartDialogue()
        {
            TryStartDialogue();
        }

        public bool TryStartDialogue()
        {
            if (lines == null || lines.Length == 0)
            {
                Debug.LogWarning($"{nameof(TutorialDialogueController)} on {name} has no dialogue lines.", this);
                return false;
            }

            if (autoFindPlayer && (playerController == null || weaponController == null))
                FindPlayerReferences();

            if (_activeDialogue != null && _activeDialogue != this)
                _activeDialogue.EndDialogue();

            ClearSpawnedPose();
            _activeDialogue = this;
            _isPlaying = true;
            _lineIndex = 0;
            _nextAdvanceTime = Time.unscaledTime + 0.15f;
            SetPlayerLocked(true);
            SetDialogueVisible(true);
            ShowLine(_lineIndex);
            return true;
        }

        public void EndDialogue()
        {
            if (!_isPlaying && _activeDialogue != this)
                return;

            _isPlaying = false;
            SetDialogueVisible(false);

            if (_activeDialogue == this)
            {
                SetPlayerLocked(false);
                _activeDialogue = null;
            }

            if (hidePoseOnEnd)
                ClearSpawnedPose();

            CompleteTextReveal();
        }

        public void ResetDialogue()
        {
            if (_activeDialogue == this)
            {
                SetPlayerLocked(false);
                _activeDialogue = null;
            }

            _isPlaying = false;
            _lineIndex = 0;
            _nextAdvanceTime = 0f;
            _visibleCharacters = 0;
            _characterRevealProgress = 0f;
            _lineFullyVisible = true;
            SetDialogueVisible(false);
            ClearSpawnedPose();
            CompleteTextReveal();
        }

        public void ResetForLevelRetry()
        {
            ResetDialogue();
        }

        private void HandleAdvancePressed()
        {
            if (useTypewriter && enterCompletesCurrentLine && !_lineFullyVisible)
            {
                CompleteTextReveal();
                _nextAdvanceTime = Time.unscaledTime + 0.05f;
                return;
            }

            ShowNextLine();
        }

        private void ShowNextLine()
        {
            _lineIndex++;
            if (_lineIndex >= lines.Length)
            {
                EndDialogue();
                return;
            }

            ShowLine(_lineIndex);
        }

        private void ShowLine(int index)
        {
            if (lines == null || index < 0 || index >= lines.Length)
                return;

            DialogueLine line = lines[index];
            if (speakerNameText != null)
                speakerNameText.text = line.speakerName;

            if (dialogueText != null)
                dialogueText.text = line.text;

            StartTextReveal();
            SpawnPose(line.speakerPosePrefab);
        }

        private void StartTextReveal()
        {
            if (dialogueText == null)
                return;

            dialogueText.ForceMeshUpdate();
            _visibleCharacters = useTypewriter ? 0 : dialogueText.textInfo.characterCount;
            _characterRevealProgress = _visibleCharacters;
            _lineFullyVisible = !useTypewriter;
            dialogueText.maxVisibleCharacters = useTypewriter ? 0 : int.MaxValue;
        }

        private void UpdateTextReveal()
        {
            if (!useTypewriter || _lineFullyVisible || dialogueText == null)
                return;

            dialogueText.ForceMeshUpdate();
            int totalCharacters = dialogueText.textInfo.characterCount;
            _characterRevealProgress += Mathf.Max(1f, charactersPerSecond) * Time.unscaledDeltaTime;
            _visibleCharacters = Mathf.Min(totalCharacters, Mathf.FloorToInt(_characterRevealProgress));
            dialogueText.maxVisibleCharacters = _visibleCharacters;

            if (_visibleCharacters >= totalCharacters)
                _lineFullyVisible = true;
        }

        private void CompleteTextReveal()
        {
            if (dialogueText == null)
                return;

            dialogueText.maxVisibleCharacters = int.MaxValue;
            _lineFullyVisible = true;
        }

        private void SetDialogueVisible(bool visible)
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(visible);
        }

        private void SetPlayerLocked(bool locked)
        {
            if (playerController != null)
                playerController.SetInputLocked(locked);

            if (weaponController != null)
                weaponController.SetInputLocked(locked);
        }

        private void SpawnPose(GameObject posePrefab)
        {
            ClearSpawnedPose();

            if (posePrefab == null)
                return;

            Transform anchor = poseSpawnAnchor != null ? poseSpawnAnchor : transform;
            _spawnedPose = Instantiate(posePrefab, anchor.position, anchor.rotation, parentPoseToAnchor ? anchor : null);

            if (parentPoseToAnchor)
            {
                _spawnedPose.transform.localPosition = poseLocalPosition;
                _spawnedPose.transform.localRotation = Quaternion.Euler(poseLocalRotation);
                _spawnedPose.transform.localScale = poseLocalScale;
            }
            else
            {
                _spawnedPose.transform.SetPositionAndRotation(
                    anchor.TransformPoint(poseLocalPosition),
                    anchor.rotation * Quaternion.Euler(poseLocalRotation));
                _spawnedPose.transform.localScale = poseLocalScale;
            }
        }

        private void ClearSpawnedPose()
        {
            if (_spawnedPose == null)
                return;

            _spawnedPose.SetActive(false);
            Destroy(_spawnedPose);
            _spawnedPose = null;
        }

        private void FindPlayerReferences()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (weaponController == null)
                weaponController = FindFirstObjectByType<PlayerWeaponController>();
        }

        private static bool WasAdvancePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }
    }
}
