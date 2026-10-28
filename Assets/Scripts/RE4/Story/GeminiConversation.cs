using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Gameplay.RE4
{
    /// <summary>
    /// Conversacion con Gemini (API de Google) por HTTP para los NPC.
    /// Si un modelo esta saturado (503), limitado (429) o retirado (404) se prueba el siguiente de la lista.
    /// </summary>
    public class GeminiConversation : NpcChat
    {
        public const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        /// <summary>Modelos de texto en orden de preferencia (rapidos y baratos para dialogo).</summary>
        // flash-lite responde en ~1 s (probado); los demas son respaldo si esta saturado
        public static readonly string[] Models = { "gemini-3.1-flash-lite", "gemini-3.5-flash", "gemini-3.8-flash", "gemini-flash-latest" };

        // respuestas de dialogo cortas: menos tokens = respuesta antes
        public int maxOutputTokens = 512;

        /// <summary>Segundos maximos por modelo y en total: pasado ese tiempo el NPC usa su respuesta escrita.</summary>
        public const int RequestTimeout = 12;
        public const float TotalTimeout = 25f;
        public override string Provider => "Gemini";

        private readonly List<object> _contents = new List<object>();
        private static int _preferred;
        private static readonly HashSet<string> NoThinkingConfig = new HashSet<string>();

        public GeminiConversation(string systemPrompt) : base(systemPrompt) { }

        public override void Reset() => _contents.Clear();

        public override IEnumerator Send(string userText, Action<string> onReply, Action<string> onError)
        {
            string key = GeminiKey;
            if (string.IsNullOrEmpty(key)) { onError?.Invoke("sin clave de Gemini"); yield break; }
            if (Busy) yield break;
            Busy = true;

            _contents.Add(Content("user", userText));
            string lastError = "sin respuesta";
            float deadline = Time.realtimeSinceStartup + TotalTimeout;

            for (int attempt = 0; attempt < Models.Length; attempt++)
            {
                // no encadenar esperas: si ya se paso el tiempo, contestar con el dialogo escrito
                if (Time.realtimeSinceStartup >= deadline) { lastError = "la IA tardó demasiado"; break; }
                int index = (_preferred + attempt) % Models.Length;
                string model = Models[index];
                string body = MiniJson.Write(Body(model));

                using (var req = new UnityWebRequest(BaseUrl + model + ":generateContent", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = Mathf.Clamp(Mathf.CeilToInt(deadline - Time.realtimeSinceStartup), 3, RequestTimeout);
                    req.SetRequestHeader("content-type", "application/json");
                    req.SetRequestHeader("x-goog-api-key", key);
                    yield return req.SendWebRequest();

                    long code = req.responseCode;
                    string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        lastError = Describe(code, text, req.error);
                        // Algunos modelos no aceptan el ajuste de razonamiento: repetir sin el
                        if (code == 400 && text.Contains("thinking") && NoThinkingConfig.Add(model)) { attempt--; continue; }
                        if (code == 503 || code == 429 || code == 404 || code >= 500 || code == 0) continue;
                        break;
                    }

                    var resp = MiniJson.Parse(text) as MiniJson.Obj;
                    var candidate = (resp?["candidates"] as List<object>)?.Count > 0 ? ((List<object>)resp["candidates"])[0] as MiniJson.Obj : null;
                    var content = candidate?["content"] as MiniJson.Obj;
                    var parts = content?["parts"] as List<object>;
                    var sb = new StringBuilder();
                    if (parts != null)
                    {
                        foreach (var p in parts)
                        {
                            if (!(p is MiniJson.Obj po)) continue;
                            if (po["thought"] is bool t && t) continue;
                            if (po.Str("text") != null) sb.Append(po.Str("text"));
                        }
                    }
                    string reply = sb.ToString().Trim();
                    if (reply.Length == 0)
                    {
                        lastError = "respuesta bloqueada (" + (candidate?.Str("finishReason") ?? "vacia") + ")";
                        break;
                    }

                    // El turno del modelo se guarda tal cual (incluidas las firmas de razonamiento)
                    var modelTurn = new MiniJson.Obj();
                    modelTurn.Set("role", "model");
                    modelTurn.Set("parts", parts);
                    _contents.Add(modelTurn);
                    _preferred = index;
                    Busy = false;
                    onReply?.Invoke(reply);
                    yield break;
                }
            }

            _contents.RemoveAt(_contents.Count - 1);
            Busy = false;
            onError?.Invoke(lastError);
        }

        private MiniJson.Obj Body(string model)
        {
            var sysPart = new MiniJson.Obj();
            sysPart.Set("text", system);
            var sys = new MiniJson.Obj();
            sys.Set("parts", new List<object> { sysPart });

            var gen = new MiniJson.Obj();
            gen.Set("maxOutputTokens", maxOutputTokens);
            if (!NoThinkingConfig.Contains(model))
            {
                // dialogo corto: poco razonamiento = respuesta rapida
                var thinking = new MiniJson.Obj();
                thinking.Set("thinkingLevel", "minimal");
                gen.Set("thinkingConfig", thinking);
            }

            var body = new MiniJson.Obj();
            body.Set("system_instruction", sys);
            body.Set("contents", _contents);
            body.Set("generationConfig", gen);
            return body;
        }

        public static MiniJson.Obj Content(string role, string text)
        {
            var part = new MiniJson.Obj();
            part.Set("text", text);
            var c = new MiniJson.Obj();
            c.Set("role", role);
            c.Set("parts", new List<object> { part });
            return c;
        }

        public static string Describe(long code, string body, string fallback)
        {
            switch (code)
            {
                case 400: return "petición no válida";
                case 401:
                case 403: return "clave de Gemini no válida o sin permiso";
                case 404: return "modelo no disponible";
                case 429: return "límite de uso alcanzado";
                case 503: return "Gemini está saturado ahora mismo";
                case 0: return "sin conexión a internet";
            }
            var err = (MiniJson.Parse(body) as MiniJson.Obj)?["error"] as MiniJson.Obj;
            string msg = err?.Str("message");
            return string.IsNullOrEmpty(msg) ? fallback : msg;
        }
    }
}
