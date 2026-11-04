using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadEcho.Core
{
    /// <summary>Estados globales del juego (03_ESPECIFICACION_FUNCIONAL.md, seccion 1).</summary>
    public enum GameState
    {
        MainMenu,
        Loading,
        Playing,
        Paused,
        Inventory
    }

    /// <summary>
    /// Estado global de Dead Echo. Singleton que persiste entre escenas y avisa de cada cambio.
    /// Tambien centraliza la pausa del tiempo (Time.timeScale) con un contador, para que varias
    /// pantallas puedan pedir pausa a la vez sin pisarse.
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        /// <summary>(estado anterior, estado nuevo)</summary>
        public static event Action<GameState, GameState> OnStateChanged;

        public GameState State { get; private set; } = GameState.Playing;

        private int _pauseRequests;
        private float _timeScaleBeforePause = 1f;

        /// <summary>Se crea solo al arrancar, antes de cargar la primera escena.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[GameStateManager]");
            Instance = go.AddComponent<GameStateManager>();
            DontDestroyOnLoad(go);
            // aplicar las opciones guardadas (resolucion, FPS, V-Sync, volumen...) al arrancar;
            // si el jugador aun no ha guardado opciones, se respeta la configuracion del proyecto
            if (SettingsService.HasSavedFile) SettingsService.Apply();
            else SettingsService.ApplyLight(); // volumenes e idioma por defecto
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Al entrar en una escena se deduce el estado: menu principal, carga o juego.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            _pauseRequests = 0;
            Time.timeScale = 1f;
            if (scene.name == SceneNames.MainMenu) SetState(GameState.MainMenu);
            else if (scene.name == SceneNames.Loading) SetState(GameState.Loading);
            else SetState(GameState.Playing);
        }

        public void SetState(GameState next)
        {
            if (next == State) return;
            GameState prev = State;
            State = next;
            OnStateChanged?.Invoke(prev, next);
        }

        /// <summary>Pide detener el tiempo de juego (pausa, inventario...). Cada llamada debe tener su ReleasePause.</summary>
        public void RequestPause()
        {
            if (_pauseRequests == 0)
            {
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            _pauseRequests++;
        }

        public void ReleasePause()
        {
            if (_pauseRequests == 0) return;
            _pauseRequests--;
            if (_pauseRequests == 0) Time.timeScale = _timeScaleBeforePause;
        }

        public bool IsTimePaused => _pauseRequests > 0;
    }

    /// <summary>Nombres de escena usados por el flujo de pantallas.</summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Loading = "Loading";
        public const string Game = "SampleScene";
    }
}
