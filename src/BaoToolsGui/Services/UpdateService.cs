using System.IO;
using Velopack;
using Velopack.Sources;
using BaoToolsGui.Models;

namespace BaoToolsGui.Services;

/// <summary>
/// Silent background auto-update via Velopack + GitHub Releases. Checks on launch,
/// downloads the (delta) update in the background, and stages it to apply on next exit.
/// <para>
/// Resilience is two-layered: (1) <see cref="ProxiedFileDownloader"/> routes each repo's feed + package
/// downloads through GitHub mirrors for blocked/throttled regions (e.g. China); (2) it tries each repo in
/// <see cref="AppConfig.GithubReleasesRepos"/> in order, so if the PRIMARY repo is gone entirely
/// (banned / DMCA'd / account removed. Something the mirrors can't fix) it falls through to a backup repo.
/// </para>
/// </summary>
public class UpdateService
{
    private readonly GithubProxy _gh;

    public UpdateService(GithubProxy gh)
    {
        _gh = gh;
    }

    public UpdateService() : this(new GithubProxy())
    {
    }

    // One UpdateManager per configured repo, in priority order (primary first). All share the proxied
    // downloader so every repo is also mirror-resilient.
    private readonly UpdateManager[] _managers =
        AppConfig.GithubReleasesRepos
            .Select(repo => new UpdateManager(
                new GithubSource(repo, accessToken: null, prerelease: false,
                    downloader: new ProxiedFileDownloader())))
            .ToArray();

    // The manager whose repo actually produced the staged update. Apply against this same one.
    private UpdateManager? _stagedMgr;
    private UpdateInfo? _staged;

    /// <summary>Raised on the thread pool when an update has finished downloading and is ready.</summary>
    public event Action? UpdateReady;

    /// <summary>True once an update is downloaded and waiting to be applied.</summary>
    public bool HasStagedUpdate => _staged is not null;

    /// <summary>Check for, download, and stage an update. Tries each repo in order until one yields a
    /// usable update; the first success wins. No-op for un-installed (dev) builds.</summary>
    public async Task CheckAndStageAsync()
    {
        // IsInstalled is a property of the Velopack install, not the repo, so any manager answers it.
        if (_managers.Length == 0 || !_managers[0].IsInstalled) return; // `dotnet run` / unpacked builds

        foreach (var mgr in _managers)
        {
            try
            {
                var info = await mgr.CheckForUpdatesAsync();
                // A reachable repo returning null means we're already up to date. STOP. Don't fall
                // through to a backup (it may lag behind the primary and would offer no/older update).
                // Backups exist for an UNreachable primary, which surfaces as an exception below.
                if (info is null) return;

                await mgr.DownloadUpdatesAsync(info);
                _stagedMgr = mgr;
                _staged = info;
                UpdateReady?.Invoke();
                return; // staged from this repo. Done
            }
            catch
            {
                // This repo is unreachable/gone (or a download failed). Fall through to the next backup.
            }
        }
        // Every repo failed (offline, or all repos down). Fail silently, retry next launch.
    }

    /// <summary>Apply the staged update now and relaunch into the new version. <paramref name="restartArgs"/>
    /// are passed to the relaunched process (e.g. the loader's --minimized --tray-locked) so the new
    /// instance keeps its session semantics.</summary>
    public void ApplyAndRestart(string[]? restartArgs = null)
    {
        if (_stagedMgr is not null && _staged is not null)
            _stagedMgr.ApplyUpdatesAndRestart(_staged, restartArgs);
    }

    /// <summary>Apply the staged update after the app exits (no forced restart).</summary>
    public void ApplyOnExit()
    {
        if (_stagedMgr is not null && _staged is not null)
            _stagedMgr.WaitExitThenApplyUpdates(_staged, silent: true, restart: false);
    }

    /// <summary>
    /// Checks GitHub Releases API for DevBaor/BaoTools_1005 to see if a newer version is released.
    /// Returns full release info (with IsNewer = true if newer version detected), or null on network failure.
    /// </summary>
    public async Task<GitHubReleaseInfo?> CheckGitHubReleaseFullAsync(string currentVersion)
    {
        string url = "https://api.github.com/repos/DevBaor/BaoTools_1005/releases/latest";
        using var client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BaoTools");
        client.Timeout = TimeSpan.FromSeconds(5);

        foreach (var candidate in GithubProxy.Candidates(url))
        {
            try
            {
                var res = await client.GetAsync(candidate);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var parsed = ParseGitHubRelease(json, currentVersion);
                    if (parsed is not null) return parsed;
                }
            }
            catch
            {
                // Network failure / rate limit / offline. Try next mirror candidate.
            }
        }
        return null;
    }

    /// <summary>
    /// Parses GitHub release JSON payload and compares version against currentVersion.
    /// </summary>
    public static GitHubReleaseInfo? ParseGitHubRelease(string json, string currentVersion)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return ParseSingleReleaseElement(doc.RootElement, currentVersion);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks GitHub Releases API for list of releases to display in update history.
    /// </summary>
    public async Task<List<GitHubReleaseInfo>> FetchReleasesHistoryAsync(string currentVersion)
    {
        string url = "https://api.github.com/repos/DevBaor/BaoTools_1005/releases?per_page=10";
        using var client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BaoTools");
        client.Timeout = TimeSpan.FromSeconds(5);

        foreach (var candidate in GithubProxy.Candidates(url))
        {
            try
            {
                var res = await client.GetAsync(candidate);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = ParseGitHubReleasesList(json, currentVersion);
                    if (list.Count > 0) return list;
                }
            }
            catch
            {
                // Try next mirror
            }
        }
        return new List<GitHubReleaseInfo>();
    }

    /// <summary>
    /// Parses an array of GitHub releases from JSON.
    /// </summary>
    public static List<GitHubReleaseInfo> ParseGitHubReleasesList(string json, string currentVersion)
    {
        var result = new List<GitHubReleaseInfo>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return result;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var parsed = ParseSingleReleaseElement(elem, currentVersion);
                if (parsed is not null) result.Add(parsed);
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Parses a single release JsonElement into GitHubReleaseInfo.
    /// </summary>
    public static GitHubReleaseInfo? ParseSingleReleaseElement(System.Text.Json.JsonElement elem, string currentVersion)
    {
        try
        {
            if (!elem.TryGetProperty("tag_name", out var tagProp)) return null;

            string latestTag = tagProp.GetString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(latestTag)) return null;

            string title = elem.TryGetProperty("name", out var nameProp) ? nameProp.GetString()?.Trim() ?? "" : "";
            string body = elem.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString()?.Trim() ?? "" : "";
            string htmlUrl = elem.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString()?.Trim() ?? "" : "";
            DateTimeOffset? publishedAt = null;
            if (elem.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTimeOffset(out var pubDate))
            {
                publishedAt = pubDate;
            }

            bool isNewer = IsVersionNewer(latestTag, currentVersion);

            string? setupUrl = null;
            string? portableUrl = null;
            string? exeUrl = null;

            if (elem.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string dl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (name.EndsWith("_Setup.exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
                        setupUrl = dl;
                    else if (name.EndsWith("Portable.zip", StringComparison.OrdinalIgnoreCase))
                        portableUrl = dl;
                    else if (name.Equals("BaoTools.exe", StringComparison.OrdinalIgnoreCase))
                        exeUrl = dl;
                }
            }

            return new GitHubReleaseInfo
            {
                TagName = latestTag,
                Title = string.IsNullOrWhiteSpace(title) ? latestTag : title,
                Body = body,
                HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? "https://github.com/DevBaor/BaoTools_1005/releases/latest" : htmlUrl,
                SetupDownloadUrl = setupUrl,
                PortableDownloadUrl = portableUrl,
                StandaloneExeDownloadUrl = exeUrl,
                DownloadUrl = setupUrl ?? portableUrl ?? exeUrl ?? "https://baotools.baotranduy666666.workers.dev/",
                PublishedAt = publishedAt,
                IsNewer = isNewer
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines whether latestTag represents a newer version than currentVersion.
    /// </summary>
    public static bool IsVersionNewer(string latestTag, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestTag) || string.IsNullOrWhiteSpace(currentVersion))
            return false;

        string tLatest = latestTag.Trim().TrimStart('v', 'V');
        string tCurrent = currentVersion.Trim().TrimStart('v', 'V');

        if (string.Equals(tLatest, tCurrent, StringComparison.OrdinalIgnoreCase))
            return false;

        // If both have dot-separated version components (e.g. 105.1 vs 105.2)
        if (tLatest.Contains('.') && tCurrent.Contains('.'))
        {
            var pLatest = tLatest.Split('.');
            var pCurrent = tCurrent.Split('.');
            int len = Math.Max(pLatest.Length, pCurrent.Length);
            for (int i = 0; i < len; i++)
            {
                long nLatest = i < pLatest.Length && long.TryParse(new string(pLatest[i].Where(char.IsDigit).ToArray()), out var nl) ? nl : 0;
                long nCurrent = i < pCurrent.Length && long.TryParse(new string(pCurrent[i].Where(char.IsDigit).ToArray()), out var nc) ? nc : 0;
                if (nLatest != nCurrent)
                    return nLatest > nCurrent;
            }
            return false;
        }

        // Fallback / legacy comparison: all digits (e.g. 105.1 -> 1051 vs 1005 -> 1005)
        string cleanLatest = new string(latestTag.Where(char.IsDigit).ToArray());
        string cleanCurrent = new string(currentVersion.Where(char.IsDigit).ToArray());

        if (long.TryParse(cleanLatest, out var latestNum) && long.TryParse(cleanCurrent, out var currentNum))
        {
            return latestNum > currentNum;
        }

        return !string.Equals(latestTag, currentVersion, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(latestTag, "v" + currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks GitHub Releases API for DevBaor/BaoTools_1005 to see if a newer version is released.
    /// Returns the newer tag name (e.g. "v1006") if an update is available, or null if up to date / error.
    /// </summary>
    public async Task<string?> CheckGitHubReleaseAsync(string currentVersion)
    {
        var info = await CheckGitHubReleaseFullAsync(currentVersion);
        return info?.IsNewer == true ? info.TagName : null;
    }

    /// <summary>
    /// Downloads the latest release asset and automatically launches it to apply the update.
    /// Supports Inno Setup installations, Single-File standalone executables, and Portable folder deployments.
    /// Ensures current process is gracefully closed and the updated application is automatically restarted.
    /// </summary>
    public async Task<bool> DownloadAndApplyUpdateAsync(GitHubReleaseInfo info, IProgress<double?>? progress = null, CancellationToken ct = default)
    {
        if (info is null) return false;

        string appDir = AppContext.BaseDirectory;
        int currentPid = Environment.ProcessId;
        string targetExe = File.Exists(Path.Combine(appDir, "BaoTools.exe"))
            ? Path.Combine(appDir, "BaoTools.exe")
            : (Environment.ProcessPath ?? Path.Combine(appDir, "BaoTools.exe"));

        bool isSetupInstall = File.Exists(Path.Combine(appDir, "unins000.exe"));
        bool isLooseFolder = File.Exists(Path.Combine(appDir, "BaoToolsGui.dll"));

        if (isSetupInstall)
        {
            // 1. Setup mode: download BaoTools_Setup.exe and execute with supervisor script
            string setupUrl = info.SetupDownloadUrl
                ?? $"https://github.com/DevBaor/BaoTools_1005/releases/download/{info.TagName}/BaoTools_Setup.exe";

            string tempSetup = Path.Combine(Path.GetTempPath(), $"BaoTools_Setup_{info.TagName}.exe");
            await _gh.DownloadAsync(setupUrl, tempSetup, progress, ct);

            if (!File.Exists(tempSetup)) return false;

            string batchPath = Path.Combine(Path.GetTempPath(), $"baotools_update_setup_{info.TagName}.bat");
            string scriptContent = BuildSetupBatchScript(currentPid, tempSetup, appDir, targetExe);
            File.WriteAllText(batchPath, scriptContent, System.Text.Encoding.ASCII);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(psi);

            ShutdownForUpdate();
            return true;
        }
        else if (!isLooseFolder && !string.IsNullOrEmpty(info.StandaloneExeDownloadUrl))
        {
            // 2. Standalone single-file mode: download BaoTools.exe directly
            string exeUrl = info.StandaloneExeDownloadUrl;
            string tempExe = Path.Combine(Path.GetTempPath(), $"BaoTools_{info.TagName}.exe");
            await _gh.DownloadAsync(exeUrl, tempExe, progress, ct);

            if (!File.Exists(tempExe)) return false;

            string batchPath = Path.Combine(Path.GetTempPath(), $"baotools_update_single_{info.TagName}.bat");
            string scriptContent = BuildSingleExeBatchScript(currentPid, tempExe, appDir, targetExe);
            File.WriteAllText(batchPath, scriptContent, System.Text.Encoding.ASCII);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(psi);

            ShutdownForUpdate();
            return true;
        }
        else
        {
            // 3. Portable folder mode: download Portable ZIP and extract over existing files
            string portableUrl = info.PortableDownloadUrl
                ?? $"https://github.com/DevBaor/BaoTools_1005/releases/download/{info.TagName}/BaoTools_{info.TagName}_Portable.zip";

            string tempZip = Path.Combine(Path.GetTempPath(), $"BaoTools_Portable_{info.TagName}.zip");
            await _gh.DownloadAsync(portableUrl, tempZip, progress, ct);

            if (!File.Exists(tempZip)) return false;

            string stagingDir = Path.Combine(Path.GetTempPath(), $"BaoTools_Staging_{info.TagName}");
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, true); } catch { }
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, stagingDir);

            string batchPath = Path.Combine(Path.GetTempPath(), $"baotools_update_{info.TagName}.bat");
            string scriptContent = BuildPortableBatchScript(currentPid, stagingDir, tempZip, appDir, targetExe);
            File.WriteAllText(batchPath, scriptContent, System.Text.Encoding.ASCII);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(psi);

            ShutdownForUpdate();
            return true;
        }
    }

    internal static string BuildSetupBatchScript(int currentPid, string tempSetup, string appDir, string targetExe)
    {
        return $@"@echo off
chcp 65001 > nul
:wait_exit
timeout /t 1 /nobreak > nul
tasklist /fi ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" > nul
if not errorlevel 1 goto wait_exit

""{tempSetup}"" /SILENT /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS

timeout /t 2 /nobreak > nul
tasklist /fi ""imagename eq BaoTools.exe"" 2>nul | find /i ""BaoTools.exe"" > nul
if errorlevel 1 (
    cd /d ""{appDir}""
    start """" ""{targetExe}""
)

del ""{tempSetup}"" > nul 2>&1
(goto) 2>nul & del ""%~f0""
";
    }

    internal static string BuildSingleExeBatchScript(int currentPid, string tempExe, string appDir, string targetExe)
    {
        return $@"@echo off
chcp 65001 > nul
:wait_exit
timeout /t 1 /nobreak > nul
tasklist /fi ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" > nul
if not errorlevel 1 goto wait_exit

move /y ""{tempExe}"" ""{targetExe}"" > nul
if errorlevel 1 (
    copy /y ""{tempExe}"" ""{targetExe}"" > nul
    del ""{tempExe}"" > nul 2>&1
)

cd /d ""{appDir}""
start """" ""{targetExe}""
(goto) 2>nul & del ""%~f0""
";
    }

    internal static string BuildPortableBatchScript(int currentPid, string stagingDir, string tempZip, string appDir, string targetExe)
    {
        return $@"@echo off
chcp 65001 > nul
:wait_exit
timeout /t 1 /nobreak > nul
tasklist /fi ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" > nul
if not errorlevel 1 goto wait_exit

xcopy /y /s /e ""{stagingDir}\*"" ""{appDir}\"" > nul

cd /d ""{appDir}""
start """" ""{targetExe}""

rd /s /q ""{stagingDir}"" > nul 2>&1
del ""{tempZip}"" > nul 2>&1
(goto) 2>nul & del ""%~f0""
";
    }

    private static void ShutdownForUpdate()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                mw.PrepareForShutdown();
            }
            System.Windows.Application.Current.Shutdown();
        });
    }
}

