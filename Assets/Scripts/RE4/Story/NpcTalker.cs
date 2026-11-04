using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    [Serializable]
    public class ScriptedLine
    {
        [Tooltip("Pregunta que aparece como opcion numerada.")]
        public string question;
        [Tooltip("Palabras clave que, escritas por el jugador, eligen esta respuesta (sin IA).")]
        public string keywords;
        [TextArea(2, 6)] public string answer;
    }

    /// <summary>
    /// Personaje con el que se puede hablar [E]. Con clave de API conversa libremente usando Claude
    /// (personalidad y conocimientos definidos aqui); sin ella usa las respuestas escritas.
    /// </summary>
    public class NpcTalker : MonoBehaviour
    {
        public static readonly List<NpcTalker> All = new List<NpcTalker>();

        public string npcId = "tomas";
        public string displayName = "Tomás";
        public float talkRange = 3f;

        [Header("Voz (Gemini TTS)")]
        public string voiceName = NpcVoice.VoiceTomas;
        public float voicePitch = 0.97f;

        [Header("Personalidad para la IA")]
        [TextArea(6, 30)] public string persona = "";
        [TextArea(4, 30)] public string knowledge = "";

        [Header("Dialogo escrito (respaldo sin IA y opciones rapidas)")]
        [TextArea(2, 6)] public string greeting = "";
        public List<ScriptedLine> lines = new List<ScriptedLine>();
        [TextArea(2, 4)] public string unknownAnswer = "No sé de qué me hablas, forastero.";

        public bool HasTalked { get; private set; }
        public NpcChat Conversation { get; private set; }
        public AudioSource Voice { get; private set; }

        public static event Action<NpcTalker> OnTalkedTo;

        private void Awake()
        {
            Voice = gameObject.AddComponent<AudioSource>();
            Voice.spatialBlend = 0.85f;
            Voice.rolloffMode = AudioRolloffMode.Linear;
            Voice.minDistance = 2f;
            Voice.maxDistance = 25f;
            Voice.playOnAwake = false;
        }

        /// <summary>Dice una frase en voz alta (si hay voz disponible).</summary>
        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || Voice == null) return;
            NpcVoice.Get().Say(Voice, text, voiceName, 1f, voicePitch);
        }

        private void Start()
        {
            LoadModel();
            // la primera frase lista de antemano para que suene al instante
            NpcVoice.Get().Prefetch(greeting, voiceName);
        }

        // ------------------------------------------------------------ Modelo descargado

        [Header("Modelo (Resources)")]
        [Tooltip("Modelo en Resources que sustituye al cuerpo de la escena. Vacio en Tomas = RE4Modelos/Tomas/Tomas.")]
        public string modelResource = "";
        public float modelYaw = 0f;
        public float modelScale = 0.97f;

        /// <summary>
        /// Pone el modelo descargado (malla estatica con los brazos ya bajados, pies en el suelo)
        /// y oculta el cuerpo antiguo. Si no existe, se queda el que hubiera en la escena.
        /// </summary>
        private void LoadModel()
        {
            string res = modelResource;
            if (string.IsNullOrEmpty(res) && npcId == "tomas") res = "RE4Modelos/Tomas/Tomas";
            if (string.IsNullOrEmpty(res)) return;
            GameObject prefab = Resources.Load<GameObject>(res);
            if (prefab == null) return;

            foreach (Renderer old in GetComponentsInChildren<Renderer>())
                if (old.GetComponent<Light>() == null) old.enabled = false;

            GameObject model = Instantiate(prefab, transform);
            model.name = "Modelo_" + npcId;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            model.transform.localScale = Vector3.one * modelScale;
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);

            Material mat = new Material(Shader.Find("Standard")) { name = "Modelo_" + npcId };
            Texture2D color = Resources.Load<Texture2D>(res + "_Color");
            if (color != null) mat.mainTexture = color;
            mat.SetFloat("_Glossiness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            // Mirar al jugador cuando esta cerca
            Transform player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (player == null) return;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            if (to.magnitude < 8f && to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 2.5f);
        }

        public bool InRange(Vector3 playerPos)
        {
            Vector3 to = transform.position - playerPos;
            to.y = 0f;
            return to.magnitude <= talkRange;
        }

        public string SystemPrompt()
        {
            return
                $"Eres {displayName}, un personaje de un videojuego de terror y supervivencia ambientado en un pueblo rural de España. " +
                "Hablas con Leon S. Kennedy, agente del gobierno de EE. UU., en primera persona y siempre dentro del personaje.\n\n" +
                "PERSONALIDAD Y SITUACION:\n" + persona + "\n\n" +
                "LO QUE SABES (no inventes hechos que contradigan esto):\n" + knowledge + "\n\n" +
                "FORMA DE RESPONDER:\n" +
                "- Responde en español, con 1 a 3 frases cortas, como en un diálogo hablado de videojuego.\n" +
                "- No uses listas, ni markdown, ni acotaciones entre asteriscos.\n" +
                "- No sabes que eres un personaje de ficción ni una IA; si te preguntan por cosas del mundo real moderno, muéstrate confundido.\n" +
                "- Cada mensaje de Leon puede empezar con [Situación: ...]: es lo que ves y oyes ahora mismo; úsalo con naturalidad sin mencionar los corchetes.\n" +
                "- Puedes orientar a Leon sobre qué hacer, pero no conoces el futuro.";
        }

        public void EnsureConversation()
        {
            if (Conversation == null) Conversation = NpcChat.Create(SystemPrompt());
        }

        public void MarkTalked()
        {
            if (HasTalked) return;
            HasTalked = true;
            OnTalkedTo?.Invoke(this);
        }

        /// <summary>Respuesta escrita para lo que haya tecleado el jugador (sin IA).</summary>
        public string ScriptedAnswer(string playerText)
        {
            string t = playerText.ToLowerInvariant();
            foreach (var l in lines)
            {
                if (string.IsNullOrEmpty(l.keywords)) continue;
                foreach (var k in l.keywords.Split(','))
                {
                    string kw = k.Trim().ToLowerInvariant();
                    if (kw.Length > 0 && t.Contains(kw)) return l.answer;
                }
            }
            return unknownAnswer;
        }

        public void Talk(GameObject player)
        {
            TalkUI ui = player.GetComponent<TalkUI>();
            if (ui == null) ui = player.AddComponent<TalkUI>();
            ui.Open(this);
        }
    }
}
