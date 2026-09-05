using System;
using System.Collections;
using System.Collections.Generic;
using SoulCore;
using SoulCore.BehaviorTree;
using UnityEngine;

namespace SoulCore.Unity
{
    /// <summary>
    /// NPC 场景代理 — 持有 Soul 实例，管理场景生命周期（对齐 6.1.8 方案 SoulNpcNode）。
    /// 负责创建 Soul、应用配置、注册到 World、驱动每日循环、转发信号。
    /// </summary>
    [AddComponentMenu("魂核/NPC（单个角色）")]
    public class SoulNpcBehaviour : MonoBehaviour
    {
        [Header("身份")]
        public string soulId = "";
        public string displayName = "";

        [Header("🎨 外观")]
        [Tooltip("NPC 立绘（对话窗口头像）")]
        public Texture2D portrait;

        [Header("配置")]
        [Tooltip("魂核 NPC 配置资产（创建：右键 → 创建 → 魂核 → NPC 配置）。不填则用默认人格。")]
        public SoulConfig config;

        [Header("每日循环")]
        [Tooltip("多少真实秒等于游戏里的一天")]
        public float secondsPerGameDay = 300f;
        public bool runDailyAutoReset = true;

        [Header("资源")]
        [Range(0f, 1f)] public float resourceScarcity = 0.5f;
        public bool useScarcityAsProvider = true;

        [Header("频率限制")]
        [Range(0f, 60f)] public float maxPerceptionsPerSecond = 5f;
        public bool enforceBudget = true;

        /// <summary>持有的魂核实例（纯逻辑层）</summary>
        public Soul soul { get; private set; }

        /// <summary>最近一次决策</summary>
        public SoulDecision lastDecision { get; private set; }

        /// <summary>世界节点引用</summary>
        public SoulWorldBehaviour worldNode;

        // ==================== 事件（转发给场景树） ====================
        public System.Action<SoulDecision> OnDecisionReceived;
        public System.Action<string, float, float> OnEmotionChanged;
        public System.Action<Memory> OnMemoryStored;
        public System.Action<string, int, int, string> OnRelationshipChanged;

        private float _dayTimer = 0f;
        private float _nextPerceiveTime = 0f;

        private void Awake()
        {
            EnsureSoul();
            if (worldNode == null) worldNode = FindObjectOfType<SoulWorldBehaviour>();
            if (worldNode != null) worldNode.RegisterNpc(this);
            BindEvents();
        }

        private void Update()
        {
            // 每日循环（仅运行模式且开启时）
            if (runDailyAutoReset && soul != null && soul.IsRunning())
            {
                _dayTimer += Time.deltaTime;
                if (_dayTimer >= secondsPerGameDay)
                {
                    _dayTimer = 0f;
                    soul.DailyReset();
                }
            }
            // 行为树（1.8.0）：读魂核状态，按频率输出行为意图
            if (useBehaviorTree && behaviorTree != null && soul != null)
                TickBehaviorTree(Time.deltaTime);

            // 情绪光环（演示）：头顶色球随主导情绪变化
            if (showEmotionAura)
            {
                EnsureAura();
                UpdateAura();
            }
        }

        private void OnDestroy()
        {
            UnbindEvents();
            if (worldNode != null) worldNode.UnregisterNpc(soulId);
        }

        // ==================== 对外 API ====================

        /// <summary>感知入口（带频率限制）。编辑模式下也能用（自初始化 Soul，且不限制频率）。</summary>
        public SoulDecision Perceive(PerceptionContext ctx)
        {
            EnsureSoul();
            if (soul == null) return null;
            // 频率限制仅在运行模式生效：编辑模式下 Time.time 不前进，
            // 若照常判断会把后续 Perceive 全部拦截成"返回上次决策"（复读 bug）
            if (enforceBudget && Application.isPlaying && Time.time < _nextPerceiveTime) return lastDecision;
            _nextPerceiveTime = Time.time + 1f / Mathf.Max(0.1f, maxPerceptionsPerSecond);
            lastDecision = soul.Perceive(ctx);
            return lastDecision;
        }

        /// <summary>导出灵魂档案（调试/UI 用）</summary>
        public Dictionary<string, object> GetProfile() => soul?.GetProfile();

        [Header("LLM 对话（需在配置资产的 LLM 区填写接口）")]
        [Tooltip("自定义角色提示词前缀（留空 = 自动注入人格/情绪/行为倾向）")]
        public string chatSystemPromptOverride = "";
        [Tooltip("对话最大输出 token（推理模型会被推理占掉一部分，1024 起步）")]
        [Min(64)] public int chatMaxTokens = 1024;

        [Header("🌳 行为树（1.8.0，可选）")]
        [Tooltip("启用专属行为树（读魂核状态决定行为意图）。关闭 = 用决策引擎默认行为")]
        public bool useBehaviorTree = false;
        [Tooltip("行为树资产（右键 → 创建 → 魂核 → 行为树）")]
        public SoulBehaviorTree behaviorTree;
        [Tooltip("树评估频率（秒）")]
        [Min(0.1f)] public float treeTickInterval = 1f;
        [Tooltip("行为意图输出事件（actionName）")]
        public System.Action<string> OnBehaviorIntent;

        [Header("🗣️ LLM 叙事（1.8.0）")]
        [Tooltip("行为树 LlmNarration 节点生成台词（低频异步，防烧 token）")]
        public bool llmNarrationEnabled = true;
        [Tooltip("叙事生成最小间隔（秒）")]
        [Min(10f)] public float llmNarrationInterval = 60f;

        /// <summary>
        /// LLM 真实对话入口：先走魂核感知（情绪/记忆/决策更新），
        /// 再以「人格 + 情绪 + 行为倾向」为提示词调用 LLM 生成对白（声道）。
        /// 需要配置资产开启 LLM 并填写接口。
        /// </summary>
        public string Chat(string userText)
        {
            EnsureSoul();
            if (soul == null) return "[魂核] 魂核未初始化";
            if (config == null || !config.llmEnabled)
                return "[魂核] 未启用 LLM：请先在 NPC 配置资产的 LLM 区开启并填写接口";
            if (string.IsNullOrWhiteSpace(userText)) return "";

            // 1) 感知：对话作为事件喂给魂核（更新情绪/记忆/决策倾向）
            var decision = Perceive(new PerceptionContext
            {
                event_type = "conversation",
                intensity = 0.5f,
                content = userText
            });

            // 2) 构建系统提示（人格/情绪/行为倾向）
            var system = string.IsNullOrEmpty(chatSystemPromptOverride)
                ? BuildChatSystemPrompt(decision)
                : chatSystemPromptOverride;

            // 3) 调用 LLM 生成对白
            try
            {
                return SoulCore.Llm.SoulLlmClient.Chat(
                    config.llmBaseUrl, config.llmApiKey, config.llmModel,
                    system, userText, chatMaxTokens);
            }
            catch (Exception e)
            {
                return "[魂核] LLM 调用失败：" + e.Message;
            }
        }

        private string BuildChatSystemPrompt(SoulDecision decision)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("你是游戏世界里的NPC【").Append(displayName).Append("】，现在正与玩家对话。\n");

            // 人格：挑偏离中值最大的 5 个特质（最有辨识度的）
            var t = soul.personality.traits;
            sb.Append("你的人格特质：");
            var list = new List<KeyValuePair<string, float>>(t);
            list.Sort((a, b) =>
                Mathf.Abs(b.Value - 0.5f).CompareTo(Mathf.Abs(a.Value - 0.5f)));
            var count = 0;
            foreach (var kv in list)
            {
                if (count++ >= 5) break;
                sb.Append(kv.Key).Append(Math.Round(kv.Value, 2)).Append("，");
            }
            if (sb[sb.Length - 1] == '，') sb.Length -= 1;
            sb.Append("。\n");

            // 情绪
            sb.Append("当前情绪：").Append(soul.emotion.GetDominant()).Append("。\n");

            // 行为倾向（决策引擎的输出）
            if (decision != null)
                sb.Append("面对当前情境你的行为倾向是：").Append(decision.action)
                  .Append("（").Append(decision.explanation).Append("）。\n");

            sb.Append("请完全以这个角色的身份用中文自然回应玩家：简短真诚，符合你的人格与情绪，"
                      + "不要使用括号做动作描写，不要用【】标记，不要谈论你是AI。");
            return sb.ToString();
        }

        // ==================== 运行时对话 UI（点击 NPC 弹出） ====================

        [Header("💬 对话 UI（运行时点击 NPC 触发，需 Collider）")]
        [Tooltip("启用对话窗口（点击 NPC 打开/关闭）")]
        public bool enableChatUI = true;

        [Header("🎨 情绪光环（演示）")]
        [Tooltip("NPC 头顶显示情绪光环（颜色随主导情绪变化）")]
        public bool showEmotionAura = true;
        [Tooltip("光环半径")]
        [Range(0.2f, 1f)] public float auraSize = 0.35f;

        private GameObject _aura;
        private Renderer _auraRenderer;
        private Material _auraMat;
        private float _auraTimer = 0f;
        [Tooltip("对话历史最多保留条数（超出从最早开始丢）")]
        [Min(4)] public int chatHistoryLimit = 12;
        [Tooltip("发送后显示'思考中...'（真实 LLM 调用需要几秒）")]
        public bool showTyping = true;

        private readonly List<string> _chatHistory = new List<string>();
        private string _chatInput = "";
        private string _chatStatus = "";
        private Vector2 _chatScroll = Vector2.zero;
        private bool _chatOpen = false;
        private bool _chatBusy = false;
        // volatile：后台线程写、主线程读的完成标志（无 volatile 会被 JIT 缓存，协程永远等不到）
        private volatile bool _chatDone = false;

        private void OnMouseDown()
        {
            if (enableChatUI) _chatOpen = !_chatOpen;
        }

        private void OnDrawGizmos()
        {
            // Scene 视图标记：紫色线框球（定位用，Game 视图不可见但 Collider 可点击）
            Gizmos.color = new Color(0.6f, 0.4f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        private void OnGUI()
        {
            if (!_chatOpen || !enableChatUI) return;

            var w = 440f;
            var h = 330f;
            var x = (Screen.width - w) / 2f;
            var y = Screen.height - h - 60f;

            GUI.Box(new Rect(x, y, w, h), "");
            // 顶部：NPC 立绘 + 标题
            if (portrait != null)
                GUI.DrawTexture(new Rect(x + 10, y + 10, 72, 72), portrait, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x + 90, y + 8, w - 130, 22f), "💬 " + displayName + "（魂核对话）");
            if (GUI.Button(new Rect(x + w - 30, y + 4, 26, 22f), "✕"))
                _chatOpen = false;

            // 历史区（自动换行：每条按实际文本高度分配，不截断）
            var historyRect = new Rect(x + 10, y + 90, w - 20, h - 154);
            GUI.Box(historyRect, "");
            var textW = w - 52f;

            // 第一遍：计算总高度（供滚动区域）
            var contentH = 0f;
            for (var i = 0; i < _chatHistory.Count; i++)
                contentH += GUI.skin.label.CalcHeight(new GUIContent(_chatHistory[i]), textW) + 4f;
            contentH = Mathf.Max(historyRect.height, contentH);

            // 第二遍：在滚动区域内绘制（坐标是内容区相对坐标，不是屏幕坐标）
            _chatScroll = GUI.BeginScrollView(historyRect, _chatScroll, new Rect(0, 0, w - 44, contentH));
            var yPos = 0f;
            for (var i = 0; i < _chatHistory.Count; i++)
            {
                var lineH = GUI.skin.label.CalcHeight(new GUIContent(_chatHistory[i]), textW);
                GUI.Label(new Rect(4, yPos, textW, lineH), _chatHistory[i]);
                yPos += lineH + 4f;   // 行间距
            }
            GUI.EndScrollView();

            // 状态 + 输入
            GUI.Label(new Rect(x + 10, y + h - 58, w - 20, 20f), _chatStatus);
            _chatInput = GUI.TextField(new Rect(x + 10, y + h - 34, w - 100, 24f), _chatInput, 200);
            if (GUI.Button(new Rect(x + w - 86, y + h - 34, 76, 24f), _chatBusy ? "…" : "发送")
                || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
                SendChat();
        }

        private void SendChat()
        {
            var text = _chatInput.Trim();
            if (string.IsNullOrEmpty(text) || _chatBusy) return;
            _chatInput = "";
            _chatHistory.Add("你：" + text);
            _chatStatus = showTyping ? "思考中..." : "";
            _chatBusy = true;
            StartCoroutine(ChatCoroutine(text));
        }

        private IEnumerator ChatCoroutine(string text)
        {
            Debug.Log("[SoulCore] 对话：发送「" + text + "」");
            // 防御：config 未挂 / LLM 未启用时给出友好提示（不再 NRE）
            if (config == null)
            {
                _chatHistory.Add(displayName + "：[魂核] 未挂 NPC 配置资产（选中 NPC → 配置 字段）");
                _chatBusy = false;
                yield break;
            }
            if (!config.llmEnabled)
            {
                _chatHistory.Add(displayName + "：[魂核] 配置资产未启用 LLM（选中配置资产 → LLM 入口 → 启用）");
                _chatBusy = false;
                yield break;
            }
            // 主线程：魂核感知（更新情绪/记忆/决策倾向）
            InjectChatEmotion(soul, text);   // 先按玩家话语注入情绪（光环/情绪引擎联动）
            var decision = Perceive(new PerceptionContext
            {
                event_type = "conversation",
                intensity = 0.5f,
                content = text
            });
            Debug.Log("[SoulCore] 对话：感知完成，构建提示词");
            var system = string.IsNullOrEmpty(chatSystemPromptOverride)
                ? BuildChatSystemPrompt(decision)
                : chatSystemPromptOverride;

            // UnityWebRequest 协程：Unity 原生、主线程、自带 60 秒超时（无跨线程 NRE）
            string reply = "";
            var started = Time.realtimeSinceStartup;
            yield return LlmRoutine(config.llmBaseUrl, config.llmApiKey, config.llmModel,
                                    system, text, chatMaxTokens, r => reply = r);
            Debug.Log("[SoulCore] 对话：LLM 完成，耗时 "
                + (Time.realtimeSinceStartup - started).ToString("0.0") + " 秒");
            _chatStatus = "";
            _chatHistory.Add(displayName + "：" + reply);
            _chatBusy = false;
            TrimChatHistory();
        }

        /// <summary>LLM 请求协程（UnityWebRequest，主线程安全）。成功/失败都通过 onDone 回传文本。</summary>
        private static IEnumerator LlmRoutine(string baseUrl, string apiKey, string model,
                                              string system, string user, int maxTokens,
                                              System.Action<string> onDone)
        {
            var url = baseUrl.TrimEnd('/');
            if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                url += url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? "/chat/completions"
                    : "/v1/chat/completions";
            var payload = "{\"model\":\"" + EscapeJson(model) + "\",\"max_tokens\":" + Math.Max(64, maxTokens) +
                ",\"messages\":[{\"role\":\"system\",\"content\":\"" + EscapeJson(system) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJson(user) + "\"}]}";

            using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(
                    System.Text.Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);
                req.timeout = 60;
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var content = ExtractChatContent(req.downloadHandler.text);
                    if (string.IsNullOrWhiteSpace(content) && maxTokens < 2048)
                    {
                        // 推理模型占满 max_tokens：翻倍重试（512→1024→2048）
                        yield return LlmRoutine(baseUrl, apiKey, model, system, user, maxTokens * 2, onDone);
                        yield break;
                    }
                    onDone(string.IsNullOrWhiteSpace(content)
                        ? "[魂核] LLM 返回为空：推理占满 Token，请调大对话最大 Token"
                        : content);
                }
                else
                {
                    onDone("[魂核] LLM 请求失败：" + req.error);
                }
            }
        }

        private static string ExtractChatContent(string json)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                json, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? UnescapeJson(m.Groups[1].Value) : "";
        }

        private static string EscapeJson(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string UnescapeJson(string s)
            => s.Replace("\\r", "").Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        private void TrimChatHistory()
        {
            while (_chatHistory.Count > chatHistoryLimit)
                _chatHistory.RemoveAt(0);
        }

        // ==================== 情绪光环（演示） ====================

        private void EnsureAura()
        {
            if (_aura != null || !showEmotionAura) return;
            _aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _aura.name = "情绪光环";
            _aura.transform.SetParent(transform, false);
            _aura.transform.localPosition = new Vector3(0, 1.2f, 0);
            _aura.transform.localScale = Vector3.one * auraSize;
            _auraRenderer = _aura.GetComponent<Renderer>();
            var col = _aura.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);   // 光环不参与点击（避免挡射线）
        }

        private void UpdateAura()
        {
            if (!showEmotionAura || soul == null) return;
            _auraTimer -= Time.deltaTime;
            if (_auraTimer > 0f) return;
            _auraTimer = 0.3f;
            if (_auraRenderer == null) return;
            var emotion = soul.emotion.GetDominant();
            var value = soul.emotion.emotions.TryGetValue(emotion, out var v) ? v : 0.5f;
            // 颜色 × 强度：情绪越强光环越亮
            var color = EmotionColor(emotion) * (0.55f + value * 0.6f);
            color.a = 1f;
            // 独立材质（避免共享材质互相污染）
            if (_auraMat == null)
            {
                _auraMat = new Material(_auraRenderer.sharedMaterial);
                _auraRenderer.material = _auraMat;
            }
            _auraMat.color = color;
        }

        private static Color EmotionColor(string emotion)
        {
            switch (emotion)
            {
                case "joy": return new Color(1f, 0.85f, 0.2f);      // 金黄
                case "hope": return new Color(0.6f, 1f, 0.5f);      // 亮绿
                case "gratitude": return new Color(1f, 0.6f, 0.8f); // 粉
                case "fear": return new Color(0.5f, 0.5f, 1f);      // 蓝
                case "anxiety": return new Color(0.7f, 0.5f, 1f);   // 紫
                case "anger": return new Color(1f, 0.3f, 0.2f);     // 红
                case "sadness": return new Color(0.5f, 0.6f, 0.8f); // 灰蓝
                case "despair": return new Color(0.3f, 0.3f, 0.3f); // 灰
                case "calm": return new Color(0.5f, 0.9f, 0.9f);    // 青
                default: return Color.white;
            }
        }

        /// <summary>按玩家话语关键词注入情绪（光环/情绪引擎联动：普通对话也能看到情绪变化）</summary>
        private static void InjectChatEmotion(Soul soul, string text)
        {
            if (soul == null || string.IsNullOrEmpty(text)) return;
            var e = soul.emotion.emotions;
            if (text.Contains("爱") || text.Contains("喜欢") || text.Contains("太棒")
                || text.Contains("厉害") || text.Contains("开心") || text.Contains("好")
                || text.Contains("谢谢") || text.Contains("感激"))
                e["joy"] = Mathf.Min(1f, e["joy"] + 0.25f);
            else if (text.Contains("生气") || text.Contains("愤怒") || text.Contains("讨厌")
                || text.Contains("可恶") || text.Contains("滚"))
                e["anger"] = Mathf.Min(1f, e["anger"] + 0.25f);
            else if (text.Contains("难过") || text.Contains("伤心") || text.Contains("哭")
                || text.Contains("失落"))
                e["sadness"] = Mathf.Min(1f, e["sadness"] + 0.25f);
            else if (text.Contains("害怕") || text.Contains("恐惧") || text.Contains("救命")
                || text.Contains("危险"))
                e["fear"] = Mathf.Min(1f, e["fear"] + 0.25f);
            else
                e["joy"] = Mathf.Min(1f, e["joy"] + 0.05f);   // 普通对话：轻微喜悦
        }

        // ==================== 行为树（1.8.0） ====================

        private float _treeTimer = 0f;
        private float _lastNarrationTime = 0f;   // LLM 叙事限频
        private SoulBtContext _btCtx;
        private string _lastBehaviorIntent = "";

        /// <summary>最近一次行为树输出的行为意图（供场景/UI 读取）</summary>
        public string lastBehaviorIntent => _lastBehaviorIntent;

        private void TickBehaviorTree(float dt)
        {
            _treeTimer += dt;
            if (_treeTimer < treeTickInterval) return;
            _treeTimer = 0f;

            if (_btCtx == null) _btCtx = new SoulBtContext { soul = soul };
            _btCtx.now = Time.time;
            _btCtx.lastDecision = lastDecision;
            _btCtx.goal = lastDecision != null ? lastDecision.goal : "";   // 目标（why）→ 行为树
            _btCtx.behaviorIntent = "";
            _btCtx.intentText = "";
            _btCtx.OnIntent = (action, text) => OnBehaviorIntent?.Invoke(action);

            behaviorTree.Evaluate(_btCtx);
            // 只在意图变化时打日志（避免每秒刷屏）
            if (!string.IsNullOrEmpty(_btCtx.behaviorIntent) && _btCtx.behaviorIntent != _lastBehaviorIntent)
            {
                _lastBehaviorIntent = _btCtx.behaviorIntent;
                Debug.Log("[SoulCore] 行为树：[" + displayName + "] -> " + _btCtx.behaviorIntent);
            }
            // LLM 叙事：LlmNarration 节点 → 限频异步生成台词（防烧 token）
            if (llmNarrationEnabled && _btCtx.intentText != null && _btCtx.intentText.StartsWith("__llm__:")
                && Time.time - _lastNarrationTime >= llmNarrationInterval)
            {
                _lastNarrationTime = Time.time;
                StartCoroutine(NarrationRoutine(_btCtx.intentText.Substring(7)));
            }
        }

        /// <summary>叙事台词生成（LlmNarration 节点）：模板提示交给 LLM，低频调用</summary>
        private IEnumerator NarrationRoutine(string template)
        {
            if (config == null || !config.llmEnabled) yield break;
            var prompt = template.Replace("{name}", displayName)
                                 .Replace("{emotion}", soul != null ? soul.emotion.GetDominant() : "?")
                                 .Replace("{goal}", lastDecision != null ? lastDecision.goal : "");
            yield return LlmRoutine(config.llmBaseUrl, config.llmApiKey, config.llmModel,
                                    "你是" + displayName + "，用一句话自然说出你的心声，不要括号和【】。",
                                    prompt, 160, r =>
            {
                Debug.Log("[SoulCore] 叙事台词：" + r);
                OnBehaviorIntent?.Invoke("narration:" + r);
            });
        }

        private void EnsureSoul()
        {
            if (soul != null) return;
            soul = new Soul(soulId, string.IsNullOrEmpty(displayName) ? soulId : displayName);
            // 注册日志回调（Core 层无 UnityEngine 依赖，这里接到 Debug.Log，Console 可见涌现/校准变化）
            soul.OnLog = msg => Debug.Log(msg);
            if (config != null)
            {
                config.ApplyTo(soul);   // 应用配置资产（人格/情绪/记忆/决策/关系/模块）
                if (secondsPerGameDay <= 0f && config.secondsPerGameDay > 0f)
                    secondsPerGameDay = config.secondsPerGameDay;
            }
            if (useScarcityAsProvider)
                soul.resourceScarcityProvider = () => resourceScarcity;
        }

        private void BindEvents()
        {
            if (soul == null) return;
            soul.DecisionMade += OnSoulDecision;
            soul.EmotionChanged += OnSoulEmotion;
            soul.MemoryStored += OnSoulMemory;
            soul.RelationshipChanged += OnSoulRelationship;
        }

        private void UnbindEvents()
        {
            if (soul == null) return;
            soul.DecisionMade -= OnSoulDecision;
            soul.EmotionChanged -= OnSoulEmotion;
            soul.MemoryStored -= OnSoulMemory;
            soul.RelationshipChanged -= OnSoulRelationship;
        }

        private void OnSoulDecision(SoulDecision d) => OnDecisionReceived?.Invoke(d);
        private void OnSoulEmotion(string k, float o, float n) => OnEmotionChanged?.Invoke(k, o, n);
        private void OnSoulMemory(Memory m) => OnMemoryStored?.Invoke(m);
        private void OnSoulRelationship(string t, int o, int n, string r) => OnRelationshipChanged?.Invoke(t, o, n, r);
    }
}
