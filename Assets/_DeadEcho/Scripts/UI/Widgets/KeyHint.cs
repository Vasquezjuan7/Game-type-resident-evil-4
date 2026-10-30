using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Pista de control: tecla dentro de un circulo + texto ("Esc Volver", "↵ Confirmar").
    /// Cambia la tecla mostrada segun el ultimo dispositivo usado (InputModeWatcher).
    /// La tecla "Enter" se dibuja con el icono ↵.
    /// </summary>
    public class KeyHint : MonoBehaviour
    {
        public string keyboardKey = "Esc";
        public string gamepadKey = "B";
        public TextMeshProUGUI keyText;
        public Image ring;
        public Image enterIcon;
        public TextMeshProUGUI label;

        public static KeyHint Create(Transform parent, string keyboard, string gamepad, string text, float size = 26f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Tecla_" + text);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hint = rt.gameObject.AddComponent<KeyHint>();
            hint.keyboardKey = keyboard;
            hint.gamepadKey = gamepad;

            float d = size * 1.45f;
            hint.ring = UIFx.Image(rt, "Aro", th.aro, th.huesoClaro);
            hint.ring.rectTransform.sizeDelta = new Vector2(d, d);
            hint.keyText = UIFx.Text(hint.ring.transform, "Tecla", "", th.rajdhaniSemiBold, size * 0.62f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Stretch(hint.keyText.rectTransform);
            hint.enterIcon = UIFx.Image(hint.ring.transform, "Enter", th.enter, th.huesoClaro);
            UIFx.Stretch(hint.enterIcon.rectTransform, d * 0.25f, d * 0.25f, d * 0.25f, d * 0.25f);

            hint.label = UIFx.Text(rt, "Texto", text, th.rajdhaniMedium, size, th.huesoClaro, TextAlignmentOptions.Left);
            hint.label.rectTransform.sizeDelta = new Vector2(hint.label.preferredWidth + 4f, d);
            hint.Refresh();
            return hint;
        }

        private void OnEnable()
        {
            InputModeWatcher.OnModeChanged += OnMode;
            Refresh();
        }

        private void OnDisable() => InputModeWatcher.OnModeChanged -= OnMode;

        private void OnMode(InputMode _) => Refresh();

        public void SetLabel(string text)
        {
            if (label == null) return;
            label.text = text;
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 4f, label.rectTransform.sizeDelta.y);
        }

        public void Refresh()
        {
            string key = InputModeWatcher.Mode == InputMode.Mando ? gamepadKey : keyboardKey;
            bool enter = key == "Enter" || key == "↵";
            if (enterIcon != null) enterIcon.enabled = enter;
            if (keyText != null)
            {
                keyText.text = enter ? "" : key;
                // teclas largas ("Esc", "Tab") en letra mas pequeña para que quepan en el circulo
                keyText.fontSizeMax = keyText.fontSize;
                keyText.enableAutoSizing = key.Length > 1;
                keyText.fontSizeMin = 8f;
            }
        }
    }
}
