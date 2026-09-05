using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>传闻（对齐 6.1.8 方案 SoulRumor）</summary>
    [Serializable]
    public class SoulRumor
    {
        public string rumor_id;
        public string event_type;
        public string actor_id;
        public string source_id;      // 传闻来源（上一跳的传播者）
        public int hop;               // 第几跳
        public double timestamp;
        public float intensity = 1.0f;

        /// <summary>传闻是否对 actor 不利（雾港审问用）</summary>
        public bool IsHostileTowardActor()
        {
            switch (event_type)
            {
                case "betray":
                case "lie":
                case "rob":
                case "fight":
                case "insult":
                case "break_promise":
                    return true;
                default:
                    return false;
            }
        }

        public Dictionary<string, object> ToDict() => new Dictionary<string, object>
        {
            ["rumor_id"] = rumor_id,
            ["event_type"] = event_type,
            ["actor_id"] = actor_id,
            ["source_id"] = source_id,
            ["hop"] = hop,
            ["timestamp"] = timestamp,
            ["intensity"] = intensity,
        };

        public static SoulRumor FromDict(Dictionary<string, object> d)
        {
            var r = new SoulRumor();
            if (d == null) return r;
            r.rumor_id = Memory.GetStr(d, "rumor_id", "");
            r.event_type = Memory.GetStr(d, "event_type", "");
            r.actor_id = Memory.GetStr(d, "actor_id", "");
            r.source_id = Memory.GetStr(d, "source_id", "");
            r.hop = Memory.GetInt(d, "hop", 0);
            r.timestamp = Memory.GetDbl(d, "timestamp", 0);
            r.intensity = Memory.GetFlt(d, "intensity", 1.0f);
            return r;
        }
    }
}
