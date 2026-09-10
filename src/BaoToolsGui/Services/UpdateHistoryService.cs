using System.IO;
using System.Text.Json;
using BaoToolsGui.Models;

namespace BaoToolsGui.Services;

/// <summary>
/// Manages the persistence and caching of app update notifications/releases as an update history.
/// Stored in %AppData%\BaoToolsGui\update-history.json.
/// </summary>
public class UpdateHistoryService
{
    private readonly string _historyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BaoToolsGui",
        "update-history.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] DefaultTagList = ["v105.3", "v105.2", "v105.1", "v1005", "1005"];

    public static bool IsDefaultTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string norm = tag.Trim().TrimStart('v', 'V');
        return DefaultTagList.Any(d => string.Equals(d.TrimStart('v', 'V'), norm, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Default built-in releases so the update history in the bell is never blank even offline.
    /// Adapts title and body based on current UI culture (Vietnamese vs English/other).
    /// </summary>
    public static List<UpdateHistoryItem> GetDefaultHistory()
    {
        string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (lang.Equals("vi", StringComparison.OrdinalIgnoreCase))
        {
            return new List<UpdateHistoryItem>
            {
                new()
                {
                    TagName = "v105.3",
                    Title = "BaoTools v105.3 - Gỡ Fix sạch sẽ, Bộ lọc Trò chơi của tôi & Di chuyển DepotCache",
                    Body = "• Hỗ trợ gỡ Fix sạch sẽ (Revert fixes cleanly): tính hash SHA-256 đối chiếu an toàn, tự động khôi phục file gốc khi gỡ fix.\n• Bộ lọc 'My games' (Trò chơi của tôi) trong trang Fixes.\n• Tự động di chuyển thư mục depotcache chuẩn theo Steam.\n• Cập nhật thông báo và lưu trữ lịch sử phiên bản ngay trong chuông thông báo.",
                    PublishedAt = "10/09/2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest"
                },
                new()
                {
                    TagName = "v105.2",
                    Title = "BaoTools v105.2 - Trích xuất Ticket Steam & Denuvo, Thiết kế lại Online Fixes",
                    Body = "• Trích xuất Steam & Denuvo Tickets trực tiếp từ Steam Client Interface.\n• Thiết kế lại trang Online Fixes với giao diện hiện đại và trực quan hơn.",
                    PublishedAt = "07/09/2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2"
                },
                new()
                {
                    TagName = "v105.1",
                    Title = "BaoTools v105.1 - Chuông thông báo cập nhật & Tối ưu tải về",
                    Body = "• Bổ sung chuông thông báo và kiểm tra cập nhật tự động.\n• Tối ưu hóa tải manifest và cơ chế dự phòng download.",
                    PublishedAt = "05/09/2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.1"
                },
                new()
                {
                    TagName = "v1005",
                    Title = "BaoTools v1005 - Bản cập nhật Trình tải xuống (DOWNLOADER)",
                    Body = "• Ra mắt tính năng tải trực tiếp và quản lý game Steam.\n• Tích hợp hệ thống đa ngôn ngữ 30 quốc gia.",
                    PublishedAt = "01/09/2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1005"
                }
            };
        }

        if (lang.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return new List<UpdateHistoryItem>
            {
                new()
                {
                    TagName = "v105.3",
                    Title = "BaoTools v105.3 - 纯净还原 Fix、我的游戏筛选与 DepotCache 迁移",
                    Body = "• 支持纯净还原 Fix (Revert fixes cleanly)：基于 SHA-256 哈希比对确保安全，还原时自动恢复原始文件。\n• Fixes 页面新增“我的游戏”筛选。\n• 自动将 depotcache 文件夹迁移至 Steam 标准路径。\n• 通知铃铛内置更新通知与版本历史记录。",
                    PublishedAt = "2026-09-10",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest"
                },
                new()
                {
                    TagName = "v105.2",
                    Title = "BaoTools v105.2 - Steam & Denuvo Ticket 提取器、Online Fixes 全新设计",
                    Body = "• 直接从 Steam 客户端接口提取 Steam 与 Denuvo Ticket。\n• 全新现代化设计 Online Fixes 页面，更加直观易用。",
                    PublishedAt = "2026-09-07",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2"
                },
                new()
                {
                    TagName = "v105.1",
                    Title = "BaoTools v105.1 - 更新通知铃铛与下载优化",
                    Body = "• 新增更新通知铃铛与自动检查更新机制。\n• 优化 manifest 下载与备用下载策略。",
                    PublishedAt = "2026-09-05",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.1"
                },
                new()
                {
                    TagName = "v1005",
                    Title = "BaoTools v1005 - 下载器全新发布 (DOWNLOADER)",
                    Body = "• 推出直接下载与 Steam 游戏管理功能。\n• 全面集成 30 种语言的多语言系统。",
                    PublishedAt = "2026-09-01",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1005"
                }
            };
        }

        if (lang.Equals("ru", StringComparison.OrdinalIgnoreCase))
        {
            return new List<UpdateHistoryItem>
            {
                new()
                {
                    TagName = "v105.3",
                    Title = "BaoTools v105.3 - Чистый откат фиксов, фильтр Мои игры и миграция DepotCache",
                    Body = "• Чистый откат фиксов (Revert fixes cleanly): безопасное сравнение SHA-256 хэшей, автоматическое восстановление оригинальных файлов.\n• Фильтр «Мои игры» на странице фиксов.\n• Автоматическая миграция папки depotcache в стандартную папку Steam.\n• Уведомления об обновлениях и история версий в колокольчике.",
                    PublishedAt = "10.09.2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest"
                },
                new()
                {
                    TagName = "v105.2",
                    Title = "BaoTools v105.2 - Извлечение тикетов Steam и Denuvo, редизайн Online Fixes",
                    Body = "• Извлечение тикетов Steam и Denuvo напрямую из интерфейса клиента Steam.\n• Обновлённый современный интерфейс страницы Online Fixes.",
                    PublishedAt = "07.09.2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2"
                },
                new()
                {
                    TagName = "v105.1",
                    Title = "BaoTools v105.1 - Колокольчик уведомлений и оптимизация загрузки",
                    Body = "• Добавлен колокольчик уведомлений и автоматическая проверка обновлений.\n• Оптимизация загрузки манифестов и резервные каналы скачивания.",
                    PublishedAt = "05.09.2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.1"
                },
                new()
                {
                    TagName = "v1005",
                    Title = "BaoTools v1005 - Обновление загрузчика (DOWNLOADER)",
                    Body = "• Запуск прямой загрузки и управления играми Steam.\n• Интеграция мультиязычной системы на 30 языков.",
                    PublishedAt = "01.09.2026",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1005"
                }
            };
        }

        return new List<UpdateHistoryItem>
        {
            new()
            {
                TagName = "v105.3",
                Title = "BaoTools v105.3 - Reverting Fixes Cleanly, My Games Filter & DepotCache Migration",
                Body = "• Clean fix reversion: safe SHA-256 hash comparison, automatically restores original files when reverting fixes.\n• 'My games' filter in Fixes page.\n• Automatic depotcache folder migration to standard Steam location.\n• Persistent update notifications and update history in notification bell.",
                PublishedAt = "Sep 10, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest"
            },
            new()
            {
                TagName = "v105.2",
                Title = "BaoTools v105.2 - Steam & Denuvo Tickets Extractor, Online Fixes Redesign",
                Body = "• Extract Steam & Denuvo Tickets directly from Steam Client Interface.\n• Redesigned Online Fixes page with a modern and intuitive interface.",
                PublishedAt = "Sep 07, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2"
            },
            new()
            {
                TagName = "v105.1",
                Title = "BaoTools v105.1 - Update Notification & Download Optimizations",
                Body = "• Added notification bell and automatic update checks.\n• Optimized manifest downloads and download fallback mechanism.",
                PublishedAt = "Sep 05, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.1"
            },
            new()
            {
                TagName = "v1005",
                Title = "BaoTools v1005 - The DOWNLOADER Update",
                Body = "• Direct downloads and Steam game management launched.\n• Integrated multi-language system supporting 30 languages.",
                PublishedAt = "Sep 01, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1005"
            }
        };
    }

    /// <summary>
    /// Loads update history from local disk or falls back to built-in defaults.
    /// Updates IsNew and IsCurrent based on currentVersion.
    /// </summary>
    public List<UpdateHistoryItem> LoadHistory(string currentVersion)
    {
        List<UpdateHistoryItem>? items = null;

        if (File.Exists(_historyPath))
        {
            try
            {
                string json = File.ReadAllText(_historyPath);
                items = JsonSerializer.Deserialize<List<UpdateHistoryItem>>(json, JsonOpts);
            }
            catch { /* corrupted or unreadable: fallback */ }
        }

        if (items is null || items.Count == 0)
        {
            items = GetDefaultHistory();
        }

        // Ensure defaults are merged and synchronized with current language
        var defaults = GetDefaultHistory();
        foreach (var def in defaults)
        {
            string defNorm = def.TagName.Trim().TrimStart('v', 'V');
            var existingItem = items.FirstOrDefault(x => string.Equals(x.TagName.Trim().TrimStart('v', 'V'), defNorm, StringComparison.OrdinalIgnoreCase));
            if (existingItem is null)
            {
                items.Add(def);
            }
            else
            {
                // Synchronize language for default items
                existingItem.TagName = def.TagName;
                existingItem.Title = def.Title;
                existingItem.Body = def.Body;
                existingItem.PublishedAt = def.PublishedAt;
                existingItem.HtmlUrl = def.HtmlUrl;
            }
        }

        // Deduplicate items with the same normalized tag (e.g. "1005" and "v1005")
        var deduped = new List<UpdateHistoryItem>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
        {
            string norm = it.TagName.Trim().TrimStart('v', 'V');
            if (seenTags.Add(norm))
            {
                deduped.Add(it);
            }
        }
        items = deduped;

        UpdateItemFlags(items, currentVersion);
        var sorted = SortItems(items);
        SaveHistory(sorted);
        return sorted;
    }

    /// <summary>
    /// Saves items to disk.
    /// </summary>
    public void SaveHistory(IEnumerable<UpdateHistoryItem> items)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(items, JsonOpts);
            File.WriteAllText(_historyPath, json);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Merges remote GitHub releases into local history, updates flags, and saves to disk.
    /// </summary>
    public List<UpdateHistoryItem> MergeWithRemoteReleases(IEnumerable<GitHubReleaseInfo> remoteReleases, string currentVersion)
    {
        var existing = LoadHistory(currentVersion);
        var map = new Dictionary<string, UpdateHistoryItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in existing)
        {
            string k = it.TagName.Trim().TrimStart('v', 'V').ToLowerInvariant();
            map[k] = it;
        }

        foreach (var remote in remoteReleases)
        {
            if (string.IsNullOrWhiteSpace(remote.TagName)) continue;
            string key = remote.TagName.Trim().TrimStart('v', 'V').ToLowerInvariant();

            string dateStr = remote.PublishedAt?.ToString("MMM dd, yyyy") ?? "";
            if (map.TryGetValue(key, out var item))
            {
                // Never overwrite curated localized content for default built-in items!
                if (!IsDefaultTag(item.TagName))
                {
                    item.Title = !string.IsNullOrWhiteSpace(remote.Title) ? remote.Title : item.Title;
                    item.Body = !string.IsNullOrWhiteSpace(remote.Body) ? remote.Body : item.Body;
                }
                item.HtmlUrl = !string.IsNullOrWhiteSpace(remote.HtmlUrl) ? remote.HtmlUrl : item.HtmlUrl;
                if (!string.IsNullOrWhiteSpace(dateStr)) item.PublishedAt = dateStr;
            }
            else
            {
                var newItem = new UpdateHistoryItem
                {
                    TagName = remote.TagName,
                    Title = string.IsNullOrWhiteSpace(remote.Title) ? remote.TagName : remote.Title,
                    Body = remote.Body,
                    HtmlUrl = remote.HtmlUrl,
                    PublishedAt = dateStr
                };
                map[key] = newItem;
                existing.Add(newItem);
            }
        }

        UpdateItemFlags(existing, currentVersion);
        var sorted = SortItems(existing);
        SaveHistory(sorted);
        return sorted;
    }

    private static void UpdateItemFlags(List<UpdateHistoryItem> items, string currentVersion)
    {
        string curNorm = currentVersion.Trim().TrimStart('v', 'V');
        foreach (var item in items)
        {
            string itemNorm = item.TagName.Trim().TrimStart('v', 'V');
            item.IsCurrent = string.Equals(itemNorm, curNorm, StringComparison.OrdinalIgnoreCase);
            item.IsNew = UpdateService.IsVersionNewer(item.TagName, currentVersion);
        }
    }

    private static List<UpdateHistoryItem> SortItems(List<UpdateHistoryItem> items)
    {
        return items.OrderByDescending(x =>
        {
            // Try sorting by version components
            string v = x.TagName.Trim().TrimStart('v', 'V');
            if (v.Contains('.'))
            {
                var parts = v.Split('.');
                long score = 0;
                foreach (var p in parts)
                {
                    if (long.TryParse(new string(p.Where(char.IsDigit).ToArray()), out var n))
                    {
                        score = score * 1000 + n;
                    }
                }
                return score;
            }
            if (long.TryParse(new string(v.Where(char.IsDigit).ToArray()), out var legacyScore))
            {
                return legacyScore;
            }
            return 0L;
        }).ToList();
    }
}
