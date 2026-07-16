using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Level Run Start Trigger")]
    public sealed class LevelRunStartTrigger : MonoBehaviour
    {
        [SerializeField] private LevelRunTimer runTimer;
        [SerializeField] private string playerBodyName = "Player_Object";

        private bool _triggered;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        public void ResetTrigger()
        {
            _triggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !IsPlayer(other))
                return;

            if (runTimer == null)
                runTimer = FindFirstObjectByType<LevelRunTimer>();

            if (runTimer == null)
            {
                Debug.LogWarning($"{nameof(LevelRunStartTrigger)} on {name} has no run timer assigned.", this);
                return;
            }

            _triggered = true;
            runTimer.StartRun();
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
