using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCore.BehaviorTree
{
    /// <summary>
    /// 专属魂核行为树资产：数据驱动的行为选择器（1.8.0）。
    /// 树读魂核状态（人格/情绪/关系/记忆/决策倾向）决定 NPC 的具体行为。
    /// 用法：右键 → 创建 → 魂核 → 行为树；根节点用 Selector/Sequence 组合条件与动作。
    /// </summary>
    [CreateAssetMenu(fileName = "SoulBehaviorTree", menuName = "魂核/行为树")]
    public class SoulBehaviorTree : ScriptableObject
    {
        [Tooltip("根节点（通常是 Selector/Sequence）")]
        public SoulBtNode root = new SoulBtNode(SoulBtNodeType.Selector);

        public SoulBtResult Evaluate(SoulBtContext ctx)
        {
            if (root == null) return SoulBtResult.Failure;
            return EvaluateNode(root, ctx);
        }

        private SoulBtResult EvaluateNode(SoulBtNode node, SoulBtContext ctx)
        {
            if (node == null) return SoulBtResult.Failure;

            switch (node.type)
            {
                case SoulBtNodeType.Selector:
                    foreach (var c in node.children)
                        if (EvaluateNode(c, ctx) == SoulBtResult.Success) return SoulBtResult.Success;
                    return SoulBtResult.Failure;

                case SoulBtNodeType.Sequence:
                    foreach (var c in node.children)
                        if (EvaluateNode(c, ctx) != SoulBtResult.Success) return SoulBtResult.Failure;
                    return SoulBtResult.Success;

                case SoulBtNodeType.Inverter:
                    return EvaluateNode(node.children.Count > 0 ? node.children[0] : null, ctx) == SoulBtResult.Success
                        ? SoulBtResult.Failure : SoulBtResult.Success;

                case SoulBtNodeType.Random:
                    if (node.children.Count == 0) return SoulBtResult.Failure;
                    return EvaluateNode(node.children[UnityEngine.Random.Range(0, node.children.Count)], ctx);

                case SoulBtNodeType.TraitCondition:
                    return Compare(GetTrait(ctx, node.key), node.threshold, node.op);

                case SoulBtNodeType.EmotionCondition:
                    return ctx.soul != null && ctx.soul.emotion.GetDominant() == node.key
                        ? SoulBtResult.Success : SoulBtResult.Failure;

                case SoulBtNodeType.RelationshipCondition:
                    return Compare(ctx.soul != null ? ctx.soul.relationship.GetValue(ctx.soul.id, node.key) : 0f,
                                   node.threshold, node.op);

                case SoulBtNodeType.MemoryCondition:
                    return HasRecentMemory(ctx, node.key, node.recentCount)
                        ? SoulBtResult.Success : SoulBtResult.Failure;

                case SoulBtNodeType.DecisionCondition:
                    if (ctx.lastDecision == null) return SoulBtResult.Failure;
                    if (string.IsNullOrEmpty(node.key) || ctx.lastDecision.action == node.key)
                        return Compare(ctx.lastDecision.confidence, node.threshold, node.op);
                    return SoulBtResult.Failure;

                case SoulBtNodeType.GoalCondition:
                    // 目标匹配（魂核出 why → 行为树出 how）：goal 含关键词即 Success
                    if (string.IsNullOrEmpty(ctx.goal) || string.IsNullOrEmpty(node.key))
                        return SoulBtResult.Failure;
                    return ctx.goal.Contains(node.key) ? SoulBtResult.Success : SoulBtResult.Failure;

                case SoulBtNodeType.CooldownCondition:
                    if (ctx.now - ctx.GetLastTime(node.key) >= node.cooldown)
                    {
                        ctx.SetLastTime(node.key, ctx.now);
                        return SoulBtResult.Success;
                    }
                    return SoulBtResult.Failure;

                case SoulBtNodeType.Action:
                    ctx.behaviorIntent = node.key;
                    ctx.intentText = node.text;
                    ctx.OnIntent?.Invoke(node.key, node.text);
                    return SoulBtResult.Success;

                case SoulBtNodeType.EmotionShift:
                    if (ctx.soul != null && !string.IsNullOrEmpty(node.key))
                    {
                        var e = ctx.soul.emotion.emotions;
                        var baseVal = e.TryGetValue(node.key, out var bv) ? bv : 0.5f;
                        e[node.key] = Mathf.Clamp01(baseVal + node.amount);
                    }
                    return SoulBtResult.Success;

                case SoulBtNodeType.RelationshipShift:
                    ctx.soul?.relationship.Change(ctx.soul.id, node.key, Mathf.RoundToInt(node.amount), "行为树");
                    return SoulBtResult.Success;

                case SoulBtNodeType.MemoryStore:
                    if (ctx.soul != null && !string.IsNullOrEmpty(node.text))
                    {
                        var content = node.text.Replace("{name}", ctx.soul.name)
                                               .Replace("{emotion}", ctx.soul.emotion.GetDominant());
                        var mem = ctx.soul.memory.CreateMemory(content, node.key2, Mathf.RoundToInt(node.amount));
                        ctx.soul.memory.Store(mem);
                    }
                    return SoulBtResult.Success;

                case SoulBtNodeType.Log:
                    Debug.Log("[SoulCore] 行为树: "
                        + node.text.Replace("{name}", ctx.soul != null ? ctx.soul.name : "?")
                                   .Replace("{emotion}", ctx.soul != null ? ctx.soul.emotion.GetDominant() : "?"));
                    return SoulBtResult.Success;

                case SoulBtNodeType.LlmNarration:
                    // 叙事节点：输出 "__llm__:提示模板" 到上下文，由 Unity 层低频异步调 LLM 生成台词
                    ctx.intentText = "__llm__:" + (node.text ?? "");
                    ctx.OnIntent?.Invoke(node.key, ctx.intentText);
                    return SoulBtResult.Success;
            }
            return SoulBtResult.Failure;
        }

        private static float GetTrait(SoulBtContext ctx, string key)
        {
            if (ctx.soul == null || string.IsNullOrEmpty(key)) return 0.5f;
            return ctx.soul.personality.traits.TryGetValue(key, out var v) ? v : 0.5f;
        }

        private static bool HasRecentMemory(SoulBtContext ctx, string memoryType, int recentCount)
        {
            if (ctx.soul == null || string.IsNullOrEmpty(memoryType)) return false;
            var recent = ctx.soul.memory.GetRecent(Mathf.Max(1, recentCount));
            foreach (var m in recent)
                if (m.type == memoryType) return true;
            return false;
        }

        private static SoulBtResult Compare(float v, float t, int op)
        {
            var ok = op switch
            {
                1 => v < t,
                2 => v >= t,
                _ => v > t,
            };
            return ok ? SoulBtResult.Success : SoulBtResult.Failure;
        }
    }
}
