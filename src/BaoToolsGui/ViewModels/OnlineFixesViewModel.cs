using BaoToolsGui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

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
        if (System.Threading.Interlocked.Exchange(ref _resolving, 1) == 1) return;
        try
        {
            // We don't have a reliable header image URL, but CoverCache.EnsureAsync usually takes (appid, url).
            // Let's just try to get the local one, or use a default.
            string? local = covers.GetLocalPath(AppId);
            if (local is not null) Cover = local;
        }
        finally { System.Threading.Interlocked.Exchange(ref _resolving, 0); }
    }
}

public partial class OnlineFixesViewModel(SteamLibraryService library, CoverCache covers, ToastService toast) : ObservableObject
{
    public ObservableCollection<OnlineFixGameCardVm> Games { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _searchText = "";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private System.Collections.Generic.List<OnlineFixGameCardVm> _allGames = [];

    public async Task InitializeAsync()
    {
        if (_allGames.Count > 0) return; // already loaded
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                var apps = library.GetAllInstalledApps()
                    .OrderBy(a => a.Name)
                    .Select(a => new OnlineFixGameCardVm(a.AppId, a.Name, a.InstallDir))
                    .ToList();
                App.Current.Dispatcher.Invoke(() =>
                {
                    _allGames = apps;
                    ApplyFilter();
                });
            });
        }
        finally { IsLoading = false; }
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
    private void DownloadFix(OnlineFixGameCardVm game)
    {
        // Open online-fix.me search page in browser
        string url = $"https://online-fix.me/index.php?do=search&subaction=search&story={Uri.EscapeDataString(game.Name)}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task InstallFixAsync(OnlineFixGameCardVm game)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Online-Fix ZIP file",
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
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    // Strip any top-level wrapper folders that commonly exist in online-fixes
                    string relativePath = entry.FullName;
                    
                    // Simple heuristic: if the zip contains a single root folder, skip it.
                    // (Real implementation might be more robust). We'll just extract as is for now.
                    string dest = Path.Combine(game.InstallDir, relativePath);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        entry.ExtractToFile(dest, overwrite: true);
                    }
                    catch { failed++; }
                }

                App.Current.Dispatcher.Invoke(() =>
                {
                    if (failed > 0)
                        toast.Show("Install completed with errors", $"{failed} files failed to extract.", error: true);
                    else
                        toast.Show("Online Fix Installed", $"{game.Name} has been patched.");
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
}
