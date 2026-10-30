using DeadEcho.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>Ceniza (o brasas, si sube) que cae lentamente sobre la pantalla. Sin asignaciones por fotograma.</summary>
    public class FallingAsh : MonoBehaviour
    {
        public int count = 40;
        public Color color = new Color(0.85f, 0.84f, 0.8f, 0.55f);
        [Tooltip("Velocidad vertical en px/s (negativa = cae, positiva = sube como brasas).")]
        public Vector2 fallSpeed = new Vector2(-40f, -90f);
        public Vector2 sizeRange = new Vector2(2f, 6f);

        private RectTransform[] _flakes;
        private Vector2[] _vel;
        private float[] _sway;
        private RectTransform _area;
        private bool _hidden;

        public static FallingAsh Create(Transform parent, int count, Color color, bool rising = false)
        {
            RectTransform rt = UIFx.Stretch(UIFx.Rect(parent, rising ? "Brasas" : "Ceniza"));
            var ash = rt.gameObject.AddComponent<FallingAsh>();
            ash.count = count;
            ash.color = color;
            if (rising) ash.fallSpeed = new Vector2(30f, 80f);
            return ash;
        }

        private void Start()
        {
            _area = (RectTransform)transform;
            _flakes = new RectTransform[count];
            _vel = new Vector2[count];
            _sway = new float[count];
            Sprite dot = UITheme.Current.blanco;
            for (int i = 0; i < count; i++)
            {
                Image img = UIFx.Image(transform, "p", dot, color);
                float s = Random.Range(sizeRange.x, sizeRange.y);
                img.rectTransform.sizeDelta = new Vector2(s, s);
                img.color = UITheme.WithAlpha(color, color.a * Random.Range(0.4f, 1f));
                _flakes[i] = img.rectTransform;
                Respawn(i, true);
            }
        }

        private void Respawn(int i, bool anywhere)
        {
            Rect r = _area.rect;
            float y = anywhere ? Random.Range(r.yMin, r.yMax) : (fallSpeed.x < 0f ? r.yMax + 10f : r.yMin - 10f);
            _flakes[i].anchoredPosition = new Vector2(Random.Range(r.xMin, r.xMax), y);
            _vel[i] = new Vector2(Random.Range(-12f, 12f), Random.Range(fallSpeed.x, fallSpeed.y));
            _sway[i] = Random.value * 10f;
        }

        private void Update()
        {
            if (_flakes == null) return;
            bool reduce = SettingsService.ReduceMotion;
            if (reduce != _hidden)
            {
                _hidden = reduce;
                for (int i = 0; i < _flakes.Length; i++) _flakes[i].gameObject.SetActive(!reduce);
            }
            if (reduce) return;

            float dt = Time.unscaledDeltaTime;
            Rect r = _area.rect;
            for (int i = 0; i < _flakes.Length; i++)
            {
                Vector2 p = _flakes[i].anchoredPosition;
                p += new Vector2(_vel[i].x + Mathf.Sin(Time.unscaledTime + _sway[i]) * 10f, _vel[i].y) * dt;
                _flakes[i].anchoredPosition = p;
                if (p.y < r.yMin - 20f || p.y > r.yMax + 20f || p.x < r.xMin - 20f || p.x > r.xMax + 20f) Respawn(i, false);
            }
        }
    }
}
