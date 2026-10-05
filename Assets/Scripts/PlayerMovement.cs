using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSmoothTime = 0.12f;
        [SerializeField] private float speedDampTime = 0.1f;

        [Header("Input References")]
        [Tooltip("Opcional: Asigna la accion de movimiento (Vector2) del New Input System.")]
        [SerializeField] private InputActionReference moveAction;

        private CharacterController _controller;
        private Animator _animator;
        private Transform _cameraTransform;
        
        private float _turnSmoothVelocity;
        private Vector3 _verticalVelocity;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }
        }

        private void Update()
        {
            HandleMovement();
            ApplyGravity();
        }

        private void HandleMovement()
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            Vector2 input = Vector2.zero;

            // 1. Probar New Input System si esta asignado
            if (moveAction != null && moveAction.action != null && moveAction.action.enabled)
            {
                input = moveAction.action.ReadValue<Vector2>();
            }

            // 2. Fallback robusto con New Input System directo desde Keyboard si input es 0
            if (input == Vector2.zero && Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                input = new Vector2(x, y).normalized;
            }

            // 3. Fallback con Legacy Input por si acaso
            if (input == Vector2.zero)
            {
                try
                {
                    input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
                }
                catch { }
            }

            // Animacion
            float speedMag = input.magnitude;
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, speedMag, speedDampTime, Time.deltaTime);
            }

            // Movimiento relativo a camara o directo
            if (input.magnitude >= 0.1f)
            {
                float camY = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;
                float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + camY;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                _controller.Move(moveDir.normalized * (moveSpeed * Time.deltaTime));
            }
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity.y < 0)
            {
                _verticalVelocity.y = -2f;
            }

            _verticalVelocity.y += Physics.gravity.y * Time.deltaTime;
            _controller.Move(_verticalVelocity * Time.deltaTime);
        }

        /// <summary>Indica si el controlador está apoyado en el suelo y devuelve la velocidad base configurada.</summary>
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public float ConfiguredMoveSpeed => moveSpeed;
    }
}
