using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>关系引擎 — NPC 间 -10~10 的关系值管理，事件驱动 + 人格影响 + 日衰减（对齐 6.1.8 方案 RelationshipEngine）</summary>
    public class RelationshipEngine
    {
        /// <summary>关系条目</summary>
        public class Relationship
        {
            public string target_id;
            public int value;
            public List<Dictionary<string, object>> history = new List<Dictionary<string, object>>();
            public Dictionary<string, object> last_change = new Dictionary<string, object>();

            public string GetLevelName()
            {
                var levelNames = new Dictionary<int, string>
                {
                    [-10] = "死敌", [-8] = "仇敌", [-6] = "憎恨", [-4] = "厌恶", [-2] = "不满",
                    [0] = "陌生",
                    [2] = "友善", [4] = "好感", [6] = "信赖", [8] = "挚友", [10] = "灵魂伴侣",
                };
                var keys = new List<int>(levelNames.Keys);
                keys.Sort((a, b) => b.CompareTo(a));
                foreach (var th in keys)
                    if (value >= th) return levelNames[th];
                return "陌生";
            }
        }

        public bool enablePersonalityEffect = true;
        public Dictionary<string, int> stats = new Dictionary<string, int> { ["total_changes"] = 0, ["threshold_triggers"] = 0 };

        /// <summary>关系表：agentId → { targetId → Relationship }</summary>
        public Dictionary<string, Dictionary<string, Relationship>> relationships = new Dictionary<string, Dictionary<string, Relationship>>();

        /// <summary>默认事件变化量表</summary>
        public static readonly Dictionary<string, int> DefaultEvents = new Dictionary<string, int>
        {
            ["help"] = 1, ["rescue"] = 2, ["gift"] = 1, ["praise"] = 1, ["keep_promise"] = 1,
            ["betray"] = -2, ["insult"] = -1, ["lie"] = -1, ["break_promise"] = -2, ["ignore_help"] = -1,
            ["rob"] = -2, ["fight"] = -2, ["share"] = 1, ["protect"] = 2,
            ["teach"] = 1, ["learn_from"] = 1, ["disagree"] = -1, ["agree"] = 1,
        };

        public Relationship GetRelationship(string agentId, string targetId)
        {
            if (!relationships.TryGetValue(agentId, out var map))
            {
                map = new Dictionary<string, Relationship>();
                relationships[agentId] = map;
            }
            if (!map.TryGetValue(targetId, out var r))
            {
                r = new Relationship { target_id = targetId, value = 0 };
                map[targetId] = r;
            }
            return r;
        }

        public int GetValue(string agentId, string targetId)
            => GetRelationship(agentId, targetId).value;

        public string GetLevelName(string agentId, string targetId)
            => GetRelationship(agentId, targetId).GetLevelName();

        /// <summary>应用事件（查表获取 delta 再 change）</summary>
        public Dictionary<string, object> ApplyEvent(string agentId, string targetId, string eventType,
            Dictionary<string, float> personality = null)
        {
            if (!DefaultEvents.TryGetValue(eventType, out var delta) || delta == 0)
                return new Dictionary<string, object> { ["old"] = GetValue(agentId, targetId), ["new"] = GetValue(agentId, targetId), ["delta"] = 0 };
            return Change(agentId, targetId, delta, "事件:" + eventType, personality);
        }

        /// <summary>修改关系值（含人格调制）</summary>
        public Dictionary<string, object> Change(string agentId, string targetId, int delta, string reason,
            Dictionary<string, float> personality = null)
        {
            var rel = GetRelationship(agentId, targetId);
            var oldV = rel.value;

            // 人格调制（幅度影响）
            var finalDelta = delta;
            if (enablePersonalityEffect && personality != null)
            {
                if (delta > 0 && GetTrait(personality, "warmth", 0.5f) > 0.6f) finalDelta += 1;
                if (delta < 0 && GetTrait(personality, "anger_tendency", 0.2f) > 0.6f) finalDelta -= 1;
                if (delta < 0 && GetTrait(personality, "forgiveness", 0.5f) > 0.7f) finalDelta += 1;
            }

            rel.value = Math.Max(-10, Math.Min(10, rel.value + finalDelta));
            rel.last_change = new Dictionary<string, object> { ["delta"] = finalDelta, ["reason"] = reason, ["old"] = oldV };
            rel.history.Add(rel.last_change);
            stats["total_changes"] += 1;

            // 阈值触发检测（跨过 ±4 或 ±8 时记录）
            if ((oldV < 4 && rel.value >= 4) || (oldV > -4 && rel.value <= -4)
                || (oldV < 8 && rel.value >= 8) || (oldV > -8 && rel.value <= -8))
                stats["threshold_triggers"] += 1;

            return new Dictionary<string, object> { ["old"] = oldV, ["new"] = rel.value, ["delta"] = finalDelta, ["reason"] = reason };
        }

        /// <summary>日更新（少量向 0 回归）</summary>
        public void DailyUpdate(Random rng)
        {
            foreach (var map in relationships.Values)
            {
                foreach (var rel in map.Values)
                {
                    if (rel.value == 0) continue;
                    var direction = rel.value > 0 ? -1 : 1;
                    if (rng.NextDouble() < 0.3)
                        rel.value = Math.Max(-10, Math.Min(10, rel.value + direction));
                }
            }
        }

        public int CountForAgent(string agentId)
            => relationships.TryGetValue(agentId, out var map) ? map.Count : 0;

        /// <summary>导出某人的全部关系边（存档用）</summary>
        public List<Dictionary<string, object>> ExportEdgesForAgent(string agentId)
        {
            var list = new List<Dictionary<string, object>>();
            if (relationships.TryGetValue(agentId, out var map))
                foreach (var kv in map)
                    list.Add(new Dictionary<string, object> { ["target_id"] = kv.Key, ["value"] = kv.Value.value, ["last_change_reason"] = kv.Value.last_change.TryGetValue("reason", out var r) ? (r?.ToString() ?? "") : "" });
            return list;
        }

        /// <summary>替换某人的全部关系边（存档恢复）</summary>
        public void ReplaceEdgesForAgent(string agentId, List<Dictionary<string, object>> edges)
        {
            if (relationships.TryGetValue(agentId, out var map)) map.Clear();
            foreach (var e in edges)
            {
                var targetId = Memory.GetStr(e, "target_id", "");
                if (targetId.Length == 0) continue;
                var r = GetRelationship(agentId, targetId);
                r.value = Math.Max(-10, Math.Min(10, Memory.GetInt(e, "value", 0)));
            }
        }

        private static float GetTrait(Dictionary<string, float> p, string key, float def)
            => p.TryGetValue(key, out var v) ? v : def;
    }
}
