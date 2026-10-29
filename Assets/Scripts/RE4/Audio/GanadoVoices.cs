using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    public enum GanadoSound
    {
        Grunt,
        Pant,
        Scream,
        Pain,
        Death
    }

    /// <summary>
    /// Voces no verbales de los aldeanos (gruñidos, jadeos, gritos, dolor, estertores) sintetizadas
    /// con formantes de voz humana. Se cargan de Resources/RE4Audio/Voces_Ganado (van dentro del juego).
    /// </summary>
    public static class GanadoVoices
    {
        private static Dictionary<string, AudioClip[]> _sets;

        private static void Load()
        {
            if (_sets != null) return;
            _sets = new Dictionary<string, AudioClip[]>();
            var all = Resources.LoadAll<AudioClip>("RE4Audio/Voces_Ganado");
            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in all)
            {
                // "Ganado_Grunido_03" -> "Ganado_Grunido"
                int cut = c.name.LastIndexOf('_');
                string key = cut > 0 ? c.name.Substring(0, cut) : c.name;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            foreach (var kv in groups) _sets[kv.Key] = kv.Value.ToArray();
        }

        public static AudioClip Pick(GanadoSound kind, bool female)
        {
            Load();
            string key;
            switch (kind)
            {
                case GanadoSound.Grunt: key = female ? "Aldeana_Grunido" : "Ganado_Grunido"; break;
                case GanadoSound.Pant: key = female ? "Aldeana_Grunido" : "Ganado_Jadeo"; break;
                case GanadoSound.Scream: key = female ? "Aldeana_Grito" : "Ganado_Grito"; break;
                case GanadoSound.Pain: key = female ? "Aldeana_Dolor" : "Ganado_Dolor"; break;
                default: key = female ? "Aldeana_Dolor" : "Ganado_Muerte"; break;
            }
            return _sets.TryGetValue(key, out var set) && set.Length > 0 ? set[Random.Range(0, set.Length)] : null;
        }
    }
}
