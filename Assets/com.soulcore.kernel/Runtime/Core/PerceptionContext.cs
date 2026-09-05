namespace SoulCore
{
    /// <summary>感知上下文 — 外界刺激的载体（对齐 6.1.8 方案 PerceptionContext）</summary>
    public class PerceptionContext
    {
        public string event_type = "conversation";
        public float intensity = 1.0f;
        public string content = "";
        public string target_id = "";
        public string user_emotion = "";   // 用户情绪标签（情绪感染用）

        public PerceptionContext() { }

        public PerceptionContext(string pEventType, float pIntensity = 1.0f,
            string pContent = "", string pTargetId = "", string pUserEmotion = "")
        {
            event_type = pEventType;
            intensity = pIntensity;
            content = pContent;
            target_id = pTargetId;
            user_emotion = pUserEmotion;
        }
    }
}
