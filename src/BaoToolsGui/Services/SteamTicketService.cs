using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaoToolsGui.Models;
using Microsoft.Win32;

namespace BaoToolsGui.Services;

public record DenuvoGameOption(long AppId, string Name, string? HeaderImage, string DetectionSource);

public record TicketExtractResult(
    bool Success,
    uint AppId,
    string? AppTicketHex,
    int AppTicketBytes,
    string? ETicketHex,
    int ETicketBytes,
    string? Error,
    string? SavedFolderPath,
    byte[]? RawAppTicket = null,
    byte[]? RawETicket = null);

/// <summary>
/// Service responsible for extracting AppOwnershipTicket and EncryptedAppTicket (Denuvo ticket)
/// from the running Steam client instance, matching OpenSteamTool's extract_tickets logic.
/// Also provides auto-filtering for games with Denuvo protection and zip package export.
/// </summary>
public class SteamTicketService
{
    private readonly SteamService _steam;
    private readonly SteamLibraryService _library;
    private readonly SteamAppInfoCache _appInfoCache;
    private readonly BaoToolsApiClient _api;
    private readonly ToastService _toast;

    public SteamTicketService(
        SteamService steam,
        SteamLibraryService library,
        SteamAppInfoCache appInfoCache,
        BaoToolsApiClient api,
        ToastService toast)
    {
        _steam = steam;
        _library = library;
        _appInfoCache = appInfoCache;
        _api = api;
        _toast = toast;
    }

    /// <summary>True if a steam.exe process is currently active.</summary>
    public bool IsSteamRunning => Process.GetProcessesByName("steam").Length > 0;

    /// <summary>
    /// Scans installed and managed games, returning only those protected by Denuvo.
    /// Combines Steam Store DRM notices (Denuvo Anti-tamper), verified BaoTools Denuvo entries,
    /// and PE binary section scans.
    /// </summary>
    public async Task<List<DenuvoGameOption>> GetInstalledDenuvoGamesAsync(CancellationToken ct = default)
    {
        var denuvoList = new Dictionary<long, (string Name, string? Cover, string Source)>();

        // 1. Fetch Denuvo listings from BaoTools API - only keep items explicitly tagged as Denuvo / crack
        HashSet<long> apiDenuvoIds = [];
        try
        {
            var listings = await _api.GetDenuvoListingsAsync(ct);
            if (listings?.Games is not null)
            {
                foreach (var g in listings.Games)
                {
                    bool hasDenuvoTag = g.Tags != null && g.Tags.Any(t =>
                        t.Slug.Contains("denuvo", StringComparison.OrdinalIgnoreCase) ||
                        t.Slug.Contains("voices38", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("Denuvo", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("crack", StringComparison.OrdinalIgnoreCase));

                    if (hasDenuvoTag && long.TryParse(g.AppId, out long id) && id > 0)
                    {
                        apiDenuvoIds.Add(id);
                    }
                }
            }
        }
        catch { /* offline or API unreachable, continue with other detectors */ }

        // 2. Gather candidate games from installed Steam apps and stplug-in Lua files
        var installedMap = new Dictionary<long, (string Name, string InstallDir)>();
        foreach (var (appId, name, installDir) in _library.GetAllInstalledApps())
        {
            installedMap[appId] = (name, installDir);
        }

        var candidateAppIds = new HashSet<long>(installedMap.Keys);

        if (!string.IsNullOrWhiteSpace(_steam.StPlugInDir) && Directory.Exists(_steam.StPlugInDir))
        {
            foreach (var file in Directory.EnumerateFiles(_steam.StPlugInDir, "*.lua"))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                if (long.TryParse(stem, out long appId) && appId > 0)
                {
                    candidateAppIds.Add(appId);
                }
            }
        }

        // 3. Evaluate each candidate
        foreach (long appId in candidateAppIds)
        {
            string? name = null;
            if (installedMap.TryGetValue(appId, out var inst))
            {
                name = inst.Name;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                var cached = _appInfoCache.GetCached(appId);
                name = cached?.Name;
            }

            // A. Check Steam Store DRM Notice (fetch if not yet cached)
            string detailsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BaoToolsGui", "details", $"{appId}.json");

            if (!File.Exists(detailsPath))
            {
                try
                {
                    await _appInfoCache.EnsureFullDetailsAsync(appId, ct, background: false);
                }
                catch { }
            }

            if (CheckAppDetailsForDenuvo(appId))
            {
                name ??= _appInfoCache.GetCached(appId)?.Name ?? $"App {appId}";
                denuvoList[appId] = (name, SteamAppInfoCache.GuessHeaderImageUrl(appId), "Steam DRM Notice");
                continue;
            }

            // B. Check verified BaoTools Denuvo tags
            if (apiDenuvoIds.Contains(appId))
            {
                name ??= _appInfoCache.GetCached(appId)?.Name ?? $"App {appId}";
                denuvoList[appId] = (name, SteamAppInfoCache.GuessHeaderImageUrl(appId), "BaoTools Database");
                continue;
            }

            // C. PE binary section scan for games installed on disk
            if (installedMap.TryGetValue(appId, out var installedInfo) &&
                Directory.Exists(installedInfo.InstallDir) &&
                CheckFolderForDenuvoPe(installedInfo.InstallDir))
            {
                name ??= $"App {appId}";
                denuvoList[appId] = (name, SteamAppInfoCache.GuessHeaderImageUrl(appId), "PE Binary Scan");
            }
        }

        return denuvoList
            .Select(kvp => new DenuvoGameOption(kvp.Key, kvp.Value.Name, kvp.Value.Cover, kvp.Value.Source))
            .OrderBy(g => g.Name)
            .ToList();
    }

    /// <summary>
    /// Check if cached Steam Store details indicate Denuvo DRM in drm_notice.
    /// </summary>
    public static bool CheckAppDetailsForDenuvo(long appId)
    {
        try
        {
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BaoToolsGui", "details");
            string path = Path.Combine(appDataDir, $"{appId}.json");
            if (!File.Exists(path)) return false;

            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("drm_notice", out var drmNotice))
            {
                string? notice = drmNotice.GetString();
                if (!string.IsNullOrEmpty(notice) && notice.Contains("Denuvo", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { return false; }
        return false;
    }

    /// <summary>
    /// Fast check of PE section headers for Denuvo signatures (.arch, .srdata, .xpdata, .xtls).
    /// </summary>
    private static bool CheckFolderForDenuvoPe(string installDir)
    {
        try
        {
            var exes = Directory.EnumerateFiles(installDir, "*.exe", SearchOption.AllDirectories)
                .Take(15); // limit scan depth

            foreach (var exe in exes)
            {
                var fi = new FileInfo(exe);
                // Denuvo binaries are typically > 30MB
                if (fi.Length < 30 * 1024 * 1024) continue;

                if (HasDenuvoPeSections(exe)) return true;
            }
        }
        catch { /* folder access issues */ }

        return false;
    }

    private static bool HasDenuvoPeSections(string exePath)
    {
        try
        {
            using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            if (fs.Length < 0x200) return false;
            if (br.ReadUInt16() != 0x5A4D) return false; // "MZ"

            fs.Seek(0x3C, SeekOrigin.Begin);
            int e_lfanew = br.ReadInt32();
            if (e_lfanew <= 0 || e_lfanew + 0x100 > fs.Length) return false;

            fs.Seek(e_lfanew, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) return false; // "PE\0\0"

            fs.Seek(e_lfanew + 4 + 2, SeekOrigin.Begin);
            ushort numberOfSections = br.ReadUInt16();

            fs.Seek(e_lfanew + 4 + 16, SeekOrigin.Begin);
            ushort sizeOfOptionalHeader = br.ReadUInt16();

            long sectionTableOffset = e_lfanew + 24 + sizeOfOptionalHeader;
            if (sectionTableOffset + (numberOfSections * 40) > fs.Length) return false;

            for (int i = 0; i < numberOfSections; i++)
            {
                fs.Seek(sectionTableOffset + (i * 40), SeekOrigin.Begin);
                byte[] nameBytes = br.ReadBytes(8);
                string sectionName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                if (sectionName.Equals(".arch", StringComparison.OrdinalIgnoreCase) ||
                    sectionName.Equals(".srdata", StringComparison.OrdinalIgnoreCase) ||
                    sectionName.Equals(".xpdata", StringComparison.OrdinalIgnoreCase) ||
                    sectionName.Equals(".xtls", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Executes ticket extraction for the specified AppID.
    /// </summary>
    public async Task<TicketExtractResult> ExtractTicketsAsync(
        uint appId,
        bool updateLua,
        bool saveFiles,
        CancellationToken ct = default)
    {
        if (appId == 0)
        {
            return new TicketExtractResult(false, 0, null, 0, null, 0, "Invalid AppID.", null);
        }

        if (!IsSteamRunning)
        {
            return new TicketExtractResult(false, appId, null, 0, null, 0,
                "Steam is not running. Please open Steam and sign in to an account that owns the game.", null);
        }

        string? steamPath = _steam.EffectivePath;
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            return new TicketExtractResult(false, appId, null, 0, null, 0, "Steam installation path not found.", null);
        }

        return await Task.Run(() =>
        {
            try
            {
                return ExtractTicketsCore(appId, steamPath, updateLua, saveFiles);
            }
            catch (Exception ex)
            {
                return new TicketExtractResult(false, appId, null, 0, null, 0, $"Extraction failed: {ex.Message}", null);
            }
        }, ct);
    }

    private TicketExtractResult ExtractTicketsCore(uint appId, string steamPath, bool updateLua, bool saveFiles)
    {
        // 1. Set target game environment variables
        Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
        Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());

        string steamClientDll = Path.Combine(steamPath, "steamclient64.dll");
        if (!File.Exists(steamClientDll))
        {
            return new TicketExtractResult(false, appId, null, 0, null, 0, $"steamclient64.dll not found in {steamPath}", null);
        }

        // Set DLL directory to Steam root so tier0_s64.dll and vstdlib_s64.dll load correctly
        SetDllDirectory(steamPath);

        IntPtr hModule = LoadLibraryEx(steamClientDll, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (hModule == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            return new TicketExtractResult(false, appId, null, 0, null, 0, $"Failed to load steamclient64.dll (error code: {err}).", null);
        }

        try
        {
            IntPtr createInterfaceProc = GetProcAddress(hModule, "CreateInterface");
            if (createInterfaceProc == IntPtr.Zero)
            {
                return new TicketExtractResult(false, appId, null, 0, null, 0, "steamclient64.dll does not export CreateInterface.", null);
            }

            var createInterface = Marshal.GetDelegateForFunctionPointer<CreateInterfaceDelegate>(createInterfaceProc);

            // Try interfaces
            IntPtr pSteamClient = IntPtr.Zero;
            string[] clientVersions = ["SteamClient023", "SteamClient022", "SteamClient021", "SteamClient020"];
            foreach (var ver in clientVersions)
            {
                pSteamClient = createInterface(ver, out int _);
                if (pSteamClient != IntPtr.Zero) break;
            }

            if (pSteamClient == IntPtr.Zero)
            {
                return new TicketExtractResult(false, appId, null, 0, null, 0, "Could not initialize ISteamClient interface.", null);
            }

            // Create Steam Pipe & User
            IntPtr vtableClient = Marshal.ReadIntPtr(pSteamClient);

            // CreateSteamPipe (index 0)
            var createSteamPipe = Marshal.GetDelegateForFunctionPointer<CreateSteamPipeDelegate>(
                Marshal.ReadIntPtr(vtableClient, 0 * IntPtr.Size));
            int pipe = createSteamPipe(pSteamClient);
            if (pipe == 0)
            {
                return new TicketExtractResult(false, appId, null, 0, null, 0, "CreateSteamPipe returned 0. Ensure Steam is running and logged in.", null);
            }

            try
            {
                // ConnectToGlobalUser (index 2)
                var connectToGlobalUser = Marshal.GetDelegateForFunctionPointer<ConnectToGlobalUserDelegate>(
                    Marshal.ReadIntPtr(vtableClient, 2 * IntPtr.Size));
                int user = connectToGlobalUser(pSteamClient, pipe);
                if (user == 0)
                {
                    return new TicketExtractResult(false, appId, null, 0, null, 0, "ConnectToGlobalUser returned 0. Make sure a user is signed into Steam.", null);
                }

                // Get ISteamAppTicket (index 12: GetISteamGenericInterface)
                var getGenericInterface = Marshal.GetDelegateForFunctionPointer<GetISteamGenericInterfaceDelegate>(
                    Marshal.ReadIntPtr(vtableClient, 12 * IntPtr.Size));

                IntPtr pAppTicket = getGenericInterface(pSteamClient, user, pipe, "STEAMAPPTICKET_INTERFACE_VERSION001");

                byte[]? appTicketBytes = null;
                if (pAppTicket != IntPtr.Zero)
                {
                    IntPtr vtableAppTicket = Marshal.ReadIntPtr(pAppTicket);
                    var getAppOwnershipTicketData = Marshal.GetDelegateForFunctionPointer<GetAppOwnershipTicketDataDelegate>(
                        Marshal.ReadIntPtr(vtableAppTicket, 0 * IntPtr.Size));

                    byte[] buffer = new byte[4096];
                    uint written = getAppOwnershipTicketData(
                        pAppTicket, appId, buffer, (uint)buffer.Length,
                        out uint _, out uint _, out uint _, out uint _);

                    if (written > 0 && written <= buffer.Length)
                    {
                        appTicketBytes = new byte[written];
                        Array.Copy(buffer, appTicketBytes, written);
                    }
                }

                // Get ISteamUser (index 5) & ISteamUtils (index 9)
                var getISteamUser = Marshal.GetDelegateForFunctionPointer<GetISteamUserDelegate>(
                    Marshal.ReadIntPtr(vtableClient, 5 * IntPtr.Size));
                var getISteamUtils = Marshal.GetDelegateForFunctionPointer<GetISteamUtilsDelegate>(
                    Marshal.ReadIntPtr(vtableClient, 9 * IntPtr.Size));

                IntPtr pUser = getISteamUser(pSteamClient, user, pipe, "SteamUser023");
                if (pUser == IntPtr.Zero) pUser = getISteamUser(pSteamClient, user, pipe, "SteamUser021");

                IntPtr pUtils = getISteamUtils(pSteamClient, pipe, "SteamUtils010");
                if (pUtils == IntPtr.Zero) pUtils = getISteamUtils(pSteamClient, pipe, "SteamUtils009");

                byte[]? encryptedTicketBytes = null;
                if (pUser != IntPtr.Zero && pUtils != IntPtr.Zero)
                {
                    IntPtr vtableUser = Marshal.ReadIntPtr(pUser);
                    IntPtr vtableUtils = Marshal.ReadIntPtr(pUtils);

                    // RequestEncryptedAppTicket (index 21)
                    var requestEncryptedTicket = Marshal.GetDelegateForFunctionPointer<RequestEncryptedAppTicketDelegate>(
                        Marshal.ReadIntPtr(vtableUser, 21 * IntPtr.Size));
                    // GetEncryptedAppTicket (index 22)
                    var getEncryptedTicket = Marshal.GetDelegateForFunctionPointer<GetEncryptedAppTicketDelegate>(
                        Marshal.ReadIntPtr(vtableUser, 22 * IntPtr.Size));

                    // IsAPICallCompleted (index 11)
                    var isAPICallCompleted = Marshal.GetDelegateForFunctionPointer<IsAPICallCompletedDelegate>(
                        Marshal.ReadIntPtr(vtableUtils, 11 * IntPtr.Size));
                    // GetAPICallResult (index 13)
                    var getAPICallResult = Marshal.GetDelegateForFunctionPointer<GetAPICallResultDelegate>(
                        Marshal.ReadIntPtr(vtableUtils, 13 * IntPtr.Size));

                    ulong hCall = requestEncryptedTicket(pUser, IntPtr.Zero, 0);
                    if (hCall != 0)
                    {
                        const int maxWaitMs = 15000;
                        const int stepMs = 50;
                        int waited = 0;
                        bool failed = false;

                        while (!isAPICallCompleted(pUtils, hCall, out failed) && waited < maxWaitMs)
                        {
                            Thread.Sleep(stepMs);
                            waited += stepMs;
                        }

                        if (!failed && waited < maxWaitMs)
                        {
                            IntPtr pResp = Marshal.AllocHGlobal(Marshal.SizeOf<EncryptedAppTicketResponse>());
                            try
                            {
                                if (getAPICallResult(pUtils, hCall, pResp, Marshal.SizeOf<EncryptedAppTicketResponse>(), 154, out failed) && !failed)
                                {
                                    var resp = Marshal.PtrToStructure<EncryptedAppTicketResponse>(pResp);
                                    if (resp.Result == 1) // k_EResultOK
                                    {
                                        getEncryptedTicket(pUser, null, 0, out uint ticketSize);
                                        if (ticketSize > 0 && ticketSize < 65536)
                                        {
                                            byte[] buf = new byte[ticketSize];
                                            if (getEncryptedTicket(pUser, buf, buf.Length, out uint actualWritten) && actualWritten > 0)
                                            {
                                                encryptedTicketBytes = new byte[actualWritten];
                                                Array.Copy(buf, encryptedTicketBytes, actualWritten);
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pResp);
                            }
                        }
                    }
                }

                string? appTicketHex = appTicketBytes is not null ? Convert.ToHexString(appTicketBytes).ToLowerInvariant() : null;
                string? eTicketHex = encryptedTicketBytes is not null ? Convert.ToHexString(encryptedTicketBytes).ToLowerInvariant() : null;

                if (appTicketHex is null && eTicketHex is null)
                {
                    return new TicketExtractResult(false, appId, null, 0, null, 0,
                        $"No tickets returned for AppID {appId}. Make sure your signed-in Steam account genuinely owns this game.", null);
                }

                // Write to Registry for OpenSteamTool platform credential store
                try
                {
                    using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Valve\Steam\Apps\{appId}");
                    if (appTicketHex is not null) appKey.SetValue("AppTicket", appTicketHex, RegistryValueKind.String);
                    if (eTicketHex is not null) appKey.SetValue("ETicket", eTicketHex, RegistryValueKind.String);
                }
                catch { }

                string? savedFolder = null;
                if (saveFiles)
                {
                    savedFolder = SaveTicketFiles(appId, appTicketBytes, encryptedTicketBytes, appTicketHex, eTicketHex);
                }

                if (updateLua)
                {
                    ApplyTicketsToLua(appId, appTicketHex, eTicketHex);
                }

                return new TicketExtractResult(
                    true,
                    appId,
                    appTicketHex,
                    appTicketBytes?.Length ?? 0,
                    eTicketHex,
                    encryptedTicketBytes?.Length ?? 0,
                    null,
                    savedFolder,
                    appTicketBytes,
                    encryptedTicketBytes);
            }
            finally
            {
                // BReleaseSteamPipe (index 1)
                var releaseSteamPipe = Marshal.GetDelegateForFunctionPointer<BReleaseSteamPipeDelegate>(
                    Marshal.ReadIntPtr(vtableClient, 1 * IntPtr.Size));
                releaseSteamPipe(pSteamClient, pipe);
            }
        }
        finally
        {
            FreeLibrary(hModule);
        }
    }

    /// <summary>
    /// Injects or updates setAppTicket and setETicket into config/stplug-in/<appid>.lua.
    /// </summary>
    public void ApplyTicketsToLua(uint appId, string? appTicketHex, string? eTicketHex)
    {
        string? pluginDir = _steam.StPlugInDir;
        if (string.IsNullOrWhiteSpace(pluginDir)) return;

        Directory.CreateDirectory(pluginDir);
        string luaPath = Path.Combine(pluginDir, $"{appId}.lua");

        string content = File.Exists(luaPath) ? File.ReadAllText(luaPath) : $"addappid({appId})\n";

        // Update setAppTicket
        if (!string.IsNullOrWhiteSpace(appTicketHex))
        {
            var rx = new Regex(@"setAppTicket\s*\(\s*" + appId + @"\s*,\s*""[^""]*""\s*\)", RegexOptions.IgnoreCase);
            string line = $"setAppTicket({appId}, \"{appTicketHex}\")";
            if (rx.IsMatch(content))
            {
                content = rx.Replace(content, line);
            }
            else
            {
                content += "\n" + line;
            }
        }

        // Update setETicket
        if (!string.IsNullOrWhiteSpace(eTicketHex))
        {
            var rx = new Regex(@"setETicket\s*\(\s*" + appId + @"\s*,\s*""[^""]*""\s*\)", RegexOptions.IgnoreCase);
            string line = $"setETicket({appId}, \"{eTicketHex}\")";
            if (rx.IsMatch(content))
            {
                content = rx.Replace(content, line);
            }
            else
            {
                content += "\n" + line;
            }
        }

        File.WriteAllText(luaPath, content.Trim() + "\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// Packages tickets, <appid>.lua, BaoTools_ticket.bat, and ReadMe.txt into a ready-to-share ZIP file.
    /// </summary>
    public string ExportSharingZip(
        uint appId,
        string? appTicketHex,
        string? eTicketHex,
        byte[]? appTicketBytes,
        byte[]? eTicketBytes,
        string? customOutputFolder = null)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string outputDir = !string.IsNullOrWhiteSpace(customOutputFolder) && Directory.Exists(customOutputFolder)
            ? customOutputFolder
            : (Directory.Exists(desktop) ? desktop : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BaoToolsGui", "exports"));

        Directory.CreateDirectory(outputDir);
        string zipPath = Path.Combine(outputDir, $"BaoTools_ticket_{appId}.zip");

        // Temporary staging folder
        string stagingDir = Path.Combine(Path.GetTempPath(), "BaoTools_ticket_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        try
        {
            // 1. Generate <appId>.lua
            var luaSb = new StringBuilder();
            luaSb.AppendLine($"addappid({appId})");
            if (!string.IsNullOrWhiteSpace(appTicketHex))
                luaSb.AppendLine($"setAppTicket({appId}, \"{appTicketHex}\")");
            if (!string.IsNullOrWhiteSpace(eTicketHex))
                luaSb.AppendLine($"setETicket({appId}, \"{eTicketHex}\")");
            File.WriteAllText(Path.Combine(stagingDir, $"{appId}.lua"), luaSb.ToString(), new UTF8Encoding(false));

            // 2. Generate BaoTools_ticket.bat
            string batContent = $@"@echo off
chcp 65001 >nul
title BaoTools Ticket - Auto Installer
echo ============================================================
echo            BaoTools Ticket - Steam Auto Installer
echo ============================================================
echo [BaoTools] Dang cai dat ve ban quyen cho AppID: {appId}...
echo.

set ""STEAMPATH=""
for /f ""tokens=2*"" %%a in ('reg query ""HKCU\Software\Valve\Steam"" /v ""SteamPath"" 2^>nul') do set ""STEAMPATH=%%b""
if ""%STEAMPATH%""=="""" (
    for /f ""tokens=2*"" %%a in ('reg query ""HKLM\SOFTWARE\WOW6432Node\Valve\Steam"" /v ""InstallPath"" 2^>nul') do set ""STEAMPATH=%%b""
)
if ""%STEAMPATH%""=="""" (
    for /f ""tokens=2*"" %%a in ('reg query ""HKLM\SOFTWARE\Valve\Steam"" /v ""InstallPath"" 2^>nul') do set ""STEAMPATH=%%b""
)

if ""%STEAMPATH%""=="""" (
    echo [LOI] Khong tim thay thu muc cai dat Steam tren may cua ban!
    echo Vui long tu copy file ""{appId}.lua"" vao thu muc ""Steam\config\stplug-in\"".
    echo.
    pause
    exit /b 1
)

echo [OK] Da tim thay Steam tai: ""%STEAMPATH%""
set ""TARGET=%STEAMPATH%\config\stplug-in""
if not exist ""%TARGET%"" mkdir ""%TARGET%""

copy /y ""%~dp0{appId}.lua"" ""%TARGET%\{appId}.lua"" >nul
if errorlevel 1 (
    echo [LOI] Khong the sao chep file vao thu muc Steam.
    echo Vui long thu chay file nay bang quyen Administrator (Run as administrator)!
) else (
    echo.
    echo [THANH CONG] Da cai dat ve thanh cong vao:
    echo ""%TARGET%\{appId}.lua""
    echo.
    echo Gio ban co the mo Steam de khoi chay game!
)
echo.
pause
";
            File.WriteAllText(Path.Combine(stagingDir, "BaoTools_ticket.bat"), batContent, Encoding.Default);

            // 3. Generate ReadMe.txt
            string readmeContent = $@"============================================================
                 BaoTools_ticket Sharing Package
============================================================
AppID: {appId}

HUONG DAN SU DUNG:

Cach 1 (Nhanh nhat - Khuyen dung):
  Bam dup chuot vao file ""BaoTools_ticket.bat"" de tu dong cai dat vao Steam.

Cach 2 (Neu ban dung ung dung BaoTools):
  Keo tha file zip nay hoac file ""{appId}.lua"" truc tiep vao BaoTools.

Cach 3 (Thu cong):
  Sao chep file ""{appId}.lua"" vao thu muc:
  <Thu muc cai Steam>\config\stplug-in\{appId}.lua
============================================================
";
            File.WriteAllText(Path.Combine(stagingDir, "ReadMe.txt"), readmeContent, Encoding.UTF8);

            // 4. Raw tickets subfolder
            string ticketsSubDir = Path.Combine(stagingDir, "tickets");
            Directory.CreateDirectory(ticketsSubDir);

            if (appTicketBytes is not null && appTicketBytes.Length > 0)
                File.WriteAllBytes(Path.Combine(ticketsSubDir, "appticket.bin"), appTicketBytes);
            if (eTicketBytes is not null && eTicketBytes.Length > 0)
                File.WriteAllBytes(Path.Combine(ticketsSubDir, "eticket.bin"), eTicketBytes);

            var sb = new StringBuilder();
            sb.AppendLine($"appid:{appId}");
            sb.AppendLine(!string.IsNullOrWhiteSpace(appTicketHex) ? $"appticket({appTicketBytes?.Length ?? 0}bytes):{appTicketHex}" : "appticket:null");
            sb.AppendLine(!string.IsNullOrWhiteSpace(eTicketHex) ? $"eticket({eTicketBytes?.Length ?? 0}bytes):{eTicketHex}" : "eticket:null");
            File.WriteAllText(Path.Combine(ticketsSubDir, "tickets.txt"), sb.ToString(), Encoding.UTF8);

            // 5. Create ZIP
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(stagingDir, zipPath);

            return zipPath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
            }
            catch { }
        }
    }

    private static string SaveTicketFiles(
        uint appId,
        byte[]? appTicket,
        byte[]? eTicket,
        string? appTicketHex,
        string? eTicketHex)
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BaoToolsGui", "tickets", appId.ToString());

        Directory.CreateDirectory(baseDir);

        if (appTicket is not null)
            File.WriteAllBytes(Path.Combine(baseDir, "appticket.bin"), appTicket);

        if (eTicket is not null)
            File.WriteAllBytes(Path.Combine(baseDir, "eticket.bin"), eTicket);

        var sb = new StringBuilder();
        sb.AppendLine($"appid:{appId}");
        sb.AppendLine(appTicketHex is not null ? $"appticket({appTicket?.Length ?? 0}bytes):{appTicketHex}" : "appticket:null");
        sb.AppendLine(eTicketHex is not null ? $"eticket({eTicket?.Length ?? 0}bytes):{eTicketHex}" : "eticket:null");

        File.WriteAllText(Path.Combine(baseDir, "tickets.txt"), sb.ToString(), Encoding.UTF8);

        return baseDir;
    }

    // ── Win32 / Steam Interop ───────────────────────────────────────

    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateInterfaceDelegate([MarshalAs(UnmanagedType.LPStr)] string pName, out int pReturnCode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateSteamPipeDelegate(IntPtr thisPtr);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool BReleaseSteamPipeDelegate(IntPtr thisPtr, int hSteamPipe);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ConnectToGlobalUserDelegate(IntPtr thisPtr, int hSteamPipe);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetISteamGenericInterfaceDelegate(IntPtr thisPtr, int hSteamUser, int hSteamPipe, [MarshalAs(UnmanagedType.LPStr)] string pchVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetISteamUserDelegate(IntPtr thisPtr, int hSteamUser, int hSteamPipe, [MarshalAs(UnmanagedType.LPStr)] string pchVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetISteamUtilsDelegate(IntPtr thisPtr, int hSteamPipe, [MarshalAs(UnmanagedType.LPStr)] string pchVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetAppOwnershipTicketDataDelegate(
        IntPtr thisPtr,
        uint nAppId,
        [Out] byte[] pvBuffer,
        uint cbBufferLength,
        out uint piAppId,
        out uint piSteamId,
        out uint piSignature,
        out uint pcbSignature);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong RequestEncryptedAppTicketDelegate(IntPtr thisPtr, IntPtr pDataToInclude, int cbDataToInclude);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool GetEncryptedAppTicketDelegate(IntPtr thisPtr, [Out] byte[]? pTicket, int cbMaxTicket, out uint pcbTicket);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool IsAPICallCompletedDelegate(IntPtr thisPtr, ulong hSteamAPICall, [MarshalAs(UnmanagedType.I1)] out bool pbFailed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool GetAPICallResultDelegate(IntPtr thisPtr, ulong hSteamAPICall, IntPtr pCallback, int cubCallback, int iCallbackExpected, [MarshalAs(UnmanagedType.I1)] out bool pbFailed);

    [StructLayout(LayoutKind.Sequential)]
    private struct EncryptedAppTicketResponse
    {
        public int Result; // 1 = k_EResultOK
    }
}
