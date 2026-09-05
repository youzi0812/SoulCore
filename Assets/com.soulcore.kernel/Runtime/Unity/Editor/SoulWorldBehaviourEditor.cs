using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SoulCore.Unity;

namespace SoulCore
{
    /// <summary>
    /// 世界节点面板中文化：所有字段显示中文标签 + 悬停中文说明。
    /// 不改字段名（避免破坏场景序列化），仅自定义 Inspector 显示。
    /// </summary>
    [CustomEditor(typeof(SoulWorldBehaviour))]
    public class SoulWorldBehaviourEditor : Editor
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["defaultPropagationMode"] = "默认事件传播模式",
            ["defaultMaxHops"] = "最大传播跳数",
            ["saveFileName"] = "存档文件名",
            ["autoLoadOnStart"] = "启动时自动读档",
            ["autoSaveOnDestroy"] = "退出时自动存档",
            ["autoSaveInterval"] = "自动存档间隔（秒）",
            ["runDailyAutoTick"] = "自动每日循环",
            ["secondsPerGameDay"] = "每日循环时长（秒）",
        };

        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
        {
            ["defaultPropagationMode"] = "立即 = 事件立刻传到所有 NPC；延迟 = 逐跳传播（像谣言扩散）",
            ["defaultMaxHops"] = "谣言/事件最多经过几个人（防无限传播）",
            ["saveFileName"] = "世界存档文件名（存在 persistentDataPath）",
            ["autoLoadOnStart"] = "场景启动时自动读取上次的世界存档",
            ["autoSaveOnDestroy"] = "场景销毁/退出时自动保存世界（NPC 人格/记忆/关系）",
            ["autoSaveInterval"] = "每隔多少秒自动存档一次（0 = 关闭自动存档）",
            ["runDailyAutoTick"] = "世界级每日循环（统一触发所有 NPC 的 DailyReset）",
            ["secondsPerGameDay"] = "多少真实秒等于游戏里的一天（5 分钟 = 1 天）",
        };

        private static readonly string[] ModeLabels = { "延迟传播（逐跳）", "立即传播（全广播）" };
        private static readonly int[] ModeValues = { 1, 0 };   // Deferred=1, Immediate=0

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var type = typeof(SoulWorldBehaviour);
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

                // 传播模式枚举：用中文下拉（Immediate/Deferred → 立即/延迟）
                if (it.name == "defaultPropagationMode")
                {
                    var cur = it.enumValueIndex == 0 ? 0 : 1;   // 默认 Immediate=0
                    var idx = EditorGUILayout.Popup(
                        new GUIContent("默认事件传播模式", Tips["defaultPropagationMode"]),
                        cur == 0 ? 1 : 0, ModeLabels);          // 映射到中文顺序
                    it.enumValueIndex = idx == 0 ? 1 : 0;       // 映射回枚举值
                    continue;
                }

                var label = Labels.TryGetValue(it.name, out var l) ? l : it.displayName;
                var tip = Tips.TryGetValue(it.name, out var t) ? t : "";
                EditorGUILayout.PropertyField(it, new GUIContent(label, tip), true);
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "世界节点：管理全部 NPC 的注册/事件传播/存档/每日循环。\n"
                + "一个场景放一个，NPC 组件会自动注册进来。",
                MessageType.None);
        }
    }
}
