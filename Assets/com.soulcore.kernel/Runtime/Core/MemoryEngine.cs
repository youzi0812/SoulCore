using System;
using System.Collections.Generic;
using System.Linq;

namespace SoulCore
{
    /// <summary>记忆引擎 — 三层记忆池（短期/长期/永久），线索召回 + 遗忘淘汰 + 容量上限（对齐 6.1.8 方案 MemoryEngine）</summary>
    public class MemoryEngine
    {
        private readonly Dictionary<string, List<Memory>> _memories = new Dictionary<string, List<Memory>>
        {
            ["short_term"] = new List<Memory>(),
            ["long_term"] = new List<Memory>(),
            ["permanent"] = new List<Memory>(),
        };

        public Dictionary<string, int> stats = new Dictionary<string, int> { ["total"] = 0, ["forgotten"] = 0 };

        public int maxMemories = 200;

        private int _idSeq = 0;

        public Memory CreateMemory(string content, string type = "event", int importance = 5, string emotion = "")
        {
            var m = new Memory(content, type, importance, emotion);
            m.id = "m" + (_idSeq++).ToString("x4");
            m.created_at = UnixNow();
            return m;
        }

        /// <summary>按重要性自动分层存储</summary>
        public void Store(Memory mem)
        {
            if (mem == null) return;
            if (mem.importance >= 9) _memories["permanent"].Add(mem);
            else if (mem.importance >= 5) _memories["long_term"].Add(mem);
            else _memories["short_term"].Add(mem);
            stats["total"] += 1;
            EnforceCap();
        }

        /// <summary>取最近 count 条记忆（跨三层，按创建时间倒序，供行为树/对话上下文用）</summary>
        public List<Memory> GetRecent(int count)
        {
            var all = new List<Memory>();
            foreach (var bucket in _memories.Values)
                all.AddRange(bucket);
            all.Sort((a, b) => b.created_at.CompareTo(a.created_at));
            if (all.Count <= count) return all;
            return all.GetRange(0, count);
        }

        /// <summary>强制存储（重要性默认 9 = 永久）</summary>
        public Memory StoreForced(string content, int importance = 9, string emotion = "")
        {
            var m = CreateMemory(content, "forced", importance, emotion);
            Store(m);
            return m;
        }

        public void SetMaxMemories(int maxVal)
        {
            maxMemories = Math.Max(8, maxVal);
            EnforceCap();
        }

        public int GetTotalCount()
            => _memories["short_term"].Count + _memories["long_term"].Count + _memories["permanent"].Count;

        public List<Memory> QueryByType(string type, int limit = 20)
        {
            var results = new List<Memory>();
            if (string.IsNullOrEmpty(type) || limit <= 0) return results;
            foreach (var category in new[] { "permanent", "long_term", "short_term" })
            {
                foreach (var memory in _memories[category])
                    if (memory.type == type) results.Add(memory);
                if (results.Count >= limit) break;
            }
            return results.Take(limit).ToList();
        }

        /// <summary>线索召回 — 按相关性排序（关键词/标签匹配）</summary>
        public List<Memory> Recall(string cue, Dictionary<string, object> context = null, int limit = 5)
        {
            var results = new List<Tuple<float, Memory>>();
            var now = UnixNow();
            foreach (var category in new[] { "permanent", "long_term", "short_term" })
            {
                foreach (var memory in _memories[category])
                {
                    var score = CalcRelevance(memory, cue, context);
                    if (score > 0.3f)
                    {
                        memory.Strengthen(now);
                        results.Add(Tuple.Create(score, memory));
                    }
                }
            }
            results.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            return results.Take(limit).Select(t => t.Item2).ToList();
        }

        /// <summary>导出全部记忆（存档用）— [bucket, Memory] 对</summary>
        public List<Tuple<string, Memory>> ExportAllWithBuckets()
        {
            var list = new List<Tuple<string, Memory>>();
            foreach (var category in new[] { "permanent", "long_term", "short_term" })
                foreach (var m in _memories[category])
                    list.Add(Tuple.Create(category, m));
            return list;
        }

        /// <summary>清空并导入（存档恢复）</summary>
        public void ReplaceAll(List<Tuple<string, Memory>> entries)
        {
            ClearAllStored();
            foreach (var entry in entries)
            {
                if (entry == null || entry.Item2 == null) continue;
                var bucket = NormalizeBucket(entry.Item1, entry.Item2.importance);
                _memories[bucket].Add(entry.Item2);
                stats["total"] += 1;
            }
        }

        /// <summary>执行遗忘（衰减 + 移除）</summary>
        public void Forget()
        {
            var now = UnixNow();
            foreach (var category in new[] { "short_term", "long_term" })
            {
                var list = _memories[category];
                var toRemove = new List<Memory>();
                foreach (var memory in list)
                {
                    var last = memory.last_accessed >= 0.0 ? memory.last_accessed : memory.created_at;
                    var days = (now - last) / 86400.0;
                    memory.Decay(days);
                    if (memory.IsForgotten()) toRemove.Add(memory);
                }
                foreach (var memory in toRemove)
                {
                    list.Remove(memory);
                    stats["forgotten"] += 1;
                    stats["total"] -= 1;
                }
            }
        }

        // ==================== 内部 ====================

        private void EnforceCap()
        {
            var guard = 0;
            while (GetTotalCount() > maxMemories && guard < 10000)
            {
                guard += 1;
                if (!TryEvictWeakest()) break;
            }
        }

        private bool TryEvictWeakest()
        {
            foreach (var bucket in new[] { "short_term", "long_term", "permanent" })
            {
                var list = _memories[bucket];
                if (list.Count == 0) continue;
                if (bucket == "permanent")
                {
                    // 永久记忆只淘汰 importance < 9 的
                    var idx = -1;
                    var best = 11;
                    for (var i = 0; i < list.Count; i++)
                        if (list[i].importance < best) { best = list[i].importance; idx = i; }
                    if (idx >= 0 && best < 9) { list.RemoveAt(idx); stats["total"] -= 1; return true; }
                    break;
                }
                // short_term / long_term — 找 strength 最低的
                var weakest = float.MaxValue;
                var wi = 0;
                for (var i = 0; i < list.Count; i++)
                    if (list[i].strength < weakest) { weakest = list[i].strength; wi = i; }
                list.RemoveAt(wi);
                stats["total"] -= 1;
                return true;
            }
            return false;
        }

        private float CalcRelevance(Memory memory, string cue, Dictionary<string, object> context)
        {
            var score = 0.0f;
            if (!string.IsNullOrEmpty(cue))
            {
                // 内容关键词匹配（2 字以上）
                var key = cue.Length > 2 ? cue.Substring(0, Math.Min(cue.Length, 8)) : cue;
                if (memory.content.Contains(cue)) score += 0.5f;
                else if (key.Length >= 2 && memory.content.Contains(key)) score += 0.3f;
            }
            if (context != null && context.TryGetValue("type", out var tv) && tv != null)
                if (memory.type == tv.ToString()) score += 0.3f;
            if (score > 0) score += memory.strength * 0.2f;
            return score;
        }

        private static string NormalizeBucket(string bucket, int importance)
        {
            if (bucket == "permanent" || importance >= 9) return "permanent";
            if (bucket == "long_term" || importance >= 5) return "long_term";
            return "short_term";
        }

        private void ClearAllStored()
        {
            _memories["short_term"].Clear();
            _memories["long_term"].Clear();
            _memories["permanent"].Clear();
            stats["total"] = 0;
            stats["forgotten"] = 0;
        }

        internal static double UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }
}
