using System.IO;
using System.Text.Json;

namespace BaoToolsGui.Services;

public class AppSettings
{
    public string? SteamPathOverride { get; set; }
    public int DailyAddCount { get; set; }
    public DateTime? LastAddDate { get; set; }

    // ── Unlocker mode (Mode page). User's chosen backend ────────────
    // "Ost" | "Bst" | "Custom". Older builds wrote "SteamTools" | "OpenSteamTools" |
    // "OpenSteamToolsNightly" | "CloudRedirect"; see ModeMigration, which rewrites those on startup.
    public string? SelectedMode { get; set; }

    // ── Install behavior ─────────────────────────────────────────────
    // When true (default), installs comment out setManifestid() lines and skip copying .manifest
    // files, so games aren't pinned to a version and Steam keeps them updated. Nullable so we can
    // tell "never set" (→ default ON) from an explicit user choice.
    public bool? AutoUpdateApps { get; set; }

    // When true (default), donate spare Steam decryption keys to the community pool. Nullable so
    // "never set" (→ default ON) is distinguishable from an explicit user choice.
    public bool? DonateKeys { get; set; }

    // Manage-page results-per-page. 0 = "All" (single infinite scroll). Nullable so "never set"
    // (→ default 24) is distinguishable from an explicit choice.
    public int? ManagePageSize { get; set; }

    // Fixes-page results-per-page. 0 = "All" (single infinite scroll). Nullable so "never set"
    // (→ default 24) is distinguishable from an explicit choice.
    public int? FixesPageSize { get; set; }

    // Builds-page game-list results-per-page. 0 = "All". Kept separate from ManagePageSize. The Builds
    // list is a narrow sidebar, so a size that suits the Manage grid rarely suits both.
    public int? BuildsPageSize { get; set; }

    // UI language as a BCP-47 tag ("en", "zh-Hans"). Null = follow the Windows display language.
    public string? Language { get; set; }

    // The user's own Hubcap (hubcapmanifest.com) API key ("smm_…"). Null = not configured; key-gated
    // sources stay locked until set. Stored locally. The app calls Hubcap directly with it.
    public string? HubcapApiKey { get; set; }

    // When true, registering the app to launch on Windows sign-in (HKCU …\Run). Nullable so "never set"
    // (→ default OFF) is distinguishable from an explicit choice.
    public bool? StartWithWindows { get; set; }

    // When true, launch the app silently into the system tray when Steam starts (via winmm.dll loader).
    public bool? StartWithSteam { get; set; }

    // When true, minimizing hides the window to the system tray instead of the taskbar. Nullable so
    // "never set" (→ default OFF) is distinguishable from an explicit choice.
    public bool? MinimizeToTray { get; set; }

    // When true, FastFetch auto-picks the first available source and downloads immediately.
    // Nullable so "never set" (→ default OFF) is distinguishable from an explicit choice.
    public bool? FastFetch { get; set; }

    // UI Theme ("Default" | "Dracula" | "Nord" | "Catppuccin" | "Cyberpunk" | "Midnight")
    public string? Theme { get; set; }

    // Floating AI Assistant
    public bool? EnableAiAssistant { get; set; }

    // How host names are resolved: "Auto" (default), "Always" or "Never". Stored in English because
    // it is matched in code; the Settings page localizes the display only. Null = never set → "Auto".
    // See AppHttp for what each mode does.
    public string? DnsMode { get; set; }
}

public class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BaoToolsGui");
    private static readonly string DefaultFilePath = Path.Combine(Dir, "settings.json");

    private readonly string _filePath;
    private readonly string _dir;
    private readonly string _bakPath;
    private readonly string _tmpPath;

    private AppSettings _settings = new();

    public SettingsService(string? customFilePath = null)
    {
        _filePath = customFilePath ?? DefaultFilePath;
        _dir = Path.GetDirectoryName(_filePath) ?? Dir;
        _bakPath = _filePath + ".bak";
        _tmpPath = _filePath + ".tmp";
        Load();
    }

    public bool CanAddGame()
    {
        if (_settings.LastAddDate?.Date != DateTime.Today)
        {
            _settings.DailyAddCount = 0;
            _settings.LastAddDate = DateTime.Today;
            Save();
        }
        return _settings.DailyAddCount < AppConfig.AppDailyDownloadLimit;
    }

    public int DailyAddCount => _settings.DailyAddCount;

    public void IncrementAddCount()
    {
        _settings.DailyAddCount++;
        Save();
    }

    /// <summary>User-chosen Steam folder. Null = auto-detect from registry. Persisted only when set.</summary>
    public string? SteamPathOverride
    {
        get => _settings.SteamPathOverride;
        set
        {
            _settings.SteamPathOverride = string.IsNullOrWhiteSpace(value) ? null : value;
            Save();
        }
    }

    /// <summary>Selected unlocker backend ("SteamTools" | "OpenSteamTools"), or null if never chosen.</summary>
    public string? SelectedMode
    {
        get => _settings.SelectedMode;
        set { _settings.SelectedMode = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>When true (default), installs don't lock manifests so apps keep auto-updating.</summary>
    public bool AutoUpdateApps
    {
        get => _settings.AutoUpdateApps ?? true; // default ON
        set { _settings.AutoUpdateApps = value; Save(); }
    }

    /// <summary>When true (default), donate spare Steam decryption keys to the community pool.</summary>
    public bool DonateKeys
    {
        get => _settings.DonateKeys ?? true; // default ON
        set { _settings.DonateKeys = value; Save(); }
    }

    /// <summary>Manage-page results-per-page (default 24). 0 = "All" (single infinite scroll).</summary>
    public int ManagePageSize
    {
        get => _settings.ManagePageSize ?? 24; // default 24
        set { _settings.ManagePageSize = value; Save(); }
    }

    /// <summary>Fixes-page results-per-page (default 24). 0 = "All" (single infinite scroll).</summary>
    public int FixesPageSize
    {
        get => _settings.FixesPageSize ?? 24; // default 24
        set { _settings.FixesPageSize = value; Save(); }
    }

    /// <summary>Builds-page game-list results-per-page (default 10). 0 = "All". Smaller than the other
    /// pages' 24. This list is a narrow sidebar next to the build detail, not a full-width grid.</summary>
    public int BuildsPageSize
    {
        get => _settings.BuildsPageSize ?? 10; // default 10
        set { _settings.BuildsPageSize = value; Save(); }
    }

    /// <summary>UI language tag ("en" | "zh-Hans"), or null to follow the Windows display language.</summary>
    public string? Language
    {
        get => _settings.Language;
        set { _settings.Language = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>The user's Hubcap API key ("smm_…"), or null if not configured.</summary>
    public string? HubcapApiKey
    {
        get => _settings.HubcapApiKey;
        set { _settings.HubcapApiKey = string.IsNullOrWhiteSpace(value) ? null : value; Save(); }
    }

    /// <summary>When true, the app is registered to launch on Windows sign-in (default OFF).</summary>
    public bool StartWithWindows
    {
        get => _settings.StartWithWindows ?? false; // default OFF
        set { _settings.StartWithWindows = value; Save(); }
    }

    /// <summary>When true, launch the app silently in tray when Steam starts.</summary>
    public bool? StartWithSteam
    {
        get => _settings.StartWithSteam;
        set { _settings.StartWithSteam = value; Save(); }
    }

    /// <summary>When true, minimizing hides the window to the system tray (default OFF).</summary>
    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray ?? false; // default OFF
        set { _settings.MinimizeToTray = value; Save(); }
    }

    /// <summary>When true, FastFetch auto-picks the first available source and downloads immediately (default OFF).</summary>
    public bool FastFetch
    {
        get => _settings.FastFetch ?? false; // default OFF
        set { _settings.FastFetch = value; Save(); }
    }

    public string Theme
    {
        get => _settings.Theme ?? "Default";
        set { _settings.Theme = value; Save(); }
    }

    public bool EnableAiAssistant
    {
        get => _settings.EnableAiAssistant ?? true; // default ON
        set { _settings.EnableAiAssistant = value; Save(); }
    }

    /// <summary>
    /// How host names are resolved: "Auto" (default), "Always" or "Never". See <see cref="AppHttp"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to Auto rather than off on purpose. A user whose ISP DNS-blocks lua.tools cannot reach
    /// anything in the app to discover that a setting would fix it, so an opt-in toggle would be found
    /// by everyone except the people who need it. Auto costs unaffected users nothing: the system
    /// resolver is still tried first and DoH only engages once it has actually failed.
    /// </remarks>
    public string DnsMode
    {
        get => _settings.DnsMode is "Always" or "Never" ? _settings.DnsMode : "Auto";
        set { _settings.DnsMode = value; Save(); }
    }

    private void Load()
    {
        // Prefer the primary file; fall back to the last-good .bak. Crucially, NEVER silently reset a
        // corrupt-but-present file to defaults (a later Save would then overwrite it and lose real data).
        // Move it aside to .corrupt so it's preserved and can't be clobbered.
        if (TryLoad(_filePath)) return;
        PreserveCorrupt(_filePath);
        if (TryLoad(_bakPath)) return;
        _settings = new AppSettings();
    }

    private bool TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            if (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) is { } loaded)
            {
                _settings = loaded;
                return true;
            }
        }
        catch { /* missing / truncated / invalid JSON → caller falls through */ }
        return false;
    }

    /// <summary>A present-but-unparseable settings file is moved aside (not deleted) so its contents survive
    /// for manual recovery and a subsequent Save can't overwrite it.</summary>
    private static void PreserveCorrupt(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch { /* best effort */ }
    }

    private void Save()
    {
        // Nothing worth persisting → don't leave a settings file behind.
        // Every persisted field must be listed here. A field left out is treated as "nothing worth
        // keeping", so a user whose ONLY change was that setting gets the file deleted and the setting
        // silently lost on the next save. (FixesPageSize was missing.)
        bool empty = _settings.SteamPathOverride is null
            && _settings.SelectedMode is null
            && _settings.AutoUpdateApps is null
            && _settings.DonateKeys is null
            && _settings.ManagePageSize is null
            && _settings.FixesPageSize is null
            && _settings.BuildsPageSize is null
            && _settings.Language is null
            && _settings.HubcapApiKey is null
            && _settings.StartWithWindows is null
            && _settings.MinimizeToTray is null
            && _settings.FastFetch is null
            && _settings.Theme is null
            && _settings.EnableAiAssistant is null;
        if (empty)
        {
            foreach (var p in new[] { _filePath, _bakPath, _tmpPath })
                try { if (File.Exists(p)) File.Delete(p); } catch { /* best effort */ }
            return;
        }

        Directory.CreateDirectory(_dir);
        string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });

        // Atomic write: fill a temp file, then rename it over the target. A crash/kill mid-write can only
        // ever truncate the .tmp. The live settings.json is replaced by an atomic move (same-volume rename)
        // and is therefore never left half-written. (This class of loss is exactly what a forced kill during
        // a plain WriteAllText caused.) A .bak of the last good file is kept as a second recovery source.
        File.WriteAllText(_tmpPath, json);
        try { if (File.Exists(_filePath)) File.Copy(_filePath, _bakPath, overwrite: true); } catch { /* best effort */ }
        File.Move(_tmpPath, _filePath, overwrite: true);
    }
}
