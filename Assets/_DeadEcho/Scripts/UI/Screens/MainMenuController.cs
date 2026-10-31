using System.Collections;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Menu principal (F1, 01_Main.html). Va en la escena MainMenu y construye toda la interfaz al arrancar:
    /// logo con eco, opciones con trazo de tinta, descripcion, "↵ Confirmar", usuario arriba a la derecha,
    /// Creditos y el modal de Salir. El fondo es provisional (niebla + lente) hasta las tomas cinematicas de F2.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private UIRouter _router;
        private UIScreen _menu;
        private CreditsScreen _credits;
        private UIScreen _exitModal;
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

            BuildBackground(root);

            _router = canvas.gameObject.AddComponent<UIRouter>();
            _menu = BuildMenu(root);
            _credits = CreditsScreen.Create(root);
            _exitModal = DEBuild.ConfirmModal(root, "Modal_Salir", "¿Salir de Dead Echo?", null, "No, volver", "Sí, salir del juego",
                () => StartCoroutine(Quit()));
            _router.initialScreen = _menu;

            LensFX.Create(root, 30, false);

            _black = UIFx.Image(root, "Negro", th.blanco, new Color(0f, 0f, 0f, 0f));
            UIFx.Stretch(_black.rectTransform);
            var top = _black.gameObject.AddComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = 40;
        }

        // ------------------------------------------------------------ Fondo provisional

        private static void BuildBackground(Transform root)
        {
            UITheme th = UITheme.Current;
            RawImage bg = UIFx.Stretch(UIFx.Rect(root, "Fondo")).gameObject.AddComponent<RawImage>();
            bg.texture = DEBuild.VerticalGradient(new Color(0.03f, 0.03f, 0.03f), new Color(0.16f, 0.16f, 0.15f));
            bg.raycastTarget = false;

            for (int i = 0; i < 3; i++)
            {
                RawImage fog = UIFx.Stretch(UIFx.Rect(root, "Niebla" + i)).gameObject.AddComponent<RawImage>();
                fog.texture = DEBuild.FogTexture(256, 128, 2.5f + i * 1.5f, 21 + i * 5, 1.3f);
                fog.uvRect = new Rect(0f, 0f, 1f + i * 0.4f, 1f);
                fog.raycastTarget = false;
                var d = fog.gameObject.AddComponent<FogDrift>();
                d.speed = new Vector2(0.004f + i * 0.005f, 0f) * (i % 2 == 0 ? 1f : -1f);
                d.baseAlpha = 0.16f + i * 0.05f;
                d.alphaPulse = 0.05f;
                d.pushScale = i == 0 ? 1.12f : 1f;
                d.pushTime = 30f;
            }
            FallingAsh.Create(root, 30, new Color(0.8f, 0.8f, 0.78f, 0.35f));

            // degradado negro a la izquierda para leer el menu
            Image left = UIFx.Image(root, "DegradadoIzq", th.degradadoIzq, new Color(0f, 0f, 0f, 0.9f));
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
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

            InkMenuItem cont = InkMenuItem.Create(column, "Continuar");
            cont.description = "Retoma la investigación desde tu último guardado.";
            cont.interactable = SaveService.HasAnyMemory;
            if (!cont.interactable) cont.description = "No hay ninguna memoria guardada.";
            cont.onClick.AddListener(() => SceneLoader.Load(SceneNames.Game));

            InkMenuItem nueva = InkMenuItem.Create(column, "Nueva partida");
            nueva.description = "Empieza una nueva investigación en el pueblo de Valdeoscuro.";
            nueva.onClick.AddListener(() => SceneLoader.Load(SceneNames.Game));

            InkMenuItem opciones = InkMenuItem.Create(column, "Opciones");
            opciones.description = "Gráficos, audio, controles y accesibilidad (disponible en la fase F5).";
            opciones.interactable = false;

            InkMenuItem creditos = InkMenuItem.Create(column, "Créditos");
            creditos.description = "Las personas detrás de Dead Echo.";
            creditos.onClick.AddListener(() => _router.Push(_credits));

            InkMenuItem salir = InkMenuItem.Create(column, "Salir");
            salir.description = "Cierra el juego.";
            salir.onClick.AddListener(() => _router.Push(_exitModal, true));

            DEBuild.LinkVertical(cont, nueva, opciones, creditos, salir);
            screen.firstSelected = cont.interactable ? cont : nueva;

            // Descripcion + Confirmar
            var desc = UIFx.Text(rt, "Descripcion", "", th.courierPrime, 24f, th.huesoClaro, TextAlignmentOptions.Left, 1f);
            UIFx.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(159f, 110f), new Vector2(1100f, 34f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            var hints = DEBuild.HintsRow(rt, false, new DEBuild.KeyHintDef("Enter", "A", "Confirmar", 20f));
            UIFx.Place(hints, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(159f, 50f), new Vector2(600f, 40f));
            return screen;
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
