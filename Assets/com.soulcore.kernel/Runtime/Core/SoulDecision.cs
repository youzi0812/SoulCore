using System.Collections.Generic;

namespace SoulCore
{
    /// <summary>决策结果（对齐 6.1.8 方案 SoulDecision）</summary>
    public class SoulDecision
    {
        public string action = "proceed";
        public string explanation = "";
        public float confidence = 0.5f;
        public string emotion = "";
        public float emotion_intensity = 0f;
        /// <summary>目标（why）：魂核此刻想要什么——求生存/救人/助人/自保/探索/维持现状。行为树读它决定 how</summary>
        public string goal = "维持现状";
        public List<Memory> relevant_memories = new List<Memory>();
        public Dictionary<string, string> flavor = new Dictionary<string, string>();
    }
}
