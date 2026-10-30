using System;
using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Ayudas para construir y animar la interfaz por codigo. Todas las animaciones usan tiempo
    /// sin escala (funcionan en pausa) y respetan "Reducir movimiento".
    /// </summary>
    public static class UIFx
    {
        public static float Ease(float t) => t * t * (3f - 2f * t); // ease in-out suave

        /// <summary>Anima de 0 a 1 en 'seconds' (instantaneo si Reducir movimiento esta activo y skipOnReduce).</summary>
        public static IEnumerator Tween(float seconds, Action<float> step, bool skipOnReduce = false)
        {
            if (seconds <= 0f || (skipOnReduce && SettingsService.ReduceMotion))
            {
                step(1f);
                yield break;
            }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                step(Ease(Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            step(1f);
        }

        public static IEnumerator Fade(CanvasGroup g, float to, float seconds)
        {
            float from = g.alpha;
            yield return Tween(seconds, k => g.alpha = Mathf.Lerp(from, to, k));
        }

        // ------------------------------------------------------------ Construccion

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Estira el rectangulo a todo el padre (con margenes opcionales).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }
    }
}
