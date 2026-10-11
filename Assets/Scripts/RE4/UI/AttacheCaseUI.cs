using System.Collections.Generic;
using DeadEcho.Core;
using DeadEcho.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gameplay.RE4
{
    /// <summary>
    /// Inventario (maletin) con el diseño de Dead Echo (F7, 07_Inventario.html). Pausa el juego.
    /// Objetivo arriba a la izquierda, pestañas Mapa / Objetos / Documentos (Q / E), pesetas arriba a la derecha,
    /// cuadricula con numeracion de filas, ficha del objeto y estadisticas con barras.
    /// Raton: arrastrar para mover, clic derecho usar. Teclado / mando: flechas o cruceta para moverse,
    /// Enter / A usar, R / X coger-rotar-soltar (soltar una hierba sobre otra las combina), F / Y mantener ordenar,
    /// 1-5 atajo de arma, Supr tirar, C fabricar, Tab / Esc / B cerrar.
    /// </summary>
    public class AttacheCaseUI : MonoBehaviour
    {
        public float cellSize = 70f;
        [Tooltip("Segundos que hay que mantener F para ordenar.")]
        public float sortHoldTime = 0.6f;

        public bool IsOpen { get; private set; }

        private enum Tab { Mapa, Objetos, Documentos }

        private AttacheCase _case;
        private WeaponSystem _weapons;
        private FirstPersonController _fps;

        private Canvas _canvas;
        private RectTransform _grid, _tilesRoot, _cursor, _ghost;
        private Image _ghostImage, _cursorImage;
        private TextMeshProUGUI _objective, _pesetas, _tabTitle, _infoTitle, _infoBody, _capacity, _message;
        private RectTransform _statsRoot, _itemsPanel, _mapPanel, _docsPanel;
        private Image[] _statFills;
        private TextMeshProUGUI[] _statLabels;
        private Image[] _tabIcons;
        private TextMeshProUGUI _docList, _docText;
        private Image _sortBar;
        private RectTransform _craftPanel;
        private TextMeshProUGUI _craftText;
        private readonly List<GameObject> _tiles = new List<GameObject>();

        private Tab _tab = Tab.Objetos;
        private Vector2Int _cell;
        private CaseEntry _dragging;
        private bool _dragRotated, _mouseDrag;
        private Vector2Int _dragOffset;
        private CaseEntry _shown;
        private bool _crafting;
        private string _craftMessage = "";
        private int _docIndex;
        private float _sortHeld;
        private float _messageUntil;
        private float _prevTimeScale = 1f;

        // repeticion de la navegacion al mantener una direccion
        private Vector2Int _heldDir;
        private float _nextRepeat;

        private void Awake()
        {
            _case = GetComponent<AttacheCase>();
            _weapons = GetComponent<WeaponSystem>();
            _fps = GetComponent<FirstPersonController>();
        }

        private void Start()
        {
            Build();
            _case.OnChanged += Refresh;
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_case != null) _case.OnChanged -= Refresh;
        }

        // ------------------------------------------------------------ Abrir / cerrar

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool toggle = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame))
                          || (pad != null && pad.selectButton.wasPressedThisFrame);
            bool back = IsOpen && ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame));

            if (IsOpen && back)
            {
                // Esc / B primero cancela lo que se este haciendo
                if (_dragging != null) { CancelDrag(); return; }
                if (_crafting) { ToggleCrafting(); return; }
                Close();
                return;
            }
            if (toggle)
            {
                if (IsOpen) Close();
                else if (CanOpen()) Open();
                return;
            }
            if (IsOpen) UpdateOpen();
        }

        private bool CanOpen()
        {
            if (PlayerHealth.Instance != null && (PlayerHealth.Instance.IsDead || PlayerHealth.Instance.IsGrabbed)) return false;
            if (PauseMenuController.IsOpen) return false;
            return _fps == null || !_fps.InputLocked;
        }

        public void Open()
        {
            IsOpen = true;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = false;
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Inventory);
            if (_fps != null)
            {
                _fps.PushInputLock();
                _fps.LockCursor(false);
            }
            _canvas.gameObject.SetActive(true);
            PlayUI(ProceduralAudio.CaseOpen());
            SetTab(Tab.Objetos);
            // empezar sobre el arma equipada (o la primera casilla)
            _cell = _weapons != null && _weapons.CurrentEntry != null ? _weapons.CurrentEntry.position : Vector2Int.zero;
            _shown = null;
            Refresh();
        }

        public void Close()
        {
            if (_dragging != null) CancelDrag();
            IsOpen = false;
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
            if (GameStateManager.Instance != null) GameStateManager.Instance.SetState(GameState.Playing);
            if (_fps != null)
            {
                _fps.PopInputLock();
                _fps.LockCursor(true);
                _fps.IgnoreEscapeFrame = Time.frameCount;
            }
            _canvas.gameObject.SetActive(false);
            _crafting = false;
            if (_craftPanel != null) _craftPanel.gameObject.SetActive(false);
            PlayUI(ProceduralAudio.CaseOpen());
        }

        private void PlayUI(AudioClip clip)
        {
            if (_weapons != null && _weapons.audioSource != null) _weapons.audioSource.PlayOneShot(clip, 0.6f);
        }

        // ------------------------------------------------------------ Construccion

        private void Build()
        {
            UITheme th = UITheme.Current;
            _canvas = DEBuild.Canvas("RE4_Inventario", 50);
            Transform root = _canvas.transform;

            Image bg = UIFx.Image(root, "Fondo", th.blanco, new Color(0.05f, 0.05f, 0.05f, 0.93f), true);
            UIFx.Stretch(bg.rectTransform);
            // columnas laterales mas oscuras, como en la referencia
            foreach (float x in new[] { 0.18f, 0.82f })
            {
                Image line = UIFx.Image(root, "Columna", th.blanco, UITheme.WithAlpha(th.hueso, 0.08f));
                line.rectTransform.anchorMin = new Vector2(x, 0f);
                line.rectTransform.anchorMax = new Vector2(x, 1f);
                line.rectTransform.sizeDelta = new Vector2(1.5f, 0f);
            }
            Image vignette = UIFx.Image(root, "Vineta", th.vineta, new Color(0f, 0f, 0f, 0.7f));
            UIFx.Stretch(vignette.rectTransform);

            // Objetivo (arriba a la izquierda)
            Image diamond = UIFx.Image(root, "Rombo", th.rombo, th.hueso);
            UIFx.Place(diamond.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(72f, -85f), new Vector2(16f, 16f));
            _objective = UIFx.Text(root, "Objetivo", "", th.rajdhaniMedium, 32f, th.hueso, TextAlignmentOptions.Left);
            _objective.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Place(_objective.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(98f, -85f), new Vector2(520f, 80f));
            Image objLine = UIFx.Image(root, "LineaObjetivo", th.blanco, UITheme.WithAlpha(th.hueso, 0.25f));
            UIFx.Place(objLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -132f), new Vector2(560f, 1.5f));

            // Pesetas (arriba a la derecha)
            Image moneyBox = UIFx.Image(root, "Pesetas", th.blanco, new Color(0f, 0f, 0f, 0.6f));
            UIFx.Place(moneyBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-60f, -86f), new Vector2(255f, 42f));
            Image moneyLine = UIFx.Image(moneyBox.rectTransform, "Borde", th.blanco, UITheme.WithAlpha(th.hueso, 0.2f));
            UIFx.Place(moneyLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(255f, 1.5f));
            var moneyLabel = UIFx.Text(moneyBox.rectTransform, "Etiqueta", "PESETAS", th.rajdhaniMedium, 18f, th.grisMedio, TextAlignmentOptions.Left, 6f);
            UIFx.Stretch(moneyLabel.rectTransform, 14f, 0f);
            _pesetas = UIFx.Text(moneyBox.rectTransform, "Valor", "0", th.specialElite, 26f, th.hueso, TextAlignmentOptions.Right);
            UIFx.Stretch(_pesetas.rectTransform, 0f, 14f);

            BuildTabs(root);

            _itemsPanel = UIFx.Stretch(UIFx.Rect(root, "Panel_Objetos"));
            BuildGrid(_itemsPanel);
            BuildInfo(_itemsPanel);

            _mapPanel = UIFx.Stretch(UIFx.Rect(root, "Panel_Mapa"));
            var map = UIFx.Text(_mapPanel, "Texto", "", th.rajdhaniMedium, 28f, th.huesoClaro, TextAlignmentOptions.Center);
            map.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 400f));
            map.text = "<size=44><b>VALDEOSCURO</b></size>\n\nEl mapa del pueblo llegará más adelante.\n\n<color=#9A968D>Plaza al centro · capilla al oeste · aserradero al este · portón del bosque al norte</color>";

            _docsPanel = UIFx.Stretch(UIFx.Rect(root, "Panel_Documentos"));
            _docList = UIFx.Text(_docsPanel, "Lista", "", th.rajdhaniMedium, 26f, th.huesoClaro, TextAlignmentOptions.TopLeft);
            _docList.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Place(_docList.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-560f, -190f), new Vector2(380f, 600f));
            _docText = UIFx.Text(_docsPanel, "Documento", "", th.specialElite, 24f, th.hueso, TextAlignmentOptions.TopLeft);
            _docText.textWrappingMode = TextWrappingModes.Normal;
            _docText.overflowMode = TextOverflowModes.Truncate;
            UIFx.Place(_docText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-120f, -190f), new Vector2(680f, 640f));

            _message = UIFx.Text(root, "Mensaje", "", th.rajdhaniSemiBold, 26f, th.verdeOk, TextAlignmentOptions.Center);
            UIFx.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1200f, 36f));

            var hints = DEBuild.HintsRow(root, true,
                new DEBuild.KeyHintDef("F", "Y", "Ordenar (mantener)"),
                new DEBuild.KeyHintDef("R", "X", "Mover / rotar"),
                new DEBuild.KeyHintDef("Enter", "A", "Usar / equipar"),
                new DEBuild.KeyHintDef("C", "RT", "Fabricar"),
                new DEBuild.KeyHintDef("Tab", "B", "Cerrar"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(1600f, 44f));
            // barra de progreso de "mantener F"
            _sortBar = UIFx.Image(root, "Ordenando", th.blanco, th.hueso);
            UIFx.Place(_sortBar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-600f, 20f), new Vector2(0f, 3f));

            BuildCrafting(root);
            LensFX.Create(root, 55, false).grainOpacity = 0.12f;
        }

        private void BuildTabs(Transform root)
        {
            UITheme th = UITheme.Current;
            _tabIcons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                Image box = UIFx.Image(root, "Pestana" + i, th.vidrio, Color.white);
                UIFx.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 110f, -78f), new Vector2(70f, 60f));
                _tabIcons[i] = box;
            }
            // iconos dibujados con formas (las fuentes no traen estos simbolos)
            Transform mapIcon = _tabIcons[0].transform;
            Image ring = UIFx.Image(mapIcon, "Brujula", th.aro, th.hueso);
            UIFx.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
            Image needle = UIFx.Image(mapIcon, "Aguja", th.rombo, th.hueso);
            UIFx.Place(needle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 22f));

            Transform caseIcon = _tabIcons[1].transform;
            Image body = UIFx.Image(caseIcon, "Maletin", th.blanco, th.hueso);
            UIFx.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -3f), new Vector2(36f, 22f));
            Image handle = UIFx.Image(caseIcon, "Asa", th.aro, th.hueso);
            UIFx.Place(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(14f, 12f));
            Image strap = UIFx.Image(caseIcon, "Cierre", th.blanco, new Color(0.1f, 0.1f, 0.1f));
            UIFx.Place(strap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(36f, 2f));

            Transform docIcon = _tabIcons[2].transform;
            for (int k = 0; k < 2; k++)
            {
                Image page = UIFx.Image(docIcon, "Hoja" + k, th.blanco, k == 0 ? th.grisMedio : th.hueso);
                UIFx.Place(page.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(k * 5f - 2f, k * -3f + 1f), new Vector2(20f, 26f));
                page.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 12f : -6f);
            }
            _tabTitle = UIFx.Text(root, "TituloPestana", "OBJETOS", th.specialElite, 26f, th.hueso, TextAlignmentOptions.Center, 4f);
            UIFx.Place(_tabTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(400f, 34f));
            var q = DEBuild.HintsRow(root, true, new DEBuild.KeyHintDef("Q", "LB", "", 22f));
            UIFx.Place(q, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-265f, -88f), new Vector2(60f, 40f));
            var e = DEBuild.HintsRow(root, true, new DEBuild.KeyHintDef("E", "RB", "", 22f));
            UIFx.Place(e, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(265f, -88f), new Vector2(60f, 40f));
        }

        private void BuildGrid(Transform parent)
        {
            UITheme th = UITheme.Current;
            float gw = _case.width * cellSize, gh = _case.height * cellSize;

            _grid = UIFx.Rect(parent, "Cuadricula");
            _grid.anchorMin = _grid.anchorMax = new Vector2(0.5f, 1f);
            _grid.pivot = new Vector2(0f, 1f);
            _grid.sizeDelta = new Vector2(gw, gh);
            _grid.anchoredPosition = new Vector2(-gw * 0.5f, -170f);
            Image gbg = _grid.gameObject.AddComponent<Image>();
            gbg.sprite = th.blanco;
            gbg.color = new Color(0.02f, 0.02f, 0.02f, 0.7f);
            gbg.raycastTarget = false;

            Color line = UITheme.WithAlpha(th.hueso, 0.14f);
            for (int x = 0; x <= _case.width; x++)
            {
                Image l = UIFx.Image(_grid, "V" + x, th.blanco, line);
                UIFx.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(x * cellSize, 0f), new Vector2(1f, gh));
            }
            for (int y = 0; y <= _case.height; y++)
            {
                Image l = UIFx.Image(_grid, "H" + y, th.blanco, line);
                UIFx.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -y * cellSize), new Vector2(gw, 1f));
                if (y < _case.height)
                {
                    // numeracion de filas
                    var n = UIFx.Text(_grid, "Fila" + y, (y + 1).ToString("00"), th.courierPrime, 15f, th.grisMedio, TextAlignmentOptions.Center);
                    UIFx.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, -(y + 0.5f) * cellSize), new Vector2(34f, 22f));
                }
            }

            // textos verticales decorativos
            var left = UIFx.Text(_grid, "DatosAgente", "DATOS DEL AGENTE", th.specialElite, 15f, th.grisMedio, TextAlignmentOptions.Center, 5f);
            UIFx.Place(left.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-58f, -gh * 0.5f), new Vector2(gh, 24f));
            left.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var right = UIFx.Text(_grid, "Contenido", "CONTENIDO", th.specialElite, 15f, th.grisMedio, TextAlignmentOptions.Center, 5f);
            UIFx.Place(right.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(26f, -gh * 0.5f), new Vector2(gh, 24f));
            right.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            _tilesRoot = UIFx.Rect(_grid, "Objetos");
            _tilesRoot.anchorMin = _tilesRoot.anchorMax = _tilesRoot.pivot = new Vector2(0f, 1f);

            _cursorImage = UIFx.Image(_grid, "Cursor", th.vidrio, Color.white);
            _cursor = _cursorImage.rectTransform;
            _cursor.anchorMin = _cursor.anchorMax = _cursor.pivot = new Vector2(0f, 1f);

            _ghostImage = UIFx.Image(_grid, "Fantasma", th.blanco, new Color(0.7f, 1f, 0.7f, 0.25f));
            _ghost = _ghostImage.rectTransform;
            _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(0f, 1f);
            _ghost.gameObject.SetActive(false);
        }

        private void BuildInfo(Transform parent)
        {
            UITheme th = UITheme.Current;
            float top = -170f - _case.height * cellSize - 22f;

            // linea discontinua
            for (int i = 0; i < 46; i++)
            {
                Image d = UIFx.Image(parent, "Guion", th.blanco, UITheme.WithAlpha(th.hueso, 0.25f));
                UIFx.Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-460f + i * 20f, top), new Vector2(10f, 1.5f));
            }

            Image card = UIFx.Image(parent, "Ficha", th.blanco, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            UIFx.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-8f, top - 18f), new Vector2(470f, 170f));
            _infoTitle = UIFx.Text(card.rectTransform, "Nombre", "", th.rajdhaniSemiBold, 30f, th.hueso, TextAlignmentOptions.TopLeft);
            UIFx.Place(_infoTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(430f, 38f));
            _infoBody = UIFx.Text(card.rectTransform, "Descripcion", "", th.rajdhaniMedium, 22f, th.huesoClaro, TextAlignmentOptions.TopLeft);
            _infoBody.textWrappingMode = TextWrappingModes.Normal;
            _infoBody.overflowMode = TextOverflowModes.Ellipsis;
            UIFx.Place(_infoBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -56f), new Vector2(430f, 106f));

            Image stats = UIFx.Image(parent, "Estadisticas", th.blanco, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            UIFx.Place(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(8f, top - 18f), new Vector2(470f, 170f));
            _statsRoot = stats.rectTransform;
            _statFills = new Image[5];
            _statLabels = new TextMeshProUGUI[5];
            for (int i = 0; i < 5; i++)
            {
                int col = i % 2, row = i / 2;
                Vector2 p = new Vector2(22f + col * 225f, -14f - row * 50f);
                _statLabels[i] = UIFx.Text(_statsRoot, "Etiqueta" + i, "", th.rajdhaniMedium, 21f, th.huesoClaro, TextAlignmentOptions.TopLeft);
                UIFx.Place(_statLabels[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), p, new Vector2(200f, 26f));
                Image barBg = UIFx.Image(_statsRoot, "Barra" + i, th.blanco, UITheme.WithAlpha(th.hueso, 0.18f));
                UIFx.Place(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), p + new Vector2(0f, -29f), new Vector2(195f, 4f));
                _statFills[i] = UIFx.Image(barBg.rectTransform, "Relleno", th.blanco, th.hueso);
                UIFx.Place(_statFills[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
            }
            _capacity = UIFx.Text(_statsRoot, "Capacidad", "", th.rajdhaniMedium, 21f, th.huesoClaro, TextAlignmentOptions.TopLeft);
            _capacity.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Place(_capacity.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(247f, -114f), new Vector2(210f, 52f));
        }

        private void BuildCrafting(Transform root)
        {
            UITheme th = UITheme.Current;
            _craftPanel = UIFx.Stretch(UIFx.Rect(root, "Fabricar"));
            Image dim = UIFx.Image(_craftPanel, "Oscurecer", th.blanco, new Color(0f, 0f, 0f, 0.85f), true);
            UIFx.Stretch(dim.rectTransform);
            var title = UIFx.Text(_craftPanel, "Titulo", "FABRICAR", th.rajdhaniSemiBold, 54f, th.hueso, TextAlignmentOptions.Center, 4f);
            UIFx.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 70f));
            _craftText = UIFx.Text(_craftPanel, "Recetas", "", th.rajdhaniMedium, 26f, th.huesoClaro, TextAlignmentOptions.TopLeft);
            _craftText.textWrappingMode = TextWrappingModes.Normal;
            _craftText.lineSpacing = 8f;
            UIFx.Place(_craftText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1100f, 700f));
            _craftPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ Vista

        private void SetTab(Tab t)
        {
            _tab = t;
            if (_dragging != null) CancelDrag();
            _itemsPanel.gameObject.SetActive(t == Tab.Objetos);
            _mapPanel.gameObject.SetActive(t == Tab.Mapa);
            _docsPanel.gameObject.SetActive(t == Tab.Documentos);
            _tabTitle.text = t == Tab.Mapa ? "MAPA" : t == Tab.Objetos ? "OBJETOS" : "DOCUMENTOS";
            for (int i = 0; i < _tabIcons.Length; i++)
                _tabIcons[i].color = i == (int)t ? Color.white : new Color(1f, 1f, 1f, 0f);
            if (t == Tab.Documentos) RefreshDocs();
        }

        private void Refresh()
        {
            if (_tilesRoot == null) return;
            foreach (var t in _tiles) Destroy(t);
            _tiles.Clear();

            foreach (var e in _case.entries)
            {
                if (e == _dragging) continue;
                _tiles.Add(CreateTile(e));
            }

            StoryDirector story = StoryDirector.Instance;
            _objective.text = StoryDirector.ObjectiveText(story != null ? story.Step : StoryStep.Arrive);
            _pesetas.text = _case.pesetas.ToString("N0");
            _shown = null; // forzar ficha
            UpdateCursor();
        }

        private GameObject CreateTile(CaseEntry e)
        {
            UITheme th = UITheme.Current;
            Vector2Int size = e.Size;
            RectTransform rt = UIFx.Rect(_tilesRoot, "Objeto_" + e.item.id);
            UIFx.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(e.position.x * cellSize + 2f, -e.position.y * cellSize - 2f),
                new Vector2(size.x * cellSize - 4f, size.y * cellSize - 4f));

            // fondo de vidrio oscuro con un toque del color del objeto
            Color tint = Color.Lerp(new Color(0.16f, 0.16f, 0.16f), e.item.tileColor, 0.35f);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = th.blanco;
            bg.color = new Color(tint.r, tint.g, tint.b, 0.92f);
            bg.raycastTarget = false;
            Image edge = UIFx.Image(rt, "Borde", th.vidrio, new Color(1f, 1f, 1f, 0.18f));
            UIFx.Stretch(edge.rectTransform);

            string label = string.IsNullOrEmpty(e.item.shortLabel) ? e.item.displayName : e.item.shortLabel;
            bool small = size.x * size.y <= 1;
            var name = UIFx.Text(rt, "Nombre", label, th.rajdhaniSemiBold, small ? 15f : 20f, th.hueso, TextAlignmentOptions.Center);
            name.textWrappingMode = TextWrappingModes.Normal;
            UIFx.Stretch(name.rectTransform, 4f, 4f, 4f, small ? 16f : 4f);

            if (e.item.weapon != null)
            {
                // municion cargada en verde, como en la referencia
                var mag = UIFx.Text(rt, "Cargador", e.loadedAmmo.ToString(), th.rajdhaniSemiBold, 22f, th.verdeOk, TextAlignmentOptions.BottomRight);
                UIFx.Stretch(mag.rectTransform, 0f, 7f, 0f, 3f);
                int slot = _case.QuickSlotOf(e);
                if (slot >= 0)
                {
                    var s = UIFx.Text(rt, "Atajo", (slot + 1).ToString(), th.courierPrimeBold, 18f, th.grisMedio, TextAlignmentOptions.TopLeft);
                    UIFx.Stretch(s.rectTransform, 7f, 0f, 3f, 0f);
                }
                if (_weapons != null && _weapons.CurrentEntry == e)
                {
                    // marca de arma equipada: esquina blanca
                    Image corner = UIFx.Image(rt, "Equipada", th.blanco, th.hueso);
                    UIFx.Place(corner.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-2f, -2f), new Vector2(14f, 14f));
                    corner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }
            else if (e.item.IsStackable || e.count > 1)
            {
                var c = UIFx.Text(rt, "Cantidad", e.count.ToString(), th.rajdhaniSemiBold, 22f, th.hueso, TextAlignmentOptions.BottomRight);
                UIFx.Stretch(c.rectTransform, 0f, 7f, 0f, 3f);
            }
            return rt.gameObject;
        }

        private void UpdateCursor()
        {
            if (_cursor == null) return;
            CaseEntry e = _dragging == null ? _case.EntryAt(_cell) : null;
            Vector2Int pos = e != null ? e.position : _cell;
            Vector2Int size = e != null ? e.Size : Vector2Int.one;
            _cursor.anchoredPosition = new Vector2(pos.x * cellSize, -pos.y * cellSize);
            _cursor.sizeDelta = new Vector2(size.x * cellSize, size.y * cellSize);
            _cursor.gameObject.SetActive(_dragging == null || !_mouseDrag);
            _cursor.SetAsLastSibling();
            if (_ghost.gameObject.activeSelf) _ghost.SetAsLastSibling();
            if (_dragging == null && e != _shown) ShowInfo(e);
        }

        private void ShowInfo(CaseEntry e)
        {
            _shown = e;
            for (int i = 0; i < 5; i++) { _statLabels[i].text = ""; _statFills[i].transform.parent.gameObject.SetActive(false); }
            if (e == null)
            {
                _infoTitle.text = "Maletín";
                _infoBody.text = "Selecciona un objeto para ver su ficha.";
                _capacity.text = "";
                _statLabels[0].text = $"Espacio libre: {FreeCells()} casillas";
                return;
            }
            _infoTitle.text = e.item.displayName;
            _infoBody.text = e.item.description;
            WeaponDefinition w = e.item.weapon;
            if (w != null)
            {
                float dmg = WeaponStats.Damage(w, e) * Mathf.Max(1, w.pellets);
                float rate = 1f / Mathf.Max(0.01f, WeaponStats.FireInterval(w, e));
                Stat(0, "Daño", Mathf.Clamp01(dmg / 160f));
                Stat(1, "Cadencia", Mathf.Clamp01(rate / 14f));
                Stat(2, "Estabilidad", Mathf.Clamp01(1f - w.recoilPitch / 9f));
                Stat(3, "Recarga", Mathf.Clamp01(1f - WeaponStats.ReloadTime(w, e) / 4f));
                Stat(4, "Precisión", Mathf.Clamp01(1f - w.aimSpread / 2f));
                int ammo = w.ammoItem != null ? _case.CountItem(w.ammoItem) : 0;
                _capacity.text = $"Capacidad\n<size=26><b>{e.loadedAmmo} / {e.MagazineSize}</b></size>  <color=#9A968D>+{ammo}</color>";
            }
            else
            {
                _capacity.text = "";
                if (e.item.IsStackable) _statLabels[0].text = $"Cantidad: {e.count} / {e.item.maxStack}";
                else if (e.item.healPercent > 0f) _statLabels[0].text = $"Recupera {e.item.healPercent * 100f:0}% de vida";
                else if (e.item.herb != HerbColor.None) _statLabels[0].text = "Combínala con una hierba verde (R)";
            }
        }

        private void Stat(int i, string label, float value)
        {
            _statLabels[i].text = label;
            _statFills[i].transform.parent.gameObject.SetActive(true);
            _statFills[i].rectTransform.sizeDelta = new Vector2(195f * value, 4f);
        }

        private int FreeCells()
        {
            int used = 0;
            foreach (var e in _case.entries) used += e.item.size.x * e.item.size.y;
            return _case.width * _case.height - used;
        }

        private void RefreshDocs()
        {
            StoryDirector story = StoryDirector.Instance;
            var docs = story != null ? story.Documents : null;
            if (docs == null || docs.Count == 0)
            {
                _docList.text = "<color=#9A968D>Aún no has encontrado documentos.</color>";
                _docText.text = "";
                return;
            }
            _docIndex = Mathf.Clamp(_docIndex, 0, docs.Count - 1);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < docs.Count; i++)
            {
                bool sel = i == _docIndex;
                sb.Append(sel ? "<color=#FFFFFF><b>› " : "<color=#9A968D>   ").Append(docs[i].displayName).Append(sel ? "</b>" : "").Append("</color>\n\n");
            }
            _docList.text = sb.ToString();
            ItemDefinition d = docs[_docIndex];
            _docText.text = $"<size=32>{d.displayName}</size>\n\n{(string.IsNullOrEmpty(d.documentText) ? d.description : d.documentText)}";
        }

        private void RefreshCrafting()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _case.recipes.Count && i < 9; i++)
            {
                CraftRecipe r = _case.recipes[i];
                if (r == null || r.result == null || r.ingredientA == null) continue;
                bool ok = r.CanCraft(_case);
                sb.Append(ok ? "<color=#ECE8DE>" : "<color=#5A5852>");
                sb.Append($"<b>{i + 1}</b>   {r.Describe()}</color>\n");
            }
            sb.Append($"\n<color=#9A968D>Tienes: pólvora {Count("polvora")} · recurso (S) {Count("recurso_s")} · recurso (L) {Count("recurso_l")}</color>\n");
            if (!string.IsNullOrEmpty(_craftMessage)) sb.Append($"\n<color=#B9E4B0>{_craftMessage}</color>\n");
            sb.Append("\n<color=#9A968D>1-9 fabricar · C / Esc volver al maletín</color>");
            _craftText.text = sb.ToString();
        }

        private int Count(string id)
        {
            int n = 0;
            foreach (var e in _case.entries) if (e.item != null && e.item.id == id) n += e.count;
            return n;
        }

        private void Flash(string text, bool error = false)
        {
            _message.text = text;
            _message.color = error ? new Color(1f, 0.6f, 0.5f) : UITheme.Current.verdeOk;
            _messageUntil = Time.unscaledTime + 2.5f;
            if (error) UIAudio.Play(UISound.Error);
        }

        // ------------------------------------------------------------ Interaccion

        private void UpdateOpen()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (_message.text.Length > 0 && Time.unscaledTime > _messageUntil) _message.text = "";

            bool craftKey = (kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
            if (craftKey && _dragging == null && _tab == Tab.Objetos) { ToggleCrafting(); return; }
            if (_crafting)
            {
                if (kb != null)
                {
                    for (int i = 0; i < _case.recipes.Count && i < 9; i++)
                    {
                        if (!kb[Key.Digit1 + i].wasPressedThisFrame) continue;
                        _craftMessage = _case.recipes[i].Craft(_case);
                        PlayUI(_craftMessage.Contains("Fabricado") ? ProceduralAudio.Rack() : ProceduralAudio.Click(250f, 60f));
                        RefreshCrafting();
                    }
                }
                return;
            }

            // Pestañas Q / E (LB / RB)
            bool prevTab = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool nextTab = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            if ((prevTab || nextTab) && _dragging == null)
            {
                int t = Mathf.Clamp((int)_tab + (nextTab ? 1 : -1), 0, 2);
                if (t != (int)_tab) { SetTab((Tab)t); UIAudio.Play(UISound.Hover); }
                return;
            }

            Vector2Int dir = NavInput(kb, pad);
            if (_tab == Tab.Documentos)
            {
                if (dir.y != 0 && StoryDirector.Instance != null && StoryDirector.Instance.Documents.Count > 0)
                {
                    _docIndex = Mathf.Clamp(_docIndex - dir.y, 0, StoryDirector.Instance.Documents.Count - 1);
                    RefreshDocs();
                    UIAudio.Play(UISound.Hover);
                }
                return;
            }
            if (_tab != Tab.Objetos) return;

            UpdateMouse();
            if (dir != Vector2Int.zero) MoveCursor(dir);

            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool move = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);

            if (_dragging != null)
            {
                if (move) { _dragRotated = !_dragRotated; _dragOffset = new Vector2Int(_dragOffset.y, _dragOffset.x); UIAudio.Play(UISound.Hover); }
                if (confirm && !_mouseDrag) Drop(_cell);
                UpdateGhost();
                return;
            }

            // Ordenar: mantener F / Y (tambien O de un toque)
            bool sortHeld = (kb != null && kb.fKey.isPressed) || (pad != null && pad.buttonNorth.isPressed);
            _sortHeld = sortHeld ? _sortHeld + Time.unscaledDeltaTime : 0f;
            _sortBar.rectTransform.sizeDelta = new Vector2(1200f * Mathf.Clamp01(_sortHeld / sortHoldTime), 3f);
            if (_sortHeld >= sortHoldTime || (kb != null && kb.oKey.wasPressedThisFrame))
            {
                _sortHeld = -10f; // no repetir hasta soltar
                _case.AutoSort();
                PlayUI(ProceduralAudio.Click(900f, 80f));
                Flash("Maletín ordenado");
            }

            CaseEntry hover = _case.EntryAt(_cell);
            if (hover == null) return;

            if (move) { StartDrag(hover, false, Vector2Int.zero); return; }
            if (confirm || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)) Use(hover);

            if (kb != null)
            {
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!kb[keys[i]].wasPressedThisFrame || hover.item.type != ItemType.Weapon) continue;
                    _case.AssignQuickSlot(i, hover);
                    PlayUI(ProceduralAudio.Click(1200f, 90f));
                    Flash($"{hover.item.displayName} asignada al atajo {i + 1}");
                }
                if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Discard(hover);
            }
        }

        private void ToggleCrafting()
        {
            _crafting = !_crafting;
            _craftMessage = "";
            _craftPanel.gameObject.SetActive(_crafting);
            if (_crafting) RefreshCrafting();
            PlayUI(ProceduralAudio.Click(800f, 80f));
        }

        /// <summary>Direccion de navegacion (flechas, cruceta o stick) con repeticion al mantener.</summary>
        private Vector2Int NavInput(Keyboard kb, Gamepad pad)
        {
            Vector2Int d = Vector2Int.zero;
            if (kb != null)
            {
                if (kb.upArrowKey.isPressed) d.y = 1;
                else if (kb.downArrowKey.isPressed) d.y = -1;
                if (kb.leftArrowKey.isPressed) d.x = -1;
                else if (kb.rightArrowKey.isPressed) d.x = 1;
            }
            if (pad != null && d == Vector2Int.zero)
            {
                Vector2 s = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                if (Mathf.Abs(s.y) > 0.6f) d.y = s.y > 0f ? 1 : -1;
                if (Mathf.Abs(s.x) > 0.6f) d.x = s.x > 0f ? 1 : -1;
            }
            if (d == Vector2Int.zero) { _heldDir = d; return d; }
            if (d != _heldDir)
            {
                _heldDir = d;
                _nextRepeat = Time.unscaledTime + 0.35f;
                return d;
            }
            if (Time.unscaledTime >= _nextRepeat)
            {
                _nextRepeat = Time.unscaledTime + 0.09f;
                return d;
            }
            return Vector2Int.zero;
        }

        /// <summary>Mueve la seleccion saltando por encima del objeto actual (los objetos ocupan varias casillas).</summary>
        private void MoveCursor(Vector2Int dir)
        {
            Vector2Int step = new Vector2Int(dir.x, -dir.y); // arriba en pantalla = fila anterior
            CaseEntry from = _dragging == null ? _case.EntryAt(_cell) : null;
            Vector2Int next = _cell + step;
            while (from != null && from.Rect.Contains(next)) next += step;
            next.x = Mathf.Clamp(next.x, 0, _case.width - 1);
            next.y = Mathf.Clamp(next.y, 0, _case.height - 1);
            if (next == _cell) return;
            _cell = next;
            UIAudio.Play(UISound.Hover);
            UpdateCursor();
        }

        private void UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            bool moved = mouse.delta.ReadValue().sqrMagnitude > 0.5f;
            bool inside = MouseCell(out Vector2Int cell);
            if (inside && (moved || mouse.leftButton.wasPressedThisFrame) && cell != _cell)
            {
                _cell = cell;
                UpdateCursor();
            }

            if (_dragging != null && _mouseDrag)
            {
                if (mouse.leftButton.wasReleasedThisFrame) { if (inside) Drop(cell); else CancelDrag(); }
                else if (mouse.rightButton.wasPressedThisFrame) CancelDrag();
                return;
            }
            if (_dragging == null && inside && mouse.leftButton.wasPressedThisFrame)
            {
                CaseEntry e = _case.EntryAt(cell);
                if (e != null) StartDrag(e, true, cell - e.position);
            }
        }

        private bool MouseCell(out Vector2Int cell)
        {
            cell = Vector2Int.zero;
            if (Mouse.current == null) return false;
            Vector2 screen = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, screen, null, out Vector2 local)) return false;
            // pivote arriba-izquierda: x positivo a la derecha, y negativo hacia abajo
            cell = new Vector2Int(Mathf.FloorToInt(local.x / cellSize), Mathf.FloorToInt(-local.y / cellSize));
            return cell.x >= 0 && cell.y >= 0 && cell.x < _case.width && cell.y < _case.height;
        }

        private void StartDrag(CaseEntry e, bool mouse, Vector2Int offset)
        {
            _dragging = e;
            _mouseDrag = mouse;
            _dragRotated = e.rotated;
            _dragOffset = offset;
            if (!mouse) _cell = e.position;
            _ghost.gameObject.SetActive(true);
            Refresh();
            UpdateGhost();
            PlayUI(ProceduralAudio.Click(700f, 90f));
        }

        private void UpdateGhost()
        {
            if (_dragging == null) return;
            Vector2Int size = _dragRotated ? new Vector2Int(_dragging.item.size.y, _dragging.item.size.x) : _dragging.item.size;
            Vector2Int pos = _cell - _dragOffset;
            _ghost.anchoredPosition = new Vector2(pos.x * cellSize, -pos.y * cellSize);
            _ghost.sizeDelta = new Vector2(size.x * cellSize, size.y * cellSize);

            CaseEntry target = _case.EntryAt(_cell);
            bool combine = target != null && target != _dragging && AttacheCase.CombineHerbs(_dragging.item.herb, target.item.herb) != HerbColor.None;
            bool ok = combine || _case.CanPlace(new RectInt(pos, size), _dragging);
            _ghostImage.color = combine ? new Color(0.6f, 0.85f, 1f, 0.35f) : ok ? new Color(0.72f, 0.9f, 0.69f, 0.3f) : new Color(1f, 0.35f, 0.3f, 0.3f);
            _ghost.SetAsLastSibling();
        }

        private void Drop(Vector2Int cell)
        {
            CaseEntry dragged = _dragging;
            _dragging = null;
            _mouseDrag = false;
            _ghost.gameObject.SetActive(false);

            CaseEntry target = _case.EntryAt(cell);
            if (target != null && target != dragged && _case.TryCombine(dragged, target))
            {
                PlayUI(ProceduralAudio.Pickup());
                Flash("Hierbas combinadas");
                return;
            }
            if (_case.TryMove(dragged, cell - _dragOffset, _dragRotated))
            {
                PlayUI(ProceduralAudio.Click(500f, 90f));
                return;
            }
            Flash("No cabe ahí", true);
            Refresh();
        }

        private void CancelDrag()
        {
            _dragging = null;
            _mouseDrag = false;
            _ghost.gameObject.SetActive(false);
            Refresh();
        }

        private void Use(CaseEntry e)
        {
            switch (e.item.type)
            {
                case ItemType.Weapon:
                    if (_weapons != null) _weapons.Equip(e);
                    PlayUI(ProceduralAudio.Rack());
                    Flash(e.item.displayName + " equipada");
                    Refresh();
                    break;
                case ItemType.Herb:
                case ItemType.Heal:
                    if (e.item.healPercent <= 0f)
                    {
                        Flash("Esta hierba sola no cura: combínala con una verde (R)", true);
                        return;
                    }
                    if (PlayerHealth.Instance != null && PlayerHealth.Instance.HealPercent(e.item.healPercent))
                    {
                        if (e.count > 1) { e.count--; _case.NotifyChanged(); }
                        else _case.Remove(e);
                        Flash("Vida recuperada");
                    }
                    else Flash("Ya tienes la vida al máximo", true);
                    break;
                default:
                    UIAudio.Play(UISound.Error);
                    break;
            }
        }

        private void Discard(CaseEntry e)
        {
            if (_weapons != null && _weapons.CurrentEntry == e && _case.Weapons().Count <= 1)
            {
                Flash("No puedes tirar tu única arma", true);
                return;
            }
            Vector3 pos = transform.position + transform.forward * 1.2f + Vector3.up * 0.5f;
            ItemPickup dropped = ItemPickup.Spawn(e.item, e.count, pos, e.item.weapon != null ? e.loadedAmmo : -1);
            if (dropped != null && e.item.weapon != null && e.upgrades != null) dropped.upgrades = e.upgrades.Clone();
            _case.Remove(e);
            Flash(e.item.displayName + " tirado");
            PlayUI(ProceduralAudio.Click(400f, 60f));
        }
    }
}
