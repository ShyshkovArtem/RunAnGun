using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Aim Target Group Trigger")]
    public sealed class AimTargetGroupTrigger : MonoBehaviour
    {
        [SerializeField] private AimTargetGroupController targetGroup;
        [SerializeField] private bool activateOnce = true;
        [SerializeField] private bool reactivateAfterFail = true;
        [SerializeField] private bool stayActivatedAfterComplete = true;

        private bool _completedLock;
        private AimTargetGroupController _subscribedGroup;

        private void Awake()
        {
            ResolveTargetGroup();
        }

        private void OnEnable()
        {
            SubscribeGroupEvents();
        }

        private void OnDisable()
        {
            UnsubscribeGroupEvents();
        }

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryStartFromCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryStartFromCollider(other);
        }

        public void ResetTrigger()
        {
            _completedLock = false;
        }

        private void TryStartFromCollider(Collider other)
        {
            if (activateOnce && _completedLock)
                return;

            if (other.GetComponentInParent<PlayerController>() == null)
                return;

            ResolveTargetGroup();
            if (targetGroup == null || !targetGroup.CanStart)
                return;

            SubscribeGroupEvents();
            targetGroup.StartGroup();
        }

        private void ResolveTargetGroup()
        {
            if (targetGroup == null)
                targetGroup = GetComponentInParent<AimTargetGroupController>();
        }

        private void SubscribeGroupEvents()
        {
            ResolveTargetGroup();
            if (targetGroup == null || _subscribedGroup == targetGroup)
                return;

            UnsubscribeGroupEvents();
            _subscribedGroup = targetGroup;
            _subscribedGroup.GroupFailed += HandleGroupFailed;
            _subscribedGroup.GroupCompleted += HandleGroupCompleted;
        }

        private void UnsubscribeGroupEvents()
        {
            if (_subscribedGroup == null)
                return;

            _subscribedGroup.GroupFailed -= HandleGroupFailed;
            _subscribedGroup.GroupCompleted -= HandleGroupCompleted;
            _subscribedGroup = null;
        }

        private void HandleGroupFailed()
        {
            if (reactivateAfterFail)
                _completedLock = false;
        }

        private void HandleGroupCompleted()
        {
            if (stayActivatedAfterComplete)
                _completedLock = true;
        }
    }
}
