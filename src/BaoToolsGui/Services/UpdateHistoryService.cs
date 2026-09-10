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

    /// <summary>
    /// Default built-in releases so the update history in the bell is never blank even offline.
    /// </summary>
    public static List<UpdateHistoryItem> GetDefaultHistory()
    {
        return new List<UpdateHistoryItem>
        {
            new()
            {
                TagName = "v105.3",
                Title = "BaoTools v105.3 - Reverting Fixes Cleanly, My Games Filter & DepotCache Migration",
                Body = "• Hỗ trợ gỡ Fix sạch sẽ (Revert fixes cleanly): tính hash SHA-256 đối chiếu an toàn, tự động khôi phục file gốc khi gỡ fix.\n• Bộ lọc 'My games' (Trò chơi của tôi) trong trang Fixes.\n• Tự động di chuyển thư mục depotcache chuẩn theo Steam.\n• Cập nhật thông báo và lưu trữ lịch sử phiên bản ngay trong chuông thông báo.",
                PublishedAt = "Sep 10, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest"
            },
            new()
            {
                TagName = "v105.2",
                Title = "BaoTools v105.2 - Steam & Denuvo Tickets Extractor, Online Fixes Redesign",
                Body = "• Trích xuất Steam & Denuvo Tickets trực tiếp từ Steam Client Interface.\n• Thiết kế lại trang Online Fixes với giao diện hiện đại và trực quan hơn.",
                PublishedAt = "Sep 07, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2"
            },
            new()
            {
                TagName = "v105.1",
                Title = "BaoTools v105.1 - Update Notification & Download Optimizations",
                Body = "• Bổ sung chuông thông báo và kiểm tra cập nhật tự động.\n• Tối ưu hóa tải manifest và cơ chế dự phòng download.",
                PublishedAt = "Sep 05, 2026",
                HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.1"
            },
            new()
            {
                TagName = "v1005",
                Title = "BaoTools v1005 - The DOWNLOADER Update",
                Body = "• Ra mắt tính năng tải trực tiếp và quản lý game Steam.\n• Tích hợp hệ thống đa ngôn ngữ 30 quốc gia.",
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

        // Ensure defaults are merged if missing
        foreach (var def in GetDefaultHistory())
        {
            if (!items.Any(x => string.Equals(x.TagName, def.TagName, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(def);
            }
        }

        UpdateItemFlags(items, currentVersion);
        return SortItems(items);
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
        var map = existing.ToDictionary(x => x.TagName.Trim().ToLowerInvariant(), x => x);

        foreach (var remote in remoteReleases)
        {
            if (string.IsNullOrWhiteSpace(remote.TagName)) continue;
            string key = remote.TagName.Trim().ToLowerInvariant();

            string dateStr = remote.PublishedAt?.ToString("MMM dd, yyyy") ?? "";
            if (map.TryGetValue(key, out var item))
            {
                item.Title = !string.IsNullOrWhiteSpace(remote.Title) ? remote.Title : item.Title;
                item.Body = !string.IsNullOrWhiteSpace(remote.Body) ? remote.Body : item.Body;
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
