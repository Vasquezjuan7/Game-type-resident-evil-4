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
    /// Voces no verbales de los aldeanos (gruñidos, jadeos, gritos, dolor, estertores) y rugidos del Tyrant
    /// (Jefe_*), generadas con ElevenLabs. Se cargan de Resources/RE4Audio/Voces_Ganado (van dentro del juego).
    /// </summary>
    public static class GanadoVoices
    {
        public static AudioClip Pick(GanadoSound kind, bool female, bool boss = false)
        {
            if (boss)
            {
                // el Tyrant tiene sus propios rugidos; si faltan, la voz de aldeano grave
                string b = kind == GanadoSound.Pain ? "Jefe_Dolor" : kind == GanadoSound.Death ? "Jefe_Muerte" : "Jefe_Rugido";
                AudioClip bc = BundledAudio.Pick("Voces_Ganado", b);
                if (bc != null) return bc;
            }
            string key;
            switch (kind)
            {
                case GanadoSound.Grunt: key = female ? "Aldeana_Grunido" : "Ganado_Grunido"; break;
                case GanadoSound.Pant: key = female ? "Aldeana_Grunido" : "Ganado_Jadeo"; break;
                case GanadoSound.Scream: key = female ? "Aldeana_Grito" : "Ganado_Grito"; break;
                case GanadoSound.Pain: key = female ? "Aldeana_Dolor" : "Ganado_Dolor"; break;
                default: key = female ? "Aldeana_Dolor" : "Ganado_Muerte"; break;
            }
            return BundledAudio.Pick("Voces_Ganado", key);
        }
    }
}
