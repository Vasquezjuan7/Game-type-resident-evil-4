using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gameplay.RE4
{
    /// <summary>
    /// Voces sin internet ni limites: usa el motor de voz de Windows (voces neuronales en español
    /// "Microsoft Raul" y "Microsoft Sabina") a traves de un proceso de PowerShell que se queda abierto
    /// y genera un .wav por frase. Cada personaje tiene su voz, su tono y su velocidad.
    /// </summary>
    public static class WindowsVoice
    {
        /// <summary>Voz de Windows, tono y velocidad de un personaje.</summary>
        public struct Profile
        {
            public string voice;   // parte del nombre de la voz de Windows
            public string pitch;   // SSML: "-15%"
            public string rate;    // SSML: "0.9"
            public Profile(string voice, string pitch, string rate) { this.voice = voice; this.pitch = pitch; this.rate = rate; }
        }

        /// <summary>Equivalencia de las voces de Gemini de cada personaje con las de Windows.</summary>
        public static Profile ProfileFor(string geminiVoice)
        {
            switch (geminiVoice)
            {
                case NpcVoice.VoiceTomas: return new Profile("Raul", "-18%", "0.88");    // pastor viejo
                case NpcVoice.VoiceIngrid: return new Profile("Sabina", "-4%", "1.04");  // agente por radio
                case NpcVoice.VoiceLeon: return new Profile("Raul", "-6%", "1.0");      // Leon
                case NpcVoice.VoiceMerchant: return new Profile("Raul", "-28%", "0.84"); // mercader
                case "Fenrir": return new Profile("Raul", "-24%", "0.95");
                case "Orus": return new Profile("Raul", "-12%", "0.97");
                case "Umbriel": return new Profile("Raul", "-30%", "0.92");
                case "Gacrux": return new Profile("Sabina", "-16%", "0.95");
                case "Leda": return new Profile("Sabina", "-6%", "1.0");
                case "Despina": return new Profile("Sabina", "-11%", "0.97");
                default: return new Profile("Raul", "0%", "1.0");
            }
        }

        public static bool IsWindows =>
            Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;

        /// <summary>Se puede usar (Windows y el proceso de voz no ha fallado).</summary>
        public static bool Available => IsWindows && !_failed;

        private static Process _proc;
        private static bool _ready, _failed;
        private static readonly object Lock = new object();
        private static readonly HashSet<string> Done = new HashSet<string>();
        private static readonly HashSet<string> Failed = new HashSet<string>();

        // ------------------------------------------------------------ API

        /// <summary>Genera la frase en outFile (.wav). ok recibe si salio bien.</summary>
        public static IEnumerator Synthesize(string text, string geminiVoice, string outFile, Action<bool> ok)
        {
            if (!Available || string.IsNullOrWhiteSpace(text)) { ok?.Invoke(false); yield break; }
            if (!EnsureServer()) { ok?.Invoke(false); yield break; }

            // esperar a que PowerShell termine de cargar el motor de voz (solo la primera vez)
            float wait = Time.realtimeSinceStartup + 15f;
            while (!_ready && !_failed && Time.realtimeSinceStartup < wait) yield return null;
            if (!_ready) { MarkFailed("el motor de voz de Windows no respondió"); ok?.Invoke(false); yield break; }

            Profile p = ProfileFor(geminiVoice);
            string key = Path.GetFullPath(outFile);
            lock (Lock) { Done.Remove(key); Failed.Remove(key); }
            string line = B64(key) + "\t" + p.voice + "\t" + p.pitch + "\t" + p.rate + "\t" + B64(text);
            try
            {
                _proc.StandardInput.WriteLine(line);
                _proc.StandardInput.Flush();
            }
            catch (Exception e)
            {
                MarkFailed(e.Message);
                ok?.Invoke(false);
                yield break;
            }

            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until)
            {
                lock (Lock)
                {
                    if (Done.Remove(key)) { ok?.Invoke(true); yield break; }
                    if (Failed.Remove(key)) { ok?.Invoke(false); yield break; }
                }
                if (_proc == null || _proc.HasExited) break;
                yield return null;
            }
            ok?.Invoke(false);
        }

        // ------------------------------------------------------------ Proceso

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

        private static void MarkFailed(string why)
        {
            if (_failed) return;
            _failed = true;
            Debug.LogWarning("[RE4] Voces de Windows no disponibles: " + why);
            Shutdown();
        }

        private static bool EnsureServer()
        {
            if (_proc != null && !_proc.HasExited) return true;
            try
            {
                string script = Path.Combine(Application.persistentDataPath, "voz_windows.ps1");
                File.WriteAllText(script, ServerScript, new UTF8Encoding(true));
                _ready = false;
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _proc.OutputDataReceived += OnOutput;
                _proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[RE4] Voz de Windows: " + e.Data); };
                _proc.Start();
                _proc.BeginOutputReadLine();
                _proc.BeginErrorReadLine();
                Application.quitting -= Shutdown;
                Application.quitting += Shutdown;
                return true;
            }
            catch (Exception e)
            {
                MarkFailed(e.Message);
                return false;
            }
        }

        private static void OnOutput(object sender, DataReceivedEventArgs e)
        {
            string s = e.Data;
            if (string.IsNullOrEmpty(s)) return;
            if (s == "READY") { _ready = true; return; }
            string[] parts = s.Split('\t');
            if (parts.Length < 2) return;
            string file;
            try { file = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])); }
            catch { return; }
            lock (Lock)
            {
                if (parts[0] == "OK") Done.Add(file);
                else Failed.Add(file);
            }
            if (parts[0] != "OK" && parts.Length > 2) Debug.LogWarning("[RE4] Voz de Windows: " + parts[2]);
        }

        public static void Shutdown()
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    _proc.StandardInput.Close();
                    if (!_proc.WaitForExit(500)) _proc.Kill();
                }
            }
            catch (Exception) { }
            _proc = null;
            _ready = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorHooks()
        {
            // no dejar PowerShell abierto al recompilar o al salir de Play
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += s =>
            {
                if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) Shutdown();
            };
        }
#endif

        // Lee lineas "ruta(b64) TAB voz TAB tono TAB velocidad TAB texto(b64)" y responde "OK|ERR TAB ruta(b64)".
        private const string ServerScript = @"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, [Type]$t) { $task = $asTask.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(-1) | Out-Null; $task.Result }
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$voices = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::Out.WriteLine('READY'); [Console]::Out.Flush()
while ($true) {
  $line = [Console]::In.ReadLine()
  if ($line -eq $null) { break }
  $p = $line.Split([char]9)
  if ($p.Count -lt 5) { continue }
  try {
    $out = $utf8.GetString([Convert]::FromBase64String($p[0]))
    $text = $utf8.GetString([Convert]::FromBase64String($p[4]))
    $v = $voices | Where-Object { $_.DisplayName -like ('*' + $p[1] + '*') } | Select-Object -First 1
    if ($v -eq $null) { $v = $voices | Where-Object { $_.Language -like 'es*' } | Select-Object -First 1 }
    if ($v -ne $null) { $synth.Voice = $v }
    $lang = $synth.Voice.Language
    $esc = [System.Security.SecurityElement]::Escape($text)
    $ssml = ""<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$lang'><prosody pitch='$($p[2])' rate='$($p[3])'>$esc</prosody></speak>""
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $size = [uint32]$stream.Size
    $null = Await ($reader.LoadAsync($size)) ([uint32])
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    $reader.Dispose(); $stream.Dispose()
    $dir = [System.IO.Path]::GetDirectoryName($out)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    [System.IO.File]::WriteAllBytes($out + '.tmp', $bytes)
    Move-Item -Force ($out + '.tmp') $out
    [Console]::Out.WriteLine('OK' + [char]9 + $p[0])
  } catch {
    [Console]::Out.WriteLine('ERR' + [char]9 + $p[0] + [char]9 + ($_.Exception.Message -replace '\s+', ' '))
  }
  [Console]::Out.Flush()
}
";
    }
}
