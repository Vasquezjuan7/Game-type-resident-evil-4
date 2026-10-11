using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.RE4
{
    /// <summary>
    /// HUD estilo Resident Evil 4: vida (FINE/CAUTION/DANGER) y municion abajo a la derecha,
    /// mira, marcador de impacto, mira telescopica, mensajes contextuales, QTE de agarre,
    /// vineta de dano, banners de oleada y pantalla de muerte.
    /// </summary>
    public class RE4HUD : MonoBehaviour
    {
        public float controlsHintSeconds = 18f;

        private Canvas _canvas;
        private Text _ammoLoaded, _ammoReserve, _weaponName, _status, _waveInfo, _pesetas, _pesetasGain;
        private float _pesetasGainTime = -10f;
        private int _pesetasGainAmount;
        private AttacheCase _case;
        private Image _bossBack, _bossFill;
        private Text _parry, _knife;
        private float _parryTime = -10f;
        private bool _parryPerfect;
        private Text _bossName;
        private Image _healthFill, _healthBack;
        private RectTransform _ecg;
        private Image _crosshair;
        private Image[] _hitMarks = new Image[4];
        private Image _scope;
        private Text _prompt, _message, _banner, _qteText, _controls;
        private Image _qteBar, _qteBack;
        private Image _vignette, _deathOverlay;
        private Text _deathTitle, _deathHint;
        private float _hitMarkerTime = -10f;
        private bool _hitKill;
        private float _bannerTime = -10f;
        private float _startTime;

        private WeaponSystem _weapons;
        private PlayerInteractor _interactor;

        private void Start()
        {
            _weapons = WeaponSystem.Instance != null ? WeaponSystem.Instance : FindAnyObjectByType<WeaponSystem>();
            _interactor = FindAnyObjectByType<PlayerInteractor>();
            _case = AttacheCase.Instance != null ? AttacheCase.Instance : FindAnyObjectByType<AttacheCase>();
            Build();
            if (_case != null) _case.OnPesetasChanged += OnPesetas;
            if (_weapons != null) _weapons.OnHitConfirmed += OnHit;
            if (_weapons != null) _weapons.OnParry += OnParry;
            HordeWaveManager.OnBanner += ShowBanner;
            _startTime = Time.time;
        }

        private void OnDestroy()
        {
            if (_weapons != null) _weapons.OnHitConfirmed -= OnHit;
            if (_weapons != null) _weapons.OnParry -= OnParry;
            if (_case != null) _case.OnPesetasChanged -= OnPesetas;
            HordeWaveManager.OnBanner -= ShowBanner;
        }

        private void OnHit(bool kill, bool head)
        {
            _hitMarkerTime = Time.time;
            _hitKill = kill;
        }

        private void OnParry(bool perfect)
        {
            _parryTime = Time.time;
            _parryPerfect = perfect;
        }

        private void OnPesetas(int delta)
        {
            if (delta <= 0) return;
            _pesetasGainAmount = Time.time - _pesetasGainTime < 2.5f ? _pesetasGainAmount + delta : delta;
            _pesetasGainTime = Time.time;
        }

        public void ShowBanner(string text)
        {
            if (_banner == null) return;
            _banner.text = text;
            _bannerTime = Time.time;
        }

        // ------------------------------------------------------------ Construccion

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("RE4_HUD", 10);
            Transform root = _canvas.transform;
            Color amber = new Color(1f, 0.82f, 0.35f);

            // Vineta de dano
            _vignette = UIFactory.Stretch(root, "DamageVignette", new Color(0.6f, 0f, 0f, 0f));
            _vignette.sprite = UIFactory.RadialSprite(256, 0.45f, 1.25f, false);

            // Mira telescopica
            _scope = UIFactory.Panel(root, "Scope", Color.black, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
            _scope.sprite = UIFactory.ScopeSprite(512);
            Image left = UIFactory.Panel(_scope.transform, "L", Color.black, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(2000f, 0f));
            Image right = UIFactory.Panel(_scope.transform, "R", Color.black, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2000f, 0f));
            left.raycastTarget = right.raycastTarget = false;
            _scope.gameObject.SetActive(false);

            // Mira central
            _crosshair = UIFactory.Panel(root, "Crosshair", new Color(1f, 1f, 1f, 0.8f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
            UIFactory.AddOutline(_crosshair, new Color(0f, 0f, 0f, 0.7f), 1f);
            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                Image m = UIFactory.Panel(root, "HitMark" + i, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 16f, new Vector2(3f, 13f));
                m.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang - 90f);
                _hitMarks[i] = m;
            }

            // Panel inferior derecho: arma + municion + vida
            Image panel = UIFactory.Panel(root, "StatusPanel", new Color(0f, 0f, 0f, 0.55f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(420f, 170f));
            UIFactory.AddOutline(panel, new Color(amber.r, amber.g, amber.b, 0.25f), 1.5f);
            _weaponName = UIFactory.Label(panel.transform, "WeaponName", "", 20, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(380f, 30f), FontStyle.Bold);
            _ammoLoaded = UIFactory.Label(panel.transform, "AmmoLoaded", "0", 64, amber, TextAnchor.LowerRight,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(230f, -34f), new Vector2(200f, 70f), FontStyle.Bold);
            _ammoReserve = UIFactory.Label(panel.transform, "AmmoReserve", "/ 0", 30, new Color(0.9f, 0.9f, 0.85f), TextAnchor.LowerLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(238f, -46f), new Vector2(170f, 50f), FontStyle.Bold);

            _healthBack = UIFactory.Panel(panel.transform, "HealthBack", new Color(0.12f, 0.12f, 0.12f, 0.9f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 22f), new Vector2(300f, 22f));
            _healthFill = UIFactory.Panel(_healthBack.transform, "HealthFill", new Color(0.2f, 0.85f, 0.3f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _ecg = UIFactory.Panel(_healthBack.transform, "ECG", new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 0f)).rectTransform;
            _status = UIFactory.Label(panel.transform, "Status", "FINE", 22, new Color(0.3f, 1f, 0.4f), TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(326f, 18f), new Vector2(110f, 30f), FontStyle.Bold);
            UIFactory.Label(panel.transform, "LifeLbl", "LIFE", 14, new Color(0.7f, 0.7f, 0.7f), TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 46f), new Vector2(100f, 20f));

            // Pesetas
            _pesetas = UIFactory.Label(root, "Pesetas", "", 26, amber, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 206f), new Vector2(400f, 36f), FontStyle.Bold);
            _pesetasGain = UIFactory.Label(root, "PesetasGain", "", 22, new Color(1f, 0.95f, 0.6f), TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 240f), new Vector2(400f, 30f), FontStyle.Bold);

            // Parry y estado del cuchillo
            _parry = UIFactory.Label(root, "Parry", "", 44, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 70f), FontStyle.Bold);
            _knife = UIFactory.Label(panel.transform, "Knife", "", 16, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(200f, 24f));

            // Barra del jefe
            _bossBack = UIFactory.Panel(root, "BossBack", new Color(0f, 0f, 0f, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 22f));
            UIFactory.AddOutline(_bossBack, new Color(0.8f, 0.15f, 0.1f, 0.8f), 2f);
            _bossFill = UIFactory.Panel(_bossBack.transform, "BossFill", new Color(0.75f, 0.08f, 0.06f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _bossName = UIFactory.Label(_bossBack.transform, "BossName", "", 22, new Color(1f, 0.85f, 0.8f), TextAnchor.LowerCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(760f, 30f), FontStyle.Bold);
            _bossBack.gameObject.SetActive(false);

            // Info de oleada
            _waveInfo = UIFactory.Label(root, "WaveInfo", "", 22, new Color(1f, 0.45f, 0.35f), TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(600f, 40f), FontStyle.Bold);

            // Banner central superior
            _banner = UIFactory.Label(root, "Banner", "", 46, new Color(0.95f, 0.2f, 0.15f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 160f), FontStyle.Bold);

            // Avisos contextuales
            _prompt = UIFactory.Label(root, "Prompt", "", 28, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 50f), FontStyle.Bold);
            _message = UIFactory.Label(root, "Message", "", 24, amber, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 40f));

            // QTE de agarre
            _qteText = UIFactory.Label(root, "QTE", "¡PULSA  [ESPACIO]  REPETIDAMENTE!", 36, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 60f), FontStyle.Bold);
            _qteBack = UIFactory.Panel(root, "QTEBack", new Color(0f, 0f, 0f, 0.7f), new Vector2(0.5f, 0.37f), new Vector2(0.5f, 0.37f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 22f));
            _qteBar = UIFactory.Panel(_qteBack.transform, "QTEFill", new Color(1f, 0.85f, 0.2f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            // Controles
            _controls = UIFactory.Label(root, "Controls",
                "<b>CONTROLES</b>\nWASD Mover · Shift Correr\nClic der. Apuntar · Clic izq. Disparar\nR Recargar · 1-5 / Rueda Cambiar arma\nV Cuchillo · F Patada (enemigo aturdido)\nE Recoger / Hablar · TAB Maletín (C fabricar) · J Archivos\nV Cuchillo (mantén para guardia, pulsa al recibir un golpe para PARRY)\nS+Shift Giro rápido · L Linterna",
                18, new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 26f), new Vector2(700f, 230f));

            // Pantalla de muerte
            _deathOverlay = UIFactory.Stretch(root, "DeathOverlay", new Color(0.15f, 0f, 0f, 0f));
            _deathTitle = UIFactory.Label(_deathOverlay.transform, "DeathTitle", "HAS MUERTO", 110, new Color(0.75f, 0.05f, 0.05f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 160f), FontStyle.Bold);
            _deathHint = UIFactory.Label(_deathOverlay.transform, "DeathHint", "Pulsa ENTER para reintentar", 30, new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 50f));
            _deathOverlay.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ Actualizacion

        private void Update()
        {
            if (_canvas == null) return;
            PlayerHealth ph = PlayerHealth.Instance;
            WeaponSystem ws = _weapons;

            // Municion
            if (ws != null && ws.Current != null)
            {
                _weaponName.text = ws.Current.displayName.ToUpperInvariant() + (ws.IsReloading ? "   <color=#ffcc55>RECARGANDO</color>" : "");
                _ammoLoaded.text = ws.Loaded.ToString();
                _ammoLoaded.color = ws.Loaded == 0 ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.82f, 0.35f);
                _ammoReserve.text = "/ " + ws.Reserve;
                if (ws.Loaded > 0 && ws.Loaded <= Mathf.Max(1, ws.Capacity / 4)) _ammoLoaded.color = new Color(1f, 0.6f, 0.25f);
            }
            else
            {
                _weaponName.text = "SIN ARMA";
                _ammoLoaded.text = "-";
                _ammoReserve.text = "";
            }

            // Vida
            if (ph != null)
            {
                float n = ph.Normalized;
                RectTransform fill = _healthFill.rectTransform;
                fill.anchorMax = new Vector2(n, 1f);
                Color c;
                string st;
                switch (ph.Status)
                {
                    case HealthStatus.Fine: c = new Color(0.2f, 0.85f, 0.3f); st = "FINE"; break;
                    case HealthStatus.Caution: c = new Color(0.95f, 0.75f, 0.15f); st = "CAUTION"; break;
                    default: c = new Color(0.9f, 0.15f, 0.1f); st = "DANGER"; break;
                }
                _healthFill.color = c;
                _status.text = st;
                _status.color = c;
                // ECG que recorre la barra (mas rapido en peligro)
                float speed = ph.Status == HealthStatus.Danger ? 1.6f : ph.Status == HealthStatus.Caution ? 1.1f : 0.7f;
                float x = Mathf.Repeat(Time.time * speed, 1f) * 300f;
                _ecg.anchoredPosition = new Vector2(x, 0f);

                // Vineta
                float hitFlash = Mathf.Clamp01(1f - (Time.time - ph.LastHitTime) / 0.6f) * 0.55f;
                float low = ph.Status == HealthStatus.Danger ? 0.25f + Mathf.Sin(Time.time * 6f) * 0.08f : 0f;
                _vignette.color = new Color(0.6f, 0f, 0f, Mathf.Max(hitFlash, low));

                // QTE
                bool grabbed = ph.IsGrabbed;
                _qteText.gameObject.SetActive(grabbed);
                _qteBack.gameObject.SetActive(grabbed);
                if (grabbed)
                {
                    _qteBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ph.GrabProgress), 1f);
                    _qteText.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 18f) * 0.04f);
                }

                // Muerte
                if (ph.IsDead)
                {
                    _deathOverlay.gameObject.SetActive(true);
                    float t = Mathf.Clamp01((Time.unscaledTime - ph.DeathTime) / 2f);
                    _deathOverlay.color = new Color(0.15f, 0f, 0f, t * 0.75f);
                    _deathTitle.color = new Color(0.75f, 0.05f, 0.05f, t);
                    _deathHint.color = new Color(0.9f, 0.9f, 0.9f, Mathf.Clamp01(t * 2f - 1f));
                }
            }

            // Mira, mira telescopica y marcador
            bool scoped = ws != null && ws.IsScoped;
            bool laser = ws != null && ws.AimBlend > 0.6f && !scoped;
            _scope.gameObject.SetActive(scoped);
            _crosshair.gameObject.SetActive(!scoped && !laser && (ph == null || !ph.IsDead));
            float hm = Mathf.Clamp01(1f - (Time.time - _hitMarkerTime) / 0.25f);
            foreach (var m in _hitMarks)
            {
                m.color = _hitKill ? new Color(1f, 0.15f, 0.1f, hm) : new Color(1f, 1f, 1f, hm);
            }

            // Parry y cuchillo
            float pt = Time.time - _parryTime;
            _parry.text = pt < 0.9f ? (_parryPerfect ? "¡PARRY PERFECTO!" : "PARRY") : "";
            _parry.color = new Color(1f, _parryPerfect ? 0.85f : 0.95f, _parryPerfect ? 0.3f : 0.8f, Mathf.Clamp01(1.2f - pt * 1.3f));
            if (ws != null)
            {
                float k = ws.KnifeNormalized;
                string kc = k <= 0f ? "#ff4030" : k < 0.3f ? "#ffaa40" : "#cccccc";
                _knife.text = ws.IsGuarding ? "<color=#ffd060>GUARDIA</color>" : $"<color={kc}>Cuchillo {k * 100f:0}%</color>";
            }

            // Pesetas
            if (_case != null)
            {
                _pesetas.text = $"₧ {_case.pesetas:N0}";
                float gt = Time.time - _pesetasGainTime;
                _pesetasGain.text = gt < 2.5f ? $"+{_pesetasGainAmount:N0}" : "";
                _pesetasGain.color = new Color(1f, 0.95f, 0.6f, Mathf.Clamp01(2.5f - gt));
            }

            // Avisos
            string prompt = "";
            if (ws != null && ws.StealthTarget != null) prompt = "<color=#ffd84a>[V]</color>  ASESINATO SIGILOSO";
            else if (ws != null && ws.KickTarget != null) prompt = "<color=#ffd84a>[F]</color>  PATADA";
            else if (_interactor != null && _interactor.Focused != null) prompt = $"<color=#ffd84a>[E]</color>  Recoger {_interactor.Focused.Label}";
            else if (_interactor != null && _interactor.FocusedMerchant != null) prompt = "<color=#ffd84a>[E]</color>  Mercader: comprar, vender y mejorar";
            else if (_interactor != null && _interactor.FocusedNpc != null) prompt = $"<color=#ffd84a>[E]</color>  Hablar con {_interactor.FocusedNpc.displayName}";
            _prompt.text = prompt;
            if (ws != null && ws.KickTarget != null) _prompt.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 12f) * 0.06f);
            else _prompt.rectTransform.localScale = Vector3.one;
            _message.text = _interactor != null ? _interactor.Message ?? "" : "";

            // Banner
            float bt = Time.time - _bannerTime;
            float ba = bt < 0.3f ? bt / 0.3f : bt < 3.5f ? 1f : Mathf.Clamp01(1f - (bt - 3.5f) / 1f);
            _banner.color = new Color(0.95f, 0.2f, 0.15f, ba);

            // Jefe
            HordeWaveManager hm3 = HordeWaveManager.Instance;
            bool bossAlive = hm3 != null && hm3.Boss != null && !hm3.Boss.IsDead;
            _bossBack.gameObject.SetActive(bossAlive);
            if (bossAlive)
            {
                _bossFill.rectTransform.anchorMax = new Vector2(hm3.Boss.Normalized, 1f);
                _bossName.text = hm3.bossName;
            }

            // Oleadas
            HordeWaveManager hm2 = HordeWaveManager.Instance;
            if (hm2 != null)
            {
                if (hm2.Victory) _waveInfo.text = "PUEBLO SUPERADO · Bajas: " + hm2.Kills;
                else if (hm2.SiegeActive) _waveInfo.text = $"OLEADA {Mathf.Max(1, hm2.CurrentWave)}/{hm2.MaxWaves}   ·   ALDEANOS: {hm2.Remaining}   ·   BAJAS: {hm2.Kills}";
                else _waveInfo.text = "EXPLORA EL PUEBLO";
            }

            // Controles (desaparecen)
            float ct = Time.time - _startTime;
            _controls.color = new Color(1f, 1f, 1f, Mathf.Clamp01((controlsHintSeconds - ct) / 2f) * 0.85f);
        }
    }
}
