using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>记忆条目 — NPC 的单条记忆（对齐 6.1.8 方案 Memory）</summary>
    [Serializable]
    public class Memory
    {
        public string id;
        public string content;
        public string type = "event";          // event / forced / conversation / witness / rumor
        public int importance = 5;             // 1~10（≥9 永久、≥5 长期、其余短期）
        public double created_at;
        public double last_accessed = -1.0;    // -1 = 从未访问
        public int access_count;
        public float strength = 1.0f;          // 0~1（<0.1 被遗忘）
        public string emotion = "";
        public List<string> associations = new List<string>();

        /// <summary>关联（key:value 格式，如 "actor:lao_wang"）</summary>
        public void AddAssociation(string key, string value)
        {
            associations.Add(key + ":" + value);
        }

        public Memory() { }

        public Memory(string pContent, string pType = "event", int pImportance = 5, string pEmotion = "")
        {
            content = pContent;
            type = pType;
            importance = Math.Max(1, Math.Min(10, pImportance));
            emotion = pEmotion;
            strength = 1.0f;
            last_accessed = -1.0;
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>被回忆时强化（访问次数 +1、强度 +0.1）</summary>
        public void Strengthen(double now)
        {
            access_count += 1;
            last_accessed = now;
            strength = Math.Min(1.0f, strength + 0.1f);
        }

        /// <summary>随时间衰减（重要性 ≥9 不衰减）</summary>
        public void Decay(double daysPassed)
        {
            if (importance >= 9) return;
            var rate = 0.05 * (10.0 - importance) / 9.0;
            strength = Math.Max(0.0f, strength * (float)(1.0 - rate * daysPassed));
        }

        public bool IsForgotten() => strength < 0.1f;

        public void AddAssociation(string tag)
        {
            if (!associations.Contains(tag)) associations.Add(tag);
        }

        public bool HasAssociation(string prefix, string value)
        {
            var target = prefix + ":" + value;
            foreach (var a in associations)
                if (a == target) return true;
            return false;
        }

        public Dictionary<string, object> ToDict() => new Dictionary<string, object>
        {
            ["id"] = id,
            ["content"] = content,
            ["type"] = type,
            ["importance"] = importance,
            ["created_at"] = created_at,
            ["last_accessed"] = last_accessed,
            ["access_count"] = access_count,
            ["strength"] = strength,
            ["emotion"] = emotion,
            ["associations"] = new List<string>(associations),
        };

        public static Memory FromDict(Dictionary<string, object> d)
        {
            var m = new Memory();
            if (d == null) return m;
            m.id = GetStr(d, "id", m.id);
            m.content = GetStr(d, "content", "");
            m.type = GetStr(d, "type", "event");
            m.importance = GetInt(d, "importance", 5);
            m.created_at = GetDbl(d, "created_at", 0);
            m.last_accessed = GetDbl(d, "last_accessed", -1);
            m.access_count = GetInt(d, "access_count", 0);
            m.strength = (float)GetDbl(d, "strength", 1.0);
            m.emotion = GetStr(d, "emotion", "");
            if (d.TryGetValue("associations", out var av) && av is List<object> al)
                foreach (var item in al) m.associations.Add(item?.ToString() ?? "");
            return m;
        }

        internal static string GetStr(Dictionary<string, object> d, string key, string def)
            => d.TryGetValue(key, out var v) && v != null ? v.ToString() : def;
        internal static int GetInt(Dictionary<string, object> d, string key, int def)
            => d.TryGetValue(key, out var v) && v != null && int.TryParse(v.ToString(), out var i) ? i : def;
        internal static double GetDbl(Dictionary<string, object> d, string key, double def)
            => d.TryGetValue(key, out var v) && v != null && double.TryParse(v.ToString(), out var f) ? f : def;
        internal static float GetFlt(Dictionary<string, object> d, string key, float def)
            => (float)GetDbl(d, key, def);
        internal static bool GetBool(Dictionary<string, object> d, string key, bool def)
            => d.TryGetValue(key, out var v) && v != null && bool.TryParse(v.ToString(), out var b) ? b : def;
    }
}
