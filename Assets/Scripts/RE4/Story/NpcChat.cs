using System;
using System.Collections;

namespace Gameplay.RE4
{
    /// <summary>
    /// Conversacion de un NPC con una IA. Se usa Gemini si hay clave de Gemini;
    /// si no, Claude; si no hay ninguna, el NPC usa sus dialogos escritos.
    /// </summary>
    public abstract class NpcChat
    {
        public string system;
        public bool Busy { get; protected set; }
        public abstract string Provider { get; }

        protected NpcChat(string systemPrompt)
        {
            system = systemPrompt;
        }

        public abstract void Reset();

        /// <summary>Envia un mensaje del jugador. onReply recibe el texto; onError un mensaje legible.</summary>
        public abstract IEnumerator Send(string userText, Action<string> onReply, Action<string> onError);

        private static string _geminiKey;
        private static bool _geminiLoaded;

        /// <summary>Clave de Gemini: variable de entorno GEMINI_API_KEY o archivo cifrado (menu RE4 > Configurar IA).</summary>
        public static string GeminiKey
        {
            get
            {
                if (!_geminiLoaded)
                {
                    _geminiLoaded = true;
                    string env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                    _geminiKey = !string.IsNullOrWhiteSpace(env) ? env.Trim() : ApiKeyStore.Load("gemini");
                }
                return _geminiKey;
            }
        }

        /// <summary>Vuelve a leer las claves (tras cambiarlas en el editor).</summary>
        public static void ReloadKeys() => _geminiLoaded = false;

        public static bool Available => !string.IsNullOrEmpty(GeminiKey) || ClaudeConversation.Available;

        public static string ProviderName => !string.IsNullOrEmpty(GeminiKey) ? "Gemini" : ClaudeConversation.Available ? "Claude" : "ninguna";

        public static NpcChat Create(string systemPrompt)
        {
            if (!string.IsNullOrEmpty(GeminiKey)) return new GeminiConversation(systemPrompt);
            if (ClaudeConversation.Available) return new ClaudeConversation(systemPrompt);
            return null;
        }
    }
}
