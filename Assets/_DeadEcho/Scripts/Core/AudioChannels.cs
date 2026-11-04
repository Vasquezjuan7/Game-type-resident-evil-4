using UnityEngine;

namespace DeadEcho.Core
{
    /// <summary>
    /// Canales de volumen de Opciones › Audio sin AudioMixer (el del proyecto es de la demo y no tiene
    /// parametros expuestos). Efectos = todo lo que pasa por el AudioListener (General × Efectos).
    /// La musica y las voces ignoran el volumen del listener y aplican su propio factor (General × canal).
    /// </summary>
    public static class AudioChannels
    {
        public static float Master => Mathf.Clamp01(SettingsService.Current.masterVolume / 100f);
        public static float Music => Master * Mathf.Clamp01(SettingsService.Current.musicVolume / 100f);
        public static float Voice => Master * Mathf.Clamp01(SettingsService.Current.voiceVolume / 100f);
        public static float Effects => Master * Mathf.Clamp01(SettingsService.Current.sfxVolume / 100f);

        /// <summary>Marca una fuente como musica: su volumen se multiplica por <see cref="Music"/>.</summary>
        public static float AsMusic(AudioSource s, float volume)
        {
            if (s != null) s.ignoreListenerVolume = true;
            return volume * Music;
        }

        /// <summary>Marca una fuente como voz: su volumen se multiplica por <see cref="Voice"/>.</summary>
        public static float AsVoice(AudioSource s, float volume)
        {
            if (s != null) s.ignoreListenerVolume = true;
            return volume * Voice;
        }
    }
}
