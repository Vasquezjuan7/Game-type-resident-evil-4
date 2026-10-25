using System;
using UnityEngine;

namespace Gameplay.RE4
{
    [Serializable]
    public class SurfaceSteps
    {
        public string surface = "dirt";
        public AudioClip[] walk = new AudioClip[0];
        public AudioClip[] run = new AudioClip[0];
    }

    /// <summary>
    /// Sonidos del juego que no son de un arma concreta: pasos por superficie, respiracion,
    /// ambiente nocturno, pasos de enemigos y explosiones. Lo rellena el menu RE4 con los audios del proyecto.
    /// Lo que falte se sustituye por los sonidos procedimentales de <see cref="ProceduralAudio"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RE4/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Pasos de Leon por superficie (dirt, stone, gravel, grass, wood)")]
        public SurfaceSteps[] steps = new SurfaceSteps[0];

        [Header("Respiracion (cansancio / peligro)")]
        public AudioClip[] breathing = new AudioClip[0];

        [Header("Enemigos")]
        public AudioClip[] enemySteps = new AudioClip[0];
        public AudioClip[] dogSteps = new AudioClip[0];

        [Header("Ambiente nocturno")]
        public AudioClip[] windLoops = new AudioClip[0];
        public AudioClip[] forestLoops = new AudioClip[0];
        public AudioClip[] randomOneShots = new AudioClip[0];
        public AudioClip[] tensionDrones = new AudioClip[0];
        public AudioClip music;

        [Header("Efectos")]
        public AudioClip explosion;

        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        public SurfaceSteps Steps(string surface)
        {
            if (steps == null || steps.Length == 0) return null;
            foreach (var s in steps) if (s != null && s.surface == surface) return s;
            return steps[0];
        }

        public static SoundBank Current
        {
            get
            {
                FXManager fx = FXManager.Get();
                return fx != null && fx.library != null ? fx.library.sounds : null;
            }
        }
    }
}
