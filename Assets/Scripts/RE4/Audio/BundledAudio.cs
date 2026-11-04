using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Audio que va dentro del juego (generado con ElevenLabs) en Resources/RE4Audio.
    /// - Efectos: "Carpeta/Prefijo_NN" se agrupan por prefijo (todo antes del ultimo '_') y se elige uno al azar.
    /// - Frases: "Frases/Personaje/slug_N", donde slug es el texto sin tildes ni signos (ver <see cref="Slug"/>).
    /// Si un archivo no existe, devuelve null y cada sistema usa su sonido de siempre.
    /// </summary>
    public static class BundledAudio
    {
        public const string Root = "RE4Audio/";
        public const string FrasesLeon = "Frases/Leon";
        public const string FrasesAldeano = "Frases/Aldeano";
        public const string FrasesAldeana = "Frases/Aldeana";
        public const string FrasesMercader = "Frases/Mercader";
        public const string FrasesIngrid = "Frases/Ingrid";
        public const string FrasesTomas = "Frases/Tomas";

        private static readonly Dictionary<string, Dictionary<string, AudioClip[]>> Folders =
            new Dictionary<string, Dictionary<string, AudioClip[]>>();

        /// <summary>Clips de una carpeta agrupados por prefijo ("Perro_Grunido_02" -> "Perro_Grunido").</summary>
        public static Dictionary<string, AudioClip[]> Group(string folder)
        {
            if (Folders.TryGetValue(folder, out var sets)) return sets;
            sets = new Dictionary<string, AudioClip[]>();
            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>(Root + folder))
            {
                int cut = c.name.LastIndexOf('_');
                string key = cut > 0 ? c.name.Substring(0, cut) : c.name;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            foreach (var kv in groups) sets[kv.Key] = kv.Value.ToArray();
            Folders[folder] = sets;
            return sets;
        }

        /// <summary>Un clip al azar del grupo, o null.</summary>
        public static AudioClip Pick(string folder, string key)
        {
            return Group(folder).TryGetValue(key, out var set) && set.Length > 0 ? set[Random.Range(0, set.Length)] : null;
        }

        /// <summary>Clip de una frase concreta ("¡Cógelo!" -> "cogelo_1"/"cogelo_2"...), o null.</summary>
        public static AudioClip Line(string folder, string text)
        {
            if (folder == null || string.IsNullOrWhiteSpace(text)) return null;
            return Pick(folder, Slug(text));
        }

        /// <summary>Carpeta de frases de una voz de <see cref="NpcVoice"/> (null si ese personaje no tiene frases incluidas).</summary>
        public static string FolderForVoice(string voice)
        {
            if (voice == NpcVoice.VoiceLeon) return FrasesLeon;
            if (voice == NpcVoice.VillagerMale[0]) return FrasesAldeano;
            if (voice == NpcVoice.VillagerFemale[0]) return FrasesAldeana;
            if (voice == NpcVoice.VoiceMerchant) return FrasesMercader;
            if (voice == NpcVoice.VoiceIngrid) return FrasesIngrid;
            if (voice == NpcVoice.VoiceTomas) return FrasesTomas;
            return null;
        }

        /// <summary>Texto -> nombre de archivo: minusculas, sin tildes ni signos, espacios a '_'.</summary>
        public static string Slug(string text)
        {
            string d = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            bool sep = false;
            foreach (char ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    if (sep && sb.Length > 0) sb.Append('_');
                    sb.Append(ch);
                    sep = false;
                }
                else sep = true;
            }
            return sb.ToString();
        }
    }
}
