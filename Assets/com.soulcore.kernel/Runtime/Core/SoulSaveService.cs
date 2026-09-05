using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoulCore
{
    /// <summary>
    /// 存档服务 — JSON 世界存档的导出/导入/序列化/反序列化（对齐 6.1.8 方案 SoulSaveService）。
    /// 内置轻量 JSON 序列化器（零依赖，Unity/团结引擎任何版本可用）。
    /// 存档结构：{ version, saved_at, npcs: [{core, relations, memories}], world_event_log }
    /// </summary>
    public static class SoulSaveService
    {
        public const string SaveVersion = "0.1.0";
        public const string DefaultSaveFileName = "soulcore_world_save.json";

        public class ApplyResult
        {
            public int appliedNpcCount = 0;
            public int appliedEventCount = 0;
            public List<string> skippedNpcIds = new List<string>();
        }

        // ==================== 捕获/应用 ====================

        public static Dictionary<string, object> CaptureWorld(
            List<KeyValuePair<string, Soul>> bindings, SoulWorldEventBus bus = null)
        {
            var data = new Dictionary<string, object>
            {
                ["version"] = SaveVersion,
                ["saved_at"] = MemoryEngine.UnixNow(),
                ["npcs"] = new List<object>(),
                ["world_event_log"] = new List<object>(),
            };
            var npcList = new List<object>();
            foreach (var kv in bindings)
            {
                if (kv.Value == null || string.IsNullOrEmpty(kv.Key)) continue;
                npcList.Add(CaptureNpc(kv.Key, kv.Value));
            }
            data["npcs"] = npcList;
            if (bus != null)
                data["world_event_log"] = new List<object>(bus.ExportEventLog());
            return data;
        }

        public static ApplyResult ApplyWorld(Dictionary<string, object> data,
            List<KeyValuePair<string, Soul>> bindings, SoulWorldEventBus bus = null)
        {
            var result = new ApplyResult();
            if (data == null || data.Count == 0 || bindings == null || bindings.Count == 0)
                return result;

            var map = new Dictionary<string, Soul>();
            foreach (var kv in bindings)
                if (kv.Value != null && !string.IsNullOrEmpty(kv.Key))
                    map[kv.Key] = kv.Value;

            if (data.TryGetValue("npcs", out var npcsObj) && npcsObj is List<object> npcs)
            {
                foreach (var entryObj in npcs)
                {
                    if (!(entryObj is Dictionary<string, object> entry)) continue;
                    if (!entry.TryGetValue("core", out var coreObj) || !(coreObj is Dictionary<string, object> core)) continue;
                    var npcId = Memory.GetStr(core, "id", "");
                    if (string.IsNullOrEmpty(npcId)) continue;
                    if (!map.TryGetValue(npcId, out var soul))
                    {
                        result.skippedNpcIds.Add(npcId);
                        continue;
                    }
                    ApplyNpc(entry, soul);
                    result.appliedNpcCount += 1;
                }
            }

            if (bus != null && data.TryGetValue("world_event_log", out var logObj) && logObj is List<object> log)
            {
                var eventList = new List<Dictionary<string, object>>();
                foreach (var item in log)
                    if (item is Dictionary<string, object> d) eventList.Add(d);
                bus.ReplaceEventLog(eventList);
                result.appliedEventCount = eventList.Count;
            }
            return result;
        }

        private static Dictionary<string, object> CaptureNpc(string citizenId, Soul soul)
        {
            var entry = new Dictionary<string, object>
            {
                ["core"] = soul.ExportSnapshot(),
                ["relations"] = new List<object>(),
                ["memories"] = new List<object>(),
            };
            ((Dictionary<string, object>)entry["core"])["id"] = citizenId;

            var relations = new List<object>();
            foreach (var edge in soul.relationship.ExportEdgesForAgent(citizenId))
                relations.Add(edge);
            entry["relations"] = relations;

            var memories = new List<object>();
            foreach (var pair in soul.memory.ExportAllWithBuckets())
            {
                if (pair.Item2 == null) continue;
                var memDict = pair.Item2.ToDict();
                memDict["bucket"] = pair.Item1;
                memories.Add(memDict);
            }
            entry["memories"] = memories;
            return entry;
        }

        private static void ApplyNpc(Dictionary<string, object> entry, Soul soul)
        {
            if (entry.TryGetValue("core", out var coreObj) && coreObj is Dictionary<string, object> core)
                soul.ApplySnapshot(core);

            if (entry.TryGetValue("relations", out var relObj) && relObj is List<object> relations)
            {
                var edges = new List<Dictionary<string, object>>();
                foreach (var item in relations)
                    if (item is Dictionary<string, object> d) edges.Add(d);
                soul.relationship.ReplaceEdgesForAgent(soul.id, edges);
            }

            if (entry.TryGetValue("memories", out var memObj) && memObj is List<object> memories)
            {
                var pairs = new List<Tuple<string, Memory>>();
                foreach (var item in memories)
                {
                    if (!(item is Dictionary<string, object> memDict)) continue;
                    var m = Memory.FromDict(memDict);
                    var bucket = Memory.GetStr(memDict, "bucket", "short_term");
                    pairs.Add(Tuple.Create(bucket, m));
                }
                soul.memory.ReplaceAll(pairs);
            }
        }

        // ==================== 轻量 JSON 序列化（零依赖） ====================

        public static string Serialize(Dictionary<string, object> data)
            => data == null || data.Count == 0 ? "{}" : JsonWriter.Write(data);

        public static Dictionary<string, object> TryDeserialize(string jsonStr)
        {
            if (string.IsNullOrWhiteSpace(jsonStr)) return new Dictionary<string, object>();
            try
            {
                var parser = new JsonParser(jsonStr);
                var result = parser.ParseValue();
                return result as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        // ---------- Writer ----------
        private static class JsonWriter
        {
            public static string Write(object value)
            {
                var sb = new StringBuilder();
                WriteValue(sb, value);
                return sb.ToString();
            }

            private static void WriteValue(StringBuilder sb, object v)
            {
                switch (v)
                {
                    case null: sb.Append("null"); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case string s: WriteString(sb, s); break;
                    case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                    case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                    case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                    case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                    case Dictionary<string, object> dict:
                        sb.Append('{');
                        var first = true;
                        foreach (var kv in dict)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteString(sb, kv.Key);
                            sb.Append(':');
                            WriteValue(sb, kv.Value);
                        }
                        sb.Append('}');
                        break;
                    case System.Collections.IDictionary idict:
                        // 关键：支持 Dictionary<string, float> 等强类型字典——
                        // 否则 traits/emotions（Dictionary<string,float>）会落到默认分支
                        // 被序列化成字符串，反序列化后 is Dictionary<string,object> 判断失败，
                        // 人格/情绪还原静默失效（存档还原 bug 根因）
                        sb.Append('{');
                        var firstD = true;
                        foreach (System.Collections.DictionaryEntry kv in idict)
                        {
                            if (!firstD) sb.Append(',');
                            firstD = false;
                            WriteString(sb, kv.Key.ToString());
                            sb.Append(':');
                            WriteValue(sb, kv.Value);
                        }
                        sb.Append('}');
                        break;
                    case List<object> list:
                        sb.Append('[');
                        var f2 = true;
                        foreach (var item in list)
                        {
                            if (!f2) sb.Append(',');
                            f2 = false;
                            WriteValue(sb, item);
                        }
                        sb.Append(']');
                        break;
                    default:
                        WriteString(sb, v.ToString() ?? "");
                        break;
                }
            }

            private static void WriteString(StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }
                }
                sb.Append('"');
            }
        }

        // ---------- Parser ----------
        private class JsonParser
        {
            private readonly string _s;
            private int _pos;

            public JsonParser(string s) { _s = s; _pos = 0; }

            public object ParseValue()
            {
                SkipWs();
                if (_pos >= _s.Length) return null;
                var c = _s[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': _pos += 4; return true;
                    case 'f': _pos += 5; return false;
                    case 'n': _pos += 4; return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                _pos++; // {
                SkipWs();
                if (_pos < _s.Length && _s[_pos] == '}') { _pos++; return dict; }
                while (_pos < _s.Length)
                {
                    SkipWs();
                    var key = ParseString();
                    SkipWs();
                    if (_pos < _s.Length && _s[_pos] == ':') _pos++;
                    dict[key] = ParseValue();
                    SkipWs();
                    if (_pos < _s.Length && _s[_pos] == ',') { _pos++; continue; }
                    if (_pos < _s.Length && _s[_pos] == '}') { _pos++; break; }
                    break;
                }
                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _pos++; // [
                SkipWs();
                if (_pos < _s.Length && _s[_pos] == ']') { _pos++; return list; }
                while (_pos < _s.Length)
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (_pos < _s.Length && _s[_pos] == ',') { _pos++; continue; }
                    if (_pos < _s.Length && _s[_pos] == ']') { _pos++; break; }
                    break;
                }
                return list;
            }

            private string ParseString()
            {
                _pos++; // "
                var sb = new StringBuilder();
                while (_pos < _s.Length)
                {
                    var c = _s[_pos++];
                    if (c == '"') break;
                    if (c == '\\' && _pos < _s.Length)
                    {
                        var esc = _s[_pos++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                var start = _pos;
                while (_pos < _s.Length && "-+.0123456789eE".IndexOf(_s[_pos]) >= 0) _pos++;
                var text = _s.Substring(start, _pos - start);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                return text;
            }

            private void SkipWs()
            {
                while (_pos < _s.Length && char.IsWhiteSpace(_s[_pos])) _pos++;
            }
        }
    }
}
