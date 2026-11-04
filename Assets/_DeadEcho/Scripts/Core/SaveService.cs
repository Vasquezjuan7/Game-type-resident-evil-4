using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadEcho.Core
{
    /// <summary>
    /// Ranura de partida guardada: datos que se ven en las listas (lugar, tiempo, fecha, dificultad, % del caso)
    /// y el estado completo del juego en <see cref="game"/> (JSON que escribe y lee GameSaveSystem).
    /// </summary>
    [Serializable]
    public class DemoSlot
    {
        public bool used;
        public string location = "";
        public int playSeconds;
        public string dateTicks = "0";   // DateTime.Ticks como texto (JsonUtility no serializa long en todas las versiones)
        public int difficulty = 1;       // 0 Narrativa, 1 Superviviente, 2 Pesadilla
        public int casePercent;
        public bool siegeWon;
        public int kills;
        public string game = "";         // estado del juego (vacio = memoria antigua sin datos)

        public DateTime Date => long.TryParse(dateTicks, out long t) ? new DateTime(t) : DateTime.MinValue;
        public bool HasGame => used && !string.IsNullOrEmpty(game);
    }

    [Serializable]
    internal class DemoSlotFile
    {
        public List<DemoSlot> slots = new List<DemoSlot>();
    }

    /// <summary>
    /// Ranuras de partida (3) guardadas en saves.json dentro de Application.persistentDataPath.
    /// (Antes eran memorias DEMO falsas; ahora guardan la partida real.)
    /// </summary>
    public static class SaveService
    {
        public const int SlotCount = 3;
        public const string FileName = "saves.json";

        private static List<DemoSlot> _slots;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static IReadOnlyList<DemoSlot> Slots
        {
            get
            {
                if (_slots == null) Load();
                return _slots;
            }
        }

        /// <summary>Hay al menos una partida que se puede continuar.</summary>
        public static bool HasAnyMemory
        {
            get
            {
                foreach (var s in Slots) if (s.HasGame) return true;
                return false;
            }
        }

        /// <summary>Indice de la ranura con la partida mas reciente, o -1.</summary>
        public static int MostRecentIndex
        {
            get
            {
                int best = -1;
                for (int i = 0; i < Slots.Count; i++)
                    if (Slots[i].HasGame && (best < 0 || Slots[i].Date > Slots[best].Date)) best = i;
                return best;
            }
        }

        public static int FirstEmptyOr(int fallback)
        {
            for (int i = 0; i < Slots.Count; i++) if (!Slots[i].used) return i;
            return fallback;
        }

        public static void Load()
        {
            _slots = new List<DemoSlot>();
            try
            {
                if (File.Exists(FilePath))
                {
                    var file = JsonUtility.FromJson<DemoSlotFile>(File.ReadAllText(FilePath));
                    if (file != null && file.slots != null) _slots = file.slots;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeadEcho] No se pudieron leer las partidas guardadas: " + e.Message);
            }
            while (_slots.Count < SlotCount) _slots.Add(new DemoSlot());
        }

        public static void Save()
        {
            try
            {
                // primero a un temporal y despues se reemplaza: un cierre a medias no corrompe las partidas
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(new DemoSlotFile { slots = new List<DemoSlot>(Slots) }, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeadEcho] No se pudieron guardar las partidas: " + e.Message);
            }
        }

        /// <summary>Guarda una partida real en la ranura indicada.</summary>
        public static void Write(int index, DemoSlot slot)
        {
            if (_slots == null) Load();
            index = Mathf.Clamp(index, 0, SlotCount - 1);
            slot.used = true;
            slot.dateTicks = DateTime.Now.Ticks.ToString();
            _slots[index] = slot;
            Save();
        }

        /// <summary>Vacia una ranura.</summary>
        public static void Clear(int index)
        {
            if (_slots == null) Load();
            if (index < 0 || index >= SlotCount) return;
            _slots[index] = new DemoSlot();
            Save();
        }
    }
}
