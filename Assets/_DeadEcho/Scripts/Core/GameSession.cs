namespace DeadEcho.Core
{
    /// <summary>
    /// Datos de la partida en curso: dificultad, ranura de guardado, tiempo jugado y, al cargar, la partida
    /// pendiente de restaurar. Si se entra al juego directamente (sin pasar por el menu) se usa Superviviente.
    /// </summary>
    public static class GameSession
    {
        public static readonly string[] DifficultyNames = { "Narrativa", "Superviviente", "Pesadilla" };
        public static readonly string[] DifficultyDescriptions =
        {
            "Para disfrutar la historia. Enemigos débiles, munición abundante.",
            "La experiencia recomendada. Recursos limitados.",
            "Enemigos agresivos, sin autoguardado. Cada bala cuenta."
        };
        private static readonly float[] DamageTaken = { 0.5f, 1f, 1.5f };
        private static readonly bool[] AutoSaveByDifficulty = { true, true, false };

        /// <summary>0 Narrativa, 1 Superviviente (por defecto), 2 Pesadilla.</summary>
        public static int Difficulty { get; private set; } = 1;

        /// <summary>Ranura donde se guarda esta partida (-1 = aun sin ranura: se usa la primera libre).</summary>
        public static int Slot { get; set; } = -1;

        /// <summary>Se ha empezado desde Nueva partida o Continuar (no directamente desde la escena de juego).</summary>
        public static bool StartedFromMenu { get; private set; }

        /// <summary>Nueva partida: al entrar en el pueblo se reproduce la cinematica de introduccion (una vez).</summary>
        public static bool IntroPending { get; set; }

        /// <summary>Segundos jugados en esta partida (se guardan con ella).</summary>
        public static float PlaySeconds { get; set; }

        // ---- restauracion de una partida guardada (la consume GameSaveSystem al cargar la escena)
        public static bool IsRestoring { get; private set; }
        public static string PendingSave { get; private set; }
        public static bool RestoreSiegeWon { get; private set; }
        public static int RestoreKills { get; private set; }

        /// <summary>Multiplicador del daño que recibe el jugador (×0.5 / ×1.0 / ×1.5).</summary>
        public static float DamageTakenMultiplier => DamageTakenFor(Difficulty);

        public static bool AutoSave => AutoSaveFor(Difficulty);

        public static float DamageTakenFor(int difficulty) => DamageTaken[Clamp(difficulty)];
        public static bool AutoSaveFor(int difficulty) => AutoSaveByDifficulty[Clamp(difficulty)];

        /// <summary>Nueva partida (Comenzar →) en la ranura elegida.</summary>
        public static void Begin(int difficulty, int slot = -1)
        {
            Difficulty = Clamp(difficulty);
            Slot = slot;
            StartedFromMenu = true;
            PlaySeconds = 0f;
            IntroPending = true;
            ClearRestore();
        }

        /// <summary>Continuar / Cargar partida: la escena de juego restaurara este estado al arrancar.</summary>
        public static void BeginFromSave(int slot, int difficulty, float playSeconds, string json, bool siegeWon, int kills)
        {
            Difficulty = Clamp(difficulty);
            Slot = slot;
            StartedFromMenu = true;
            PlaySeconds = playSeconds;
            IntroPending = false;
            PendingSave = json;
            IsRestoring = !string.IsNullOrEmpty(json);
            RestoreSiegeWon = siegeWon;
            RestoreKills = kills;
        }

        /// <summary>La partida ya se restauro (o se abandono): el proximo arranque es normal.</summary>
        public static void ClearRestore()
        {
            IsRestoring = false;
            PendingSave = null;
            RestoreSiegeWon = false;
            RestoreKills = 0;
        }

        private static int Clamp(int d) => d < 0 ? 0 : d > 2 ? 2 : d;
    }
}
