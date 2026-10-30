using System;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Piezas comunes de las pantallas de Dead Echo construidas en tiempo de ejecucion:
    /// Canvas 1920x1080, EventSystem, logo, pie de controles, modales de confirmacion y texturas de niebla.
    /// </summary>
    public static class DEBuild
    {
        /// <summary>Canvas a pantalla completa con Canvas Scaler 1920x1080 (match 0.5).</summary>
        public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Crea un EventSystem con el Input System nuevo si la escena no tiene ninguno.</summary>
        public static EventSystem EnsureEventSystem()
        {
            if (EventSystem.current != null) return EventSystem.current;
            EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return existing;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().moveRepeatDelay = 0.35f;
            return go.GetComponent<EventSystem>();
        }

        // ------------------------------------------------------------ Logo

        /// <summary>Logo: "DEAD" espaciado + "echo" de maquina de escribir sobre trazo de pincel, con efecto eco.</summary>
        public static RectTransform Logo(Transform parent, float scale = 1f, bool withEcho = true)
        {
            UITheme th = UITheme.Current;
            RectTransform root = UIFx.Rect(parent, "Logo");
            root.sizeDelta = new Vector2(600f, 190f) * scale;

            var dead = UIFx.Text(root, "DEAD", "DEAD", th.specialElite, 66f * scale, th.hueso, TextAlignmentOptions.Left, 22f);
            UIFx.Place(dead.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(75f * scale, 0f), new Vector2(500f * scale, 80f * scale));

            Image brush = UIFx.Image(root, "Pincel", th.pincelBlanco, th.hueso);
            UIFx.Place(brush.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -78f * scale), new Vector2(590f * scale, 105f * scale));

            var echo = UIFx.Text(brush.rectTransform, "echo", "|echo     |", th.specialElite, 88f * scale, th.negroFondo, TextAlignmentOptions.Left, 18f);
            UIFx.Stretch(echo.rectTransform, 60f * scale, 0f, 0f, 6f * scale);
            echo.alignment = TextAlignmentOptions.MidlineLeft;

            if (withEcho)
            {
                var fx = echo.gameObject.AddComponent<EchoText>();
                fx.Build(echo, 36f * scale);
            }
            return root;
        }

        // ------------------------------------------------------------ Pie de controles

        /// <summary>Fila de pistas de teclas centrada o alineada a la izquierda.</summary>
        public static RectTransform HintsRow(Transform parent, bool centered, params KeyHintDef[] hints)
        {
            RectTransform row = UIFx.Rect(parent, "Controles");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            foreach (var h in hints) KeyHint.Create(row, h.keyboard, h.gamepad, h.text, h.size);
            return row;
        }

        public struct KeyHintDef
        {
            public string keyboard, gamepad, text;
            public float size;
            public KeyHintDef(string keyboard, string gamepad, string text, float size = 26f)
            {
                this.keyboard = keyboard; this.gamepad = gamepad; this.text = text; this.size = size;
            }
        }

        // ------------------------------------------------------------ Modales

        /// <summary>
        /// Modal de confirmacion estilo pausa (09_Salir.html): titulo, texto opcional y dos botones vidrio.
        /// "No" queda seleccionado por defecto.
        /// </summary>
        public static UIScreen ConfirmModal(Transform canvas, string name, string title, string body, string noText, string yesText,
            Action onYes, Action onNo = null)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, name));
            rt.gameObject.AddComponent<CanvasGroup>();
            var screen = rt.gameObject.AddComponent<UIScreen>();

            Image dim = UIFx.Image(rt, "Oscurecer", th.blanco, new Color(0f, 0f, 0f, 0.78f), true);
            UIFx.Stretch(dim.rectTransform);

            Image box = UIFx.Image(rt, "Caja", th.blanco, new Color(0.06f, 0.06f, 0.06f, 0.96f));
            UIFx.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, body != null ? 400f : 330f));
            Image line = UIFx.Image(box.rectTransform, "Borde", th.blanco, UITheme.WithAlpha(th.hueso, 0.18f));
            UIFx.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(780f, 1.5f));

            var t = UIFx.Text(box.rectTransform, "Titulo", title, th.rajdhaniSemiBold, 46f, th.hueso, TextAlignmentOptions.Center, 1f);
            UIFx.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(740f, 64f));
            float y = -112f;
            if (!string.IsNullOrEmpty(body))
            {
                var b = UIFx.Text(box.rectTransform, "Texto", body, th.rajdhaniMedium, th.textoNormal, th.grisMedio, TextAlignmentOptions.Center);
                b.textWrappingMode = TextWrappingModes.Normal;
                UIFx.Place(b.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(680f, 70f));
                y -= 74f;
            }

            var no = GlassButton.Create(box.rectTransform, noText, new Vector2(480f, 60f));
            UIFx.Place((RectTransform)no.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(480f, 60f));
            var yes = GlassButton.Create(box.rectTransform, yesText, new Vector2(480f, 60f));
            UIFx.Place((RectTransform)yes.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 66f), new Vector2(480f, 60f));
            LinkVertical(no, yes);

            no.onClick.AddListener(() =>
            {
                if (screen.Router != null) screen.Router.Pop();
                onNo?.Invoke();
            });
            yes.onClick.AddListener(() => onYes?.Invoke());
            screen.firstSelected = no;
            return screen;
        }

        /// <summary>Panel de solo texto (Tutoriales, Cargar partida DEMO...). Esc vuelve.</summary>
        public static UIScreen InfoScreen(Transform canvas, string name, string title, string body)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, name));
            rt.gameObject.AddComponent<CanvasGroup>();
            var screen = rt.gameObject.AddComponent<UIScreen>();

            Image dim = UIFx.Image(rt, "Oscurecer", th.blanco, new Color(0f, 0f, 0f, 0.82f), true);
            UIFx.Stretch(dim.rectTransform);

            var t = UIFx.Text(rt, "Titulo", title, th.rajdhaniSemiBold, th.tituloPantalla, th.hueso, TextAlignmentOptions.Center, 3f);
            UIFx.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 80f));

            var b = UIFx.Text(rt, "Texto", body, th.rajdhaniMedium, th.textoNormal + 2f, th.huesoClaro, TextAlignmentOptions.TopLeft);
            b.textWrappingMode = TextWrappingModes.Normal;
            b.lineSpacing = 18f;
            UIFx.Place(b.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 620f));

            // boton invisible para que la pantalla tenga seleccion (Enter tambien vuelve)
            var back = GlassButton.Create(rt, "Volver", new Vector2(320f, 58f));
            UIFx.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(320f, 58f));
            back.onClick.AddListener(() => { if (screen.Router != null) screen.Router.Pop(); });
            screen.firstSelected = back;

            var hints = HintsRow(rt, true, new KeyHintDef("Esc", "B", "Volver"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(800f, 40f));
            return screen;
        }

        /// <summary>Navegacion explicita arriba/abajo entre una lista de elementos (con salto al final).</summary>
        public static void LinkVertical(params Selectable[] items)
        {
            var list = new System.Collections.Generic.List<Selectable>();
            foreach (var s in items) if (s != null) list.Add(s);
            for (int i = 0; i < list.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit, wrapAround = true };
                nav.selectOnUp = FindInteractable(list, i, -1);
                nav.selectOnDown = FindInteractable(list, i, +1);
                list[i].navigation = nav;
            }
        }

        // los deshabilitados se saltan (03_ESPECIFICACION_FUNCIONAL.md)
        private static Selectable FindInteractable(System.Collections.Generic.List<Selectable> list, int from, int dir)
        {
            for (int k = 1; k <= list.Count; k++)
            {
                int j = ((from + dir * k) % list.Count + list.Count) % list.Count;
                if (list[j].interactable) return list[j];
            }
            return null;
        }

        // ------------------------------------------------------------ Texturas procedurales

        /// <summary>Textura de niebla / nubes con ruido Perlin (tileable en horizontal).</summary>
        public static Texture2D FogTexture(int w, int h, float scale, int seed, float contrast = 1.4f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            float ox = seed * 13.7f, oy = seed * 7.3f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float n = 0f, amp = 0.55f, f = scale;
                    for (int o = 0; o < 4; o++)
                    {
                        // mezcla de dos muestras para que se repita sin costura en horizontal
                        float a = Mathf.PerlinNoise(ox + u * f, oy + v * f);
                        float b = Mathf.PerlinNoise(ox + (u - 1f) * f, oy + v * f);
                        n += Mathf.Lerp(a, b, u) * amp;
                        amp *= 0.5f;
                        f *= 2f;
                    }
                    n = Mathf.Clamp01((n - 0.32f) * contrast);
                    // los bordes de arriba y abajo se desvanecen
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) * 3f);
                    byte a8 = (byte)(n * edge * 255f);
                    px[y * w + x] = new Color32(255, 255, 255, a8);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Degradado vertical (abajo → arriba) en una textura de 1x64.</summary>
        public static Texture2D VerticalGradient(Color bottom, Color top)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            return tex;
        }
    }
}
