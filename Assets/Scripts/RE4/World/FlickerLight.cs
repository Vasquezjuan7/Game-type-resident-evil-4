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
        private float _gust;
        private float _nextGust;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed) - 0.5f;
            // Rachas de viento: de vez en cuando la llama casi se apaga y se recupera
            if (Time.time >= _nextGust)
            {
                _nextGust = Time.time + Random.Range(6f, 22f);
                _gust = Random.Range(0.45f, 0.75f);
            }
            _gust = Mathf.MoveTowards(_gust, 0f, Time.deltaTime * 1.4f);
            float gutter = 1f - _gust * Mathf.PerlinNoise(_seed + 7f, Time.time * 18f);
            _light.intensity = baseIntensity * (1f + n * amount * 2f) * gutter;
        }

        /// <summary>Permite actualizar la intensidad base de la antorcha u hoguera en tiempo de ejecución.</summary>
        public void SetBaseIntensity(float newIntensity)
        {
            baseIntensity = Mathf.Max(0f, newIntensity);
        }
    }
}
