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

        [Header("CAMERA SETTINGS")]
        public float maxLookUpAngle = 90f;
        public float maxLookDownAngle = -90f;
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

        private bool _isGrounded;
        private bool _isCrouching;
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _jumpPressedThisFrame;
        private bool _sprintPressed;
        private bool _crouchPressed;
        private Vector2 _queuedMouseDelta;
        private Vector2 _lastMousePosition;
        private bool _hasLastMousePosition;
        private string _debugText;
        private MovementState _currentMovementState = MovementState.Walking;

        public enum MovementState { Walking, Running, Crouching, Jumping }

        public bool IsGrounded => _isGrounded;
        public bool IsCrouching => _isCrouching;
        public Vector2 MoveInput => _moveInput;
        public MovementState CurrentState => _currentMovementState;

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
            Vector3 origin = transform.position + Vector3.up * controller.radius;
            bool groundHit = Physics.SphereCast(origin, controller.radius * 0.8f, Vector3.down, out _, groundCheckDistance, groundLayer);
            _isGrounded = groundHit || controller.isGrounded;

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -5f;
            }
        }

        private void UpdateMovementState()
        {
            bool wantsToRun = _sprintPressed && _moveInput.y > 0.1f;

            if (!_isGrounded)
            {
                _currentMovementState = MovementState.Jumping;
                _currentMovementSpeed = wantsToRun ? runSpeed : speed;
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
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _isGrounded = false;
            }

            if (standingHeightMarker != null)
                standingHeightMarker.transform.position = new Vector3(transform.position.x, transform.position.y + _markerHeightOffset, transform.position.z);

            controller.Move(moveInput * _currentMovementSpeed * Time.deltaTime);
            _velocity.y += gravity * Time.deltaTime;
            controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleCrouchLogic()
        {
            _isCrouching = _crouchPressed || !CanStandUp();
            _targetHeight = _isCrouching ? crouchHeight : _originalHeight;
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

            float smoothFactor = Mathf.Clamp01(Time.deltaTime * cameraWeight);
            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, smoothFactor);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, smoothFactor);

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
            if (!enableRunFov || playerCamera.GetComponent<Camera>() == null) return;
            bool isActuallyRunning = _sprintPressed && _moveInput.y > 0.1f;
            Camera cam = playerCamera.GetComponent<Camera>();
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, isActuallyRunning ? runFov : normalFov, Time.deltaTime * fovChangeSpeed);
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
            Camera activeCamera = Camera.main;
            _debugText =
                $"PlayerController running: {enabled}\n" +
                $"Mouse current: {Mouse.current != null}\n" +
                $"Mouse count: {GetMouseDeviceCount()}\n" +
                $"Mouse enabled: {(GetActiveMouse() != null ? GetActiveMouse().enabled.ToString() : "NULL")}\n" +
                $"Mouse pos: {(GetActiveMouse() != null ? GetActiveMouse().position.ReadValue().ToString() : "NULL")}\n" +
                $"Update mode: {InputSystem.settings.updateMode}\n" +
                $"Game focused: {Application.isFocused}\n" +
                $"Require lock: {requireCursorLockForLook}\n" +
                $"Look input: {_lookInput}\n" +
                $"Move input: {_moveInput}\n" +
                $"Yaw/Pitch: {_currentYaw:0.0} / {_currentPitch:0.0}\n" +
                $"Player camera: {(playerCamera != null ? playerCamera.name : "NULL")}\n" +
                $"Camera.main: {(activeCamera != null ? activeCamera.name : "NULL")}\n" +
                $"Cursor: {Cursor.lockState}";
        }

        private void OnGUI()
        {
            if (!showInputDebug) return;

            GUI.Box(new Rect(12f, 12f, 360f, 170f), _debugText);
        }

        /// <summary>
        /// Checks for obstacles above the player when trying to stand up.
        /// </summary>
        /// <returns>True if there is enough space to stand.</returns>
        public bool CanStandUp()
        {
            if (standingHeightMarker == null) return true;
            Collider[] hits = Physics.OverlapSphere(standingHeightMarker.transform.position, standingCheckRadius, obstacleLayerMask);
            foreach (Collider col in hits)
            {
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
