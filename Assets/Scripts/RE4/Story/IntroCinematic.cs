using System.Collections;
using System.IO;
using DeadEcho.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Gameplay.RE4
{
    /// <summary>
    /// Cinematica de introduccion narrada. Se reproduce al empezar una NUEVA partida (Menu › Nueva partida),
    /// en cuanto se carga el pueblo y antes de que Leon pueda moverse:
    ///  - el juego queda en pausa (tiempo y sonidos), Leon bloqueado y el HUD tapado;
    ///  - se puede saltar MANTENIENDO Espacio / Enter / Esc / (A) un segundo;
    ///  - al acabar, fundido a negro y el juego empieza (la radio de Ingrid espera a que termine).
    /// El video esta en StreamingAssets/Cinematicas (asi se copia tal cual a la build, sin recodificar).
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        public const string VideoFile = "Cinematicas/cinematica_re4_narrada.mp4";
        private const float HoldToSkip = 1f;

        /// <summary>La introduccion se esta reproduciendo (la historia y la pausa esperan).</summary>
        public static bool Playing { get; private set; }

        private VideoPlayer _video;
        private AudioSource _audio;
        private RenderTexture _rt;
        private CanvasGroup _black;
        private CanvasGroup _videoGroup;
        private RawImage _screen;
        private AspectRatioFitter _fitter;
        private Image _skipFill;
        private CanvasGroup _skipGroup;
        private bool _failed;

        // ------------------------------------------------------------ Arranque

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryStart();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryStart();

        private static void TryStart()
        {
            if (!GameSession.IntroPending || GameSession.IsRestoring || Playing) return;
            if (FindAnyObjectByType<FirstPersonController>() == null) return; // no es la escena del pueblo
            GameSession.IntroPending = false;
            string path = Path.Combine(Application.streamingAssetsPath, VideoFile);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Intro] No se encontro la cinematica: " + path);
                return;
            }
            Playing = true; // desde ya: la historia no arranca la radio hasta que acabe
            var go = new GameObject("Cinematica_Intro");
            go.AddComponent<IntroCinematic>().StartCoroutine(go.GetComponent<IntroCinematic>().Run(path));
        }

        // ------------------------------------------------------------ Reproduccion

        private IEnumerator Run(string path)
        {
            BuildUI();
            _black.alpha = 1f;

            // Leon bloqueado desde el primer fotograma y sin sonido del juego
            AudioListener.pause = true;
            FirstPersonController fps = null;
            for (int i = 0; i < 30 && (fps = FirstPersonController.Instance) == null; i++) yield return null;
            if (fps != null) { fps.PushInputLock(); fps.LockCursor(true); }

            // Esperar a que se quite la pantalla de carga (usa el tiempo normal)
            float wait = 0f;
            while (DeadEcho.UI.SceneLoader.IsLoading && wait < 15f) { wait += Time.unscaledDeltaTime; yield return null; }

            // Pausar el mundo: nadie se mueve ni ataca mientras dura la cinematica
            float oldScale = Time.timeScale;
            Time.timeScale = 0f;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.ignoreListenerPause = true; // el juego esta en pausa, la cinematica no
            _audio.spatialBlend = 0f;
            _audio.volume = AudioChannels.AsVoice(_audio, 1f);

            _video = gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.source = VideoSource.Url;
            _video.url = path;
            _video.isLooping = false;
            _video.skipOnDrop = true;
            _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _video.EnableAudioTrack(0, true);
            _video.SetTargetAudioSource(0, _audio);
            _video.errorReceived += (vp, msg) => { _failed = true; Debug.LogWarning("[Intro] Error de video: " + msg); };
            _video.Prepare();

            float t = 0f;
            while (!_video.isPrepared && !_failed && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }

            if (!_failed && _video.isPrepared)
            {
                int w = (int)Mathf.Max(16, _video.width), h = (int)Mathf.Max(16, _video.height);
                _rt = new RenderTexture(w, h, 0) { name = "Intro_RT" };
                _video.targetTexture = _rt;
                _screen.texture = _rt;
                _fitter.aspectRatio = w / (float)h;
                _video.Play();

                // esperar al primer fotograma para no ver un frame negro/gris
                t = 0f;
                while (_video.frame < 1 && !_failed && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
                _videoGroup.alpha = 1f;
                yield return Fade(_black, 1f, 0f, 0.8f);

                double length = _video.length > 0 ? _video.length : 600;
                float held = 0f, elapsed = 0f;
                while (!_failed)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bool ended = (!_video.isPlaying && elapsed > 1f) || (_video.frameCount > 0 && (ulong)_video.frame >= _video.frameCount - 1) || elapsed > length + 3.0;
                    if (ended) break;

                    // Saltar: mantener pulsado (evita saltarla sin querer)
                    if (SkipHeld()) held += Time.unscaledDeltaTime;
                    else held = Mathf.Max(0f, held - Time.unscaledDeltaTime * 2f);
                    _skipFill.fillAmount = held / HoldToSkip;
                    float hintTarget = held > 0f || AnyInput() ? 1f : (elapsed < 4f ? 0.9f : 0f);
                    _skipGroup.alpha = Mathf.MoveTowards(_skipGroup.alpha, hintTarget, Time.unscaledDeltaTime * (hintTarget > 0f ? 4f : 1f));
                    if (held >= HoldToSkip) break;
                    yield return null;
                }

                // Fundido a negro y bajar la voz
                float v0 = _audio.volume;
                for (float f = 0f; f < 0.6f; f += Time.unscaledDeltaTime)
                {
                    _black.alpha = f / 0.6f;
                    _audio.volume = Mathf.Lerp(v0, 0f, f / 0.6f);
                    yield return null;
                }
                _black.alpha = 1f;
                _video.Stop();
            }

            // Devolver el control
            _videoGroup.alpha = 0f;
            _skipGroup.alpha = 0f;
            Time.timeScale = oldScale <= 0f ? 1f : oldScale;
            AudioListener.pause = false;
            if (fps != null) fps.PopInputLock();
            Playing = false;
            yield return Fade(_black, 1f, 0f, 1.2f);
            Destroy(gameObject);
        }

        private static bool SkipHeld()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.escapeKey.isPressed)) ||
                   (pad != null && (pad.buttonSouth.isPressed || pad.startButton.isPressed));
        }

        private static bool AnyInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return (kb != null && kb.anyKey.isPressed) || (mouse != null && mouse.leftButton.isPressed) ||
                   (pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.startButton.isPressed));
        }

        private static IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
        {
            for (float f = 0f; f < seconds; f += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, f / seconds);
                yield return null;
            }
            g.alpha = to;
        }

        private void OnDestroy()
        {
            if (Playing)
            {
                // por si se destruye a mitad (cambio de escena): no dejar el juego congelado
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Playing = false;
            }
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        // ------------------------------------------------------------ Interfaz

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // por encima del HUD, la pausa y la pantalla de carga
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Fondo negro (bandas) + video ajustado sin deformar
            RectTransform bg = Rect("Fondo", transform);
            bg.gameObject.AddComponent<Image>().color = Color.black;
            _videoGroup = bg.gameObject.AddComponent<CanvasGroup>();
            _videoGroup.alpha = 0f;

            RectTransform scr = Rect("Video", bg);
            _screen = scr.gameObject.AddComponent<RawImage>();
            _screen.color = Color.white;
            _fitter = scr.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = 16f / 9f;

            // Aviso para saltar (abajo a la derecha) con anillo que se llena
            RectTransform skip = Rect("Saltar", transform);
            skip.anchorMin = skip.anchorMax = new Vector2(1f, 0f);
            skip.pivot = new Vector2(1f, 0f);
            skip.sizeDelta = new Vector2(520f, 60f);
            skip.anchoredPosition = new Vector2(-48f, 40f);
            _skipGroup = skip.gameObject.AddComponent<CanvasGroup>();
            _skipGroup.alpha = 0f;

            RectTransform ringBack = Rect("Anillo", skip);
            ringBack.anchorMin = ringBack.anchorMax = new Vector2(1f, 0.5f);
            ringBack.pivot = new Vector2(1f, 0.5f);
            ringBack.sizeDelta = new Vector2(34f, 34f);
            ringBack.anchoredPosition = Vector2.zero;
            var back = ringBack.gameObject.AddComponent<Image>();
            back.sprite = Ring();
            back.color = new Color(1f, 1f, 1f, 0.25f);
            RectTransform ringFill = Rect("Relleno", ringBack);
            _skipFill = ringFill.gameObject.AddComponent<Image>();
            _skipFill.sprite = Ring();
            _skipFill.type = Image.Type.Filled;
            _skipFill.fillMethod = Image.FillMethod.Radial360;
            _skipFill.fillOrigin = 2;
            _skipFill.fillAmount = 0f;
            _skipFill.color = new Color(0.93f, 0.91f, 0.87f, 0.95f);

            RectTransform label = Rect("Texto", skip);
            label.offsetMax = new Vector2(-48f, 0f);
            var txt = label.gameObject.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleRight;
            txt.color = new Color(0.93f, 0.91f, 0.87f, 0.85f);
            txt.text = Loc.IsEnglish ? "Hold SPACE to skip" : "Mantén ESPACIO para saltar";

            // Negro de fundido por encima de todo
            RectTransform black = Rect("Negro", transform);
            black.gameObject.AddComponent<Image>().color = Color.black;
            _black = black.gameObject.AddComponent<CanvasGroup>();
            _black.blocksRaycasts = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Sprite _ring;

        /// <summary>Anillo dibujado por codigo (indicador de "mantener para saltar").</summary>
        private static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01((1f - d) * 20f) * Mathf.Clamp01((d - 0.72f) * 20f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            return _ring;
        }
    }
}
