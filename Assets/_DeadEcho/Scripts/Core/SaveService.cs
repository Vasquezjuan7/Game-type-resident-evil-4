using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadEcho.Core
{
    /// <summary>Memoria (DEMO) de una ranura. No guarda posicion ni inventario reales.</summary>
    [Serializable]
    public class DemoSlot
    {
        public bool used;
        public string location = "";
        public int playSeconds;
        public string dateTicks = "0";   // DateTime.Ticks como texto (JsonUtility no serializa long en todas las versiones)
        public int difficulty = 1;       // 0 Narrativa, 1 Superviviente, 2 Pesadilla
        public int casePercent;

        public DateTime Date => long.TryParse(dateTicks, out long t) ? new DateTime(t) : DateTime.MinValue;
    }

    [Serializable]
    internal class DemoSlotFile
    {
        public List<DemoSlot> slots = new List<DemoSlot>();
    }

    /// <summary>
    /// Ranuras de memoria FALSAS (03_ESPECIFICACION_FUNCIONAL.md, Nueva partida). Se guardan en
    /// demo_slots.json solo para que Continuar se habilite y las ranuras se vean al volver.
    /// </summary>
    public static class SaveService
    {
        public const int SlotCount = 3;
        public const string FileName = "demo_slots.json";

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

        public static bool HasAnyMemory
        {
            get
            {
                foreach (var s in Slots) if (s.used) return true;
                return false;
            }
        }

        /// <summary>Indice de la ranura con la memoria mas reciente, o -1.</summary>
        public static int MostRecentIndex
        {
            get
            {
                int best = -1;
                for (int i = 0; i < Slots.Count; i++)
                    if (Slots[i].used && (best < 0 || Slots[i].Date > Slots[best].Date)) best = i;
                return best;
            }
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
                Debug.LogWarning("[DeadEcho] No se pudieron leer las ranuras DEMO: " + e.Message);
            }
            while (_slots.Count < SlotCount) _slots.Add(new DemoSlot());
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(new DemoSlotFile { slots = new List<DemoSlot>(Slots) }, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DeadEcho] No se pudieron guardar las ranuras DEMO: " + e.Message);
            }
        }

        /// <summary>Asigna una memoria falsa (datos inventados) a la ranura indicada y la guarda.</summary>
        public static DemoSlot AssignFakeMemory(int index, int difficulty)
        {
            if (_slots == null) Load();
            index = Mathf.Clamp(index, 0, SlotCount - 1);
            var s = new DemoSlot
            {
                used = true,
                location = "Hospital San Aurelio",
                playSeconds = UnityEngine.Random.Range(20 * 60, 9 * 3600),
                dateTicks = DateTime.Now.Ticks.ToString(),
                difficulty = difficulty,
                casePercent = UnityEngine.Random.Range(3, 88)
            };
            _slots[index] = s;
            Save();
            return s;
        }
    }
}
