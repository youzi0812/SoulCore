using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>模块开关 — 按模块裁剪功能（对齐 6.1.8 方案 SoulModuleFlags）</summary>
    [Serializable]
    public class SoulModuleFlags
    {
        public bool personality = true;
        public bool emotion = true;
        public bool memory = true;
        public bool decision = true;
        public bool relationship = true;
        public bool interest = true;
        public bool curiosity = true;
        public bool emotionInfect = true;
        public bool flavor = true;
        public bool dream = true;

        public SoulModuleFlags Clone() => (SoulModuleFlags)MemberwiseClone();

        public Dictionary<string, bool> ToDict() => new Dictionary<string, bool>
        {
            ["personality"] = personality,
            ["emotion"] = emotion,
            ["memory"] = memory,
            ["decision"] = decision,
            ["relationship"] = relationship,
            ["interest"] = interest,
            ["curiosity"] = curiosity,
            ["emotion_infect"] = emotionInfect,
            ["flavor"] = flavor,
            ["dream"] = dream,
        };

        public static SoulModuleFlags FromDict(Dictionary<string, object> data)
        {
            var f = new SoulModuleFlags();
            if (data == null) return f;
            f.personality = GetBool(data, "personality", f.personality);
            f.emotion = GetBool(data, "emotion", f.emotion);
            f.memory = GetBool(data, "memory", f.memory);
            f.decision = GetBool(data, "decision", f.decision);
            f.relationship = GetBool(data, "relationship", f.relationship);
            f.interest = GetBool(data, "interest", f.interest);
            f.curiosity = GetBool(data, "curiosity", f.curiosity);
            f.emotionInfect = GetBool(data, "emotion_infect", f.emotionInfect);
            f.flavor = GetBool(data, "flavor", f.flavor);
            f.dream = GetBool(data, "dream", f.dream);
            return f;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            if (d.TryGetValue(key, out var v) && v is bool b) return b;
            return def;
        }
    }
}
