using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BaoToolsGui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BaoToolsGui.ViewModels;

public partial class OnlineFixGameCardVm(long appId, string name, string installDir) : ObservableObject
{
    public long AppId { get; } = appId;
    public string Name { get; } = name;
    public string InstallDir { get; } = installDir;

    [ObservableProperty] private string? _cover;
    private int _resolving;

    public bool Matches(string q) =>
        Name.Contains(q, StringComparison.OrdinalIgnoreCase) || AppId.ToString().Contains(q);

    public async Task EnsureCoverAsync(CoverCache covers)
    {
        if (Cover is not null) return;
        if (Interlocked.Exchange(ref _resolving, 1) == 1) return;
        try
        {
            string? local = covers.GetLocalPath(AppId);
            if (local is null)
            {
                local = await covers.EnsureAsync(AppId, SteamAppInfoCache.GuessHeaderImageUrl(AppId));
            }
            if (local is not null)
            {
                Application.Current?.Dispatcher.Invoke(() => Cover = local);
            }
        }
        catch { }
        finally { Interlocked.Exchange(ref _resolving, 0); }
    }
}

public partial class OnlineFixesViewModel(SteamLibraryService library, CoverCache covers, ToastService toast) : ObservableObject
{
    public ObservableCollection<OnlineFixGameCardVm> Games { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";

    public bool IsEmpty => !IsLoading && Games.Count == 0;
    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchText)
        ? Resources.Strings.OnlineFixes_EmptyLibrary
        : Resources.Strings.OnlineFixes_EmptySearch;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private List<OnlineFixGameCardVm> _allGames = [];

    public async Task InitializeAsync(bool force = false)
    {
        if (!force && (_allGames.Count > 0 || IsLoading)) return;
        IsLoading = true;
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var apps = await Task.Run(() =>
            {
                return library.GetAllInstalledApps()
                    .Where(a => !IsToolOrRedistributable(a.AppId, a.Name))
                    .OrderBy(a => a.Name)
                    .Select(a => new OnlineFixGameCardVm(a.AppId, a.Name, a.InstallDir))
                    .ToList();
            });

            _allGames = apps;
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    private void ApplyFilter()
    {
        var shown = string.IsNullOrWhiteSpace(SearchText)
            ? _allGames
            : _allGames.Where(g => g.Matches(SearchText));

        Games.Clear();
        foreach (var g in shown)
        {
            Games.Add(g);
            _ = g.EnsureCoverAsync(covers);
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await InitializeAsync(force: true);
    }

    [RelayCommand]
    private void DownloadFix(OnlineFixGameCardVm game)
    {
        // Open online-fix.me search page in browser
        string url = $"https://online-fix.me/index.php?do=search&subaction=search&story={Uri.EscapeDataString(game.Name)}";
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenFolder(OnlineFixGameCardVm game)
    {
        if (Directory.Exists(game.InstallDir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = game.InstallDir,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void CopyAppId(OnlineFixGameCardVm game)
    {
        Clipboard.SetText(game.AppId.ToString());
        toast.Show("BaoTools", $"Copied AppID: {game.AppId}");
    }

    [RelayCommand]
    private async Task InstallFixAsync(OnlineFixGameCardVm game)
    {
        var dlg = new OpenFileDialog
        {
            Title = $"Select Online-Fix ZIP for {game.Name}",
            Filter = "ZIP Archives (*.zip)|*.zip|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(dlg.FileName);
                int failed = 0;
                int extracted = 0;

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string relativePath = entry.FullName;
                    string dest = Path.Combine(game.InstallDir, relativePath);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        entry.ExtractToFile(dest, overwrite: true);
                        extracted++;
                    }
                    catch { failed++; }
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (failed > 0)
                        toast.Show("Install with warnings", $"{extracted} files extracted, {failed} failed.", error: true);
                    else
                        toast.Show("Online Fix Installed", $"{game.Name} has been successfully patched!");
                });
            });
        }
        catch (Exception ex)
        {
            toast.Show("Install Failed", ex.Message, error: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static bool IsToolOrRedistributable(long appId, string name)
    {
        // Steam tools, runtimes, SDKs, and redistributables
        if (appId is 228980 or 250820 or 1070560 or 1391110 or 896660 or 217030 or 244850)
            return true;

        if (name.Contains("Steamworks Common Redistributables", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dedicated Server", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Proton ", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" SDK", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" Tool", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Shared Resources", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
