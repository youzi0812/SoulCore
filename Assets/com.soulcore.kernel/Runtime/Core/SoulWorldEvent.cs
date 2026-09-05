using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>世界事件（对齐 6.1.8 方案 SoulWorldEvent）</summary>
    [Serializable]
    public class SoulWorldEvent
    {
        public string event_id;
        public string event_type;
        public string actor_id;
        public string target_id;
        public double timestamp;
        public float intensity = 1.0f;

        public SoulWorldEvent Clone() => new SoulWorldEvent
        {
            event_id = event_id,
            event_type = event_type,
            actor_id = actor_id,
            target_id = target_id,
            timestamp = timestamp,
            intensity = intensity,
        };

        public Dictionary<string, object> ToDict() => new Dictionary<string, object>
        {
            ["event_id"] = event_id,
            ["event_type"] = event_type,
            ["actor_id"] = actor_id,
            ["target_id"] = target_id,
            ["timestamp"] = timestamp,
            ["intensity"] = intensity,
        };

        public static SoulWorldEvent FromDict(Dictionary<string, object> d)
        {
            var e = new SoulWorldEvent();
            if (d == null) return e;
            e.event_id = Memory.GetStr(d, "event_id", "");
            e.event_type = Memory.GetStr(d, "event_type", "");
            e.actor_id = Memory.GetStr(d, "actor_id", "");
            e.target_id = Memory.GetStr(d, "target_id", "");
            e.timestamp = Memory.GetDbl(d, "timestamp", 0);
            e.intensity = Memory.GetFlt(d, "intensity", 1.0f);
            return e;
        }
    }
}
