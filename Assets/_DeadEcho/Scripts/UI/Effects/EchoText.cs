using DeadEcho.Core;
using TMPro;
using UnityEngine;

namespace DeadEcho.UI
{
    /// <summary>
    /// Efecto eco del logo: dos copias del texto se desplazan a la derecha y se desvanecen (3.2 s en loop).
    /// </summary>
    public class EchoText : MonoBehaviour
    {
        public float period = 3.2f;
        public float distance = 36f;

        private TextMeshProUGUI[] _copies;
        private Color _baseColor;

        public void Build(TextMeshProUGUI source, float travel)
        {
            distance = travel;
            _baseColor = source.color;
            _copies = new TextMeshProUGUI[2];
            for (int i = 0; i < 2; i++)
            {
                var copy = UIFx.Text(source.transform.parent, source.name + "_Eco" + (i + 1), source.text, source.font, source.fontSize,
                    _baseColor, source.alignment, source.characterSpacing);
                RectTransform rt = copy.rectTransform, src = source.rectTransform;
                rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
                rt.offsetMin = src.offsetMin; rt.offsetMax = src.offsetMax;
                rt.SetSiblingIndex(src.GetSiblingIndex()); // detras del texto original
                _copies[i] = copy;
            }
        }

        private void Update()
        {
            if (_copies == null) return;
            bool reduce = SettingsService.ReduceMotion;
            for (int i = 0; i < _copies.Length; i++)
            {
                var c = _copies[i];
                if (c == null) continue;
                if (reduce)
                {
                    c.enabled = false;
                    continue;
                }
                c.enabled = true;
                // cada copia va con medio ciclo de desfase
                float k = Mathf.Repeat(Time.unscaledTime / period + i * 0.5f, 1f);
                c.rectTransform.anchoredPosition = new Vector2(distance * k, 0f) + Base(c);
                c.color = UITheme.WithAlpha(_baseColor, 0.35f * (1f - k));
            }
        }

        private Vector2 _base0, _base1;
        private bool _hasBase;

        private Vector2 Base(TextMeshProUGUI c)
        {
            if (!_hasBase)
            {
                _base0 = _copies[0].rectTransform.anchoredPosition;
                _base1 = _copies[1].rectTransform.anchoredPosition;
                _hasBase = true;
            }
            return c == _copies[0] ? _base0 : _base1;
        }
    }
}
