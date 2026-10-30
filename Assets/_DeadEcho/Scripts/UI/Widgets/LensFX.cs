using DeadEcho.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Capas de lente de las pantallas cinematicas (01_DISENO_VISUAL.md): grano de pelicula (18 %),
    /// viñeta, parpadeo de luz cada 9 s y degradado negro a la izquierda para leer el texto.
    /// Va en su propio Canvas (se redibuja cada fotograma sin tocar el resto de la interfaz).
    /// Con "Reducir movimiento" el grano queda quieto y no hay parpadeo.
    /// </summary>
    public class LensFX : MonoBehaviour
    {
        [Range(0f, 1f)] public float grainOpacity = 0.18f;
        [Tooltip("Imagenes de grano por segundo (como el grano de una pelicula).")]
        public float grainFps = 24f;
        [Range(0f, 1f)] public float vignetteOpacity = 0.85f;
        public bool leftGradient = true;
        [Range(0f, 1f)] public float leftGradientOpacity = 0.9f;
        public float flickerEvery = 9f;

        public RawImage grain;
        public Image vignette;
        public Image flicker;
        public Image gradient;

        private float _nextGrainFrame;
        private float _nextFlicker;
        private float _flickerT = -1f;

        public static LensFX Create(Transform parent, int sortingOrder = 20, bool withLeftGradient = true)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(parent, "LensFX"));
            var canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            var fx = rt.gameObject.AddComponent<LensFX>();
            fx.leftGradient = withLeftGradient;

            fx.gradient = UIFx.Image(rt, "DegradadoIzq", th.degradadoIzq, new Color(0f, 0f, 0f, fx.leftGradientOpacity));
            fx.gradient.rectTransform.anchorMin = Vector2.zero;
            fx.gradient.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            fx.gradient.rectTransform.offsetMin = fx.gradient.rectTransform.offsetMax = Vector2.zero;
            fx.gradient.gameObject.SetActive(withLeftGradient);

            fx.vignette = UIFx.Image(rt, "Vineta", th.vineta, new Color(0f, 0f, 0f, fx.vignetteOpacity));
            UIFx.Stretch(fx.vignette.rectTransform);

            var g = UIFx.Rect(rt, "Grano");
            UIFx.Stretch(g);
            fx.grain = g.gameObject.AddComponent<RawImage>();
            fx.grain.texture = th.grano;
            fx.grain.raycastTarget = false;
            fx.grain.color = new Color(1f, 1f, 1f, fx.grainOpacity);

            fx.flicker = UIFx.Image(rt, "Parpadeo", th.blanco, new Color(0f, 0f, 0f, 0f));
            UIFx.Stretch(fx.flicker.rectTransform);
            return fx;
        }

        private void OnEnable()
        {
            _nextFlicker = Time.unscaledTime + flickerEvery;
            SettingsService.OnApplied += OnSettings;
        }

        private void OnDisable() => SettingsService.OnApplied -= OnSettings;

        private void OnSettings(GameSettings s)
        {
            // el grano de pelicula de Opciones (0..100, 30 = el del diseño)
            grainOpacity = 0.18f * s.filmGrain / 30f;
        }

        private void Update()
        {
            bool reduce = SettingsService.ReduceMotion;
            float now = Time.unscaledTime;

            if (grain != null)
            {
                grain.color = new Color(1f, 1f, 1f, Mathf.Clamp01(grainOpacity));
                if (!reduce && now >= _nextGrainFrame)
                {
                    _nextGrainFrame = now + 1f / Mathf.Max(1f, grainFps);
                    // desplazar la textura de ruido al azar: grano "vivo"
                    float s = Screen.height / 256f;
                    grain.uvRect = new Rect(Random.value, Random.value, Screen.width / 256f / s * 2f, 2f);
                }
            }
            if (gradient != null) gradient.color = new Color(0f, 0f, 0f, leftGradientOpacity);
            if (vignette != null) vignette.color = new Color(0f, 0f, 0f, vignetteOpacity);

            // parpadeo: dos bajadas rapidas de luz cada 'flickerEvery' segundos
            if (flicker == null) return;
            if (reduce)
            {
                flicker.color = new Color(0f, 0f, 0f, 0f);
                return;
            }
            if (_flickerT < 0f && now >= _nextFlicker)
            {
                _flickerT = 0f;
                _nextFlicker = now + flickerEvery * Random.Range(0.85f, 1.15f);
            }
            if (_flickerT >= 0f)
            {
                _flickerT += Time.unscaledDeltaTime;
                float a = _flickerT < 0.08f ? 0.35f : _flickerT < 0.14f ? 0.05f : _flickerT < 0.22f ? 0.25f : 0f;
                flicker.color = new Color(0f, 0f, 0f, a);
                if (_flickerT > 0.3f) _flickerT = -1f;
            }
        }
    }
}
