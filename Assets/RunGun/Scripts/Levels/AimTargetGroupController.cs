using System;
using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
using UnityEngine.Events;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Aim Target Group Controller")]
    public sealed class AimTargetGroupController : MonoBehaviour
    {
        public enum ChallengeState
        {
            Idle,
            Running,
            Completed
        }

        [Header("Targets")]
        [SerializeField] private bool autoCollectChildTargets = true;
        [SerializeField] private List<ShootableAimTarget> targets = new();
        [SerializeField] private bool startOnAwake;
        [SerializeField] private bool hideTargetsUntilStarted = true;
        [SerializeField] private bool completeOnce = true;

        [Header("Start Actions")]
        [SerializeField] private GameObject[] activateOnStart;
        [SerializeField] private GameObject[] deactivateOnStart;
        [SerializeField] private MovingPlatform[] movingPlatformsToStart;

        [Header("Complete Actions")]
        [SerializeField] private GameObject[] activateOnComplete;
        [SerializeField] private GameObject[] deactivateOnComplete;
        [SerializeField] private MovingPlatform[] movingPlatformsToStartOnComplete;
        [SerializeField] private UnityEvent completed = new();

        [Header("Fail Actions")]
        [SerializeField] private MovingPlatform[] failWhenThesePlatformsFinish;
        [SerializeField] private MovingPlatform[] movingPlatformsToResetOnFail;
        [SerializeField] private bool failWhenPlayerRespawns = true;
        [SerializeField] private bool respawnPlayerOnFail = true;
        [SerializeField] private LevelPlayerSpawner playerSpawner;
        [SerializeField] private GameObject[] activateOnFail;
        [SerializeField] private GameObject[] deactivateOnFail;
        [SerializeField] private UnityEvent failed = new();

        private readonly HashSet<MovingPlatform> _armedFailPlatforms = new();
        private ChallengeState _state = ChallengeState.Idle;
        private int _remainingTargets;
        private float _failAllowedTime;

        public ChallengeState State => _state;
        public bool IsStarted => _state == ChallengeState.Running;
        public bool IsCompleted => _state == ChallengeState.Completed;
        public bool CanStart => _state == ChallengeState.Idle || (_state == ChallengeState.Completed && !completeOnce);
        public UnityEvent Completed => completed;
        public UnityEvent Failed => failed;
        public event Action GroupCompleted;
        public event Action GroupFailed;

        private void Awake()
        {
            RefreshTargets();
            ResetToIdleState();

            if (startOnAwake)
                StartGroup();
        }

        private void OnEnable()
        {
            SubscribeFailPlatforms();
            SubscribePlayerSpawner();
        }

        private void OnDisable()
        {
            UnsubscribeFailPlatforms();
            UnsubscribePlayerSpawner();
        }

        public bool StartGroup()
        {
            if (!CanStart)
                return false;

            ResetAttemptObjects();
            RefreshTargets();

            _state = ChallengeState.Running;
            _remainingTargets = targets.Count;
            _failAllowedTime = Time.time + 0.15f;
            ArmFailPlatforms();

            SetObjectsActive(activateOnStart, true);
            SetObjectsActive(deactivateOnStart, false);

            ResetTargets(false);
            SetTargetsActive(true);

            StartMovingPlatforms(movingPlatformsToStart);

            if (_remainingTargets == 0)
                CompleteGroup();

            return true;
        }

        public void NotifyTargetShot(ShootableAimTarget target)
        {
            if (_state != ChallengeState.Running || target == null || !targets.Contains(target))
                return;

            _remainingTargets = Mathf.Max(0, _remainingTargets - 1);
            if (_remainingTargets == 0)
                CompleteGroup();
        }

        public void CompleteGroup()
        {
            if (_state == ChallengeState.Completed)
                return;

            _state = ChallengeState.Completed;
            _armedFailPlatforms.Clear();

            SetObjectsActive(activateOnComplete, true);
            SetObjectsActive(deactivateOnComplete, false);
            StartMovingPlatforms(movingPlatformsToStartOnComplete);

            GroupCompleted?.Invoke();
            completed?.Invoke();
        }

        public void FailGroup()
        {
            if (_state != ChallengeState.Running)
                return;

            _state = ChallengeState.Idle;
            _armedFailPlatforms.Clear();

            SetObjectsActive(activateOnFail, true);
            SetObjectsActive(deactivateOnFail, false);
            HideAttemptObjectsForReset();

            if (respawnPlayerOnFail)
            {
                if (playerSpawner == null)
                    playerSpawner = FindFirstObjectByType<LevelPlayerSpawner>();

                playerSpawner?.RespawnPlayer();
            }

            ResetToIdleState();
            if (startOnAwake)
                StartGroup();

            GroupFailed?.Invoke();
            failed?.Invoke();
        }

        public void ResetToIdleState()
        {
            _state = ChallengeState.Idle;
            _armedFailPlatforms.Clear();
            ResetAttemptObjects();
        }

        public void ResetForLevelRetry()
        {
            ResetToIdleState();

            if (startOnAwake)
                StartGroup();
        }

        private void ResetAttemptObjects()
        {
            RefreshTargets();
            _remainingTargets = targets.Count;

            HideAttemptObjectsForReset();

            ResetTargets(false);
            ResetMovingPlatforms(movingPlatformsToStart);
            ResetMovingPlatforms(failWhenThesePlatformsFinish);
            ResetMovingPlatforms(movingPlatformsToResetOnFail);
        }

        private void HideAttemptObjectsForReset()
        {
            SetObjectsActive(activateOnStart, false);
            SetObjectsActive(deactivateOnStart, true);
            SetObjectsActive(activateOnComplete, false);
            SetObjectsActive(deactivateOnComplete, true);

            if (hideTargetsUntilStarted)
                SetTargetsActive(false);
        }

        private void RefreshTargets()
        {
            if (autoCollectChildTargets)
            {
                targets.Clear();
                GetComponentsInChildren(true, targets);
            }

            targets.RemoveAll(target => target == null);
            _remainingTargets = targets.Count;
        }

        private void SubscribeFailPlatforms()
        {
            if (failWhenThesePlatformsFinish == null)
                return;

            for (int i = 0; i < failWhenThesePlatformsFinish.Length; i++)
            {
                if (failWhenThesePlatformsFinish[i] != null)
                    failWhenThesePlatformsFinish[i].Completed += HandleFailPlatformCompleted;
            }
        }

        private void SubscribePlayerSpawner()
        {
            if (!failWhenPlayerRespawns)
                return;

            if (playerSpawner == null)
                playerSpawner = FindFirstObjectByType<LevelPlayerSpawner>();

            if (playerSpawner != null)
                playerSpawner.PlayerRespawned += HandlePlayerRespawned;
        }

        private void UnsubscribePlayerSpawner()
        {
            if (playerSpawner != null)
                playerSpawner.PlayerRespawned -= HandlePlayerRespawned;
        }

        private void HandlePlayerRespawned()
        {
            if (_state == ChallengeState.Running)
                FailGroup();
        }

        private void UnsubscribeFailPlatforms()
        {
            if (failWhenThesePlatformsFinish == null)
                return;

            for (int i = 0; i < failWhenThesePlatformsFinish.Length; i++)
            {
                if (failWhenThesePlatformsFinish[i] != null)
                    failWhenThesePlatformsFinish[i].Completed -= HandleFailPlatformCompleted;
            }
        }

        private void ArmFailPlatforms()
        {
            _armedFailPlatforms.Clear();

            if (failWhenThesePlatformsFinish == null)
                return;

            for (int i = 0; i < failWhenThesePlatformsFinish.Length; i++)
            {
                if (failWhenThesePlatformsFinish[i] != null)
                    _armedFailPlatforms.Add(failWhenThesePlatformsFinish[i]);
            }
        }

        private void HandleFailPlatformCompleted(MovingPlatform platform)
        {
            if (_state != ChallengeState.Running || Time.time < _failAllowedTime)
                return;

            if (!_armedFailPlatforms.Contains(platform))
                return;

            FailGroup();
        }

        private static void StartMovingPlatforms(MovingPlatform[] platforms)
        {
            if (platforms == null)
                return;

            for (int i = 0; i < platforms.Length; i++)
                platforms[i]?.ResetAndPlay();
        }

        private static void ResetMovingPlatforms(MovingPlatform[] platforms)
        {
            if (platforms == null)
                return;

            for (int i = 0; i < platforms.Length; i++)
                platforms[i]?.StopAndReset();
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    objects[i].SetActive(active);
            }
        }

        private void SetTargetsActive(bool active)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                    targets[i].gameObject.SetActive(active);
            }
        }

        private void ResetTargets(bool makeVisible)
        {
            for (int i = 0; i < targets.Count; i++)
                targets[i]?.ResetTarget(makeVisible);
        }
    }
}
