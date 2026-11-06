// Naija Streets (Unity) — tiny JSON reader/escaper for Core (no Unity, no packages).
// Only for small protocol frames (Realtime messages). Objects become
// Dictionary<string, object>, arrays List<object>, numbers double, plus string,
// bool and null. Returns null for anything malformed; never throws.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NaijaLife.Network
{
    public static class MiniJson
    {
        const int MaxDepth = 32;
        const int MaxLength = 64 * 1024;

        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > MaxLength) return null;
            int i = 0;
            if (!TryValue(json, ref i, 0, out object v)) return null;
            SkipWs(json, ref i);
            return i == json.Length ? v : null;
        }

        /// <summary>A JSON string literal (with quotes) for any text.</summary>
        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
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
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static Dictionary<string, object> Obj(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;

        public static string Str(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) ? v as string : null;

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static bool TryValue(string s, ref int i, int depth, out object v)
        {
            v = null;
            if (depth > MaxDepth) return false;
            SkipWs(s, ref i);
            if (i >= s.Length) return false;
            char c = s[i];
            if (c == '{') return TryObject(s, ref i, depth, out v);
            if (c == '[') return TryArray(s, ref i, depth, out v);
            if (c == '"') { bool ok = TryString(s, ref i, out string str); v = str; return ok; }
            if (Word(s, ref i, "true")) { v = true; return true; }
            if (Word(s, ref i, "false")) { v = false; return true; }
            if (Word(s, ref i, "null")) { v = null; return true; }
            return TryNumber(s, ref i, out v);
        }

        static bool Word(string s, ref int i, string w)
        {
            if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) return false;
            i += w.Length;
            return true;
        }

        static bool TryObject(string s, ref int i, int depth, out object v)
        {
            v = null;
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; v = d; return true; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"' || !TryString(s, ref i, out string key)) return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') return false;
                i++;
                if (!TryValue(s, ref i, depth + 1, out object val)) return false;
                d[key] = val;
                SkipWs(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; v = d; return true; }
                return false;
            }
        }

        static bool TryArray(string s, ref int i, int depth, out object v)
        {
            v = null;
            var list = new List<object>();
            i++; SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; v = list; return true; }
            while (true)
            {
                if (!TryValue(s, ref i, depth + 1, out object val)) return false;
                list.Add(val);
                SkipWs(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; v = list; return true; }
                return false;
            }
        }

        static bool TryString(string s, ref int i, out string str)
        {
            str = null;
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') { str = sb.ToString(); return true; }
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return false;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)) return false;
                        sb.Append((char)code); i += 4; break;
                    default: return false;
                }
            }
            return false;
        }

        static bool TryNumber(string s, ref int i, out object v)
        {
            v = null;
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) return false;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return false;
            v = d;
            return true;
        }
    }
}
