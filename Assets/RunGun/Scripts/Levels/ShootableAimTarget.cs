using RunGun.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("RunGun/Levels/Shootable Aim Target")]
    public sealed class ShootableAimTarget : MonoBehaviour, IWeaponHitReceiver
    {
        [SerializeField] private AimTargetGroupController group;
        [SerializeField, Min(1)] private int hitPoints = 1;
        [SerializeField] private bool disableWhenShot = true;
        [SerializeField] private UnityEvent shot;

        private int _remainingHitPoints;
        private bool _isShot;

        public bool IsShot => _isShot;

        private void Awake()
        {
            if (group == null)
                group = GetComponentInParent<AimTargetGroupController>();

            ResetTarget();
        }

        private void OnEnable()
        {
            if (_remainingHitPoints <= 0 && !_isShot)
                ResetTarget();
        }

        public void ResetTarget()
        {
            ResetTarget(true);
        }

        public void ResetTarget(bool makeVisible)
        {
            _remainingHitPoints = Mathf.Max(1, hitPoints);
            _isShot = false;

            if (makeVisible)
                gameObject.SetActive(true);
        }

        public void ReceiveWeaponHit(WeaponHitInfo hitInfo)
        {
            if (_isShot)
                return;

            _remainingHitPoints -= Mathf.Max(1, hitInfo.ImpactPower);
            if (_remainingHitPoints > 0)
                return;

            _isShot = true;
            shot?.Invoke();
            group?.NotifyTargetShot(this);

            if (disableWhenShot)
                gameObject.SetActive(false);
        }
    }
}
