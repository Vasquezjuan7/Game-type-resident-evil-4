using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>Pestaña trapezoidal de seccion (#2C2C2B → #3A3A38) con titulo y etiqueta opcional (p. ej. "DEMO").</summary>
    public class SectionHeader : MonoBehaviour
    {
        public TextMeshProUGUI title;
        public Image tab;
        public TextMeshProUGUI badge;

        public static SectionHeader Create(Transform parent, string text, string badgeText = null, float width = 510f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Seccion_" + text);
            rt.sizeDelta = new Vector2(width, 45f);
            var h = rt.gameObject.AddComponent<SectionHeader>();
            h.tab = UIFx.Image(rt, "Pestana", th.trapecio, Color.white);
            UIFx.Stretch(h.tab.rectTransform);
            h.title = UIFx.Text(rt, "Titulo", text, th.rajdhaniMedium, th.textoPequeno + 1f, new Color(0.62f, 0.61f, 0.58f), TextAlignmentOptions.Left, 1.5f);
            UIFx.Stretch(h.title.rectTransform, 12f, 60f);
            if (!string.IsNullOrEmpty(badgeText))
            {
                Image back = UIFx.Image(rt, "Etiqueta", th.blanco, th.huesoClaro);
                UIFx.Place(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f + h.title.preferredWidth + 14f, 0f), new Vector2(88f, 22f));
                h.badge = UIFx.Text(back.transform, "Texto", badgeText, th.courierPrimeBold, 16f, th.negroFondo, TextAlignmentOptions.Center, 4f);
                UIFx.Stretch(h.badge.rectTransform);
            }
            return h;
        }
    }
}
