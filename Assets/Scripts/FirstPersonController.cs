using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    /// <summary>
    /// Control en primera persona de Leon: movimiento, mirada, apuntado, sprint,
    /// retroceso de camara, temblor (trauma) y balanceo al caminar.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController Instance { get; private set; }

        [Header("Movimiento")]
        public float walkSpeed = 4.2f;
        public float runSpeed = 6.8f;
        public float aimSpeed = 1.7f;
        public float backwardMultiplier = 0.7f;
        public float acceleration = 12f;
        public float gravity = -18f;

        [Header("Mirada")]
        public Transform cameraHolder;
        public float mouseSensitivity = 2.0f;
        public float aimSensitivityMultiplier = 0.55f;
        public float maxPitch = 85f;
        public float minPitch = -85f;

        [Header("Camara")]
        public Camera weaponCamera;
        public float baseFov = 68f;
        public float sprintFov = 74f;
        public float fovLerp = 10f;
        public float bobAmount = 0.035f;
        public float bobFrequency = 1.9f;

        [Header("Compatibilidad")]
        public Transform weaponHolder;

        public bool IsAiming { get; set; }
        public bool IsSprinting { get; private set; }
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public Vector3 Velocity => _velocity;
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float BobPhase { get; private set; }
        public float TargetFovOverride { get; set; }
        public Camera ViewCamera => _camera;

        /// <summary>Cuando es mayor que cero el jugador no se mueve ni mira (maletin, agarre, muerte, patada).</summary>
        public int InputLocks { get; private set; }
        public bool InputLocked => InputLocks > 0;
        public bool LookLocked { get; set; }
        /// <summary>(corriendo) en cada paso; si nadie escucha se usa el sonido procedimental.</summary>
        public event System.Action<bool> OnFootstep;
        public bool IsQuickTurning => _quickTurnLeft > 0.01f;

        /// <summary>Frame en el que se cerro un menu con ESC (para no soltar el cursor ese mismo frame).</summary>
        public int IgnoreEscapeFrame { get; set; } = -1;

        private CharacterController _cc;
        private Camera _camera;
        private float _pitch;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private Vector3 _camBaseLocalPos;

        private Vector2 _recoilTarget;
        private Vector2 _recoilCurrent;

        private float _trauma;
        private float _rollOffset;
        private float _rollTarget;

        private Transform _forcedLook;
        private float _stepTimer;
        private float _quickTurnLeft;
        private bool _quickTurnArmed = true;

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            if (cameraHolder != null) _camera = cameraHolder.GetComponentInChildren<Camera>();
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _camBaseLocalPos = _camera.transform.localPosition;
            LockCursor(true);
        }

        public void PushInputLock() => InputLocks++;
        public void PopInputLock() => InputLocks = Mathf.Max(0, InputLocks - 1);

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void AddRecoil(float pitch, float yaw) => _recoilTarget += new Vector2(pitch, yaw);

        public void AddShake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void SetRoll(float degrees) => _rollTarget = degrees;

        public void ForceLookAt(Transform target) => _forcedLook = target;

        /// <summary>Empujon (golpes): limitado para que Leon no se deslice solo.</summary>
        public void Push(Vector3 impulse)
        {
            _velocity += Vector3.ClampMagnitude(new Vector3(impulse.x, 0f, impulse.z), 2.5f);
            _velocity = Vector3.ClampMagnitude(_velocity, runSpeed);
        }

        /// <summary>
        /// Zona muerta del mando: sin ella, un stick un poco desgastado (o un mando conectado sin usar)
        /// hacia que Leon caminara o girara solo.
        /// </summary>
        private static Vector2 DeadZone(Vector2 v, float dead = 0.25f)
        {
            float m = v.magnitude;
            if (m < dead) return Vector2.zero;
            return v / m * Mathf.InverseLerp(dead, 1f, Mathf.Min(1f, m));
        }

        private void Update()
        {
            bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;

            if (!dead && !InputLocked)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Time.frameCount != IgnoreEscapeFrame) LockCursor(false);
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            }

            UpdateActiveDevice();
            if (!dead && !InputLocked && !LookLocked) HandleLook();
            else LookDelta = Vector2.zero;

            if (_forcedLook != null) HandleForcedLook();

            HandleMovement(dead || InputLocked);
            HandleCamera();
        }

        /// <summary>El mando solo mueve a Leon mientras sea el dispositivo en uso.</summary>
        public bool GamepadActive => _padActive && Gamepad.current != null;
        private bool _padActive;

        /// <summary>
        /// Decide si se esta jugando con mando o con teclado/raton. Un mando conectado sin usar (o con el
        /// stick algo desgastado / un mando virtual de Steam o DS4Windows) hacia que Leon caminara o girara
        /// solo: ahora sus sticks se ignoran hasta que se pulsa un boton o se mueve un stick con decision,
        /// y en cuanto se toca el teclado o el raton se vuelve a ignorar.
        /// </summary>
        private void UpdateActiveDevice()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
                (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 9f)))
            {
                _padActive = false;
                return;
            }
            var pad = Gamepad.current;
            if (pad == null) { _padActive = false; return; }
            if (_padActive) return;
            bool button = pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                          pad.buttonNorth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                          pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
                          pad.selectButton.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame;
            bool stick = pad.leftStick.ReadValue().magnitude > 0.7f || pad.rightStick.ReadValue().magnitude > 0.7f;
            if (button || stick) _padActive = true;
        }

        private void HandleLook()
        {
            Vector2 delta = Vector2.zero;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                delta = Mouse.current.delta.ReadValue() * 0.1f * mouseSensitivity;
            }
            if (GamepadActive)
            {
                delta += DeadZone(Gamepad.current.rightStick.ReadValue()) * 60f * Time.deltaTime * mouseSensitivity;
            }
            if (IsAiming) delta *= aimSensitivityMultiplier;
            LookDelta = delta;

            transform.Rotate(Vector3.up * delta.x);
            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
        }

        private void HandleForcedLook()
        {
            if (cameraHolder == null) return;
            Vector3 to = _forcedLook.position + Vector3.up * 1.55f - cameraHolder.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 8f);
            }
            float pitch = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
            _pitch = Mathf.Lerp(_pitch, pitch, Time.deltaTime * 8f);
        }

        private void HandleMovement(bool locked)
        {
            Vector2 input = Vector2.zero;
            bool sprintKey = false;

            if (!locked)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.wKey.isPressed) input.y += 1f;
                    if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                    if (Keyboard.current.dKey.isPressed) input.x += 1f;
                    if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                    sprintKey = Keyboard.current.leftShiftKey.isPressed;
                }
                if (GamepadActive)
                {
                    Vector2 stick = DeadZone(Gamepad.current.leftStick.ReadValue());
                    if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
                    sprintKey |= Gamepad.current.leftStickButton.isPressed;
                }
            }
            input = Vector2.ClampMagnitude(input, 1f);
            MoveInput = input;

            // Giro rapido de 180 grados (atras + correr), como en RE4
            if (!locked && input.y < -0.5f && sprintKey && _quickTurnArmed && !IsAiming)
            {
                _quickTurnLeft = 180f;
                _quickTurnArmed = false;
            }
            if (!sprintKey || input.y > -0.2f) _quickTurnArmed = true;
            if (_quickTurnLeft > 0f)
            {
                float step = Mathf.Min(_quickTurnLeft, 900f * Time.deltaTime);
                transform.Rotate(Vector3.up * step);
                _quickTurnLeft -= step;
                input = Vector2.zero;
            }

            IsSprinting = sprintKey && !IsAiming && input.y > 0.3f;
            float speed = IsAiming ? aimSpeed : IsSprinting ? runSpeed : walkSpeed;
            if (input.y < 0f) speed *= backwardMultiplier;

            Vector3 desired = (transform.forward * input.y + transform.right * input.x) * speed;
            _velocity = Vector3.Lerp(_velocity, desired, Time.deltaTime * acceleration);

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 move = _velocity + Vector3.up * _verticalVelocity;
            if (_cc.enabled) _cc.Move(move * Time.deltaTime);

            float planar = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            if (_cc.isGrounded && planar > 0.5f)
            {
                BobPhase += Time.deltaTime * bobFrequency * (planar / walkSpeed) * Mathf.PI * 2f;
                _stepTimer -= Time.deltaTime * (planar / walkSpeed);
                if (_stepTimer <= 0f)
                {
                    _stepTimer = 0.5f;
                    if (OnFootstep != null) OnFootstep(IsSprinting);
                    else RE4.ProceduralAudio.PlayAt(RE4.ProceduralAudio.Impact(0.4f), transform.position, IsSprinting ? 0.22f : 0.12f, 0.2f, 15f);
                }
            }
        }

        private void HandleCamera()
        {
            // Retroceso: sube rapido y se recupera parcialmente
            _recoilCurrent = Vector2.Lerp(_recoilCurrent, _recoilTarget, Time.deltaTime * 25f);
            _recoilTarget = Vector2.Lerp(_recoilTarget, Vector2.zero, Time.deltaTime * 6f);

            if (cameraHolder != null)
            {
                cameraHolder.localEulerAngles = new Vector3(_pitch - _recoilCurrent.x, _recoilCurrent.y, 0f);
            }

            if (_camera == null) return;

            _trauma = Mathf.Max(0f, _trauma - Time.deltaTime * 1.6f);
            float shake = _trauma * _trauma;
            float t = Time.time * 25f;
            Vector3 shakeRot = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 8f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 8f,
                (Mathf.PerlinNoise(t, t) - 0.5f) * 10f) * shake;

            _rollOffset = Mathf.Lerp(_rollOffset, _rollTarget, Time.deltaTime * 10f);

            float planar = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            float bobScale = Mathf.Clamp01(planar / walkSpeed) * (IsAiming ? 0.25f : 1f) * (IsSprinting ? 1.5f : 1f);
            Vector3 bob = new Vector3(Mathf.Sin(BobPhase * 0.5f) * bobAmount * 0.6f, Mathf.Abs(Mathf.Sin(BobPhase * 0.5f)) * bobAmount, 0f) * bobScale;

            _camera.transform.localPosition = _camBaseLocalPos + bob;
            _camera.transform.localRotation = Quaternion.Euler(shakeRot + new Vector3(0f, 0f, _rollOffset));

            float targetFov = TargetFovOverride > 1f ? TargetFovOverride : IsSprinting ? sprintFov : baseFov;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, Time.deltaTime * fovLerp);
        }

        /// <summary>Magnitud de la velocidad en el plano XZ y limpieza de retroceso acumulado.</summary>
        public float PlanarSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        public void ClearRecoil()
        {
            _recoilTarget = Vector2.zero;
            _recoilCurrent = Vector2.zero;
        }
    }
}
