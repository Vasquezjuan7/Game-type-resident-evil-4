using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Fila de opciones "‹ valor ›" con indicador de segmentos (Opciones). ←→ cambia el valor,
    /// ↑↓ pasa a otra fila, clic en la mitad izquierda/derecha del valor tambien lo cambia.
    /// Muestra ◆ cuando el valor no es el de fabrica.
    /// </summary>
    public class OptionRowSelector : GlassButton
    {
        public TextMeshProUGUI valueText;
        public TextMeshProUGUI arrowLeft, arrowRight;
        public RectTransform valueArea;
        public RectTransform segmentsRoot;
        public Image changedMark;

        public List<string> options = new List<string>();
        public int index;
        public int defaultIndex;
        public bool wrap;

        [System.Serializable] public class IntEvent : UnityEvent<int> { }
        public IntEvent onValueChanged = new IntEvent();

        private readonly List<Image> _segments = new List<Image>();

        public static OptionRowSelector Create(Transform parent, string label, IList<string> values, int value, int defaultValue, float width = 930f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, "Fila_" + label);
            rt.sizeDelta = new Vector2(width, th.alturaFila);
            Image hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var row = rt.gameObject.AddComponent<OptionRowSelector>();
            row.targetGraphic = hit;
            row.BuildGlass(label, TextAlignmentOptions.Left, th.filaSistema);
            row.options = new List<string>(values);
            row.index = Mathf.Clamp(value, 0, Mathf.Max(0, values.Count - 1));
            row.defaultIndex = defaultValue;
            row.BuildValue(width);
            row.Refresh();
            return row;
        }

        protected void BuildValue(float width)
        {
            UITheme th = UITheme.Current;
            // linea divisoria inferior de la fila
            Image div = UIFx.Image(transform, "Divisor", th.blanco, th.grisLinea);
            div.rectTransform.anchorMin = new Vector2(0f, 0f);
            div.rectTransform.anchorMax = new Vector2(1f, 0f);
            div.rectTransform.pivot = new Vector2(0.5f, 0f);
            div.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            div.transform.SetAsFirstSibling();

            valueArea = UIFx.Place(UIFx.Rect(transform, "Valor"), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width * 0.674f, 0f), new Vector2(width * 0.52f, th.alturaFila));
            arrowLeft = UIFx.Text(valueArea, "Izq", "‹", th.rajdhaniMedium, 34f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(arrowLeft.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(40f, 60f));
            arrowRight = UIFx.Text(valueArea, "Der", "›", th.rajdhaniMedium, 34f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(arrowRight.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(40f, 60f));
            valueText = UIFx.Text(valueArea, "Texto", "", th.rajdhaniMedium, th.filaSistema - 1f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(width * 0.42f, 40f));
            segmentsRoot = UIFx.Place(UIFx.Rect(valueArea, "Segmentos"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -19f), new Vector2(200f, 3f));

            changedMark = UIFx.Image(transform, "Cambiado", th.rombo, th.huesoClaro);
            UIFx.Place(changedMark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(27f, 0f), new Vector2(15f, 15f));
        }

        public void SetOptions(IList<string> values, int value)
        {
            options = new List<string>(values);
            index = Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void SetValueWithoutNotify(int value)
        {
            index = Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void Change(int delta)
        {
            if (options.Count == 0 || !IsInteractable()) return;
            int next = index + delta;
            if (wrap) next = (next % options.Count + options.Count) % options.Count;
            else next = Mathf.Clamp(next, 0, options.Count - 1);
            if (next == index)
            {
                UIAudio.Play(UISound.Error);
                return;
            }
            index = next;
            UIAudio.Play(UISound.Hover);
            Refresh();
            onValueChanged.Invoke(index);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) { Change(-1); eventData.Use(); return; }
            if (eventData.moveDir == MoveDirection.Right) { Change(1); eventData.Use(); return; }
            base.OnMove(eventData);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (valueArea != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(valueArea, eventData.position, eventData.pressEventCamera, out Vector2 local)
                && Mathf.Abs(local.x) < valueArea.rect.width * 0.6f)
            {
                Change(local.x < 0f ? -1 : 1);
                return;
            }
            base.OnPointerClick(eventData);
        }

        /// <summary>Las filas no "se pulsan": Enter no hace nada salvo que se asigne onClick.</summary>
        protected override void Press()
        {
            if (onClick.GetPersistentEventCount() > 0) base.Press();
        }

        public void Refresh()
        {
            if (valueText != null) valueText.text = options.Count > 0 ? options[index] : "-";
            if (changedMark != null) changedMark.enabled = index != defaultIndex;
            if (segmentsRoot == null) return;
            int count = Mathf.Clamp(options.Count, 0, 8);
            while (_segments.Count < count)
            {
                Image s = UIFx.Image(segmentsRoot, "Seg", UITheme.Current.blanco, Color.white);
                _segments.Add(s);
            }
            const float w = 22f, gap = 5f;
            float total = count * w + (count - 1) * gap;
            for (int i = 0; i < _segments.Count; i++)
            {
                bool show = i < count;
                _segments[i].gameObject.SetActive(show);
                if (!show) continue;
                RectTransform r = _segments[i].rectTransform;
                UIFx.Place(r, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-total * 0.5f + i * (w + gap), 0f), new Vector2(w, 3f));
                int active = options.Count > 8 ? Mathf.RoundToInt(index / (float)(options.Count - 1) * (count - 1)) : index;
                _segments[i].color = i == active ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
        }
    }
}
