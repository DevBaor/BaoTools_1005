using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BaoToolsGui.Services.Ai;

/// <summary>
/// Snapshot of the current BaoTools application and host system state.
/// Provides contextual awareness for technical diagnosis and assistance.
/// </summary>
public class BaoToolsContext
{
    public string CurrentPage { get; set; } = "Home";
    public GameContextInfo? SelectedGame { get; set; }
    public int InstalledGamesCount { get; set; }
    public PluginContextInfo PluginStatus { get; set; } = new();
    public DownloadContextInfo DownloadStatus { get; set; } = new();
    public SystemHardwareInfo SystemInfo { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
    public DateTime CapturedAt { get; set; } = DateTime.Now;

    public class GameContextInfo
    {
        public uint AppId { get; set; }
        public string Name { get; set; } = "";
        public string? InstallPath { get; set; }
        public string? Version { get; set; }
        public bool HasSteamlessBackup { get; set; }
        public int DlcCount { get; set; }
    }

    public class PluginContextInfo
    {
        public bool IsInstalled { get; set; }
        public string Version { get; set; } = "Not installed";
        public string ActiveMode { get; set; } = "BetterSteamTools";
        public bool SteamRunning { get; set; }
    }

    public class DownloadContextInfo
    {
        public int ActiveDownloads { get; set; }
        public int QueueCount { get; set; }
        public string TotalDownloadedToday { get; set; } = "0/10";
    }

    public class SystemHardwareInfo
    {
        public string GpuName { get; set; } = "Unknown GPU";
        public int VramMb { get; set; }
        public int RamGb { get; set; }
        public string OsVersion { get; set; } = "Windows";
        public string DirectXVersion { get; set; } = "DirectX 12";
        public bool HasVcRedist2015_2022_x64 { get; set; }
        public bool HasVcRedist2015_2022_x86 { get; set; }
    }

    /// <summary>
    /// Collects live application and system context.
    /// </summary>
    public static BaoToolsContext CollectLive(
        SteamService? steam,
        LuaVault? vault,
        SettingsService? settings,
        UnlockerService? unlocker = null,
        string currentPage = "Home",
        uint? selectedAppId = null)
    {
        var ctx = new BaoToolsContext
        {
            CurrentPage = currentPage,
            CapturedAt = DateTime.Now
        };

        // 1. Hardware & System specs
        ctx.SystemInfo = InspectSystemHardware();

        // 2. Steam & Plugin info
        if (steam is not null)
        {
            ctx.PluginStatus.SteamRunning = SteamService.IsSteamRunning();
            string? steamDir = steam.EffectivePath;
            bool hasLoader = !string.IsNullOrWhiteSpace(steamDir) && File.Exists(Path.Combine(steamDir, "winmm.dll"));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pluginDir = Path.Combine(appData, "BaoToolsGui", "plugin");
            bool hasFrontend = Directory.Exists(pluginDir) && Directory.EnumerateFileSystemEntries(pluginDir).Any();
            ctx.PluginStatus.IsInstalled = hasLoader && hasFrontend;
            ctx.PluginStatus.Version = ctx.PluginStatus.IsInstalled ? "Active" : "Not installed";
        }

        if (unlocker is not null)
        {
            ctx.PluginStatus.ActiveMode = unlocker.SelectedModeDisplayName ?? unlocker.SelectedMode?.ToString() ?? "Unknown";
        }

        // 3. Vault & Games
        if (vault is not null)
        {
            var apps = vault.AppsWithVariants();
            ctx.InstalledGamesCount = apps.Count;

            // If an AppId is selected, inspect that game
            if (selectedAppId.HasValue && selectedAppId.Value > 0)
            {
                ctx.SelectedGame = InspectGame(selectedAppId.Value, vault, steam);
            }
            else if (apps.Count > 0)
            {
                // Default to first app as sample context if available
                ctx.SelectedGame = InspectGame((uint)apps[0], vault, steam);
            }
        }

        // 4. Download stats
        if (settings is not null)
        {
            int used = settings.DailyAddCount;
            ctx.DownloadStatus.TotalDownloadedToday = $"{used} games added";
        }

        return ctx;
    }

    private static GameContextInfo InspectGame(uint appId, LuaVault vault, SteamService? steam)
    {
        var activeVariant = vault.GetActiveVariant(appId);
        string? label = activeVariant?.DisplayLabel;

        var info = new GameContextInfo
        {
            AppId = appId,
            Name = !string.IsNullOrWhiteSpace(label) ? label : $"AppID {appId}"
        };

        try
        {
            string? steamPath = steam?.EffectivePath;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                string manifestFile = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
                if (File.Exists(manifestFile))
                {
                    string content = File.ReadAllText(manifestFile);
                    var matchName = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"");
                    if (matchName.Success)
                    {
                        info.Name = matchName.Groups[1].Value;
                    }

                    var matchDir = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"");
                    if (matchDir.Success)
                    {
                        string installDirName = matchDir.Groups[1].Value;
                        string fullPath = Path.Combine(steamPath, "steamapps", "common", installDirName);
                        if (Directory.Exists(fullPath))
                        {
                            info.InstallPath = fullPath;
                            var backups = Directory.GetFiles(fullPath, "*.bak", SearchOption.AllDirectories);
                            info.HasSteamlessBackup = backups.Length > 0;
                        }
                    }
                }
            }
        }
        catch { }

        return info;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public static SystemHardwareInfo InspectSystemHardware()
    {
        var info = new SystemHardwareInfo
        {
            OsVersion = $"{Environment.OSVersion.VersionString} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})"
        };

        // 1. RAM via GlobalMemoryStatusEx
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(mem) && mem.ullTotalPhys > 0)
            {
                info.RamGb = (int)Math.Round((double)mem.ullTotalPhys / (1024 * 1024 * 1024));
            }
            else
            {
                info.RamGb = (int)Math.Round((double)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024 * 1024));
            }
        }
        catch
        {
            info.RamGb = 16;
        }

        // 2. GPU & VRAM via Registry (Windows Display Class)
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey != null)
            {
                foreach (string subKeyName in classKey.GetSubKeyNames())
                {
                    if (subKeyName.Length == 4 && int.TryParse(subKeyName, out _))
                    {
                        using var adapterKey = classKey.OpenSubKey(subKeyName);
                        if (adapterKey != null)
                        {
                            string? desc = adapterKey.GetValue("DriverDesc") as string;
                            if (!string.IsNullOrWhiteSpace(desc))
                            {
                                info.GpuName = desc;
                                object? memVal = adapterKey.GetValue("HardwareInformation.qwMemorySize");
                                if (memVal is long lBytes && lBytes > 0)
                                {
                                    info.VramMb = (int)(lBytes / (1024 * 1024));
                                }

                                // Prefer dedicated NVIDIA / AMD over integrated Intel / standard
                                if (desc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                    desc.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                                    desc.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                                    desc.Contains("RTX", StringComparison.OrdinalIgnoreCase))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            info.GpuName = "DirectX Graphics Device";
        }

        // 3. VC++ Redistributable 2015-2022 check via registry
        info.HasVcRedist2015_2022_x64 = CheckRegistryInstalled(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
        info.HasVcRedist2015_2022_x86 = CheckRegistryInstalled(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86");

        // 4. DirectX version
        if (Environment.OSVersion.Version.Major >= 10)
        {
            info.DirectXVersion = "DirectX 12 (D3D12)";
        }
        else
        {
            info.DirectXVersion = "DirectX 11 (D3D11)";
        }

        return info;
    }

    private static bool CheckRegistryInstalled(string subKey)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey);
            if (key != null)
            {
                var val = key.GetValue("Installed");
                return val != null && Convert.ToInt32(val) == 1;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Exports safe context summary for AI prompt without exposing credentials, keys, or private tokens.
    /// </summary>
    public string ToSanitizedSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Current Page]: {CurrentPage}");
        sb.AppendLine($"[OS]: {SystemInfo.OsVersion}");
        sb.AppendLine($"[GPU]: {SystemInfo.GpuName} (VRAM: {SystemInfo.VramMb} MB)");
        sb.AppendLine($"[RAM]: {SystemInfo.RamGb} GB");
        sb.AppendLine($"[DirectX]: {SystemInfo.DirectXVersion}");
        sb.AppendLine($"[VC++ 2015-2022 x64]: {(SystemInfo.HasVcRedist2015_2022_x64 ? "Installed" : "Missing")}");
        sb.AppendLine($"[VC++ 2015-2022 x86]: {(SystemInfo.HasVcRedist2015_2022_x86 ? "Installed" : "Missing")}");
        sb.AppendLine($"[Steam Plugin]: {(PluginStatus.IsInstalled ? $"Installed ({PluginStatus.Version})" : "Not installed")}");
        sb.AppendLine($"[Unlocker Mode]: {PluginStatus.ActiveMode}");
        sb.AppendLine($"[Installed Games In Library]: {InstalledGamesCount}");

        if (SelectedGame is not null)
        {
            sb.AppendLine($"[Selected Game]: {SelectedGame.Name} (AppID {SelectedGame.AppId})");
            if (!string.IsNullOrWhiteSpace(SelectedGame.InstallPath))
            {
                sb.AppendLine($"[Game Path]: {SelectedGame.InstallPath}");
            }
            sb.AppendLine($"[Steamless DRM Cleaned]: {(SelectedGame.HasSteamlessBackup ? "Yes (.bak present)" : "No backup detected")}");
        }

        return sb.ToString();
    }
}
