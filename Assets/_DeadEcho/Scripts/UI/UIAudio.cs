using UnityEngine;

namespace DeadEcho.UI
{
    public enum UISound
    {
        Hover,
        Confirmar,
        Atras,
        Error
    }

    /// <summary>
    /// Sonidos de la interfaz (hover, confirmar, atras, error). Si falta un clip se genera uno sencillo por codigo.
    /// Se carga desde Resources/DeadEcho/UIAudio.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Echo/UI Audio", fileName = "UIAudio")]
    public class UIAudio : ScriptableObject
    {
        public AudioClip hover;
        public AudioClip confirmar;
        public AudioClip atras;
        public AudioClip error;
        [Range(0f, 1f)] public float volumen = 0.6f;

        private static UIAudio _current;
        private static AudioSource _source;
        private static AudioClip _gHover, _gConfirm, _gBack, _gError;

        public static UIAudio Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<UIAudio>("DeadEcho/UIAudio");
                if (_current == null) _current = CreateInstance<UIAudio>();
                return _current;
            }
        }

        public static void Play(UISound sound)
        {
            UIAudio a = Current;
            AudioClip clip;
            switch (sound)
            {
                case UISound.Hover: clip = a.hover != null ? a.hover : _gHover ??= Tone("ui_hover", 1150f, 0.05f, 0.25f); break;
                case UISound.Confirmar: clip = a.confirmar != null ? a.confirmar : _gConfirm ??= Tone("ui_confirm", 620f, 0.14f, 0.5f, 930f); break;
                case UISound.Atras: clip = a.atras != null ? a.atras : _gBack ??= Tone("ui_back", 520f, 0.12f, 0.45f, 350f); break;
                default: clip = a.error != null ? a.error : _gError ??= Tone("ui_error", 160f, 0.22f, 0.5f, 150f); break;
            }
            if (_source == null)
            {
                var go = new GameObject("[UIAudio]");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.spatialBlend = 0f;
                _source.playOnAwake = false;
                _source.ignoreListenerPause = true;
            }
            _source.PlayOneShot(clip, a.volumen);
        }

        /// <summary>Tono corto con caida (y glissando opcional) para los sonidos que falten.</summary>
        private static AudioClip Tone(string name, float freq, float seconds, float gain, float endFreq = -1f)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = endFreq > 0f ? Mathf.Lerp(freq, endFreq, t) : freq;
                ph += f / rate;
                float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 200f);
                data[i] = (Mathf.Sin(ph * 2f * Mathf.PI) * 0.7f + Mathf.Sin(ph * 4f * Mathf.PI) * 0.15f) * env * gain;
            }
            AudioClip c = AudioClip.Create(name, n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
