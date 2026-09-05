using System;
using System.Collections.Generic;
using System.IO;
using SoulCore;
using UnityEngine;

namespace SoulCore.Unity
{
    /// <summary>
    /// 世界节点 — 管理魂核世界的全局状态（对齐 6.1.8 方案 SoulWorldNode）：
    /// NPC 注册表、事件传播、世界存档、世界时钟（日终 tick）。
    /// </summary>
    [AddComponentMenu("魂核/World（世界节点）")]
    public class SoulWorldBehaviour : MonoBehaviour
    {
        [Header("事件传播")]
        public SoulEnums.PropagationMode defaultPropagationMode = SoulEnums.PropagationMode.Deferred;
        public int defaultMaxHops = 3;

        [Header("存档")]
        public string saveFileName = SoulSaveService.DefaultSaveFileName;
        public bool autoLoadOnStart = true;
        public bool autoSaveOnDestroy = true;
        [Tooltip("自动存档间隔（秒），0 关闭")]
        public float autoSaveInterval = 120f;

        [Header("世界时钟")]
        public bool runDailyAutoTick = true;
        [Tooltip("多少真实秒等于游戏里的一天")]
        public float secondsPerGameDay = 300f;

        /// <summary>事件总线（世界事件传播）</summary>
        public SoulWorldEventBus bus { get; private set; }

        private readonly Dictionary<string, SoulNpcBehaviour> _npcs = new Dictionary<string, SoulNpcBehaviour>();
        private float _dayTimer = 0f;
        private float _saveTimer = 0f;

        private void Awake()
        {
            bus = new SoulWorldEventBus();
            bus.propagationMode = defaultPropagationMode;
            bus.propagation.maxHops = defaultMaxHops;
        }

        private void Start()
        {
            if (autoLoadOnStart) LoadWorld();
        }

        private void Update()
        {
            if (runDailyAutoTick) TickDaily();
            if (autoSaveInterval > 0) TickAutoSave();
        }

        private void OnDestroy()
        {
            if (autoSaveOnDestroy) SaveWorld();
        }

        // ==================== NPC 注册表 ====================

        public void RegisterNpc(SoulNpcBehaviour npc)
        {
            if (npc == null || string.IsNullOrEmpty(npc.soulId)) return;
            _npcs[npc.soulId] = npc;
        }

        public void UnregisterNpc(string soulId)
        {
            if (!string.IsNullOrEmpty(soulId)) _npcs.Remove(soulId);
        }

        public SoulNpcBehaviour GetNpc(string soulId)
            => _npcs.TryGetValue(soulId, out var npc) ? npc : null;

        /// <summary>广播世界事件（NPC 行为产生的）</summary>
        public void EmitWorldEvent(SoulWorldEvent evt)
            => bus?.Report(evt.event_type, evt.actor_id, evt.target_id, evt.intensity);

        // ==================== 世界时钟 ====================

        private void TickDaily()
        {
            _dayTimer += Time.deltaTime;
            if (_dayTimer < secondsPerGameDay) return;
            _dayTimer = 0f;
            foreach (var kv in _npcs)
                kv.Value?.soul?.DailyReset();
        }

        private void TickAutoSave()
        {
            _saveTimer += Time.deltaTime;
            if (_saveTimer < autoSaveInterval) return;
            _saveTimer = 0f;
            SaveWorld();
        }

        // ==================== 世界存档 ====================

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        /// <summary>导出整个世界（NPC 核心/关系/记忆 + 事件日志）</summary>
        public Dictionary<string, object> ExportWorld()
        {
            var bindings = new List<KeyValuePair<string, Soul>>();
            foreach (var kv in _npcs)
                if (kv.Value?.soul != null)
                    bindings.Add(new KeyValuePair<string, Soul>(kv.Key, kv.Value.soul));
            return SoulSaveService.CaptureWorld(bindings, bus);
        }

        public void SaveWorld()
        {
            try
            {
                var json = SoulSaveService.Serialize(ExportWorld());
                File.WriteAllText(SavePath, json, System.Text.Encoding.UTF8);
                Debug.Log($"[SoulCore] 世界已保存：{SavePath}（{json.Length} 字符）");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoulCore] 存档失败：{e.Message}");
            }
        }

        public SoulSaveService.ApplyResult LoadWorld()
        {
            var result = new SoulSaveService.ApplyResult();
            try
            {
                if (!File.Exists(SavePath)) return result;
                var json = File.ReadAllText(SavePath, System.Text.Encoding.UTF8);
                var data = SoulSaveService.TryDeserialize(json);
                var bindings = new List<KeyValuePair<string, Soul>>();
                foreach (var kv in _npcs)
                    if (kv.Value?.soul != null)
                        bindings.Add(new KeyValuePair<string, Soul>(kv.Key, kv.Value.soul));
                result = SoulSaveService.ApplyWorld(data, bindings, bus);
                Debug.Log($"[SoulCore] 世界已加载：{result.appliedNpcCount} NPC，{result.appliedEventCount} 事件");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoulCore] 读档失败：{e.Message}");
            }
            return result;
        }
    }
}
