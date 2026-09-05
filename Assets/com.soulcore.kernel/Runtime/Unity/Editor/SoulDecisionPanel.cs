using UnityEditor;
using UnityEngine;
using SoulCore;
using SoulCore.Unity;

namespace SoulCore
{
    /// <summary>
    /// 决策调试面板 — 把「这个 NPC 为什么选这个动作」画成四根柱子。
    /// 选 NPC → 选情境 → 触发决策 → 看人格/情绪/记忆/资源四维分解 + 综合分 + 动作。
    /// 调参从盲人摸象变成所见即所得。
    /// </summary>
    public class SoulDecisionPanel : EditorWindow
    {
        private SoulNpcBehaviour _npc;
        private int _sitIdx = 0;
        private float _intensity = 0.6f;
        private Vector2 _scroll;

        private static readonly string[] Situations =
        {
            "help", "conversation", "evacuation", "rescue", "learn",
            "interrogate_pressure", "interrogate_empathy", "interrogate_evidence",
            "interrogate_probe", "default",
        };

        [MenuItem("魂核/决策调试面板")]
        public static void Open() => GetWindow<SoulDecisionPanel>("魂核 · 决策调试");

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "选中 NPC → 选情境 → 触发决策 → 看四根柱子（人格/情绪/记忆/资源）如何决定动作",
                MessageType.Info);

            _npc = (SoulNpcBehaviour)EditorGUILayout.ObjectField("NPC", _npc, typeof(SoulNpcBehaviour), true);
            _sitIdx = EditorGUILayout.Popup("情境", _sitIdx, Situations);
            _intensity = EditorGUILayout.Slider("强度", _intensity, 0f, 1f);

            using (new EditorGUI.DisabledScope(_npc == null))
            {
                if (GUILayout.Button("🎯 触发决策（Perceive）", GUILayout.Height(30)))
                {
                    _npc.Perceive(new PerceptionContext
                    {
                        event_type = Situations[_sitIdx],
                        intensity = _intensity,
                        content = "决策调试",
                    });
                }
            }

            EditorGUILayout.Space(10);

            if (_npc == null)
            {
                EditorGUILayout.HelpBox("请先在场景中选中一个魂核 NPC", MessageType.Warning);
            }
            else if (_npc.soul == null)
            {
                EditorGUILayout.HelpBox("NPC 魂核未初始化（Play 模式或调用 Perceive 后自动初始化）", MessageType.Info);
            }
            else
            {
                DrawBreakdown(_npc.soul.decision.LastBreakdown);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBreakdown(DecisionEngine.DecisionBreakdown bd)
        {
            if (bd == null)
            {
                EditorGUILayout.HelpBox("尚未触发决策——点上方「触发决策」按钮", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("决策结果", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("动作", bd.action);
            EditorGUILayout.LabelField("置信度", bd.confidence);
            EditorGUILayout.Space(6);

            DrawBar("人格", bd.personality, new Color(0.55f, 0.75f, 1f));
            DrawBar("情绪", bd.emotion, new Color(1f, 0.85f, 0.4f));
            DrawBar("记忆", bd.memory, new Color(0.65f, 1f, 0.55f));
            DrawBar("资源", bd.resource, new Color(1f, 0.6f, 0.6f));
            EditorGUILayout.Space(4);
            DrawBar("综合", bd.final, new Color(1f, 1f, 1f));

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(bd.explanation, MessageType.Info);
        }

        private void DrawBar(string label, float value, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(46));

            var rect = GUILayoutUtility.GetRect(120, 18);
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.17f));      // 轨道底
            var fill = rect;
            fill.width = rect.width * Mathf.Clamp01(value);
            if (fill.width > 1f)
                EditorGUI.DrawRect(fill, color);                            // 填充

            EditorGUILayout.LabelField(value.ToString("0.00"), GUILayout.Width(42));
            EditorGUILayout.EndHorizontal();
        }
    }
}
