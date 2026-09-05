using System;
using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>好奇心系统 — 好奇心水平追踪（对齐 6.1.8 方案 CuriositySystem）</summary>
    public class CuriositySystem
    {
        public float curiosityLevel = 0.5f;

        public void UpdateCuriosity(float delta)
            => curiosityLevel = Math.Max(0.0f, Math.Min(1.0f, curiosityLevel + delta));

        /// <summary>好奇心足够高时生成提问</summary>
        public string GenerateQuestion()
            => curiosityLevel > 0.4f ? "你为什么会来这里？" : "";
    }

    /// <summary>兴趣系统 — 话题热度追踪（对齐 6.1.8 方案 InterestSystem）</summary>
    public class InterestSystem
    {
        private readonly Dictionary<string, float> _topics = new Dictionary<string, float>();

        public void UpdateTopic(string topic, float intensity = 0.05f)
        {
            if (string.IsNullOrEmpty(topic)) return;
            var key = topic.Length > 40 ? topic.Substring(0, 40) : topic;
            _topics[key] = Math.Max(0.0f, Math.Min(1.0f, (_topics.TryGetValue(key, out var v) ? v : 0.0f) + intensity));
        }

        public void Decay()
        {
            foreach (var key in new List<string>(_topics.Keys))
                _topics[key] = Math.Max(0.0f, _topics[key] - 0.02f);
        }

        public List<KeyValuePair<string, float>> GetTopInterests(int n)
        {
            var entries = new List<KeyValuePair<string, float>>();
            foreach (var kv in _topics) entries.Add(kv);
            entries.Sort((a, b) => b.Value.CompareTo(a.Value));
            return entries.GetRange(0, Math.Min(n, entries.Count));
        }
    }

    /// <summary>情绪感染系统 — 接收外部情绪并微弱影响自身（对齐 6.1.8 方案 EmotionInfectionSystem）</summary>
    public class EmotionInfectionSystem
    {
        private readonly Soul _soul;

        public EmotionInfectionSystem(Soul soul) { _soul = soul; }

        /// <summary>感染 — 接收外部情绪，以 0.1 倍强度影响自身</summary>
        public void Infect(string userEmotion, float intensity)
        {
            if (string.IsNullOrEmpty(userEmotion) || _soul == null) return;
            if (_soul.emotion.emotions.ContainsKey(userEmotion))
                _soul.AddEmotion(userEmotion, intensity * 0.1f);
        }
    }

    /// <summary>风味系统 — 产生点缀性文本（10 合 1，对齐 6.1.8 方案 FlavorSystem）</summary>
    public class FlavorSystem
    {
        private readonly Soul _soul;

        public FlavorSystem(Soul soul) { _soul = soul; }

        /// <summary>生成所有风味文本（空字符串表示未触发）</summary>
        public Dictionary<string, string> GenerateFlavor(string content = "")
        {
            var result = new Dictionary<string, string>
            {
                ["humor"] = "", ["complain"] = "", ["embarrassment"] = "",
                ["exploration"] = "", ["nostalgia"] = "", ["awe"] = "",
                ["intuition"] = "", ["inspiration"] = "", ["serendipity"] = "",
            };
            if (_soul == null) return result;

            var rng = _soul.GetRng();
            // 幽默（温暖度高 + 概率）
            if (_soul.personality.Get(PersonalityEngine.TRAIT_WARMTH, 0.5f) > 0.6f && rng.NextDouble() < 0.3)
                result["humor"] = "（笑了笑）生活总是充满惊喜。";
            // 直觉（直觉度高 + 概率）
            if (_soul.personality.Get(PersonalityEngine.TRAIT_INTUITION, 0.5f) > 0.6f && rng.NextDouble() < 0.25)
                result["intuition"] = "（若有所思）我总觉得事情没那么简单。";
            // 怀旧（经历多 + 概率）
            if (_soul.stats["experience_count"] > 20 && rng.NextDouble() < 0.2)
                result["nostalgia"] = "（目光放远）让我想起从前……";
            return result;
        }
    }
}
