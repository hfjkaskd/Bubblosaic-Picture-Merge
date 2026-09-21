using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BubblePics.SpineLite
{
    /// <summary>Minimal JSON parser (objects -> Dictionary, arrays -> List,
    /// numbers -> double). Enough for parsed .skel exports.</summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            return ParseValue(s, ref i);
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                i++; // :
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // }
                return d;
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var a = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ]
                return a;
            }
        }

        static string ParseString(string s, ref int i)
        {
            i++; // "
            int start = i;
            StringBuilder sb = null;
            while (true)
            {
                char c = s[i];
                if (c == '\\')
                {
                    sb ??= new StringBuilder(s, start, i - start, 64);
                    i++;
                    char e = s[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                    i++;
                }
                else if (c == '"')
                {
                    i++;
                    return sb != null ? sb.ToString() : s.Substring(start, i - 1 - start);
                }
                else
                {
                    sb?.Append(c);
                    i++;
                }
            }
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                else break;
            }
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\t' || s[i] == '\r')) i++;
        }
    }
}
