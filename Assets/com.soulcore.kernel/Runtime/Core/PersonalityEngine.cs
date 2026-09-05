using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>人格引擎 — 21 项人格特质（0~1）的管理与预设（对齐 6.1.8 方案 PersonalityEngine）</summary>
    public class PersonalityEngine
    {
        // --- 必填 8 项（Profile 滑条）---
        public const string TRAIT_WARMTH = "warmth";
        public const string TRAIT_COMPASSION = "compassion";
        public const string TRAIT_TRUST = "trust";
        public const string TRAIT_OPTIMISM = "optimism";
        public const string TRAIT_COURAGE = "courage";
        public const string TRAIT_CURIOSITY = "curiosity";
        public const string TRAIT_SELFISHNESS = "selfishness";
        public const string TRAIT_FEAR_TENDENCY = "fear_tendency";

        // --- 情境 7 项（决策引擎读取）---
        public const string TRAIT_PATIENCE = "patience";
        public const string TRAIT_RATIONALITY = "rationality";
        public const string TRAIT_CREATIVITY = "creativity";
        public const string TRAIT_PERSISTENCE = "persistence";
        public const string TRAIT_ENERGY = "energy";
        public const string TRAIT_HONESTY = "honesty";
        public const string TRAIT_JUSTICE = "justice";

        // --- 个性 6 项（深度定制）---
        public const string TRAIT_LOYALTY = "loyalty";
        public const string TRAIT_ANGER_TENDENCY = "anger_tendency";
        public const string TRAIT_SADNESS_TENDENCY = "sadness_tendency";
        public const string TRAIT_HOPE_TENDENCY = "hope_tendency";
        public const string TRAIT_INTUITION = "intuition";
        public const string TRAIT_RESILIENCE = "resilience";

        public static readonly string[] AllTraits =
        {
            TRAIT_WARMTH, TRAIT_COMPASSION, TRAIT_TRUST, TRAIT_OPTIMISM,
            TRAIT_COURAGE, TRAIT_CURIOSITY, TRAIT_SELFISHNESS, TRAIT_FEAR_TENDENCY,
            TRAIT_PATIENCE, TRAIT_RATIONALITY, TRAIT_CREATIVITY, TRAIT_PERSISTENCE,
            TRAIT_ENERGY, TRAIT_HONESTY, TRAIT_JUSTICE,
            TRAIT_LOYALTY, TRAIT_ANGER_TENDENCY, TRAIT_SADNESS_TENDENCY,
            TRAIT_HOPE_TENDENCY, TRAIT_INTUITION, TRAIT_RESILIENCE,
        };

        /// <summary>特质表：key → 0~1</summary>
        public Dictionary<string, float> traits = new Dictionary<string, float>();

        public PersonalityEngine()
        {
            // 默认中性预设（全部 0.5）
            foreach (var t in AllTraits)
                traits[t] = 0.5f;
        }

        /// <summary>应用预设（default / kind / brave / selfish / creative / grumpy）</summary>
        public void ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "kind":
                    Set(TRAIT_WARMTH, 0.8f); Set(TRAIT_COMPASSION, 0.8f);
                    Set(TRAIT_TRUST, 0.7f); Set(TRAIT_SELFISHNESS, 0.2f);
                    Set(TRAIT_ANGER_TENDENCY, 0.2f);
                    break;
                case "brave":
                    Set(TRAIT_COURAGE, 0.8f); Set(TRAIT_FEAR_TENDENCY, 0.2f);
                    Set(TRAIT_PERSISTENCE, 0.7f); Set(TRAIT_RESILIENCE, 0.7f);
                    break;
                case "selfish":
                    Set(TRAIT_SELFISHNESS, 0.8f); Set(TRAIT_COMPASSION, 0.2f);
                    Set(TRAIT_WARMTH, 0.2f); Set(TRAIT_TRUST, 0.3f);
                    break;
                case "creative":
                    Set(TRAIT_CREATIVITY, 0.8f); Set(TRAIT_CURIOSITY, 0.8f);
                    Set(TRAIT_INTUITION, 0.7f);
                    break;
                case "grumpy":
                    Set(TRAIT_ANGER_TENDENCY, 0.8f); Set(TRAIT_WARMTH, 0.2f);
                    Set(TRAIT_OPTIMISM, 0.3f); Set(TRAIT_HOPE_TENDENCY, 0.3f);
                    break;
            }
        }

        /// <summary>默认预设自动离散化（避免同质化）</summary>
        public void AutoDiversify(float strength, Random rng)
        {
            foreach (var t in AllTraits)
            {
                var delta = (float)((rng.NextDouble() * 2 - 1) * strength);
                traits[t] = Clamp01(traits[t] + delta);
            }
        }

        public void Set(string key, float value)
        {
            // camelCase 别名 → 下划线（SoulConfig 面板字段是 camelCase，traits 键是下划线）
            if (Alias.TryGetValue(key, out var real)) key = real;
            traits[key] = Clamp01(value);   // 有则更新，无则添加（防御 traits 缺键）
        }

        public float Get(string key, float def = 0.5f)
        {
            // 读取同样支持 camelCase 别名（外部代码不用关心键名风格）
            if (Alias.TryGetValue(key, out var real)) key = real;
            return traits.TryGetValue(key, out var v) ? v : def;
        }

        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            ["fearTendency"] = TRAIT_FEAR_TENDENCY,
            ["angerTendency"] = TRAIT_ANGER_TENDENCY,
            ["sadnessTendency"] = TRAIT_SADNESS_TENDENCY,
            ["hopeTendency"] = TRAIT_HOPE_TENDENCY,
        };

        /// <summary>
        /// 人格涌现：按近期记忆的情绪标签统计，微调特质（经历塑造人格）。
        /// emotionCounts: 情绪标签 → 出现次数；strength: 变化强度（1 = 正常）。
        /// 变化被 clamp 在 0.05~0.95，避免人格走到极端。
        /// </summary>
        public void ApplyLifeFeedback(Dictionary<string, int> emotionCounts, float strength)
        {
            foreach (var kv in emotionCounts)
            {
                var n = kv.Value;
                if (n <= 0) continue;
                switch (kv.Key.ToLowerInvariant())
                {
                    case "joy":
                    case "gratitude":
                    case "hope":
                        Shift(TRAIT_WARMTH, 0.01f * n * strength);
                        Shift(TRAIT_OPTIMISM, 0.01f * n * strength);
                        Shift(TRAIT_HOPE_TENDENCY, 0.01f * n * strength);
                        break;
                    case "fear":
                    case "anxiety":
                        Shift(TRAIT_FEAR_TENDENCY, 0.02f * n * strength);
                        Shift(TRAIT_COURAGE, -0.01f * n * strength);
                        break;
                    case "anger":
                        Shift(TRAIT_ANGER_TENDENCY, 0.02f * n * strength);
                        Shift(TRAIT_PATIENCE, -0.01f * n * strength);
                        break;
                    case "sadness":
                    case "loneliness":
                        Shift(TRAIT_SADNESS_TENDENCY, 0.02f * n * strength);
                        Shift(TRAIT_OPTIMISM, -0.01f * n * strength);
                        break;
                    case "betray":
                    case "betrayal":
                        Shift(TRAIT_TRUST, -0.02f * n * strength);
                        Shift(TRAIT_FEAR_TENDENCY, 0.01f * n * strength);
                        break;
                    case "proud":
                        Shift(TRAIT_HOPE_TENDENCY, 0.01f * n * strength);
                        break;
                }
            }
        }

        private void Shift(string key, float delta)
        {
            if (!traits.ContainsKey(key)) return;
            traits[key] = Math.Max(0.05f, Math.Min(0.95f, traits[key] + delta));
        }

        private static float Clamp01(float v) => Math.Max(0.0f, Math.Min(1.0f, v));
    }
}
