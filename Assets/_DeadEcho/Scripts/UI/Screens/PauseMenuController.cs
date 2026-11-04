using System.Collections;
using System.Text;
using DeadEcho.Core;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Pausa de Dead Echo conectada al juego RE4 (F6, 06_Pausa.html). Se crea sola en cualquier escena
    /// que tenga a Leon (FirstPersonController). Esc / Start: detiene el tiempo, atenua el audio, captura
    /// el fondo y lo desenfoca. Mientras esta abierta bloquea el control de Leon (disparo, maletin, documentos...).
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private FirstPersonController _fps;
        private Canvas _canvas;
        private CanvasGroup _backGroup;
        private RawImage _backdrop;
        private RenderTexture _blurRT;
        private UIRouter _router;
        private ActionScreen _pause;
        private UIScreen _tutorials, _loadGame, _restartModal, _quitModal;
        private OptionsScreen _options;
        private DEToast _toast;

        private bool _blockedLastFrame = true;
        private bool _opening;
        private int _openedFrame = -1, _closedFrame = -1;
        private bool _holdingPause;

        // ------------------------------------------------------------ Arranque automatico

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate();

        private static void TryCreate()
        {
            if (FindAnyObjectByType<PauseMenuController>() != null) return;
            if (FindAnyObjectByType<FirstPersonController>() == null) return; // no es una escena de juego
            new GameObject("[DeadEcho_Pausa]").AddComponent<PauseMenuController>();
        }

        private void Start()
        {
            IsOpen = false;
            DEBuild.EnsureEventSystem();
            Build();
            // El Canvas queda activo pero invisible: asi el UIRouter hace su Start antes de la primera pausa
            _backGroup.alpha = 0f;
            _backGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                AudioListener.pause = false;
            }
            ReleaseBlur();
        }

        // ------------------------------------------------------------ Abrir / cerrar

        private FirstPersonController Fps => _fps != null ? _fps : (_fps = FirstPersonController.Instance);

        /// <summary>Otra interfaz del juego (maletin, mercader, documentos, conversacion, muerte...) ya tiene el control.</summary>
        private bool Blocked()
        {
            if (IsOpen || _opening || SceneLoader.IsLoading) return true;
            if (Time.timeScale <= 0f) return true;
            if (Fps != null && Fps.InputLocked) return true;
            return PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool start = pad != null && pad.startButton.wasPressedThisFrame;

            if (!IsOpen)
            {
                // el Esc que cierra el maletin o un documento no debe abrir la pausa en el mismo fotograma
                if ((esc || start) && !_blockedLastFrame && !Blocked() && Time.frameCount != _closedFrame)
                    StartCoroutine(Open());
                return;
            }

            // Start tambien cierra la pausa (Esc / B ya lo gestiona el UIRouter)
            if (start && Time.frameCount != _openedFrame && _router.Top == _pause) Resume();
        }

        private void LateUpdate() => _blockedLastFrame = Blocked();

        private IEnumerator Open()
        {
            _opening = true;
            // capturar el juego antes de dibujar la pausa
            yield return new WaitForEndOfFrame();
            CaptureBlurredBackground();

            IsOpen = true;
            _opening = false;
            _openedFrame = Time.frameCount;
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.RequestPause();
                GameStateManager.Instance.SetState(GameState.Paused);
                _holdingPause = true;
            }
            else Time.timeScale = 0f;
            AudioListener.pause = true;
            if (Fps != null)
            {
                Fps.PushInputLock();
                Fps.LockCursor(false);
            }

            _backGroup.alpha = 0f;
            _backGroup.blocksRaycasts = true;
            StartCoroutine(UIFx.Fade(_backGroup, 1f, 0.25f));
            _router.Push(_pause);
        }

        /// <summary>Cierra la pausa y devuelve el control a Leon.</summary>
        public void Resume()
        {
            if (!IsOpen) return;
            while (_router.Count > 0) _router.Pop();
            // sin seleccion: Enter / Espacio / WASD durante el juego no deben llegar a los botones de la pausa
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            ReleaseGame(true);
            StartCoroutine(FadeOutCanvas());
        }

        /// <summary>Restaura tiempo, audio y control. lockCursor = false cuando se va a cambiar de escena.</summary>
        private void ReleaseGame(bool lockCursor)
        {
            IsOpen = false;
            _closedFrame = Time.frameCount;
            if (_holdingPause && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ReleasePause();
                GameStateManager.Instance.SetState(GameState.Playing);
            }
            else if (!_holdingPause) Time.timeScale = 1f;
            _holdingPause = false;
            AudioListener.pause = false;
            if (Fps != null)
            {
                Fps.PopInputLock();
                if (lockCursor)
                {
                    Fps.LockCursor(true);
                    Fps.IgnoreEscapeFrame = Time.frameCount; // el mismo Esc no debe volver a soltar el raton
                }
            }
        }

        private IEnumerator FadeOutCanvas()
        {
            yield return UIFx.Fade(_backGroup, 0f, 0.2f);
            if (!IsOpen)
            {
                _backGroup.blocksRaycasts = false;
                ReleaseBlur();
            }
        }

        /// <summary>Sale de la pausa hacia otra escena (reiniciar o menu principal) con la pantalla de carga.</summary>
        private void LeaveTo(string scene)
        {
            if (SceneLoader.IsLoading) return;
            // El tiempo sigue parado hasta que la escena nueva este lista (SceneLoader lo restaura)
            IsOpen = false;
            if (Fps != null) Fps.PopInputLock();
            SceneLoader.Load(scene);
        }

        // ------------------------------------------------------------ Fondo desenfocado

        private void CaptureBlurredBackground()
        {
            ReleaseBlur();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return;
            // desenfoque barato: reducir en pasos y volver a ampliar con filtro bilineal
            int w = Mathf.Max(16, shot.width / 4), h = Mathf.Max(16, shot.height / 4);
            RenderTexture a = RenderTexture.GetTemporary(w, h, 0);
            RenderTexture b = RenderTexture.GetTemporary(w / 4, h / 4, 0);
            RenderTexture c = RenderTexture.GetTemporary(w / 16 + 1, h / 16 + 1, 0);
            a.filterMode = b.filterMode = c.filterMode = FilterMode.Bilinear;
            Graphics.Blit(shot, a);
            Graphics.Blit(a, b);
            Graphics.Blit(b, c);
            _blurRT = new RenderTexture(w / 4, h / 4, 0) { filterMode = FilterMode.Bilinear };
            Graphics.Blit(c, _blurRT);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
            RenderTexture.ReleaseTemporary(c);
            Destroy(shot);
            _backdrop.texture = _blurRT;
            _backdrop.color = new Color(0.55f, 0.55f, 0.55f, 1f); // oscurecido y desaturado por el overlay
        }

        private void ReleaseBlur()
        {
            if (_backdrop != null) _backdrop.texture = null;
            if (_blurRT != null)
            {
                _blurRT.Release();
                Destroy(_blurRT);
                _blurRT = null;
            }
        }

        // ------------------------------------------------------------ Construccion

        private void Build()
        {
            UITheme th = UITheme.Current;
            _canvas = DEBuild.Canvas("Canvas_Pausa", 900, transform);
            Transform root = _canvas.transform;
            _backGroup = _canvas.gameObject.AddComponent<CanvasGroup>();

            _backdrop = UIFx.Stretch(UIFx.Rect(root, "FondoJuego")).gameObject.AddComponent<RawImage>();
            _backdrop.raycastTarget = true; // el raton no atraviesa la pausa
            Image dark = UIFx.Image(root, "Oscurecer", th.blanco, new Color(0.04f, 0.04f, 0.04f, 0.62f));
            UIFx.Stretch(dark.rectTransform);
            Image vignette = UIFx.Image(root, "Vineta", th.vineta, new Color(0f, 0f, 0f, 0.75f));
            UIFx.Stretch(vignette.rectTransform);

            // franjas de luz verticales en los bordes
            float[] strips = { 14f, 36f, 56f, 78f, 100f };
            foreach (float x in strips)
            {
                float a = 0.05f + 0.06f * (1f - x / 100f);
                Image l = UIFx.Image(root, "FranjaIzq", th.blanco, new Color(1f, 1f, 1f, a));
                UIFx.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x * 1.5f, 0f), new Vector2(2f, 0f));
                l.rectTransform.anchorMin = new Vector2(0f, 0f);
                l.rectTransform.anchorMax = new Vector2(0f, 1f);
                l.rectTransform.sizeDelta = new Vector2(2f, 0f);
                Image r = UIFx.Image(root, "FranjaDer", th.blanco, new Color(1f, 1f, 1f, a));
                r.rectTransform.anchorMin = new Vector2(1f, 0f);
                r.rectTransform.anchorMax = new Vector2(1f, 1f);
                r.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                r.rectTransform.anchoredPosition = new Vector2(-x * 1.5f, 0f);
                r.rectTransform.sizeDelta = new Vector2(2f, 0f);
            }

            _router = _canvas.gameObject.AddComponent<UIRouter>();
            _pause = BuildPause(root);
            _tutorials = DEBuild.InfoScreen(root, "Pantalla_Tutoriales", "TUTORIALES", TutorialText());
            var slots = SlotListScreen.Create(root, "CARGAR PARTIDA", "¿Cargar la partida?",
                "Se perderá el progreso que no esté guardado.", "Sí, cargar");
            slots.onChosen = LoadFromSlot;
            _loadGame = slots;
            _toast = DEToast.Create(root);
            _restartModal = DEBuild.ConfirmModal(root, "Modal_Reiniciar", "¿Reiniciar desde el punto de control?",
                "Se perderá el progreso desde el último punto de control.", "No, volver", "Sí, reiniciar",
                () => LeaveTo(SceneManager.GetActiveScene().name));
            _quitModal = DEBuild.ConfirmModal(root, "Modal_SalirMenu", "¿Salir al menú principal?",
                "Se perderá el progreso que no esté guardado.", "No, volver", "Sí, salir al menú",
                () => LeaveTo(SceneNames.MainMenu));
            _options = OptionsScreen.Create(root, 0.96f);

            LensFX.Create(root, 905, false).grainOpacity = 0.1f;
        }

        private ActionScreen BuildPause(Transform root)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(root, "Pantalla_Pausa"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var screen = rt.gameObject.AddComponent<ActionScreen>();
            screen.onBack = Resume;

            var title = UIFx.Text(rt, "Titulo", "PAUSA", th.rajdhaniSemiBold, 66f, th.hueso, TextAlignmentOptions.Center, 5f);
            UIFx.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(800f, 80f));

            RectTransform column = UIFx.Rect(rt, "Opciones");
            UIFx.Place(column, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(480f, 470f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;

            Vector2 size = new Vector2(480f, 60f);
            var resume = GlassButton.Create(column, "Reanudar", size);
            resume.description = "Vuelve a la partida donde la dejaste.";
            resume.onClick.AddListener(Resume);

            var restart = GlassButton.Create(column, "Reiniciar desde punto de control", size);
            restart.description = "Vuelve a empezar desde el último punto de control.";
            restart.onClick.AddListener(() => _router.Push(_restartModal, true));

            var save = GlassButton.Create(column, "Guardar partida", size);
            save.description = "Guarda la partida para seguirla más tarde (no durante el asedio).";
            save.onClick.AddListener(SaveGame);

            var load = GlassButton.Create(column, "Cargar partida", size);
            load.description = "Elige una partida guardada.";
            load.onClick.AddListener(() => _router.Push(_loadGame));

            var tutorials = GlassButton.Create(column, "Tutoriales", size);
            tutorials.description = "Repasa los controles y las técnicas de supervivencia.";
            tutorials.onClick.AddListener(() => _router.Push(_tutorials));

            var options = GlassButton.Create(column, "Opciones", size);
            options.description = "Gráficos, audio, controles y accesibilidad.";
            options.onClick.AddListener(() => _router.Push(_options));

            var quit = GlassButton.Create(column, "Salir al menú principal", size);
            bool hasMenu = Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu);
            quit.description = hasMenu ? "Abandona la partida y vuelve al menú principal."
                : "Falta la escena MainMenu: créala con el menú Dead Echo > F1 · Crear escena MainMenu.";
            quit.interactable = hasMenu;
            quit.onClick.AddListener(() => _router.Push(_quitModal, true));

            DEBuild.LinkVertical(resume, restart, save, load, tutorials, options, quit);
            screen.firstSelected = resume;

            var desc = UIFx.Text(rt, "Descripcion", "", th.rajdhaniMedium, th.textoNormal + 2f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(1400f, 40f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            var hints = DEBuild.HintsRow(rt, true,
                new DEBuild.KeyHintDef("Enter", "A", "Confirmar"),
                new DEBuild.KeyHintDef("Esc", "B", "Cerrar"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(800f, 44f));
            return screen;
        }

        private static string TutorialText()
        {
            return
                "<b>Movimiento</b>   W A S D · Shift correr · Espacio saltar · Ctrl/C agacharse · Q giro rápido\n" +
                "<b>Apuntar y disparar</b>   Ratón · clic izquierdo · R recargar · 1-5 cambiar arma\n" +
                "<b>Cuerpo a cuerpo</b>   V cuchillo (parry y guardia) · F patada a enemigos aturdidos\n" +
                "<b>Maletín</b>   Tab / I · C fabricar · R rotar objeto\n" +
                "<b>Interactuar</b>   E recoger, abrir y hablar · J documentos\n" +
                "<b>Mercader</b>   U junto al farol azul: comprar, vender y mejorar\n" +
                "<b>Linterna</b>   L\n\n" +
                "<color=#9A968D>Dispara a las piernas o a la cabeza para aturdir a los aldeanos y remátalos con una patada. " +
                "Si te agarran, pulsa rápido para soltarte.</color>";
        }

        // ------------------------------------------------------------ Guardar / cargar

        private void SaveGame()
        {
            if (DeadEcho.Game.GameSaveSystem.SaveNow(false, out string msg)) _toast.Show(msg, 2.5f);
            else
            {
                UIAudio.Play(UISound.Error);
                _toast.Show(msg ?? "No se puede guardar ahora.", 2.5f);
            }
        }

        /// <summary>Carga una partida guardada desde la pausa (con la pantalla de carga).</summary>
        private void LoadFromSlot(int slot)
        {
            if (SceneLoader.IsLoading) return;
            // el tiempo sigue parado hasta que la escena nueva este lista (SceneLoader lo restaura)
            IsOpen = false;
            if (Fps != null) Fps.PopInputLock();
            if (!DeadEcho.Game.GameSaveSystem.LoadSlot(slot))
            {
                IsOpen = true;
                if (Fps != null) Fps.PushInputLock();
                UIAudio.Play(UISound.Error);
            }
        }
    }
}
