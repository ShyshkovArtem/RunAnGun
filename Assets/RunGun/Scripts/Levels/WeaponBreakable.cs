using System.Collections;
using RunGun.Weapons;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Weapon Breakable")]
    public sealed class WeaponBreakable : MonoBehaviour, IWeaponHitReceiver
    {
        [SerializeField, Min(1)] private int requiredImpactPower = 3;
        [SerializeField] private bool resetOnPlayerRespawn = true;
        [SerializeField, Min(0.01f)] private float breakDuration = 0.18f;
        [SerializeField, Range(1f, 1.25f)] private float hitScale = 1.06f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _initialScale;
        private LevelPlayerSpawner _playerSpawner;
        private Coroutine _breakRoutine;
        private bool _broken;

        private void Awake()
        {
            CacheState();
        }

        private void Start()
        {
            if (!resetOnPlayerRespawn)
                return;

            _playerSpawner = FindFirstObjectByType<LevelPlayerSpawner>();
            if (_playerSpawner != null)
                _playerSpawner.PlayerRespawned += ResetBreakable;
        }

        private void OnDestroy()
        {
            if (_playerSpawner != null)
                _playerSpawner.PlayerRespawned -= ResetBreakable;
        }

        public void ReceiveWeaponHit(WeaponHitInfo hitInfo)
        {
            if (_broken || hitInfo.ImpactPower < requiredImpactPower)
                return;

            _broken = true;
            if (_breakRoutine != null)
                StopCoroutine(_breakRoutine);

            _breakRoutine = StartCoroutine(PlayBreakAnimation());
        }

        public void ResetBreakable()
        {
            if (_breakRoutine != null)
            {
                StopCoroutine(_breakRoutine);
                _breakRoutine = null;
            }

            CacheState();
            SetRenderersEnabled(false);
            transform.localScale = _initialScale;
            SetCollidersEnabled(true);
            SetRenderersEnabled(true);
            _broken = false;
        }

        private IEnumerator PlayBreakAnimation()
        {
            Vector3 startScale = _initialScale * hitScale;
            transform.localScale = startScale;
            float elapsed = 0f;

            while (elapsed < breakDuration)
            {
                float t = elapsed / breakDuration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = Vector3.zero;
            SetCollidersEnabled(false);
            SetRenderersEnabled(false);
            _breakRoutine = null;
        }

        private void CacheState()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);

            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider>(true);

            if (_initialScale == Vector3.zero)
                _initialScale = transform.localScale;
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = enabled;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].enabled = enabled;
            }
        }
    }
}
