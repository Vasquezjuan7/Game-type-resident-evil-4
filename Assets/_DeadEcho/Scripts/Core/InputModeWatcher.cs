using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DeadEcho.Core
{
    public enum InputMode
    {
        TecladoRaton,
        Mando
    }

    /// <summary>
    /// Detecta el ultimo dispositivo usado (teclado/raton o mando) para cambiar los iconos de KeyHint,
    /// y guarda la hora del ultimo input (lo usa IdleWatcher para el modo cine).
    /// </summary>
    public class InputModeWatcher : MonoBehaviour
    {
        public static InputModeWatcher Instance { get; private set; }
        public static event Action<InputMode> OnModeChanged;

        public static InputMode Mode { get; private set; } = InputMode.TecladoRaton;
        /// <summary>Tiempo real (sin escala) del ultimo input de cualquier dispositivo.</summary>
        public static float LastInputTime { get; private set; }

        private const float StickDeadZone = 0.35f;
        private IDisposable _anyButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[InputModeWatcher]");
            Instance = go.AddComponent<InputModeWatcher>();
            DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            // cualquier boton de cualquier dispositivo
            _anyButton = InputSystem.onAnyButtonPress.Call(OnButton);
            LastInputTime = Time.unscaledTime;
        }

        private void OnDisable()
        {
            _anyButton?.Dispose();
            _anyButton = null;
        }

        private void OnButton(InputControl control)
        {
            LastInputTime = Time.unscaledTime;
            SetMode(control.device is Gamepad ? InputMode.Mando : InputMode.TecladoRaton);
        }

        private void Update()
        {
            // movimientos que no son "botones": raton y sticks
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || Mathf.Abs(mouse.scroll.ReadValue().y) > 0.1f))
            {
                LastInputTime = Time.unscaledTime;
                SetMode(InputMode.TecladoRaton);
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().magnitude > StickDeadZone || pad.rightStick.ReadValue().magnitude > StickDeadZone))
            {
                LastInputTime = Time.unscaledTime;
                SetMode(InputMode.Mando);
            }
        }

        private static void SetMode(InputMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            OnModeChanged?.Invoke(mode);
        }

        /// <summary>Marca actividad a mano (por ejemplo al cerrar el modo cine).</summary>
        public static void Touch() => LastInputTime = Time.unscaledTime;
    }
}
