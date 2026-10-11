using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Materiales compartidos de efectos. Se crean como assets desde el menu RE4
    /// para que los shaders se incluyan en la build.
    /// </summary>
    [CreateAssetMenu(menuName = "RE4/Combat FX Library", fileName = "CombatFXLibrary")]
    public class CombatFXLibrary : ScriptableObject
    {
        public Material muzzleFlash;
        public Material tracer;
        public Material laser;
        public Material laserDot;
        public Material blood;
        public Material bloodDecal;
        public Material dust;
        public Material spark;
        public Material shellCasing;
        public Material smoke;
        public Material fire;
        public Material knife;
        public Material itemGlint;

        [Header("Sonidos")]
        public SoundBank sounds;
    }
}
