using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gameplay.RE4
{
    /// <summary>
    /// Ventana de conversacion con un NPC. Se escribe libremente (ENTER envia) o se elige una pregunta con 1-4.
    /// Con IA disponible responde Claude; si no, las respuestas escritas del NPC. Pausa el juego.
    /// </summary>
    public class TalkUI : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        private NpcTalker _npc;
        private FirstPersonController _fps;
        private Canvas _canvas;
        private Text _name, _log, _input, _options, _status;
        private readonly List<string> _history = new List<string>();
        private readonly StringBuilder _typed = new StringBuilder();
        private float _prevTimeScale = 1f;
        private int _openFrame;
        private bool _waiting;

        private void Awake()
        {
            _fps = GetComponent<FirstPersonController>();
        }

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("RE4_Conversacion", 70);
            Transform root = _canvas.transform;
            UIFactory.Stretch(root, "Dim", new Color(0f, 0f, 0f, 0.55f));
            Image panel = UIFactory.Panel(root, "Panel", new Color(0.05f, 0.05f, 0.07f, 0.95f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(1400f, 560f));
            UIFactory.AddOutline(panel, new Color(0.6f, 0.45f, 0.2f, 0.9f), 2f);
            Transform p = panel.transform;
            _name = UIFactory.Label(p, "Name", "", 34, new Color(1f, 0.85f, 0.45f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -16f), new Vector2(900f, 46f), FontStyle.Bold);
            _status = UIFactory.Label(p, "Status", "", 18, new Color(0.6f, 0.8f, 1f), TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(600f, 30f));
            _log = UIFactory.Label(p, "Log", "", 23, new Color(0.92f, 0.92f, 0.9f), TextAnchor.LowerLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -70f), new Vector2(1340f, 290f));
            _log.verticalOverflow = VerticalWrapMode.Truncate;
            _options = UIFactory.Label(p, "Options", "", 20, new Color(0.85f, 0.8f, 0.6f), TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 70f), new Vector2(1340f, 120f));
            Image inputBack = UIFactory.Panel(p, "InputBack", new Color(1f, 1f, 1f, 0.06f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 16f), new Vector2(1360f, 44f));
            _input = UIFactory.Label(inputBack.transform, "Input", "", 22, Color.white, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, 0f));
            _canvas.gameObject.SetActive(false);
        }

        public void Open(NpcTalker npc)
        {
            if (IsOpen || npc == null) return;
            if (_fps != null && _fps.InputLocked) return;
            if (_canvas == null) Build();
            _npc = npc;
            IsOpen = true;
            _openFrame = Time.frameCount;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_fps != null)
            {
                _fps.PushInputLock();
                _fps.LockCursor(false);
            }
            NpcVoice.HoldPrefetch = true;
            _history.Clear();
            _typed.Clear();
            _name.text = npc.displayName.ToUpperInvariant();
            bool ai = NpcChat.Available;
            string voz = NpcVoice.Enabled ? " · con voz" : "";
            _status.text = ai ? $"IA activa ({NpcChat.ProviderName}){voz} · escribe lo que quieras" : "Sin IA (configúrala en el menú RE4) · elige una pregunta";
            npc.EnsureConversation();
            Say(npc.displayName, npc.greeting);
            RefreshOptions();
            _canvas.gameObject.SetActive(true);
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnText;
            npc.MarkTalked();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (_npc != null && _npc.Voice != null) _npc.Voice.Stop();
            IsOpen = false;
            NpcVoice.HoldPrefetch = false;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
            if (_fps != null)
            {
                _fps.PopInputLock();
                _fps.LockCursor(true);
                _fps.IgnoreEscapeFrame = Time.frameCount;
            }
            _canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
        }

        private void OnText(char c)
        {
            if (!IsOpen || Time.frameCount == _openFrame || _waiting) return;
            if (char.IsControl(c)) return;
            if (_typed.Length < 220) _typed.Append(c);
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kb.backspaceKey.wasPressedThisFrame && _typed.Length > 0) _typed.Length--;

            // Preguntas rapidas solo si no se esta escribiendo
            if (_typed.Length == 0 && !_waiting)
            {
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < keys.Length && i < _npc.lines.Count; i++)
                {
                    if (kb[keys[i]].wasPressedThisFrame) Ask(_npc.lines[i].question, _npc.lines[i].answer);
                }
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && _typed.Length > 0 && !_waiting)
            {
                string text = _typed.ToString().Trim();
                _typed.Clear();
                if (text.Length > 0) Ask(text, null);
            }

            string caret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "_" : " ";
            _input.text = _waiting ? $"<color=#999999>{_npc.displayName} está pensando...</color>"
                                   : (_typed.Length > 0 ? _typed.ToString() : "<color=#777777>Escribe y pulsa ENTER...</color>") + caret;
        }

        /// <summary>El NPC contesta en voz alta cuando Leon termina (su voz se va generando mientras tanto).</summary>
        private System.Collections.IEnumerator SpeakAfterLeon(NpcTalker npc, string text)
        {
            if (!NpcVoice.Enabled) yield break;
            AudioClip clip = null;
            bool done = false;
            StartCoroutine(NpcVoice.Get().GetClip(text, npc.voiceName, c => { clip = c; done = true; }));
            float t0 = Time.realtimeSinceStartup;
            while (!done && Time.realtimeSinceStartup - t0 < 25f) yield return null;
            // no pisar a Leon si esta hablando (ya no se espera a que se genere su voz)
            LeonVoice leon = LeonVoice.Instance;
            while (leon != null && leon.IsSpeaking && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            if (clip == null || npc == null || npc.Voice == null || !IsOpen) yield break;
            npc.Voice.Stop();
            npc.Voice.clip = clip;
            npc.Voice.pitch = npc.voicePitch;
            npc.Voice.Play();
        }

        private void Ask(string question, string scriptedAnswer)
        {
            Say("Leon", question);
            // Leon solo pone voz a las preguntas rapidas (se repiten y quedan guardadas); lo escrito a mano
            // no se sintetiza para no gastar el limite de Gemini antes de la respuesta del NPC
            if (scriptedAnswer != null && LeonVoice.Instance != null) LeonVoice.Instance.SayIfCached(question);
            if (NpcChat.Available && _npc.Conversation != null)
            {
                _waiting = true;
                string msg = $"[Situación: {StoryDirector.SituationText()}]\n{question}";
                StartCoroutine(_npc.Conversation.Send(msg,
                    reply =>
                    {
                        _waiting = false;
                        Say(_npc.displayName, reply);
                    },
                    error =>
                    {
                        _waiting = false;
                        _status.text = "IA no disponible (" + error + ") · respuesta escrita";
                        Say(_npc.displayName, scriptedAnswer ?? _npc.ScriptedAnswer(question));
                    }));
            }
            else
            {
                Say(_npc.displayName, scriptedAnswer ?? _npc.ScriptedAnswer(question));
            }
        }

        private void Say(string who, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string color = who == "Leon" ? "#9fd0ff" : "#ffd27a";
            _history.Add($"<color={color}><b>{who}:</b></color> {text}");
            while (_history.Count > 7) _history.RemoveAt(0);
            _log.text = string.Join("\n\n", _history);
            if (who != "Leon" && _npc != null) StartCoroutine(SpeakAfterLeon(_npc, text));
            if (who != "Leon" && _fps != null) ProceduralAudio.PlayAt(ProceduralAudio.Click(420f, 30f), _npc.transform.position + Vector3.up * 1.6f, 0.25f, 0.1f, 10f);
        }

        private void RefreshOptions()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _npc.lines.Count && i < 5; i++)
            {
                sb.Append($"<b>[{i + 1}]</b> {_npc.lines[i].question}    ");
                if (i == 2) sb.Append('\n');
            }
            sb.Append("\n<b>[ESC]</b> Terminar");
            _options.text = sb.ToString();
        }
    }
}
