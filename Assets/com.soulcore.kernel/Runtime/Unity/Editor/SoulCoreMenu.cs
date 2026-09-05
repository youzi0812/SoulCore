using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SoulCore.BehaviorTree;
using SoulCore.Unity;

namespace SoulCore
{
    /// <summary>
    /// 一键演示菜单：创建 3 个示例 NPC 配置（铁匠/渔夫/药师）+ 示例行为树 + 人格涌现自测。
    /// 使用：团结菜单栏 → 魂核 → 对应菜单项。
    /// 注意：演示配置不读取任何本机路径或 Key（隐私与可移植性）——LLM Key 请手动填入。
    /// </summary>
    public static class SoulCoreMenu
    {
        [MenuItem("魂核/创建演示 NPC 配置（3 个示例 + 自动挂场景）")]
        public static void CreateDemoNpcConfigs()
        {
            // LLM Key 不自动读取（隐私）：请用户手动填入
            var key = "";

            if (!AssetDatabase.IsValidFolder("Assets/SoulConfigs"))
                AssetDatabase.CreateFolder("Assets", "SoulConfigs");

            // 创建/更新 3 个示例配置（人格 + LLM 开启）
            var smith = Ensure("SmithConfig", new Dictionary<string, float>
            {
                ["courage"] = 0.9f, ["compassion"] = 0.8f, ["fearTendency"] = 0.1f,
                ["persistence"] = 0.85f, ["honesty"] = 0.8f,
            }, key);
            var fisherman = Ensure("FishermanConfig", new Dictionary<string, float>
            {
                ["persistence"] = 0.85f, ["patience"] = 0.8f, ["courage"] = 0.7f,
                ["compassion"] = 0.6f, ["curiosity"] = 0.6f,
            }, key);
            var herbalist = Ensure("HerbalistConfig", new Dictionary<string, float>
            {
                ["warmth"] = 0.85f, ["compassion"] = 0.9f, ["patience"] = 0.8f,
                ["honesty"] = 0.8f, ["fearTendency"] = 0.3f,
            }, key);
            AssetDatabase.SaveAssets();

            // 自动挂到场景 NPC（按名字匹配）
            var count = 0;
            foreach (var npc in Object.FindObjectsOfType<SoulNpcBehaviour>())
            {
                if (npc.name.Contains("铁匠")) { npc.config = smith; count++; }
                else if (npc.name.Contains("渔夫")) { npc.config = fisherman; count++; }
                else if (npc.name.Contains("药师")) { npc.config = herbalist; count++; }
                EditorUtility.SetDirty(npc);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[SoulCore] 演示配置已创建并挂载 " + count + " 个 NPC | LLM Key 请手动填入");
        }

        private static SoulConfig Ensure(string name, Dictionary<string, float> traits, string key)
        {
            var path = "Assets/SoulConfigs/" + name + ".asset";
            var cfg = AssetDatabase.LoadAssetAtPath<SoulConfig>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<SoulConfig>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            foreach (var kv in traits)
            {
                var f = typeof(SoulConfig).GetField(kv.Key);
                if (f != null) f.SetValue(cfg, kv.Value);
            }
            cfg.llmEnabled = true;
            cfg.llmBaseUrl = "https://api.deepseek.com/v1/chat/completions";
            cfg.llmModel = "deepseek-v4-flash";
            cfg.llmApiKey = key;   // 为空则用户手动填
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        [MenuItem("魂核/创建示例行为树（并挂到 NPC）")]
        public static void CreateDemoBehaviorTree()
        {
            // 1) 创建树资产（若不存在）
            var path = "Assets/SoulConfigs/SoulBehaviorTree.asset";
            var tree = AssetDatabase.LoadAssetAtPath<SoulBehaviorTree>(path);
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<SoulBehaviorTree>();
                AssetDatabase.CreateAsset(tree, path);
            }

            // 2) 构建示例树：Selector(根) -> [记忆命中"落水" → 救人] / [共情>0.6 → 相助] / [默认游荡]
            tree.root = new SoulBtNode(SoulBtNodeType.Selector) { key = "root" };

            var rescue = new SoulBtNode(SoulBtNodeType.Sequence) { key = "rescue" };
            rescue.children.Add(new SoulBtNode(SoulBtNodeType.MemoryCondition)
                { key = "rescue", recentCount = 5, threshold = 0.3f, op = 0 });
            rescue.children.Add(new SoulBtNode(SoulBtNodeType.Action)
                { key = "go_rescue", text = "冲去救人" });
            tree.root.children.Add(rescue);

            var help = new SoulBtNode(SoulBtNodeType.Sequence) { key = "help" };
            help.children.Add(new SoulBtNode(SoulBtNodeType.TraitCondition)
                { key = "compassion", threshold = 0.6f, op = 0 });
            help.children.Add(new SoulBtNode(SoulBtNodeType.Action)
                { key = "help_others", text = "出手相助" });
            tree.root.children.Add(help);

            tree.root.children.Add(new SoulBtNode(SoulBtNodeType.Action)
                { key = "wander", text = "继续日常" });

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            // 3) 挂到所有场景 NPC
            var count = 0;
            foreach (var npc in Object.FindObjectsOfType<SoulNpcBehaviour>())
            {
                npc.useBehaviorTree = true;
                npc.behaviorTree = tree;
                EditorUtility.SetDirty(npc);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[SoulCore] 示例行为树已创建并挂载 " + count + " 个 NPC（Play 后 Console 看行为意图）");
        }

        /// <summary>人格涌现自测：构造 12 条愤怒记忆 → DailyReset → 看易怒性是否上升</summary>
        [MenuItem("魂核/人格涌现自测（Console 看结果）")]
        public static void TestPersonalityEmergence()
        {
            var soul = new Soul("test", "涌现测试");
            soul.personality.Set("angerTendency", 0.3f);
            var before = soul.personality.Get("angerTendency");
            for (int i = 0; i < 12; i++)
                soul.memory.Store(new Memory { content = "第" + i + "次被激怒", emotion = "anger", importance = 7 });
            soul.DailyReset();
            var after = soul.personality.Get("angerTendency");
            var ok = after > before;
            Debug.Log("[SoulCore] 涌现自测：易怒性 " + before.ToString("0.00") + " -> "
                + after.ToString("0.00") + (ok ? "  PASS（经历塑造人格生效）" : "  CHECK（无变化）"));
        }

        /// <summary>
        /// AI 测 AI 仿真：批量跑 5 人格 × 6 情境 × 20 次，看决策分布是否符合设计预期。
        /// 用于回归保障（改决策引擎后跑一次，防止某类人格被"拍扁"）。
        /// </summary>
        [MenuItem("魂核/AI 仿真：决策分布（Console 看结果）")]
        public static void RunAiSimulation()
        {
            var personalities = new Dictionary<string, Dictionary<string, float>>
            {
                ["勇者"] = new() { ["courage"] = 0.9f, ["fearTendency"] = 0.1f, ["compassion"] = 0.8f },
                ["谨慎者"] = new() { ["courage"] = 0.2f, ["fearTendency"] = 0.8f, ["rationality"] = 0.8f },
                ["温暖者"] = new() { ["warmth"] = 0.9f, ["compassion"] = 0.9f, ["selfishness"] = 0.1f },
                ["冷漠者"] = new() { ["warmth"] = 0.2f, ["compassion"] = 0.2f, ["selfishness"] = 0.8f },
                ["好奇者"] = new() { ["curiosity"] = 0.9f, ["creativity"] = 0.8f },
            };
            var situations = new[] { "help", "conversation", "evacuation", "rescue", "learn", "interrogate_pressure" };
            const int runs = 20;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== AI 仿真：决策分布（5 人格 × 6 情境 × 20 次） ===");
            foreach (var kv in personalities)
            {
                foreach (var sit in situations)
                {
                    var counts = new Dictionary<string, int>();
                    for (var i = 0; i < runs; i++)
                    {
                        var soul = new Soul(kv.Key + i, kv.Key);
                        foreach (var t in kv.Value) soul.personality.Set(t.Key, t.Value);
                        var d = soul.Perceive(new PerceptionContext
                        {
                            event_type = sit,
                            intensity = 0.8f,
                            content = sit
                        });
                        if (d != null)
                            counts[d.action] = counts.TryGetValue(d.action, out var c) ? c + 1 : 1;
                    }
                    var top = new System.Text.StringBuilder();
                    foreach (var c in counts) top.Append(c.Key).Append("×").Append(c.Value).Append(" ");
                    sb.AppendLine(kv.Key + " | " + sit + " => " + top.ToString().Trim());
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 人格涌现自测：构造 12 条愤怒 + 3 条背叛记忆 → DailyReset → Console 看特质变化。
        /// 验证"经历塑造人格 + 阈值缓动校准"（不需要走完整游戏流程）。
        /// </summary>
        [MenuItem("魂核/人格涌现自测（经历塑造人格）")]
        public static void RunEmergenceSelfTest()
        {
            var soul = new Soul("emergence_test", "测试NPC");
            soul.emergenceEnabled = true;
            soul.emergenceStrength = 1.0f;
            soul.trustAdaptationEnabled = true;
            soul.personality.Set("angerTendency", 0.3f);
            soul.personality.Set("trust", 0.5f);
            var beforeA = soul.personality.traits.TryGetValue("anger_tendency", out var ba) ? ba : 0.5f;
            var beforeT = soul.personality.traits.TryGetValue("trust", out var bt) ? bt : 0.5f;
            for (var i = 0; i < 12; i++)
                soul.memory.Store(new Memory { content = "第" + i + "次被激怒", emotion = "anger", importance = 7 });
            for (var i = 0; i < 3; i++)
                soul.memory.Store(new Memory { content = "被背叛" + i, emotion = "betray", importance = 8 });
            soul.DailyReset();
            var afterA = soul.personality.traits.TryGetValue("anger_tendency", out var aa) ? aa : 0.5f;
            var afterT = soul.personality.traits.TryGetValue("trust", out var at) ? at : 0.5f;
            Debug.Log(string.Format(
                "[SoulCore] 涌现自测：12愤怒+3背叛 → 易怒 {0:0.00}→{1:0.00}，信任 {2:0.00}→{3:0.00}（PASS={4}）",
                beforeA, afterA, beforeT, afterT, afterA > beforeA + 0.01f && afterT < beforeT - 0.01f));
        }
    }
}
