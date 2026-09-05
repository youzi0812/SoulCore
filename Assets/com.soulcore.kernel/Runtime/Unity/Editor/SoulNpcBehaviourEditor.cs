using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SoulCore.Unity;

namespace SoulCore
{
    /// <summary>
    /// NPC 面板增强（二合一）：
    /// 1) 字段中文化：所有设置显示中文标签 + 悬停中文说明（不改字段名，不破坏序列化）
    /// 2) 底部"运行时人格"区：显示当前 21 特质（中文）+ 主导情绪 + 记忆数——人格涌现效果在这里看
    /// </summary>
    [CustomEditor(typeof(SoulNpcBehaviour))]
    public class SoulNpcBehaviourEditor : Editor
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["soulId"] = "魂核 ID",
            ["displayName"] = "显示名",
            ["config"] = "NPC 配置资产",
            ["secondsPerGameDay"] = "每日循环时长（秒）",
            ["runDailyAutoReset"] = "自动每日重置",
            ["resourceScarcity"] = "资源稀缺度",
            ["useScarcityAsProvider"] = "使用资源稀缺度",
            ["maxPerceptionsPerSecond"] = "每秒最大感知次数",
            ["enforceBudget"] = "启用感知频率限制",
            ["chatSystemPromptOverride"] = "自定义对话提示词",
            ["chatMaxTokens"] = "对话最大 Token",
            ["enableChatUI"] = "启用对话窗口",
            ["chatHistoryLimit"] = "对话历史保留条数",
            ["showTyping"] = "显示思考中",
            ["useBehaviorTree"] = "启用行为树",
            ["behaviorTree"] = "行为树资产",
            ["treeTickInterval"] = "行为树评估间隔（秒）",
            ["OnBehaviorIntent"] = "行为意图输出事件",
        };

        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            ["soulId"] = "唯一标识（用于存档/注册/事件定位），如 blacksmith_zhou",
            ["displayName"] = "NPC 名字，会出现在对话和日志里",
            ["config"] = "右键 → 创建 → 魂核 → NPC 配置。留空用默认人格",
            ["secondsPerGameDay"] = "多少真实秒等于游戏里的一天（日终触发每日重置）",
            ["runDailyAutoReset"] = "每天自动执行一次 DailyReset（情绪衰减/关系日结算/人格涌现）",
            ["resourceScarcity"] = "0=资源充足 1=资源匮乏，影响决策的资源权重",
            ["useScarcityAsProvider"] = "用上面的数值作为本 NPC 的资源稀缺度来源",
            ["maxPerceptionsPerSecond"] = "限制每秒感知次数，防止高频事件刷爆决策",
            ["enforceBudget"] = "运行模式下生效（编辑模式自动放行）",
            ["chatSystemPromptOverride"] = "留空 = 自动注入人格/情绪/行为倾向；填了则完全使用自定义提示词",
            ["chatMaxTokens"] = "对话最大输出 token（推理模型会被推理占掉一部分，1024 起步）",
            ["enableChatUI"] = "运行时点击 NPC 弹出对话窗口（需 Collider）",
            ["chatHistoryLimit"] = "对话历史最多保留条数（超出从最早开始丢）",
            ["showTyping"] = "发送后显示'思考中...'（真实 LLM 调用需要几秒）",
            ["useBehaviorTree"] = "启用专属行为树（读魂核状态决定行为意图）",
            ["behaviorTree"] = "行为树资产（右键 → 创建 → 魂核 → 行为树）",
            ["treeTickInterval"] = "树评估频率（秒）",
            ["OnBehaviorIntent"] = "行为意图输出事件（actionName）",
        };

        private static readonly Dictionary<string, string> TraitLabels = new Dictionary<string, string>
        {
            ["warmth"] = "温暖", ["compassion"] = "共情", ["trust"] = "信任",
            ["optimism"] = "乐观", ["courage"] = "勇气", ["curiosity"] = "好奇",
            ["creativity"] = "创造力", ["persistence"] = "坚韧", ["rationality"] = "理性",
            ["patience"] = "耐心", ["honesty"] = "诚实", ["loyalty"] = "忠诚",
            ["justice"] = "正义感", ["intuition"] = "直觉", ["resilience"] = "抗压",
            ["energy"] = "活力", ["selfishness"] = "自私",
            ["fear_tendency"] = "恐惧倾向", ["anger_tendency"] = "易怒",
            ["sadness_tendency"] = "悲伤倾向", ["hope_tendency"] = "希望感",
        };

        public override void OnInspectorGUI()
        {
            // ==================== 字段中文化 ====================
            serializedObject.Update();
            var type = typeof(SoulNpcBehaviour);
            var it = serializedObject.GetIterator();
            it.NextVisible(true);
            if (it.propertyPath == "m_Script")
                EditorGUILayout.PropertyField(it, true);

            string lastHeader = null;
            while (it.NextVisible(false))
            {
                var fi = type.GetField(it.name);
                if (fi != null)
                {
                    var h = fi.GetCustomAttribute<HeaderAttribute>();
                    if (h != null && h.header != lastHeader)
                    {
                        lastHeader = h.header;
                        EditorGUILayout.Space(6);
                        EditorGUILayout.LabelField(h.header, EditorStyles.boldLabel);
                    }
                }
                var label = Labels.TryGetValue(it.name, out var l) ? l : it.displayName;
                var tip = Tips.TryGetValue(it.name, out var t) ? t : "";
                EditorGUILayout.PropertyField(it, new GUIContent(label, tip), true);
            }
            serializedObject.ApplyModifiedProperties();

            // ==================== 运行时人格显示（人格涌现效果在这里看） ====================
            var npc = (SoulNpcBehaviour)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🧬 运行时人格（人格涌现效果在这里看）", EditorStyles.boldLabel);
            if (npc.soul == null)
            {
                EditorGUILayout.HelpBox("魂核未初始化：Play 模式运行后，或 NPC 首次感知/对话后显示。", MessageType.Info);
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            foreach (var kv in npc.soul.personality.traits)
            {
                var label = TraitLabels.TryGetValue(kv.Key, out var zh) ? zh : kv.Key;
                EditorGUILayout.Slider(label, kv.Value, 0f, 1f);
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("主导情绪", npc.soul.emotion.GetDominant());
            EditorGUILayout.LabelField("情绪强度", npc.soul.emotion.GetIntensity().ToString("0.00"));
            EditorGUILayout.LabelField("记忆数", npc.soul.memory.GetTotalCount().ToString());
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "人格涌现：NPC 的近期经历（记忆情绪标签）每天微调人格。\n"
                + "让 NPC 经历冲突/温暖事件 → 触发每日重置（5 分钟=1 天）→ 看上方特质变化。",
                MessageType.None);
        }
    }
}
