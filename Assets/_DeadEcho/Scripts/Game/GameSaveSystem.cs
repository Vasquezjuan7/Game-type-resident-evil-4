using System;
using System.Collections;
using System.Collections.Generic;
using DeadEcho.Core;
using DeadEcho.UI;
using Gameplay;
using Gameplay.RE4;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadEcho.Game
{
    /// <summary>Objeto del maletin guardado (el objeto se identifica por su ItemDefinition.id).</summary>
    [Serializable]
    public class SavedEntry
    {
        public string item;
        public int count;
        public int x, y;
        public bool rotated;
        public int loadedAmmo;
        public int power, fireRate, reloadSpeed, capacity;
        public int quickSlot = -1;
    }

    /// <summary>Estado completo de una partida (se guarda como JSON dentro de la ranura).</summary>
    [Serializable]
    public class GameSnapshot
    {
        public int version = 1;
        public float px, py, pz, yaw;
        public float health;
        public int pesetas;
        public List<SavedEntry> entries = new List<SavedEntry>();
        public int currentWeapon = -1;
        public int storyStep;
        public List<string> documents = new List<string>();
        public bool pendantSpawned;
        public bool siegeWon;
        public int kills;
        public List<string> collected = new List<string>();
        public bool flashlight;
    }

    /// <summary>
    /// Guardado REAL de la partida del pueblo: posicion y orientacion de Leon, vida, maletin (objetos, posiciones,
    /// balas cargadas, mejoras y atajos), pesetas, arma equipada, paso de la historia, documentos leidos, colgante,
    /// asedio superado, enemigos abatidos y objetos del escenario ya recogidos.
    /// · Pausa › Guardar partida guarda en la ranura de la partida.
    /// · Autoguardado (Narrativa y Superviviente) al empezar y en cada cambio de objetivo.
    /// · No se puede guardar durante el asedio ni muerto (como en los survival horror clasicos).
    /// · Continuar / Cargar partida restauran todo al cargar la escena de juego.
    /// </summary>
    public class GameSaveSystem : MonoBehaviour
    {
        public static GameSaveSystem Instance { get; private set; }

        /// <summary>Se ha guardado una partida (mensaje para la interfaz).</summary>
        public static event Action<string> OnSaved;

        private readonly Dictionary<string, ItemPickup> _scenePickups = new Dictionary<string, ItemPickup>();
        private readonly HashSet<string> _collectedBefore = new HashSet<string>();
        private bool _inGame;
        private float _autosaveAt = -1f;

        private CanvasGroup _indicator;
        private TextMeshProUGUI _indicatorText;
        private Coroutine _indicatorRun;
        private RectTransform _spinner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("[DeadEcho_Guardado]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameSaveSystem>();
            Instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StoryDirector.OnStepChanged += OnStepChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StoryDirector.OnStepChanged -= OnStepChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            _inGame = false;
            _autosaveAt = -1f;
            _scenePickups.Clear();
            _collectedBefore.Clear();
            StopAllCoroutines();
            StartCoroutine(Setup());
        }

        private IEnumerator Setup()
        {
            // esperar a que todos los Start de la escena hayan corrido (maletin, armas, historia...)
            yield return null;
            yield return null;
            if (AttacheCase.Instance == null || PlayerHealth.Instance == null)
            {
                GameSession.ClearRestore();
                yield break;
            }
            _inGame = true;
            RegisterScenePickups();

            if (GameSession.IsRestoring)
            {
                GameSnapshot snap = null;
                try { snap = JsonUtility.FromJson<GameSnapshot>(GameSession.PendingSave); }
                catch (Exception e) { Debug.LogWarning("[DeadEcho] Partida guardada ilegible: " + e.Message); }
                GameSession.ClearRestore();
                if (snap != null) Apply(snap);
                ShowIndicator(Loc.IsEnglish ? "Game loaded" : "Partida cargada");
            }
            else if (GameSession.StartedFromMenu && GameSession.AutoSave)
            {
                // primer autoguardado para que Continuar funcione desde el principio
                yield return new WaitForSeconds(3f);
                SaveNow(true, out _);
            }
        }

        private void Update()
        {
            if (_spinner != null && _indicator != null && _indicator.alpha > 0f && !SettingsService.ReduceMotion)
                _spinner.Rotate(0f, 0f, -200f * Time.unscaledDeltaTime);
            if (!_inGame) return;
            if (Time.timeScale > 0f && PlayerHealth.Instance != null && !PlayerHealth.Instance.IsDead)
                GameSession.PlaySeconds += Time.unscaledDeltaTime;

            if (_autosaveAt > 0f && Time.unscaledTime >= _autosaveAt)
            {
                // si ahora mismo no se puede (asedio, menu abierto...), se reintenta despues
                if (CanSave(out _) && Time.timeScale > 0f)
                {
                    _autosaveAt = -1f;
                    SaveNow(true, out _);
                }
                else _autosaveAt = Time.unscaledTime + 3f;
            }
        }

        private void OnStepChanged(StoryStep step)
        {
            if (!_inGame || !GameSession.AutoSave) return;
            _autosaveAt = Time.unscaledTime + 1.5f;
        }

        // ------------------------------------------------------------ Guardar

        /// <summary>¿Se puede guardar ahora? (no en pleno asedio, ni muerto, ni fuera del juego)</summary>
        public static bool CanSave(out string reason)
        {
            reason = null;
            if (Instance == null || !Instance._inGame || AttacheCase.Instance == null || PlayerHealth.Instance == null)
            {
                reason = Loc.IsEnglish ? "You can only save during the game." : "Solo se puede guardar durante la partida.";
                return false;
            }
            if (PlayerHealth.Instance.IsDead)
            {
                reason = Loc.IsEnglish ? "You can't save now." : "No se puede guardar ahora.";
                return false;
            }
            HordeWaveManager h = HordeWaveManager.Instance;
            if (h != null && h.SiegeActive && !h.Victory)
            {
                reason = Loc.IsEnglish ? "You can't save during the siege." : "No se puede guardar durante el asedio.";
                return false;
            }
            if (StoryDirector.Instance != null && StoryDirector.Instance.Step == StoryStep.Finished)
            {
                reason = Loc.IsEnglish ? "The chapter is over." : "El capítulo ha terminado.";
                return false;
            }
            return true;
        }

        /// <summary>Guarda la partida en su ranura (o en la primera libre). Devuelve el mensaje para mostrar.</summary>
        public static bool SaveNow(bool auto, out string message)
        {
            if (!CanSave(out message))
            {
                if (auto) message = null;
                return false;
            }
            int slot = GameSession.Slot;
            if (slot < 0 || slot >= SaveService.SlotCount)
            {
                SaveService.Load();
                slot = SaveService.FirstEmptyOr(0);
                GameSession.Slot = slot;
            }

            GameSnapshot snap = Instance.Capture();
            StoryStep step = (StoryStep)snap.storyStep;
            var data = new DemoSlot
            {
                location = "Valdeoscuro · " + Place(step),
                playSeconds = Mathf.RoundToInt(GameSession.PlaySeconds),
                difficulty = GameSession.Difficulty,
                casePercent = Percent(step),
                siegeWon = snap.siegeWon,
                kills = snap.kills,
                game = JsonUtility.ToJson(snap)
            };
            SaveService.Write(slot, data);

            message = auto
                ? (Loc.IsEnglish ? "Autosave" : "Autoguardado")
                : (Loc.IsEnglish ? "Game saved in slot " : "Partida guardada en la ranura ") + (slot + 1);
            Instance.ShowIndicator(message);
            OnSaved?.Invoke(message);
            return true;
        }

        private GameSnapshot Capture()
        {
            var snap = new GameSnapshot();
            PlayerHealth ph = PlayerHealth.Instance;
            Transform p = ph.transform;
            snap.px = p.position.x;
            snap.py = p.position.y;
            snap.pz = p.position.z;
            snap.yaw = p.eulerAngles.y;
            snap.health = ph.currentHealth;

            AttacheCase ac = AttacheCase.Instance;
            snap.pesetas = ac.pesetas;
            WeaponSystem ws = FindAnyObjectByType<WeaponSystem>();
            for (int i = 0; i < ac.entries.Count; i++)
            {
                CaseEntry e = ac.entries[i];
                if (e == null || e.item == null) continue;
                var se = new SavedEntry
                {
                    item = e.item.id,
                    count = e.count,
                    x = e.position.x,
                    y = e.position.y,
                    rotated = e.rotated,
                    loadedAmmo = e.loadedAmmo
                };
                if (e.upgrades != null)
                {
                    se.power = e.upgrades.power;
                    se.fireRate = e.upgrades.fireRate;
                    se.reloadSpeed = e.upgrades.reloadSpeed;
                    se.capacity = e.upgrades.capacity;
                }
                for (int q = 0; q < ac.quickSlots.Length; q++) if (ac.quickSlots[q] == e) se.quickSlot = q;
                if (ws != null && ws.CurrentEntry == e) snap.currentWeapon = snap.entries.Count;
                snap.entries.Add(se);
            }

            StoryDirector sd = StoryDirector.Instance;
            if (sd != null)
            {
                snap.storyStep = (int)sd.Step;
                foreach (var d in sd.Documents) if (d != null) snap.documents.Add(d.id);
                snap.pendantSpawned = sd.PendantSpawned;
            }
            HordeWaveManager h = HordeWaveManager.Instance;
            if (h != null)
            {
                snap.siegeWon = h.Victory;
                snap.kills = h.Kills;
            }

            var collected = new HashSet<string>(_collectedBefore);
            foreach (var kv in _scenePickups) if (kv.Value == null) collected.Add(kv.Key);
            snap.collected.AddRange(collected);

            Flashlight fl = FindAnyObjectByType<Flashlight>();
            if (fl != null) snap.flashlight = fl.enabled && FlashlightOn(fl);
            return snap;
        }

        // ------------------------------------------------------------ Cargar

        /// <summary>Continuar / Cargar partida: prepara la sesion y carga la escena de juego.</summary>
        public static bool LoadSlot(int index)
        {
            SaveService.Load();
            if (index < 0 || index >= SaveService.SlotCount) return false;
            DemoSlot s = SaveService.Slots[index];
            if (!s.HasGame) return false;
            GameSession.BeginFromSave(index, s.difficulty, s.playSeconds, s.game, s.siegeWon, s.kills);
            SceneLoader.Load(SceneNames.Game);
            return true;
        }

        private void Apply(GameSnapshot snap)
        {
            Dictionary<string, ItemDefinition> items = ItemLookup();

            // ---- maletin
            AttacheCase ac = AttacheCase.Instance;
            ac.entries.Clear();
            for (int q = 0; q < ac.quickSlots.Length; q++) ac.quickSlots[q] = null;
            var created = new List<CaseEntry>();
            foreach (SavedEntry se in snap.entries)
            {
                if (se == null || string.IsNullOrEmpty(se.item) || !items.TryGetValue(se.item, out ItemDefinition def))
                {
                    if (se != null) Debug.LogWarning("[DeadEcho] Objeto guardado desconocido: " + se.item);
                    created.Add(null);
                    continue;
                }
                var e = new CaseEntry
                {
                    item = def,
                    count = Mathf.Max(1, se.count),
                    position = new Vector2Int(se.x, se.y),
                    rotated = se.rotated,
                    loadedAmmo = se.loadedAmmo,
                    upgrades = new WeaponUpgrades { power = se.power, fireRate = se.fireRate, reloadSpeed = se.reloadSpeed, capacity = se.capacity }
                };
                ac.entries.Add(e);
                if (se.quickSlot >= 0 && se.quickSlot < ac.quickSlots.Length) ac.quickSlots[se.quickSlot] = e;
                created.Add(e);
            }
            ac.pesetas = Mathf.Max(0, snap.pesetas);
            ac.NotifyChanged();
            WeaponSystem ws = FindAnyObjectByType<WeaponSystem>();
            if (ws != null && snap.currentWeapon >= 0 && snap.currentWeapon < created.Count && created[snap.currentWeapon] != null)
                ws.Equip(created[snap.currentWeapon], true);

            // ---- Leon
            PlayerHealth ph = PlayerHealth.Instance;
            var cc = ph.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ph.transform.SetPositionAndRotation(new Vector3(snap.px, snap.py, snap.pz), Quaternion.Euler(0f, snap.yaw, 0f));
            if (cc != null) cc.enabled = true;
            ph.currentHealth = Mathf.Clamp(snap.health, 1f, ph.maxHealth);

            // ---- historia
            if (StoryDirector.Instance != null)
            {
                var docs = new List<ItemDefinition>();
                foreach (string id in snap.documents) if (id != null && items.TryGetValue(id, out ItemDefinition d)) docs.Add(d);
                StoryDirector.Instance.RestoreFromSave((StoryStep)snap.storyStep, docs, snap.pendantSpawned);
            }

            // ---- objetos del escenario ya recogidos
            foreach (string key in snap.collected)
            {
                _collectedBefore.Add(key);
                if (_scenePickups.TryGetValue(key, out ItemPickup p) && p != null) Destroy(p.gameObject);
            }

            Flashlight fl = FindAnyObjectByType<Flashlight>();
            if (fl != null && snap.flashlight != FlashlightOn(fl)) fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
        }

        private static Dictionary<string, ItemDefinition> ItemLookup()
        {
            var d = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition it in Resources.FindObjectsOfTypeAll<ItemDefinition>())
                if (it != null && !string.IsNullOrEmpty(it.id) && !d.ContainsKey(it.id)) d.Add(it.id, it);
            return d;
        }

        private static bool FlashlightOn(Flashlight fl)
        {
            Light l = fl.GetComponentInChildren<Light>(true);
            return l != null && l.enabled && l.gameObject.activeInHierarchy;
        }

        /// <summary>Objetos colocados en el escenario al cargar la escena, con una clave estable (objeto + posicion).</summary>
        private void RegisterScenePickups()
        {
            var used = new Dictionary<string, int>();
            foreach (ItemPickup p in ItemPickup.All)
            {
                if (p == null || p.item == null) continue;
                Vector3 pos = p.transform.position;
                string key = $"{p.item.id}@{Mathf.RoundToInt(pos.x * 2f)},{Mathf.RoundToInt(pos.y * 2f)},{Mathf.RoundToInt(pos.z * 2f)}";
                used.TryGetValue(key, out int n);
                used[key] = n + 1;
                if (n > 0) key += "#" + n;
                _scenePickups[key] = p;
            }
        }

        // ------------------------------------------------------------ Textos de la ranura

        private static string Place(StoryStep step)
        {
            bool en = Loc.IsEnglish;
            switch (step)
            {
                case StoryStep.Arrive: return en ? "South road" : "Camino del sur";
                case StoryStep.FindWitness: return en ? "Village square" : "Plaza del pueblo";
                case StoryStep.Survive: return en ? "Village square" : "Plaza del pueblo";
                case StoryStep.Church: return en ? "Church" : "Iglesia";
                case StoryStep.Escape: return en ? "North forest" : "Bosque del norte";
                default: return en ? "North gate" : "Portón del norte";
            }
        }

        private static int Percent(StoryStep step)
        {
            switch (step)
            {
                case StoryStep.Arrive: return 5;
                case StoryStep.FindWitness: return 20;
                case StoryStep.Survive: return 35;
                case StoryStep.Church: return 60;
                case StoryStep.Escape: return 85;
                default: return 100;
            }
        }

        // ------------------------------------------------------------ Indicador "Partida guardada"

        private void ShowIndicator(string text)
        {
            if (_indicator == null)
            {
                UITheme th = UITheme.Current;
                Canvas c = DEBuild.Canvas("Canvas_Guardado", 880, transform);
                RectTransform rt = UIFx.Rect(c.transform, "Indicador");
                UIFx.Place(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(560f, 48f));
                _indicator = rt.gameObject.AddComponent<CanvasGroup>();
                _indicator.blocksRaycasts = false;
                _indicator.alpha = 0f;
                UnityEngine.UI.Image ring = UIFx.Image(rt, "Icono", th.aro, th.huesoClaro);
                UIFx.Place(ring.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                _spinner = ring.rectTransform;
                _indicatorText = UIFx.Text(rt, "Texto", "", th.rajdhaniSemiBold, 24f, th.huesoClaro, TextAlignmentOptions.Right, 3f);
                UIFx.Stretch(_indicatorText.rectTransform, 0f, 44f);
            }
            _indicatorText.text = text.ToUpperInvariant();
            if (_indicatorRun != null) StopCoroutine(_indicatorRun);
            _indicatorRun = StartCoroutine(IndicatorRoutine());
        }

        private IEnumerator IndicatorRoutine()
        {
            yield return UIFx.Tween(0.25f, k => _indicator.alpha = Mathf.Max(_indicator.alpha, k));
            yield return new WaitForSecondsRealtime(2f);
            yield return UIFx.Tween(0.5f, k => _indicator.alpha = 1f - k);
            _indicatorRun = null;
        }
    }
}
