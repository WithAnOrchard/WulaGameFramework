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

        /// <summary>
        /// 反序列化为 <see cref="JsonNode"/> 包装器，支持类 JToken 的链式访问：
        /// <c>node["data"]["users"][0].Value&lt;string&gt;("name")</c>。
        /// 缺失的 key / 越界 index 不抛异常，返回 <see cref="JsonNode.Missing"/>，配合 <c>ToString()</c> 返回 <c>""</c> / <c>ToObject&lt;T&gt;()</c> 返回 default。
        /// </summary>
        public static JsonNode Parse(string json) => new JsonNode(Deserialize(json));

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

    /// <summary>
    /// MiniJson 的 <c>object</c> 解析结果包装器，模拟 Newtonsoft.JToken 的链式访问 API：
    /// <list type="bullet">
    /// <item>对象/数组索引：<c>node["key"]</c> / <c>node[0]</c> 缺失时返回 <see cref="Missing"/>（不抛异常）</item>
    /// <item>标量转换：<c>ToObject&lt;int&gt;()</c> / <c>Value&lt;string&gt;("uname")</c>；失败返回 <c>default(T)</c></item>
    /// <item><c>ToString()</c>：非 null 原值转字符串；null / missing 返回 <see cref="string.Empty"/>（不是 "null"）</item>
    /// <item><c>ToList()</c>：把 JSON 数组展开为 <c>List&lt;JsonNode&gt;</c></item>
    /// </list>
    /// </summary>
    public sealed class JsonNode
    {
        /// <summary>全局"缺失节点"哨兵，多次访问不抛异常。</summary>
        public static readonly JsonNode Missing = new JsonNode(null, isMissing: true);

        /// <summary>底层原始值（Dictionary / List / 原语 / null）。直接操作需要时读取。</summary>
        public readonly object Raw;
        private readonly bool _isMissing;

        internal JsonNode(object raw, bool isMissing = false)
        {
            Raw = raw;
            _isMissing = isMissing;
        }

        /// <summary>当前节点是否是"找不到 key / 越界"哨兵。</summary>
        public bool IsMissing => _isMissing;

        /// <summary>当前节点是否为 JSON null（包含缺失）。</summary>
        public bool IsNull => Raw == null;

        /// <summary>是否是非 null 的真实值。</summary>
        public bool Exists => !_isMissing && Raw != null;

        /// <summary>按 key 取子节点；不是 dict 或 key 不存在返回 <see cref="Missing"/>。</summary>
        public JsonNode this[string key]
        {
            get
            {
                if (Raw is Dictionary<string, object> d && d.TryGetValue(key, out var v))
                    return new JsonNode(v);
                return Missing;
            }
        }

        /// <summary>按 index 取子节点；不是 list 或越界返回 <see cref="Missing"/>。</summary>
        public JsonNode this[int index]
        {
            get
            {
                if (Raw is List<object> l && index >= 0 && index < l.Count)
                    return new JsonNode(l[index]);
                return Missing;
            }
        }

        /// <summary>原值转字符串；null / missing 返回 <see cref="string.Empty"/>。</summary>
        public override string ToString() => Raw == null ? string.Empty : Raw.ToString();

        /// <summary>
        /// 把底层值转成 <typeparamref name="T"/>。支持 <c>int/long/short/uint/ulong/double/float/decimal/bool/string</c>
        /// 以及枚举（按整数解析）；null / 转换失败返回 <c>default</c>。
        /// </summary>
        public T ToObject<T>()
        {
            if (Raw == null) return default;
            var type = typeof(T);
            try
            {
                if (type == typeof(string))  return (T)(object)Raw.ToString();
                if (type == typeof(int))     return (T)(object)Convert.ToInt32(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(long))    return (T)(object)Convert.ToInt64(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(short))   return (T)(object)Convert.ToInt16(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(uint))    return (T)(object)Convert.ToUInt32(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(ulong))   return (T)(object)Convert.ToUInt64(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(double))  return (T)(object)Convert.ToDouble(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(float))   return (T)(object)Convert.ToSingle(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(decimal)) return (T)(object)Convert.ToDecimal(Raw, CultureInfo.InvariantCulture);
                if (type == typeof(bool))    return (T)(object)Convert.ToBoolean(Raw, CultureInfo.InvariantCulture);
                if (type.IsEnum)             return (T)Enum.ToObject(type, Convert.ToInt64(Raw, CultureInfo.InvariantCulture));
                if (Raw is T t) return t;
            }
            catch
            {
                return default;
            }
            return default;
        }

        /// <summary>
        /// 等价于 <c>this[key].ToObject&lt;T&gt;()</c>，模拟 Newtonsoft <c>JToken.Value&lt;T&gt;(string)</c>。
        /// </summary>
        public T Value<T>(string key) => this[key].ToObject<T>();

        /// <summary>把数组节点展开为 <c>List&lt;JsonNode&gt;</c>；非数组返回空列表。</summary>
        public List<JsonNode> ToList()
        {
            var result = new List<JsonNode>();
            if (Raw is List<object> l)
            {
                for (var i = 0; i < l.Count; i++) result.Add(new JsonNode(l[i]));
            }
            return result;
        }
    }
}
