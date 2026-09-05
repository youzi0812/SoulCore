using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>
    /// 决策引擎 — 四维加权（人格+情绪+记忆+资源）评分，情境映射到动作，生成中文解释。
    /// 对齐 6.1.8 方案 DecisionEngine：确定性 + 可复现（seed 控制随机扰动）。
    /// </summary>
    public class DecisionEngine
    {
        private readonly Soul _soul;           // Duck Typing：不硬依赖类型（避免循环引用）
        private readonly Random _rng;

        /// <summary>四维权重</summary>
        public Dictionary<string, float> weights = new Dictionary<string, float>
        {
            ["personality"] = 0.25f,
            ["emotion"] = 0.25f,
            ["memory"] = 0.20f,
            ["resource"] = 0.30f,
        };

        /// <summary>决策历史</summary>
        public List<Dictionary<string, object>> history = new List<Dictionary<string, object>>();

        /// <summary>决策四维分解（供调试面板可视化：这个 NPC 为什么选这个动作）</summary>
        public class DecisionBreakdown
        {
            public float personality;
            public float emotion;
            public float memory;
            public float resource;
            public float final;
            public string action;
            public string explanation;
            public string confidence;
        }

        /// <summary>最近一次决策的分解（调试面板读取）</summary>
        public DecisionBreakdown LastBreakdown;

        /// <summary>外部情境配置覆盖（sit_type → thresholds/actions）</summary>
        private Dictionary<string, SituationMap> _situationOverrides = new Dictionary<string, SituationMap>();

        public class SituationMap
        {
            public List<float> thresholds = new List<float>();
            public List<string> actions = new List<string>();
        }

        /// <summary>内置默认情境映射（原版 6 种 + 雾港 4 种审问）</summary>
        private static readonly Dictionary<string, SituationMap> DefaultSituations = BuildDefaults();

        private static Dictionary<string, SituationMap> BuildDefaults()
        {
            var d = new Dictionary<string, SituationMap>();
            d["help"] = Map(new[] { 0.6f }, new[] { "help_others", "self_first" });
            d["conversation"] = Map(new[] { 0.7f, 0.4f }, new[] { "open_up", "listen", "deflect", "silence" });
            d["evacuation"] = Map(new[] { 0.7f, 0.5f, 0.3f }, new[] { "leave_now", "prepare", "wait", "stay" });
            d["rescue"] = Map(new[] { 0.7f, 0.5f }, new[] { "go_rescue", "call_help", "stay_safe" });
            d["learn"] = Map(new[] { 0.5f }, new[] { "learn", "ignore" });
            d["default"] = Map(new[] { 0.6f }, new[] { "proceed", "wait", "avoid" });
            d["interrogate_pressure"] = Map(new[] { 0.7f, 0.4f }, new[] { "confess", "deflect", "silence" });
            d["interrogate_empathy"] = Map(new[] { 0.6f, 0.3f }, new[] { "open_up", "cooperate", "listen" });
            d["interrogate_evidence"] = Map(new[] { 0.65f, 0.35f }, new[] { "confess", "panic", "lie", "break_down" });
            d["interrogate_probe"] = Map(new[] { 0.5f, 0.3f }, new[] { "slip", "alert", "silence" });
            return d;
        }

        private static SituationMap Map(float[] ths, string[] acts)
        {
            var m = new SituationMap();
            m.thresholds.AddRange(ths);
            m.actions.AddRange(acts);
            return m;
        }

        public DecisionEngine(Soul soul = null, int randomSeed = 0)
        {
            _soul = soul;
            _rng = randomSeed == 0 ? new Random() : new Random(randomSeed);
        }

        /// <summary>设置外部情境配置覆盖</summary>
        public void SetSituationConfig(Dictionary<string, SituationMap> config)
        {
            if (config != null) _situationOverrides = config;
        }

        public void SetWeights(Dictionary<string, float> w)
        {
            foreach (var key in new[] { "personality", "emotion", "memory", "resource" })
                if (w.ContainsKey(key)) weights[key] = w[key];
        }

        /// <summary>决策入口，返回 [action, explanation, confidence]</summary>
        public string[] Decide(string sitType, Dictionary<string, object> situation = null, List<Memory> memories = null)
        {
            var map = GetSituationMap(sitType);
            var ths = map.thresholds;
            var acts = map.actions;

            var personalityScore = ScorePersonality(sitType);
            var emotionScore = ScoreEmotion();
            var memoryScore = ScoreMemory(memories);
            var resourceScore = ScoreResource(sitType);

            var finalScore = personalityScore * weights["personality"]
                + emotionScore * weights["emotion"]
                + memoryScore * weights["memory"]
                + resourceScore * weights["resource"];

            // 随机扰动 ±0.02（确定性：由 seed 控制）。
            // 注意：扰动不能太大——±0.1 会把人格差异（±0.025）淹没，导致不同人格决策趋同
            finalScore += (float)_rng.NextDouble() * 0.04f - 0.02f;
            finalScore = Clamp01(finalScore);

            var action = MapToAction(finalScore, ths, acts, out var confidence);
            var explanation = GenerateExplanation(action, finalScore, sitType, situation, memories);
            var goal = InferGoal(sitType, personalityScore, emotionScore, finalScore);
            var bd = new DecisionBreakdown
            {
                personality = personalityScore,
                emotion = emotionScore,
                memory = memoryScore,
                resource = resourceScore,
                final = finalScore,
                action = action,
                explanation = explanation,
                confidence = confidence.ToString("0.00"),
            };
            LastBreakdown = bd;
            history.Add(new Dictionary<string, object> { ["sit"] = sitType, ["action"] = action, ["score"] = finalScore });
            return new[] { action, explanation, confidence.ToString("0.00"), goal };
        }

        private SituationMap GetSituationMap(string sitType)
        {
            if (_situationOverrides.TryGetValue(sitType, out var ov)) return ov;
            if (DefaultSituations.TryGetValue(sitType, out var def)) return def;
            return DefaultSituations["default"];
        }

        private static string MapToAction(float score, List<float> ths, List<string> acts, out float confidence)
        {
            for (var i = 0; i < ths.Count; i++)
            {
                if (score >= ths[i]) { confidence = score; return acts[i]; }
            }
            confidence = score;
            return acts[acts.Count - 1];
        }

        // ==================== 四维评分 ====================

        private float ScorePersonality(string sit)
        {
            if (_soul == null) return 0.5f;
            var t = _soul.personality.traits;
            var score = 0.5f;
            // 幅度说明：人格分差必须足够大（±0.25 级别）才能跨越动作阈值，
            // 否则不同人格的 NPC 最终评分只差 0.05，决策动作完全趋同
            switch (sit)
            {
                case "help":
                    if (GetTrait(t, "compassion", 0.5f) > 0.6f) score += 0.25f;
                    if (GetTrait(t, "selfishness", 0.5f) > 0.6f) score -= 0.25f;
                    break;
                case "conversation":
                    if (GetTrait(t, "warmth", 0.5f) > 0.6f) score += 0.2f;
                    if (GetTrait(t, "trust", 0.5f) > 0.6f) score += 0.1f;
                    if (GetTrait(t, "curiosity", 0.5f) > 0.6f) score += 0.08f;
                    if (GetTrait(t, "selfishness", 0.5f) > 0.62f) score -= 0.12f;
                    if (GetTrait(t, "fear_tendency", 0.2f) > 0.65f) score -= 0.08f;
                    break;
                case "evacuation":
                    if (GetTrait(t, "courage", 0.5f) > 0.6f) score += 0.25f;
                    if (GetTrait(t, "fear_tendency", 0.2f) > 0.6f) score -= 0.25f;
                    if (GetTrait(t, "persistence", 0.5f) > 0.7f) score += 0.1f;
                    break;
                case "rescue":
                    if (GetTrait(t, "courage", 0.5f) > 0.7f) score += 0.3f;
                    if (GetTrait(t, "compassion", 0.5f) > 0.6f) score += 0.15f;
                    if (GetTrait(t, "selfishness", 0.5f) > 0.7f) score -= 0.3f;
                    break;
                case "learn":
                    if (GetTrait(t, "curiosity", 0.5f) > 0.6f) score += 0.25f;
                    if (GetTrait(t, "creativity", 0.5f) > 0.6f) score += 0.1f;
                    break;
                case "interrogate_pressure":
                    if (GetTrait(t, "courage", 0.5f) > 0.65f) score += 0.2f;
                    if (GetTrait(t, "fear_tendency", 0.2f) > 0.6f) score -= 0.2f;
                    if (GetTrait(t, "honesty", 0.5f) > 0.7f) score -= 0.15f;
                    break;
                case "interrogate_empathy":
                    if (GetTrait(t, "warmth", 0.5f) > 0.6f) score += 0.2f;
                    if (GetTrait(t, "trust", 0.5f) > 0.6f) score += 0.1f;
                    break;
                case "interrogate_evidence":
                    if (GetTrait(t, "fear_tendency", 0.2f) > 0.6f) score -= 0.2f;
                    if (GetTrait(t, "honesty", 0.5f) > 0.7f) score -= 0.15f;
                    break;
                case "interrogate_probe":
                    if (GetTrait(t, "courage", 0.5f) > 0.6f) score += 0.15f;
                    if (GetTrait(t, "intuition", 0.5f) > 0.6f) score += 0.15f;
                    break;
            }
            return Clamp01(score);
        }

        private float ScoreEmotion()
        {
            if (_soul == null) return 0.5f;
            var dominant = _soul.emotion.GetDominant();
            switch (dominant)
            {
                case "joy": return 0.65f;
                case "hope": return 0.6f;
                case "gratitude": return 0.6f;
                case "fear": return 0.3f;
                case "anxiety": return 0.35f;
                case "anger": return 0.4f;
                case "sadness": return 0.4f;
                case "despair": return 0.2f;
                default: return 0.5f;
            }
        }

        private float ScoreMemory(List<Memory> memories)
        {
            if (memories == null || memories.Count == 0) return 0.5f;
            var score = 0.5f;
            foreach (var m in memories)
            {
                if (m.type == "betray") score -= 0.2f;
                else if (m.type == "gift") score += 0.15f;
                else if (m.type == "help") score += 0.1f;
                else if (m.type == "insult") score -= 0.15f;
            }
            return Clamp01(score);
        }

        private float ScoreResource(string sit)
        {
            var scarcity = _soul != null ? _soul.GetResourceScarcity() : 0.5f;
            if (sit == "evacuation" || sit == "rescue")
                return 1.0f - scarcity;   // 资源稀缺时更倾向行动
            return scarcity;
        }

        private string GenerateExplanation(string action, float score, string sitType,
            Dictionary<string, object> situation, List<Memory> memories)
        {
            var name = _soul?.name ?? "未知";
            var emotion = _soul != null ? _soul.emotion.GetDominant() : "";
            return string.Format("{0}当前{1}，在{2}情境下选择「{3}」（综合评分{4:0.00}）",
                name, emotion, sitType, action, score);
        }

        /// <summary>目标推断（why）：把四维评分解读成 NPC 此刻想要什么——行为树读它决定 how</summary>
        private static string InferGoal(string sitType, float personalityScore, float emotionScore, float finalScore)
        {
            switch (sitType)
            {
                case "rescue":
                case "evacuation":
                    return finalScore > 0.6f ? "救人/求生" : "自保";
                case "help":
                    return emotionScore > 0.55f ? "助人" : "权衡利弊";
                case "interrogate_pressure":
                case "interrogate_probe":
                    return personalityScore < 0.45f ? "自保/隐瞒" : "坦然应对";
                case "interrogate_empathy":
                    return emotionScore > 0.5f ? "交流/开诚布公" : "谨慎观察";
                case "learn":
                case "conversation":
                    return personalityScore > 0.55f ? "探索/交流" : "维持现状";
                default:
                    return "维持现状";
            }
        }

        private static float GetTrait(Dictionary<string, float> t, string key, float def)
            => t.TryGetValue(key, out var v) ? v : def;

        private static float Clamp01(float v) => Math.Max(0.0f, Math.Min(1.0f, v));
    }
}
