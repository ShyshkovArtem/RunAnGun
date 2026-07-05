using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ElmanGameDevTools.PlayerSystem
{
    /// <summary>
    /// Advanced First Person Controller.
    /// Handles movement, crouching, jumping, and camera effects like HeadBob and Tilt.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        [Header("REFERENCES")]
        [Tooltip("The CharacterController component used for physics-based movement.")]
        public CharacterController controller;
        [Tooltip("The Transform of the camera, usually a child of the player object.")]
        public Transform playerCamera;

        [Header("MOVEMENT SETTINGS")]
        public float speed = 6f;
        public float runSpeed = 9f;
        public float jumpHeight = 1.2f;
        public float gravity = -25f;
        public float sensitivity = 0.08f;
        public float groundAcceleration = 28f;
        public float groundDeceleration = 34f;
        public float airAcceleration = 18f;
        public float slopeAcceleration = 30f;
        public float uphillSlowdown = 12f;
        public float maxSlopeRunSpeed = 18f;

        [Header("EXTERNAL IMPULSES")]
        public float maxExternalHorizontalSpeed = 42f;
        public float groundedImpulseLiftThreshold = 0.5f;

        [Header("BUNNY HOP SETTINGS")]
        public float bunnyHopWindow = 0.12f;
        [Range(0f, 1f)] public float airControl = 0.75f;
        public float airStrafeAcceleration = 14f;
        public float maxAirSpeed = 28f;
        public float missedBunnyHopDeceleration = 18f;
        public float jumpRedirectMinSpeed = 6f;

        [Header("CAMERA SETTINGS")]
        public float maxLookUpAngle = 90f;
        public float maxLookDownAngle = -90f;
        public bool useCameraSmoothing;
        public bool enableHeadBob = true;
        public bool requireCursorLockForLook = true;
        public bool showInputDebug;
        [Range(0.01f, 0.15f)] public float bobAmountX = 0.04f;
        [Range(0.01f, 0.15f)] public float bobAmountY = 0.05f;
        public float walkBobFrequency = 12f;
        public float runBobFrequency = 16f;
        public float crouchBobFrequency = 8f;
        public float bobSmoothness = 10f;

        [Header("CAMERA INERTIA & WEIGHT")]
        [Range(1f, 30f)] public float cameraWeight = 12f;
        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;
        private float _smoothInputX;

        [Header("CAMERA EFFECTS")]
        public bool enableCameraTilt = true;
        public float tiltAmount = 2f;
        public float tiltSmoothness = 8f;
        public float runTiltMultiplier = 1.2f;
        public float crouchTiltMultiplier = 0.5f;
        [Space]
        public float turnTiltAmount = 1.5f;
        public float maxTotalTilt = 5f;

        [Header("CROUCH SETTINGS")]
        public float crouchHeight = 1.2f;
        public float crouchSmoothTime = 0.1f;

        [Header("SLIDE SETTINGS")]
        public float slideMinStartSpeed = 6f;
        public float slideStartSpeed = 12.5f;
        public float slideStartBoost = 2.5f;
        public float slideFriction = 2f;
        public float uphillSlideFriction = 9f;
        [Range(0f, 12f)] public float slideSteering = 4.5f;
        public float downhillAcceleration = 38f;
        public float maxSlideSpeed = 30f;
        public float minSlideSpeed = 5f;
        public float downhillSlideStartAngle = 5f;

        [Header("FOV SETTINGS")]
        public bool enableRunFov = true;
        public float normalFov = 60f;
        public float runFov = 70f;
        public float fovChangeSpeed = 8f;

        [Header("STANDING DETECTION & GROUND CHECK")]
        public GameObject standingHeightMarker;
        public float standingCheckRadius = 0.2f;
        public LayerMask obstacleLayerMask = ~0;
        public float minStandingClearance = 0.01f;
        public LayerMask groundLayer = 1;
        public float groundCheckDistance = 0.5f;

        private Vector3 _velocity;
        private float _currentTilt;
        private float _timer;
        private float _originalHeight;
        private float _targetHeight;
        private float _currentMovementSpeed;
        private float _cameraBaseHeight;
        private float _markerHeightOffset;
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _horizontalVelocity;
        private Vector3 _slideVelocity;
        private float _timeSinceLanded = 999f;

        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _isCrouching;
        private bool _isSliding;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _jumpPressedThisFrame;
        private bool _sprintPressed;
        private bool _crouchPressed;
        private bool _crouchPressedThisFrame;
        private Vector2 _queuedMouseDelta;
        private Vector2 _lastMousePosition;
        private bool _hasLastMousePosition;
        private string _debugText;
        private GUIStyle _debugStyle;
        private Camera _playerCameraComponent;
        private readonly Collider[] _standUpHits = new Collider[16];
        private MovementState _currentMovementState = MovementState.Walking;

        public enum MovementState { Walking, Running, Crouching, Sliding, Jumping }

        public bool IsGrounded => _isGrounded;
        public bool IsCrouching => _isCrouching;
        public bool IsSliding => _isSliding;
        public Vector2 MoveInput => _moveInput;
        public float CurrentHorizontalSpeed { get; private set; }
        public MovementState CurrentState => _currentMovementState;

        public void AddExternalImpulse(Vector3 impulse, float airRedirectStrength = 0f)
        {
            Vector3 horizontalImpulse = Vector3.ProjectOnPlane(impulse, Vector3.up);
            bool applyAirRedirect = !_isGrounded
                && !_isSliding
                && airRedirectStrength > 0f
                && horizontalImpulse.sqrMagnitude > 0.001f
                && _horizontalVelocity.sqrMagnitude > 0.001f;

            if (applyAirRedirect)
            {
                float currentSpeed = _horizontalVelocity.magnitude;
                Vector3 redirectedVelocity = horizontalImpulse.normalized * currentSpeed;
                _horizontalVelocity = Vector3.Slerp(_horizontalVelocity, redirectedVelocity, Mathf.Clamp01(airRedirectStrength));
            }

            if (_isSliding)
            {
                _slideVelocity += horizontalImpulse;
                _slideVelocity = ClampHorizontalSpeed(_slideVelocity);
            }
            else
            {
                _horizontalVelocity += horizontalImpulse;
                _horizontalVelocity = ClampHorizontalSpeed(_horizontalVelocity);
            }

            if (impulse.y > 0f)
            {
                _velocity.y = Mathf.Max(_velocity.y, impulse.y);

                if (impulse.y >= groundedImpulseLiftThreshold)
                {
                    _isGrounded = false;
                    _timeSinceLanded = 999f;
                }
            }
            else
            {
                _velocity.y += impulse.y;
            }
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();

            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));

            if (playerCamera != null)
                playerCamera.localRotation = Quaternion.identity;

            _velocity = Vector3.zero;
            _horizontalVelocity = Vector3.zero;
            _slideVelocity = Vector3.zero;
            _isSliding = false;
            _isCrouching = false;
            _isGrounded = false;
            _timeSinceLanded = 999f;
            CurrentHorizontalSpeed = 0f;
            _currentMovementState = MovementState.Walking;

            _targetYaw = transform.eulerAngles.y;
            _currentYaw = _targetYaw;
            _targetPitch = 0f;
            _currentPitch = 0f;
            _currentTilt = 0f;

            if (_originalHeight > 0f)
            {
                _targetHeight = _originalHeight;
                if (controller != null)
                    controller.height = _originalHeight;

                if (playerCamera != null)
                    playerCamera.localPosition = new Vector3(0f, _cameraBaseHeight, 0f);
            }

            if (standingHeightMarker != null)
                standingHeightMarker.transform.position = new Vector3(transform.position.x, transform.position.y + _markerHeightOffset, transform.position.z);

            if (wasEnabled)
                controller.enabled = true;
        }

        private void OnEnable()
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            EnableMouseDevices();
            InputSystem.onEvent += HandleInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= HandleInputEvent;
        }

        private void Start()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (Mouse.current != null)
            {
                _lastMousePosition = Mouse.current.position.ReadValue();
                _hasLastMousePosition = true;
            }
            _originalHeight = controller.height;
            _targetHeight = _originalHeight;
            _cameraBaseHeight = playerCamera.localPosition.y;
            _playerCameraComponent = playerCamera.GetComponent<Camera>();

            _targetYaw = transform.eulerAngles.y;
            _targetPitch = playerCamera.localEulerAngles.x;
            _currentYaw = _targetYaw;
            _currentPitch = _targetPitch;

            if (standingHeightMarker != null)
                _markerHeightOffset = standingHeightMarker.transform.position.y - transform.position.y;
        }

        private void Update()
        {
            HandleCursorLock();
            ReadInput();
            CheckGroundStatus();
            HandleCrouchLogic();
            UpdateMovementState();
            HandleMovement();
            HandleHeightAndCamera();
            HandleCameraControl();
            HandleCameraTilt();
            HandleFovChange();

            if (enableHeadBob) HandleHeadBob();

            if (showInputDebug) UpdateDebugText();
        }

        /// <summary>
        /// SphereCast based ground detection to ensure stability on slopes and stairs.
        /// </summary>
        private void CheckGroundStatus()
        {
            _wasGrounded = _isGrounded;

            float sphereRadius = controller.radius * 0.8f;
            float castDistance = Mathf.Max(groundCheckDistance, (controller.height * 0.5f) - sphereRadius + groundCheckDistance);
            Vector3 origin = transform.position + controller.center;
            bool groundHit = Physics.SphereCast(origin, sphereRadius, Vector3.down, out RaycastHit hit, castDistance, groundLayer);
            _isGrounded = groundHit || controller.isGrounded;
            _groundNormal = groundHit ? hit.normal : Vector3.up;

            if (_isGrounded && !_wasGrounded)
                _timeSinceLanded = 0f;
            else if (_isGrounded)
                _timeSinceLanded += Time.deltaTime;
            else
                _timeSinceLanded = 999f;

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -5f;
            }
        }

        private void UpdateMovementState()
        {
            bool wantsToRun = _sprintPressed && _moveInput.sqrMagnitude > 0.01f;

            if (!_isGrounded)
            {
                _isSliding = false;
                _currentMovementState = MovementState.Jumping;
                _currentMovementSpeed = wantsToRun ? runSpeed : speed;
                return;
            }

            if (_isSliding)
            {
                _currentMovementState = MovementState.Sliding;
                _currentMovementSpeed = _slideVelocity.magnitude;
                return;
            }

            if (_isCrouching)
            {
                _currentMovementState = MovementState.Crouching;
                _currentMovementSpeed = speed * 0.5f;
            }
            else
            {
                _currentMovementState = wantsToRun ? MovementState.Running : MovementState.Walking;
                _currentMovementSpeed = wantsToRun ? runSpeed : speed;
            }
        }

        private void HandleMovement()
        {
            Vector3 moveInput = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            if (moveInput.magnitude > 1f) moveInput.Normalize();

            if (_jumpPressedThisFrame && _isGrounded && !_isCrouching)
            {
                ApplyJumpTakeoffDirection(moveInput);
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _isGrounded = false;
                _timeSinceLanded = 999f;
            }

            if (standingHeightMarker != null)
                standingHeightMarker.transform.position = new Vector3(transform.position.x, transform.position.y + _markerHeightOffset, transform.position.z);

            if (_isSliding)
                HandleSlideMovement(moveInput);
            else
                HandleNormalMovement(moveInput);

            CurrentHorizontalSpeed = _isSliding ? _slideVelocity.magnitude : _horizontalVelocity.magnitude;

            _velocity.y += gravity * Time.deltaTime;
            controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleNormalMovement(Vector3 moveInput)
        {
            float targetSpeed = _currentMovementSpeed;
            bool inBunnyHopWindow = _timeSinceLanded <= bunnyHopWindow;
            if (_isGrounded && inBunnyHopWindow && _horizontalVelocity.magnitude > targetSpeed)
                targetSpeed = _horizontalVelocity.magnitude;

            Vector3 desiredVelocity = moveInput * targetSpeed;

            if (_isGrounded)
            {
                float acceleration = moveInput.sqrMagnitude > 0.001f ? groundAcceleration : groundDeceleration;
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, acceleration * Time.deltaTime);
                ApplyMissedBunnyHopSpeedLoss();
                ApplyGroundSlopeForces();
            }
            else
            {
                ApplyAirMovement(moveInput);
            }

            controller.Move(_horizontalVelocity * Time.deltaTime);
        }

        private void ApplyGroundSlopeForces()
        {
            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            if (downhillDirection.sqrMagnitude < 0.001f)
                return;

            downhillDirection.Normalize();
            float slopeAngle01 = Mathf.Clamp01(Vector3.Angle(_groundNormal, Vector3.up) / controller.slopeLimit);

            if (_horizontalVelocity.sqrMagnitude > 0.001f)
            {
                float downhillAlignment = Vector3.Dot(_horizontalVelocity.normalized, downhillDirection);
                if (downhillAlignment > 0f)
                {
                    _horizontalVelocity += downhillDirection * (slopeAcceleration * slopeAngle01 * downhillAlignment * Time.deltaTime);
                    _horizontalVelocity = ClampHorizontalSpeed(_horizontalVelocity, maxSlopeRunSpeed);
                }
                else
                {
                    _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, uphillSlowdown * slopeAngle01 * Time.deltaTime);
                }
            }
            else if (_moveInput.sqrMagnitude > 0.001f)
            {
                Vector3 inputDirection = (transform.right * _moveInput.x + transform.forward * _moveInput.y).normalized;
                float inputDownhillAlignment = Vector3.Dot(inputDirection, downhillDirection);
                if (inputDownhillAlignment > 0f)
                {
                    _horizontalVelocity += downhillDirection * (slopeAcceleration * slopeAngle01 * inputDownhillAlignment * Time.deltaTime);
                    _horizontalVelocity = ClampHorizontalSpeed(_horizontalVelocity, maxSlopeRunSpeed);
                }
            }
        }

        private void ApplyAirMovement(Vector3 moveInput)
        {
            if (moveInput.sqrMagnitude < 0.001f)
                return;

            Vector3 inputDirection = moveInput.normalized;
            float currentSpeed = _horizontalVelocity.magnitude;
            float desiredSpeed = Mathf.Max(_currentMovementSpeed, Mathf.Min(currentSpeed, maxAirSpeed));
            Vector3 desiredVelocity = inputDirection * desiredSpeed;
            float controlAcceleration = Mathf.Max(airAcceleration, airStrafeAcceleration) * Mathf.Clamp01(airControl);
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, controlAcceleration * Time.deltaTime);

            if (_horizontalVelocity.magnitude > maxAirSpeed)
                _horizontalVelocity = _horizontalVelocity.normalized * maxAirSpeed;
        }

        private void ApplyJumpTakeoffDirection(Vector3 moveInput)
        {
            float speed = _horizontalVelocity.magnitude;
            Vector3 takeoffDirection = moveInput;

            if (takeoffDirection.sqrMagnitude < 0.001f)
            {
                takeoffDirection = _horizontalVelocity;
            }

            if (takeoffDirection.sqrMagnitude < 0.001f)
                return;

            if (speed < jumpRedirectMinSpeed)
            {
                speed = _currentMovementSpeed;
            }

            _horizontalVelocity = takeoffDirection.normalized * speed;
        }

        private void ApplyMissedBunnyHopSpeedLoss()
        {
            if (_timeSinceLanded <= bunnyHopWindow || _horizontalVelocity.magnitude <= _currentMovementSpeed)
                return;

            Vector3 cappedVelocity = _horizontalVelocity.normalized * _currentMovementSpeed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, cappedVelocity, missedBunnyHopDeceleration * Time.deltaTime);
        }

        private Vector3 ClampHorizontalSpeed(Vector3 horizontalVelocity)
        {
            return ClampHorizontalSpeed(horizontalVelocity, maxExternalHorizontalSpeed);
        }

        private static Vector3 ClampHorizontalSpeed(Vector3 horizontalVelocity, float maxSpeed)
        {
            if (horizontalVelocity.magnitude <= maxSpeed)
                return horizontalVelocity;

            return horizontalVelocity.normalized * maxSpeed;
        }

        private void HandleCrouchLogic()
        {
            if (_isSliding)
            {
                bool wantsToEndSlide = !_crouchPressed || _slideVelocity.magnitude < minSlideSpeed || !_isGrounded;
                if (wantsToEndSlide)
                    EndSlide();
            }
            else if (CanStartSlide())
            {
                StartSlide();
            }

            _isCrouching = _isSliding || _crouchPressed || !CanStandUp();
            _targetHeight = _isCrouching ? crouchHeight : _originalHeight;
        }

        private bool CanStartSlide()
        {
            if (!_crouchPressedThisFrame || !_isGrounded || _isSliding)
                return false;

            Vector3 horizontalVelocity = _horizontalVelocity;
            bool hasEnoughSpeed = horizontalVelocity.magnitude >= slideMinStartSpeed;
            bool hasDownhillStart = IsTryingToSlideDownhill(horizontalVelocity);
            if (!hasEnoughSpeed && !hasDownhillStart)
                return false;

            return _moveInput.sqrMagnitude > 0.01f || horizontalVelocity.magnitude >= slideMinStartSpeed;
        }

        private void StartSlide()
        {
            Vector3 horizontalVelocity = _horizontalVelocity;
            Vector3 slideDirection = horizontalVelocity.sqrMagnitude > 0.01f
                ? horizontalVelocity.normalized
                : transform.forward;

            float startSpeed = Mathf.Max(slideStartSpeed, horizontalVelocity.magnitude + slideStartBoost);
            _slideVelocity = slideDirection * Mathf.Min(startSpeed, maxSlideSpeed);
            _horizontalVelocity = Vector3.zero;
            _isSliding = true;
        }

        private void EndSlide()
        {
            _horizontalVelocity = _slideVelocity;
            _isSliding = false;
            _slideVelocity = Vector3.zero;
        }

        private bool IsTryingToSlideDownhill(Vector3 horizontalVelocity)
        {
            Vector3 testVelocity = horizontalVelocity;
            if (testVelocity.sqrMagnitude < 0.01f)
                testVelocity = GetInputDirection() * speed;

            if (testVelocity.sqrMagnitude < 0.01f)
                return false;

            float slopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
            if (slopeAngle < downhillSlideStartAngle)
                return false;

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            return downhillDirection.sqrMagnitude > 0.001f
                && Vector3.Dot(testVelocity.normalized, downhillDirection.normalized) > 0.25f;
        }

        private Vector3 GetInputDirection()
        {
            Vector3 inputDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            return inputDirection.sqrMagnitude > 1f ? inputDirection.normalized : inputDirection;
        }

        private void HandleSlideMovement(Vector3 steeringInput)
        {
            if (_jumpPressedThisFrame && _isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _isGrounded = false;
                EndSlide();
                return;
            }

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            if (downhillDirection.sqrMagnitude > 0.001f)
            {
                downhillDirection.Normalize();
                float downhillAlignment = Vector3.Dot(_slideVelocity.normalized, downhillDirection);
                if (downhillAlignment > 0f)
                {
                    _slideVelocity += downhillDirection * (downhillAcceleration * downhillAlignment * Time.deltaTime);
                    _slideVelocity = ClampHorizontalSpeed(_slideVelocity, maxSlideSpeed);
                }
            }

            if (steeringInput.sqrMagnitude > 0.001f)
            {
                Vector3 targetDirection = Vector3.ProjectOnPlane(steeringInput, _groundNormal).normalized;
                Vector3 targetVelocity = targetDirection * _slideVelocity.magnitude;
                _slideVelocity = Vector3.MoveTowards(_slideVelocity, targetVelocity, slideSteering * _slideVelocity.magnitude * Time.deltaTime);
            }

            _slideVelocity = Vector3.ProjectOnPlane(_slideVelocity, _groundNormal);
            float friction = slideFriction;
            if (downhillDirection.sqrMagnitude > 0.001f && Vector3.Dot(_slideVelocity.normalized, downhillDirection.normalized) < -0.1f)
                friction = uphillSlideFriction;

            _slideVelocity = Vector3.MoveTowards(_slideVelocity, Vector3.zero, friction * Time.deltaTime);

            controller.Move(_slideVelocity * Time.deltaTime);
        }

        private void HandleHeightAndCamera()
        {
            float prevHeight = controller.height;
            controller.height = Mathf.Lerp(controller.height, _targetHeight, Time.deltaTime * (1f / crouchSmoothTime));

            if (_isGrounded)
            {
                float heightDiff = controller.height - prevHeight;
                if (heightDiff > 0) controller.Move(Vector3.up * heightDiff);
            }

            float currentRelativeHeight = _cameraBaseHeight * (controller.height / _originalHeight);
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, currentRelativeHeight, Time.deltaTime * (1f / crouchSmoothTime));
            playerCamera.localPosition = camPos;
        }

        private void HandleCameraControl()
        {
            float mouseX = _lookInput.x * sensitivity;
            float mouseY = _lookInput.y * sensitivity;

            _smoothInputX = Mathf.Lerp(_smoothInputX, mouseX, Time.deltaTime * cameraWeight);

            _targetYaw += mouseX;
            _targetPitch -= mouseY;
            _targetPitch = Mathf.Clamp(_targetPitch, maxLookDownAngle, maxLookUpAngle);

            if (useCameraSmoothing)
            {
                float smoothFactor = Mathf.Clamp01(Time.deltaTime * cameraWeight);
                _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, smoothFactor);
                _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, smoothFactor);
            }
            else
            {
                _currentYaw = _targetYaw;
                _currentPitch = _targetPitch;
            }

            transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            playerCamera.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentTilt);
        }

        private void HandleCameraTilt()
        {
            if (!enableCameraTilt) { _currentTilt = 0; return; }

            float keyboardTilt = -_moveInput.x * tiltAmount;
            float mouseTilt = -_smoothInputX * turnTiltAmount;
            float targetTiltTotal = keyboardTilt + mouseTilt;

            if (_currentMovementState == MovementState.Running) targetTiltTotal *= runTiltMultiplier;
            if (_isCrouching) targetTiltTotal *= crouchTiltMultiplier;

            targetTiltTotal = Mathf.Clamp(targetTiltTotal, -maxTotalTilt, maxTotalTilt);
            _currentTilt = Mathf.Lerp(_currentTilt, targetTiltTotal, Time.deltaTime * tiltSmoothness);
        }

        private void HandleFovChange()
        {
            if (!enableRunFov || _playerCameraComponent == null) return;
            bool isActuallyRunning = _sprintPressed && _moveInput.sqrMagnitude > 0.01f;
            _playerCameraComponent.fieldOfView = Mathf.Lerp(_playerCameraComponent.fieldOfView, isActuallyRunning ? runFov : normalFov, Time.deltaTime * fovChangeSpeed);
        }

        private void HandleHeadBob()
        {
            float moveMag = _moveInput.magnitude;
            float currentCamH = _cameraBaseHeight * (controller.height / _originalHeight);

            if (!_isGrounded || moveMag <= 0.1f)
            {
                _timer = 0;
                playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, new Vector3(0, currentCamH, 0), Time.deltaTime * bobSmoothness);
                return;
            }

            float freq = (_currentMovementState == MovementState.Running) ? runBobFrequency : (_isCrouching ? crouchBobFrequency : walkBobFrequency);
            _timer += Time.deltaTime * freq;

            Vector3 newPos = new Vector3(
                Mathf.Cos(_timer * 0.5f) * bobAmountX,
                currentCamH + Mathf.Sin(_timer) * bobAmountY,
                0
            );
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, newPos, Time.deltaTime * bobSmoothness);
        }

        private void ReadInput()
        {
            _moveInput = ReadMoveInput();
            _lookInput = ReadLookInput();
            _jumpPressedThisFrame = IsJumpPressedThisFrame();
            _sprintPressed = IsSprintPressed();
            _crouchPressed = IsCrouchPressed();
            _crouchPressedThisFrame = IsCrouchPressedThisFrame();
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 move = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
            }

            if (Gamepad.current != null)
                move += Gamepad.current.leftStick.ReadValue();

            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        private Vector2 ReadLookInput()
        {
            if (requireCursorLockForLook && Cursor.lockState != CursorLockMode.Locked)
                return Vector2.zero;

            Vector2 look = _queuedMouseDelta;
            _queuedMouseDelta = Vector2.zero;

            Mouse mouse = GetActiveMouse();
            if (look == Vector2.zero && mouse != null)
                look = mouse.delta.ReadValue();

            if (look == Vector2.zero && mouse != null)
            {
                Vector2 mousePosition = mouse.position.ReadValue();
                if (_hasLastMousePosition)
                    look = mousePosition - _lastMousePosition;

                _lastMousePosition = mousePosition;
                _hasLastMousePosition = true;
            }

            if (Gamepad.current != null)
                look += Gamepad.current.rightStick.ReadValue() * 15f;

            return look;
        }

        private static void EnableMouseDevices()
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is not Mouse mouse)
                    continue;

                if (!mouse.enabled)
                    InputSystem.EnableDevice(mouse);
            }
        }

        private static Mouse GetActiveMouse()
        {
            if (Mouse.current != null)
                return Mouse.current;

            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Mouse mouse)
                    return mouse;
            }

            return null;
        }

        private static int GetMouseDeviceCount()
        {
            int count = 0;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is Mouse)
                    count++;
            }

            return count;
        }

        private void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device is not Mouse mouse)
                return;

            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            _queuedMouseDelta += mouse.delta.ReadValueFromEvent(eventPtr);
        }

        private static bool IsJumpPressedThisFrame()
        {
            return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private static bool IsSprintPressed()
        {
            return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        }

        private static bool IsCrouchPressed()
        {
            return (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed))
                || (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
        }

        private static bool IsCrouchPressedThisFrame()
        {
            return (Keyboard.current != null && (Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        }

        private static void HandleCursorLock()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateDebugText()
        {
            _debugText =
                $"Speed: {CurrentHorizontalSpeed:0.00} m/s\n" +
                $"State: {_currentMovementState}";
        }

        private void OnGUI()
        {
            if (!showInputDebug) return;

            _debugStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 28,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(16, 16, 12, 12)
            };

            GUI.Box(new Rect(12f, 12f, 320f, 100f), _debugText, _debugStyle);
        }

        /// <summary>
        /// Checks for obstacles above the player when trying to stand up.
        /// </summary>
        /// <returns>True if there is enough space to stand.</returns>
        public bool CanStandUp()
        {
            if (standingHeightMarker == null) return true;
            int hitCount = Physics.OverlapSphereNonAlloc(standingHeightMarker.transform.position, standingCheckRadius, _standUpHits, obstacleLayerMask);
            for (var i = 0; i < hitCount; i++)
            {
                Collider col = _standUpHits[i];
                if (col.transform.IsChildOf(transform) || col.transform == transform || col.isTrigger) continue;
                if (col.bounds.min.y < standingHeightMarker.transform.position.y + minStandingClearance) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (standingHeightMarker != null)
            {
                Gizmos.color = CanStandUp() ? Color.green : Color.red;
                Gizmos.DrawWireSphere(standingHeightMarker.transform.position, standingCheckRadius);
            }
        }
    }
}
