using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Level Checkpoint")]
    public sealed class LevelCheckpoint : MonoBehaviour
    {
        [SerializeField] private LevelPlayerSpawner playerSpawner;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private bool useCheckpointRotation = true;
        [SerializeField] private bool activateOnce = true;

        private bool _activated;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
            respawnPoint = transform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (activateOnce && _activated)
                return;

            if (other.GetComponentInParent<PlayerController>() == null)
                return;

            if (playerSpawner == null)
                playerSpawner = FindFirstObjectByType<LevelPlayerSpawner>();

            if (playerSpawner == null)
            {
                Debug.LogWarning($"{nameof(LevelCheckpoint)} on {name} has no player spawner assigned.", this);
                return;
            }

            playerSpawner.SetRespawnPoint(respawnPoint != null ? respawnPoint : transform, useCheckpointRotation);
            _activated = true;
        }
    }
}
