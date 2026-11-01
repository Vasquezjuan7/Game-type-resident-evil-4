using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.RE4
{
    /// <summary>Linterna de Leon (tecla L). Tiembla un poco al moverse y parpadea cuando hay peligro.</summary>
    public class Flashlight : MonoBehaviour
    {
        public bool startOn = true;
        public float intensity = 2.4f;
        public float range = 28f;
        public float spotAngle = 52f;

        private Light _light;
        private FirstPersonController _fps;
        private bool _on;

        private void Start()
        {
            _fps = GetComponent<FirstPersonController>();
            Transform holder = _fps != null && _fps.cameraHolder != null ? _fps.cameraHolder : transform;
            Transform existing = holder.Find("Linterna");
            GameObject go = existing != null ? existing.gameObject : new GameObject("Linterna");
            go.transform.SetParent(holder, false);
            go.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f);
            go.transform.localRotation = Quaternion.Euler(2f, -2f, 0f);
            _light = go.GetComponent<Light>();
            if (_light == null) _light = go.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.color = new Color(1f, 0.95f, 0.85f);
            _light.range = range;
            _light.spotAngle = spotAngle;
            _light.innerSpotAngle = spotAngle * 0.45f;
            _light.shadows = LightShadows.Soft;
            _light.shadowStrength = 0.8f;
            _on = startOn;
            _light.enabled = _on;
        }

        private void Update()
        {
            if (_light == null) return;
            var kb = Keyboard.current;
            bool locked = _fps != null && _fps.InputLocked;
            if (!locked && kb != null && kb.lKey.wasPressedThisFrame)
            {
                _on = !_on;
                _light.enabled = _on;
                ProceduralAudio.PlayAt(ProceduralAudio.Click(2600f, 160f), transform.position + Vector3.up * 1.5f, 0.35f, 0.05f, 6f);
            }
            if (!_on) return;

            // Parpadeo nervioso con poca vida
            bool danger = PlayerHealth.Instance != null && PlayerHealth.Instance.Status == HealthStatus.Danger;
            float flicker = danger && Mathf.PerlinNoise(Time.time * 9f, 0.3f) > 0.78f ? 0.25f : 1f;
            _light.intensity = intensity * flicker;
        }

        /// <summary>Permite consultar o cambiar por código el estado de la linterna de Leon.</summary>
        public bool IsOn => _on;

        public void SetFlashlight(bool state)
        {
            _on = state;
            if (_light != null) _light.enabled = _on;
        }
    }
}
