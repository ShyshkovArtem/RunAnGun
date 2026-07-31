using RunGun.Settings;
using RunGun.Weapons;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private AudioClip hitClip;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.8f;
        [SerializeField, Range(0.1f, 3f)] private float hitPitch = 1f;
        [SerializeField] private bool animateDisable = true;
        [SerializeField, Min(1f)] private float hitPopScale = 1.18f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.12f;
        [SerializeField] private UnityEvent shot;

        private int _remainingHitPoints;
        private bool _isShot;
        private Vector3 _baseScale;
        private bool _hasBaseScale;
        private Coroutine _disableRoutine;

        public bool IsShot => _isShot;

        private void Awake()
        {
            if (group == null)
                group = GetComponentInParent<AimTargetGroupController>();

            AutoBindHitClip();
            CaptureBaseScale();
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
            CancelDisableAnimation();
            CaptureBaseScale();
            _remainingHitPoints = Mathf.Max(1, hitPoints);
            _isShot = false;
            transform.localScale = _baseScale;

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
            PlayHitSound(hitInfo.Hit.point);
            shot?.Invoke();
            group?.NotifyTargetShot(this);

            if (disableWhenShot)
                DisableTarget();
        }

        private void DisableTarget()
        {
            if (!animateDisable || !gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_disableRoutine != null)
                StopCoroutine(_disableRoutine);

            _disableRoutine = StartCoroutine(AnimateDisable());
        }

        private void CancelDisableAnimation()
        {
            if (_disableRoutine == null)
                return;

            StopCoroutine(_disableRoutine);
            _disableRoutine = null;
        }

        private void CaptureBaseScale()
        {
            if (_hasBaseScale)
                return;

            _baseScale = transform.localScale.sqrMagnitude > 0.0001f
                ? transform.localScale
                : Vector3.one;
            _hasBaseScale = true;
        }

        private System.Collections.IEnumerator AnimateDisable()
        {
            Vector3 popScale = _baseScale * hitPopScale;

            yield return ScaleOverTime(transform.localScale, popScale, popDuration);
            yield return ScaleOverTime(popScale, Vector3.zero, shrinkDuration);

            _disableRoutine = null;
            gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            transform.localScale = to;
        }

        private void PlayHitSound(Vector3 position)
        {
            if (hitClip == null || hitVolume <= 0f)
                return;

            var audioObject = new GameObject($"{name}_HitSound");
            audioObject.transform.position = position;

            var audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.clip = hitClip;
            GameSettings.SetSfxSourceVolume(audioSource, hitVolume);
            audioSource.pitch = hitPitch;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 40f;
            audioSource.Play();

            Destroy(audioObject, hitClip.length / Mathf.Max(0.01f, hitPitch) + 0.1f);
        }

        private void AutoBindHitClip()
        {
#if UNITY_EDITOR
            if (hitClip != null)
                return;

            hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RunGun/Audio/TargerHit.mp3");
#endif
        }
    }
}
