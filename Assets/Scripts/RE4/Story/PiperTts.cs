using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gameplay.RE4
{
    /// <summary>
    /// Voz neuronal sin internet (Piper, codigo abierto) para las frases que no vienen grabadas en el juego,
    /// sobre todo las respuestas que inventa la IA de Tomas. Usa StreamingAssets/Piper/piper.exe y los modelos
    /// de StreamingAssets/Piper/voces/*.onnx. Mismo tono que las frases incluidas (tools/gen_piper.py).
    /// Si falta algo, no hace nada y se usa la voz de Windows.
    /// </summary>
    public static class PiperTts
    {
        public struct Profile
        {
            public string model; public int speaker; public float pitch, length, noise, hard;
            public Profile(string model, int speaker, float pitch, float length, float noise, float hard)
            { this.model = model; this.speaker = speaker; this.pitch = pitch; this.length = length; this.noise = noise; this.hard = hard; }
        }

        private const string Dave = "es_ES-davefx-medium", Sharvard = "es_ES-sharvard-medium", Mls = "es_ES-mls_9972-low";

        /// <summary>Perfil de cada personaje (igual que en gen_piper.py). speaker -1 = modelo de una sola voz.</summary>
        public static Profile ProfileFor(string voice)
        {
            switch (voice)
            {
                case NpcVoice.VoiceTomas: return new Profile(Dave, -1, 0.84f, 1.10f, 0.70f, 0.15f);
                case NpcVoice.VoiceLeon: return new Profile(Dave, -1, 0.97f, 1.00f, 0.60f, 0f);
                case NpcVoice.VoiceIngrid: return new Profile(Sharvard, 1, 1.00f, 0.95f, 0.55f, 0f);
                case NpcVoice.VoiceMerchant: return new Profile(Sharvard, 0, 0.78f, 1.15f, 0.65f, 0.1f);
            }
            if (Array.IndexOf(NpcVoice.VillagerFemale, voice) >= 0) return new Profile(Mls, -1, 0.90f, 0.92f, 0.80f, 0.5f);
            return new Profile(Sharvard, 0, 0.86f, 0.92f, 0.80f, 0.6f); // aldeanos
        }

        public static string Folder => Path.Combine(Application.streamingAssetsPath, "Piper");
        private static string Exe => Path.Combine(Folder, "piper.exe");
        private static string ModelPath(string model) => Path.Combine(Folder, "voces", model + ".onnx");

        private static bool _failed;

        /// <summary>Hay Piper y el modelo de este personaje.</summary>
        public static bool Has(string voice) =>
            !_failed && WindowsVoice.IsWindows && File.Exists(Exe) && File.Exists(ModelPath(ProfileFor(voice).model));

        public static bool Available => !_failed && WindowsVoice.IsWindows && File.Exists(Exe);

        /// <summary>Id de cache de una frase (cambia si cambia el perfil).</summary>
        public static string Key(string voice)
        {
            Profile p = ProfileFor(voice);
            return $"{p.model}|{p.speaker}|{p.pitch}|{p.length}|{p.noise}|{p.hard}";
        }

        /// <summary>Genera la frase en outFile (.wav). ok recibe si salio bien.</summary>
        public static IEnumerator Synthesize(string text, string voice, string outFile, Action<bool> ok)
        {
            if (!Has(voice) || string.IsNullOrWhiteSpace(text)) { ok?.Invoke(false); yield break; }
            Profile p = ProfileFor(voice);
            string clean = text.Replace("«", "").Replace("»", "").Replace("*", "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            Task<string> job = Task.Run(() => Run(clean, p, outFile));
            float until = Time.realtimeSinceStartup + 30f;
            while (!job.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
            string err = job.IsCompleted ? (job.IsFaulted ? job.Exception?.GetBaseException().Message : job.Result) : "tardó demasiado";
            if (err != null) Debug.LogWarning("[RE4] Voz Piper: " + err);
            ok?.Invoke(err == null && File.Exists(outFile));
        }

        /// <summary>Hilo aparte: piper.exe -> wav crudo -> tono/aspereza/recorte -> outFile. Devuelve el error o null.</summary>
        private static string Run(string text, Profile p, string outFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            string raw = outFile + ".raw.wav";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string args = $"-m \"{ModelPath(p.model)}\" -f \"{raw}\" --length_scale {(p.length * p.pitch).ToString(inv)} --noise_scale {p.noise.ToString(inv)}";
            if (p.speaker >= 0) args += " --speaker " + p.speaker;
            var psi = new ProcessStartInfo
            {
                FileName = Exe,
                Arguments = args,
                WorkingDirectory = Folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            try
            {
                using (var proc = Process.Start(psi))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text + "\n");
                    proc.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                    proc.StandardInput.Close();
                    proc.StandardOutput.ReadToEndAsync();
                    var errText = proc.StandardError.ReadToEndAsync();
                    if (!proc.WaitForExit(25000)) { try { proc.Kill(); } catch { } return "piper no terminó"; }
                    if (proc.ExitCode != 0 || !File.Exists(raw)) return "piper falló (" + proc.ExitCode + "): " + Tail(errText.Result);
                }
                byte[] wav = PostProcess(File.ReadAllBytes(raw), p);
                File.Delete(raw);
                if (wav == null) return "wav no válido";
                File.WriteAllBytes(outFile, wav);
                return null;
            }
            catch (Exception e)
            {
                if (e is System.ComponentModel.Win32Exception) _failed = true; // no se puede ejecutar piper.exe
                return e.Message;
            }
        }

        private static string Tail(string s) => string.IsNullOrEmpty(s) ? "" : s.Length > 200 ? s.Substring(s.Length - 200) : s;

        /// <summary>Tono mas grave (re-muestreo), voz mas aspera, sin silencios y normalizada. PCM 16 bit mono.</summary>
        private static byte[] PostProcess(byte[] wav, Profile p)
        {
            int rate = 22050, dataAt = -1, dataLen = 0;
            for (int i = 12; i + 8 <= wav.Length;)
            {
                string id = Encoding.ASCII.GetString(wav, i, 4);
                int len = BitConverter.ToInt32(wav, i + 4);
                if (id == "fmt ") rate = BitConverter.ToInt32(wav, i + 12);
                if (id == "data") { dataAt = i + 8; dataLen = Math.Min(len, wav.Length - dataAt); break; }
                i += 8 + len + (len & 1);
            }
            if (dataAt < 0 || dataLen < 4) return null;
            int n = dataLen / 2;
            var x = new float[n];
            for (int i = 0; i < n; i++) x[i] = BitConverter.ToInt16(wav, dataAt + i * 2) / 32768f;

            // tono: estirar la onda (se oye mas grave y mas lenta; la velocidad ya se compenso en length_scale)
            int m = (int)(n / p.pitch);
            var y = new float[m];
            for (int i = 0; i < m; i++)
            {
                float s = i * p.pitch; int a = (int)s; float f = s - a;
                y[i] = a + 1 < n ? x[a] * (1 - f) + x[a + 1] * f : x[n - 1];
            }
            if (p.hard > 0f)
            {
                float k = 1f + 4f * p.hard, norm = (float)Math.Tanh(k);
                for (int i = 0; i < m; i++) y[i] = (float)Math.Tanh(y[i] * k) / norm;
            }
            int first = 0, last = m - 1;
            while (first < m && Math.Abs(y[first]) < 0.02f) first++;
            while (last > first && Math.Abs(y[last]) < 0.02f) last--;
            first = Math.Max(0, first - rate / 50);
            last = Math.Min(m - 1, last + rate * 8 / 100);
            float peak = 1e-6f;
            for (int i = first; i <= last; i++) peak = Math.Max(peak, Math.Abs(y[i]));
            float gain = 0.89f / peak;

            int count = Math.Max(1, last - first + 1);
            var o = new byte[44 + count * 2];
            void Str(int at, string s) { Encoding.ASCII.GetBytes(s, 0, 4, o, at); }
            void I32(int at, int v) { BitConverter.GetBytes(v).CopyTo(o, at); }
            void I16(int at, short v) { BitConverter.GetBytes(v).CopyTo(o, at); }
            Str(0, "RIFF"); I32(4, 36 + count * 2); Str(8, "WAVE");
            Str(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, 1); I32(24, rate); I32(28, rate * 2); I16(32, 2); I16(34, 16);
            Str(36, "data"); I32(40, count * 2);
            for (int i = 0; i < count; i++)
            {
                float v = Mathf.Clamp(y[first + i] * gain, -1f, 1f);
                I16(44 + i * 2, (short)(v * 32767f));
            }
            return o;
        }
    }
}
