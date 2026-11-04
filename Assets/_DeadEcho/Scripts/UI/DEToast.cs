using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>Aviso arriba a la derecha (punto que pulsa + texto) de 03_NuevaPartida.html.</summary>
    public class DEToast : MonoBehaviour
    {
        public CanvasGroup group;
        public TextMeshProUGUI text;
        public Image dot;
        private Coroutine _run;

        public static DEToast Create(Transform parent)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Toast");
            UIFx.Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -45f), new Vector2(620f, 54f));
            var t = rt.gameObject.AddComponent<DEToast>();
            t.group = rt.gameObject.AddComponent<CanvasGroup>();
            t.group.alpha = 0f;
            t.group.blocksRaycasts = false;
            Image bg = UIFx.Image(rt, "Fondo", th.blanco, new Color(0f, 0f, 0f, 0.72f));
            UIFx.Stretch(bg.rectTransform);
            DEExtras.Outline(rt, new Color(1f, 1f, 1f, 0.25f));
            t.dot = UIFx.Image(rt, "Punto", th.aro, th.huesoClaro);
            UIFx.Place(t.dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(12f, 12f));
            t.text = UIFx.Text(rt, "Texto", "", th.rajdhaniMedium, 22f, th.huesoClaro, TextAlignmentOptions.Left);
            UIFx.Stretch(t.text.rectTransform, 50f, 18f);
            return t;
        }

        public void Show(string message, float seconds = 3f)
        {
            if (_run != null) StopCoroutine(_run);
            text.text = Loc.T(message);
            _run = StartCoroutine(Run(seconds));
        }

        private IEnumerator Run(float seconds)
        {
            RectTransform rt = (RectTransform)transform;
            Vector2 basePos = new Vector2(-96f, -45f);
            yield return UIFx.Tween(0.25f, k =>
            {
                group.alpha = Mathf.Max(group.alpha, k);
                rt.anchoredPosition = basePos + Vector2.down * (1f - k) * 15f;
            }, true);
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                float a = SettingsService.ReduceMotion ? 1f : Mathf.Lerp(0.3f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f));
                dot.color = UITheme.WithAlpha(UITheme.Current.huesoClaro, a);
                yield return null;
            }
            yield return UIFx.Tween(0.3f, k => group.alpha = 1f - k);
            _run = null;
        }
    }
}
