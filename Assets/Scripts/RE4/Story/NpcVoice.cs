using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Gameplay.RE4
{
    /// <summary>
    /// Voces habladas de los personajes con la sintesis de voz de Gemini (TTS).
    /// Cada frase se genera una sola vez y se guarda como .wav en la carpeta de datos del usuario,
    /// asi que las siguientes veces suena al instante y sin coste. Sin clave de Gemini no hay voz (solo subtitulos).
    /// </summary>
    public class NpcVoice : MonoBehaviour
    {
        /// <summary>Modelos de voz en orden de preferencia.</summary>
        public static readonly string[] TtsModels = { "gemini-3.8-flash-tts", "gemini-3.8-flash-lite-tts", "gemini-3.1-flash-tts-preview", "gemini-2.5-flash-preview-tts" };

        // Voces de Gemini asignadas a cada personaje
        public const string VoiceTomas = "Algenib";     // grave y rasposa: un pastor viejo
        public const string VoiceIngrid = "Kore";       // firme: la agente por radio
        public const string VoiceLeon = "Alnilam";      // firme y seca
        public const string VoiceMerchant = "Charon";   // pausada, misteriosa
        public static readonly string[] VillagerMale = { "Fenrir", "Algenib", "Orus", "Umbriel" };
        public static readonly string[] VillagerFemale = { "Gacrux", "Leda", "Despina" };

        private static NpcVoice _instance;
        private readonly Dictionary<string, AudioClip> _memory = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _pending = new HashSet<string>();
        private readonly Queue<KeyValuePair<string, string>> _prefetchHigh = new Queue<KeyValuePair<string, string>>();
        private readonly Queue<KeyValuePair<string, string>> _prefetchLow = new Queue<KeyValuePair<string, string>>();
        private static int _model;
        private bool _prefetching;

        // Limite de uso (la clave gratuita admite pocas peticiones por minuto)
        private const float DirectInterval = 2.5f;
        private const float PrefetchInterval = 7f;
        private float _nextAllowed;
        private int _directActive;
        public static float BackoffUntil { get; private set; }

        /// <summary>
        /// Mientras hay una conversacion abierta no se generan voces en segundo plano: la clave gratuita
        /// admite pocas peticiones por minuto y deben quedar libres para la respuesta del NPC.
        /// </summary>
        public static bool HoldPrefetch { get; set; }

        /// <summary>
        /// Usar la sintesis de Gemini. Desactivada: la clave gratuita solo da para unas pocas frases al dia
        /// (despues todo quedaba en silencio). Se usan las voces de Windows, sin internet ni limite.
        /// </summary>
        public static bool UseGeminiTts = false;

        /// <summary>
        /// Generar de antemano los gritos de los aldeanos y las frases de combate de Leon. Con Gemini agotaba
        /// el cupo diario; con las voces de Windows es gratis, asi que se hace.
        /// </summary>
        public static bool AllowBackgroundVoices => !UseGeminiTts && (WindowsVoice.Available || PiperTts.Available);

        /// <summary>Hay voz: la de Windows, o Gemini si esta activado y hay clave.</summary>
        public static bool Enabled => WindowsVoice.Available || PiperTts.Available || (UseGeminiTts && !string.IsNullOrEmpty(NpcChat.GeminiKey));

        public static string CacheFolder => Path.Combine(Application.persistentDataPath, "voces");

        public static NpcVoice Get()
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<NpcVoice>();
                if (_instance == null) _instance = new GameObject("NpcVoice").AddComponent<NpcVoice>();
            }
            return _instance;
        }

        private static string Hash(string voice, string text)
        {
            using (var md5 = MD5.Create())
            {
                byte[] h = md5.ComputeHash(Encoding.UTF8.GetBytes(voice + "|" + text));
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Identificador de la voz de Windows de una frase (cambia si cambia el perfil del personaje).</summary>
        private static string WinId(string voice, string text)
        {
            WindowsVoice.Profile p = WindowsVoice.ProfileFor(voice);
            return "win_" + Hash(p.voice + p.pitch + p.rate, text);
        }

        /// <summary>Identificador de la voz de Piper de una frase (cambia si cambia el perfil del personaje).</summary>
        private static string PiperId(string voice, string text) => "piper_" + Hash(PiperTts.Key(voice), text);

        /// <summary>Clip ya disponible (memoria o disco) o null.</summary>
        public AudioClip Cached(string text, string voice)
        {
            // frases incluidas en el juego (ElevenLabs, Resources/RE4Audio/Frases): siempre primero
            AudioClip bundled = BundledAudio.Line(BundledAudio.FolderForVoice(voice), text);
            if (bundled != null) return bundled;
            // voz de Piper (la misma que las frases incluidas): antes que la de Windows
            if (!UseGeminiTts && PiperTts.Has(voice))
            {
                AudioClip pc = Load(PiperId(voice, text));
                if (pc != null) return pc;
            }
            // con Gemini desactivado no se mezclan sus voces viejas con las de Windows (cada personaje suena siempre igual)
            if (UseGeminiTts)
            {
                AudioClip g = Load(Hash(voice, text));
                if (g != null) return g;
            }
            return Load(WinId(voice, text));
        }

        private AudioClip Load(string id)
        {
            if (_memory.TryGetValue(id, out AudioClip c)) return c;
            string file = Path.Combine(CacheFolder, id + ".wav");
            if (!File.Exists(file)) return null;
            try
            {
                c = WavToClip(File.ReadAllBytes(file), id);
                if (c != null) _memory[id] = c;
                return c;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Obtiene la voz de una frase (de la cache o generandola). done recibe null si no se pudo.</summary>
        public IEnumerator GetClip(string text, string voice, Action<AudioClip> done)
        {
            if (string.IsNullOrWhiteSpace(text)) { done?.Invoke(null); yield break; }
            AudioClip cached = Cached(text, voice); // las frases incluidas suenan aunque no haya voz de Windows/Gemini
            if (cached != null) { done?.Invoke(cached); yield break; }
            if (!Enabled) { done?.Invoke(null); yield break; }

            bool piper = !UseGeminiTts && PiperTts.Has(voice);
            string id = UseGeminiTts ? Hash(voice, text) : piper ? PiperId(voice, text) : WinId(voice, text);
            // otra peticion ya esta generando esta frase: esperar a que acabe
            if (_pending.Contains(id))
            {
                float until = Time.realtimeSinceStartup + 40f;
                while (_pending.Contains(id) && Time.realtimeSinceStartup < until) yield return null;
                done?.Invoke(Cached(text, voice));
                yield break;
            }
            _pending.Add(id);
            bool direct = !_prefetchRunning;
            if (direct) _directActive++;

            AudioClip result = null;
            if (UseGeminiTts && !string.IsNullOrEmpty(NpcChat.GeminiKey))
                yield return GeminiTts(text, voice, id, direct, c => result = c);

            // Piper: voz neuronal sin internet, la misma que las frases incluidas en el juego
            if (result == null && piper)
            {
                string file = Path.Combine(CacheFolder, id + ".wav");
                bool ok = false;
                yield return PiperTts.Synthesize(text, voice, file, r => ok = r);
                if (ok)
                {
                    try
                    {
                        result = WavToClip(File.ReadAllBytes(file), id);
                        if (result != null) _memory[id] = result;
                    }
                    catch (Exception) { result = null; }
                }
            }

            // Voz de Windows: sin internet ni limite (tambien de respaldo si Gemini o Piper fallan)
            if (result == null && WindowsVoice.Available)
            {
                string wid = WinId(voice, text);
                string file = Path.Combine(CacheFolder, wid + ".wav");
                bool ok = false;
                yield return WindowsVoice.Synthesize(text, voice, file, r => ok = r);
                if (ok && File.Exists(file))
                {
                    try
                    {
                        result = WavToClip(File.ReadAllBytes(file), wid);
                        if (result != null) _memory[wid] = result;
                    }
                    catch (Exception) { result = null; }
                }
            }

            _pending.Remove(id);
            if (direct) _directActive--;
            done?.Invoke(result);
        }

        /// <summary>Sintesis con Gemini (de calidad, pero la clave gratuita se agota enseguida).</summary>
        private IEnumerator GeminiTts(string text, string voice, string id, bool direct, Action<AudioClip> done)
        {
            // respetar el ritmo de peticiones; si Google pidio esperar (429), no insistir ahora
            while (Time.realtimeSinceStartup < _nextAllowed)
            {
                if (direct && Time.realtimeSinceStartup < BackoffUntil) break;
                yield return null;
            }
            if (Time.realtimeSinceStartup < BackoffUntil) { done(null); yield break; }
            _nextAllowed = Time.realtimeSinceStartup + DirectInterval;

            AudioClip result = null;
            float deadline = Time.realtimeSinceStartup + 25f; // mas tarde ya no tiene sentido que hable
            for (int attempt = 0; attempt < TtsModels.Length && result == null && Time.realtimeSinceStartup < deadline; attempt++)
            {
                int index = (_model + attempt) % TtsModels.Length;
                using (var req = new UnityWebRequest(GeminiConversation.BaseUrl + TtsModels[index] + ":generateContent", "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Write(TtsBody(text, voice))));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = 20;
                    req.SetRequestHeader("content-type", "application/json");
                    req.SetRequestHeader("x-goog-api-key", NpcChat.GeminiKey);
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        long code = req.responseCode;
                        if (code == 401 || code == 403) break; // clave no valida: no insistir
                        if (code == 429)
                        {
                            BackoffUntil = Time.realtimeSinceStartup + RetryDelay(req.downloadHandler != null ? req.downloadHandler.text : "");
                            Debug.Log($"[RE4] Voces: límite de Gemini alcanzado, se reanudan en {BackoffUntil - Time.realtimeSinceStartup:0} s.");
                            break;
                        }
                        continue;
                    }
                    byte[] audio = ExtractAudio(req.downloadHandler.text, out string mime);
                    if (audio == null) continue;
                    // algunos modelos devuelven PCM crudo (audio/L16): se le pone cabecera WAV
                    byte[] wav = audio.Length > 12 && audio[0] == 'R' && audio[1] == 'I' && audio[2] == 'F' && audio[3] == 'F' ? audio : PcmToWav(audio, RateFromMime(mime));
                    try
                    {
                        Directory.CreateDirectory(CacheFolder);
                        File.WriteAllBytes(Path.Combine(CacheFolder, id + ".wav"), wav);
                    }
                    catch (Exception) { }
                    result = WavToClip(wav, id);
                    if (result != null)
                    {
                        _memory[id] = result;
                        _model = index;
                    }
                }
            }
            done(result);
        }

        /// <summary>Segundos que pide esperar Google en un 429 ("retryDelay": "37s"); 60 s si no lo dice.</summary>
        private static float RetryDelay(string body)
        {
            int i = body.IndexOf("retryDelay", StringComparison.Ordinal);
            if (i >= 0)
            {
                int q = body.IndexOf('"', body.IndexOf(':', i) + 1);
                int end = q >= 0 ? body.IndexOf('s', q) : -1;
                if (q >= 0 && end > q && float.TryParse(body.Substring(q + 1, end - q - 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float sec))
                    return Mathf.Clamp(sec + 2f, 5f, 600f);
            }
            return 60f;
        }

        /// <summary>
        /// Genera en segundo plano frases que se usaran luego. low = gritos de aldeanos (van despues de
        /// las frases de personajes). Va despacio para no agotar el limite de la clave gratuita.
        /// </summary>
        public void Prefetch(string text, string voice, bool low = false)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(text) || Cached(text, voice) != null) return;
            (low ? _prefetchLow : _prefetchHigh).Enqueue(new KeyValuePair<string, string>(text, voice));
            if (!_prefetching) StartCoroutine(RunPrefetch());
        }

        private bool _prefetchRunning;

        private IEnumerator RunPrefetch()
        {
            _prefetching = true;
            while (_prefetchHigh.Count > 0 || _prefetchLow.Count > 0)
            {
                // las frases que hacen falta ya (radio, conversacion) tienen prioridad
                while (_directActive > 0 || HoldPrefetch || (UseGeminiTts && Time.realtimeSinceStartup < BackoffUntil)) yield return new WaitForSecondsRealtime(0.5f);
                var p = _prefetchHigh.Count > 0 ? _prefetchHigh.Dequeue() : _prefetchLow.Dequeue();
                if (Cached(p.Key, p.Value) != null) continue;
                _prefetchRunning = true;
                yield return GetClip(p.Key, p.Value, null);
                _prefetchRunning = false;
                // Gemini: despacio por el limite; Windows: solo una pausa minima
                yield return new WaitForSecondsRealtime(UseGeminiTts ? PrefetchInterval : 0.05f);
            }
            _prefetching = false;
        }

        /// <summary>Dice una frase por un AudioSource (3D para personajes, 2D para la radio).</summary>
        public void Say(AudioSource source, string text, string voice, float volume = 1f, float pitch = 1f, Action<float> onStart = null)
        {
            if (source == null) return;
            StartCoroutine(SayRoutine(source, text, voice, volume, pitch, onStart));
        }

        private IEnumerator SayRoutine(AudioSource source, string text, string voice, float volume, float pitch, Action<float> onStart)
        {
            AudioClip clip = null;
            yield return GetClip(text, voice, c => clip = c);
            if (clip == null || source == null) yield break;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = DeadEcho.Core.AudioChannels.AsVoice(source, volume); // Opciones › Audio › Voces
            source.Play();
            onStart?.Invoke(clip.length / Mathf.Max(0.1f, pitch));
        }

        // ------------------------------------------------------------ Formato

        private static MiniJson.Obj TtsBody(string text, string voice)
        {
            var part = new MiniJson.Obj();
            part.Set("text", text);
            var content = new MiniJson.Obj();
            content.Set("parts", new List<object> { part });

            var prebuilt = new MiniJson.Obj();
            prebuilt.Set("voiceName", voice);
            var voiceCfg = new MiniJson.Obj();
            voiceCfg.Set("prebuiltVoiceConfig", prebuilt);
            var speech = new MiniJson.Obj();
            speech.Set("voiceConfig", voiceCfg);
            var gen = new MiniJson.Obj();
            gen.Set("responseModalities", new List<object> { "AUDIO" });
            gen.Set("speechConfig", speech);

            var body = new MiniJson.Obj();
            body.Set("contents", new List<object> { content });
            body.Set("generationConfig", gen);
            return body;
        }

        private static byte[] ExtractAudio(string json, out string mime)
        {
            mime = null;
            var resp = MiniJson.Parse(json) as MiniJson.Obj;
            var cands = resp?["candidates"] as List<object>;
            if (cands == null || cands.Count == 0) return null;
            var parts = ((cands[0] as MiniJson.Obj)?["content"] as MiniJson.Obj)?["parts"] as List<object>;
            if (parts == null) return null;
            foreach (var p in parts)
            {
                var inline = (p as MiniJson.Obj)?["inlineData"] as MiniJson.Obj;
                string data = inline?.Str("data");
                if (string.IsNullOrEmpty(data)) continue;
                mime = inline.Str("mimeType");
                try { return Convert.FromBase64String(data); }
                catch (Exception) { return null; }
            }
            return null;
        }

        private static int RateFromMime(string mime)
        {
            if (!string.IsNullOrEmpty(mime))
            {
                int i = mime.IndexOf("rate=", StringComparison.Ordinal);
                if (i >= 0 && int.TryParse(mime.Substring(i + 5).Split(';')[0], out int r)) return r;
            }
            return 24000;
        }

        private static byte[] PcmToWav(byte[] pcm, int rate)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + pcm.Length);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(pcm.Length);
                w.Write(pcm);
                return ms.ToArray();
            }
        }

        /// <summary>WAV PCM de 16 bits a AudioClip.</summary>
        public static AudioClip WavToClip(byte[] wav, string name)
        {
            if (wav == null || wav.Length < 44) return null;
            int channels = BitConverter.ToInt16(wav, 22);
            int rate = BitConverter.ToInt32(wav, 24);
            int bits = BitConverter.ToInt16(wav, 34);
            if (bits != 16 || channels < 1) return null;
            // buscar el bloque "data"
            int pos = 12;
            int dataStart = -1, dataLen = 0;
            while (pos + 8 <= wav.Length)
            {
                string chunk = Encoding.ASCII.GetString(wav, pos, 4);
                int len = BitConverter.ToInt32(wav, pos + 4);
                if (chunk == "data")
                {
                    dataStart = pos + 8;
                    dataLen = Mathf.Min(len < 0 ? int.MaxValue : len, wav.Length - dataStart);
                    break;
                }
                pos += 8 + Mathf.Max(0, len);
            }
            if (dataStart < 0) return null;
            int samples = dataLen / 2;
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;
            AudioClip clip = AudioClip.Create("Voz_" + name, samples / channels, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
