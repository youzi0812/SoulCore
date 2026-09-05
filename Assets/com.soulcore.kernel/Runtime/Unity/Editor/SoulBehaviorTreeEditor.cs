using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SoulCore.BehaviorTree;

namespace SoulCore
{
    /// <summary>
    /// 行为树自定义 Inspector：手动逐层绘制（折叠/缩进/深度限制），
    /// 绕开 Unity 默认递归序列化的深度限制（Serialization depth limit 10）。
    /// </summary>
    [CustomEditor(typeof(SoulBehaviorTree))]
    public class SoulBehaviorTreeEditor : Editor
    {
        private static readonly Dictionary<int, bool> Foldouts = new Dictionary<int, bool>();

        // 节点类型中文标签（顺序与 SoulBtNodeType 枚举完全一致：0-16 共 17 个）
        private static readonly string[] TypeLabels =
        {
            "选择器（任选其一）", "顺序（全部执行）", "取反（反转结果）", "随机（随机选一个）",
            "特质条件", "情绪条件", "关系条件", "记忆条件", "决策条件", "目标条件", "冷却条件",
            "动作（输出行为意图）", "情绪偏移", "关系偏移", "存记忆", "日志", "LLM 叙事（台词生成）",
        };

        public override void OnInspectorGUI()
        {
            var tree = (SoulBehaviorTree)target;
            serializedObject.Update();

            EditorGUILayout.LabelField("🌳 专属行为树（点节点名展开/折叠编辑）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Selector=任选其一 · Sequence=顺序全过 · 条件节点读魂核状态 · Action 输出行为意图",
                MessageType.None);

            if (tree.root == null)
            {
                if (GUILayout.Button("创建根节点（Selector）"))
                    tree.root = new SoulBtNode { type = SoulBtNodeType.Selector, key = "root" };
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var removedRoot = false;
            DrawNode(tree.root, 0, "根", null, -1, ref removedRoot);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
        }

        private void DrawNode(SoulBtNode node, int depth, string label, SoulBtNode parent, int index, ref bool removed)
        {
            if (node == null) return;
            if (depth > 12)
            {
                EditorGUILayout.LabelField("…（超过 12 层，已折叠）");
                return;
            }

            var id = GetId(node, depth);
            var header = label + ": " + Describe(node);
            if (node.children.Count > 0) header += "  (" + node.children.Count + " 子)";

            // 头部行：折叠标题 + 删除按钮。
            // 注意：只有这一行用 Horizontal（内容短）；展开内容必须纵向布局——
            // 之前把整个递归节点塞进 Horizontal 导致多行控件互相挤压重叠
            var open = Foldouts.TryGetValue(id, out var v) && v;
            EditorGUILayout.BeginHorizontal();
            open = EditorGUILayout.Foldout(open, header, true);
            if (parent != null && GUILayout.Button("删", GUILayout.Width(30)))
                removed = true;   // 标记删除（由父节点循环执行 RemoveAt）
            EditorGUILayout.EndHorizontal();
            Foldouts[id] = open;
            if (!open) return;

            EditorGUI.indentLevel++;
            // 节点类型：中文下拉（改类型时清空 key/text 防残留）
            var typeIdx = EditorGUILayout.Popup("类型", (int)node.type, TypeLabels);
            if (typeIdx != (int)node.type)
            {
                node.type = (SoulBtNodeType)typeIdx;
                node.key = "";
                node.text = "";
            }

            // 按类型显示相关字段
            if (NeedsKey(node.type))
                node.key = EditorGUILayout.TextField("键", node.key);
            if (NeedsThreshold(node.type))
                node.threshold = EditorGUILayout.Slider("阈值", node.threshold, 0f, 1f);
            if (NeedsOp(node.type))
                node.op = EditorGUILayout.Popup("比较", node.op, new[] { "大于", "小于", "大于等于" });
            if (NeedsAmount(node.type))
                node.amount = EditorGUILayout.FloatField("幅度", node.amount);
            if (NeedsText(node.type))
                node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(40));
            if (NeedsCooldown(node.type))
                node.cooldown = EditorGUILayout.FloatField("冷却(秒)", node.cooldown);
            if (node.type == SoulBtNodeType.MemoryStore)
                node.key2 = EditorGUILayout.TextField("记忆类型", node.key2);
            if (node.type == SoulBtNodeType.MemoryCondition)
                node.recentCount = EditorGUILayout.IntField("最近条数", node.recentCount);

            EditorGUILayout.Space(4);
            // 子节点纵向递归（每个子节点独占整行区域，不包 Horizontal）
            for (var i = 0; i < node.children.Count; i++)
            {
                var childRemoved = false;
                DrawNode(node.children[i], depth + 1, "子" + i, node, i, ref childRemoved);
                if (childRemoved)
                {
                    node.children.RemoveAt(i);   // 布局已配对，安全删除
                    break;
                }
            }
            if (GUILayout.Button("+ 添加子节点"))
                node.children.Add(new SoulBtNode { type = SoulBtNodeType.Action });
            EditorGUI.indentLevel--;
        }

        private static int GetId(SoulBtNode node, int depth)
            => node.GetHashCode() ^ (depth << 16);

        private static string Describe(SoulBtNode node)
        {
            var s = node.type.ToString();
            if (!string.IsNullOrEmpty(node.key)) s += " [" + node.key + "]";
            return s;
        }

        private static bool NeedsKey(SoulBtNodeType t)
            => t == SoulBtNodeType.TraitCondition || t == SoulBtNodeType.EmotionCondition
            || t == SoulBtNodeType.RelationshipCondition || t == SoulBtNodeType.MemoryCondition
            || t == SoulBtNodeType.CooldownCondition || t == SoulBtNodeType.Action
            || t == SoulBtNodeType.EmotionShift || t == SoulBtNodeType.RelationshipShift
            || t == SoulBtNodeType.DecisionCondition || t == SoulBtNodeType.GoalCondition;

        private static bool NeedsThreshold(SoulBtNodeType t)
            => t == SoulBtNodeType.TraitCondition || t == SoulBtNodeType.EmotionCondition
            || t == SoulBtNodeType.RelationshipCondition || t == SoulBtNodeType.MemoryCondition
            || t == SoulBtNodeType.DecisionCondition;

        private static bool NeedsOp(SoulBtNodeType t) => NeedsThreshold(t);

        private static bool NeedsAmount(SoulBtNodeType t)
            => t == SoulBtNodeType.EmotionShift || t == SoulBtNodeType.RelationshipShift;

        private static bool NeedsText(SoulBtNodeType t)
            => t == SoulBtNodeType.Action || t == SoulBtNodeType.MemoryStore
            || t == SoulBtNodeType.Log || t == SoulBtNodeType.LlmNarration;

        private static bool NeedsCooldown(SoulBtNodeType t) => t == SoulBtNodeType.CooldownCondition;
    }
}
