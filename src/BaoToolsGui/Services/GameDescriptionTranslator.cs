using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BaoToolsGui.Services;

public static class GameDescriptionTranslator
{
    private static readonly HttpClient Http = AppHttp.Create(TimeSpan.FromSeconds(5));
    private static readonly ConcurrentDictionary<string, string> MemoryCache = new();
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BaoToolsGui",
        "translations");

    static GameDescriptionTranslator()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
        }
        catch { /* best effort */ }
    }

    public static string GetTargetLanguageCode()
    {
        var culture = CultureInfo.CurrentUICulture;
        string name = culture.Name;
        string twoLetter = culture.TwoLetterISOLanguageName.ToLowerInvariant();

        if (twoLetter == "zh")
        {
            return name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("HK", StringComparison.OrdinalIgnoreCase)
                ? "zh-TW"
                : "zh-CN";
        }

        if (twoLetter == "pt" && name.Contains("BR", StringComparison.OrdinalIgnoreCase))
            return "pt";

        if (twoLetter == "es" && name.Contains("419", StringComparison.OrdinalIgnoreCase))
            return "es";

        return twoLetter;
    }

    public static string GetOriginalLabel(string targetLang) => targetLang switch
    {
        "vi" => "🌐 Bản gốc",
        "zh-CN" or "zh-TW" => "🌐 原文",
        "ja" => "🌐 原文",
        "ko" => "🌐 원문",
        "ru" or "uk" => "🌐 Оригинал",
        "fr" => "🌐 Original",
        "de" => "🌐 Original",
        "es" => "🌐 Original",
        "pt" => "🌐 Original",
        "it" => "🌐 Originale",
        "pl" => "🌐 Oryginał",
        "tr" => "🌐 Orijinal",
        "th" => "🌐 ข้อความเดิม",
        "id" => "🌐 Teks Asli",
        "ar" => "🌐 النص الأصلي",
        _ => "🌐 Original"
    };

    public static string GetTranslateLabel(string targetLang) => targetLang switch
    {
        "vi" => "🌐 Dịch sang Tiếng Việt",
        "zh-CN" => "🌐 翻译为中文",
        "zh-TW" => "🌐 翻譯為中文",
        "ja" => "🌐 日本語に翻訳",
        "ko" => "🌐 한국어로 번역",
        "ru" => "🌐 Перевести на русский",
        "uk" => "🌐 Перекласти",
        "fr" => "🌐 Traduire en français",
        "de" => "🌐 Auf Deutsch übersetzen",
        "es" => "🌐 Traducir al español",
        "pt" => "🌐 Traduzir para português",
        "it" => "🌐 Traduci in italiano",
        "pl" => "🌐 Przetłumacz na polski",
        "tr" => "🌐 Türkçe'ye çevir",
        "th" => "🌐 แปลเป็นภาษาไทย",
        "id" => "🌐 Terjemahkan",
        "ar" => "🌐 ترجمة",
        _ => "🌐 Translate"
    };

    public static string GetTranslatingLabel(string targetLang) => targetLang switch
    {
        "vi" => "⏳ Đang dịch...",
        "zh-CN" or "zh-TW" => "⏳ 翻译中...",
        "ja" => "⏳ 翻訳中...",
        "ko" => "⏳ 번역 중...",
        "ru" or "uk" => "⏳ Перевод...",
        "fr" => "⏳ Traduction...",
        "de" => "⏳ Übersetzen...",
        "es" => "⏳ Traduciendo...",
        "pt" => "⏳ Traduzindo...",
        "it" => "⏳ Traduzione...",
        _ => "⏳ Translating..."
    };

    private static string GetCacheKey(string text, string targetLang)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"{targetLang}:{text}"));
        return Convert.ToHexString(hash);
    }

    public static bool TryGetCached(string text, string targetLang, out string? translated)
    {
        translated = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string key = GetCacheKey(text, targetLang);
        if (MemoryCache.TryGetValue(key, out translated))
            return true;

        try
        {
            string path = Path.Combine(CacheDir, $"{key}.txt");
            if (File.Exists(path))
            {
                translated = File.ReadAllText(path, Encoding.UTF8);
                MemoryCache[key] = translated;
                return true;
            }
        }
        catch { /* best effort */ }

        return false;
    }

    public static async Task<string> TranslateAsync(string text, string targetLang = "vi", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (TryGetCached(text, targetLang, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached!;

        try
        {
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
            using var res = await Http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return text;

            string json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            
            // Format is [[["translated text", "original text", ...], ...], ...]
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var sentences = doc.RootElement[0];
                var sb = new StringBuilder();
                foreach (var s in sentences.EnumerateArray())
                {
                    if (s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                    {
                        string? part = s[0].GetString();
                        if (!string.IsNullOrEmpty(part))
                            sb.Append(part);
                    }
                }

                string result = sb.ToString();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    string key = GetCacheKey(text, targetLang);
                    MemoryCache[key] = result;
                    try
                    {
                        Directory.CreateDirectory(CacheDir);
                        await File.WriteAllTextAsync(Path.Combine(CacheDir, $"{key}.txt"), result, Encoding.UTF8, ct);
                    }
                    catch { /* best effort */ }
                    return result;
                }
            }
        }
        catch
        {
            // Offline or rate-limited: gracefully return original text
        }

        return text;
    }
}
