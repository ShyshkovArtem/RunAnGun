using System;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Level Player Spawner")]
    public sealed class LevelPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool moveExistingPlayer = true;
        [SerializeField] private bool useSpawnerRotation = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string playerBodyName = "Player_Object";
        [SerializeField] private bool treatSpawnerAsGroundPoint = true;
        [SerializeField] private float groundSpawnClearance = 0.05f;
        [SerializeField] private bool respawnWhenFalling = true;
        [SerializeField] private float fallY = -20f;

        private Transform _fallCheckTarget;
        private Transform _activeRespawnPoint;
        private bool _activeRespawnUsesSpawnerRotation;

        public GameObject SpawnedPlayer { get; private set; }

        public Transform ActiveRespawnPoint => _activeRespawnPoint != null ? _activeRespawnPoint : transform;
        public event Action PlayerRespawned;

        private void Awake()
        {
            _activeRespawnPoint = transform;
            _activeRespawnUsesSpawnerRotation = useSpawnerRotation;
            SpawnOrMovePlayer();
        }

        private void Update()
        {
            if (!respawnWhenFalling || SpawnedPlayer == null)
            {
                return;
            }

            Transform fallTarget = GetFallCheckTarget();
            if (fallTarget == null)
            {
                return;
            }

            if (fallTarget.position.y < fallY)
            {
                RespawnPlayer();
            }
        }

        public void RespawnPlayer()
        {
            if (SpawnedPlayer == null)
            {
                SpawnOrMovePlayer();
                return;
            }

            var playerController = SpawnedPlayer.GetComponentInChildren<PlayerController>(true);
            if (playerController != null)
            {
                playerController.TeleportTo(
                    GetSpawnPosition(playerController),
                    GetSpawnRotation(playerController.transform.rotation));
                PlayerRespawned?.Invoke();
                return;
            }

            PlacePlayer(SpawnedPlayer);
            PlayerRespawned?.Invoke();
        }

        private void SpawnOrMovePlayer()
        {
            if (moveExistingPlayer && TryFindExistingPlayer(out var existingPlayer))
            {
                SpawnedPlayer = existingPlayer;
                CacheFallCheckTarget();
                PlacePlayer(SpawnedPlayer);
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogWarning($"{nameof(LevelPlayerSpawner)} on {name} has no player prefab assigned.", this);
                return;
            }

            Quaternion rotation = useSpawnerRotation ? transform.rotation : playerPrefab.transform.rotation;
            SpawnedPlayer = Instantiate(playerPrefab, transform.position, rotation);
            CacheFallCheckTarget();
            PlacePlayer(SpawnedPlayer);
        }

        private bool TryFindExistingPlayer(out GameObject player)
        {
            player = null;

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    return true;
                }
            }

            var controller = FindFirstObjectByType<PlayerController>();
            if (controller == null)
            {
                return false;
            }

            player = controller.transform.root.gameObject;
            return true;
        }

        private void PlacePlayer(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            var playerController = player.GetComponentInChildren<PlayerController>(true);
            if (playerController != null)
            {
                playerController.TeleportTo(
                    GetSpawnPosition(playerController),
                    GetSpawnRotation(playerController.transform.rotation));
                return;
            }

            Transform targetTransform = GetFallCheckTarget() != null ? GetFallCheckTarget() : player.transform;
            var characterController = targetTransform.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled)
            {
                characterController.enabled = false;
            }

            targetTransform.SetPositionAndRotation(
                GetSpawnPosition(characterController),
                GetSpawnRotation(targetTransform.rotation));

            if (wasEnabled)
            {
                characterController.enabled = true;
            }
        }

        public void SetRespawnPoint(Transform respawnPoint, bool useRespawnRotation = true)
        {
            if (respawnPoint == null)
            {
                return;
            }

            _activeRespawnPoint = respawnPoint;
            _activeRespawnUsesSpawnerRotation = useRespawnRotation;
        }

        public void ResetRespawnPoint()
        {
            _activeRespawnPoint = transform;
            _activeRespawnUsesSpawnerRotation = useSpawnerRotation;
        }

        private Vector3 GetSpawnPosition(PlayerController playerController)
        {
            CharacterController characterController = playerController != null ? playerController.controller : null;
            if (characterController == null && playerController != null)
                characterController = playerController.GetComponent<CharacterController>();

            return GetSpawnPosition(characterController);
        }

        private Vector3 GetSpawnPosition(CharacterController characterController)
        {
            Transform respawnPoint = ActiveRespawnPoint;
            Vector3 position = respawnPoint.position;
            if (!treatSpawnerAsGroundPoint || characterController == null)
                return position;

            float bottomToOrigin = (characterController.height * 0.5f) - characterController.center.y;
            position.y += Mathf.Max(0f, bottomToOrigin + groundSpawnClearance);
            return position;
        }

        private Quaternion GetSpawnRotation(Quaternion fallbackRotation)
        {
            Transform respawnPoint = ActiveRespawnPoint;
            return _activeRespawnUsesSpawnerRotation ? respawnPoint.rotation : fallbackRotation;
        }

        private Transform GetFallCheckTarget()
        {
            if (_fallCheckTarget != null)
            {
                return _fallCheckTarget;
            }

            CacheFallCheckTarget();
            return _fallCheckTarget;
        }

        private void CacheFallCheckTarget()
        {
            _fallCheckTarget = null;

            if (SpawnedPlayer == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(playerBodyName))
            {
                _fallCheckTarget = FindChildRecursive(SpawnedPlayer.transform, playerBodyName);
                if (_fallCheckTarget != null)
                {
                    return;
                }
            }

            var playerController = SpawnedPlayer.GetComponentInChildren<PlayerController>(true);
            _fallCheckTarget = playerController != null ? playerController.transform : SpawnedPlayer.transform;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = FindChildRecursive(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
