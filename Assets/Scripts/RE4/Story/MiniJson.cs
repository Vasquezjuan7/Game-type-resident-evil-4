using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gameplay.RE4
{
    /// <summary>
    /// JSON minimo (lectura y escritura) para hablar con la API de Claude sin dependencias.
    /// Los objetos se leen como listas ordenadas de pares para poder reenviarlos tal cual.
    /// </summary>
    public static class MiniJson
    {
        public class Obj : List<KeyValuePair<string, object>>
        {
            public object this[string key]
            {
                get
                {
                    foreach (var kv in this) if (kv.Key == key) return kv.Value;
                    return null;
                }
            }

            public void Set(string key, object value) => Add(new KeyValuePair<string, object>(key, value));
            public string Str(string key) => this[key] as string;
        }

        // ------------------------------------------------------------ Lectura

        public static object Parse(string json)
        {
            int i = 0;
            object v = ParseValue(json, ref i);
            return v;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
            return d;
        }

        private static Obj ParseObject(string s, ref int i)
        {
            var o = new Obj();
            i++; // {
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string key = ParseString(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                o.Set(key, ParseValue(s, ref i));
            }
            return o;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length && int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ Escritura

        public static string Write(object v)
        {
            var sb = new StringBuilder();
            WriteValue(sb, v);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case Obj o:
                    sb.Append('{');
                    for (int k = 0; k < o.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteString(sb, o[k].Key);
                        sb.Append(':');
                        WriteValue(sb, o[k].Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                default: WriteString(sb, v.ToString()); break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
