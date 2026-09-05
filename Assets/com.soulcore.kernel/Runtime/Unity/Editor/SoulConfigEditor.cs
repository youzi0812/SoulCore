using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using SoulCore.Llm;

namespace SoulCore
{
    /// <summary>
    /// SoulConfig 全中文面板：所有字段显示中文标签 + 分组标题 + Tooltip，
    /// 底部提供"用 LLM 解析一句话人格"工具（配置在「🔌 LLM 入口」区）。
    /// </summary>
    [CustomEditor(typeof(SoulConfig))]
    public class SoulConfigEditor : Editor
    {
        private string _status = "";
        private const string TraitFields =
            "warmth,compassion,trust,optimism,courage,curiosity,creativity,persistence," +
            "rationality,patience,honesty,loyalty,justice,intuition,resilience,energy," +
            "selfishness,fearTendency,angerTendency,sadnessTendency,hopeTendency";

        /// <summary>字段名 → 中文标签（面板全中文化）</summary>
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["oneLinePersonality"] = "一句话人格",
            ["personalityPreset"] = "人格预设",
            ["autoDiversify"] = "自动多样化",
            ["diversifyStrength"] = "多样化强度",
            ["warmth"] = "温暖", ["compassion"] = "共情", ["trust"] = "信任",
            ["optimism"] = "乐观", ["courage"] = "勇气", ["curiosity"] = "好奇",
            ["creativity"] = "创造力", ["persistence"] = "坚韧", ["rationality"] = "理性",
            ["patience"] = "耐心", ["honesty"] = "诚实", ["loyalty"] = "忠诚",
            ["justice"] = "正义感", ["intuition"] = "直觉", ["resilience"] = "抗压",
            ["energy"] = "活力", ["selfishness"] = "自私",
            ["fearTendency"] = "恐惧倾向", ["angerTendency"] = "易怒",
            ["sadnessTendency"] = "悲伤倾向", ["hopeTendency"] = "希望感",
            ["initialJoy"] = "初始喜悦", ["initialHope"] = "初始希望",
            ["initialGratitude"] = "初始感激", ["initialAnxiety"] = "初始焦虑",
            ["initialLoneliness"] = "初始孤独", ["emotionDailyDecayScale"] = "情绪日衰减",
            ["maxMemories"] = "记忆容量上限",
            ["weightPersonality"] = "人格权重", ["weightEmotion"] = "情绪权重",
            ["weightMemory"] = "记忆权重", ["weightResource"] = "资源权重",
            ["initialRelations"] = "初始关系",
            ["autoSave"] = "自动存档", ["autoSaveIntervalSeconds"] = "自动存档间隔（秒）",
            ["saveFileName"] = "存档文件名",
            ["secondsPerGameDay"] = "每日循环时长（秒）",
            ["modulePersonality"] = "人格模块", ["moduleEmotion"] = "情绪模块",
            ["moduleMemory"] = "记忆模块", ["moduleDecision"] = "决策模块",
            ["moduleRelationship"] = "关系模块", ["moduleInterest"] = "兴趣模块",
            ["moduleCuriosity"] = "好奇模块", ["moduleEmotionInfect"] = "情绪感染",
            ["moduleFlavor"] = "表达风味", ["moduleDream"] = "梦境模块",
            ["llmEnabled"] = "启用 LLM", ["llmBaseUrl"] = "LLM 接口地址",
            ["llmApiKey"] = "LLM API Key", ["llmModel"] = "LLM 模型",
            ["llmMaxChars"] = "一句话最大字数",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var cfg = (SoulConfig)target;
            var type = typeof(SoulConfig);

            // 遍历所有可见属性（含 List 展开），中文标签 + 分组标题 + Tooltip
            string lastHeader = null;
            var it = serializedObject.GetIterator();
            it.NextVisible(true);
            if (it.propertyPath == "m_Script")
                EditorGUILayout.PropertyField(it, true);

            while (it.NextVisible(false))
            {
                var fi = type.GetField(it.name);
                if (fi != null)
                {
                    var header = fi.GetCustomAttribute<HeaderAttribute>();
                    if (header != null && header.header != lastHeader)
                    {
                        lastHeader = header.header;
                        EditorGUILayout.Space(8);
                        EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
                    }
                }

                var label = Labels.TryGetValue(it.name, out var l) ? l : it.displayName;
                var tip = fi?.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
                EditorGUILayout.PropertyField(it, new GUIContent(label, tip), true);
            }

            serializedObject.ApplyModifiedProperties();

            // ==================== LLM 工具区 ====================
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("🤖 LLM 工具", EditorStyles.boldLabel);

            if (!cfg.llmEnabled)
            {
                EditorGUILayout.HelpBox("在上方「🔌 LLM 入口」区填写接口配置并勾选「启用 LLM」后，" +
                                        "可一键把一句话人格解析成 21 个特质。", MessageType.Info);
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.llmApiKey))
                EditorGUILayout.HelpBox("「LLM API Key」为空，请先填写。", MessageType.Warning);
            if (string.IsNullOrWhiteSpace(cfg.oneLinePersonality))
                EditorGUILayout.HelpBox("先在「一句话人格」填描述（如：沉默寡言的渔夫，嘴硬心软）。", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(cfg.oneLinePersonality) ||
                                         string.IsNullOrWhiteSpace(cfg.llmApiKey));
            if (GUILayout.Button("✨ 用 LLM 解析「一句话人格」到 21 特质", GUILayout.Height(32)))
            {
                _status = ParseWithLlm(cfg);
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _status.StartsWith("✅") ? MessageType.Info : MessageType.Error);
        }

        private string ParseWithLlm(SoulConfig cfg)
        {
            try
            {
                var system = "你是人格分析器。把人物的一句话描述解析成 21 个 0~1 的人格特质数值。"
                    + "只输出 JSON 对象，不要任何解释或前后缀。字段列表：" + TraitFields
                    + "。格式示例：{\"warmth\":0.7,\"compassion\":0.8,\"courage\":0.9}";
                var user = "人物描述：" + cfg.oneLinePersonality;
                var reply = SoulLlmClient.Chat(cfg.llmBaseUrl, cfg.llmApiKey, cfg.llmModel, system, user, 800);

                var m = Regex.Match(reply, "\\{[^{}]*\\}");
                if (!m.Success)
                    return "❌ LLM 返回无法解析（需要 JSON 对象）：\n" + Truncate(reply, 200);

                var kv = Regex.Matches(m.Value, "\"([A-Za-z_]+)\"\\s*:\\s*([0-9.]+)");
                var applied = 0;
                foreach (Match k in kv)
                {
                    if (!float.TryParse(k.Groups[2].Value,
                                        System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out var val))
                        continue;
                    var field = typeof(SoulConfig).GetField(k.Groups[1].Value);
                    if (field != null && field.FieldType == typeof(float))
                    {
                        field.SetValue(cfg, Mathf.Clamp01(val));
                        applied++;
                    }
                }

                if (applied == 0)
                    return "❌ 没有匹配到任何特质字段（模型输出格式不符）：\n" + Truncate(reply, 200);

                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                return "✅ 已解析并写入 " + applied + " 个特质 ← " + cfg.oneLinePersonality;
            }
            catch (Exception e)
            {
                return "❌ " + e.Message;
            }
        }

        private static string Truncate(string s, int n)
            => s.Length <= n ? s : s.Substring(0, n) + "...";
    }
}