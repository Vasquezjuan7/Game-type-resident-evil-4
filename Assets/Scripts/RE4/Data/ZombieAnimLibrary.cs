using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Animaciones alternativas de los Ganados (paquete de zombis de Mixamo + las originales).
    /// Cada aldeano elige al nacer su propia combinacion para que no todos se muevan igual.
    /// La crea el menu RE4 > Integrar animaciones nuevas y se carga desde Resources/RE4ZombieAnims.
    /// </summary>
    [CreateAssetMenu(menuName = "RE4/Zombie Anim Library", fileName = "RE4ZombieAnims")]
    public class ZombieAnimLibrary : ScriptableObject
    {
        public AnimationClip[] idles;
        public AnimationClip[] walks;
        public AnimationClip[] runs;
        public AnimationClip[] attacks;
        public AnimationClip[] hits;
        public AnimationClip[] screams;
        public AnimationClip[] standUps;

        /// <summary>Nombre del clip que usa el estado "StandUp" del controlador (el que se sustituye).</summary>
        public const string StandUpBaseClip = "zombie stand up";

        private static ZombieAnimLibrary _current;
        private static bool _loaded;

        public static ZombieAnimLibrary Current
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _current = Resources.Load<ZombieAnimLibrary>("RE4ZombieAnims");
                }
                return _current;
            }
        }

        /// <summary>Clip al azar de la lista, o el original con probabilidad keepOriginal.</summary>
        public static AnimationClip Pick(AnimationClip[] pool, AnimationClip original, float keepOriginal)
        {
            if (pool == null || pool.Length == 0 || (original != null && Random.value < keepOriginal)) return original;
            AnimationClip c = pool[Random.Range(0, pool.Length)];
            return c != null ? c : original;
        }
    }
}
