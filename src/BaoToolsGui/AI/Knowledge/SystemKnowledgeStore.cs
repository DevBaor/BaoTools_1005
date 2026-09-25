using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BaoToolsGui.AI.Discovery;
using BaoToolsGui.AI.Knowledge.Models;

namespace BaoToolsGui.AI.Knowledge;

/// <summary>
/// Thread-safe in-memory store for verified BaoTools system knowledge.
/// Grounded strictly in real codebase metadata.
/// </summary>
public class SystemKnowledgeStore
{
    private static readonly Lazy<SystemKnowledgeStore> _instance = new(() => new SystemKnowledgeStore());
    public static SystemKnowledgeStore Instance => _instance.Value;

    public SystemManifest Manifest { get; private set; } = new();
    public List<FeatureItem> Features => Manifest.Features;
    public List<ScreenItem> Screens => Manifest.Screens;
    public List<SettingItem> Settings => Manifest.Settings;
    public List<ToolItem> Tools => Manifest.Tools;
    public List<CommandItem> Commands => Manifest.Commands;
    public List<ServiceItem> Services => Manifest.Services;
    public List<WorkflowItem> Workflows => Manifest.Workflows;
    public List<LimitationItem> Limitations => Manifest.Limitations;

    public bool IsLoaded { get; private set; }

    public SystemKnowledgeStore()
    {
        LoadKnowledge();
    }

    private void LoadKnowledge()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("SystemManifest.json", StringComparison.OrdinalIgnoreCase)) ?? "";

            if (!string.IsNullOrEmpty(resourceName))
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    var loaded = JsonSerializer.Deserialize<SystemManifest>(json);
                    if (loaded != null)
                    {
                        Manifest = loaded;
                        IsLoaded = true;
                        return;
                    }
                }
            }

            // Fallback: Scan codebase directly if running in development mode
            Manifest = CodebaseScanner.ScanSystem();
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SystemKnowledgeStore] Error loading embedded knowledge: {ex.Message}");
            Manifest = CodebaseScanner.ScanSystem();
            IsLoaded = true;
        }
    }

    /// <summary>
    /// Finds a screen/view by name or query.
    /// </summary>
    public ScreenItem? FindScreen(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string q = query.Trim().ToLowerInvariant();

        return Screens.FirstOrDefault(s =>
            q.Contains(s.Id.Replace("screen_", "").ToLowerInvariant()) ||
            s.Id.ToLowerInvariant().Contains(q) ||
            s.NameVi.ToLowerInvariant().Contains(q) ||
            q.Contains(s.NameVi.ToLowerInvariant()) ||
            s.NameEn.ToLowerInvariant().Contains(q) ||
            q.Contains(s.NameEn.ToLowerInvariant()) ||
            s.Sections.Any(sec => q.Contains(sec.ToLowerInvariant())));
    }

    /// <summary>
    /// Finds a setting by property name, label or topic.
    /// </summary>
    public SettingItem? FindSetting(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string q = query.Trim().ToLowerInvariant();

        return Settings.FirstOrDefault(s =>
            s.Id.ToLowerInvariant().Contains(q) ||
            s.Property.ToLowerInvariant().Contains(q) ||
            s.NameVi.ToLowerInvariant().Contains(q) ||
            s.NameEn.ToLowerInvariant().Contains(q) ||
            s.LocationVi.ToLowerInvariant().Contains(q) ||
            s.LocationEn.ToLowerInvariant().Contains(q));
    }

    /// <summary>
    /// Finds a feature by keyword or name.
    /// </summary>
    public FeatureItem? FindFeature(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string q = query.Trim().ToLowerInvariant();

        return Features.FirstOrDefault(f =>
            f.Id.ToLowerInvariant().Contains(q) ||
            f.NameVi.ToLowerInvariant().Contains(q) ||
            f.NameEn.ToLowerInvariant().Contains(q) ||
            f.Keywords.Any(k => q.Contains(k.ToLowerInvariant())));
    }

    /// <summary>
    /// Finds a workflow matching user intention.
    /// </summary>
    public WorkflowItem? FindWorkflow(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        string q = query.Trim().ToLowerInvariant();

        return Workflows.FirstOrDefault(w =>
            w.Id.ToLowerInvariant().Contains(q) ||
            w.NameVi.ToLowerInvariant().Contains(q) ||
            w.NameEn.ToLowerInvariant().Contains(q) ||
            q.Contains(w.Id.Replace("workflow_", "")));
    }

    /// <summary>
    /// Finds a registered AI tool by name.
    /// </summary>
    public ToolItem? FindTool(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return null;
        return Tools.FirstOrDefault(t => t.Name.Equals(toolName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a capability is explicitly unsupported by BaoTools.
    /// Returns a message explaining BaoTools's real scope.
    /// </summary>
    public (bool isUnsupported, string? explanation) CheckUnsupportedCapability(string query, bool isVi)
    {
        if (string.IsNullOrWhiteSpace(query)) return (false, null);
        string q = query.Trim().ToLowerInvariant();

        string[] unsupportedTokens = new[]
        {
            "livestream", "live stream", "quay màn hình", "screen record",
            "hack game", "cheat engine", "aimbot", "mod menu",
            "torrent", "peer to peer", "p2p",
            "bán game", "mua game bằng thẻ", "nạp tiền vào baotools",
            "phát nhạc", "nghe nhạc", "play music", "voice chat",
            "chỉnh sửa video", "render video"
        };

        foreach (var token in unsupportedTokens)
        {
            if (q.Contains(token))
            {
                string expl = isVi
                    ? $"BaoTools hiện **chưa có chức năng '{token}'**. BaoTools tập trung chính vào việc nạp game, quản lý thư viện Steam, tải game tốc độ cao, gỡ Steam DRM (Steamless) và sửa lỗi crash game."
                    : $"BaoTools currently **does not support '{token}'**. BaoTools focuses strictly on Steam game management, fast downloads, Steamless DRM removal, and crash diagnosis.";
                return (true, expl);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Formats a concise grounded snippet from verified knowledge.
    /// </summary>
    public string BuildGroundedContext(string query, bool isVi)
    {
        var sb = new StringBuilder();

        // 1. Check setting
        var setting = FindSetting(query);
        if (setting != null)
        {
            sb.AppendLine(isVi
                ? $"[Cài đặt thực tế] Tên: {setting.NameVi} | Vị trí UI: {setting.LocationVi} | Thuộc tính code: {setting.Property} | Mặc định: {setting.DefaultValue}"
                : $"[Verified Setting] Name: {setting.NameEn} | UI Location: {setting.LocationEn} | Code Property: {setting.Property} | Default: {setting.DefaultValue}");
        }

        // 2. Check screen
        var screen = FindScreen(query);
        if (screen != null)
        {
            sb.AppendLine(isVi
                ? $"[Màn hình thực tế] {screen.NameVi} ({screen.SourceFile}) - Điều hướng: {screen.NavigationRoute}"
                : $"[Verified Screen] {screen.NameEn} ({screen.SourceFile}) - Navigation: {screen.NavigationRoute}");
        }

        // 3. Check workflow
        var workflow = FindWorkflow(query);
        if (workflow != null)
        {
            sb.AppendLine(isVi
                ? $"[Quy trình thực tế] {workflow.NameVi}: {workflow.SummaryVi}"
                : $"[Verified Workflow] {workflow.NameEn}: {workflow.SummaryEn}");
        }

        return sb.ToString().Trim();
    }
}
