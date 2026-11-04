using System;
using System.Collections.Generic;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Opciones (F5, 05_Opciones.html), desde el menu principal y desde la pausa.
    /// Pestañas con Q/E (LB/RB), filas con ↑↓, valores con ←→. Los cambios se ven al momento (vista previa)
    /// y se guardan con ↵ Confirmar; Esc con cambios sin confirmar pregunta "¿Descartar cambios?".
    /// ◆ = valor distinto del de fabrica. R restablece todo (con confirmacion), T la categoria.
    /// La resolucion y el modo de pantalla se aplican al confirmar, con "¿Mantener esta configuracion? 15 s".
    /// A la derecha, panel decorativo: figura en la niebla (la toma 1 del menu en vivo, si existe).
    /// </summary>
    public class OptionsScreen : UIScreen
    {
        public static readonly string[] Tabs = { "CONTROLES", "CÁMARA", "HUD", "GRÁFICOS", "AUDIO", "IDIOMA", "ACCESIBILIDAD" };
        private const float RowWidth = 930f;
        private const float KeepSeconds = 15f;

        private class Row
        {
            public Selectable sel;
            public Action refresh;
            public Action reset;
        }

        private int _tab = 3; // Graficos, como la referencia
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<TextMeshProUGUI> _tabLabels = new List<TextMeshProUGUI>();
        private readonly List<Image> _tabGlows = new List<Image>();
        private RectTransform _viewport, _content, _track, _thumb;
        private TextMeshProUGUI _chipPrev, _chipNext;
        private DEToast _toast;
        private UIScreen _resetModal, _discardModal, _keepModal, _keysScreen;
        private TextMeshProUGUI _keepText;

        private GameSettings _snapshot;
        private GameSettings _displayBefore;
        private bool _keepConfirmed;
        private float _keepUntil;
        private int _shownFrame = -1;
        private GameObject _lastSel;
        private InputMode _chipMode = (InputMode)(-1);

        private static List<Vector2Int> _resolutions;

        // ------------------------------------------------------------ Creacion

        /// <summary>Crea Opciones y sus modales en el Canvas (con su UIRouter). 'backgroundAlpha' &lt; 1 deja intuir el juego detras (pausa).</summary>
        public static OptionsScreen Create(Transform canvas, float backgroundAlpha = 1f)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, "Pantalla_Opciones"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var s = rt.gameObject.AddComponent<OptionsScreen>();

            Image bg = UIFx.Image(rt, "Fondo", th.blanco, UITheme.WithAlpha(th.negroFondo, backgroundAlpha), true);
            UIFx.Stretch(bg.rectTransform);

            var title = UIFx.Text(rt, "Titulo", "OPCIONES", th.rajdhaniSemiBold, 42f, th.huesoClaro, TextAlignmentOptions.Left, 2.5f);
            UIFx.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -30f), new Vector2(900f, 56f));

            s.BuildTabs(rt);
            s.BuildContentArea(rt);

            // panel decorativo (figura en la niebla)
            Texture live = MenuCinematics.Instance != null ? MenuCinematics.Instance.FogShotTexture : null;
            RectTransform panel = DEExtras.FogFigurePanel(rt, "PanelNiebla", live);
            UIFx.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1140f, -210f), new Vector2(684f, 705f));

            var desc = UIFx.Text(rt, "Descripcion", "", th.rajdhaniMedium, th.textoNormal, th.grisMedio, TextAlignmentOptions.Left);
            UIFx.Place(desc.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(96f, 105f), new Vector2(1000f, 34f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            var hints = DEBuild.HintsRow(rt, true,
                new DEBuild.KeyHintDef("R", "Y", "Restablecer todo"),
                new DEBuild.KeyHintDef("T", "X", "Restablecer categoría"),
                new DEBuild.KeyHintDef("Enter", "A", "Confirmar"),
                new DEBuild.KeyHintDef("Esc", "B", "Volver"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(1400f, 44f));
            DEExtras.Clickable(hints, 0, () => s.AskResetAll());
            DEExtras.Clickable(hints, 1, () => s.ResetCategory());
            DEExtras.Clickable(hints, 2, () => s.Confirm());
            DEExtras.Clickable(hints, 3, () => s.OnBack());

            s._toast = DEToast.Create(rt);

            // modales (otras pantallas del mismo router)
            s._resetModal = DEBuild.ConfirmModal(canvas, "Modal_RestablecerOpciones", "¿Restablecer todas las opciones?",
                "Se recuperarán los valores de fábrica de todas las categorías.", "No, volver", "Sí, restablecer", () => s.ResetAll());
            s._discardModal = DEBuild.ConfirmModal(canvas, "Modal_DescartarOpciones", "¿Descartar cambios?",
                "Hay cambios sin confirmar.", "No, volver", "Sí, descartar", () => s.Discard());
            s._keepModal = DEBuild.ConfirmModal(canvas, "Modal_MantenerPantalla", "¿Mantener esta configuración?",
                " ", "No, revertir", "Sí, mantener", () => s.KeepDisplay());
            Transform keepBody = s._keepModal.transform.Find("Caja/Texto");
            s._keepText = keepBody != null ? keepBody.GetComponent<TextMeshProUGUI>() : null;
            s._keepModal.OnHidden += s.OnKeepModalHidden;
            s._keysScreen = DEBuild.InfoScreen(canvas, "Pantalla_Teclas", "TECLAS DEL JUEGO", KeysText());

            s.OnHidden += () => { if (!s.IsVisible && MenuCinematics.Instance != null) MenuCinematics.Instance.keepFirstShotAlive = false; };
            Loc.OnChanged += s.OnLanguageChanged;
            return s;
        }

        private void OnDestroy() => Loc.OnChanged -= OnLanguageChanged;

        private void BuildTabs(RectTransform rt)
        {
            UITheme th = UITheme.Current;
            RectTransform bar = UIFx.Rect(rt, "Pestanas");
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(96f, -159f);
            bar.offsetMax = new Vector2(-96f, -93f);
            Image line = UIFx.Image(bar, "Linea", th.blanco, UITheme.Hex("#3C3C3A"));
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 1.5f);

            _chipPrev = Chip(bar, "Q", new Vector2(0f, 0.5f), new Vector2(18f, 0f));
            _chipNext = Chip(bar, "E", new Vector2(1f, 0.5f), new Vector2(-18f, 0f));

            RectTransform tabs = UIFx.Rect(bar, "Lista");
            UIFx.Stretch(tabs, 84f, 84f);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                RectTransform tab = UIFx.Rect(tabs, "Pestana_" + Tabs[i]);
                Image hit = tab.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var b = tab.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { if (Group.interactable) SetTab(idx); });
                Image div = UIFx.Image(tab, "Divisor", th.blanco, UITheme.Hex("#3C3C3A"));
                div.rectTransform.anchorMin = new Vector2(1f, 0.15f);
                div.rectTransform.anchorMax = new Vector2(1f, 0.85f);
                div.rectTransform.sizeDelta = new Vector2(1.5f, 0f);
                if (i == 0)
                {
                    Image div0 = UIFx.Image(tab, "DivisorIzq", th.blanco, UITheme.Hex("#3C3C3A"));
                    div0.rectTransform.anchorMin = new Vector2(0f, 0.15f);
                    div0.rectTransform.anchorMax = new Vector2(0f, 0.85f);
                    div0.rectTransform.sizeDelta = new Vector2(1.5f, 0f);
                }
                var label = UIFx.Text(tab, "Texto", Tabs[i], th.rajdhaniSemiBold, 24f, UITheme.Hex("#A9A9A5"), TextAlignmentOptions.Center, 1.5f);
                UIFx.Stretch(label.rectTransform);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
                Image glow = UIFx.Image(tab, "Brillo", th.blanco, Color.white);
                glow.rectTransform.anchorMin = new Vector2(0.18f, 0f);
                glow.rectTransform.anchorMax = new Vector2(0.82f, 0f);
                glow.rectTransform.pivot = new Vector2(0.5f, 0f);
                glow.rectTransform.sizeDelta = new Vector2(0f, 4.5f);
                _tabLabels.Add(label);
                _tabGlows.Add(glow);
            }
        }

        private static TextMeshProUGUI Chip(RectTransform bar, string key, Vector2 anchor, Vector2 pos)
        {
            UITheme th = UITheme.Current;
            Image chip = UIFx.Image(bar, "Tecla_" + key, th.blanco, UITheme.Hex("#D8D8D3"));
            UIFx.Place(chip.rectTransform, anchor, new Vector2(anchor.x, 0.5f), pos, new Vector2(48f, 30f));
            var t = UIFx.Text(chip.transform, "Texto", key, th.rajdhaniSemiBold, 18f, UITheme.Hex("#111111"), TextAlignmentOptions.Center);
            UIFx.Stretch(t.rectTransform);
            return t;
        }

        private void BuildContentArea(RectTransform rt)
        {
            UITheme th = UITheme.Current;
            _viewport = UIFx.Rect(rt, "Contenido");
            UIFx.Place(_viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -192f), new Vector2(RowWidth + 45f, 723f));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = UIFx.Rect(_viewport, "Filas");
            _content.anchorMin = _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(RowWidth, 100f);
            var v = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 0f;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // barra de desplazamiento (decorativa, refleja la posicion)
            _track = UIFx.Image(rt, "Scroll", th.blanco, th.grisLinea).rectTransform;
            UIFx.Place(_track, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1068f, -210f), new Vector2(3f, 705f));
            _thumb = UIFx.Image(_track, "Tirador", th.blanco, UITheme.Hex("#8C8C87")).rectTransform;
            _thumb.anchorMin = new Vector2(0f, 1f);
            _thumb.anchorMax = new Vector2(1f, 1f);
            _thumb.pivot = new Vector2(0.5f, 1f);
            _thumb.sizeDelta = new Vector2(0f, 300f);
        }

        // ------------------------------------------------------------ Mostrar / volver

        public override void Show(bool restoreSelection)
        {
            if (!restoreSelection)
            {
                _snapshot = SettingsService.Current.Clone();
                BuildTab(_tab, 0);
                if (MenuCinematics.Instance != null) MenuCinematics.Instance.keepFirstShotAlive = true;
            }
            // el Enter que abre Opciones o cierra un modal no debe confirmar en el mismo fotograma
            _shownFrame = Time.frameCount;
            base.Show(restoreSelection);
        }

        public override void OnBack()
        {
            if (Router == null) return;
            if (_snapshot != null && !SettingsService.Current.SameAs(_snapshot))
            {
                UIAudio.Play(UISound.Atras);
                Router.Push(_discardModal, true);
                return;
            }
            base.OnBack();
        }

        // ------------------------------------------------------------ Pestañas y filas

        private void SetTab(int tab)
        {
            tab = (tab % Tabs.Length + Tabs.Length) % Tabs.Length;
            if (tab == _tab) return;
            UIAudio.Play(UISound.Hover);
            BuildTab(tab, 0);
        }

        private void BuildTab(int tab, int selectIndex)
        {
            _tab = tab;
            for (int i = 0; i < _tabLabels.Count; i++)
            {
                _tabLabels[i].color = i == tab ? Color.white : UITheme.Hex("#A9A9A5");
                _tabGlows[i].enabled = i == tab;
            }

            // vaciar (sacar del layout ya, destruir al final del fotograma)
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Transform c = _content.GetChild(i);
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }
            _rows.Clear();

            GameSettings d = SettingsService.Defaults;
            switch (tab)
            {
                case 0: // CONTROLES
                    Section("Ratón y mando", true);
                    Slider("Sensibilidad", "Velocidad de la cámara con el ratón y el stick derecho.", 0, 100, 1,
                        () => S.sensitivity, v => S.sensitivity = v, d.sensitivity);
                    Toggle("Invertir eje Y", "Invierte el movimiento vertical de la cámara.", () => S.invertY, v => S.invertY = v, d.invertY);
                    Toggle("Vibración", "El mando vibra al recibir daño.", () => S.vibration, v => S.vibration = v, d.vibration);
                    Section("Teclas");
                    Button("Reasignar teclas", "Consulta las teclas del juego.", "Ver", () => Router.Push(_keysScreen));
                    break;
                case 1: // CAMARA
                    Section("Vista", true);
                    Slider("Campo de visión", "Amplitud de la vista en primera persona.", 55, 90, 1, () => S.fov, v => S.fov = v, d.fov);
                    Toggle("Balanceo al caminar", "Movimiento de la cámara al caminar y correr.", () => S.headBob, v => S.headBob = v, d.headBob);
                    break;
                case 2: // HUD
                    Section("Interfaz", true);
                    Toggle("Mostrar HUD", "Muestra la munición, la vida y las pesetas.", () => S.showHud, v => S.showHud = v, d.showHud);
                    Slider("Opacidad del HUD", "Transparencia del HUD durante el juego.", 20, 100, 5, () => S.hudOpacity, v => S.hudOpacity = v, d.hudOpacity);
                    break;
                case 3: // GRAFICOS
                    Section("Pantalla", true);
                    BuildResolutionRow();
                    Selector("Modo de pantalla", "Cambia entre pantalla completa y ventana.",
                        new[] { "Pantalla completa", "Ventana sin bordes", "Ventana" }, () => S.screenMode, v => S.screenMode = v, d.screenMode);
                    int[] fps = { 30, 60, 120, 144, 0 };
                    Selector("Límite de FPS", "Fotogramas por segundo máximos (sin efecto con V-Sync activado).",
                        new[] { "30", "60", "120", "144", "Sin límite" },
                        () => Mathf.Max(0, Array.IndexOf(fps, S.fpsLimit)), v => S.fpsLimit = fps[v], Array.IndexOf(fps, d.fpsLimit));
                    Section("Calidad");
                    var q = new List<string> { "Proyecto" };
                    q.AddRange(QualitySettings.names);
                    Selector("Preajuste de calidad", "Calidad general de texturas, sombras y efectos.", q.ToArray(),
                        () => Mathf.Clamp(S.qualityLevel + 1, 0, q.Count - 1), v => S.qualityLevel = v - 1, d.qualityLevel + 1);
                    Selector("Sombras", "Calidad de las sombras en tiempo real.", new[] { "Desactivado", "Baja", "Media", "Alta" },
                        () => S.shadows, v => S.shadows = v, d.shadows);
                    Toggle("V-Sync", "Sincroniza los fotogramas con el monitor para evitar cortes.", () => S.vSync, v => S.vSync = v, d.vSync);
                    Section("Imagen");
                    Slider("Brillo", "Brillo general de la imagen.", 0, 100, 1, () => S.brightness, v => S.brightness = v, d.brightness);
                    Slider("Grano de película", "Intensidad del grano cinematográfico.", 0, 100, 1, () => S.filmGrain, v => S.filmGrain = v, d.filmGrain);
                    break;
                case 4: // AUDIO
                    Section("Volumen", true);
                    Slider("General", "Volumen de todo el juego.", 0, 100, 1, () => S.masterVolume, v => S.masterVolume = v, d.masterVolume);
                    Slider("Música", "Volumen de la música.", 0, 100, 1, () => S.musicVolume, v => S.musicVolume = v, d.musicVolume);
                    Slider("Efectos", "Disparos, pasos, golpes y sonidos de la interfaz.", 0, 100, 1, () => S.sfxVolume, v => S.sfxVolume = v, d.sfxVolume);
                    Slider("Voces", "Diálogos, radio y gritos de los aldeanos.", 0, 100, 1, () => S.voiceVolume, v => S.voiceVolume = v, d.voiceVolume);
                    Section("Texto");
                    Toggle("Subtítulos", "Muestra los diálogos de la radio en pantalla.", () => S.subtitles, v => S.subtitles = v, d.subtitles);
                    break;
                case 5: // IDIOMA
                    Section("Idioma", true);
                    Selector("Idioma de la interfaz", "Idioma de los menús.", new[] { "Español", "English" },
                        () => S.language == Loc.English ? 1 : 0, v => S.language = v == 1 ? Loc.English : Loc.Spanish, 0, false);
                    break;
                default: // ACCESIBILIDAD
                    Section("Lectura", true);
                    Selector("Tamaño de texto", "Tamaño de los subtítulos y descripciones.", new[] { "Pequeño", "Normal", "Grande" },
                        () => S.textSize, v => S.textSize = v, d.textSize);
                    Section("Color");
                    Selector("Modo daltónico", "Ajusta los colores para distintos tipos de daltonismo.",
                        new[] { "No", "Protanopía", "Deuteranopía", "Tritanopía" }, () => S.colorBlindMode, v => S.colorBlindMode = v, d.colorBlindMode);
                    Section("Movimiento");
                    Toggle("Reducir movimiento", "Apaga grano, parpadeo, empujes de cámara y partículas.", () => S.reduceMotion, v => S.reduceMotion = v, d.reduceMotion);
                    Selector("Apuntar", "Mantener o pulsar para apuntar.", new[] { "Mantener", "Pulsar" },
                        () => S.holdToToggle ? 1 : 0, v => S.holdToToggle = v == 1, d.holdToToggle ? 1 : 0);
                    break;
            }

            var list = new List<Selectable>();
            foreach (var r in _rows) list.Add(r.sel);
            DEBuild.LinkVertical(list.ToArray());
            firstSelected = list.Count > 0 ? list[Mathf.Clamp(selectIndex, 0, list.Count - 1)] : null;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _content.anchoredPosition = Vector2.zero;
            UpdateThumb();
            if (IsVisible && firstSelected != null) Select(firstSelected.gameObject);
        }

        private static GameSettings S => SettingsService.Current;

        private void Section(string title, bool first = false)
        {
            if (!first) UIFx.Rect(_content, "Espacio").sizeDelta = new Vector2(10f, 21f);
            SectionHeader.Create(_content, title);
        }

        private void Selector(string label, string desc, string[] values, Func<int> get, Action<int> set, int def, bool translate = true)
        {
            var shown = new string[values.Length];
            for (int i = 0; i < values.Length; i++) shown[i] = translate ? Loc.T(values[i]) : values[i];
            OptionRowSelector row = OptionRowSelector.Create(_content, label, shown, get(), def, RowWidth);
            row.description = desc;
            row.onValueChanged.AddListener(v => { set(v); Changed(); });
            _rows.Add(new Row { sel = row, refresh = () => row.SetValueWithoutNotify(get()), reset = () => set(def) });
        }

        private void Toggle(string label, string desc, Func<bool> get, Action<bool> set, bool def)
        {
            Selector(label, desc, new[] { "Desactivado", "Activado" }, () => get() ? 1 : 0, v => set(v == 1), def ? 1 : 0);
        }

        private void Slider(string label, string desc, int min, int max, int step, Func<int> get, Action<int> set, int def)
        {
            OptionRowSlider row = OptionRowSlider.Create(_content, label, min, max, get(), def, step, RowWidth);
            row.description = desc;
            row.onValueChanged.AddListener(v => { set(Mathf.RoundToInt(v)); Changed(); });
            _rows.Add(new Row { sel = row, refresh = () => row.SetValueWithoutNotify(get()), reset = () => set(def) });
        }

        private void Button(string label, string desc, string value, Action onClick)
        {
            UITheme th = UITheme.Current;
            GlassButton b = GlassButton.Create(_content, label, new Vector2(RowWidth, th.alturaFila), TextAlignmentOptions.Left, th.filaSistema);
            Image div = UIFx.Image(b.transform, "Divisor", th.blanco, th.grisLinea);
            div.rectTransform.anchorMin = Vector2.zero;
            div.rectTransform.anchorMax = new Vector2(1f, 0f);
            div.rectTransform.pivot = new Vector2(0.5f, 0f);
            div.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            div.transform.SetAsFirstSibling();
            var v = UIFx.Text(b.transform, "Valor", value, th.rajdhaniMedium, th.filaSistema - 1f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(v.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(RowWidth * 0.674f, 0f), new Vector2(300f, 40f));
            var arrow = UIFx.Text(b.transform, "Flecha", "›", th.rajdhaniMedium, 34f, th.huesoClaro, TextAlignmentOptions.Center);
            UIFx.Place(arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(RowWidth * 0.674f + RowWidth * 0.26f, 0f), new Vector2(40f, 60f));
            b.description = desc;
            b.onClick.AddListener(() => onClick());
            _rows.Add(new Row { sel = b, refresh = null, reset = null });
        }

        private void BuildResolutionRow()
        {
            if (_resolutions == null)
            {
                _resolutions = new List<Vector2Int> { Vector2Int.zero };
                foreach (Resolution r in Screen.resolutions)
                {
                    var v = new Vector2Int(r.width, r.height);
                    if (!_resolutions.Contains(v)) _resolutions.Add(v);
                }
            }
            var labels = new string[_resolutions.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = i == 0 ? "Nativa" : $"{_resolutions[i].x} × {_resolutions[i].y}";
            Selector("Resolución", "Resolución de la pantalla. Al confirmar se pide mantenerla.", labels,
                () => Mathf.Max(0, _resolutions.IndexOf(new Vector2Int(S.resolutionWidth, S.resolutionHeight))),
                v => { S.resolutionWidth = _resolutions[v].x; S.resolutionHeight = _resolutions[v].y; }, 0);
        }

        /// <summary>Vista previa en vivo (todo menos resolucion y modo de pantalla, que esperan a Confirmar).</summary>
        private void Changed() => SettingsService.Apply(false);

        private void RefreshRows()
        {
            foreach (var r in _rows) r.refresh?.Invoke();
        }

        private void OnLanguageChanged()
        {
            if (_content == null) return;
            // reconstruir la pestaña para traducir los valores, manteniendo la fila seleccionada
            int index = 0;
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].sel != null && _rows[i].sel.gameObject == sel) index = i;
            BuildTab(_tab, index);
            if (_keysScreen != null)
            {
                Transform body = _keysScreen.transform.Find("Texto");
                if (body != null) body.GetComponent<TextMeshProUGUI>().text = KeysText();
            }
        }

        // ------------------------------------------------------------ Confirmar / restablecer

        private void Confirm()
        {
            if (!Group.interactable) return;
            GameSettings cur = SettingsService.Current;
            if (_snapshot != null && !cur.SameDisplay(_snapshot))
            {
                // pantalla nueva: aplicarla y pedir que se mantenga (15 s o se revierte)
                _displayBefore = _snapshot.Clone();
                _keepConfirmed = false;
                _keepUntil = Time.unscaledTime + KeepSeconds;
                SettingsService.ApplyDisplay(cur);
                UpdateKeepText();
                Router.Push(_keepModal, true);
                return;
            }
            Commit();
        }

        private void Commit()
        {
            SettingsService.Apply(false);
            SettingsService.Save();
            _snapshot = SettingsService.Current.Clone();
            _toast.Show("Opciones guardadas", 2f);
        }

        private void KeepDisplay()
        {
            _keepConfirmed = true;
            Router.Pop();
            Commit();
        }

        private void OnKeepModalHidden()
        {
            if (_keepConfirmed || _displayBefore == null) return;
            // no se confirmo (No, Esc o se acabo el tiempo): volver a la pantalla anterior
            GameSettings cur = SettingsService.Current;
            cur.resolutionWidth = _displayBefore.resolutionWidth;
            cur.resolutionHeight = _displayBefore.resolutionHeight;
            cur.screenMode = _displayBefore.screenMode;
            SettingsService.ApplyDisplay(cur);
            _displayBefore = null;
            RefreshRows();
        }

        private void UpdateKeepText()
        {
            if (_keepText == null) return;
            int left = Mathf.Max(0, Mathf.CeilToInt(_keepUntil - Time.unscaledTime));
            _keepText.text = Loc.IsEnglish ? $"The previous display settings will be restored in {left} s." : $"Se volverá a la configuración anterior en {left} s.";
        }

        private void AskResetAll()
        {
            if (!Group.interactable) return;
            Router.Push(_resetModal, true);
        }

        private void ResetAll()
        {
            SettingsService.ResetAll();
            SettingsService.Apply(false);
            Router.Pop();
            RefreshRows();
        }

        private void ResetCategory()
        {
            if (!Group.interactable) return;
            foreach (var r in _rows) r.reset?.Invoke();
            UIAudio.Play(UISound.Confirmar);
            Changed();
            RefreshRows();
        }

        private void Discard()
        {
            if (_snapshot != null)
            {
                SettingsService.Set(_snapshot);
                SettingsService.Apply(false);
            }
            Router.Pop();          // el modal
            UIAudio.Play(UISound.Atras);
            Router.Pop();          // Opciones
        }

        // ------------------------------------------------------------ Entrada y desplazamiento

        protected virtual void Update()
        {
            // cuenta atras del modal de pantalla (aunque Opciones no tenga el foco)
            if (_keepModal != null && Router != null && Router.Top == _keepModal)
            {
                UpdateKeepText();
                if (Time.unscaledTime >= _keepUntil) Router.Pop();
            }

            if (!IsVisible || !Group.interactable || Router == null || Router.Top != this) return;
            UpdateChips();
            ScrollWithSelection();
            if (Time.frameCount == _shownFrame) return; // el Enter que abrio Opciones no confirma nada

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) SetTab(_tab - 1);
            else if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) SetTab(_tab + 1);
            else if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) AskResetAll();
            else if ((kb != null && kb.tKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) ResetCategory();
            else if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
            {
                // las filas de valor confirman; un boton (Reasignar teclas) hace su propia accion
                GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                bool valueRow = sel == null || sel.GetComponent<OptionRowSelector>() != null || sel.GetComponent<OptionRowSlider>() != null;
                if (valueRow)
                {
                    UIAudio.Play(UISound.Confirmar);
                    Confirm();
                }
            }

            // rueda del raton
            Mouse m = Mouse.current;
            if (m != null)
            {
                float wheel = m.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.1f) SetScroll(_content.anchoredPosition.y - Mathf.Sign(wheel) * 70f);
            }
        }

        private void UpdateChips()
        {
            if (_chipMode == InputModeWatcher.Mode) return;
            _chipMode = InputModeWatcher.Mode;
            bool pad = _chipMode == InputMode.Mando;
            _chipPrev.text = pad ? "LB" : "Q";
            _chipNext.text = pad ? "RB" : "E";
        }

        private void ScrollWithSelection()
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == _lastSel) return;
            _lastSel = sel;
            if (sel == null || !sel.transform.IsChildOf(_content)) return;

            var item = (RectTransform)sel.transform;
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            float top = _viewport.InverseTransformPoint(corners[1]).y;
            float bottom = _viewport.InverseTransformPoint(corners[0]).y;
            Rect vp = _viewport.rect;
            const float margin = 70f; // deja ver el titulo de la seccion
            float y = _content.anchoredPosition.y;
            if (top > vp.yMax - margin) y -= top - (vp.yMax - margin);
            else if (bottom < vp.yMin + 20f) y += vp.yMin + 20f - bottom;
            SetScroll(y);
        }

        private void SetScroll(float y)
        {
            float max = Mathf.Max(0f, _content.rect.height - _viewport.rect.height);
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
            UpdateThumb();
        }

        private void UpdateThumb()
        {
            if (_thumb == null) return;
            float vh = _viewport.rect.height, ch = Mathf.Max(1f, _content.rect.height);
            float trackH = _track.rect.height;
            float ratio = Mathf.Clamp01(vh / ch);
            float thumbH = Mathf.Max(60f, trackH * ratio);
            float max = Mathf.Max(0f, ch - vh);
            float t = max > 0f ? _content.anchoredPosition.y / max : 0f;
            _thumb.sizeDelta = new Vector2(0f, thumbH);
            _thumb.anchoredPosition = new Vector2(0f, -(trackH - thumbH) * t);
        }

        // ------------------------------------------------------------ Teclas

        public static string KeysText()
        {
            if (Loc.IsEnglish)
                return
                    "<b>Movement</b>   W A S D · Shift sprint · Space jump · Ctrl/C crouch · Q quick turn\n" +
                    "<b>Aim and shoot</b>   Mouse · left click · R reload · 1-5 change weapon\n" +
                    "<b>Melee</b>   V knife (parry and guard) · F kick stunned enemies\n" +
                    "<b>Attaché case</b>   Tab / I · C craft · R rotate item\n" +
                    "<b>Interact</b>   E pick up, open and talk · J documents\n" +
                    "<b>Merchant</b>   U next to the blue lantern\n" +
                    "<b>Flashlight</b>   L     <b>Pause</b>   Esc / Start\n\n" +
                    "<color=#9A968D>Key remapping needs the game controls to move to Input Actions; for now the keys are fixed.</color>";
            return
                "<b>Movimiento</b>   W A S D · Shift correr · Espacio saltar · Ctrl/C agacharse · Q giro rápido\n" +
                "<b>Apuntar y disparar</b>   Ratón · clic izquierdo · R recargar · 1-5 cambiar arma\n" +
                "<b>Cuerpo a cuerpo</b>   V cuchillo (parry y guardia) · F patada a enemigos aturdidos\n" +
                "<b>Maletín</b>   Tab / I · C fabricar · R rotar objeto\n" +
                "<b>Interactuar</b>   E recoger, abrir y hablar · J documentos\n" +
                "<b>Mercader</b>   U junto al farol azul\n" +
                "<b>Linterna</b>   L     <b>Pausa</b>   Esc / Start\n\n" +
                "<color=#9A968D>Para reasignar teclas, los controles del juego tienen que pasar a Input Actions; de momento las teclas son fijas.</color>";
        }
    }
}
