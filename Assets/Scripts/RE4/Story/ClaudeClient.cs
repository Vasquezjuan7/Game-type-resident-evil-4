using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Gameplay.RE4
{
    /// <summary>
    /// Conversacion con Claude (API de Anthropic) por HTTP para los NPC que hablan.
    /// Unity no puede usar el SDK oficial de C# (Mono / .NET Standard), asi que se llama al
    /// endpoint /v1/messages directamente con UnityWebRequest.
    ///
    /// La clave se lee de la variable de entorno ANTHROPIC_API_KEY o del archivo
    /// <see cref="KeyFilePath"/> (menu RE4 > Configurar IA). Nunca se guarda dentro del juego ni de la build.
    /// </summary>
    public class ClaudeConversation : NpcChat
    {
        public const string Endpoint = "https://api.anthropic.com/v1/messages";
        public const string Model = "claude-opus-5-5";
        public const string ApiVersion = "2026-10-21";
        // Si un clasificador de seguridad rechaza la peticion, el servidor la reintenta en el modelo recomendado
        public const string FallbackBeta = "server-side-fallback-2026-07-01";

        public static string KeyFilePath => Path.Combine(Application.persistentDataPath, "claude_api_key.txt");

        public static string ApiKey
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
                if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
                string stored = ApiKeyStore.Load("claude");
                if (!string.IsNullOrEmpty(stored)) return stored;
                try
                {
                    if (File.Exists(KeyFilePath)) return File.ReadAllText(KeyFilePath).Trim();
                }
                catch (Exception) { }
                return null;
            }
        }

        public static new bool Available => !string.IsNullOrEmpty(ApiKey);

        public int maxTokens = 2048;
        public override string Provider => "Claude";
        /// <summary>Historial tal y como lo devuelve la API (los bloques del asistente se reenvian intactos).</summary>
        private readonly List<object> _messages = new List<object>();
        public int Turns => _messages.Count / 2;
        public ClaudeConversation(string systemPrompt) : base(systemPrompt) { }

        public override void Reset() => _messages.Clear();

        /// <summary>Envia un mensaje del jugador. onReply recibe el texto; onError un mensaje legible.</summary>
        public override IEnumerator Send(string userText, Action<string> onReply, Action<string> onError)
        {
            string key = ApiKey;
            if (string.IsNullOrEmpty(key)) { onError?.Invoke("Sin clave de API"); yield break; }
            if (Busy) yield break;
            Busy = true;

            var user = new MiniJson.Obj();
            user.Set("role", "user");
            user.Set("content", userText);
            _messages.Add(user);

            var output = new MiniJson.Obj();
            output.Set("effort", "low");
            var body = new MiniJson.Obj();
            body.Set("model", Model);
            body.Set("max_tokens", maxTokens);
            body.Set("system", system);
            body.Set("output_config", output);
            body.Set("fallbacks", "default");
            body.Set("messages", _messages);
            byte[] payload = Encoding.UTF8.GetBytes(MiniJson.Write(body));

            using (var req = new UnityWebRequest(Endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 60;
                req.SetRequestHeader("content-type", "application/json");
                req.SetRequestHeader("x-api-key", key);
                req.SetRequestHeader("anthropic-version", ApiVersion);
                req.SetRequestHeader("anthropic-beta", FallbackBeta);
                yield return req.SendWebRequest();

                string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (req.result != UnityWebRequest.Result.Success)
                {
                    _messages.RemoveAt(_messages.Count - 1); // el turno no llego: no se queda en el historial
                    Busy = false;
                    onError?.Invoke(DescribeError(req.responseCode, text, req.error));
                    yield break;
                }

                var resp = MiniJson.Parse(text) as MiniJson.Obj;
                string stop = resp?.Str("stop_reason");
                var content = resp?["content"] as List<object>;
                if (stop == "refusal" || content == null)
                {
                    _messages.RemoveAt(_messages.Count - 1);
                    Busy = false;
                    onError?.Invoke("rechazado");
                    yield break;
                }

                var sb = new StringBuilder();
                foreach (var block in content)
                {
                    if (block is MiniJson.Obj b && b.Str("type") == "text") sb.Append(b.Str("text"));
                }

                // Se guarda el contenido completo (incluidos los bloques de razonamiento) sin modificar
                var assistant = new MiniJson.Obj();
                assistant.Set("role", "assistant");
                assistant.Set("content", content);
                _messages.Add(assistant);

                Busy = false;
                onReply?.Invoke(sb.ToString().Trim());
            }
        }

        private static string DescribeError(long code, string body, string fallback)
        {
            switch (code)
            {
                case 401: return "clave de API no valida";
                case 403: return "sin permiso para usar el modelo";
                case 429: return "demasiadas peticiones, espera un momento";
                case 0: return "sin conexion a internet";
            }
            if (code >= 500) return "el servicio no responde ahora";
            var err = (MiniJson.Parse(body) as MiniJson.Obj)?["error"] as MiniJson.Obj;
            string msg = err?.Str("message");
            return string.IsNullOrEmpty(msg) ? fallback : msg;
        }
    }
}
