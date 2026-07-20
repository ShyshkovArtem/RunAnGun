using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Trial Finish Trigger")]
    public sealed class TrialFinishTrigger : MonoBehaviour
    {
        [SerializeField] private TrialLevelController trialLevelController;
        [SerializeField] private string playerBodyName = "Player_Object";

        private bool _completed;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        public void ResetTrigger()
        {
            _completed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_completed || !IsPlayer(other))
                return;

            if (trialLevelController == null)
                trialLevelController = FindFirstObjectByType<TrialLevelController>();

            if (trialLevelController == null)
            {
                Debug.LogWarning($"{nameof(TrialFinishTrigger)} on {name} has no trial level controller.", this);
                return;
            }

            _completed = trialLevelController.TryCompleteTrial();
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null)
                return false;

            if (!string.IsNullOrWhiteSpace(playerBodyName))
            {
                Transform current = other.transform;
                while (current != null)
                {
                    if (current.name == playerBodyName)
                        return true;

                    current = current.parent;
                }
            }

            return other.GetComponentInParent<PlayerController>() != null;
        }
    }
}
