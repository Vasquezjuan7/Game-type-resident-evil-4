using System.Collections.Generic;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Creditos (08_Creditos.html): las palabras caen desde arriba hasta el pie de pagina en loop
    /// (12 s por linea, 0.15 s de desfase entre palabras, 3 s entre lineas). Esc vuelve al menu.
    /// </summary>
    public class CreditsScreen : UIScreen
    {
        public static readonly string[] Lines =
        {
            "HECHO CON AMOR POR",
            "Juan Diego Gonzales Sánchez",
            "Juan José Muñoz Jurado",
            "Juan José Pérez Bravo"
        };

        private const float LineTime = 12f;
        private const float WordDelay = 0.15f;
        private const float LineGap = 3f;

        private struct Word
        {
            public RectTransform rt;
            public TextMeshProUGUI text;
            public float delay;
            public float x;
        }

        private readonly List<Word> _words = new List<Word>();
        private float _start;

        public static CreditsScreen Create(Transform canvas)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, "Pantalla_Creditos"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var s = rt.gameObject.AddComponent<CreditsScreen>();

            Image bg = UIFx.Image(rt, "Fondo", th.blanco, th.negroFondo, true);
            UIFx.Stretch(bg.rectTransform);

            RectTransform area = UIFx.Stretch(UIFx.Rect(rt, "Caida"), 0f, 0f, 60f, 150f);
            for (int l = 0; l < Lines.Length; l++)
            {
                bool header = l == 0;
                string[] words = Lines[l].Split(' ');
                TMP_FontAsset font = header ? th.rajdhaniMedium : th.specialElite;
                float size = header ? 26f : 52f;
                // ancho total para centrar la linea
                var widths = new float[words.Length];
                float total = 0f;
                for (int w = 0; w < words.Length; w++)
                {
                    var probe = UIFx.Text(area, "w", words[w], font, size, th.hueso, TextAlignmentOptions.Center, header ? 8f : 3f);
                    widths[w] = probe.preferredWidth;
                    total += widths[w] + (w > 0 ? size * 0.45f : 0f);
                    Destroy(probe.gameObject);
                }
                float x = -total * 0.5f;
                for (int w = 0; w < words.Length; w++)
                {
                    var t = UIFx.Text(area, $"L{l}_{words[w]}", words[w], font, size, header ? th.grisMedio : th.hueso, TextAlignmentOptions.Center, header ? 8f : 3f);
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    t.rectTransform.sizeDelta = new Vector2(widths[w] + 8f, size * 1.4f);
                    s._words.Add(new Word { rt = t.rectTransform, text = t, delay = l * LineGap + w * WordDelay, x = x + widths[w] * 0.5f });
                    x += widths[w] + size * 0.45f;
                }
            }

            var hints = DEBuild.HintsRow(rt, true, new DEBuild.KeyHintDef("Esc", "B", "Volver"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 40f));
            Image foot = UIFx.Image(rt, "Pie", th.blanco, th.grisLinea);
            UIFx.Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1500f, 1.5f));
            return s;
        }

        public override void Show(bool restoreSelection)
        {
            base.Show(restoreSelection);
            _start = Time.unscaledTime;
        }

        private void Update()
        {
            if (_words.Count == 0) return;
            RectTransform area = (RectTransform)_words[0].rt.parent;
            float height = area.rect.height;
            float cycle = (Lines.Length - 1) * LineGap + LineTime;
            float now = Time.unscaledTime - _start;
            bool reduce = SettingsService.ReduceMotion;

            for (int i = 0; i < _words.Count; i++)
            {
                Word w = _words[i];
                if (reduce)
                {
                    // sin movimiento: lineas fijas centradas
                    int line = Mathf.RoundToInt(w.delay / LineGap);
                    w.rt.anchoredPosition = new Vector2(w.x, -height * 0.3f - line * 80f);
                    w.text.alpha = 1f;
                    continue;
                }
                float k = Mathf.Repeat(now - w.delay, cycle) / LineTime;
                if (now < w.delay || k > 1f)
                {
                    w.text.alpha = 0f;
                    continue;
                }
                // caida con frenado suave y desvanecido arriba y abajo
                float y = -Mathf.Lerp(0f, height, k);
                w.rt.anchoredPosition = new Vector2(w.x, y);
                w.text.alpha = Mathf.Clamp01(k * 8f) * Mathf.Clamp01((1f - k) * 6f);
            }
        }
    }
}
