using System.Collections;
using System.Collections.Generic;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Nueva partida (F4, 03_NuevaPartida.html): dificultad (Narrativa / Superviviente / Pesadilla), ranuras de
    /// memoria DEMO con "Guardar progreso" (memoria falsa + toast), panel del protagonista con linea de escaneo,
    /// equipo inicial y efectos de la dificultad, y "Comenzar →" (crea GameSession y carga el juego).
    /// </summary>
    public class NewGameScreen : UIScreen
    {
        private readonly List<GlassButton> _diffRows = new List<GlassButton>();
        private readonly List<Image> _diffMarks = new List<Image>();
        private readonly List<GlassButton> _slotRows = new List<GlassButton>();
        private readonly List<Image> _slotMarks = new List<Image>();
        private readonly List<TextMeshProUGUI> _slotMain = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _slotSub = new List<TextMeshProUGUI>();
        private TextMeshProUGUI _damageValue, _autosaveValue;
        private RectTransform _scan, _portrait;
        private DEToast _toast;
        private GlassButton _saveButton;
        private bool _saving;
        private bool _slotChosen;

        private int _difficulty = 1;
        private int _slot;

        /// <summary>Se llama al guardar una memoria (el menu habilita Continuar).</summary>
        public System.Action onMemoryChanged;

        public static NewGameScreen Create(Transform canvas)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, "Pantalla_NuevaPartida"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var s = rt.gameObject.AddComponent<NewGameScreen>();

            Image bg = UIFx.Image(rt, "Fondo", th.blanco, th.negroFondo, true);
            UIFx.Stretch(bg.rectTransform);

            var title = UIFx.Text(rt, "Titulo", "NUEVA PARTIDA", th.rajdhaniSemiBold, 42f, th.huesoClaro, TextAlignmentOptions.Left, 2.5f);
            UIFx.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -30f), new Vector2(900f, 56f));
            Image line = UIFx.Image(rt, "Linea", th.blanco, UITheme.Hex("#3C3C3A"));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.offsetMin = new Vector2(96f, -100.5f);
            line.rectTransform.offsetMax = new Vector2(-96f, -99f);

            s.BuildLeft(rt);
            s.BuildRight(rt);
            s._toast = DEToast.Create(rt);

            // pie: Esc Volver ... Comenzar →
            var hints = DEBuild.HintsRow(rt, false, new DEBuild.KeyHintDef("Esc", "B", "Volver", 26f));
            UIFx.Place(hints, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(96f, 66f), new Vector2(400f, 44f));
            DEExtras.Clickable(hints, 0, () => s.OnBack());

            var desc = UIFx.Text(rt, "Descripcion", "", th.rajdhaniMedium, th.textoNormal, th.grisMedio, TextAlignmentOptions.Center);
            UIFx.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 66f), new Vector2(900f, 36f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            GlassButton start = s.CreateStartButton(rt);
            UIFx.Place((RectTransform)start.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-96f, 66f), new Vector2(300f, 66f));
            start.description = "Empieza la partida con la dificultad elegida.";
            start.onClick.AddListener(s.Begin);

            var nav = new List<Selectable>();
            nav.AddRange(s._diffRows);
            nav.AddRange(s._slotRows);
            nav.Add(s._saveButton);
            nav.Add(start);
            DEBuild.LinkVertical(nav.ToArray());
            s.firstSelected = s._diffRows[1];
            s.OnShown += s.Refresh;
            return s;
        }

        // ------------------------------------------------------------ Columna izquierda

        private void BuildLeft(RectTransform rt)
        {
            UITheme th = UITheme.Current;
            RectTransform col = UIFx.Rect(rt, "Columna");
            UIFx.Place(col, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -117f), new Vector2(960f, 800f));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 0f;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            Spacer(col, 21f);
            SectionHeader.Create(col, "Dificultad");
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                GlassButton row = GlassButton.Create(col, GameSession.DifficultyNames[i], new Vector2(960f, 75f), TextAlignmentOptions.Left, 27f);
                Divider(row.transform);
                var small = UIFx.Text(row.transform, "Detalle", GameSession.DifficultyDescriptions[i], th.rajdhaniMedium, 22f, UITheme.Hex("#A5A5A0"), TextAlignmentOptions.Left);
                UIFx.Place(small.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(300f, 0f), new Vector2(620f, 40f));
                Image mark = UIFx.Image(row.transform, "Elegida", th.rombo, th.huesoClaro);
                UIFx.Place(mark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(14f, 14f));
                row.description = GameSession.DifficultyDescriptions[i];
                row.onClick.AddListener(() => SetDifficulty(idx));
                _diffRows.Add(row);
                _diffMarks.Add(mark);
            }

            Spacer(col, 21f);
            SectionHeader.Create(col, "Ranura de memoria");
            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                int idx = i;
                GlassButton row = GlassButton.Create(col, "", new Vector2(960f, 84f), TextAlignmentOptions.Left, 20f);
                Divider(row.transform);
                var label = UIFx.Text(row.transform, "Ranura", "RANURA " + (i + 1), th.rajdhaniSemiBold, 19f, UITheme.Hex("#9D9D99"), TextAlignmentOptions.Left, 3f);
                UIFx.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(27f, 0f), new Vector2(130f, 30f));
                var main = UIFx.Text(row.transform, "Memoria", "", th.rajdhaniSemiBold, 24f, th.huesoClaro, TextAlignmentOptions.Left);
                UIFx.Place(main.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 12f), new Vector2(700f, 32f));
                var sub = UIFx.Text(row.transform, "Detalle", "", th.rajdhaniMedium, 21f, UITheme.Hex("#A5A5A0"), TextAlignmentOptions.Left);
                UIFx.Place(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -15f), new Vector2(700f, 28f));
                Image mark = UIFx.Image(row.transform, "Elegida", th.rombo, th.huesoClaro);
                UIFx.Place(mark.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(14f, 14f));
                row.description = "Elige la ranura donde se guardará la partida.";
                row.onClick.AddListener(() => SetSlot(idx));
                _slotRows.Add(row);
                _slotMarks.Add(mark);
                _slotMain.Add(main);
                _slotSub.Add(sub);
            }

            Spacer(col, 24f);
            RectTransform saveRow = UIFx.Rect(col, "FilaGuardar");
            saveRow.sizeDelta = new Vector2(960f, 66f);
            _saveButton = GlassButton.Create(saveRow, "Guardar progreso", new Vector2(330f, 66f));
            UIFx.Place((RectTransform)_saveButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(330f, 66f));
            DEExtras.Outline((RectTransform)_saveButton.transform, new Color(1f, 1f, 1f, 0.45f));
            _saveButton.description = "Asigna una memoria falsa a la ranura seleccionada (DEMO).";
            _saveButton.onClick.AddListener(() => { if (!_saving) StartCoroutine(SaveFake()); });
            // ya no hace falta: la partida se guarda de verdad durante el juego
            _saveButton.interactable = false;
            _saveButton.gameObject.SetActive(false);
            var note = UIFx.Text(saveRow, "Nota", "La partida se guarda sola en la ranura elegida (si tenía otra, se sobrescribe).", th.rajdhaniMedium, 21f, UITheme.Hex("#8D8D89"), TextAlignmentOptions.Left);
            UIFx.Place(note.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(960f, 30f));
        }

        // ------------------------------------------------------------ Panel del protagonista

        private void BuildRight(RectTransform rt)
        {
            UITheme th = UITheme.Current;
            RectTransform aside = UIFx.Rect(rt, "Protagonista");
            UIFx.Place(aside, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1140f, -138f), new Vector2(684f, 780f));

            _portrait = UIFx.Rect(aside, "Retrato");
            UIFx.Place(_portrait, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(684f, 435f));
            _portrait.gameObject.AddComponent<RectMask2D>();
            RawImage bg = UIFx.Stretch(UIFx.Rect(_portrait, "Fondo")).gameObject.AddComponent<RawImage>();
            bg.texture = DEBuild.VerticalGradient(UITheme.Hex("#121211"), UITheme.Hex("#3A3A38"));
            bg.raycastTarget = false;
            RawImage fig = UIFx.Rect(_portrait, "Silueta").gameObject.AddComponent<RawImage>();
            fig.texture = DEExtras.Silhouette();
            fig.color = UITheme.Hex("#0E0E0D");
            fig.raycastTarget = false;
            UIFx.Place(fig.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 420f));
            Image vig = UIFx.Image(_portrait, "Vineta", th.vineta, new Color(0f, 0f, 0f, 0.7f));
            UIFx.Stretch(vig.rectTransform);
            DEExtras.Outline(_portrait, new Color(1f, 1f, 1f, 0.12f));

            Image scan = UIFx.Image(_portrait, "Escaneo", th.blanco, new Color(1f, 1f, 1f, 0.7f));
            _scan = scan.rectTransform;
            _scan.anchorMin = new Vector2(0f, 1f);
            _scan.anchorMax = new Vector2(1f, 1f);
            _scan.pivot = new Vector2(0.5f, 0.5f);
            _scan.sizeDelta = new Vector2(0f, 3f);
            Image scanGlow = UIFx.Image(_scan, "Brillo", th.blanco, new Color(1f, 1f, 1f, 0.12f));
            UIFx.Stretch(scanGlow.rectTransform, 0f, 0f, -10f, -10f);

            var name = UIFx.Text(_portrait, "Nombre", "[LEON]", th.specialElite, 33f, th.huesoClaro, TextAlignmentOptions.Left, 2f);
            UIFx.Place(name.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(21f, 12f), new Vector2(400f, 44f));
            var role = UIFx.Text(_portrait, "Rol", "AGENTE DEL GOBIERNO", th.rajdhaniSemiBold, 18f, UITheme.Hex("#9D9D99"), TextAlignmentOptions.Right, 5f);
            UIFx.Place(role.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-21f, 18f), new Vector2(400f, 30f));

            // equipo inicial (el mismo que da el maletin al empezar)
            float y = -456f;
            y = Caption(aside, "EQUIPO INICIAL", y);
            y = Item(aside, "Pistola 92F", "× 1", y);
            y = Item(aside, "Pistola Resident", "× 1", y);
            y = Item(aside, "Balas de pistola", "× 45", y);
            y = Item(aside, "Hierba verde", "× 1", y);
            y = Item(aside, "Pesetas", "5.000", y);
            y -= 12f;
            y = Caption(aside, "CON ESTA DIFICULTAD", y);
            _damageValue = Effect(aside, "Daño recibido", y, out y);
            _autosaveValue = Effect(aside, "Autoguardado", y, out y);
        }

        private static float Caption(Transform parent, string text, float y)
        {
            var t = UIFx.Text(parent, "Titulo", text, UITheme.Current.rajdhaniMedium, 19f, UITheme.Hex("#9D9D99"), TextAlignmentOptions.Left, 6f);
            UIFx.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(684f, 30f));
            return y - 33f;
        }

        private static float Item(Transform parent, string name, string count, float y)
        {
            UITheme th = UITheme.Current;
            var n = UIFx.Text(parent, "Objeto", name, th.rajdhaniMedium, 24f, th.huesoClaro, TextAlignmentOptions.Left);
            UIFx.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(450f, 40f));
            var c = UIFx.Text(parent, "Cantidad", count, th.rajdhaniMedium, 24f, th.huesoClaro, TextAlignmentOptions.Right);
            UIFx.Place(c.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(450f, y), new Vector2(234f, 40f));
            Image div = UIFx.Image(parent, "Divisor", th.blanco, th.grisLinea);
            UIFx.Place(div.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y - 41f), new Vector2(684f, 1.5f));
            return y - 44f;
        }

        private static TextMeshProUGUI Effect(Transform parent, string label, float y, out float nextY)
        {
            UITheme th = UITheme.Current;
            var n = UIFx.Text(parent, "Efecto", label, th.rajdhaniMedium, 24f, UITheme.Hex("#A5A5A0"), TextAlignmentOptions.Left);
            UIFx.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(450f, 36f));
            var v = UIFx.Text(parent, "Valor", "", th.rajdhaniMedium, 24f, th.huesoClaro, TextAlignmentOptions.Right);
            UIFx.Place(v.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(450f, y), new Vector2(234f, 36f));
            nextY = y - 38f;
            return v;
        }

        // ------------------------------------------------------------ Piezas

        private static void Spacer(Transform parent, float h) => UIFx.Rect(parent, "Espacio").sizeDelta = new Vector2(10f, h);

        private static void Divider(Transform row)
        {
            Image div = UIFx.Image(row, "Divisor", UITheme.Current.blanco, UITheme.Current.grisLinea);
            div.rectTransform.anchorMin = Vector2.zero;
            div.rectTransform.anchorMax = new Vector2(1f, 0f);
            div.rectTransform.pivot = new Vector2(0.5f, 0f);
            div.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            div.transform.SetAsFirstSibling();
        }

        // ------------------------------------------------------------ Logica

        private void Refresh()
        {
            SaveService.Load();
            if (_slot < 0 || _slot >= SaveService.SlotCount) _slot = 0;
            SetDifficulty(_difficulty, false);
            SetSlot(_slot, false);
            RefreshSlots();
        }

        private void SetDifficulty(int d, bool sound = true)
        {
            _difficulty = Mathf.Clamp(d, 0, 2);
            for (int i = 0; i < _diffMarks.Count; i++)
            {
                _diffMarks[i].enabled = i == _difficulty;
                _diffRows[i].textNormal = i == _difficulty ? Color.white : new Color(0.75f, 0.75f, 0.72f);
                _diffRows[i].label.color = _diffRows[i].IsSelectedNow ? _diffRows[i].textSelected : _diffRows[i].textNormal;
            }
            float dmg = GameSession.DamageTakenFor(_difficulty);
            _damageValue.text = "× " + dmg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            _autosaveValue.text = Loc.T(GameSession.AutoSaveFor(_difficulty) ? "Activado" : "Desactivado");
        }

        private void SetSlot(int s, bool sound = true)
        {
            if (sound) _slotChosen = true;
            _slot = Mathf.Clamp(s, 0, SaveService.SlotCount - 1);
            for (int i = 0; i < _slotMarks.Count; i++) _slotMarks[i].enabled = i == _slot;
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < _slotRows.Count; i++)
            {
                DemoSlot slot = SaveService.Slots[i];
                if (!slot.used)
                {
                    _slotMain[i].text = Loc.T("— Vacía —");
                    _slotMain[i].color = UITheme.Hex("#8D8D89");
                    _slotSub[i].text = "";
                    _slotMain[i].rectTransform.anchoredPosition = new Vector2(160f, 0f);
                    continue;
                }
                int h = slot.playSeconds / 3600, m = slot.playSeconds % 3600 / 60, sec = slot.playSeconds % 60;
                _slotMain[i].text = slot.location;
                _slotMain[i].color = UITheme.Current.huesoClaro;
                _slotMain[i].rectTransform.anchoredPosition = new Vector2(160f, 12f);
                string diff = Loc.T(GameSession.DifficultyNames[Mathf.Clamp(slot.difficulty, 0, 2)]);
                _slotSub[i].text = $"{diff} · {h:00}:{m:00}:{sec:00} · {slot.Date:dd/MM/yyyy HH:mm} · {(Loc.IsEnglish ? "Case" : "Caso")} {slot.casePercent}%";
            }
        }

        /// <summary>DEMO: "Sincronizando…" 1.5 s y despues memoria falsa en la ranura elegida (o la primera vacia).</summary>
        private IEnumerator SaveFake()
        {
            _saving = true;
            // la ranura elegida por el jugador; si no ha elegido ninguna, la primera vacia
            int target = _slotChosen ? _slot : FirstEmptyOr(_slot);
            _toast.Show("Sincronizando…", 1.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            SetSlot(target);
            _toast.Show((Loc.IsEnglish ? "The game will be saved in slot " : "La partida se guardará en la ranura ") + (target + 1), 3f);
            onMemoryChanged?.Invoke();
            _saving = false;
        }

        // ------------------------------------------------------------ Boton "Comenzar →"

        private GlassButton _start;
        private RawImage _startBack;
        private Image _startHalo;

        /// <summary>Boton principal claro (gradiente hueso, texto oscuro). Seleccionado: mas claro, con brillo y algo mas grande.</summary>
        private GlassButton CreateStartButton(Transform parent)
        {
            UITheme th = UITheme.Current;
            _start = GlassButton.Create(parent, "Comenzar →", new Vector2(300f, 66f), TextAlignmentOptions.Center, 26f);
            _startBack = UIFx.Stretch(UIFx.Rect(_start.transform, "Fondo")).gameObject.AddComponent<RawImage>();
            _startBack.texture = DEBuild.VerticalGradient(UITheme.Hex("#A9A9A4"), UITheme.Hex("#E9E9E4"));
            _startBack.raycastTarget = false;
            _startBack.transform.SetAsFirstSibling();
            _startHalo = UIFx.Image(_start.transform, "Halo", th.vineta, new Color(1f, 1f, 1f, 0f));
            UIFx.Stretch(_startHalo.rectTransform, -24f, -24f, -24f, -24f);
            _startHalo.transform.SetAsFirstSibling();
            _start.textNormal = UITheme.Hex("#111111");
            _start.textSelected = Color.black;
            _start.label.font = th.rajdhaniSemiBold;
            _start.label.characterSpacing = 4f;
            _start.label.color = _start.textNormal;
            if (_start.glass != null) _start.glass.enabled = false;      // el vidrio no se ve sobre fondo claro
            if (_start.lightLine != null) _start.lightLine.enabled = false;
            return _start;
        }

        private void UpdateStartButton()
        {
            if (_start == null) return;
            float target = _start.IsSelectedNow ? 1f : 0f;
            float k = Mathf.MoveTowards(_startHalo.color.a <= 0.12f ? 0f : (_startHalo.color.a - 0.12f) / 0.25f, target,
                SettingsService.ReduceMotion ? 1f : Time.unscaledDeltaTime / 0.15f);
            float v = Mathf.Lerp(0.82f, 1f, k);
            _startBack.color = new Color(v, v, v, 1f);
            _startHalo.color = new Color(1f, 1f, 1f, 0.12f + 0.25f * k);
            _start.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.04f, k);
            _start.label.color = Color.Lerp(_start.textNormal, _start.textSelected, k);
        }

        private static int FirstEmptyOr(int fallback)
        {
            for (int i = 0; i < SaveService.Slots.Count; i++) if (!SaveService.Slots[i].used) return i;
            return fallback;
        }

        private void Begin()
        {
            GameSession.Begin(_difficulty, _slotChosen ? _slot : FirstEmptyOr(_slot));
            SceneLoader.Load(SceneNames.Game);
        }

        protected virtual void Update()
        {
            if (_scan == null || !IsVisible) return;
            UpdateStartButton();
            // linea de escaneo que sube y baja (4 s), quieta con Reducir movimiento
            float k = SettingsService.ReduceMotion ? 0.5f : UIFx.Ease(Mathf.PingPong(Time.unscaledTime / 4f, 1f));
            _scan.anchoredPosition = new Vector2(0f, -k * _portrait.rect.height);
        }
    }
}
