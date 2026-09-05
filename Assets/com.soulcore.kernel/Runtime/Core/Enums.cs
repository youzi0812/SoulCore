namespace SoulCore
{
    /// <summary>SoulCore 全局枚举（对齐 6.1.8 方案 SoulEnums）</summary>
    public static class SoulEnums
    {
        /// <summary>风味输出模式</summary>
        public enum FlavorOutputMode
        {
            BuiltInTemplates = 0,   // 内置模板（默认）
            LLM = 1,                // 外接 LLM（Pro 阶段）
            Silent = 2,             // 静默
        }

        /// <summary>世界事件传播模式</summary>
        public enum PropagationMode
        {
            Immediate = 0,   // report() 立即全链路 BFS 传播
            Deferred = 1,    // report() 只执行直接效果，tick() 推进一跳
        }

        /// <summary>决策情境类型（原版 6 种 + 雾港 4 种审问）</summary>
        public enum SituationType
        {
            Help = 0,
            Conversation = 1,
            Evacuation = 2,
            Rescue = 3,
            Learn = 4,
            Default = 5,
            InterrogatePressure = 6,
            InterrogateEmpathy = 7,
            InterrogateEvidence = 8,
            InterrogateProbe = 9,
        }

        /// <summary>情境枚举 → 字符串键（数据驱动配置用）</summary>
        public static string SituationKey(SituationType sit)
        {
            switch (sit)
            {
                case SituationType.Help: return "help";
                case SituationType.Conversation: return "conversation";
                case SituationType.Evacuation: return "evacuation";
                case SituationType.Rescue: return "rescue";
                case SituationType.Learn: return "learn";
                case SituationType.InterrogatePressure: return "interrogate_pressure";
                case SituationType.InterrogateEmpathy: return "interrogate_empathy";
                case SituationType.InterrogateEvidence: return "interrogate_evidence";
                case SituationType.InterrogateProbe: return "interrogate_probe";
                default: return "default";
            }
        }
    }
}
