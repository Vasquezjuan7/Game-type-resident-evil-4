using TMPro;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Texto de descripcion de una pantalla: muestra la descripcion del elemento seleccionado
    /// (solo de los elementos que estan dentro de su misma pantalla) con un fundido corto.
    /// Respeta Accesibilidad › Tamaño de texto y se traduce al cambiar de idioma.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DescriptionLabel : MonoBehaviour
    {
        [Tooltip("Pantalla cuyos elementos se escuchan (vacio = la pantalla padre).")]
        public Transform scope;

        private TextMeshProUGUI _text;
        private float _alpha = 1f;
        private float _baseSize;
        private DEButtonBase _current;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _baseSize = _text.fontSize;
            if (scope == null)
            {
                UIScreen s = GetComponentInParent<UIScreen>();
                scope = s != null ? s.transform : transform.parent;
            }
        }

        private void OnEnable()
        {
            DEButtonBase.OnAnySelected += OnSelected;
            Core.Loc.OnChanged += Retranslate;
            Core.SettingsService.OnApplied += OnSettings;
            OnSettings(Core.SettingsService.Current);
        }

        private void OnDisable()
        {
            DEButtonBase.OnAnySelected -= OnSelected;
            Core.Loc.OnChanged -= Retranslate;
            Core.SettingsService.OnApplied -= OnSettings;
        }

        private void OnSettings(Core.GameSettings s)
        {
            if (_text != null) _text.fontSize = _baseSize * s.TextScale;
        }

        private void Retranslate()
        {
            if (_current != null) _text.text = Core.Loc.T(_current.description);
        }

        private void OnSelected(DEButtonBase b)
        {
            if (b == null || scope == null || !b.transform.IsChildOf(scope)) return;
            _current = b;
            _text.text = Core.Loc.T(b.description);
            _alpha = Core.SettingsService.ReduceMotion ? 1f : 0f;
        }

        private void Update()
        {
            if (_alpha >= 1f) return;
            _alpha = Mathf.MoveTowards(_alpha, 1f, Time.unscaledDeltaTime / 0.15f);
            Color c = _text.color;
            c.a = _alpha;
            _text.color = c;
        }
    }
}
