using DeadEcho.Core;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>Empuje lento de camara (escala 1.05 → 1.14 ida y vuelta en 24 s). Quieto con Reducir movimiento.</summary>
    public class DESlowPush : MonoBehaviour
    {
        public float seconds = 24f;
        private void Update()
        {
            if (SettingsService.ReduceMotion) { transform.localScale = Vector3.one * 1.05f; return; }
            float k = UIFx.Ease(Mathf.PingPong(Time.unscaledTime / seconds, 1f));
            transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.14f, k);
        }
    }
}
