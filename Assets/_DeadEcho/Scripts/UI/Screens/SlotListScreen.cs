using System;
using System.Collections.Generic;
using DeadEcho.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Lista de las 3 ranuras de partida (Pausa › Cargar partida). Cada fila muestra lugar, dificultad, tiempo jugado,
    /// fecha y % del caso; al elegir una ranura con partida se pide confirmacion y se llama a onChosen(ranura).
    /// </summary>
    public class SlotListScreen : UIScreen
    {
        public Action<int> onChosen;

        private readonly List<GlassButton> _rows = new List<GlassButton>();
        private readonly List<TextMeshProUGUI> _main = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _sub = new List<TextMeshProUGUI>();
        private UIScreen _confirm;
        private TextMeshProUGUI _confirmTitle;
        private int _pending = -1;

        public static SlotListScreen Create(Transform canvas, string title, string confirmTitle, string confirmBody, string yesText)
        {
            UITheme th = UITheme.Current;
            RectTransform rt = UIFx.Stretch(UIFx.Rect(canvas, "Pantalla_Ranuras"));
            rt.gameObject.AddComponent<CanvasGroup>();
            var s = rt.gameObject.AddComponent<SlotListScreen>();

            Image dim = UIFx.Image(rt, "Oscurecer", th.blanco, new Color(0f, 0f, 0f, 0.84f), true);
            UIFx.Stretch(dim.rectTransform);

            var t = UIFx.Text(rt, "Titulo", title, th.rajdhaniSemiBold, th.tituloPantalla, th.hueso, TextAlignmentOptions.Center, 3f);
            UIFx.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 80f));

            RectTransform col = UIFx.Rect(rt, "Ranuras");
            UIFx.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1000f, 300f));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                int idx = i;
                GlassButton row = GlassButton.Create(col, "", new Vector2(1000f, 90f), TextAlignmentOptions.Left, 20f);
                Image div = UIFx.Image(row.transform, "Divisor", th.blanco, th.grisLinea);
                div.rectTransform.anchorMin = Vector2.zero;
                div.rectTransform.anchorMax = new Vector2(1f, 0f);
                div.rectTransform.pivot = new Vector2(0.5f, 0f);
                div.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
                var label = UIFx.Text(row.transform, "Ranura", (Loc.IsEnglish ? "SLOT " : "RANURA ") + (i + 1), th.rajdhaniSemiBold, 19f, UITheme.Hex("#9D9D99"), TextAlignmentOptions.Left, 3f);
                UIFx.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(27f, 0f), new Vector2(140f, 30f));
                var main = UIFx.Text(row.transform, "Partida", "", th.rajdhaniSemiBold, 26f, th.huesoClaro, TextAlignmentOptions.Left);
                UIFx.Place(main.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 13f), new Vector2(800f, 34f));
                var sub = UIFx.Text(row.transform, "Detalle", "", th.rajdhaniMedium, 21f, UITheme.Hex("#A5A5A0"), TextAlignmentOptions.Left);
                UIFx.Place(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, -16f), new Vector2(800f, 28f));
                row.onClick.AddListener(() => s.Choose(idx));
                s._rows.Add(row);
                s._main.Add(main);
                s._sub.Add(sub);
            }
            DEBuild.LinkVertical(s._rows.ToArray());
            s.firstSelected = s._rows[0];

            var desc = UIFx.Text(rt, "Descripcion", "", th.rajdhaniMedium, th.textoNormal + 2f, th.grisMedio, TextAlignmentOptions.Center);
            UIFx.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(1400f, 40f));
            desc.gameObject.AddComponent<DescriptionLabel>();

            var hints = DEBuild.HintsRow(rt, true, new DEBuild.KeyHintDef("Enter", "A", "Seleccionar"), new DEBuild.KeyHintDef("Esc", "B", "Volver"));
            UIFx.Place(hints, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(800f, 44f));
            DEExtras.Clickable(hints, 1, () => s.OnBack());

            s._confirm = DEBuild.ConfirmModal(canvas, "Modal_CargarRanura", confirmTitle, confirmBody, "No, volver", yesText, () => s.ConfirmChoice());
            Transform ct = s._confirm.transform.Find("Caja/Titulo");
            s._confirmTitle = ct != null ? ct.GetComponent<TextMeshProUGUI>() : null;
            return s;
        }

        public override void Show(bool restoreSelection)
        {
            Refresh();
            base.Show(restoreSelection);
        }

        public void Refresh()
        {
            SaveService.Load();
            for (int i = 0; i < _rows.Count; i++)
            {
                DemoSlot slot = SaveService.Slots[i];
                bool has = slot.HasGame;
                _rows[i].interactable = true; // las vacias se pueden enfocar, pero no cargan
                if (!has)
                {
                    _main[i].text = Loc.T("— Vacía —");
                    _main[i].color = UITheme.Hex("#8D8D89");
                    _sub[i].text = "";
                    _rows[i].description = Loc.IsEnglish ? "This slot is empty." : "Esta ranura está vacía.";
                    continue;
                }
                int h = slot.playSeconds / 3600, m = slot.playSeconds % 3600 / 60, sec = slot.playSeconds % 60;
                _main[i].text = slot.location;
                _main[i].color = UITheme.Current.huesoClaro;
                string diff = Loc.T(GameSession.DifficultyNames[Mathf.Clamp(slot.difficulty, 0, 2)]);
                _sub[i].text = $"{diff} · {h:00}:{m:00}:{sec:00} · {slot.Date:dd/MM/yyyy HH:mm} · {(Loc.IsEnglish ? "Case" : "Caso")} {slot.casePercent}%";
                _rows[i].description = Loc.IsEnglish ? "Load this game." : "Carga esta partida.";
            }
        }

        private void Choose(int i)
        {
            if (!SaveService.Slots[i].HasGame)
            {
                UIAudio.Play(UISound.Error);
                return;
            }
            _pending = i;
            if (_confirmTitle != null)
                _confirmTitle.text = (Loc.IsEnglish ? "Load slot " : "¿Cargar la ranura ") + (i + 1) + (Loc.IsEnglish ? "?" : "?");
            Router.Push(_confirm, true);
        }

        private void ConfirmChoice()
        {
            if (_pending < 0) return;
            int slot = _pending;
            _pending = -1;
            onChosen?.Invoke(slot);
        }
    }
}
