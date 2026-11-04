using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gameplay.RE4
{
    public enum StoryStep
    {
        Arrive,
        FindWitness,
        Survive,
        Church,
        Escape,
        Finished
    }

    /// <summary>
    /// Historia del Capitulo 1 ("El pueblo"): objetivos, llamadas de radio con subtitulos,
    /// documentos de lore (J para releerlos) y pantalla de final de capitulo.
    /// Se apoya en el asedio de HordeWaveManager, en el NPC Tomas y en zonas <see cref="StoryTrigger"/>.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        public static StoryDirector Instance { get; private set; }

        [Header("Lugares (los coloca el menu RE4)")]
        public Transform churchPoint;
        public Transform gatePoint;
        public string witnessNpcId = "tomas";

        [Header("Objetos de la historia")]
        public ItemDefinition elenaPendant;

        public StoryStep Step { get; private set; } = StoryStep.Arrive;
        public List<ItemDefinition> Documents { get; } = new List<ItemDefinition>();

        private Canvas _canvas;
        private Text _objective, _subtitle, _chapter, _docTitle, _docBody, _docHint, _endTitle, _endBody;
        private Image _docPanel, _endPanel;
        private readonly Queue<KeyValuePair<string, string>> _radio = new Queue<KeyValuePair<string, string>>();
        private bool _radioPlaying;
        private bool _docOpen, _filesOpen;
        private int _docFrame;
        private float _prevTimeScale = 1f;
        private float _startTime;
        private bool _pendantSpawned;
        private float _objectiveFlash;

        private FirstPersonController Fps => FirstPersonController.Instance;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            NpcTalker.OnTalkedTo += OnTalked;
            StoryTrigger.OnEntered += OnZone;
            ItemPickup.OnCollected += OnCollected;
        }

        private void OnDisable()
        {
            NpcTalker.OnTalkedTo -= OnTalked;
            StoryTrigger.OnEntered -= OnZone;
            ItemPickup.OnCollected -= OnCollected;
        }

        private void Start()
        {
            Build();
            BuildVoices();
            _startTime = Time.time;
            // al cargar una partida guardada no se repite la introduccion (la historia la restaura el guardado)
            if (!DeadEcho.Core.GameSession.IsRestoring) StartCoroutine(Intro());
            // gritos de los aldeanos y frases del mercader listos de antemano (solo la primera vez cuesta)
            VillagerBarks.PrefetchAll();
        }

        // ================================================================ Texto para la IA

        /// <summary>Lo que ocurre ahora mismo, para que los NPC con IA hablen con coherencia.</summary>
        public static string SituationText()
        {
            var sb = new StringBuilder("Es de noche, hay niebla y el pueblo está a oscuras salvo por antorchas y hogueras. ");
            HordeWaveManager h = HordeWaveManager.Instance;
            StoryDirector s = Instance;
            if (h != null)
            {
                if (h.Victory) sb.Append("La campana de la iglesia ya sonó y los aldeanos se han retirado. ");
                else if (h.SiegeActive) sb.Append($"Los aldeanos están atacando a Leon (oleada {h.CurrentWave} de {h.MaxWaves}). ");
                else sb.Append("Los aldeanos todavía no han descubierto a Leon. ");
                if (h.Boss != null && !h.Boss.IsDead) sb.Append("Un gigante deforme (el Tyrant) anda suelto por el pueblo. ");
            }
            if (s != null)
            {
                sb.Append("Objetivo actual de Leon: ").Append(ObjectiveText(s.Step)).Append(". ");
                if (s.Documents.Count > 0) sb.Append($"Leon ha leído {s.Documents.Count} documentos del pueblo. ");
            }
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null && ph.Status == HealthStatus.Danger) sb.Append("Leon está malherido. ");
            return sb.ToString();
        }

        public static string ObjectiveText(StoryStep step)
        {
            switch (step)
            {
                case StoryStep.Arrive: return "Llega al pueblo de Valdeoscuro";
                case StoryStep.FindWitness: return "Encuentra a alguien que sepa dónde está Elena (se oyen golpes en una cabaña del oeste)";
                case StoryStep.Survive: return "Sobrevive al asedio de los aldeanos";
                case StoryStep.Church: return "Registra la iglesia del pueblo";
                case StoryStep.Escape: return "Escapa por el portón del bosque, al norte";
                default: return "Capítulo completado";
            }
        }

        // ================================================================ Flujo

        private IEnumerator Intro()
        {
            // la cinematica narrada de la Nueva partida va primero
            while (IntroCinematic.Playing) yield return null;
            yield return new WaitForSeconds(0.5f);
            ShowChapter("CAPÍTULO 1\n<size=34>Valdeoscuro, España</size>");
            SetObjective(StoryStep.Arrive);
            yield return new WaitForSeconds(3.5f);
            Radio("Ingrid", "Leon, aquí Ingrid. Te recibo con interferencias. ¿Ya estás cerca del pueblo?");
            Radio("Leon", "Lo tengo delante. No hay ni una luz encendida... y hay demasiado silencio.");
            Radio("Ingrid", "Elena Ibarra, diecinueve años, hija del embajador. Su última señal salió de ese pueblo hace tres días.");
            Radio("Ingrid", "La policía local no contesta. Encuéntrala y sácala de ahí. Y Leon... ten cuidado con los lugareños.");
        }

        private void Update()
        {
            HordeWaveManager h = HordeWaveManager.Instance;
            if (h != null && Step < StoryStep.Church)
            {
                if (h.SiegeActive && !h.Victory && Step != StoryStep.Survive)
                {
                    SetObjective(StoryStep.Survive);
                    Radio("Ingrid", "¡Leon! Te oigo disparar. ¿Qué está pasando?");
                    Radio("Leon", "Los aldeanos... no son normales. Vienen todos a por mí.");
                }
                if (h.Victory && Step == StoryStep.Survive) StartCoroutine(AfterSiege());
            }

            if (Step == StoryStep.Escape && gatePoint != null && PlayerHealth.Instance != null)
            {
                Vector3 d = PlayerHealth.Instance.transform.position - gatePoint.position;
                d.y = 0f;
                if (d.magnitude < 7f) Finish();
            }

            UpdateDocumentsInput();
            if (_objective != null)
            {
                _objectiveFlash = Mathf.MoveTowards(_objectiveFlash, 0f, Time.unscaledDeltaTime * 0.5f);
                _objective.color = Color.Lerp(new Color(0.95f, 0.92f, 0.8f, 0.9f), new Color(1f, 0.8f, 0.3f, 1f), _objectiveFlash);
            }
        }

        private IEnumerator AfterSiege()
        {
            Step = StoryStep.Church; // evita repetir
            yield return new WaitForSeconds(7f);
            SetObjective(StoryStep.Church);
            Radio("Ingrid", "La señal de Elena aparece un momento cerca de la iglesia. Puede que dejara algo allí.");
            SpawnPendant();
        }

        private void SpawnPendant()
        {
            if (_pendantSpawned || elenaPendant == null) return;
            _pendantSpawned = true;
            Vector3 p = churchPoint != null ? churchPoint.position : Vector3.forward * 100f;
            ItemPickup.Spawn(elenaPendant, 1, p + Vector3.up * 0.3f);
        }

        private void OnTalked(NpcTalker npc)
        {
            if (npc.npcId != witnessNpcId) return;
            if (Step <= StoryStep.FindWitness)
            {
                StartCoroutine(AfterWitness());
            }
        }

        private IEnumerator AfterWitness()
        {
            yield return new WaitForSecondsRealtime(0.2f);
            while (TalkOpen()) yield return null;
            HordeWaveManager h = HordeWaveManager.Instance;
            if (h != null && !h.SiegeActive)
            {
                SetObjective(StoryStep.Survive);
                Radio("Leon", "Ingrid, un viejo dice que se llevaron a Elena a la iglesia. Y que los aldeanos vendrán a por mí.");
                Radio("Ingrid", "Entonces prepárate. Si te atacan, aguanta hasta que se retiren.");
                yield return new WaitForSeconds(6f);
                // los aldeanos descubren a Leon: empieza el asedio
                foreach (var z in new List<ZombieAI>(ZombieAI.All)) if (z != null) { z.BecomeAlerted(true); break; }
            }
        }

        private static bool TalkOpen()
        {
            if (PlayerHealth.Instance == null) return false;
            TalkUI t = PlayerHealth.Instance.GetComponent<TalkUI>();
            return t != null && t.IsOpen;
        }

        private void OnZone(StoryTrigger zone)
        {
            switch (zone.zoneId)
            {
                case "pueblo":
                    if (Step == StoryStep.Arrive)
                    {
                        SetObjective(StoryStep.FindWitness);
                        Radio("Leon", "Estoy en el pueblo. Hay una hoguera encendida en la plaza... y gente trabajando a estas horas.");
                        Radio("Ingrid", "No llames la atención. Busca a alguien que pueda hablar.");
                    }
                    break;
                case "cabana":
                    if (Step <= StoryStep.FindWitness) Radio("Leon", "Alguien golpea desde dentro de esa cabaña...");
                    break;
                case "capilla":
                    Radio("Leon", "Una capilla vieja en mitad del bosque. Esta gente lleva mucho tiempo rezando a algo.");
                    break;
                case "aserradero":
                    Radio("Leon", "Un aserradero abandonado. Huele a sangre seca.");
                    break;
                case "bosque":
                    if (Step == StoryStep.Escape) Radio("Ingrid", "Sigue el camino del bosque hacia el norte. El portón no debe de estar lejos.");
                    break;
            }
        }

        private void OnCollected(ItemDefinition item)
        {
            if (item == null) return;
            if (item == elenaPendant && Step < StoryStep.Escape)
            {
                SetObjective(StoryStep.Escape);
                Radio("Leon", "Es el colgante de Elena. Tiene algo grabado: «Me llevan al castillo. Lo llaman el Padre».");
                Radio("Ingrid", "Hay un castillo pasando el bosque del norte. Sal del pueblo por el portón y te mando refuerzos.");
            }
        }

        /// <summary>Cambio de objetivo (lo usa el autoguardado de Dead Echo).</summary>
        public static event System.Action<StoryStep> OnStepChanged;

        public bool PendantSpawned => _pendantSpawned;

        /// <summary>
        /// Restaura la historia desde una partida guardada: paso, documentos leidos y colgante (si estaba en el suelo,
        /// vuelve a aparecer en la iglesia). No reproduce llamadas de radio.
        /// </summary>
        public void RestoreFromSave(StoryStep step, List<ItemDefinition> documents, bool pendantSpawned)
        {
            Build();
            Documents.Clear();
            if (documents != null) foreach (var d in documents) if (d != null && !Documents.Contains(d)) Documents.Add(d);
            Step = step;
            if (_objective != null) _objective.text = "<b>OBJETIVO</b>\n" + ObjectiveText(step);
            _objectiveFlash = 1f;
            if (step == StoryStep.Church && pendantSpawned) SpawnPendant();
            else _pendantSpawned = pendantSpawned || step >= StoryStep.Escape;
        }

        private void SetObjective(StoryStep step)
        {
            // solo se llama en transiciones reales (AfterSiege ya fija Step antes para no repetir)
            Step = step;
            OnStepChanged?.Invoke(step);
            if (_objective != null) _objective.text = "<b>OBJETIVO</b>\n" + ObjectiveText(step);
            _objectiveFlash = 1f;
            ProceduralAudio.PlayAt(ProceduralAudio.Click(700f, 50f), Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.3f, 0f, 5f);
        }

        private void Finish()
        {
            if (Step == StoryStep.Finished) return;
            Step = StoryStep.Finished;
            HordeWaveManager h = HordeWaveManager.Instance;
            float minutes = (Time.time - _startTime) / 60f;
            _endPanel.gameObject.SetActive(true);
            _endTitle.text = "FIN DEL CAPÍTULO 1";
            _endBody.text =
                $"Tiempo: {minutes:0} min\n" +
                $"Enemigos abatidos: {(h != null ? h.Kills : 0)}\n" +
                $"Documentos encontrados: {Documents.Count}\n" +
                $"Pesetas: {(AttacheCase.Instance != null ? AttacheCase.Instance.pesetas : 0):N0}\n\n" +
                "Leon deja atrás Valdeoscuro. Al otro lado del bosque, un castillo le espera...\n\n" +
                "<size=26>Continuará en el CAPÍTULO 2: El castillo</size>\n\n<size=22>Pulsa ENTER para volver a empezar</size>";
            Pause(true);
            StartCoroutine(WaitRestart());
        }

        private IEnumerator WaitRestart()
        {
            while (Keyboard.current == null || !Keyboard.current.enterKey.wasPressedThisFrame) yield return null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ================================================================ Radio y subtitulos

        /// <summary>Empieza a generar la voz de una frase ya (sin esperar a la cola de fondo).</summary>
        private void Warm(string speaker, string line)
        {
            if (NpcVoice.Enabled && NpcVoice.Get().Cached(line, VoiceFor(speaker)) == null)
                StartCoroutine(NpcVoice.Get().GetClip(line, VoiceFor(speaker), null));
        }

        public void Radio(string speaker, string line)
        {
            if (_radio.Count < 2) Warm(speaker, line);
            _radio.Enqueue(new KeyValuePair<string, string>(speaker, line));
            if (!_radioPlaying) StartCoroutine(PlayRadio());
        }

        private AudioSource _radioVoice, _leonVoice;

        private static string VoiceFor(string speaker) => speaker == "Leon" ? NpcVoice.VoiceLeon : NpcVoice.VoiceIngrid;

        private void BuildVoices()
        {
            if (_radioVoice != null) return;
            // Ingrid suena por radio: banda estrecha y algo de distorsion
            var radio = new GameObject("VozRadio");
            radio.transform.SetParent(transform, false);
            _radioVoice = radio.AddComponent<AudioSource>();
            _radioVoice.spatialBlend = 0f;
            _radioVoice.playOnAwake = false;
            radio.AddComponent<AudioHighPassFilter>().cutoffFrequency = 450f;
            radio.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3600f;
            radio.AddComponent<AudioDistortionFilter>().distortionLevel = 0.3f;

            var leon = new GameObject("VozLeon");
            leon.transform.SetParent(transform, false);
            _leonVoice = leon.AddComponent<AudioSource>();
            _leonVoice.spatialBlend = 0f;
            _leonVoice.playOnAwake = false;
        }

        private IEnumerator PlayRadio()
        {
            _radioPlaying = true;
            if (_radioVoice == null) BuildVoices();
            while (_radio.Count > 0)
            {
                var l = _radio.Dequeue();
                bool leon = l.Key == "Leon";
                // la siguiente frase se va generando mientras suena esta
                if (_radio.Count > 0) Warm(_radio.Peek().Key, _radio.Peek().Value);

                AudioClip clip = null;
                {
                    bool done = false;
                    StartCoroutine(NpcVoice.Get().GetClip(l.Value, VoiceFor(l.Key), c => { clip = c; done = true; }));
                    float wait = Time.unscaledTime + 8f;
                    while (!done && Time.unscaledTime < wait) yield return null;
                }

                if (!leon && Camera.main != null)
                {
                    ProceduralAudio.PlayAt(ProceduralAudio.Click(1800f, 25f), Camera.main.transform.position, 0.25f, 0f, 5f);
                }
                string color = leon ? "#9fd0ff" : "#9fffb0";
                // Opciones › Audio › Subtitulos y Accesibilidad › Tamaño de texto
                var opts = DeadEcho.Core.SettingsService.Current;
                _subtitle.fontSize = Mathf.RoundToInt(26 * opts.TextScale);
                _subtitle.text = opts.subtitles ? $"<color={color}><b>{(leon ? "Leon" : l.Key + " (radio)")}:</b></color> {l.Value}" : "";
                float t = Mathf.Clamp(l.Value.Length * 0.06f, 2.5f, 7f);
                if (clip != null)
                {
                    AudioSource src = leon ? _leonVoice : _radioVoice;
                    src.clip = clip;
                    src.volume = DeadEcho.Core.AudioChannels.AsVoice(src, leon ? 0.9f : 1f); // Opciones › Audio › Voces
                    src.Play();
                    t = Mathf.Max(clip.length + 0.35f, 1.2f);
                }
                float end = Time.unscaledTime + t;
                while (Time.unscaledTime < end) yield return null;
                _subtitle.text = "";
                yield return new WaitForSecondsRealtime(0.25f);
            }
            _radioPlaying = false;
        }

        private void ShowChapter(string text)
        {
            StartCoroutine(FadeChapter(text));
        }

        private IEnumerator FadeChapter(string text)
        {
            _chapter.text = text;
            float t = 0f;
            while (t < 5f)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 1f ? t : t > 4f ? 5f - t : 1f;
                _chapter.color = new Color(0.85f, 0.15f, 0.1f, a);
                yield return null;
            }
            _chapter.text = "";
        }

        // ================================================================ Documentos

        /// <summary>Llamado por el maletin al recoger un documento: se guarda en Archivos y se lee.</summary>
        public void AddDocument(ItemDefinition doc)
        {
            if (doc == null) return;
            if (!Documents.Contains(doc)) Documents.Add(doc);
            ShowDocument(doc);
        }

        public void ShowDocument(ItemDefinition doc)
        {
            if (_docPanel == null) Build();
            _filesOpen = false;
            _docOpen = true;
            _docFrame = Time.frameCount;
            _docTitle.text = doc.displayName;
            _docBody.text = doc.documentText;
            _docHint.text = "[E / ESC] Cerrar     [J] Archivos";
            _docPanel.gameObject.SetActive(true);
            Pause(true);
        }

        private void ShowFiles()
        {
            _filesOpen = true;
            _docOpen = false;
            _docTitle.text = "ARCHIVOS";
            var sb = new StringBuilder();
            if (Documents.Count == 0) sb.Append("Todavía no has encontrado ningún documento.");
            for (int i = 0; i < Documents.Count && i < 9; i++) sb.Append($"<b>[{i + 1}]</b>  {Documents[i].displayName}\n");
            _docBody.text = sb.ToString();
            _docHint.text = "[1-9] Leer     [J / ESC] Cerrar";
            _docPanel.gameObject.SetActive(true);
            Pause(true);
        }

        private void CloseDocs()
        {
            _docOpen = _filesOpen = false;
            _docPanel.gameObject.SetActive(false);
            Pause(false);
        }

        private void UpdateDocumentsInput()
        {
            var kb = Keyboard.current;
            if (kb == null || _docPanel == null || Step == StoryStep.Finished) return;
            if (_docOpen || _filesOpen)
            {
                if (Time.frameCount == _docFrame) return;
                if (kb.escapeKey.wasPressedThisFrame || (_docOpen && kb.eKey.wasPressedThisFrame) || (_filesOpen && kb.jKey.wasPressedThisFrame)) { CloseDocs(); return; }
                if (_docOpen && kb.jKey.wasPressedThisFrame) { ShowFiles(); return; }
                if (_filesOpen)
                {
                    for (int i = 0; i < Documents.Count && i < 9; i++)
                    {
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) { ShowDocument(Documents[i]); return; }
                    }
                }
                return;
            }
            if (kb.jKey.wasPressedThisFrame && (Fps == null || !Fps.InputLocked)) ShowFiles();
        }

        private bool _paused;

        private void Pause(bool on)
        {
            if (on == _paused) return;
            _paused = on;
            if (on)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                if (Fps != null) { Fps.PushInputLock(); Fps.LockCursor(false); }
            }
            else
            {
                Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
                if (Fps != null) { Fps.PopInputLock(); Fps.LockCursor(true); Fps.IgnoreEscapeFrame = Time.frameCount; }
            }
        }

        // ================================================================ Interfaz

        private void Build()
        {
            if (_canvas != null) return;
            _canvas = UIFactory.CreateCanvas("RE4_Historia", 40);
            Transform root = _canvas.transform;

            _objective = UIFactory.Label(root, "Objective", "", 21, new Color(0.95f, 0.92f, 0.8f, 0.9f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(620f, 90f));
            _subtitle = UIFactory.Label(root, "Subtitle", "", 26, Color.white, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1300f, 90f));
            _chapter = UIFactory.Label(root, "Chapter", "", 64, new Color(0.85f, 0.15f, 0.1f, 0f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 220f), FontStyle.Bold);

            _docPanel = UIFactory.Panel(root, "Document", new Color(0.83f, 0.77f, 0.64f, 0.98f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 860f));
            UIFactory.AddOutline(_docPanel, new Color(0.3f, 0.22f, 0.12f, 1f), 3f);
            _docTitle = UIFactory.Label(_docPanel.transform, "Title", "", 34, new Color(0.2f, 0.12f, 0.06f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(820f, 50f), FontStyle.Bold, false);
            _docBody = UIFactory.Label(_docPanel.transform, "Body", "", 23, new Color(0.15f, 0.1f, 0.06f), TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(800f, 680f), FontStyle.Normal, false);
            _docHint = UIFactory.Label(_docPanel.transform, "Hint", "", 18, new Color(0.3f, 0.22f, 0.12f), TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(820f, 30f), FontStyle.Bold, false);
            _docPanel.gameObject.SetActive(false);

            _endPanel = UIFactory.Stretch(root, "Ending", new Color(0f, 0f, 0f, 0.92f));
            _endTitle = UIFactory.Label(_endPanel.transform, "Title", "", 80, new Color(0.85f, 0.15f, 0.1f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 140f), FontStyle.Bold);
            _endBody = UIFactory.Label(_endPanel.transform, "Body", "", 30, new Color(0.9f, 0.9f, 0.88f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200f, 600f));
            _endPanel.gameObject.SetActive(false);
        }
    }
}
