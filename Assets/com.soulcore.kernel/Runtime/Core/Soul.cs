using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>
    /// 魂核主体 — 一个 NPC 的完整灵魂实例。
    /// 整合五大引擎 + 子系统 + 风味系统，提供 Perceive（感知处理）、
    /// DailyReset（每日重置）、Explain（状态解释）等核心方法。
    /// 纯逻辑类（不依赖 UnityEngine），对齐 6.1.8 方案 Soul。
    /// </summary>
    public class Soul
    {
        // ==================== 事件 ====================
        public event Action<string, float, float> EmotionChanged;
        public event Action<Memory> MemoryStored;
        public event Action<string, int, int, string> RelationshipChanged;
        public event Action<SoulDecision> DecisionMade;

        // ==================== 属性 ====================
        public string id;
        public string name;
        public double created_at;
        public bool is_alive = true;

        // 五大引擎
        public PersonalityEngine personality;
        public EmotionEngine emotion;
        public MemoryEngine memory;
        public DecisionEngine decision;
        public RelationshipEngine relationship;

        // 子系统
        public InterestSystem interest;
        public CuriositySystem curiosity;
        public EmotionInfectionSystem emotion_infection;
        public FlavorSystem flavor;

        public Dictionary<string, int> stats = new Dictionary<string, int>
        {
            ["experience_count"] = 0, ["decision_count"] = 0, ["daily_interactions"] = 0,
        };

        /// <summary>资源稀缺度回调（返回 0~1）</summary>
        public Func<float> resourceScarcityProvider;

        public SoulModuleFlags modules = new SoulModuleFlags();

        public float emotionDailyDecayScale = 1.0f;

        // 人格涌现：经历（记忆情绪标签）每天微调人格——人格是活的，不是定死的
        public bool emergenceEnabled = true;
        public float emergenceStrength = 1.0f;   // 0=关闭 1=正常 2=剧烈
        public int emergenceWindow = 20;          // 统计最近 N 条记忆

        // 阈值缓动校准：反复被背叛 → 信任阈值自动提高（更难信任，可关）
        public bool trustAdaptationEnabled = true;

        /// <summary>日志回调（Core 层不依赖 UnityEngine，由宿主层注册到 Debug.Log）</summary>
        public System.Action<string> OnLog;

        public SoulEnums.FlavorOutputMode flavorOutput = SoulEnums.FlavorOutputMode.BuiltInTemplates;

        private bool _running = true;
        private readonly Random _rng;

        public Soul(string citizenId = "", string pName = "", int randomSeed = 0)
        {
            id = citizenId;
            name = pName;
            created_at = MemoryEngine.UnixNow();
            _rng = randomSeed == 0 ? new Random() : new Random(randomSeed);

            personality = new PersonalityEngine();
            emotion = new EmotionEngine();
            memory = new MemoryEngine();
            relationship = new RelationshipEngine();
            interest = new InterestSystem();
            curiosity = new CuriositySystem();
            emotion_infection = new EmotionInfectionSystem(this);
            flavor = new FlavorSystem(this);
            decision = new DecisionEngine(this, randomSeed);

            // 诞生记忆
            memory.StoreForced("魂核" + name + "诞生", 5);
        }

        internal Random GetRng() => _rng;

        public float GetResourceScarcity()
            => resourceScarcityProvider != null ? Math.Max(0.0f, Math.Min(1.0f, resourceScarcityProvider())) : 0.5f;

        // ==================== 情绪控制 API ====================

        public void SetEmotion(string key, float value)
        {
            if (!emotion.emotions.ContainsKey(key)) return;
            var oldVal = emotion.emotions[key];
            emotion.SetEmotion(key, value);
            if (!NearlyEqual(oldVal, value)) EmotionChanged?.Invoke(key, oldVal, value);
        }

        public void SetEmotions(Dictionary<string, float> dict)
        {
            foreach (var kv in dict)
                if (emotion.emotions.ContainsKey(kv.Key))
                    SetEmotion(kv.Key, kv.Value);
        }

        public void AddEmotion(string key, float delta)
        {
            if (!emotion.emotions.ContainsKey(key)) return;
            var oldVal = emotion.emotions[key];
            emotion.AddEmotion(key, delta);
            var newVal = emotion.emotions[key];
            if (!NearlyEqual(oldVal, newVal)) EmotionChanged?.Invoke(key, oldVal, newVal);
        }

        // ==================== 核心方法 ====================

        /// <summary>感知处理 — 核心入口，接收外界刺激，输出决策</summary>
        public SoulDecision Perceive(PerceptionContext ctx)
        {
            var result = new SoulDecision();

            if (!is_alive)
            {
                result.action = "silence";
                result.explanation = name + "已经不在人世";
                DecisionMade?.Invoke(result);
                return result;
            }

            var M = modules;
            stats["experience_count"] += 1;

            // 1. 情绪更新 + 感染
            if (M.emotion)
            {
                var oldEmotions = new Dictionary<string, float>(emotion.emotions);
                emotion.Update(ctx.event_type, ctx.intensity, ctx.content, personality.traits);
                if (M.emotionInfect && !string.IsNullOrEmpty(ctx.user_emotion))
                    emotion_infection.Infect(ctx.user_emotion, ctx.intensity);
                EmitEmotionChanges(oldEmotions);
            }

            // 2. 兴趣更新
            if (M.interest && !string.IsNullOrEmpty(ctx.content))
                interest.UpdateTopic(ctx.content, 0.05f);

            // 3. 关系更新
            if (M.relationship && !string.IsNullOrEmpty(ctx.target_id))
            {
                var oldVal = relationship.GetValue(id, ctx.target_id);
                var record = relationship.ApplyEvent(id, ctx.target_id, ctx.event_type, personality.traits);
                var newVal = Convert.ToInt32(record["new"]);
                if (newVal != oldVal)
                    RelationshipChanged?.Invoke(ctx.target_id, oldVal, newVal, record.TryGetValue("reason", out var r) ? (r?.ToString() ?? "") : "");
            }

            // 4. 记忆存储 + 召回
            var recalledMemories = new List<Memory>();
            if (M.memory)
            {
                var memContent = !string.IsNullOrEmpty(ctx.content)
                    ? ctx.content
                    : string.Format("{0}事件，强度{1:0.0}", ctx.event_type, ctx.intensity);
                var mem = memory.CreateMemory(memContent, ctx.event_type,
                    ImportanceForEvent(ctx.event_type), emotion.GetDominant());
                memory.Store(mem);
                MemoryStored?.Invoke(mem);
                recalledMemories = memory.Recall(ctx.content, new Dictionary<string, object> { ["type"] = ctx.event_type }, 5);
            }

            // 5. 决策
            string[] decisionResult;
            if (M.decision)
            {
                var situation = new Dictionary<string, object> { ["content"] = ctx.content, ["event_type"] = ctx.event_type };
                decisionResult = decision.Decide(SitTypeForEvent(ctx.event_type), situation, recalledMemories);
                stats["decision_count"] += 1;
            }
            else
            {
                decisionResult = new[] { "proceed", "", "0.5", "维持现状" };
            }

            result.action = decisionResult[0];
            result.explanation = decisionResult[1];
            result.confidence = float.TryParse(decisionResult[2], out var conf) ? conf : 0.5f;
            if (decisionResult.Length > 3)
                result.goal = decisionResult[3];
            result.emotion = emotion.GetDominant();
            result.emotion_intensity = emotion.GetIntensity();
            result.relevant_memories = recalledMemories;

            // 6. 风味输出
            if (M.flavor && flavorOutput != SoulEnums.FlavorOutputMode.Silent)
                result.flavor = flavor.GenerateFlavor(ctx.content);

            // 7. 好奇心
            if (M.curiosity)
            {
                var q = curiosity.GenerateQuestion();
                if (!string.IsNullOrEmpty(q)) result.flavor["curiosity_question"] = q;
            }

            stats["daily_interactions"] += 1;
            DecisionMade?.Invoke(result);
            return result;
        }

        /// <summary>每日重置 — 情绪衰减、兴趣衰减、关系日变化、记忆遗忘</summary>
        public void DailyReset()
        {
            var M = modules;
            if (M.emotion) emotion.Decay(24.0f * emotionDailyDecayScale);
            if (M.interest) interest.Decay();
            if (M.curiosity) curiosity.UpdateCuriosity(0.05f);
            if (M.relationship) relationship.DailyUpdate(_rng);
            // 人格涌现：先统计近期经历（在记忆遗忘前），再微调人格——经历塑造人格
            if (emergenceEnabled) ApplyEmergence();
            if (M.memory) memory.Forget();
        }

        /// <summary>人格涌现：统计最近记忆的情绪标签，微调人格特质</summary>
        private void ApplyEmergence()
        {
            var recent = memory.GetRecent(emergenceWindow);
            var counts = new Dictionary<string, int>();
            foreach (var m in recent)
            {
                if (string.IsNullOrEmpty(m.emotion)) continue;
                var key = m.emotion.ToLowerInvariant();
                counts[key] = counts.TryGetValue(key, out var v) ? v + 1 : 1;
            }
            if (counts.Count > 0)
            {
                // 快照关键特质（供日志对比：人格涌现生效可见）
                var keys = new[] { "courage", "compassion", "trust", "honesty", "anger_tendency", "fear_tendency" };
                var before = new Dictionary<string, float>();
                foreach (var k in keys)
                    before[k] = personality.traits.TryGetValue(k, out var v) ? v : 0.5f;

                personality.ApplyLifeFeedback(counts, emergenceStrength);

                // 变化日志（宿主层注册到 Debug.Log，Console 可见）
                var changes = new List<string>();
                foreach (var k in keys)
                {
                    var after = personality.traits.TryGetValue(k, out var v) ? v : 0.5f;
                    if (Math.Abs(after - before[k]) > 0.001f)
                        changes.Add(string.Format("{0}: {1:0.00} → {2:0.00}", k, before[k], after));
                }
                if (changes.Count > 0)
                    OnLog?.Invoke(string.Format("[SoulCore] 人格涌现：{0}（经历 {1} 条记忆）",
                        string.Join(" | ", changes), recent.Count));
            }

            // 阈值缓动校准：反复被背叛 → 信任特质缓动下降（更难信任，可关）
            // 这是"缓动 + 可关"，区别于纯随机漂移
            if (trustAdaptationEnabled && counts.TryGetValue("betray", out var betrayCount) && betrayCount > 0)
            {
                var drop = 0.02f * Math.Min(betrayCount, 10) * emergenceStrength;
                var cur = personality.traits.TryGetValue("trust", out var tv) ? tv : 0.5f;
                personality.traits["trust"] = Math.Max(0.05f, cur - drop);
                OnLog?.Invoke(string.Format("[SoulCore] 阈值缓动：背叛经历 {0} 次，信任 {1:0.00} → {2:0.00}",
                    betrayCount, cur, personality.traits["trust"]));
            }
        }

        public void Stop()
        {
            _running = false;
            if (modules.memory) memory.Forget();
        }

        public bool IsRunning() => _running;

        // ==================== 状态查询 ====================

        public Dictionary<string, object> GetProfile() => new Dictionary<string, object>
        {
            ["id"] = id,
            ["name"] = name,
            ["is_alive"] = is_alive,
            ["traits"] = new Dictionary<string, float>(personality.traits),
            ["emotions"] = new Dictionary<string, float>(emotion.emotions),
            ["dominant_emotion"] = emotion.GetDominant(),
            ["emotion_intensity"] = emotion.GetIntensity(),
            ["stats"] = new Dictionary<string, int>(stats),
            ["memory_count"] = memory.stats["total"],
            ["relationship_count"] = relationship.CountForAgent(id),
            ["curiosity_level"] = curiosity.curiosityLevel,
        };

        public string Explain()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== " + name + "的灵魂 ===");
            sb.AppendLine("【人格】");
            var keys = new List<string>(personality.traits.Keys);
            keys.Sort();
            var count = 0;
            foreach (var key in keys)
            {
                if (count >= 10) break;
                count++;
                var v = personality.traits[key];
                var n = (int)(v * 10);
                sb.AppendLine(string.Format("  {0,-16} {1} {2:0.00}", key, new string('█', n) + new string('░', 10 - n), v));
            }
            sb.AppendLine();
            sb.AppendLine("【情感】");
            foreach (var kv in emotion.emotions)
                if (kv.Value > 0.3f)
                    sb.AppendLine(string.Format("  {0} {1,-18} {2:0.00}", kv.Key == emotion.GetDominant() ? "★" : "·", kv.Key, kv.Value));
            sb.AppendLine();
            sb.AppendLine("【记忆】 " + memory.stats["total"] + " 条");
            sb.AppendLine("【关系】 " + relationship.CountForAgent(id) + " 个");
            return sb.ToString();
        }

        // ==================== 存档 ====================

        public Dictionary<string, object> ExportSnapshot() => new Dictionary<string, object>
        {
            ["id"] = id,
            ["name"] = name,
            ["is_alive"] = is_alive,
            ["experience_count"] = stats["experience_count"],
            ["decision_count"] = stats["decision_count"],
            ["daily_interactions"] = stats["daily_interactions"],
            ["traits"] = new Dictionary<string, float>(personality.traits),
            ["emotions"] = new Dictionary<string, float>(emotion.emotions),
            ["modules"] = modules.ToDict(),
        };

        public void ApplySnapshot(Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return;
            is_alive = Memory.GetBool(data, "is_alive", true);
            stats["experience_count"] = Memory.GetInt(data, "experience_count", 0);
            stats["decision_count"] = Memory.GetInt(data, "decision_count", 0);
            stats["daily_interactions"] = Memory.GetInt(data, "daily_interactions", 0);

            if (data.TryGetValue("traits", out var tv) && tv is Dictionary<string, object> traits)
                foreach (var kv in traits)
                {
                    if (kv.Value == null) continue;
                    // 宽容解析：快照里有就完整恢复（缺失 key 也添加），数字用不变文化（防小数点位差异）
                    if (float.TryParse(kv.Value.ToString(), System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out var f))
                        personality.traits[kv.Key] = Math.Max(0f, Math.Min(1f, f));
                }

            if (data.TryGetValue("emotions", out var ev) && ev is Dictionary<string, object> emotions)
                foreach (var kv in emotions)
                {
                    if (kv.Value == null) continue;
                    if (float.TryParse(kv.Value.ToString(), System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out var f))
                        emotion.emotions[kv.Key] = Math.Max(0f, Math.Min(1f, f));
                }

            if (data.TryGetValue("modules", out var mv) && mv is Dictionary<string, object> modulesDict)
                modules = SoulModuleFlags.FromDict(modulesDict);
        }

        // ==================== 内部 ====================

        private void EmitEmotionChanges(Dictionary<string, float> oldEmotions)
        {
            foreach (var kv in oldEmotions)
            {
                var newVal = emotion.emotions[kv.Key];
                if (!NearlyEqual(kv.Value, newVal))
                    EmotionChanged?.Invoke(kv.Key, kv.Value, newVal);
            }
        }

        private static int ImportanceForEvent(string eventType)
        {
            switch (eventType)
            {
                case "betray": return 8;
                case "rescued": return 7;
                case "disaster": return 7;
                case "gift": return 5;
                case "help": return 4;
                default: return 4;
            }
        }

        private static string SitTypeForEvent(string eventType)
        {
            switch (eventType)
            {
                case "help": return "help";
                case "conversation": return "conversation";
                case "evacuation": return "evacuation";
                case "rescue": return "rescue";
                case "learn": return "learn";
                case "pressure": return "interrogate_pressure";
                case "empathy": return "interrogate_empathy";
                case "show_evidence": return "interrogate_evidence";
                case "probe": return "interrogate_probe";
                default: return "default";
            }
        }

        private static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 0.001f;
    }
}
