using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Carga asincrona con la pantalla de carga (F3, 04_Carga.html): cielo nublado, ciudad en ruinas,
    /// logo arriba al centro, consejo aleatorio y "CARGANDO" con icono girando.
    /// Minimo 2 s en pantalla, activa la escena al 90 % y termina con un fundido a negro de 0.6 s.
    /// La pantalla es un Canvas que sobrevive al cambio de escena (no hace falta una escena "Loading").
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public const float MinSeconds = 2f;
        public const float FadeSeconds = 0.6f;

        public static bool IsLoading { get; private set; }

        private CanvasGroup _group;
        private Image _black;
        private RectTransform _spinner;
        private RectTransform _city;

        /// <summary>Carga una escena mostrando la pantalla de carga.</summary>
        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[DeadEcho] La escena '{sceneName}' no esta en Build Settings. Usa el menu Dead Echo > F1 · Crear escena MainMenu.");
                UIAudio.Play(UISound.Error);
                return;
            }
            var go = new GameObject("[SceneLoader]");
            DontDestroyOnLoad(go);
            var loader = go.AddComponent<SceneLoader>();
            loader.StartCoroutine(loader.Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            IsLoading = true;
            Build();

            // Fundido de entrada desde negro (la pantalla anterior desaparece debajo)
            _black.color = Color.black;
            yield return UIFx.Tween(0.35f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));

            // El juego de detras se congela mientras carga (si se viene de la pausa, los enemigos no atacan)
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Loading);
            Time.timeScale = 0f;
            AudioListener.pause = true;

            float start = Time.unscaledTime;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - start < MinSeconds) yield return null;

            // Fundido a negro y activar la escena
            yield return UIFx.Tween(FadeSeconds, k => _black.color = new Color(0f, 0f, 0f, k));
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            // La escena nueva aparece desde negro
            _group.alpha = 1f;
            foreach (Transform child in _group.transform) if (child != _black.transform) child.gameObject.SetActive(false);
            yield return null;
            yield return UIFx.Tween(0.5f, k => _black.color = new Color(0f, 0f, 0f, 1f - k));
            IsLoading = false;
            Destroy(gameObject);
        }

        private void OnDestroy() => IsLoading = false;

        private void Update()
        {
            if (_spinner != null) _spinner.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
            // empuje lento de la ciudad (40 s)
            if (_city != null && !SettingsService.ReduceMotion)
                _city.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, Mathf.PingPong(Time.unscaledTime / 40f, 1f));
        }

        // ------------------------------------------------------------ Construccion

        private void Build()
        {
            UITheme th = UITheme.Current;
            Canvas canvas = DEBuild.Canvas("Canvas_Carga", 1000, transform);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;
            Transform root = canvas.transform;

            // Cielo blanco nublado
            RawImage sky = UIFx.Stretch(UIFx.Rect(root, "Cielo")).gameObject.AddComponent<RawImage>();
            sky.texture = DEBuild.VerticalGradient(new Color(0.78f, 0.78f, 0.77f), new Color(0.55f, 0.55f, 0.55f));
            sky.raycastTarget = true;

            for (int i = 0; i < 2; i++)
            {
                RawImage clouds = UIFx.Stretch(UIFx.Rect(root, "Nubes" + i)).gameObject.AddComponent<RawImage>();
                clouds.texture = DEBuild.FogTexture(256, 128, 3f + i * 2f, 11 + i, 1.2f);
                clouds.color = new Color(1f, 1f, 1f, 0.6f);
                clouds.uvRect = new Rect(0f, 0f, 1.2f + i * 0.5f, 1f);
                clouds.rectTransform.anchorMin = new Vector2(0f, 0.35f);
                clouds.rectTransform.anchorMax = new Vector2(1f, 1f);
                var drift = clouds.gameObject.AddComponent<FogDrift>();
                drift.speed = new Vector2(0.006f + i * 0.006f, 0f);
                drift.baseAlpha = 0.55f - i * 0.15f;
                drift.alphaPulse = 0.06f;
            }

            // Ciudad en ruinas (silueta procedural, mas oscura delante)
            _city = UIFx.Rect(root, "Ciudad");
            _city.anchorMin = new Vector2(-0.05f, 0f);
            _city.anchorMax = new Vector2(1.05f, 0.62f);
            _city.offsetMin = _city.offsetMax = Vector2.zero;
            _city.pivot = new Vector2(0.5f, 0.3f);
            AddSkyline(_city, 7, new Color(0.42f, 0.42f, 0.42f), 0.95f, 0.55f);
            AddSkyline(_city, 3, new Color(0.2f, 0.2f, 0.2f), 0.75f, 1f);
            AddSkyline(_city, 5, new Color(0.08f, 0.08f, 0.08f), 0.38f, 0.8f);

            // Niebla baja
            RawImage fog = UIFx.Rect(root, "NieblaBaja").gameObject.AddComponent<RawImage>();
            fog.texture = DEBuild.FogTexture(256, 64, 4f, 3, 1.1f);
            fog.rectTransform.anchorMin = new Vector2(0f, 0f);
            fog.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            fog.rectTransform.offsetMin = fog.rectTransform.offsetMax = Vector2.zero;
            fog.uvRect = new Rect(0f, 0f, 1.5f, 1f);
            var fd = fog.gameObject.AddComponent<FogDrift>();
            fd.speed = new Vector2(0.01f, 0f);
            fd.baseAlpha = 0.5f;

            FallingAsh.Create(root, 45, new Color(0.15f, 0.15f, 0.15f, 0.7f));

            // Logo: DEAD grande (Oswald) + |echo|
            var dead = UIFx.Text(root, "DEAD", "DEAD", th.oswaldBold, 130f, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.Center, 10f);
            UIFx.Place(dead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 150f));
            var echo = UIFx.Text(root, "echo", "|e c h o|", th.specialElite, 50f, new Color(0.08f, 0.08f, 0.08f), TextAlignmentOptions.Center, 8f);
            UIFx.Place(echo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(600f, 70f));

            LensFX.Create(root, 1001, false);

            // Consejo
            var tipTitle = UIFx.Text(root, "ConsejoTitulo", "CONSEJO", th.rajdhaniMedium, 19f, th.huesoClaro, TextAlignmentOptions.Left, 8f);
            UIFx.Place(tipTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 112f), new Vector2(400f, 26f));
            var tip = UIFx.Text(root, "Consejo", LoadingTips.Current.Random(), th.rajdhaniMedium, 27f, th.hueso, TextAlignmentOptions.TopLeft);
            tip.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Place(tip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(96f, 104f), new Vector2(780f, 90f));

            // CARGANDO + icono girando
            var loading = UIFx.Text(root, "Cargando", "CARGANDO", th.rajdhaniSemiBold, 22f, th.hueso, TextAlignmentOptions.Right, 7f);
            UIFx.Place(loading.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-150f, 72f), new Vector2(400f, 40f));
            Image ring = UIFx.Image(root, "Icono", th.aro, th.hueso);
            UIFx.Place(ring.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-96f, 72f), new Vector2(36f, 36f));
            Image notch = UIFx.Image(ring.rectTransform, "Muesca", th.blanco, th.hueso);
            UIFx.Place(notch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(5f, 12f));
            _spinner = ring.rectTransform;

            // Capa negra para los fundidos (siempre encima)
            _black = UIFx.Image(root, "Negro", th.blanco, Color.black, true);
            UIFx.Stretch(_black.rectTransform);
            var top = _black.gameObject.AddComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = 1002;
        }

        /// <summary>Silueta de edificios en ruinas (textura procedural con alpha) a lo ancho de la ciudad.</summary>
        private static void AddSkyline(RectTransform parent, int seed, Color color, float heightFrac, float density)
        {
            const int w = 512, h = 160;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var rnd = new System.Random(seed);
            int x = 0;
            while (x < w)
            {
                int bw = rnd.Next(14, 46);
                bool gap = rnd.NextDouble() > density;
                // edificio central mas alto (como el de la referencia)
                float center = 1f - Mathf.Abs((x + bw * 0.5f) / w - 0.55f) * 1.6f;
                int bh = gap ? rnd.Next(4, 18) : (int)(h * Mathf.Clamp01(0.25f + (float)rnd.NextDouble() * 0.45f + Mathf.Max(0f, center) * 0.3f));
                int jag = rnd.Next(0, 3);
                for (int i = x; i < Mathf.Min(w, x + bw); i++)
                {
                    // tejados rotos: bordes irregulares
                    int top = bh - (jag > 0 ? rnd.Next(0, 10) * ((i - x) % 7 < 3 ? 1 : 0) : 0);
                    for (int y = 0; y < top && y < h; y++)
                    {
                        // ventanas vacias
                        bool window = !gap && y > 8 && y < top - 6 && (i - x) % 9 > 5 && y % 14 > 8 && rnd.NextDouble() > 0.3;
                        px[y * w + i] = window ? new Color32(0, 0, 0, 0) : (Color32)color;
                    }
                }
                x += bw;
            }
            tex.SetPixels32(px);
            tex.Apply();
            RawImage img = UIFx.Rect(parent, "Silueta" + seed).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = new Vector2(1f, heightFrac);
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
