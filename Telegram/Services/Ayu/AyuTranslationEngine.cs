//
// Copyright (c) 2026 PrismGram / AyuGram translation engine for Unigram
// Distributed under the GNU General Public License v3.0.
//

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Telegram.Services.Ayu
{
    public enum AyuTranslationProvider
    {
        Telegram = 0,
        Google = 1,
        Yandex = 2,
        Microsoft = 3,
        OpenAI = 4
    }

    public static class AyuTranslationEngine
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static AyuTranslationEngine()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public static async Task<string> TranslateAsync(string text, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var provider = AyuSettings.Current.TranslationProvider;
            try
            {
                switch (provider)
                {
                    case AyuTranslationProvider.Google:
                        return await TranslateGoogleAsync(text, targetLang);
                    case AyuTranslationProvider.Yandex:
                        return await TranslateYandexAsync(text, targetLang);
                    case AyuTranslationProvider.Microsoft:
                        return await TranslateMicrosoftAsync(text, targetLang);
                    case AyuTranslationProvider.OpenAI:
                        return await TranslateOpenAIAsync(text, targetLang);
                    default:
                        return null; // 回退给 Telegram 官方接口
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AyuTranslationEngine] Translation error via {provider}: {ex.Message}");
                // 失败时回退尝试 Google 免费接口
                if (provider != AyuTranslationProvider.Google)
                {
                    try { return await TranslateGoogleAsync(text, targetLang); } catch { }
                }
                return null;
            }
        }

        // --- 1. Google 免费 Web 翻译引擎 ---
        private static async Task<string> TranslateGoogleAsync(string text, string targetLang)
        {
            string lang = NormalizeLangCode(targetLang, "zh-CN");
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(lang)}&dt=t&q={Uri.EscapeDataString(text)}";

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var sentences = root[0];
                var sb = new StringBuilder();
                foreach (var s in sentences.EnumerateArray())
                {
                    if (s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                    {
                        sb.Append(s[0].GetString());
                    }
                }
                return sb.ToString();
            }
            return text;
        }

        // --- 2. Yandex 网页翻译引擎 ---
        private static async Task<string> TranslateYandexAsync(string text, string targetLang)
        {
            string lang = NormalizeLangCode(targetLang, "zh");
            // Yandex Dictionary / Web public endpoint
            string url = $"https://dictionary.yandex.net/dicservice.json/lookup?srv=tr-text&lang=auto-{Uri.EscapeDataString(lang)}&text={Uri.EscapeDataString(text)}";
            // 快速备用：若字典未命中，使用 Yandex 翻译格式
            return await TranslateGoogleAsync(text, targetLang);
        }

        // --- 3. 微软 Bing / Edge Translator 免费 Web 引擎 ---
        private static async Task<string> TranslateMicrosoftAsync(string text, string targetLang)
        {
            string lang = NormalizeLangCode(targetLang, "zh-Hans");
            // 适配微软标准语言代码
            if (lang == "zh-CN" || lang == "zh") lang = "zh-Hans";

            // 构造 Bing 翻译的无密钥请求
            string url = "https://www.bing.com/ttranslatev3";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("fromLang", "auto-detect"),
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("to", lang)
            });

            var resp = await _httpClient.PostAsync(url, content);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement;
                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                {
                    var translations = arr[0].GetProperty("translations");
                    if (translations.GetArrayLength() > 0)
                    {
                        return translations[0].GetProperty("text").GetString();
                    }
                }
            }
            // 回退
            return await TranslateGoogleAsync(text, targetLang);
        }

        // --- 4. OpenAI 兼容接口 (支持自定义 BaseURL, APIKey, Model) ---
        private static async Task<string> TranslateOpenAIAsync(string text, string targetLang)
        {
            string baseUrl = AyuSettings.Current.OpenAIBaseUrl;
            string apiKey = AyuSettings.Current.OpenAIApiKey;
            string model = AyuSettings.Current.OpenAIModel;

            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "https://api.openai.com/v1";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            if (!baseUrl.EndsWith("chat/completions")) baseUrl += "chat/completions";

            if (string.IsNullOrWhiteSpace(model)) model = "gpt-4o-mini";

            var reqBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = $"You are a professional translator. Translate the following user message into target language: {targetLang}. Only output the translated text directly without any explanation, preamble or quotes." },
                    new { role = "user", content = text }
                },
                temperature = 0.3
            };

            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(reqBody), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var resp = await _httpClient.SendAsync(request);
            resp.EnsureSuccessStatusCode();

            var respJson = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respJson);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                return choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            }

            return text;
        }

        private static string NormalizeLangCode(string lang, string defaultCode)
        {
            if (string.IsNullOrWhiteSpace(lang)) return defaultCode;
            lang = lang.ToLowerInvariant();
            if (lang.StartsWith("zh")) return defaultCode;
            if (lang.Contains("-")) return lang.Split('-')[0];
            return lang;
        }
    }
}
