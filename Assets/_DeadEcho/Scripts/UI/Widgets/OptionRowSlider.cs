using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Fila con deslizador "‹ – |----|---- + ›" y marcas (Opciones › Brillo, volumenes...).
    /// ←→ mueve el valor; manteniendo la tecla se acelera. Clic o arrastre sobre la pista lo fija.
    /// Muestra ◆ si difiere del valor por defecto.
    /// </summary>
    public class OptionRowSlider : GlassButton, IDragHandler, IPointerDownHandler
    {
        public RectTransform track;
        public Image handle;
        public TextMeshProUGUI valueText;
        public Image changedMark;

        public float min;
        public float max = 100f;
        public float step = 1f;
        public float value = 50f;
        public float defaultValue = 50f;
        public string format = "0";

        [System.Serializable] public class FloatEvent : UnityEvent<float> { }
        public FloatEvent onValueChanged = new FloatEvent();

        private float _lastStepTime;
        private int _repeat;

        public static OptionRowSlider Create(Transform parent, string label, float min, float max, float value, float defaultValue, float step = 1f, float width = 930f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Fila_" + label);
            rt.sizeDelta = new Vector2(width, th.alturaFila);
            Image hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var row = rt.gameObject.AddComponent<OptionRowSlider>();
            row.targetGraphic = hit;
            row.BuildGlass(label, TextAlignmentOptions.Left, th.filaSistema);
            row.min = min;
            row.max = max;
            row.step = step;
            row.value = Mathf.Clamp(value, min, max);
            row.defaultValue = defaultValue;
            row.BuildSlider(width);
            row.Refresh();
            return row;
        }

        private void BuildSlider(float width)
        {
            UITheme th = UITheme.Current;
            Image div = UIFx.Image(transform, "Divisor", th.blanco, th.grisLinea);
            div.rectTransform.anchorMin = new Vector2(0f, 0f);
            div.rectTransform.anchorMax = new Vector2(1f, 0f);
            div.rectTransform.pivot = new Vector2(0.5f, 0f);
            div.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            div.transform.SetAsFirstSibling();

            var minus = UIFx.Text(transform, "Menos", "‹ –", th.rajdhaniMedium, 26f, th.huesoClaro, TextAlignmentOptions.Right);
            UIFx.Place(minus.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(width * 0.425f, 0f), new Vector2(60f, 40f));
            var plus = UIFx.Text(transform, "Mas", "+ ›", th.rajdhaniMedium, 26f, th.huesoClaro, TextAlignmentOptions.Left);
            UIFx.Place(plus.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width * 0.845f, 0f), new Vector2(60f, 40f));

            track = UIFx.Place(UIFx.Rect(transform, "Pista"), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width * 0.43f, 0f), new Vector2(width * 0.405f, 24f));
            Image line = UIFx.Image(track, "Linea", th.blanco, new Color(1f, 1f, 1f, 0.35f));
            UIFx.Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 1.5f));
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            // 11 marcas (las de los extremos y el centro mas largas)
            for (int i = 0; i <= 10; i++)
            {
                bool big = i == 0 || i == 5 || i == 10;
                Image tick = UIFx.Image(track, "Marca" + i, th.blanco, new Color(1f, 1f, 1f, big ? 0.55f : 0.35f));
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(i / 10f, 0.5f);
                tick.rectTransform.sizeDelta = new Vector2(1.5f, big ? 15f : 7f);
            }
            handle = UIFx.Image(track, "Tirador", th.blanco, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(3f, 24f);

            valueText = UIFx.Text(transform, "Valor", "", th.rajdhaniMedium, th.filaSistema - 1f, th.huesoClaro, TextAlignmentOptions.Left);
            UIFx.Place(valueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width * 0.91f, 0f), new Vector2(80f, 40f));

            changedMark = UIFx.Image(transform, "Cambiado", th.rombo, th.huesoClaro);
            UIFx.Place(changedMark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(27f, 0f), new Vector2(15f, 15f));
        }

        public void SetValueWithoutNotify(float v)
        {
            value = Snap(v);
            Refresh();
        }

        private float Snap(float v) => Mathf.Clamp(Mathf.Round((v - min) / step) * step + min, min, max);

        public void SetValue(float v)
        {
            float next = Snap(v);
            if (Mathf.Approximately(next, value)) return;
            value = next;
            Refresh();
            onValueChanged.Invoke(value);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                // mantener pulsado = rapido
                _repeat = Time.unscaledTime - _lastStepTime < 0.25f ? _repeat + 1 : 0;
                _lastStepTime = Time.unscaledTime;
                float mult = _repeat > 8 ? 5f : _repeat > 3 ? 2f : 1f;
                float before = value;
                SetValue(value + (eventData.moveDir == MoveDirection.Right ? step : -step) * mult);
                if (!Mathf.Approximately(before, value)) UIAudio.Play(UISound.Hover);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            SetFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

        private void SetFromPointer(PointerEventData e)
        {
            if (track == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out Vector2 local)) return;
            float t = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
            if (local.y < -track.rect.height * 2f || local.y > track.rect.height * 2f) return;
            SetValue(Mathf.Lerp(min, max, t));
        }

        protected override void Press()
        {
            if (onClick.GetPersistentEventCount() > 0) base.Press();
        }

        public void Refresh()
        {
            float t = Mathf.InverseLerp(min, max, value);
            if (handle != null)
            {
                handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(t, 0.5f);
                handle.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (valueText != null) valueText.text = value.ToString(format);
            if (changedMark != null) changedMark.enabled = !Mathf.Approximately(value, defaultValue);
        }
    }
}
