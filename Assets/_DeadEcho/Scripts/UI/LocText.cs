using DeadEcho.Core;
using TMPro;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Traduce en vivo un texto fijo de la interfaz (clave = texto en español) cuando cambia el idioma.
    /// Si el codigo cambia el texto por otro (valores, contadores...), deja de tocarlo.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocText : MonoBehaviour
    {
        public string key;
        private TextMeshProUGUI _text;

        public static void Attach(TextMeshProUGUI t, string key)
        {
            if (t == null || !Loc.Has(key)) return;
            var l = t.gameObject.AddComponent<LocText>();
            l.key = key;
            l._text = t;
        }

        private void OnEnable()
        {
            Loc.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.OnChanged -= Refresh;

        private void Refresh()
        {
            if (_text == null) _text = GetComponent<TextMeshProUGUI>();
            if (_text == null || string.IsNullOrEmpty(key)) return;
            string current = _text.text;
            if (current != key && current != Loc.ToEnglish(key)) return; // el texto ya no es el original
            string next = Loc.T(key);
            if (current == next) return;
            _text.text = next;
            // las pistas de teclas ajustan su ancho al texto
            var hint = GetComponentInParent<KeyHint>();
            if (hint != null && hint.label == _text) hint.SetLabel(next);
        }
    }
}
