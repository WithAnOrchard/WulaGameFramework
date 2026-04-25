using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EssSystem.Core.Util
{
    /// <summary>
    /// 轻量级 JSON 序列化器 — 专为框架的 <c>Dictionary&lt;string, object&gt;</c> + <c>List&lt;object&gt;</c>
    /// 存档结构设计。Unity 自带的 <c>JsonUtility</c> 不支持这类动态结构，这里手写一个无依赖实现。
    /// </summary>
    /// <remarks>
    /// 支持的类型（序列化 ⇄ 反序列化对称）：<br/>
    /// - null<br/>
    /// - bool<br/>
    /// - string（带 \uXXXX 等转义）<br/>
    /// - 整数（<c>long</c> / <c>int</c>）<br/>
    /// - 浮点（<c>double</c> / <c>float</c>，InvariantCulture）<br/>
    /// - <see cref="DateTime"/>（ISO 8601 字符串）<br/>
    /// - <see cref="IList{T}"/> / 数组 → <c>List&lt;object&gt;</c><br/>
    /// - <see cref="IDictionary{TKey,TValue}"/> → <c>Dictionary&lt;string, object&gt;</c><br/>
    /// - 其它 <c>[Serializable]</c> 对象 → 反射按 public 字段序列化（反序列化后回读为 Dictionary）
    /// <para>
    /// 反序列化总是把 JSON 对象解析成 <c>Dictionary&lt;string, object&gt;</c>，数组解析成 <c>List&lt;object&gt;</c>。
    /// </para>
    /// </remarks>
    public static class MiniJson
    {
        #region Public API

        /// <summary>序列化为 JSON 字符串</summary>
        /// <param name="obj">被序列化对象（可空）</param>
        /// <param name="pretty">是否缩进输出</param>
        public static string Serialize(object obj, bool pretty = false)
        {
            var sb = new StringBuilder(256);
            WriteValue(sb, obj, pretty ? 0 : -1);
            return sb.ToString();
        }

        /// <summary>反序列化 JSON 字符串为 <c>Dictionary&lt;string, object&gt;</c> / <c>List&lt;object&gt;</c> / 原语</summary>
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var parser = new Parser(json);
            return parser.Parse();
        }

        #endregion

        #region Writer

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            if (value == null) { sb.Append("null"); return; }

            switch (value)
            {
                case string s:       WriteString(sb, s); return;
                case bool b:         sb.Append(b ? "true" : "false"); return;
                case DateTime dt:    WriteString(sb, dt.ToString("o", CultureInfo.InvariantCulture)); return;
                case float f:        sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d:       sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m:      sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case sbyte  _: case byte   _:
                case short  _: case ushort _:
                case int    _: case uint   _:
                case long   _: case ulong  _:
                                     sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture)); return;
                case Enum e:         sb.Append(Convert.ToInt64(e, CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> dict: WriteDict(sb, dict, indent); return;
            }

            // 通用 IDictionary（key 不一定是 string）
            if (value is System.Collections.IDictionary idict)
            {
                WriteDictGeneric(sb, idict, indent);
                return;
            }
            // 通用 IEnumerable
            if (value is System.Collections.IEnumerable list)
            {
                WriteList(sb, list, indent);
                return;
            }

            // 任意对象 — 反射 public 字段
            WriteReflectedObject(sb, value, indent);
        }

        private static void WriteDict(StringBuilder sb, IDictionary<string, object> dict, int indent)
        {
            if (dict.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(',');
                AppendIndent(sb, indent, childLevel: true);
                WriteString(sb, kv.Key);
                sb.Append(indent >= 0 ? ": " : ":");
                WriteValue(sb, kv.Value, indent >= 0 ? indent + 1 : -1);
                first = false;
            }
            AppendIndent(sb, indent, childLevel: false);
            sb.Append('}');
        }

        private static void WriteDictGeneric(StringBuilder sb, System.Collections.IDictionary dict, int indent)
        {
            if (dict.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool first = true;
            foreach (System.Collections.DictionaryEntry kv in dict)
            {
                if (!first) sb.Append(',');
                AppendIndent(sb, indent, childLevel: true);
                WriteString(sb, kv.Key?.ToString() ?? "");
                sb.Append(indent >= 0 ? ": " : ":");
                WriteValue(sb, kv.Value, indent >= 0 ? indent + 1 : -1);
                first = false;
            }
            AppendIndent(sb, indent, childLevel: false);
            sb.Append('}');
        }

        private static void WriteList(StringBuilder sb, System.Collections.IEnumerable list, int indent)
        {
            sb.Append('[');
            bool first = true;
            bool any = false;
            foreach (var item in list)
            {
                any = true;
                if (!first) sb.Append(',');
                AppendIndent(sb, indent, childLevel: true);
                WriteValue(sb, item, indent >= 0 ? indent + 1 : -1);
                first = false;
            }
            if (any) AppendIndent(sb, indent, childLevel: false);
            sb.Append(']');
        }

        private static void WriteReflectedObject(StringBuilder sb, object value, int indent)
        {
            sb.Append('{');
            bool first = true;
            var type = value.GetType();
            // 只序列化 public 字段（和 [Serializable] 契约一致）
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            foreach (var f in fields)
            {
                if (!first) sb.Append(',');
                AppendIndent(sb, indent, childLevel: true);
                WriteString(sb, f.Name);
                sb.Append(indent >= 0 ? ": " : ":");
                WriteValue(sb, f.GetValue(value), indent >= 0 ? indent + 1 : -1);
                first = false;
            }
            if (!first) AppendIndent(sb, indent, childLevel: false);
            sb.Append('}');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else          sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static void AppendIndent(StringBuilder sb, int indent, bool childLevel)
        {
            if (indent < 0) return; // compact
            sb.Append('\n');
            int lvl = childLevel ? indent + 1 : indent;
            for (int i = 0; i < lvl; i++) sb.Append("  ");
        }

        #endregion

        #region Parser

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; _i = 0; }

            public object Parse()
            {
                SkipWs();
                var v = ParseValue();
                SkipWs();
                if (_i < _s.Length)
                    throw new FormatException($"MiniJson: unexpected trailing data at {_i}");
                return v;
            }

            private object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) throw new FormatException("MiniJson: unexpected EOF");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': case 'f': return ParseBool();
                    case 'n': return ParseNull();
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new FormatException($"MiniJson: unexpected char '{c}' at {_i}");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                Expect('{');
                SkipWs();
                if (Peek() == '}') { _i++; return dict; }

                while (true)
                {
                    SkipWs();
                    string key = ParseString();
                    SkipWs();
                    Expect(':');
                    object val = ParseValue();
                    dict[key] = val;
                    SkipWs();
                    char c = Next();
                    if (c == '}') return dict;
                    if (c != ',') throw new FormatException($"MiniJson: expected ',' or '}}' at {_i - 1}");
                }
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                Expect('[');
                SkipWs();
                if (Peek() == ']') { _i++; return list; }

                while (true)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    char c = Next();
                    if (c == ']') return list;
                    if (c != ',') throw new FormatException($"MiniJson: expected ',' or ']' at {_i - 1}");
                }
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_i >= _s.Length) throw new FormatException("MiniJson: bad escape at EOF");
                        char esc = _s[_i++];
                        switch (esc)
                        {
                            case '"':  sb.Append('"');  break;
                            case '\\': sb.Append('\\'); break;
                            case '/':  sb.Append('/');  break;
                            case 'b':  sb.Append('\b'); break;
                            case 'f':  sb.Append('\f'); break;
                            case 'n':  sb.Append('\n'); break;
                            case 'r':  sb.Append('\r'); break;
                            case 't':  sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 > _s.Length) throw new FormatException("MiniJson: bad \\u");
                                sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                _i += 4;
                                break;
                            default: throw new FormatException($"MiniJson: bad escape \\{esc}");
                        }
                    }
                    else sb.Append(c);
                }
                throw new FormatException("MiniJson: unterminated string");
            }

            private object ParseNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                bool isFloat = false;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    isFloat = true; _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    isFloat = true; _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                string token = _s.Substring(start, _i - start);
                if (isFloat) return double.Parse(token, CultureInfo.InvariantCulture);
                return long.Parse(token, CultureInfo.InvariantCulture);
            }

            private bool ParseBool()
            {
                if (_s[_i] == 't' && _s.Length - _i >= 4 && _s.Substring(_i, 4) == "true")
                { _i += 4; return true; }
                if (_s[_i] == 'f' && _s.Length - _i >= 5 && _s.Substring(_i, 5) == "false")
                { _i += 5; return false; }
                throw new FormatException($"MiniJson: bad bool at {_i}");
            }

            private object ParseNull()
            {
                if (_s.Length - _i >= 4 && _s.Substring(_i, 4) == "null")
                { _i += 4; return null; }
                throw new FormatException($"MiniJson: bad null at {_i}");
            }

            private void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';
            private char Next() => _i < _s.Length ? _s[_i++] : '\0';
            private void Expect(char c)
            {
                if (_i >= _s.Length || _s[_i] != c)
                    throw new FormatException($"MiniJson: expected '{c}' at {_i}");
                _i++;
            }
        }

        #endregion
    }
}
