using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Gritos de los aldeanos en espanol, como los Ganados de RE4: avisos, insultos y amenazas cortas.
    /// Vienen grabadas con ElevenLabs en Resources/RE4Audio/Frases/{Aldeano,Aldeana}; las que falten se generan
    /// con la voz de Windows (en segundo plano) y se pasan por un filtro de voz rasposa en <see cref="Gameplay.ZombieAI"/>.
    /// </summary>
    public static class VillagerBarks
    {
        public static readonly string[] Alert =
        {
            "¡Forastero!", "¡Allí está!", "¡Ahí está!", "¡Un forastero!", "¡Cogedlo!", "¡Detrás de ti!",
        };

        public static readonly string[] Chase =
        {
            "¡Mátalo!", "¡Cógelo!", "¡Agárralo!", "¡Morir es vivir!", "¡Cabrón!", "¡Te voy a hacer pedazos!",
            "¡Que no se escape!", "¡Por el Padre!", "¡Hijo de perra!", "¡Ven aquí!", "¡Se escapa!",
        };

        public static readonly string[] Attack =
        {
            "¡Muérete!", "¡Toma!", "¡Te tengo!", "¡Muere!",
        };

        public static readonly string[] Idle =
        {
            "El Padre nos protege...", "Hay que dar de comer a la tierra.", "¿Qué ha sido eso?", "Forastero...",
        };

        /// <summary>Prepara todas las frases con una voz de hombre y una de mujer (prioridad baja).</summary>
        public static void PrefetchAll()
        {
            if (!NpcVoice.Enabled || !NpcVoice.AllowBackgroundVoices) return;
            NpcVoice v = NpcVoice.Get();
            // primero las mas tipicas, para que se oigan cuanto antes
            foreach (var set in new[] { Alert, Chase, Attack, Idle })
                foreach (var line in set) v.Prefetch(line, NpcVoice.VillagerMale[0], true);
            foreach (var set in new[] { Alert, Chase, Attack })
                foreach (var line in set) v.Prefetch(line, NpcVoice.VillagerFemale[0], true);
        }

        /// <summary>Clip de una frase al azar ya generado (null si aun no esta listo).</summary>
        public static AudioClip Pick(string[] set, string voice)
        {
            if (set == null || set.Length == 0) return null; // Cached() incluye las frases de ElevenLabs aunque no haya voz de Windows
            NpcVoice v = NpcVoice.Get();
            int start = Random.Range(0, set.Length);
            for (int i = 0; i < set.Length; i++)
            {
                AudioClip c = v.Cached(set[(start + i) % set.Length], voice);
                if (c != null) return c;
            }
            return null;
        }
    }
}
