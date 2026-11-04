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
        [Tooltip("Opciones › Controles › Invertir eje Y (lo fija Dead Echo).")]
        public bool invertY;
        public float maxPitch = 85f;
        public float minPitch = -85f;

        [Header("Camara")]
        public Camera weaponCamera;
        public float baseFov = 68f;
        public float sprintFov = 74f;
        public float fovLerp = 10f;
        public float bobAmount = 0.035f;
        public float bobFrequency = 1.9f;

        [Header("Resistencia (correr cansa)")]
        [Tooltip("Segundos de carrera con la resistencia llena.")]
        public float sprintDuration = 6f;
        public float staminaRegen = 0.22f;
        public float staminaRegenDelay = 0.9f;
        [Tooltip("Tras agotarse no se puede volver a correr hasta recuperar este nivel.")]
        public float exhaustedRecover = 0.35f;

        [Header("Agacharse (Ctrl mantener / C alternar)")]
        public float crouchHeight = 1.15f;
        public float crouchSpeed = 1.9f;
        public float crouchTransition = 9f;

        [Header("Salto y caidas")]
        public float jumpHeight = 0.85f;
        public float jumpStaminaCost = 0.15f;
        [Tooltip("Velocidad de caida a partir de la cual Leon se hace daño.")]
        public float fallDamageSpeed = 11f;
        public float fallDamagePerUnit = 9f;

        [Header("Realismo")]
        [Tooltip("Inclinacion de camara al moverse de lado (grados).")]
        public float strafeTilt = 1.6f;
        [Tooltip("Velocidad en estado Peligro (cojera).")]
        public float dangerSpeedMultiplier = 0.72f;
        public float cautionSpeedMultiplier = 0.92f;
        [Tooltip("Radio de ruido de cada paso al correr / al caminar (los aldeanos lo oyen).")]
        public float sprintNoiseRadius = 9f;
        public float walkNoiseRadius = 2.5f;

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

        /// <summary>Resistencia 0..1 (correr y saltar la gastan).</summary>
        public float Stamina { get; private set; } = 1f;
        /// <summary>Se agoto la resistencia: no se puede correr hasta recuperar un poco.</summary>
        public bool IsExhausted { get; private set; }
        /// <summary>Cansancio 0..1 (para temblor del arma, respiracion y HUD).</summary>
        public float Fatigue => Mathf.Clamp01(1f - Stamina / 0.6f);
        public bool IsCrouching { get; private set; }
        /// <summary>0 = de pie, 1 = agachado del todo (transicion suave).</summary>
        public float CrouchBlend { get; private set; }

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

        private float _standHeight;
        private float _ccBottom;
        private Vector3 _holderBaseLocalPos;
        private bool _crouchToggled;
        private float _lastStaminaUse = -10f;
        private float _breathTimer;
        private int _breathVariant;
        private bool _wasGrounded = true;
        private float _airMaxFallSpeed;
        private float _landDip;
        private float _landDipVel;
        private float _strafeRoll;
        private float _limpPhase;

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _standHeight = _cc.height;
            _ccBottom = _cc.center.y - _cc.height * 0.5f;
            if (cameraHolder != null) _holderBaseLocalPos = cameraHolder.localPosition;
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
            if (invertY) delta.y = -delta.y;
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
            bool crouchHeld = false;
            bool jumpPressed = false;
            bool quickTurnPressed = false;
            var kb = Keyboard.current;

            if (!locked)
            {
                if (kb != null)
                {
                    if (kb.wKey.isPressed) input.y += 1f;
                    if (kb.sKey.isPressed) input.y -= 1f;
                    if (kb.dKey.isPressed) input.x += 1f;
                    if (kb.aKey.isPressed) input.x -= 1f;
                    sprintKey = kb.leftShiftKey.isPressed;
                    crouchHeld = kb.leftCtrlKey.isPressed;
                    if (kb.cKey.wasPressedThisFrame) _crouchToggled = !_crouchToggled;
                    jumpPressed = kb.spaceKey.wasPressedThisFrame;
                    quickTurnPressed = kb.qKey.wasPressedThisFrame;
                }
                if (GamepadActive)
                {
                    var pad = Gamepad.current;
                    Vector2 stick = DeadZone(pad.leftStick.ReadValue());
                    if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
                    sprintKey |= pad.leftStickButton.isPressed;
                    if (pad.rightStickButton.wasPressedThisFrame) _crouchToggled = !_crouchToggled;
                    quickTurnPressed |= pad.dpad.down.wasPressedThisFrame;
                }
            }
            input = Vector2.ClampMagnitude(input, 1f);
            MoveInput = input;

            // Giro rapido de 180 grados (Q / cruceta abajo). Antes era "atras + correr" como en RE4,
            // pero con WASD hacia que Leon girara solo al pulsar S y Shift a la vez.
            if (!locked && quickTurnPressed && !IsAiming && _quickTurnLeft <= 0f)
            {
                _quickTurnLeft = 180f;
                RE4.ProceduralAudio.PlayAt(RE4.ProceduralAudio.Swoosh(), transform.position, 0.12f, 0.1f, 6f);
            }
            if (_quickTurnLeft > 0f)
            {
                float step = Mathf.Min(_quickTurnLeft, 900f * Time.deltaTime);
                transform.Rotate(Vector3.up * step);
                _quickTurnLeft -= step;
                input *= 0.3f;
            }

            // --- Agacharse ---
            // Correr hacia delante levanta a Leon; si hay techo encima se queda agachado.
            if (sprintKey && input.y > 0.3f) _crouchToggled = false;
            bool wantCrouch = !locked ? (crouchHeld || _crouchToggled) : IsCrouching;
            if (!wantCrouch && IsCrouching && !HasHeadroom()) wantCrouch = true;
            IsCrouching = wantCrouch;
            CrouchBlend = Mathf.MoveTowards(CrouchBlend, IsCrouching ? 1f : 0f, Time.deltaTime * crouchTransition * 0.35f);
            ApplyCrouchShape();

            // --- Estado de salud: cojera en Peligro ---
            HealthStatus status = PlayerHealth.Instance != null ? PlayerHealth.Instance.Status : HealthStatus.Fine;
            float healthMul = status == HealthStatus.Danger ? dangerSpeedMultiplier : status == HealthStatus.Caution ? cautionSpeedMultiplier : 1f;

            // --- Resistencia ---
            bool canSprint = !IsExhausted && Stamina > 0.02f && !IsCrouching;
            IsSprinting = sprintKey && !IsAiming && input.y > 0.3f && canSprint && _quickTurnLeft <= 0f;

            float planarNow = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            if (IsSprinting && planarNow > 1f)
            {
                float drain = status == HealthStatus.Danger ? 1.5f : 1f;
                Stamina = Mathf.Max(0f, Stamina - Time.deltaTime / Mathf.Max(0.5f, sprintDuration) * drain);
                _lastStaminaUse = Time.time;
            }
            else if (Time.time - _lastStaminaUse > staminaRegenDelay)
            {
                // Quieto se recupera mas rapido que caminando
                float regen = staminaRegen * (planarNow < 0.5f ? 1.4f : 1f) * (status == HealthStatus.Danger ? 0.6f : 1f);
                Stamina = Mathf.Min(1f, Stamina + regen * Time.deltaTime);
            }
            if (Stamina <= 0f) IsExhausted = true;
            else if (IsExhausted && Stamina >= exhaustedRecover) IsExhausted = false;
            UpdateBreathing();

            // --- Velocidad ---
            float speed = IsAiming ? aimSpeed : IsSprinting ? runSpeed : walkSpeed;
            if (IsCrouching) speed = Mathf.Min(speed, crouchSpeed);
            if (IsExhausted) speed *= 0.8f;
            speed *= healthMul;
            if (status == HealthStatus.Danger && planarNow > 0.5f)
            {
                // Paso irregular: la pierna herida frena cada zancada
                _limpPhase += Time.deltaTime * bobFrequency * Mathf.PI * (planarNow / walkSpeed);
                speed *= 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(_limpPhase));
            }
            if (input.y < 0f) speed *= backwardMultiplier;

            Vector3 desired = (transform.forward * input.y + transform.right * input.x) * speed;
            // En el aire casi no se puede cambiar de direccion
            float accel = _cc.isGrounded ? acceleration : acceleration * 0.15f;
            _velocity = Vector3.Lerp(_velocity, desired, Time.deltaTime * accel);

            // --- Salto ---
            if (jumpPressed && !locked && _cc.isGrounded && !IsAiming)
            {
                if (IsCrouching)
                {
                    _crouchToggled = false; // Espacio agachado = levantarse
                }
                else if (Stamina >= jumpStaminaCost)
                {
                    _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                    Stamina -= jumpStaminaCost;
                    _lastStaminaUse = Time.time;
                    _wasGrounded = false;
                    _airMaxFallSpeed = 0f;
                }
            }

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 move = _velocity + Vector3.up * _verticalVelocity;
            if (_cc.enabled) _cc.Move(move * Time.deltaTime);

            HandleLanding();

            // --- Pasos y ruido ---
            float planar = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            if (_cc.isGrounded && planar > 0.5f)
            {
                BobPhase += Time.deltaTime * bobFrequency * (planar / walkSpeed) * Mathf.PI * 2f;
                _stepTimer -= Time.deltaTime * (planar / walkSpeed);
                if (_stepTimer <= 0f)
                {
                    _stepTimer = IsCrouching ? 0.42f : 0.5f;
                    if (!IsCrouching || Random.value < 0.5f) // agachado los pasos casi no suenan
                    {
                        if (OnFootstep != null) OnFootstep(IsSprinting);
                        else RE4.ProceduralAudio.PlayAt(RE4.ProceduralAudio.Impact(0.4f), transform.position, IsSprinting ? 0.22f : 0.12f, 0.2f, 15f);
                    }
                    float noise = IsCrouching ? 0f : IsSprinting ? sprintNoiseRadius : walkNoiseRadius;
                    if (noise > 0f) RE4.NoiseSystem.Emit(transform.position, noise);
                }
            }
        }

        /// <summary>Ajusta la capsula y la altura de la camara segun lo agachado que este Leon.</summary>
        private void ApplyCrouchShape()
        {
            if (_cc == null) return;
            float h = Mathf.Lerp(_standHeight, Mathf.Min(crouchHeight, _standHeight), CrouchBlend);
            if (Mathf.Abs(_cc.height - h) > 0.0005f)
            {
                _cc.height = h;
                _cc.center = new Vector3(_cc.center.x, _ccBottom + h * 0.5f, _cc.center.z);
            }
            if (cameraHolder != null)
                cameraHolder.localPosition = _holderBaseLocalPos - Vector3.up * ((_standHeight - Mathf.Min(crouchHeight, _standHeight)) * CrouchBlend);
        }

        /// <summary>Hay sitio para ponerse de pie (nada encima de la cabeza).</summary>
        private bool HasHeadroom()
        {
            float r = _cc.radius * 0.9f;
            Vector3 bottom = transform.position + Vector3.up * (_ccBottom + r);
            Vector3 start = bottom + Vector3.up * Mathf.Max(0f, _cc.height - 2f * r);
            float dist = _standHeight - _cc.height + 0.05f;
            return !Physics.SphereCast(start, r, Vector3.up, out _, dist, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Al aterrizar: hundimiento de camara, golpe, ruido y daño si la caida es alta.</summary>
        private void HandleLanding()
        {
            bool grounded = _cc.isGrounded;
            if (!grounded)
            {
                _airMaxFallSpeed = Mathf.Max(_airMaxFallSpeed, -_verticalVelocity);
            }
            else if (!_wasGrounded)
            {
                float v = _airMaxFallSpeed;
                if (v > 3.5f)
                {
                    _landDip = Mathf.Min(0.16f, v * 0.012f);
                    AddShake(Mathf.Clamp01(v / 25f));
                    RE4.ProceduralAudio.PlayAt(RE4.ProceduralAudio.Impact(0.7f), transform.position, Mathf.Clamp01(v / 14f) * 0.35f, 0.15f, 15f);
                    RE4.NoiseSystem.Emit(transform.position, Mathf.Min(12f, v));
                }
                if (v > fallDamageSpeed && PlayerHealth.Instance != null)
                    PlayerHealth.Instance.TakeDamage((v - fallDamageSpeed) * fallDamagePerUnit, transform.position + Vector3.down);
                _airMaxFallSpeed = 0f;
            }
            _wasGrounded = grounded;
        }

        /// <summary>Jadeo cuando Leon esta cansado (mas seguido cuanto mas agotado).</summary>
        private void UpdateBreathing()
        {
            float f = IsExhausted ? 1f : Fatigue;
            if (f < 0.25f || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)) { _breathTimer = 0.3f; return; }
            _breathTimer -= Time.deltaTime;
            if (_breathTimer > 0f) return;
            _breathTimer = Mathf.Lerp(1.6f, 0.95f, f);
            _breathVariant = (_breathVariant + 1) % 3;
            Vector3 p = cameraHolder != null ? cameraHolder.position : transform.position + Vector3.up * 1.6f;
            RE4.ProceduralAudio.PlayAt(RE4.ProceduralAudio.Breath(_breathVariant), p, 0.05f + 0.13f * f, 0.08f, 4f);
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

            // Movimientos "de cuerpo": se apagan con Balanceo de camara = No o Reducir movimiento
            float bodyMotion = bobAmount > 0f ? 1f : 0f;

            // Inclinacion al moverse de lado
            float tiltTarget = -MoveInput.x * strafeTilt * (IsAiming ? 0.35f : 1f) * Mathf.Clamp01(planar / walkSpeed) * bodyMotion;
            _strafeRoll = Mathf.Lerp(_strafeRoll, tiltTarget, Time.deltaTime * 6f);

            // Hundimiento al aterrizar (muelle que vuelve a su sitio)
            _landDip = Mathf.SmoothDamp(_landDip, 0f, ref _landDipVel, 0.12f);

            // Respiracion agitada: la camara sube y baja con el jadeo
            float tired = IsExhausted ? 1f : Fatigue;
            float breathe = Mathf.Sin(Time.time * Mathf.Lerp(1.3f, 3.2f, tired)) * tired * bodyMotion;

            _camera.transform.localPosition = _camBaseLocalPos + bob + new Vector3(0f, -_landDip * bodyMotion + breathe * 0.008f, 0f);
            _camera.transform.localRotation = Quaternion.Euler(shakeRot + new Vector3(breathe * 0.45f, 0f, _rollOffset + _strafeRoll));

            float targetFov = TargetFovOverride > 1f ? TargetFovOverride : IsSprinting ? sprintFov : IsCrouching ? baseFov - 2f : baseFov;
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
