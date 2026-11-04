using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Menu principal (F1, 01_Main.html). Va en la escena MainMenu y construye toda la interfaz al arrancar:
    /// tomas cinematicas de fondo (F2), logo con eco, opciones con trazo de tinta, descripcion, "↵ Confirmar",
    /// usuario arriba a la derecha, Nueva partida (F4), Opciones (F5), Creditos, el modal de Salir y el modo cine.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private UIRouter _router;
        private UIScreen _menu;
        private CreditsScreen _credits;
        private NewGameScreen _newGame;
        private OptionsScreen _options;
        private UIScreen _exitModal;
        private InkMenuItem _continue, _newItem, _optionsItem, _creditsItem, _quitItem;
        private Image _black;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            DEBuild.EnsureEventSystem();
            Build();
        }

        private void Build()
        {
            UITheme th = UITheme.Current;
            Canvas canvas = DEBuild.Canvas("Canvas_MenuPrincipal", 10, transform);
            Transform root = canvas.transform;

            CanvasGroup leftGradient = BuildBackground(root);

            _router = canvas.gameObject.AddComponent<UIRouter>();
            _menu = BuildMenu(root);
            _credits = CreditsScreen.Create(root);
            _newGame = NewGameScreen.Create(root);
            _newGame.onMemoryChanged = RefreshContinue;
            _options = OptionsScreen.Create(root);
            _exitModal = DEBuild.ConfirmModal(root, "Modal_Salir", "¿Salir de Dead Echo?", null, "No, volver", "Sí, salir del juego",
                () => StartCoroutine(Quit()));
            _router.initialScreen = _menu;

            LensFX lens = LensFX.Create(root, 30, false);

            // modo cine: tras 20 s sin input en la raiz del menu
            IdleCinemaMode cinema = IdleCinemaMode.Create(root, _router, _menu, leftGradient);
            var cinemaCanvas = cinema.gameObject.AddComponent<Canvas>();
            cinemaCanvas.overrideSorting = true;
            cinemaCanvas.sortingOrder = 35; // encima del grano para que las franjas sean negro puro

            _black = UIFx.Image(root, "Negro", th.blanco, new Color(0f, 0f, 0f, 0f));
            UIFx.Stretch(_black.rectTransform);
            var top = _black.gameObject.AddComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = 40;
        }

        // ------------------------------------------------------------ Fondo: tomas cinematicas

        /// <summary>Fondo de reserva + las 3 tomas (F2) + degradado izquierdo para leer el menu (se devuelve para el modo cine).</summary>
        private static CanvasGroup BuildBackground(Transform root)
        {
            UITheme th = UITheme.Current;
            RawImage bg = UIFx.Stretch(UIFx.Rect(root, "Fondo")).gameObject.AddComponent<RawImage>();
            bg.texture = DEBuild.VerticalGradient(new Color(0.03f, 0.03f, 0.03f), new Color(0.16f, 0.16f, 0.15f));
            bg.raycastTarget = false;

            MenuCinematics.Create(root);

            // degradado negro a la izquierda para leer el menu
            Image left = UIFx.Image(root, "DegradadoIzq", th.degradadoIzq, new Color(0f, 0f, 0f, 0.9f));
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
            return left.gameObject.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------ Menu

        private UIScreen BuildMenu(Transform root)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(root, "Pantalla_Menu"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var screen = rt.gameObject.AddComponent<UIScreen>();
            screen.canGoBack = false;

            // linea rayada con punto
            float[] dashes = { 90f, 135f, 60f, 300f };
            float x = 138f;
            foreach (float d in dashes)
            {
                Image dash = UIFx.Image(rt, "Raya", th.blanco, UITheme.WithAlpha(th.hueso, 0.7f));
                UIFx.Place(dash.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(x, -177f), new Vector2(d, 2f));
                x += d + 5f;
            }
            Image dot = UIFx.Image(rt, "Punto", th.rombo, th.hueso);
            UIFx.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + 10f, -177f), new Vector2(9f, 9f));

            RectTransform logo = DEBuild.Logo(rt);
            UIFx.Place(logo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -215f), logo.sizeDelta);

            // Usuario arriba a la derecha
            Image userLine = UIFx.Image(rt, "LineaUsuario", th.blanco, UITheme.WithAlpha(th.hueso, 0.5f));
            UIFx.Place(userLine.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-72f, -54f), new Vector2(360f, 1.5f));
            string user = string.IsNullOrEmpty(System.Environment.UserName) ? "USUARIO" : System.Environment.UserName.ToUpperInvariant();
            var userText = UIFx.Text(rt, "Usuario", "[" + user + "]", th.courierPrime, 22f, th.huesoClaro, TextAlignmentOptions.Right, 2f);
            UIFx.Place(userText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-72f, -64f), new Vector2(500f, 34f));

            // Opciones
            RectTransform column = UIFx.Rect(rt, "Opciones");
            UIFx.Place(column, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(138f, -455f), new Vector2(560f, 300f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;

            _continue = InkMenuItem.Create(column, "Continuar");
            _continue.onClick.AddListener(ContinueGame);

            _newItem = InkMenuItem.Create(column, "Nueva partida");
            _newItem.description = "Empieza una nueva investigación en el pueblo de Valdeoscuro.";
            _newItem.onClick.AddListener(() => _router.Push(_newGame));

            _optionsItem = InkMenuItem.Create(column, "Opciones");
            _optionsItem.description = "Gráficos, audio, controles y accesibilidad.";
            _optionsItem.onClick.AddListener(() => _router.Push(_options));

            _creditsItem = InkMenuItem.Create(column, "Créditos");
            _creditsItem.description = "Las personas detrás de Dead Echo.";
            _creditsItem.onClick.AddListener(() => _router.Push(_credits));

            _quitItem = InkMenuItem.Create(column, "Salir");
            _quitItem.description = "Cierra el juego.";
            _quitItem.onClick.AddListener(() => _router.Push(_exitModal, true));

            RefreshContinue();
            screen.firstSelected = _continue.interactable ? (Selectable)_continue : _newItem;
            // al volver de Nueva partida puede haber una memoria nueva: Continuar se habilita
            screen.OnShown += RefreshContinue;

            // Descripcion + Confirmar
            var desc = UIFx.Text(rt, "Descripcion", "", th.courierPrime, 24f, th.huesoClaro, TextAlignmentOptions.Left, 1f);
            UIFx.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(159f, 110f), new Vector2(1100f, 34f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            var hints = DEBuild.HintsRow(rt, false, new DEBuild.KeyHintDef("Enter", "A", "Confirmar", 20f));
            UIFx.Place(hints, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(159f, 50f), new Vector2(600f, 40f));
            return screen;
        }

        /// <summary>Continuar: deshabilitado (y se salta) sin memorias; si hay, carga la mas reciente.</summary>
        private void RefreshContinue()
        {
            if (_continue == null) return;
            SaveService.Load();
            _continue.interactable = SaveService.HasAnyMemory;
            _continue.description = _continue.interactable ? "Retoma la investigación desde tu último guardado." : "No hay ninguna memoria guardada.";
            DEBuild.LinkVertical(_continue, _newItem, _optionsItem, _creditsItem, _quitItem);
            if (_menu != null) _menu.firstSelected = _continue.interactable ? (Selectable)_continue : _newItem;
        }

        private void ContinueGame()
        {
            int i = SaveService.MostRecentIndex;
            if (i < 0) return;
            DeadEcho.Game.GameSaveSystem.LoadSlot(i); // restaura la partida guardada
        }

        private IEnumerator Quit()
        {
            _black.raycastTarget = true;
            yield return UIFx.Tween(0.5f, k => _black.color = new Color(0f, 0f, 0f, k));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
