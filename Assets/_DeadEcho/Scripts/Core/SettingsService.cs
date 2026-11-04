using System;
using System.IO;
using UnityEngine;

namespace DeadEcho.Core
{
    /// <summary>
    /// Todas las opciones del juego (pestañas de Opciones). Los valores por defecto son los de
    /// un objeto nuevo: asi se sabe cuando mostrar el rombo ◆ y que restablecer.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        // Graficos
        public int resolutionWidth;      // 0 = resolucion nativa
        public int resolutionHeight;
        public int screenMode = 0;       // 0 completa, 1 ventana sin bordes, 2 ventana
        public int fpsLimit = 60;        // 30 / 60 / 120 / 144 / 0 (sin limite)
        public int qualityLevel = -1;    // -1 = el preajuste actual del proyecto
        public int shadows = 2;          // 0 desactivadas, 1 baja, 2 media, 3 alta
        public bool vSync = true;
        public int brightness = 50;      // 0..100
        public int filmGrain = 30;       // 0..100

        // Audio (0..100)
        public int masterVolume = 80;
        public int musicVolume = 70;
        public int sfxVolume = 80;
        public int voiceVolume = 90;
        public bool subtitles = true;

        // Controles
        public int sensitivity = 50;     // 0..100
        public bool invertY;
        public bool vibration = true;

        // Camara
        public int fov = 68;             // grados (el baseFov del FirstPersonController)
        public bool headBob = true;

        // HUD
        public bool showHud = true;
        public int hudOpacity = 100;     // 0..100

        // Idioma
        public string language = "es";

        // Accesibilidad
        public int textSize = 1;         // 0 pequeño, 1 normal, 2 grande
        public int colorBlindMode;       // 0 no, 1 protanopia, 2 deuteranopia, 3 tritanopia
        public bool reduceMotion;
        public bool holdToToggle;        // mantener (false) o pulsar (true)

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>Misma configuracion de pantalla (resolucion y modo).</summary>
        public bool SameDisplay(GameSettings o) =>
            o != null && o.resolutionWidth == resolutionWidth && o.resolutionHeight == resolutionHeight && o.screenMode == screenMode;

        /// <summary>Todos los campos iguales (para saber si hay cambios sin confirmar).</summary>
        public bool SameAs(GameSettings o) => o != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(o);

        /// <summary>Escala del texto de Accesibilidad (pequeño / normal / grande).</summary>
        public float TextScale => textSize <= 0 ? 0.85f : textSize >= 2 ? 1.25f : 1f;
    }

    /// <summary>
    /// Lee, guarda (JSON en Application.persistentDataPath) y aplica las opciones.
    /// Las pantallas modifican <see cref="Current"/> y llaman a <see cref="Apply"/> y <see cref="Save"/>.
    /// </summary>
    public static class SettingsService
    {
        public const string FileName = "settings.json";
        public static readonly GameSettings Defaults = new GameSettings();

        /// <summary>Se dispara tras aplicar las opciones (para el post-proceso, el HUD, etc.).</summary>
        public static event Action<GameSettings> OnApplied;

        private static GameSettings _current;

        public static GameSettings Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        /// <summary>Atajo muy usado por las animaciones de la interfaz.</summary>
        public static bool ReduceMotion => Current.reduceMotion;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Hay opciones guardadas por el jugador (si no, el juego conserva la configuracion del proyecto).</summary>
        public static bool HasSavedFile => File.Exists(FilePath);

        public static void Load()
        {
            _current = new GameSettings();
            try
            {
                if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), _current);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeadEcho] No se pudieron leer las opciones, se usan las de fabrica: " + e.Message);
                _current = new GameSettings();
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeadEcho] No se pudieron guardar las opciones: " + e.Message);
            }
        }

        /// <summary>Sustituye las opciones (por ejemplo al descartar cambios) sin guardar.</summary>
        public static void Set(GameSettings s)
        {
            _current = s != null ? s.Clone() : new GameSettings();
        }

        /// <summary>
        /// Aplica los valores reales al motor. El brillo, el grano y el modo daltonico los aplica ScreenGrade
        /// (y el resto de piezas del juego) al oir OnApplied. applyDisplay = false no toca resolucion ni modo
        /// de pantalla (Opciones los aplica solo al confirmar, con la cuenta atras de 15 s).
        /// </summary>
        public static void Apply(bool applyDisplay = true)
        {
            GameSettings s = Current;

            // Resolucion y modo de pantalla
            if (applyDisplay) ApplyDisplay(s);

            // Calidad
            if (s.qualityLevel >= 0 && s.qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != s.qualityLevel)
                QualitySettings.SetQualityLevel(s.qualityLevel, true);
            QualitySettings.shadows = s.shadows == 0 ? ShadowQuality.Disable : s.shadows == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = s.shadows <= 1 ? ShadowResolution.Low : s.shadows == 2 ? ShadowResolution.Medium : ShadowResolution.High;

            // FPS y V-Sync (con V-Sync activo Unity ignora targetFrameRate)
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Application.targetFrameRate = s.fpsLimit > 0 ? s.fpsLimit : -1;

            ApplyLight();
        }

        /// <summary>
        /// Solo lo que no cambia la configuracion grafica del proyecto: volumenes, idioma y avisos (OnApplied).
        /// Se usa al arrancar cuando el jugador aun no ha guardado opciones.
        /// </summary>
        public static void ApplyLight()
        {
            GameSettings s = Current;
            // Efectos = todo lo que pasa por el listener; musica y voces aplican su canal (AudioChannels)
            AudioListener.volume = AudioChannels.Effects;
            Loc.ApplyLanguage(s.language);
            OnApplied?.Invoke(s);
        }

        public static void ApplyDisplay(GameSettings s)
        {
            FullScreenMode mode = s.screenMode == 0 ? FullScreenMode.ExclusiveFullScreen
                : s.screenMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = s.resolutionWidth > 0 ? s.resolutionWidth : Screen.currentResolution.width;
            int h = s.resolutionHeight > 0 ? s.resolutionHeight : Screen.currentResolution.height;
            if (!Application.isEditor && (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode))
                Screen.SetResolution(w, h, mode);
        }

        public static void ResetAll()
        {
            _current = new GameSettings();
        }
    }
}
