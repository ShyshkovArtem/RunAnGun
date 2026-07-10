using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Moving Platform")]
    public sealed class MovingPlatform : MonoBehaviour
    {
        public enum MovementMode
        {
            DirectionAndDistance,
            BetweenPoints
        }

        public enum LoopMode
        {
            PingPong,
            Loop,
            Once
        }

        public enum EaseMode
        {
            Linear,
            SmoothStep
        }

        [Header("Movement")]
        [SerializeField] private bool moveOnPlay = true;
        [SerializeField] private MovementMode movementMode = MovementMode.DirectionAndDistance;
        [SerializeField] private LoopMode loopMode = LoopMode.PingPong;
        [SerializeField] private EaseMode easeMode = EaseMode.SmoothStep;
        [SerializeField] private bool useLocalDirection = true;
        [SerializeField] private Vector3 direction = Vector3.forward;
        [SerializeField, Min(0f)] private float distance = 5f;
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [SerializeField, Min(0.01f)] private float speed = 2f;
        [SerializeField, Min(0f)] private float waitAtEnds = 0.2f;
        [SerializeField] private bool startAtEnd;

        [Header("Rotation")]
        [SerializeField] private bool rotate;
        [SerializeField] private bool useLocalRotationAxis = true;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 45f;

        [Header("Player Carry")]
        [SerializeField] private bool carryCharacterControllers = true;
        [SerializeField] private LayerMask carryMask = ~0;
        [SerializeField, Min(0.01f)] private float carryCheckHeight = 0.3f;
        [SerializeField, Min(0f)] private float carryPadding = 0.08f;

        [Header("Debug")]
        [SerializeField] private bool drawPath = true;
        [SerializeField] private Color pathColor = new(0.1f, 0.9f, 1f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent movementCompleted = new();

        private readonly Collider[] _carryHits = new Collider[16];
        private readonly HashSet<CharacterController> _carriedControllers = new();
        private Rigidbody _rigidbody;
        private Collider _platformCollider;
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _progress;
        private float _directionSign = 1f;
        private float _waitTimer;
        private bool _isMoving;
        private bool _completed;
        private bool _hasCapturedStartPose;

        public bool IsMoving => _isMoving;
        public bool HasCompleted => _completed;
        public UnityEvent MovementCompleted => movementCompleted;
        public event Action<MovingPlatform> Completed;

        private void Awake()
        {
            CacheComponents();
            CaptureStartPose();
        }

        private void OnEnable()
        {
            _isMoving = moveOnPlay;
        }

        private void Reset()
        {
            CacheComponents();

            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
        }

        private void FixedUpdate()
        {
            EnsureStartPoseCaptured();

            if (!_isMoving && !rotate)
                return;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;

            StepMovement(Time.fixedDeltaTime);
            StepRotation(Time.fixedDeltaTime);
            ApplyPose();

            if (carryCharacterControllers)
                CarryCharacterControllers(_targetPosition - _lastPosition, _targetRotation * Quaternion.Inverse(_lastRotation));
        }

        public void Play()
        {
            EnsureStartPoseCaptured();
            _completed = false;
            _isMoving = true;
        }

        public void Pause()
        {
            EnsureStartPoseCaptured();
            _isMoving = false;
        }

        public void Toggle()
        {
            EnsureStartPoseCaptured();
            _isMoving = !_isMoving;
        }

        public void ResetPlatform()
        {
            EnsureStartPoseCaptured();
            _progress = startAtEnd ? 1f : 0f;
            _directionSign = startAtEnd ? -1f : 1f;
            _waitTimer = 0f;
            _completed = false;
            _targetPosition = Vector3.LerpUnclamped(GetPointA(), GetPointB(), GetEasedProgress());
            _targetRotation = _startRotation;
            ApplyPoseImmediate();
        }

        public void StopAndReset()
        {
            _isMoving = false;
            ResetPlatform();
        }

        public void ResetForLevelRetry()
        {
            ResetPlatform();
            _isMoving = moveOnPlay;
        }

        public void ResetAndPlay()
        {
            ResetPlatform();
            Play();
        }

        private void CacheComponents()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _platformCollider = GetComponent<Collider>();
        }

        private void EnsureStartPoseCaptured()
        {
            if (_hasCapturedStartPose)
                return;

            CacheComponents();
            CaptureStartPose();
        }

        private void CaptureStartPose()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            _progress = startAtEnd ? 1f : 0f;
            _directionSign = startAtEnd ? -1f : 1f;
            _lastPosition = _startPosition;
            _lastRotation = _startRotation;
            _targetPosition = _startPosition;
            _targetRotation = _startRotation;
            _hasCapturedStartPose = true;
        }

        private void StepMovement(float deltaTime)
        {
            if (!_isMoving)
                return;

            float pathLength = Mathf.Max(0.01f, Vector3.Distance(GetPointA(), GetPointB()));
            if (_waitTimer > 0f)
            {
                _waitTimer -= deltaTime;
                return;
            }

            float step = speed / pathLength * deltaTime;
            _progress += step * _directionSign;

            if (_progress < 0f || _progress > 1f)
                HandlePathEnd();
        }

        private void HandlePathEnd()
        {
            switch (loopMode)
            {
                case LoopMode.PingPong:
                    _progress = Mathf.Clamp01(_progress);
                    _directionSign *= -1f;
                    _waitTimer = waitAtEnds;
                    break;
                case LoopMode.Loop:
                    _progress = _directionSign > 0f ? 0f : 1f;
                    _waitTimer = waitAtEnds;
                    break;
                case LoopMode.Once:
                    _progress = Mathf.Clamp01(_progress);
                    _isMoving = false;
                    if (!_completed)
                    {
                        _completed = true;
                        Completed?.Invoke(this);
                        movementCompleted?.Invoke();
                    }
                    break;
            }
        }

        private void StepRotation(float deltaTime)
        {
            if (!rotate)
                return;

            Vector3 axis = rotationAxis.sqrMagnitude > 0.001f ? rotationAxis.normalized : Vector3.up;
            if (useLocalRotationAxis)
                axis = _targetRotation * axis;

            Quaternion delta = Quaternion.AngleAxis(degreesPerSecond * deltaTime, axis);
            _targetRotation = delta * _targetRotation;
        }

        private void ApplyPose()
        {
            _targetPosition = Vector3.LerpUnclamped(GetPointA(), GetPointB(), GetEasedProgress());

            if (_rigidbody != null && _rigidbody.isKinematic)
            {
                _rigidbody.MovePosition(_targetPosition);
                _rigidbody.MoveRotation(_targetRotation);
            }
            else
            {
                transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            }
        }

        private void ApplyPoseImmediate()
        {
            _lastPosition = _targetPosition;
            _lastRotation = _targetRotation;

            if (_rigidbody != null)
            {
                _rigidbody.position = _targetPosition;
                _rigidbody.rotation = _targetRotation;

                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
            }

            transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            Physics.SyncTransforms();
        }

        private float GetEasedProgress()
        {
            return easeMode == EaseMode.SmoothStep
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_progress))
                : Mathf.Clamp01(_progress);
        }

        private Vector3 GetPointA()
        {
            return movementMode == MovementMode.BetweenPoints && pointA != null
                ? pointA.position
                : _startPosition;
        }

        private Vector3 GetPointB()
        {
            if (movementMode == MovementMode.BetweenPoints && pointB != null)
                return pointB.position;

            Vector3 normalizedDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            if (useLocalDirection)
                normalizedDirection = _startRotation * normalizedDirection;

            return _startPosition + normalizedDirection * distance;
        }

        private void CarryCharacterControllers(Vector3 positionDelta, Quaternion rotationDelta)
        {
            if (_platformCollider == null || positionDelta.sqrMagnitude < 0.000001f && Quaternion.Angle(Quaternion.identity, rotationDelta) < 0.001f)
                return;

            _carriedControllers.Clear();

            Bounds bounds = _platformCollider.bounds;
            Vector3 center = new(bounds.center.x, bounds.max.y + carryCheckHeight * 0.5f, bounds.center.z);
            Vector3 halfExtents = new(
                bounds.extents.x + carryPadding,
                carryCheckHeight * 0.5f,
                bounds.extents.z + carryPadding);

            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _carryHits, Quaternion.identity, carryMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _carryHits[i];
                if (hit == null || hit == _platformCollider)
                    continue;

                CharacterController controller = hit.GetComponentInParent<CharacterController>();
                if (controller == null || !_carriedControllers.Add(controller))
                    continue;

                Vector3 rotatedOffset = rotationDelta * (controller.transform.position - transform.position);
                Vector3 rotationCarryDelta = (transform.position + rotatedOffset) - controller.transform.position;
                controller.Move(positionDelta + rotationCarryDelta);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawPath)
                return;

            if (!Application.isPlaying)
            {
                _startPosition = transform.position;
                _startRotation = transform.rotation;
            }

            Vector3 a = GetPointA();
            Vector3 b = GetPointB();

            Gizmos.color = pathColor;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.2f);
            Gizmos.DrawWireSphere(b, 0.2f);
        }
    }
}
