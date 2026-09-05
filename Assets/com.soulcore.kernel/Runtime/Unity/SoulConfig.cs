using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCore
{
    /// <summary>
    /// 魂核 NPC 配置资产（ScriptableObject）— 一个配置可复用于多个 NPC。
    /// 分组折叠面板：极简 / 人格 / 情绪 / 记忆 / 决策 / 关系 / 存档 / 每日循环 / 模块开关。
    /// </summary>
    [CreateAssetMenu(menuName = "魂核/NPC 配置", fileName = "SoulConfig")]
    public class SoulConfig : ScriptableObject
    {
        [Header("⚡ 极简")]
        [Tooltip("一句话人格描述（如：沉默寡言的渔夫，嘴硬心软）。非空时按关键词推断人格特质。")]
        [TextArea(1, 2)] public string oneLinePersonality = "";

        [Tooltip("人格预设：default / kind / brave / creative / grumpy / selfish 等（见 PersonalityEngine）")]
        public string personalityPreset = "default";

        [Tooltip("默认人格按 NPC ID 自动多样化，避免所有 NPC 人格完全一致")]
        public bool autoDiversify = true;

        [Range(0f, 0.35f), Tooltip("人格多样化强度（建议 0.06~0.15，0=关闭）")]
        public float diversifyStrength = 0.10f;

        [Header("🧠 人格（21 特质，默认折叠）")]
        [Range(0f, 1f), Tooltip("温暖：待人亲切，容易让人放松")] public float warmth = 0.5f;
        [Range(0f, 1f), Tooltip("共情：愿意帮助他人、感同身受")] public float compassion = 0.5f;
        [Range(0f, 1f), Tooltip("信任：容易相信别人（低 = 多疑）")] public float trust = 0.5f;
        [Range(0f, 1f), Tooltip("乐观：看事情往好处想")] public float optimism = 0.5f;
        [Range(0f, 1f), Tooltip("勇气：面对危险不退缩")] public float courage = 0.5f;
        [Range(0f, 1f), Tooltip("好奇：对新事物感兴趣")] public float curiosity = 0.5f;
        [Range(0f, 1f), Tooltip("创造力：点子多、表达有想象力")] public float creativity = 0.5f;
        [Range(0f, 1f), Tooltip("坚韧：认准的事不放弃")] public float persistence = 0.5f;
        [Range(0f, 1f), Tooltip("理性：重逻辑、轻情绪")] public float rationality = 0.5f;
        [Range(0f, 1f), Tooltip("耐心：不急躁、能等")] public float patience = 0.5f;
        [Range(0f, 1f), Tooltip("诚实：不爱撒谎（低 = 爱隐瞒）")] public float honesty = 0.5f;
        [Range(0f, 1f), Tooltip("忠诚：对亲近的人不离不弃")] public float loyalty = 0.5f;
        [Range(0f, 1f), Tooltip("正义感：见不得不公平")] public float justice = 0.5f;
        [Range(0f, 1f), Tooltip("直觉：相信直觉、第六感强")] public float intuition = 0.5f;
        [Range(0f, 1f), Tooltip("抗压：受挫后恢复快")] public float resilience = 0.5f;
        [Range(0f, 1f), Tooltip("活力：精力旺盛、行动力强")] public float energy = 0.5f;
        [Range(0f, 1f), Tooltip("自私：优先考虑自己（高 = 利己）")] public float selfishness = 0.5f;
        [Range(0f, 1f), Tooltip("恐惧倾向：容易害怕（高 = 胆小）")] public float fearTendency = 0.2f;
        [Range(0f, 1f), Tooltip("易怒：容易被激怒")] public float angerTendency = 0.3f;
        [Range(0f, 1f), Tooltip("悲伤倾向：容易低落")] public float sadnessTendency = 0.3f;
        [Range(0f, 1f), Tooltip("希望感：对未来有信心")] public float hopeTendency = 0.5f;

        [Header("💖 情绪（初始值）")]
        [Range(0f, 1f)] public float initialJoy = 0.5f;
        [Range(0f, 1f)] public float initialHope = 0.5f;
        [Range(0f, 1f)] public float initialGratitude = 0.3f;
        [Range(0f, 1f)] public float initialAnxiety = 0.2f;
        [Range(0f, 1f)] public float initialLoneliness = 0.3f;
        [Tooltip("情绪日衰减系数（1=默认速率）")]
        [Min(0.1f)] public float emotionDailyDecayScale = 1f;

        [Header("🧩 记忆")]
        [Tooltip("记忆总容量上限（短期+长期+永久）")]
        [Min(8)] public int maxMemories = 200;

        [Header("⚖️ 决策权重（合计不必为 1，相对生效）")]
        [Range(0.01f, 1f)] public float weightPersonality = 0.25f;
        [Range(0.01f, 1f)] public float weightEmotion = 0.25f;
        [Range(0.01f, 1f)] public float weightMemory = 0.20f;
        [Range(0.01f, 1f)] public float weightResource = 0.30f;

        [Header("🎯 决策情境配置（游戏侧可扩展动作库）")]
        [Tooltip("自定义情境→动作映射（覆盖内置）。不改引擎就能扩 NPC 行为")]
        public List<DecisionSituationOverride> decisionSituations = new List<DecisionSituationOverride>();

        [Header("🕸️ 初始关系")]
        [Tooltip("初始化时写入的关系值（-10 ~ 10）")]
        public List<InitialRelationEntry> initialRelations = new List<InitialRelationEntry>();

        [Header("📦 存档")]
        public bool autoSave = true;
        [Min(10f)] public float autoSaveIntervalSeconds = 120f;
        public string saveFileName = SoulSaveService.DefaultSaveFileName;

        [Header("🔄 每日循环")]
        [Tooltip("多少真实秒等于游戏里的一天")]
        public float secondsPerGameDay = 300f;

        [Header("🎛️ 模块开关（关闭以省算力）")]
        public bool modulePersonality = true;
        public bool moduleEmotion = true;
        public bool moduleMemory = true;
        public bool moduleDecision = true;
        public bool moduleRelationship = true;
        public bool moduleInterest = true;
        public bool moduleCuriosity = true;
        public bool moduleEmotionInfect = true;
        public bool moduleFlavor = true;
        public bool moduleDream = true;

        [Header("🔌 LLM 入口（开放性，可选）")]
        [Tooltip("开启后可在面板用 LLM 把「一句话人格」解析成 21 个特质")]
        public bool llmEnabled = false;
        [Tooltip("OpenAI 兼容接口地址（如 https://api.deepseek.com/v1/chat/completions）")]
        public string llmBaseUrl = "https://api.deepseek.com/v1/chat/completions";
        [Tooltip("API Key（存在资产里，注意不要提交到公开仓库）")]
        public string llmApiKey = "";
        [Tooltip("模型名（如 deepseek-v4-flash / kimi-k2.6 / abab6.5s-chat）")]
        public string llmModel = "deepseek-v4-flash";
        [Tooltip("一句话人格的最大输入字数")]
        [Min(20)] public int llmMaxChars = 120;

        [Header("🌱 人格涌现（经历塑造人格，每日微调）")]
        [Tooltip("启用：NPC 的近期经历（记忆情绪标签）每天微调人格特质——人格是活的")]
        public bool emergenceEnabled = true;
        [Tooltip("变化强度（0=关闭，1=正常，2=剧烈）")]
        [Range(0f, 2f)] public float emergenceStrength = 1.0f;
        [Tooltip("统计最近多少条记忆（窗口）")]
        [Min(5)] public int emergenceWindow = 20;
        [Tooltip("阈值缓动校准：反复被背叛 → 信任特质缓动下降（更难信任，可关）")]
        public bool trustAdaptationEnabled = true;

        [Serializable]
        public class InitialRelationEntry
        {
            public string targetNpcId;
            [Range(-10, 10)] public int value;
        }

        [Serializable]
        public class DecisionSituationOverride
        {
            [Tooltip("情境类型（如 rescue / interrogate_pressure / 自定义）")]
            public string situationType = "default";
            [Tooltip("评分阈值（从高到低，决定动作区间）")]
            public List<float> thresholds = new List<float> { 0.6f };
            [Tooltip("动作列表（对应阈值区间，最后一个为兜底）")]
            public List<string> actions = new List<string> { "proceed", "wait", "avoid" };
        }

        public SoulModuleFlags GetModuleFlags() => new SoulModuleFlags
        {
            personality = modulePersonality,
            emotion = moduleEmotion,
            memory = moduleMemory,
            decision = moduleDecision,
            relationship = moduleRelationship,
            interest = moduleInterest,
            curiosity = moduleCuriosity,
            emotionInfect = moduleEmotionInfect,
            flavor = moduleFlavor,
            dream = moduleDream,
        };

        /// <summary>把配置应用到 Soul 实例（人格/情绪/记忆/决策/关系/模块）</summary>
        public void ApplyTo(Soul soul)
        {
            if (soul == null) return;
            var p = soul.personality;

            // 人格：预设 -> 一句话推断 -> 内联特质 -> 多样化（可复现）
            if (!string.IsNullOrEmpty(personalityPreset))
                p.ApplyPreset(personalityPreset);
            if (!string.IsNullOrEmpty(oneLinePersonality))
                ApplyOneLinePersonality(p, oneLinePersonality);

            p.Set("warmth", warmth);
            p.Set("compassion", compassion);
            p.Set("trust", trust);
            p.Set("optimism", optimism);
            p.Set("courage", courage);
            p.Set("curiosity", curiosity);
            p.Set("creativity", creativity);
            p.Set("persistence", persistence);
            p.Set("rationality", rationality);
            p.Set("patience", patience);
            p.Set("honesty", honesty);
            p.Set("loyalty", loyalty);
            p.Set("justice", justice);
            p.Set("intuition", intuition);
            p.Set("resilience", resilience);
            p.Set("energy", energy);
            p.Set("selfishness", selfishness);
            p.Set("fear_tendency", fearTendency);
            p.Set("anger_tendency", angerTendency);
            p.Set("sadness_tendency", sadnessTendency);
            p.Set("hope_tendency", hopeTendency);

            // 多样化（可复现）：仅在"一句话/预设"路线下生效——
            // 滑条是用户显式配置的精确值，不应被 AutoDiversify 扰动（否则 0.9 会变成 0.83）。
            // 注意：personalityPreset 默认值 "default" 不算"用了预设"（它只是占位）
            var usedTemplate = (!string.IsNullOrEmpty(personalityPreset) && personalityPreset != "default")
                || !string.IsNullOrEmpty(oneLinePersonality);
            if (autoDiversify && usedTemplate && !string.IsNullOrEmpty(soul.id))
                p.AutoDiversify(diversifyStrength, new System.Random(StableHash(soul.id)));

            // 情绪初始值
            soul.emotion.SetEmotion("joy", initialJoy);
            soul.emotion.SetEmotion("hope", initialHope);
            soul.emotion.SetEmotion("gratitude", initialGratitude);
            soul.emotion.SetEmotion("anxiety", initialAnxiety);
            soul.emotion.SetEmotion("loneliness", initialLoneliness);

            // 记忆
            soul.memory.SetMaxMemories(maxMemories);

            // 决策权重
            soul.decision.SetWeights(new Dictionary<string, float>
            {
                ["personality"] = weightPersonality,
                ["emotion"] = weightEmotion,
                ["memory"] = weightMemory,
                ["resource"] = weightResource,
            });

            // 初始关系
            foreach (var r in initialRelations)
            {
                if (string.IsNullOrEmpty(r.targetNpcId)) continue;
                var rel = soul.relationship.GetRelationship(soul.id, r.targetNpcId);
                if (rel != null) rel.value = Mathf.Clamp(r.value, -10, 10);
            }

            // 决策情境覆盖（游戏侧扩展动作库：不改引擎就能扩 NPC 行为）
            if (decisionSituations != null && decisionSituations.Count > 0)
            {
                var map = new Dictionary<string, DecisionEngine.SituationMap>();
                foreach (var o in decisionSituations)
                {
                    if (string.IsNullOrEmpty(o.situationType) || o.actions == null || o.actions.Count == 0) continue;
                    var m = new DecisionEngine.SituationMap();
                    if (o.thresholds != null) m.thresholds.AddRange(o.thresholds);
                    m.actions.AddRange(o.actions);
                    map[o.situationType] = m;
                }
                if (map.Count > 0) soul.decision.SetSituationConfig(map);
            }

            // 模块开关
            soul.modules = GetModuleFlags();

            // 人格涌现（经历塑造人格）
            soul.emergenceEnabled = emergenceEnabled;
            soul.emergenceStrength = emergenceStrength;
            soul.emergenceWindow = emergenceWindow;
            soul.trustAdaptationEnabled = trustAdaptationEnabled;
        }

        /// <summary>一句话人格 -> 关键词推断特质（基础版，1.8.0 可升级为 LLM 推断）</summary>
        private static void ApplyOneLinePersonality(PersonalityEngine p, string line)
        {
            var l = line.ToLowerInvariant();
            if (l.Contains("沉默") || l.Contains("寡言")) { p.Set("warmth", 0.3f); p.Set("curiosity", 0.4f); }
            if (l.Contains("嘴硬心软") || l.Contains("刀子嘴")) { p.Set("warmth", 0.65f); p.Set("honesty", 0.7f); p.Set("compassion", 0.6f); }
            if (l.Contains("开朗") || l.Contains("爱笑")) { p.Set("warmth", 0.7f); p.Set("optimism", 0.7f); p.Set("energy", 0.65f); }
            if (l.Contains("谨慎") || l.Contains("胆小")) { p.Set("fear_tendency", 0.6f); p.Set("courage", 0.3f); p.Set("rationality", 0.6f); }
            if (l.Contains("固执") || l.Contains("倔")) { p.Set("persistence", 0.7f); p.Set("patience", 0.3f); }
            if (l.Contains("善良") || l.Contains("心软")) { p.Set("compassion", 0.7f); p.Set("selfishness", 0.2f); }
            if (l.Contains("精明") || l.Contains("算计")) { p.Set("rationality", 0.65f); p.Set("selfishness", 0.6f); }
            if (l.Contains("好奇") || l.Contains("爱问")) { p.Set("curiosity", 0.75f); p.Set("intuition", 0.55f); }
            if (l.Contains("懒") || l.Contains("悠闲")) { p.Set("energy", 0.35f); p.Set("patience", 0.6f); }
            if (l.Contains("急") || l.Contains("暴躁")) { p.Set("patience", 0.3f); p.Set("anger_tendency", 0.6f); }
            if (l.Contains("仗义") || l.Contains("忠厚")) { p.Set("loyalty", 0.75f); p.Set("justice", 0.65f); }
            if (l.Contains("悲观") || l.Contains("丧")) { p.Set("optimism", 0.3f); p.Set("hope_tendency", 0.3f); }
        }

        /// <summary>确定性字符串哈希（可复现多样化，不受 .NET 字符串哈希随机化影响）</summary>
        private static int StableHash(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }
}
