using System;
using System.Collections.Generic;

namespace DeadEcho.Core
{
    /// <summary>
    /// Idioma de la interfaz de Dead Echo (Opciones › Idioma). El texto en español es la clave;
    /// en inglés se busca su traduccion (si no existe, se deja en español). Cambia en vivo:
    /// los textos creados con UIFx.Text llevan un <c>LocText</c> que escucha <see cref="OnChanged"/>.
    /// (Sin el paquete Unity Localization: el proyecto no lo tiene instalado.)
    /// </summary>
    public static class Loc
    {
        public const string Spanish = "es";
        public const string English = "en";

        public static event Action OnChanged;

        private static string _applied;

        public static bool IsEnglish => SettingsService.Current.language == English;

        /// <summary>Traduce un texto de la interfaz al idioma actual.</summary>
        public static string T(string es)
        {
            if (string.IsNullOrEmpty(es) || !IsEnglish) return es;
            return En.TryGetValue(es, out string en) ? en : es;
        }

        public static bool Has(string es) => !string.IsNullOrEmpty(es) && En.ContainsKey(es);

        /// <summary>Traduccion inglesa de una clave (o null).</summary>
        public static string ToEnglish(string es) => es != null && En.TryGetValue(es, out string en) ? en : null;

        /// <summary>Lo llama SettingsService.Apply: avisa solo si el idioma ha cambiado.</summary>
        internal static void ApplyLanguage(string lang)
        {
            if (_applied == null) { _applied = lang; return; }
            if (_applied == lang) return;
            _applied = lang;
            OnChanged?.Invoke();
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            // ---- comunes
            { "Confirmar", "Confirm" }, { "Volver", "Back" }, { "Cerrar", "Close" }, { "Seleccionar", "Select" },
            { "No, volver", "No, go back" }, { "Activado", "On" }, { "Desactivado", "Off" },
            { "PRESIONA CUALQUIER TECLA", "PRESS ANY KEY" },

            // ---- menu principal
            { "Continuar", "Continue" }, { "Nueva partida", "New game" }, { "Opciones", "Options" },
            { "Créditos", "Credits" }, { "Salir", "Quit" },
            { "Retoma la investigación desde tu último guardado.", "Resume the investigation from your last save." },
            { "No hay ninguna memoria guardada.", "There is no saved memory." },
            { "Empieza una nueva investigación en el pueblo de Valdeoscuro.", "Start a new investigation in the village of Valdeoscuro." },
            { "Gráficos, audio, controles y accesibilidad.", "Graphics, audio, controls and accessibility." },
            { "Las personas detrás de Dead Echo.", "The people behind Dead Echo." },
            { "Cierra el juego.", "Closes the game." },
            { "¿Salir de Dead Echo?", "Quit Dead Echo?" }, { "Sí, salir del juego", "Yes, quit the game" },
            { "HECHO CON AMOR POR", "MADE WITH LOVE BY" },

            // ---- carga
            { "CONSEJO", "TIP" }, { "CARGANDO", "LOADING" },

            // ---- pausa
            { "PAUSA", "PAUSE" }, { "Reanudar", "Resume" }, { "Reiniciar desde punto de control", "Restart from checkpoint" },
            { "Cargar partida", "Load game" }, { "Tutoriales", "Tutorials" }, { "Salir al menú principal", "Quit to main menu" },
            { "Vuelve a la partida donde la dejaste.", "Return to the game where you left off." },
            { "Vuelve a empezar desde el último punto de control.", "Start again from the last checkpoint." },
            { "Elige una memoria guardada (DEMO).", "Choose a saved memory (DEMO)." },
            { "Repasa los controles y las técnicas de supervivencia.", "Review the controls and survival techniques." },
            { "Abandona la partida y vuelve al menú principal.", "Leave the game and return to the main menu." },
            { "¿Reiniciar desde el punto de control?", "Restart from the checkpoint?" },
            { "Se perderá el progreso desde el último punto de control.", "Progress since the last checkpoint will be lost." },
            { "Sí, reiniciar", "Yes, restart" },
            { "¿Salir al menú principal?", "Quit to the main menu?" },
            { "Se perderá el progreso que no esté guardado.", "Unsaved progress will be lost." },
            { "Sí, salir al menú", "Yes, quit to menu" },
            { "TUTORIALES", "TUTORIALS" }, { "CARGAR PARTIDA", "LOAD GAME" },
            { "Guardar partida", "Save game" }, { "Elige una partida guardada.", "Choose a saved game." },
            { "Guarda la partida para seguirla más tarde (no durante el asedio).", "Save the game to continue later (not during the siege)." },
            { "¿Cargar la partida?", "Load the game?" }, { "Sí, cargar", "Yes, load" }, { "No se puede guardar ahora.", "You can't save now." },
            { "Elige la ranura donde se guardará la partida.", "Choose the slot where the game will be saved." },
            { "La partida se guarda sola en la ranura elegida (si tenía otra, se sobrescribe).", "The game saves itself in the chosen slot (overwriting any previous one)." },

            // ---- nueva partida
            { "NUEVA PARTIDA", "NEW GAME" }, { "Dificultad", "Difficulty" }, { "Ranura de memoria", "Memory slot" },
            { "Narrativa", "Narrative" }, { "Superviviente", "Survivor" }, { "Pesadilla", "Nightmare" },
            { "Para disfrutar la historia. Enemigos débiles, munición abundante.", "To enjoy the story. Weak enemies, plenty of ammo." },
            { "La experiencia recomendada. Recursos limitados.", "The recommended experience. Limited resources." },
            { "Enemigos agresivos, sin autoguardado. Cada bala cuenta.", "Aggressive enemies, no autosave. Every bullet counts." },
            { "Guardar progreso", "Save progress" },
            { "Demo: solo asigna una memoria falsa, no guarda datos reales.", "Demo: only assigns a fake memory, no real data is saved." },
            { "Asigna una memoria falsa a la ranura seleccionada (DEMO).", "Assigns a fake memory to the selected slot (DEMO)." },
            { "Comenzar →", "Start →" }, { "Empieza la partida con la dificultad elegida.", "Start the game with the chosen difficulty." },
            { "EQUIPO INICIAL", "STARTING GEAR" }, { "CON ESTA DIFICULTAD", "WITH THIS DIFFICULTY" },
            { "Daño recibido", "Damage taken" }, { "Autoguardado", "Autosave" },
            { "— Vacía —", "— Empty —" }, { "Sincronizando…", "Syncing…" },
            { "Elige esta ranura para guardar la memoria falsa.", "Choose this slot to store the fake memory." },
            { "AGENTE DEL GOBIERNO", "GOVERNMENT AGENT" },

            // ---- opciones: pestañas y secciones
            { "OPCIONES", "OPTIONS" }, { "CONTROLES", "CONTROLS" }, { "CÁMARA", "CAMERA" }, { "HUD", "HUD" },
            { "GRÁFICOS", "GRAPHICS" }, { "AUDIO", "AUDIO" }, { "IDIOMA", "LANGUAGE" }, { "ACCESIBILIDAD", "ACCESSIBILITY" },
            { "Pantalla", "Display" }, { "Calidad", "Quality" }, { "Imagen", "Image" }, { "Volumen", "Volume" },
            { "Texto", "Text" }, { "Ratón y mando", "Mouse and gamepad" }, { "Teclas", "Keys" }, { "Vista", "View" },
            { "Interfaz", "Interface" }, { "Lectura", "Reading" }, { "Color", "Color" }, { "Movimiento", "Motion" },
            { "Idioma de la interfaz", "Interface language" },

            // ---- opciones: filas
            { "Resolución", "Resolution" }, { "Modo de pantalla", "Display mode" }, { "Límite de FPS", "FPS limit" },
            { "Preajuste de calidad", "Quality preset" }, { "Sombras", "Shadows" }, { "V-Sync", "V-Sync" },
            { "Brillo", "Brightness" }, { "Grano de película", "Film grain" },
            { "General", "Master" }, { "Música", "Music" }, { "Efectos", "Effects" }, { "Voces", "Voices" },
            { "Subtítulos", "Subtitles" }, { "Sensibilidad", "Sensitivity" }, { "Invertir eje Y", "Invert Y axis" },
            { "Vibración", "Vibration" }, { "Reasignar teclas", "Remap keys" },
            { "Campo de visión", "Field of view" }, { "Balanceo al caminar", "Head bob" },
            { "Mostrar HUD", "Show HUD" }, { "Opacidad del HUD", "HUD opacity" },
            { "Idioma", "Language" }, { "Tamaño de texto", "Text size" }, { "Modo daltónico", "Colorblind mode" },
            { "Reducir movimiento", "Reduce motion" }, { "Apuntar", "Aim" },

            // ---- opciones: valores
            { "Pantalla completa", "Fullscreen" }, { "Ventana sin bordes", "Borderless window" }, { "Ventana", "Windowed" },
            { "Sin límite", "Unlimited" }, { "Nativa", "Native" }, { "Proyecto", "Project" },
            { "Baja", "Low" }, { "Media", "Medium" }, { "Alta", "High" },
            { "Pequeño", "Small" }, { "Normal", "Normal" }, { "Grande", "Large" },
            { "No", "Off" }, { "Protanopía", "Protanopia" }, { "Deuteranopía", "Deuteranopia" }, { "Tritanopía", "Tritanopia" },
            { "Mantener", "Hold" }, { "Pulsar", "Toggle" }, { "Ver", "View" },

            // ---- opciones: descripciones
            { "Resolución de la pantalla. Al confirmar se pide mantenerla.", "Screen resolution. You will be asked to keep it after confirming." },
            { "Cambia entre pantalla completa y ventana.", "Switch between fullscreen and windowed." },
            { "Fotogramas por segundo máximos (sin efecto con V-Sync activado).", "Maximum frames per second (no effect with V-Sync on)." },
            { "Calidad general de texturas, sombras y efectos.", "Overall quality of textures, shadows and effects." },
            { "Calidad de las sombras en tiempo real.", "Quality of real-time shadows." },
            { "Sincroniza los fotogramas con el monitor para evitar cortes.", "Syncs frames with the monitor to prevent tearing." },
            { "Brillo general de la imagen.", "Overall image brightness." },
            { "Intensidad del grano cinematográfico.", "Intensity of the cinematic grain." },
            { "Volumen de todo el juego.", "Volume of the whole game." },
            { "Volumen de la música.", "Music volume." },
            { "Disparos, pasos, golpes y sonidos de la interfaz.", "Gunshots, footsteps, hits and interface sounds." },
            { "Diálogos, radio y gritos de los aldeanos.", "Dialogue, radio and villagers' shouts." },
            { "Muestra los diálogos de la radio en pantalla.", "Shows radio dialogue on screen." },
            { "Velocidad de la cámara con el ratón y el stick derecho.", "Camera speed with the mouse and right stick." },
            { "Invierte el movimiento vertical de la cámara.", "Inverts the camera's vertical movement." },
            { "El mando vibra al recibir daño.", "The gamepad vibrates when you take damage." },
            { "Consulta las teclas del juego.", "See the game's keys." },
            { "Amplitud de la vista en primera persona.", "Width of the first-person view." },
            { "Movimiento de la cámara al caminar y correr.", "Camera movement when walking and running." },
            { "Muestra la munición, la vida y las pesetas.", "Shows ammo, health and pesetas." },
            { "Transparencia del HUD durante el juego.", "HUD transparency during the game." },
            { "Idioma de los menús.", "Menu language." },
            { "Tamaño de los subtítulos y descripciones.", "Size of subtitles and descriptions." },
            { "Ajusta los colores para distintos tipos de daltonismo.", "Adjusts colors for different types of color blindness." },
            { "Apaga grano, parpadeo, empujes de cámara y partículas.", "Turns off grain, flicker, camera pushes and particles." },
            { "Mantener o pulsar para apuntar.", "Hold or toggle to aim." },

            // ---- opciones: pie y modales
            { "Restablecer todo", "Reset all" }, { "Restablecer categoría", "Reset category" }, { "Categoría", "Category" },
            { "¿Restablecer todas las opciones?", "Reset all options?" },
            { "Se recuperarán los valores de fábrica de todas las categorías.", "Factory values will be restored for every category." },
            { "Sí, restablecer", "Yes, reset" },
            { "¿Descartar cambios?", "Discard changes?" },
            { "Hay cambios sin confirmar.", "There are unconfirmed changes." },
            { "Sí, descartar", "Yes, discard" },
            { "¿Mantener esta configuración?", "Keep these settings?" },
            { "Sí, mantener", "Yes, keep" }, { "No, revertir", "No, revert" },
            { "Opciones guardadas", "Options saved" },
            { "TECLAS DEL JUEGO", "GAME KEYS" },
        };
    }
}
