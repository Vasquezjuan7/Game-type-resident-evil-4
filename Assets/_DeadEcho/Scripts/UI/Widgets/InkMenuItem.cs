using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Opcion del menu principal: texto Courier Prime gris; al seleccionarla aparece un trazo de tinta
    /// negra detras y el texto pasa a blanco en negrita. Deshabilitada = gris al 40 % y la navegacion la salta.
    /// </summary>
    public class InkMenuItem : DEButtonBase
    {
        public TextMeshProUGUI label;
        public Image ink;
        public TextMeshProUGUI dot;

        public static InkMenuItem Create(Transform parent, string text, float width = 420f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Opcion_" + text);
            rt.sizeDelta = new Vector2(width, 54f);
            Image hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var item = rt.gameObject.AddComponent<InkMenuItem>();
            item.targetGraphic = hit;

            item.ink = UIFx.Image(rt, "Tinta", th.tinta, Color.white);
            item.ink.rectTransform.anchorMin = new Vector2(0f, 0f);
            item.ink.rectTransform.anchorMax = new Vector2(0f, 1f);
            item.ink.rectTransform.pivot = new Vector2(0f, 0.5f);
            item.ink.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
            item.ink.rectTransform.sizeDelta = new Vector2(0f, -4f);

            item.dot = UIFx.Text(rt, "Punto", "·", th.courierPrime, th.opcionMenu, th.grisMedio, TextAlignmentOptions.Center);
            UIFx.Place(item.dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3f, 0f), new Vector2(20f, 40f));

            item.label = UIFx.Text(rt, "Texto", text, th.courierPrime, th.opcionMenu, th.grisMedio, TextAlignmentOptions.Left, 2f);
            UIFx.Stretch(item.label.rectTransform, 21f, 0f);
            item.ApplyVisual(0f);
            return item;
        }

        public string Text
        {
            get => label != null ? label.text : "";
            set { if (label != null) label.text = value; }
        }

        protected override void ApplyVisual(float k)
        {
            UITheme th = UITheme.Current;
            bool on = IsInteractable();
            if (ink != null)
            {
                // el trazo "se pinta" de izquierda a derecha
                float textWidth = label != null ? label.preferredWidth + 48f : 260f;
                ink.rectTransform.sizeDelta = new Vector2(textWidth * k, -4f);
                ink.color = new Color(0f, 0f, 0f, 0.92f * k);
            }
            if (label != null)
            {
                label.font = k > 0.5f && th.courierPrimeBold != null ? th.courierPrimeBold : th.courierPrime;
                Color c = Color.Lerp(th.grisMedio, Color.white, k);
                if (!on) c = UITheme.WithAlpha(th.grisMedio, 0.4f);
                label.color = c;
            }
            if (dot != null) dot.color = UITheme.WithAlpha(th.grisMedio, 0f);
        }
    }
}
