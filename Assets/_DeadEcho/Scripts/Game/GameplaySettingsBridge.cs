using DeadEcho.Core;
using DeadEcho.UI;
using Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DeadEcho.Game
{
    /// <summary>
    /// Conecta Opciones (y la dificultad de Nueva partida) con el juego RE4 sin que el juego dependa de los menus:
    /// sensibilidad, invertir Y, campo de vision, balanceo, HUD, ajuste de imagen de la camara y vibracion al recibir daño.
    /// Se crea solo al arrancar y vuelve a aplicar todo en cada escena y cada vez que cambian las opciones.
    /// </summary>
    public class GameplaySettingsBridge : MonoBehaviour
    {
        private static GameplaySettingsBridge _instance;

        // valores originales del controlador (las opciones se aplican como factor sobre ellos)
        private float _baseSensitivity = -1f;
        private float _baseBob = -1f;
        private float _sprintFovExtra = 6f;

        private FirstPersonController _fps;
        private CanvasGroup _hudGroup;
        private float _lastHitSeen = -1f;
        private float _rumbleUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[DeadEcho_AjustesJuego]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameplaySettingsBridge>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SettingsService.OnApplied += OnApplied;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SettingsService.OnApplied -= OnApplied;
            StopRumble();
        }

        private void Start() => ApplyAll();

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            _fps = null;
            _hudGroup = null;
            _baseSensitivity = _baseBob = -1f;
            _lastHitSeen = -1f;
            // el HUD y Leon se crean en su Start: aplicar un fotograma despues
            Invoke(nameof(ApplyAll), 0.05f);
        }

        private void OnApplied(GameSettings s) => ApplyAll();

        private void ApplyAll()
        {
            GameSettings s = SettingsService.Current;

            // ---- Leon (controles y camara)
            if (_fps == null) _fps = FirstPersonController.Instance;
            if (_fps != null)
            {
                if (_baseSensitivity < 0f)
                {
                    _baseSensitivity = _fps.mouseSensitivity;
                    _baseBob = _fps.bobAmount;
                    _sprintFovExtra = Mathf.Max(0f, _fps.sprintFov - _fps.baseFov);
                }
                // 50 = la sensibilidad del proyecto; cada 25 puntos la duplica o la reduce a la mitad
                _fps.mouseSensitivity = _baseSensitivity * Mathf.Pow(2f, (s.sensitivity - 50) / 25f);
                _fps.invertY = s.invertY;
                _fps.baseFov = s.fov;
                _fps.sprintFov = s.fov + _sprintFovExtra;
                _fps.bobAmount = s.headBob && !s.reduceMotion ? _baseBob : 0f;
            }

            // ---- Ajuste de imagen en la camara del juego (brillo, grano, daltonismo)
            Camera cam = _fps != null && _fps.ViewCamera != null ? _fps.ViewCamera : Camera.main;
            if (cam != null)
            {
                ScreenGrade g = ScreenGrade.On(cam);
                g.followSettings = true;
                g.grainAtMax = 0.09f;
            }

            // ---- HUD
            ApplyHud(s);
        }

        private void ApplyHud(GameSettings s)
        {
            if (_hudGroup == null)
            {
                GameObject hud = GameObject.Find("RE4_HUD");
                if (hud == null) return;
                _hudGroup = hud.GetComponent<CanvasGroup>();
                if (_hudGroup == null) _hudGroup = hud.AddComponent<CanvasGroup>();
                _hudGroup.blocksRaycasts = false;
            }
            bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            // al morir se ve siempre (la pantalla de muerte va en el mismo Canvas)
            _hudGroup.alpha = dead ? 1f : s.showHud ? Mathf.Clamp01(s.hudOpacity / 100f) : 0f;
        }

        private void Update()
        {
            GameSettings s = SettingsService.Current;
            if (_hudGroup != null && PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead && _hudGroup.alpha < 1f) _hudGroup.alpha = 1f;

            // ---- Vibracion al recibir daño (solo si se esta jugando con mando)
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null)
            {
                if (_lastHitSeen < 0f) _lastHitSeen = ph.LastHitTime;
                if (ph.LastHitTime > _lastHitSeen)
                {
                    _lastHitSeen = ph.LastHitTime;
                    if (s.vibration && _fps != null && _fps.GamepadActive)
                    {
                        Gamepad.current.SetMotorSpeeds(0.35f, 0.75f);
                        _rumbleUntil = Time.unscaledTime + 0.25f;
                    }
                }
            }
            if (_rumbleUntil > 0f && (Time.unscaledTime >= _rumbleUntil || Time.timeScale <= 0f)) StopRumble();
        }

        private void StopRumble()
        {
            _rumbleUntil = 0f;
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
}
