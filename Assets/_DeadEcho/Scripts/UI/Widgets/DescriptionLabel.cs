using TMPro;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Texto de descripcion de una pantalla: muestra la descripcion del elemento seleccionado
    /// (solo de los elementos que estan dentro de su misma pantalla) con un fundido corto.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DescriptionLabel : MonoBehaviour
    {
        [Tooltip("Pantalla cuyos elementos se escuchan (vacio = la pantalla padre).")]
        public Transform scope;

        private TextMeshProUGUI _text;
        private float _alpha = 1f;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            if (scope == null)
            {
                UIScreen s = GetComponentInParent<UIScreen>();
                scope = s != null ? s.transform : transform.parent;
            }
        }

        private void OnEnable() => DEButtonBase.OnAnySelected += OnSelected;
        private void OnDisable() => DEButtonBase.OnAnySelected -= OnSelected;

        private void OnSelected(DEButtonBase b)
        {
            if (b == null || scope == null || !b.transform.IsChildOf(scope)) return;
            _text.text = b.description;
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
