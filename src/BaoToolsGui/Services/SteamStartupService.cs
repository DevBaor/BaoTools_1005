using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace BaoToolsGui.Services;

/// <summary>
/// Manages the automatic startup of BaoTools when Steam launches.
/// Steam loads the proxy loader (winmm.dll) in its root folder upon launch, which in turn
/// executes `%LOCALAPPDATA%\BaoTools\current\BaoTools.exe --minimized --tray-locked`.
/// This service ensures winmm.dll is patched to BaoTools and maintains the forwarder bridge.
/// </summary>
public class SteamStartupService
{
    private readonly SteamService _steam;
    private readonly SettingsService _settings;
    private readonly PluginInstallerService _pluginInstaller;

    // Embedded 4.5KB C# forwarder executable (BaoToolsBridge) compiled to forward arguments to BaoTools.exe.
    private const string ForwarderExeBase64 =
        "TVqQAAMAAAAEAAAA//8AALgAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAA4fug4AtAnNIbgBTM0hVGhpcyBwcm9ncmFt" +
        "IGNhbm5vdCBiZSBydW4gaW4gRE9TIG1vZGUuDQ0KJAAAAAAAAABQRQAATAEDADZjp2oAAAAAAAAAAOAAAgELAQsAAAoAAAAIAAAAAAAA/icAAAAgAAAAQAAA" +
        "AABAAAAgAAAAAgAABAAAAAAAAAAEAAAAAAAAAACAAAAAAgAAAAAAAAIAQIUAABAAABAAAAAAEAAAEAAAAAAAABAAAAAAAAAAAAAAALAnAABLAAAAAEAAAOAE" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAGAAAAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAACAAAAAAAAAAAAAAA" +
        "CCAAAEgAAAAAAAAAAAAAAC50ZXh0AAAABAgAAAAgAAAACgAAAAIAAAAAAAAAAAAAAAAAACAAAGAucnNyYwAAAOAEAAAAQAAAAAYAAAAMAAAAAAAAAAAAAAAA" +
        "AABAAABALnJlbG9jAAAMAAAAAGAAAAACAAAAEgAAAAAAAAAAAAAAAAAAQAAAQgAAAAAAAAAAAAAAAAAAAADgJwAAAAAAAEgAAAACAAUAkCEAACAGAAABAAAA" +
        "AQAABgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABswBAAMAQAAAQAAERQKfgQAAApyAQAAcG8FAAAKCwcsVQdyWwAA" +
        "cG8GAAAKdQcAAAEMCCgHAAAKLTwIHyJvCAAACg0IHyIJF1hvCQAAChMECRYyIhEECTEdCAkXWBEECVkXWW8KAAAKEwURBSgLAAAKLAMRBQreCgcsBgdvDAAA" +
        "CtzeAybeAAYtJB8mKA0AAApyXQAAcHJvAABwKA4AAAoTBhEGKAsAAAosAxEGCgYsZigPAAAKbxAAAApvEQAAChMHBhEHGygSAAAKLAEqAiwGAo5pFjAHcokA" +
        "AHArC3K9AABwAigTAAAKEwgGEQhzFAAAChMJEQkXbxUAAAoRCQYoFgAACm8XAAAKEQkoGAAACibeAybeACoBKAAAAgASAFpsAAoAAAAAAAACAHZ4AAMBAAAB" +
        "AAD+AAoIAQMBAAABQlNKQgEAAQAAAAAADAAAAHY0LjAuMzAzMTkAAAAABQBsAAAAwAEAACN+AAAsAgAAYAIAACNTdHJpbmdzAAAAAIwEAADEAAAAI1VTAFAF" +
        "AAAQAAAAI0dVSUQAAABgBQAAwAAAACNCbG9iAAAAAAAAAAIAAAFHFQIACQAAAAD6JTMAFgAAAQAAABAAAAACAAAAAQAAAAEAAAAYAAAAAwAAAAEAAAABAAAA" +
        "AgAAAAAACgABAAAAAAAGAD4ANwAGAG8ATwAGAJUATwAGALwANwAGAN8AzwAGAOgAzwAGABQBNwAGAEUBOwEGAFEBNwAGAGUBNwArAHEBAAAGAI0BOwEKAK0B" +
        "mgEKAMcBmgEGAPEBNwAKAA4CmgEAAAAAAQAAAAAAAQABAIABEAAXAB8ABQABAAEAUCAAAAAAkQBFAAoAAQAAAAEASgARAI8AEAAZAI8AFQAhAI8AFQApAPQA" +
        "HgAxAAABIgAxAAsBKAA5ABsBLQA5ACkBMgA5ACkBNwA5ADEBPQBBAEoBLQBJAF0BFQBRAH8BQwBhAJIBSQBpALUBUABpANUBVQBxAOQBWgA5AAICXgA5AAkC" +
        "ZgCBAI8AbQCBAB8CcwBhADMCeACBAEQCfQBpAFkCggAgABsAGQAuAAsAmAAuABMAoQCJAASAAAAAAAAAAAAAAAAAAAAAALMAAAAEAAAAAAAAAAAAAAABAC4A" +
        "AAAAAAQAAAAAAAAAAAAAAAEANwAAAAAAAAAAAAA8TW9kdWxlPgBCYW9Ub29scy5leGUAUHJvZ3JhbQBCYW9Ub29sc0JyaWRnZQBtc2NvcmxpYgBTeXN0ZW0A" +
        "T2JqZWN0AE1haW4AYXJncwBTeXN0ZW0uUnVudGltZS5Db21waWxlclNlcnZpY2VzAENvbXBpbGF0aW9uUmVsYXhhdGlvbnNBdHRyaWJ1dGUALmN0b3IAUnVu" +
        "dGltZUNvbXBhdGliaWxpdHlBdHRyaWJ1dGUAQmFvVG9vbHMAU1RBVGhyZWFkQXR0cmlidXRlAE1pY3Jvc29mdC5XaW4zMgBSZWdpc3RyeQBSZWdpc3RyeUtl" +
        "eQBDdXJyZW50VXNlcgBPcGVuU3ViS2V5AEdldFZhbHVlAFN0cmluZwBJc051bGxPckVtcHR5AEluZGV4T2YAU3Vic3RyaW5nAFN5c3RlbS5JTwBGaWxlAEV4" +
        "aXN0cwBJRGlzcG9zYWJsZQBEaXNwb3NlAEVudmlyb25tZW50AFNwZWNpYWxGb2xkZXIAR2V0Rm9sZGVyUGF0aABQYXRoAENvbWJpbmUAU3lzdGVtLkRpYWdu" +
        "b3N0aWNzAFByb2Nlc3MAR2V0Q3VycmVudFByb2Nlc3MAUHJvY2Vzc01vZHVsZQBnZXRfTWFpbk1vZHVsZQBnZXRfRmlsZU5hbWUAU3RyaW5nQ29tcGFyaXNv" +
        "bgBFcXVhbHMASm9pbgBQcm9jZXNzU3RhcnRJbmZvAHNldF9Vc2VTaGVsbEV4ZWN1dGUAR2V0RGlyZWN0b3J5TmFtZQBzZXRfV29ya2luZ0RpcmVjdG9yeQBT" +
        "dGFydAAAAFlTAG8AZgB0AHcAYQByAGUAXABDAGwAYQBzAHMAZQBzAFwAYgBhAG8AdABvAG8AbABzAFwAcwBoAGUAbABsAFwAbwBwAGUAbgBcAGMAbwBtAG0A" +
        "YQBuAGQAAAEAEUIAYQBvAFQAbwBvAGwAcwAAGUIAYQBvAFQAbwBvAGwAcwAuAGUAeABlAAAzLQAtAG0AaQBuAGkAbQBpAHoAZQBkACAALQAtAHQAcgBhAHkA" +
        "LQBsAG8AYwBrAGUAZAABAyAAAAAAACeOxVfdH65Pvn3CBJP/anwACLd6XFYZNOCJBQABAR0OBCABAQgDIAABBAEAAAADBhIZBSABEhkOBCABHA4EAAECDgQg" +
        "AQgDBSACCAMIBSACDggIBQABDhEtBgADDg4ODgQAABI1BCAAEjkDIAAOBwADAg4OET0GAAIODh0OBSACAQ4OBCABAQIEAAEODgQgAQEOBgABEjUSQQ4HCg4S" +
        "GQ4ICA4ODg4SQQgBAAgAAAAAAB4BAAEAVAIWV3JhcE5vbkV4Y2VwdGlvblRocm93cwHYJwAAAAAAAAAAAADuJwAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "4CcAAAAAAAAAAF9Db3JFeGVNYWluAG1zY29yZWUuZGxsAAAAAAD/JQAgQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACABAAAAAgAACAGAAAADgAAIAAAAAAAAAAAAAAAAAAAAEAAQAAAFAAAIAAAAAAAAAAAAAAAAAAAAEAAQAAAGgA" +
        "AIAAAAAAAAAAAAAAAAAAAAEAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAJAAAACgQAAATAIAAAAAAAAAAAAA8EIAAOoBAAAAAAAAAAAAAEwCNAAAAFYA" +
        "UwBfAFYARQBSAFMASQBPAE4AXwBJAE4ARgBPAAAAAAC9BO/+AAABAAAAAAAAAAAAAAAAAAAAAAA/AAAAAAAAAAQAAAABAAAAAAAAAAAAAAAAAAAARAAAAAEA" +
        "VgBhAHIARgBpAGwAZQBJAG4AZgBvAAAAAAAkAAQAAABUAHIAYQBuAHMAbABhAHQAaQBvAG4AAAAAAAAAsASsAQAAAQBTAHQAcgBpAG4AZwBGAGkAbABlAEkA" +
        "bgBmAG8AAACIAQAAAQAwADAAMAAwADAANABiADAAAAAsAAIAAQBGAGkAbABlAEQAZQBzAGMAcgBpAHAAdABpAG8AbgAAAAAAIAAAADAACAABAEYAaQBsAGUA" +
        "VgBlAHIAcwBpAG8AbgAAAAAAMAAuADAALgAwAC4AMAAAADwADQABAEkAbgB0AGUAcgBuAGEAbABOAGEAbQBlAAAAQgBhAG8AVABvAG8AbABzAC4AZQB4AGUA" +
        "AAAAACgAAgABAEwAZQBnAGEAbABDAG8AcAB5AHIAaQBnAGgAdAAAACAAAABEAA0AAQBPAHIAaQBnAGkAbgBhAGwARgBpAGwAZQBuAGEAbQBlAAAAQgBhAG8A" +
        "VABvAG8AbABzAC4AZQB4AGUAAAAAADQACAABAFAAcgBvAGQAdQBjAHQAVgBlAHIAcwBpAG8AbgAAADAALgAwAC4AMAAuADAAAAA4AAgAAQBBAHMAcwBlAG0A" +
        "YgBsAHkAIABWAGUAcgBzAGkAbwBuAAAAMAAuADAALgAwAC4AMAAAAAAAAADvu788P3htbCB2ZXJzaW9uPSIxLjAiIGVuY29kaW5nPSJVVEYtOCIgc3RhbmRh" +
        "bG9uZT0ieWVzIj8+DQo8YXNzZW1ibHkgeG1sbnM9InVybjpzY2hlbWFzLW1pY3Jvc29mdC1jb206YXNtLnYxIiBtYW5pZmVzdFZlcnNpb249IjEuMCI+DQog" +
        "IDxhc3NlbWJseUlkZW50aXR5IHZlcnNpb249IjEuMC4wLjAiIG5hbWU9Ik15QXBwbGljYXRpb24uYXBwIi8+DQogIDx0cnVzdEluZm8geG1sbnM9InVybjpz" +
        "Y2hlbWFzLW1pY3Jvc29mdC1jb206YXNtLnYyIj4NCiAgICA8c2VjdXJpdHk+DQogICAgICA8cmVxdWVzdGVkUHJpdmlsZWdlcyB4bWxucz0idXJuOnNjaGVt" +
        "YXMtbWljcm9zb2Z0LWNvbTphc20udjMiPg0KICAgICAgICA8cmVxdWVzdGVkRXhlY3V0aW9uTGV2ZWwgbGV2ZWw9ImFzSW52b2tlciIgdWlBY2Nlc3M9ImZh" +
        "bHNlIi8+DQogICAgICA8L3JlcXVlc3RlZFByaXZpbGVnZXM+DQogICAgPC9zZWN1cml0eT4NCiAgPC90cnVzdEluZm8+DQo8L2Fzc2VtYmx5Pg0KAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAACAAAAwAAAAAOAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public SteamStartupService(SteamService steam, SettingsService settings, PluginInstallerService pluginInstaller)
    {
        _steam = steam;
        _settings = settings;
        _pluginInstaller = pluginInstaller;
    }

    /// <summary>True if winmm.dll (the Steam loader proxy) is currently in the Steam root.</summary>
    public bool IsLoaderInstalled
    {
        get
        {
            string? steamDir = _steam.EffectivePath;
            return steamDir is not null && File.Exists(Path.Combine(steamDir, "winmm.dll"));
        }
    }

    /// <summary>Whether starting with Steam is considered active.</summary>
    public bool IsStartWithSteamActive => _settings.StartWithSteam ?? IsLoaderInstalled;

    /// <summary>
    /// Path to the BaoTools loader target: %LOCALAPPDATA%\BaoTools\current\BaoTools.exe
    /// </summary>
    public static string BridgeExePath
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "BaoTools", "current", "BaoTools.exe");
        }
    }

    /// <summary>
    /// Ensures that %LOCALAPPDATA%\BaoTools\current\BaoTools.exe is present and routes to the active BaoTools,
    /// and that any winmm.dll in Steam is patched from the legacy LuaTools name to BaoTools.
    /// </summary>
    public void EnsureBridge()
    {
        try
        {
            PluginLog.Log("[SteamStartupService] EnsureBridge starting...");
            // 1. Ensure baotools:// protocol points to this running process
            ProtocolService.Register();

            // 2. Patch winmm.dll in Steam if present so it looks for BaoTools instead of LuaTools
            string? steamDir = _steam.EffectivePath;
            PluginLog.Log($"[SteamStartupService] Steam effective path: {steamDir ?? "null"}");
            if (steamDir is not null)
            {
                string winmm = Path.Combine(steamDir, "winmm.dll");
                if (File.Exists(winmm))
                {
                    PatchWinmmDll(winmm);
                }
            }

            // 3. Clean up any stale legacy LuaTools folder if it exists
            try
            {
                string oldLuaTools = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuaTools");
                if (Directory.Exists(oldLuaTools))
                {
                    Directory.Delete(oldLuaTools, recursive: true);
                }
            }
            catch { }

            // 4. Ensure %LOCALAPPDATA%\BaoTools\current\BaoTools.exe exists
            string bridgeDir = Path.GetDirectoryName(BridgeExePath)!;
            Directory.CreateDirectory(bridgeDir);

            // If the currently running exe IS already BridgeExePath (Velopack install), nothing to write
            string currentExe = Environment.ProcessPath ?? "";
            if (string.Equals(currentExe, BridgeExePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Write or update the forwarder exe
            byte[] forwarderBytes = Convert.FromBase64String(ForwarderExeBase64);
            bool needWrite = true;
            if (File.Exists(BridgeExePath))
            {
                try
                {
                    var existing = File.ReadAllBytes(BridgeExePath);
                    if (existing.Length == forwarderBytes.Length)
                    {
                        needWrite = false;
                    }
                }
                catch { needWrite = true; }
            }

            if (needWrite)
            {
                File.WriteAllBytes(BridgeExePath, forwarderBytes);
                PluginLog.Log($"[SteamStartupService] Written forwarder bridge to {BridgeExePath}");
            }
            else
            {
                PluginLog.Log($"[SteamStartupService] Bridge already up-to-date at {BridgeExePath}");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Log($"[SteamStartupService] EnsureBridge error: {ex}");
        }
    }

    /// <summary>
    /// Toggle starting with Steam. When enabled, ensures the bridge and loader DLL are installed.
    /// When disabled, removes or disables the loader DLL.
    /// </summary>
    public async Task<bool> SetStartWithSteamAsync(bool enable)
    {
        _settings.StartWithSteam = enable;

        if (enable)
        {
            EnsureBridge();

            if (!IsLoaderInstalled)
            {
                try
                {
                    var (ok, _) = await _pluginInstaller.InstallAsync(progress: null);
                    string? steamDir = _steam.EffectivePath;
                    if (steamDir is not null)
                    {
                        PatchWinmmDll(Path.Combine(steamDir, "winmm.dll"));
                    }
                    return ok;
                }
                catch { return false; }
            }
            return true;
        }
        else
        {
            string? steamDir = _steam.EffectivePath;
            if (steamDir is not null)
            {
                string winmm = Path.Combine(steamDir, "winmm.dll");
                if (File.Exists(winmm))
                {
                    try
                    {
                        bool wasRunning = Process.GetProcessesByName("steam").Length > 0;
                        if (wasRunning)
                        {
                            _steam.StopSteam();
                            await Task.Delay(1000);
                        }

                        File.Delete(winmm);

                        string real = Path.Combine(steamDir, "winmm_real.dll");
                        if (File.Exists(real)) File.Delete(real);

                        if (wasRunning) _steam.StartSteam();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SteamStartupService] Failed to remove winmm.dll: {ex.Message}");
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Patches winmm.dll to replace legacy 'LuaTools' -> 'BaoTools' and 'luatools' -> 'baotools'.
    /// Both strings have identical 8-byte lengths, allowing seamless 1:1 in-place byte patching.
    /// </summary>
    public static void PatchWinmmDll(string dllPath)
    {
        if (!File.Exists(dllPath)) return;
        try
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            bool modified = false;
            byte[] luaToolsBytes = System.Text.Encoding.ASCII.GetBytes("LuaTools");
            byte[] baoToolsBytes = System.Text.Encoding.ASCII.GetBytes("BaoTools");
            byte[] luaLowerBytes = System.Text.Encoding.ASCII.GetBytes("luatools");
            byte[] baoLowerBytes = System.Text.Encoding.ASCII.GetBytes("baotools");

            for (int i = 0; i <= bytes.Length - 8; i++)
            {
                if (Matches(bytes, i, luaToolsBytes))
                {
                    Array.Copy(baoToolsBytes, 0, bytes, i, 8);
                    modified = true;
                    i += 7;
                }
                else if (Matches(bytes, i, luaLowerBytes))
                {
                    Array.Copy(baoLowerBytes, 0, bytes, i, 8);
                    modified = true;
                    i += 7;
                }
            }

            if (modified)
            {
                File.WriteAllBytes(dllPath, bytes);
                PluginLog.Log($"[SteamStartupService] Successfully patched winmm.dll to BaoTools: {dllPath}");
            }
            else
            {
                PluginLog.Log($"[SteamStartupService] winmm.dll already patched or no matches: {dllPath}");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Log($"[SteamStartupService] Failed to patch winmm.dll: {ex}");
        }
    }

    /// <summary>
    /// Checks if a patched winmm.dll on disk matches the expected GitHub release digest when reversed
    /// back to the upstream 'LuaTools' bytes.
    /// </summary>
    public static bool MatchesPatchedWinmm(string dllPath, string? expectedDigest)
    {
        if (string.IsNullOrWhiteSpace(expectedDigest) || !File.Exists(dllPath)) return false;
        try
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            byte[] luaToolsBytes = System.Text.Encoding.ASCII.GetBytes("LuaTools");
            byte[] baoToolsBytes = System.Text.Encoding.ASCII.GetBytes("BaoTools");
            byte[] luaLowerBytes = System.Text.Encoding.ASCII.GetBytes("luatools");
            byte[] baoLowerBytes = System.Text.Encoding.ASCII.GetBytes("baotools");

            bool modified = false;
            for (int i = 0; i <= bytes.Length - 8; i++)
            {
                if (Matches(bytes, i, baoToolsBytes))
                {
                    Array.Copy(luaToolsBytes, 0, bytes, i, 8);
                    modified = true;
                    i += 7;
                }
                else if (Matches(bytes, i, baoLowerBytes))
                {
                    Array.Copy(luaLowerBytes, 0, bytes, i, 8);
                    modified = true;
                    i += 7;
                }
            }

            if (!modified) return false;

            string cleanDigest = expectedDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? expectedDigest[7..]
                : expectedDigest;

            using var sha = System.Security.Cryptography.SHA256.Create();
            string hash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
            return hash.Equals(cleanDigest.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool Matches(byte[] source, int offset, byte[] pattern)
    {
        for (int j = 0; j < pattern.Length; j++)
        {
            if (source[offset + j] != pattern[j]) return false;
        }
        return true;
    }
}
