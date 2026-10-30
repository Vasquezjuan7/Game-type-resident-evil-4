using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Boton con seleccion "vidrio" (pausa, nueva partida, salir): gradiente blanco translucido,
    /// borde de 1 px blanco al 50 %, brillo inferior y linea de luz centrada. Se construye por codigo.
    /// </summary>
    public class GlassButton : DEButtonBase
    {
        public TextMeshProUGUI label;
        public Image glass;
        public Image lightLine;
        [Tooltip("Color del texto sin seleccionar / seleccionado.")]
        public Color textNormal = new Color(0.9f, 0.9f, 0.88f, 1f);
        public Color textSelected = Color.white;

        /// <summary>Crea un boton vidrio listo para usar.</summary>
        public static GlassButton Create(Transform parent, string text, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center, float fontSize = -1f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Boton_" + text);
            rt.sizeDelta = size;
            Image hit = rt.gameObject.AddComponent<Image>(); // zona de clic invisible
            hit.color = new Color(0f, 0f, 0f, 0f);
            var b = rt.gameObject.AddComponent<GlassButton>();
            b.targetGraphic = hit;
            b.BuildGlass(text, align, fontSize > 0f ? fontSize : th.filaSistema);
            return b;
        }

        protected void BuildGlass(string text, TextAlignmentOptions align, float fontSize)
        {
            UITheme th = UITheme.Current;
            glass = UIFx.Image(transform, "Vidrio", th.vidrio, Color.white);
            UIFx.Stretch(glass.rectTransform);
            lightLine = UIFx.Image(transform, "LineaLuz", th.blanco, new Color(1f, 1f, 1f, 0.55f));
            UIFx.Place(lightLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1.5f));
            lightLine.rectTransform.anchorMin = new Vector2(0.25f, 0f);
            lightLine.rectTransform.anchorMax = new Vector2(0.75f, 0f);
            lightLine.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            label = UIFx.Text(transform, "Texto", text, th.rajdhaniMedium, fontSize, textNormal, align, 1f);
            UIFx.Stretch(label.rectTransform, align == TextAlignmentOptions.Left ? 39f : 12f, 12f);
            ApplyVisual(0f);
        }

        public string Text
        {
            get => label != null ? label.text : "";
            set { if (label != null) label.text = value; }
        }

        protected override void ApplyVisual(float k)
        {
            bool enabledState = IsInteractable();
            if (glass != null)
            {
                glass.color = new Color(1f, 1f, 1f, k);
                glass.rectTransform.localScale = new Vector3(Mathf.Lerp(0.985f, 1f, k), 1f, 1f);
            }
            if (lightLine != null) lightLine.color = new Color(1f, 1f, 1f, 0.55f * k);
            if (label != null)
            {
                Color c = Color.Lerp(textNormal, textSelected, k);
                if (!enabledState) c.a = 0.4f;
                label.color = c;
            }
        }
    }
}
