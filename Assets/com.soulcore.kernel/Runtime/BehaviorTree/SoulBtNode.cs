using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulCore.BehaviorTree
{
    /// <summary>行为树节点类型</summary>
    public enum SoulBtNodeType
    {
        // 组合节点
        Selector,      // 优先选择：第一个成功即停
        Sequence,      // 顺序：全部成功才成功
        Inverter,      // 取反
        Random,        // 随机选一个子节点
        // 条件节点（读魂核状态）
        TraitCondition,      // 特质比较（courage > 0.7）
        EmotionCondition,    // 主导情绪匹配
        RelationshipCondition, // 与目标关系值比较
        MemoryCondition,     // 记忆命中（近 N 条内）
        DecisionCondition,   // 决策倾向匹配
        GoalCondition,       // 当前目标匹配（魂核出 why：goal 含关键词）
        CooldownCondition,   // 行为冷却（防刷屏）
        // 动作节点
        Action,              // 输出行为意图（actionName）
        EmotionShift,        // 调整情绪
        RelationshipShift,   // 关系变化
        MemoryStore,         // 存记忆
        Log,                 // 调试日志
        LlmNarration,        // LLM 台词生成（叙事节点：低频异步生成台词）
    }

    /// <summary>节点求值结果</summary>
    public enum SoulBtResult { Success, Failure, Running }

    /// <summary>
    /// 行为树节点（单类多字段：Unity 序列化不支持多态 List，
    /// 用 type 枚举区分节点种类，参数统一放字段里）。
    /// </summary>
    [Serializable]
    public class SoulBtNode
    {
        public SoulBtNodeType type = SoulBtNodeType.Selector;
        public List<SoulBtNode> children = new List<SoulBtNode>();  // 组合节点的子节点

        // 条件参数
        public string key = "";        // traitKey / emotionKey / targetId / actionName / memoryType
        public string key2 = "";       // 次要参数（记忆的 source 等）
        public int op = 0;             // 0=>(大于) 1=<(小于) 2=>=(大于等于)
        public float threshold = 0.5f; // 比较阈值
        public int recentCount = 10;   // 记忆"近 N 条"范围
        public float cooldown = 10f;   // 冷却秒数

        // 动作参数
        public float amount = 0.2f;    // 情绪/关系变化幅度
        public string text = "";       // 记忆内容 / 日志模板（可含 {name} {emotion}）

        public SoulBtNode() { }
        public SoulBtNode(SoulBtNodeType t) { type = t; }
    }

    /// <summary>
    /// 行为树求值上下文：持有魂核实例、最近决策、冷却记录、行为意图输出。
    /// </summary>
    public class SoulBtContext
    {
        public Soul soul;
        public SoulDecision lastDecision;
        public float now;                        // 当前时间（秒）
        public string behaviorIntent = "";       // 树本次求值输出的行为意图
        public string intentText = "";           // 行为意图附带文本（日志/对白模板）
        public Action<string, string> OnIntent;  // (actionName, text) 输出回调
        /// <summary>目标（why）：魂核最近决策的目标（救人/助人/自保/探索...），行为树读它决定 how</summary>
        public string goal = "";

        private readonly Dictionary<string, float> _lastTimes = new Dictionary<string, float>();

        public float GetLastTime(string k)
            => _lastTimes.TryGetValue(k, out var v) ? v : float.MinValue;

        public void SetLastTime(string k, float t) => _lastTimes[k] = t;
    }
}
