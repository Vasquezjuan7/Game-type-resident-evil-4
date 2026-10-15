using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Parpadeo de fuego para antorchas y hogueras.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float baseIntensity = 1.5f;
        public float amount = 0.35f;
        public float speed = 9f;

        private Light _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed) - 0.5f;
            _light.intensity = baseIntensity * (1f + n * amount * 2f);
        }
    }
}
