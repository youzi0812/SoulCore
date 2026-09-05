using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>传播设置</summary>
    public class SoulWorldPropagationSettings
    {
        public int maxHops = 2;
        public int minRelationToPropagate = 4;
        public float hop2IntensityScale = 0.6f;
        public float hop2RelationDeltaScale = 0.5f;
    }

    /// <summary>
    /// 世界事件总线 — 场景级事件传播核心，支持双模式传播（对齐 6.1.8 方案 SoulWorldEventBus）。
    /// IMMEDIATE：report() 立即全链路 BFS 传播（visited 去重 + maxHops 限制，无死循环）
    /// DEFERRED：report() 只执行直接效果，tick() 每次推进一跳
    /// </summary>
    public class SoulWorldEventBus
    {
        public event Action<SoulWorldEvent> EventReported;
        public event Action<Soul, SoulRumor, SoulWorldEvent> RumorDelivered;

        private readonly Dictionary<string, Soul> _souls = new Dictionary<string, Soul>();
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();
        private readonly List<SoulWorldEvent> _recentEvents = new List<SoulWorldEvent>();

        public SoulWorldPropagationSettings propagation = new SoulWorldPropagationSettings();
        public SoulEnums.PropagationMode propagationMode = SoulEnums.PropagationMode.Immediate;
        public int maxRecentEvents = 32;

        private readonly List<PendingPropagation> _pending = new List<PendingPropagation>();
        private int _eventSeq = 0;

        private class PendingPropagation
        {
            public SoulWorldEvent evt;
            public int currentHop;
            public HashSet<string> visited = new HashSet<string>();
            public List<string> frontier = new List<string>();
        }

        // ==================== 注册 ====================

        public void Register(Soul soul, string displayName = "")
        {
            if (soul == null || string.IsNullOrEmpty(soul.id)) return;
            _souls[soul.id] = soul;
            _displayNames[soul.id] = string.IsNullOrEmpty(displayName) ? soul.name : displayName;
        }

        public void Unregister(string soulId)
        {
            _souls.Remove(soulId);
            _displayNames.Remove(soulId);
        }

        public Soul TryGetSoul(string soulId) => _souls.TryGetValue(soulId, out var s) ? s : null;

        public string ResolveDisplayName(string soulId)
        {
            if (string.IsNullOrEmpty(soulId)) return "某人";
            if (_displayNames.TryGetValue(soulId, out var dn) && !string.IsNullOrEmpty(dn)) return dn;
            if (_souls.TryGetValue(soulId, out var s) && !string.IsNullOrEmpty(s.name)) return s.name;
            return soulId;
        }

        // ==================== 事件报告 ====================

        public SoulWorldEvent Report(string eventType, string actorId, string targetId, float intensity = 1.0f)
        {
            if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(targetId))
                return null;

            intensity = Math.Max(0.1f, Math.Min(2.0f, intensity));
            var evt = new SoulWorldEvent
            {
                event_id = "e" + (_eventSeq++).ToString("x4"),
                event_type = eventType,
                actor_id = actorId,
                target_id = targetId,
                timestamp = MemoryEngine.UnixNow(),
                intensity = intensity,
            };

            // 直接效果（两种模式都执行）
            ApplyDirectEffects(evt);

            if (propagationMode == SoulEnums.PropagationMode.Deferred)
            {
                var pending = new PendingPropagation
                {
                    evt = evt,
                    currentHop = 0,
                };
                pending.visited.Add(actorId);
                pending.visited.Add(targetId);
                pending.frontier.Add(targetId);
                _pending.Add(pending);
            }
            else
            {
                PropagateImmediate(evt);
            }

            PushRecent(evt);
            EventReported?.Invoke(evt);
            return evt;
        }

        /// <summary>DEFERRED 模式：推进一跳传播，返回本次送达的传闻数</summary>
        public int Tick()
        {
            if (propagationMode != SoulEnums.PropagationMode.Deferred) return 0;

            var deliveredCount = 0;
            var settings = propagation;
            var stillPending = new List<PendingPropagation>();

            foreach (var pending in _pending)
            {
                if (pending.currentHop >= settings.maxHops) continue;

                var nextHop = pending.currentHop + 1;
                var newFrontier = new List<string>();

                foreach (var sourceId in pending.frontier)
                {
                    foreach (var listenerId in CollectListeners(sourceId, settings.minRelationToPropagate, pending.visited))
                    {
                        if (pending.visited.Contains(listenerId)) continue;
                        if (!_souls.ContainsKey(listenerId)) continue;
                        pending.visited.Add(listenerId);
                        var listener = _souls[listenerId];
                        DeliverRumor(listener, pending.evt, sourceId, nextHop, settings);
                        deliveredCount += 1;
                        newFrontier.Add(listenerId);
                    }
                }

                pending.frontier = newFrontier;
                pending.currentHop = nextHop;

                if (pending.currentHop < settings.maxHops && newFrontier.Count > 0)
                    stillPending.Add(pending);
            }

            _pending.Clear();
            _pending.AddRange(stillPending);
            return deliveredCount;
        }

        public bool HasPending() => _pending.Count > 0;

        // ==================== 查询 ====================

        public List<SoulRumor> GetRumorsAboutActor(Soul listener, string actorId)
        {
            var results = new List<SoulRumor>();
            if (listener == null || listener.memory == null || string.IsNullOrEmpty(actorId)) return results;
            foreach (var mem in listener.memory.QueryByType("rumor", 32))
            {
                if (mem == null) continue;
                if (!mem.HasAssociation("actor", actorId)) continue;
                results.Add(RumorFromMemory(mem, listener.id));
            }
            results.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
            return results;
        }

        /// <summary>开放式行为：对某对象的社交惩罚分（0~0.15）</summary>
        public float GetSocializePenalty(Soul listener, string targetSoulId)
        {
            if (listener == null || string.IsNullOrEmpty(targetSoulId)) return 0.0f;
            var rumors = GetRumorsAboutActor(listener, targetSoulId);
            if (rumors.Count == 0) return 0.0f;
            var hostileCount = 0;
            foreach (var r in rumors)
                if (r.IsHostileTowardActor()) hostileCount += 1;
            if (hostileCount <= 0) return 0.0f;
            return Math.Max(0.0f, Math.Min(0.15f, 0.05f + hostileCount * 0.03f));
        }

        public List<SoulWorldEvent> GetRecentEvents() => new List<SoulWorldEvent>(_recentEvents);

        // ==================== 存档支持 ====================

        public List<Dictionary<string, object>> ExportEventLog()
        {
            var copy = new List<Dictionary<string, object>>();
            foreach (var evt in _recentEvents) copy.Add(evt.ToDict());
            return copy;
        }

        public void ReplaceEventLog(List<Dictionary<string, object>> events)
        {
            _recentEvents.Clear();
            foreach (var evtData in events)
            {
                var evt = SoulWorldEvent.FromDict(evtData);
                if (evt != null) _recentEvents.Add(evt);
            }
            while (_recentEvents.Count > maxRecentEvents) _recentEvents.RemoveAt(0);
        }

        // ==================== 内部 ====================

        private void ApplyDirectEffects(SoulWorldEvent evt)
        {
            if (!_souls.TryGetValue(evt.target_id, out var target)) return;
            target.Perceive(new PerceptionContext(evt.event_type, evt.intensity,
                evt.event_type + "事件", evt.actor_id));
        }

        private void PropagateImmediate(SoulWorldEvent evt)
        {
            var settings = propagation;
            var maxHops = Math.Max(1, settings.maxHops);
            var visited = new HashSet<string> { evt.actor_id, evt.target_id };
            var frontier = new List<string> { evt.target_id };

            for (var hop = 1; hop <= maxHops; hop++)
            {
                var newFrontier = new List<string>();
                foreach (var sourceId in frontier)
                {
                    foreach (var listenerId in CollectListeners(sourceId, settings.minRelationToPropagate, visited))
                    {
                        visited.Add(listenerId);
                        if (!_souls.TryGetValue(listenerId, out var listener)) continue;
                        DeliverRumor(listener, evt, sourceId, hop, settings);
                        if (hop < maxHops) newFrontier.Add(listenerId);
                    }
                }
                frontier = newFrontier;
                if (frontier.Count == 0) break;
            }
        }

        private List<string> CollectListeners(string sourceId, int minRelation, HashSet<string> visited)
        {
            var results = new List<string>();
            if (!_souls.TryGetValue(sourceId, out var source)) return results;
            foreach (var kv in source.relationship.relationships)
            {
                var agentId = kv.Key;
                if (agentId == sourceId || visited.Contains(agentId)) continue;
                if (!kv.Value.TryGetValue(sourceId, out var rel)) continue;
                if (rel.value >= minRelation) results.Add(agentId);
            }
            return results;
        }

        private void DeliverRumor(Soul listener, SoulWorldEvent evt, string sourceNpcId, int hop, SoulWorldPropagationSettings settings)
        {
            var intensity = evt.intensity;
            if (hop >= 2) intensity *= settings.hop2IntensityScale;

            var rumor = new SoulRumor
            {
                rumor_id = "r" + (_eventSeq++).ToString("x4"),
                event_type = evt.event_type,
                actor_id = evt.actor_id,
                source_id = sourceNpcId,
                hop = hop,
                timestamp = MemoryEngine.UnixNow(),
                intensity = intensity,
            };

            // 传闻写入听者记忆（witness 关联）
            var mem = listener.memory.CreateMemory(
                string.Format("听说：{0}对{1}做了{2}", ResolveDisplayName(evt.actor_id), ResolveDisplayName(evt.target_id), evt.event_type),
                "rumor", 5, "");
            mem.AddAssociation("actor", evt.actor_id);
            mem.AddAssociation("hop", hop.ToString());
            listener.memory.Store(mem);

            // 关系微调（听者对 actor 的态度）
            if (hop >= 2)
            {
                var delta = (int)(evt.intensity * settings.hop2RelationDeltaScale);
                if (evt.event_type is "betray" or "lie" or "rob" or "fight")
                    listener.relationship.Change(listener.id, evt.actor_id, -delta, "传闻:" + evt.event_type);
            }

            RumorDelivered?.Invoke(listener, rumor, evt);
        }

        private SoulRumor RumorFromMemory(Memory mem, string listenerId)
        {
            var r = new SoulRumor();
            r.rumor_id = mem.id;
            r.event_type = mem.type;
            foreach (var a in mem.associations)
            {
                if (a.StartsWith("actor:")) r.actor_id = a.Substring(6);
                else if (a.StartsWith("hop:")) int.TryParse(a.Substring(4), out r.hop);
            }
            r.timestamp = mem.created_at;
            return r;
        }

        private void PushRecent(SoulWorldEvent evt)
        {
            _recentEvents.Add(evt);
            while (_recentEvents.Count > maxRecentEvents) _recentEvents.RemoveAt(0);
        }
    }
}
