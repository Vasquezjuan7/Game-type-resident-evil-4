using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gameplay.RE4
{
    /// <summary>
    /// Tienda del mercader al estilo RE4: mejora potencia, cadencia, velocidad de recarga y capacidad
    /// de cada arma pagando pesetas. Se abre hablando con el mercader [E] o con la tecla U. Pausa el juego.
    /// </summary>
    public class WeaponUpgradeUI : MonoBehaviour
    {
        public static WeaponUpgradeUI Instance { get; private set; }

        public bool IsOpen { get; private set; }

        [Header("Tienda")]
        [Tooltip("Lo que vende el mercader (usa buyPrice / buyAmount de cada objeto).")]
        public List<ItemDefinition> stock = new List<ItemDefinition>();
        public int knifeRepairCost = 2500;
        [Tooltip("Solo se puede comerciar junto a un mercader (como en RE4).")]
        public bool requireMerchant = true;
        public float merchantRange = 7f;

        private class ShopRow
        {
            public string name, info;
            public int price;
            public bool enabled;
            public System.Func<string> action;
        }

        private static readonly string[] TabNames = { "COMPRAR", "VENDER", "MEJORAR" };
        private int _tab;
        private int _openFrame;
        private Text _tabs;
        private RectTransform _listPanel;
        private readonly List<Image> _shopBacks = new List<Image>();
        private readonly List<Text> _shopNames = new List<Text>();
        private readonly List<Text> _shopInfos = new List<Text>();
        private readonly List<Text> _shopPrices = new List<Text>();
        private List<ShopRow> _shop = new List<ShopRow>();
        private int _shopRow, _shopScroll;
        private const int ShopVisible = 7;

        private AttacheCase _case;
        private WeaponSystem _weapons;
        private FirstPersonController _fps;

        private Canvas _canvas;
        private Text _pesetas, _weaponTitle, _weaponDesc, _feedback, _quote;
        private RectTransform _listRoot;
        private readonly List<Image> _weaponButtons = new List<Image>();
        private readonly List<Image> _rowBacks = new List<Image>();
        private readonly List<Text> _rowLabels = new List<Text>();
        private readonly List<Text> _rowValues = new List<Text>();
        private readonly List<Text> _rowCosts = new List<Text>();
        private readonly List<Image[]> _rowPips = new List<Image[]>();

        private List<CaseEntry> _list = new List<CaseEntry>();
        private int _weaponIndex;
        private int _row;
        private float _prevTimeScale = 1f;
        private float _feedbackTime = -10f;

        private static readonly string[] Quotes =
        {
            "«¿Qué estás comprando?»",
            "«Ah... tienes buen ojo, extranjero.»",
            "«¿Algo más?»",
            "«¡Je, je, je! Gracias.»",
        };

        private void Awake()
        {
            Instance = this;
            _case = GetComponent<AttacheCase>();
            _weapons = GetComponent<WeaponSystem>();
            _fps = GetComponent<FirstPersonController>();
        }

        private AudioSource _merchantVoice;

        /// <summary>El mercader dice la frase en voz alta (sin las comillas).</summary>
        private void MerchantSay(string quote)
        {
            if (_merchantVoice == null)
            {
                _merchantVoice = gameObject.AddComponent<AudioSource>();
                _merchantVoice.spatialBlend = 0f;
                _merchantVoice.playOnAwake = false;
            }
            NpcVoice.Get().Say(_merchantVoice, quote.Replace("«", "").Replace("»", ""), NpcVoice.VoiceMerchant, 1f, 0.9f);
        }

        private void Start()
        {
            foreach (var q in Quotes) NpcVoice.Get().Prefetch(q.Replace("«", "").Replace("»", ""), NpcVoice.VoiceMerchant);
            if (_canvas == null) Build();
            if (!IsOpen) _canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ Abrir / cerrar

        private void Update()
        {
            var kb = Keyboard.current;
            if (!IsOpen)
            {
                if (kb != null && kb.uKey.wasPressedThisFrame && CanOpen())
                {
                    if (!requireMerchant || NearMerchant()) Open();
                    else
                    {
                        PlayerInteractor pi = GetComponent<PlayerInteractor>();
                        if (pi != null) pi.ShowMessage("Busca al mercader (farol azul) para comerciar.");
                    }
                }
                return;
            }

            bool close = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.uKey.wasPressedThisFrame)
                         || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (close)
            {
                Close();
                return;
            }
            UpdateOpen();
        }

        private bool NearMerchant()
        {
            foreach (var m in Merchant.All)
            {
                if (m == null) continue;
                Vector3 d = m.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= merchantRange) return true;
            }
            return false;
        }

        public bool CanOpen()
        {
            if (IsOpen || _case == null) return false;
            if (PlayerHealth.Instance != null && (PlayerHealth.Instance.IsDead || PlayerHealth.Instance.IsGrabbed)) return false;
            return _fps == null || !_fps.InputLocked;
        }

        public void Open()
        {
            if (!CanOpen()) return;
            if (_canvas == null) Build();
            IsOpen = true;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_fps != null)
            {
                _fps.PushInputLock();
                _fps.LockCursor(false);
            }
            _list = new List<CaseEntry>();
            foreach (var e in _case.Weapons()) if (WeaponStats.CanUpgrade(e.item.weapon)) _list.Add(e);
            _weaponIndex = Mathf.Max(0, _weapons != null ? _list.IndexOf(_weapons.CurrentEntry) : 0);
            _row = 0;
            _tab = 0;
            _openFrame = Time.frameCount;
            _shopRow = 0;
            _shopScroll = 0;
            _quote.text = Quotes[0];
            MerchantSay(Quotes[0]);
            _feedback.text = "";
            _canvas.gameObject.SetActive(true);
            PlayUI(ProceduralAudio.CaseOpen());
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            if (_merchantVoice != null) _merchantVoice.Stop();
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
            if (_fps != null)
            {
                _fps.PopInputLock();
                _fps.LockCursor(true);
                _fps.IgnoreEscapeFrame = Time.frameCount;
            }
            _canvas.gameObject.SetActive(false);
            PlayUI(ProceduralAudio.CaseOpen());
        }

        private void PlayUI(AudioClip clip)
        {
            if (_weapons != null && _weapons.audioSource != null) _weapons.audioSource.PlayOneShot(clip, 0.6f);
        }

        // ------------------------------------------------------------ Construccion

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("RE4_Mercader", 60);
            Transform root = _canvas.transform;
            UIFactory.Stretch(root, "Dim", new Color(0f, 0f, 0f, 0.8f));
            Color gold = new Color(1f, 0.85f, 0.45f);

            Image frame = UIFactory.Panel(root, "Frame", new Color(0.13f, 0.08f, 0.12f, 0.98f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1300f, 760f));
            UIFactory.AddOutline(frame, new Color(0.6f, 0.42f, 0.2f, 1f), 3f);
            Transform f = frame.transform;

            UIFactory.Label(f, "Title", "MERCADER  ·  MEJORAS DE ARMAS", 36, gold, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(800f, 50f), FontStyle.Bold);
            _quote = UIFactory.Label(f, "Quote", "", 22, new Color(0.85f, 0.75f, 0.95f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -70f), new Vector2(800f, 34f), FontStyle.Italic);
            _pesetas = UIFactory.Label(f, "Pesetas", "", 34, gold, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -24f), new Vector2(460f, 50f), FontStyle.Bold);

            // Lista de armas
            Image list = UIFactory.Panel(f, "List", new Color(0.05f, 0.04f, 0.05f, 0.9f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -120f), new Vector2(360f, 540f));
            _listRoot = list.rectTransform;
            _listPanel = list.rectTransform;

            _tabs = UIFactory.Label(f, "Tabs", "", 26, Color.white, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, -76f), new Vector2(700f, 36f), FontStyle.Bold);

            // Detalle del arma
            Image detail = UIFactory.Panel(f, "Detail", new Color(0.05f, 0.04f, 0.05f, 0.9f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(410f, -120f), new Vector2(860f, 540f));
            _weaponTitle = UIFactory.Label(detail.transform, "WTitle", "", 32, Color.white, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(800f, 44f), FontStyle.Bold);
            _weaponDesc = UIFactory.Label(detail.transform, "WDesc", "", 19, new Color(0.8f, 0.8f, 0.78f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(810f, 60f));

            for (int i = 0; i < WeaponStats.AllStats.Length; i++)
            {
                float y = -140f - i * 92f;
                Image back = UIFactory.Panel(detail.transform, "Row" + i, new Color(1f, 1f, 1f, 0.04f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, y), new Vector2(828f, 80f));
                _rowBacks.Add(back);
                _rowLabels.Add(UIFactory.Label(back.transform, "Name", "", 24, gold, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 12f), new Vector2(260f, 36f), FontStyle.Bold));
                var pips = new Image[8];
                for (int p = 0; p < pips.Length; p++)
                {
                    pips[p] = UIFactory.Panel(back.transform, "Pip" + p, Color.gray, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(20f + p * 30f, -18f), new Vector2(24f, 14f));
                }
                _rowPips.Add(pips);
                _rowValues.Add(UIFactory.Label(back.transform, "Value", "", 22, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(300f, 0f), new Vector2(330f, 60f)));
                _rowCosts.Add(UIFactory.Label(back.transform, "Cost", "", 24, gold, TextAnchor.MiddleRight,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(200f, 60f), FontStyle.Bold));
            }

            for (int i = 0; i < ShopVisible; i++)
            {
                float y = -20f - i * 70f;
                Image back = UIFactory.Panel(detail.transform, "Shop" + i, new Color(1f, 1f, 1f, 0.04f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, y), new Vector2(828f, 62f));
                _shopBacks.Add(back);
                _shopNames.Add(UIFactory.Label(back.transform, "Name", "", 23, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 10f), new Vector2(520f, 32f), FontStyle.Bold));
                _shopInfos.Add(UIFactory.Label(back.transform, "Info", "", 17, new Color(0.75f, 0.75f, 0.72f), TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -14f), new Vector2(600f, 26f)));
                _shopPrices.Add(UIFactory.Label(back.transform, "Price", "", 24, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleRight,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(220f, 50f), FontStyle.Bold));
                back.gameObject.SetActive(false);
            }

            _feedback = UIFactory.Label(detail.transform, "Feedback", "", 22, new Color(0.5f, 1f, 0.55f), TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(810f, 36f), FontStyle.Bold);

            UIFactory.Label(f, "Help",
                "<b>Q/E</b> Pestaña    <b>↑/↓ W/S</b> Elegir    <b>←/→ A/D</b> Arma    <b>ENTER / Clic</b> Aceptar    <b>ESC / U</b> Salir",
                19, new Color(0.85f, 0.85f, 0.8f), TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1240f, 40f));
        }

        // ------------------------------------------------------------ Vista

        private CaseEntry Selected => _list.Count > 0 ? _list[Mathf.Clamp(_weaponIndex, 0, _list.Count - 1)] : null;

        private void Refresh()
        {
            _pesetas.text = $"₧ {_case.pesetas:N0}";
            var tabs = new System.Text.StringBuilder();
            for (int i = 0; i < TabNames.Length; i++)
            {
                tabs.Append(i == _tab ? $"<color=#ffd060>[ {TabNames[i]} ]</color>" : $"<color=#888888>{TabNames[i]}</color>");
                if (i < TabNames.Length - 1) tabs.Append("      ");
            }
            _tabs.text = tabs.ToString();

            bool upgrade = _tab == 2;
            _listPanel.gameObject.SetActive(upgrade);
            foreach (var b in _shopBacks) b.gameObject.SetActive(false);
            if (!upgrade)
            {
                foreach (var r in _rowBacks) r.gameObject.SetActive(false);
                RefreshShop();
                return;
            }

            // Botones de armas
            while (_weaponButtons.Count < _list.Count)
            {
                int i = _weaponButtons.Count;
                Image b = UIFactory.Panel(_listRoot, "Weapon" + i, Color.clear, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -10f - i * 66f), new Vector2(340f, 58f));
                UIFactory.Label(b.transform, "Name", "", 22, Color.white, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(-20f, 0f), FontStyle.Bold);
                _weaponButtons.Add(b);
            }
            for (int i = 0; i < _weaponButtons.Count; i++)
            {
                bool show = i < _list.Count;
                _weaponButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                bool sel = i == _weaponIndex;
                _weaponButtons[i].color = sel ? new Color(0.55f, 0.38f, 0.15f, 0.9f) : new Color(1f, 1f, 1f, 0.05f);
                Text t = _weaponButtons[i].GetComponentInChildren<Text>();
                t.text = _list[i].item.displayName;
                t.color = sel ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            }

            CaseEntry e = Selected;
            if (e == null)
            {
                _weaponTitle.text = "No llevas armas que mejorar";
                _weaponDesc.text = "";
                foreach (var r in _rowBacks) r.gameObject.SetActive(false);
                return;
            }

            WeaponDefinition w = e.item.weapon;
            if (e.upgrades == null) e.upgrades = new WeaponUpgrades();
            _weaponTitle.text = w.displayName;
            _weaponDesc.text = e.item.description;

            for (int i = 0; i < WeaponStats.AllStats.Length; i++)
            {
                UpgradeStat stat = WeaponStats.AllStats[i];
                int lvl = e.upgrades.Get(stat);
                bool maxed = lvl >= w.maxUpgradeLevel;
                bool shotgunCap = stat == UpgradeStat.Capacity && w.capacityPerLevel <= 0;
                _rowBacks[i].gameObject.SetActive(true);
                _rowBacks[i].color = i == _row ? new Color(0.55f, 0.38f, 0.15f, 0.55f) : new Color(1f, 1f, 1f, 0.04f);
                _rowLabels[i].text = WeaponStats.StatName(stat);

                Image[] pips = _rowPips[i];
                for (int p = 0; p < pips.Length; p++)
                {
                    pips[p].gameObject.SetActive(p < w.maxUpgradeLevel);
                    pips[p].color = p < lvl ? new Color(1f, 0.78f, 0.25f) : new Color(0.25f, 0.22f, 0.22f);
                }

                string now = WeaponStats.Describe(w, stat, lvl);
                if (maxed || shotgunCap)
                {
                    _rowValues[i].text = $"{now}   <color=#ffd060>MÁX.</color>";
                    _rowCosts[i].text = "—";
                    _rowCosts[i].color = new Color(0.6f, 0.6f, 0.6f);
                }
                else
                {
                    _rowValues[i].text = $"{now}  <color=#9fe8a0>→ {WeaponStats.Describe(w, stat, lvl + 1)}</color>";
                    int cost = WeaponStats.Cost(w, stat, lvl);
                    _rowCosts[i].text = $"₧ {cost:N0}";
                    _rowCosts[i].color = cost <= _case.pesetas ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.35f, 0.3f);
                }
            }
        }

        // ------------------------------------------------------------ Comprar / vender

        private void BuildShopRows()
        {
            _shop = new List<ShopRow>();
            if (_tab == 0)
            {
                WeaponSystem ws = _weapons;
                if (ws != null)
                {
                    int cost = Mathf.RoundToInt(knifeRepairCost * (1f - ws.KnifeNormalized) / 100f) * 100;
                    _shop.Add(new ShopRow
                    {
                        name = "Reparar cuchillo",
                        info = $"Estado actual: {ws.KnifeNormalized * 100f:0}%",
                        price = Mathf.Max(100, cost),
                        enabled = ws.KnifeNormalized < 0.999f,
                        action = () => { ws.knifeDurability = ws.knifeMaxDurability; return "Cuchillo como nuevo."; }
                    });
                }
                foreach (var it in stock)
                {
                    if (it == null || it.buyPrice <= 0) continue;
                    ItemDefinition item = it;
                    bool owned = item.weapon != null && _case.FindWeapon(item.weapon) != null;
                    _shop.Add(new ShopRow
                    {
                        name = item.displayName + (item.buyAmount > 1 ? $" x{item.buyAmount}" : ""),
                        info = owned ? "Ya la tienes" : item.description.Split('\n')[0],
                        price = item.buyPrice,
                        enabled = !owned,
                        action = () =>
                        {
                            _case.TryAdd(item, Mathf.Max(1, item.buyAmount), out int left);
                            return left > 0 ? null : $"Compraste {item.displayName}.";
                        }
                    });
                }
            }
            else
            {
                foreach (var e in _case.entries)
                {
                    if (e.item == null || e.item.sellPrice <= 0) continue;
                    CaseEntry entry = e;
                    _shop.Add(new ShopRow
                    {
                        name = e.item.displayName + (e.count > 1 ? $" ({e.count})" : ""),
                        info = e.item.type == ItemType.Treasure ? "Tesoro" : e.item.description.Split('\n')[0],
                        price = e.item.sellPrice,
                        enabled = true,
                        action = () =>
                        {
                            if (entry.count > 1) { entry.count--; _case.NotifyChanged(); }
                            else _case.Remove(entry);
                            _case.AddPesetas(entry.item.sellPrice);
                            return $"Vendiste {entry.item.displayName}.";
                        }
                    });
                }
            }
            _shopRow = Mathf.Clamp(_shopRow, 0, Mathf.Max(0, _shop.Count - 1));
        }

        private void RefreshShop()
        {
            BuildShopRows();
            _weaponTitle.text = _tab == 0 ? "«¿Qué estás comprando?»" : "«¿Qué estás vendiendo?»";
            _weaponDesc.text = _shop.Count == 0 ? (_tab == 0 ? "No hay nada a la venta." : "No llevas nada que me interese, extranjero.") : "";
            if (_shopRow < _shopScroll) _shopScroll = _shopRow;
            if (_shopRow >= _shopScroll + ShopVisible) _shopScroll = _shopRow - ShopVisible + 1;
            for (int i = 0; i < ShopVisible; i++)
            {
                int idx = _shopScroll + i;
                bool show = idx < _shop.Count;
                Image back = _shopBacks[i];
                back.gameObject.SetActive(show);
                if (!show) continue;
                ShopRow r = _shop[idx];
                back.rectTransform.anchoredPosition = new Vector2(16f, -120f - i * 64f);
                back.color = idx == _shopRow ? new Color(0.55f, 0.38f, 0.15f, 0.55f) : new Color(1f, 1f, 1f, 0.04f);
                _shopNames[i].text = r.name;
                _shopNames[i].color = r.enabled ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                _shopInfos[i].text = r.info;
                bool afford = _tab == 1 || r.price <= _case.pesetas;
                _shopPrices[i].text = (_tab == 1 ? "+" : "") + $"₧ {r.price:N0}";
                _shopPrices[i].color = afford ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.35f, 0.3f);
            }
        }

        private void ShopAccept()
        {
            if (_shopRow < 0 || _shopRow >= _shop.Count) return;
            ShopRow r = _shop[_shopRow];
            if (!r.enabled) { PlayUI(ProceduralAudio.Click(250f, 60f)); return; }
            if (_tab == 0)
            {
                if (!_case.SpendPesetas(r.price))
                {
                    Feedback("«No tienes suficientes pesetas, extranjero.»", new Color(1f, 0.4f, 0.35f));
                    PlayUI(ProceduralAudio.Click(250f, 60f));
                    return;
                }
                string msg = r.action();
                if (msg == null)
                {
                    _case.AddPesetas(r.price); // no cabia: devolver el dinero
                    Feedback("No tienes sitio en el maletín.", new Color(1f, 0.6f, 0.3f));
                    PlayUI(ProceduralAudio.Click(250f, 60f));
                    return;
                }
                Feedback(msg, new Color(0.5f, 1f, 0.55f));
            }
            else Feedback(r.action(), new Color(0.5f, 1f, 0.55f));
            _quote.text = Quotes[Random.Range(1, Quotes.Length)];
            MerchantSay(_quote.text);
            PlayUI(ProceduralAudio.Pickup());
            Refresh();
        }

        // ------------------------------------------------------------ Interaccion

        private void UpdateOpen()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            bool changed = false;

            if (kb != null && Time.frameCount != _openFrame && (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            {
                _tab = (_tab + (kb.eKey.wasPressedThisFrame ? 1 : 2)) % 3;
                _shopRow = 0;
                _shopScroll = 0;
                PlayUI(ProceduralAudio.Click(900f, 80f));
                Refresh();
                return;
            }
            if (_tab != 2)
            {
                if (kb != null)
                {
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { _shopRow = Mathf.Max(0, _shopRow - 1); changed = true; }
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { _shopRow = Mathf.Min(Mathf.Max(0, _shop.Count - 1), _shopRow + 1); changed = true; }
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) ShopAccept();
                }
                if (mouse != null)
                {
                    Vector2 mp = mouse.position.ReadValue();
                    for (int i = 0; i < _shopBacks.Count; i++)
                    {
                        if (!_shopBacks[i].gameObject.activeSelf || !RectTransformUtility.RectangleContainsScreenPoint(_shopBacks[i].rectTransform, mp, null)) continue;
                        int idx = _shopScroll + i;
                        if (_shopRow != idx) { _shopRow = idx; changed = true; }
                        if (mouse.leftButton.wasPressedThisFrame) ShopAccept();
                    }
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel > 0.1f) { _shopRow = Mathf.Max(0, _shopRow - 1); changed = true; }
                    if (wheel < -0.1f) { _shopRow = Mathf.Min(Mathf.Max(0, _shop.Count - 1), _shopRow + 1); changed = true; }
                }
                if (changed) { PlayUI(ProceduralAudio.Click(900f, 80f)); Refresh(); }
                float fts = Time.unscaledTime - _feedbackTime;
                _feedback.color = new Color(_feedback.color.r, _feedback.color.g, _feedback.color.b, Mathf.Clamp01(2.5f - fts));
                return;
            }

            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { _row = (_row + 3) % 4; changed = true; }
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { _row = (_row + 1) % 4; changed = true; }
                if (_list.Count > 0 && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) { _weaponIndex = (_weaponIndex + _list.Count - 1) % _list.Count; changed = true; }
                if (_list.Count > 0 && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) { _weaponIndex = (_weaponIndex + 1) % _list.Count; changed = true; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Buy();
            }
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) { _row = (_row + 3) % 4; changed = true; }
                if (pad.dpad.down.wasPressedThisFrame) { _row = (_row + 1) % 4; changed = true; }
                if (_list.Count > 0 && pad.leftShoulder.wasPressedThisFrame) { _weaponIndex = (_weaponIndex + _list.Count - 1) % _list.Count; changed = true; }
                if (_list.Count > 0 && pad.rightShoulder.wasPressedThisFrame) { _weaponIndex = (_weaponIndex + 1) % _list.Count; changed = true; }
                if (pad.buttonSouth.wasPressedThisFrame) Buy();
            }

            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                for (int i = 0; i < _list.Count && i < _weaponButtons.Count; i++)
                {
                    if (mouse.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(_weaponButtons[i].rectTransform, pos, null))
                    {
                        _weaponIndex = i;
                        changed = true;
                    }
                }
                for (int i = 0; i < _rowBacks.Count; i++)
                {
                    if (!_rowBacks[i].gameObject.activeSelf || !RectTransformUtility.RectangleContainsScreenPoint(_rowBacks[i].rectTransform, pos, null)) continue;
                    if (_row != i) { _row = i; changed = true; }
                    if (mouse.leftButton.wasPressedThisFrame) Buy();
                }
            }

            if (changed)
            {
                PlayUI(ProceduralAudio.Click(900f, 80f));
                Refresh();
            }

            float ft = Time.unscaledTime - _feedbackTime;
            _feedback.color = new Color(_feedback.color.r, _feedback.color.g, _feedback.color.b, Mathf.Clamp01(2.5f - ft));
        }

        private void Buy()
        {
            CaseEntry e = Selected;
            if (e == null) return;
            WeaponDefinition w = e.item.weapon;
            if (e.upgrades == null) e.upgrades = new WeaponUpgrades();
            UpgradeStat stat = WeaponStats.AllStats[_row];
            int lvl = e.upgrades.Get(stat);

            if (lvl >= w.maxUpgradeLevel || (stat == UpgradeStat.Capacity && w.capacityPerLevel <= 0))
            {
                Feedback("Esa mejora ya está al máximo.", new Color(1f, 0.8f, 0.4f));
                PlayUI(ProceduralAudio.Click(250f, 60f));
                return;
            }
            int cost = WeaponStats.Cost(w, stat, lvl);
            if (!_case.SpendPesetas(cost))
            {
                Feedback("«No tienes suficientes pesetas, extranjero.»", new Color(1f, 0.4f, 0.35f));
                PlayUI(ProceduralAudio.Click(250f, 60f));
                return;
            }

            e.upgrades.Set(stat, lvl + 1);
            _quote.text = Quotes[Random.Range(1, Quotes.Length)];
            MerchantSay(_quote.text);
            Feedback($"{w.displayName}: {WeaponStats.StatName(stat)} nivel {lvl + 2}", new Color(0.5f, 1f, 0.55f));
            PlayUI(ProceduralAudio.Rack());
            _case.NotifyChanged();
            Refresh();
        }

        private void Feedback(string msg, Color c)
        {
            _feedback.text = msg;
            _feedback.color = c;
            _feedbackTime = Time.unscaledTime;
        }
    }
}
