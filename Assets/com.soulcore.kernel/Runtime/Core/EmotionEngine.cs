using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>情绪引擎 — 15 种情绪的状态管理、事件驱动更新、衰减（对齐 6.1.8 方案 EmotionEngine）</summary>
    public class EmotionEngine
    {
        /// <summary>情绪状态表（键→当前值 0~1）</summary>
        public Dictionary<string, float> emotions = new Dictionary<string, float>();

        /// <summary>衰减速率表（键→每小时衰减量）</summary>
        private readonly Dictionary<string, float> _decayRates = new Dictionary<string, float>();

        public EmotionEngine()
        {
            emotions["joy"] = 0.5f; emotions["sadness"] = 0.2f;
            emotions["fear"] = 0.1f; emotions["anger"] = 0.1f;
            emotions["surprise"] = 0.1f; emotions["disgust"] = 0.1f;
            emotions["missing"] = 0.3f; emotions["loneliness"] = 0.3f;
            emotions["anxiety"] = 0.2f; emotions["hope"] = 0.5f;
            emotions["despair"] = 0.1f; emotions["gratitude"] = 0.3f;
            emotions["curiosity_emotion"] = 0.4f; emotions["pride"] = 0.2f;
            emotions["shame"] = 0.1f;

            _decayRates["joy"] = 0.02f; _decayRates["fear"] = 0.03f;
            _decayRates["anger"] = 0.02f; _decayRates["sadness"] = 0.01f;
            _decayRates["surprise"] = 0.05f; _decayRates["anxiety"] = 0.02f;
            _decayRates["curiosity_emotion"] = 0.01f;
            _decayRates["pride"] = 0.01f; _decayRates["shame"] = 0.02f;
        }

        /// <summary>事件驱动更新情绪（含人格调制）</summary>
        public void Update(string eventType, float intensity, string content = "", Dictionary<string, float> personality = null)
        {
            var changes = new Dictionary<string, float>();

            switch (eventType)
            {
                case "disaster":
                    changes["fear"] = intensity * 0.3f; changes["anxiety"] = 0.1f;
                    changes["hope"] = -0.1f;
                    break;
                case "help_others":
                    changes["joy"] = 0.2f; changes["hope"] = 0.1f; changes["gratitude"] = 0.1f;
                    break;
                case "insult":
                    changes["anger"] = 0.2f; changes["sadness"] = 0.1f;
                    break;
                case "gift":
                    changes["joy"] = 0.2f; changes["gratitude"] = 0.2f;
                    break;
                case "betray":
                    changes["sadness"] = 0.3f; changes["anger"] = 0.2f; changes["despair"] = 0.1f;
                    break;
                case "rescued":
                    changes["joy"] = 0.4f; changes["hope"] = 0.2f; changes["gratitude"] = 0.3f;
                    break;
                case "learn":
                    changes["curiosity_emotion"] = 0.2f; changes["joy"] = 0.1f;
                    break;
                case "success":
                    changes["pride"] = 0.3f; changes["joy"] = 0.2f;
                    break;
                case "failure":
                    changes["shame"] = 0.2f; changes["sadness"] = 0.1f;
                    break;
                case "conversation":
                    if (!string.IsNullOrEmpty(content))
                    {
                        if (content.Contains("谢谢") || content.Contains("感谢"))
                        { changes["gratitude"] = 0.2f; changes["joy"] = 0.1f; }
                        else if (content.Contains("对不起") || content.Contains("抱歉"))
                        { changes["shame"] = 0.1f; }
                        else if (content.Contains("想") && content.Contains("念"))
                        { changes["missing"] = 0.1f; }
                        else if (content.Contains("厉害") || content.Contains("佩服"))
                        { changes["pride"] = 0.1f; changes["joy"] = 0.1f; }
                    }
                    break;
                // 雾港审问事件类型
                case "pressure":
                    changes["fear"] = intensity * 0.25f; changes["anger"] = intensity * 0.15f;
                    break;
                case "empathy":
                    changes["hope"] = intensity * 0.15f; changes["gratitude"] = intensity * 0.1f;
                    break;
                case "show_evidence":
                    changes["fear"] = intensity * 0.3f; changes["anxiety"] = intensity * 0.15f;
                    break;
                case "probe":
                    changes["anxiety"] = intensity * 0.2f;
                    break;
            }

            // 乘以强度
            foreach (var key in new List<string>(changes.Keys))
                changes[key] *= intensity;

            // 人格调制
            if (personality != null)
            {
                if (GetTrait(personality, PersonalityEngine.TRAIT_OPTIMISM, 0.5f) > 0.7f)
                    changes["hope"] = (changes.TryGetValue("hope", out var h0) ? h0 : 0.0f) + 0.1f;
                if (GetTrait(personality, PersonalityEngine.TRAIT_FEAR_TENDENCY, 0.2f) > 0.6f && changes.ContainsKey("fear"))
                    changes["fear"] *= 1.3f;
                if (GetTrait(personality, PersonalityEngine.TRAIT_ANGER_TENDENCY, 0.2f) > 0.6f && changes.ContainsKey("anger"))
                    changes["anger"] *= 1.2f;
                if (GetTrait(personality, PersonalityEngine.TRAIT_HOPE_TENDENCY, 0.6f) > 0.7f)
                    changes["hope"] = (changes.TryGetValue("hope", out var h1) ? h1 : 0.0f) + 0.1f;
            }

            // 应用变化
            foreach (var kv in changes)
            {
                if (emotions.ContainsKey(kv.Key))
                    emotions[kv.Key] = Clamp01(emotions[kv.Key] + kv.Value);
            }
        }

        public void AddEmotion(string key, float delta)
        {
            if (emotions.ContainsKey(key))
                emotions[key] = Clamp01(emotions[key] + delta);
        }

        public void SetEmotion(string key, float value)
        {
            if (emotions.ContainsKey(key))
                emotions[key] = Clamp01(value);
        }

        public void SetEmotions(Dictionary<string, float> dict)
        {
            foreach (var kv in dict)
                if (emotions.ContainsKey(kv.Key))
                    emotions[kv.Key] = Clamp01(kv.Value);
        }

        /// <summary>随时间衰减（按游戏小时数）</summary>
        public void Decay(float hoursPassed)
        {
            var factor = hoursPassed / 24.0f;
            foreach (var kv in _decayRates)
            {
                if (!emotions.ContainsKey(kv.Key)) continue;
                emotions[kv.Key] = Math.Max(0.0f, emotions[kv.Key] - kv.Value * factor);
            }
            foreach (var e in new[] { "missing", "loneliness", "gratitude", "hope", "pride", "shame" })
            {
                if (emotions.ContainsKey(e))
                    emotions[e] = Math.Max(0.0f, emotions[e] - 0.02f * factor);
            }
        }

        public string GetDominant()
        {
            var bestKey = "joy";
            var bestVal = -1.0f;
            foreach (var kv in emotions)
            {
                if (kv.Value > bestVal) { bestVal = kv.Value; bestKey = kv.Key; }
            }
            return bestKey;
        }

        public float GetIntensity()
        {
            float sum = 0;
            foreach (var v in emotions.Values) sum += v;
            return sum / emotions.Count;
        }

        private static float GetTrait(Dictionary<string, float> p, string key, float def)
            => p.TryGetValue(key, out var v) ? v : def;

        private static float Clamp01(float v) => Math.Max(0.0f, Math.Min(1.0f, v));
    }
}
