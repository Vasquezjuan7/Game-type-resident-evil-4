using System;
using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>Piezas añadidas en F2-F8: toast, pistas de teclas clicables, silueta procedural y panel de niebla.</summary>
    public static class DEExtras
    {
        /// <summary>Hace clicable una pista de tecla del pie (para quien juega solo con raton).</summary>
        public static void Clickable(RectTransform hintsRow, int index, Action onClick)
        {
            if (hintsRow == null || index < 0 || index >= hintsRow.childCount) return;
            Transform h = hintsRow.GetChild(index);
            var img = h.GetComponent<Image>();
            if (img == null) img = h.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            var b = h.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() =>
            {
                UIAudio.Play(UISound.Confirmar);
                onClick();
            });
        }

        /// <summary>Borde de 1.5 px (cuatro lineas) alrededor de un rectangulo.</summary>
        public static void Outline(RectTransform rt, Color color, float w = 1.5f)
        {
            Sprite s = UITheme.Current.blanco;
            Image t = UIFx.Image(rt, "BordeA", s, color); t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = Vector2.one; t.rectTransform.pivot = new Vector2(0.5f, 1f); t.rectTransform.sizeDelta = new Vector2(0f, w);
            Image b = UIFx.Image(rt, "BordeB", s, color); b.rectTransform.anchorMin = Vector2.zero; b.rectTransform.anchorMax = new Vector2(1f, 0f); b.rectTransform.pivot = new Vector2(0.5f, 0f); b.rectTransform.sizeDelta = new Vector2(0f, w);
            Image l = UIFx.Image(rt, "BordeI", s, color); l.rectTransform.anchorMin = Vector2.zero; l.rectTransform.anchorMax = new Vector2(0f, 1f); l.rectTransform.pivot = new Vector2(0f, 0.5f); l.rectTransform.sizeDelta = new Vector2(w, 0f);
            Image r = UIFx.Image(rt, "BordeD", s, color); r.rectTransform.anchorMin = new Vector2(1f, 0f); r.rectTransform.anchorMax = Vector2.one; r.rectTransform.pivot = new Vector2(1f, 0.5f); r.rectTransform.sizeDelta = new Vector2(w, 0f);
        }

        // ------------------------------------------------------------ Silueta y panel de niebla

        private static Texture2D _silhouette;

        /// <summary>Silueta de busto (cabeza y hombros) con borde suave, en blanco con alfa (se tiñe con el color).</summary>
        public static Texture2D Silhouette()
        {
            if (_silhouette != null) return _silhouette;
            const int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    // cabeza (algo inclinada) y cuello
                    float dh = Vector2.Distance(new Vector2(u, v), new Vector2(0.51f, 0.66f)) / 0.115f;
                    float head = 1f - Mathf.SmoothStep(0.85f, 1.05f, dh);
                    float neck = (Mathf.Abs(u - 0.5f) < 0.055f && v > 0.44f && v < 0.58f) ? 1f : 0f;
                    // hombros: elipse ancha cortada por abajo
                    float ds = Mathf.Pow((u - 0.5f) / 0.33f, 2f) + Mathf.Pow((v - 0.18f) / 0.32f, 2f);
                    float body = (1f - Mathf.SmoothStep(0.9f, 1.05f, ds)) * (v < 0.46f ? 1f : 0f);
                    float a = Mathf.Clamp01(Mathf.Max(head, Mathf.Max(neck, body)));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _silhouette = tex;
            return tex;
        }

        /// <summary>
        /// Panel decorativo "figura en la niebla" (estilo pantalla de carga, blanco y negro). Si hay una imagen
        /// en vivo (la toma 1 del menu) la usa; si no, compone niebla + silueta + ceniza con un empuje lento.
        /// </summary>
        public static RectTransform FogFigurePanel(Transform parent, string name, Texture live)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Rect(parent, name);
            rt.gameObject.AddComponent<RectMask2D>();
            RectTransform push = UIFx.Stretch(UIFx.Rect(rt, "Empuje"));
            push.gameObject.AddComponent<DESlowPush>();

            if (live != null)
            {
                RawImage img = UIFx.Stretch(UIFx.Rect(push, "TomaEnVivo")).gameObject.AddComponent<RawImage>();
                img.texture = live;
                img.raycastTarget = false;
                // la toma es 16:9 y el panel casi cuadrado: recortar mostrando la parte derecha (donde esta la figura)
                img.uvRect = new Rect(0.42f, 0f, 0.56f, 1f);
            }
            else
            {
                Image bg = UIFx.Image(push, "Fondo", th.blanco, new Color(0.84f, 0.84f, 0.82f));
                UIFx.Stretch(bg.rectTransform);
                RawImage fig = UIFx.Rect(push, "Figura").gameObject.AddComponent<RawImage>();
                fig.texture = Silhouette();
                fig.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
                fig.raycastTarget = false;
                UIFx.Place(fig.rectTransform, new Vector2(0.58f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(520f, 640f));
            }
            for (int i = 0; i < 2; i++)
            {
                RawImage fog = UIFx.Stretch(UIFx.Rect(rt, "Niebla" + i)).gameObject.AddComponent<RawImage>();
                fog.texture = DEBuild.FogTexture(256, 128, 2.4f + i * 1.6f, 61 + i * 4, 1.25f);
                fog.uvRect = new Rect(0f, 0f, 1f + i * 0.4f, 1f);
                fog.color = new Color(0.95f, 0.95f, 0.93f, 1f);
                fog.raycastTarget = false;
                var d = fog.gameObject.AddComponent<FogDrift>();
                d.speed = new Vector2(0.008f + i * 0.005f, 0f) * (i == 0 ? 1f : -1f);
                d.baseAlpha = 0.28f + i * 0.08f;
                d.alphaPulse = 0.05f;
            }
            FallingAsh.Create(rt, 14, new Color(0.3f, 0.3f, 0.3f, 0.75f));
            // bordes que se funden con el fondo
            Image edge = UIFx.Image(rt, "Bordes", th.vineta, th.negroFondo);
            UIFx.Stretch(edge.rectTransform, -60f, -60f, -60f, -60f);
            return rt;
        }
    }
}
