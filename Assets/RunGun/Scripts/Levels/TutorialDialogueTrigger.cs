using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Tutorial Dialogue Trigger")]
    public sealed class TutorialDialogueTrigger : MonoBehaviour
    {
        [SerializeField] private TutorialDialogueController dialogueController;
        [SerializeField] private string playerBodyName = "Player_Object";
        [SerializeField] private bool playOnce = true;
        [SerializeField] private bool autoFindClosestDialogue = true;

        private bool _hasPlayed;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        public void ResetTrigger()
        {
            _hasPlayed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPlayDialogue(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryPlayDialogue(other);
        }

        private void TryPlayDialogue(Collider other)
        {
            if (playOnce && _hasPlayed)
                return;

            if (!IsPlayer(other))
                return;

            if (dialogueController == null)
                dialogueController = autoFindClosestDialogue ? FindClosestDialogueController() : null;

            if (dialogueController == null)
            {
                Debug.LogWarning($"{nameof(TutorialDialogueTrigger)} on {name} has no dialogue controller assigned.", this);
                return;
            }

            if (dialogueController.IsPlaying)
                return;

            _hasPlayed = dialogueController.TryStartDialogue();
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null)
                return false;

            if (!string.IsNullOrWhiteSpace(playerBodyName) && other.transform.root != null)
            {
                Transform current = other.transform;
                while (current != null)
                {
                    if (current.name == playerBodyName)
                        return true;

                    current = current.parent;
                }
            }

            return other.GetComponentInParent<ElmanGameDevTools.PlayerSystem.PlayerController>() != null;
        }

        private TutorialDialogueController FindClosestDialogueController()
        {
            var controllers = FindObjectsByType<TutorialDialogueController>(FindObjectsSortMode.None);
            TutorialDialogueController closest = null;
            float closestDistanceSqr = float.PositiveInfinity;
            Vector3 position = transform.position;

            for (var i = 0; i < controllers.Length; i++)
            {
                TutorialDialogueController controller = controllers[i];
                if (controller == null)
                    continue;

                float distanceSqr = (controller.transform.position - position).sqrMagnitude;
                if (distanceSqr >= closestDistanceSqr)
                    continue;

                closest = controller;
                closestDistanceSqr = distanceSqr;
            }

            return closest;
        }
    }
}
