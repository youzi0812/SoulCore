using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace SoulCore.Llm
{
    /// <summary>
    /// 开放性 LLM 入口：OpenAI 兼容 API 的轻量客户端（零第三方依赖）。
    /// 供"一句话人格解析"等编辑器工具/运行时功能调用 DeepSeek/Kimi/MiniMax 等任意兼容接口。
    /// </summary>
    public static class SoulLlmClient
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        /// <summary>
        /// 同步调用 OpenAI 兼容 /chat/completions，返回回复文本。失败抛异常（由调用方处理）。
        /// </summary>
        public static string Chat(string baseUrl, string apiKey, string model,
                                  string system, string user, int maxTokens = 512)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("LLM 配置不完整：baseUrl / apiKey / model 不能为空");

            var url = baseUrl.TrimEnd('/');
            if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                // 兼容两种写法：https://api.deepseek.com 或 https://api.deepseek.com/v1
                url += url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? "/chat/completions"
                    : "/v1/chat/completions";
            }

            var payload = "{\"model\":\"" + Escape(model) + "\",\"max_tokens\":" + Math.Max(64, maxTokens) +
                ",\"messages\":[{\"role\":\"system\",\"content\":\"" + Escape(system) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + Escape(user) + "\"}]}";

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var resp = _http.SendAsync(req).GetAwaiter().GetResult();
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("LLM HTTP " + (int)resp.StatusCode + ": " + Truncate(body, 200));

            var content = ExtractContent(body);
            if (string.IsNullOrWhiteSpace(content))
            {
                // 推理模型（如 deepseek-v4）可能把 max_tokens 全部占在推理上，content 被截没。
                // 自动翻倍重试一次（512→1024→2048 封顶）
                if (maxTokens < 2048)
                    return Chat(baseUrl, apiKey, model, system, user, maxTokens * 2);
                throw new Exception("LLM 返回内容为空：推理过程占满 max_tokens，请调大对话最大 Token");
            }
            return content;
        }

        /// <summary>从 OpenAI 兼容响应提取 content 文本</summary>
        private static string ExtractContent(string json)
        {
            var m = Regex.Match(json, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Unescape(m.Groups[1].Value) : "";
        }

        private static string Escape(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string Unescape(string s)
            => s.Replace("\\r", "").Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        private static string Truncate(string s, int n)
            => s.Length <= n ? s : s.Substring(0, n) + "...";
    }
}
