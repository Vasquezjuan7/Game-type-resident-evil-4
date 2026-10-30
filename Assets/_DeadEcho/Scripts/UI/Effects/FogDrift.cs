using DeadEcho.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Banco de niebla / nubes que cruza la pantalla (desplaza el uvRect de un RawImage) con una
    /// respiracion de opacidad lenta y, opcionalmente, un empuje de camara (escala que crece).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class FogDrift : MonoBehaviour
    {
        [Tooltip("Velocidad de desplazamiento en UV por segundo.")]
        public Vector2 speed = new Vector2(0.012f, 0f);
        public float baseAlpha = 0.35f;
        [Tooltip("Variacion de opacidad (respiracion).")]
        public float alphaPulse = 0.1f;
        public float pulsePeriod = 7f;
        [Tooltip("Empuje lento: escala de 1 a pushScale en pushTime segundos (0 = sin empuje).")]
        public float pushScale = 1f;
        public float pushTime = 40f;

        private RawImage _img;
        private float _phase;

        private void Awake()
        {
            _img = GetComponent<RawImage>();
            _phase = Random.value * 10f;
        }

        private void Update()
        {
            bool reduce = SettingsService.ReduceMotion;
            float t = Time.unscaledTime;
            Rect r = _img.uvRect;
            if (!reduce) r.position += speed * Time.unscaledDeltaTime;
            _img.uvRect = r;
            Color c = _img.color;
            c.a = baseAlpha + (reduce ? 0f : Mathf.Sin((t + _phase) * Mathf.PI * 2f / pulsePeriod) * alphaPulse);
            _img.color = c;
            if (pushScale > 1f && !reduce)
            {
                float k = Mathf.PingPong(t / pushTime, 1f);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, pushScale, UIFx.Ease(k));
            }
        }
    }
}
