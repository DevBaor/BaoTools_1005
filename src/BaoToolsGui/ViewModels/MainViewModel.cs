using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BaoToolsGui.Services;
using BaoToolsGui.Models;

namespace BaoToolsGui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SteamService _steam;
    private readonly UpdateService _updates;
    private readonly UpdateHistoryService _historyService;
    private readonly ToastService _toast;

    /// <summary>The first-run welcome overlay VM (hosted at the window root, shown via its IsOpen).</summary>
    public OnboardingViewModel Onboarding { get; }

    /// <summary>App version shown in the nav pane footer, e.g. "v1.0.1". Read from the assembly.</summary>
    public string VersionLabel { get; } = "v105.4";

    private static string ReadVersion()
    {
        // InformationalVersion carries the csproj <Version> (may have a "+commit" suffix. Trim it).
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var ver = info ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
        int plus = ver.IndexOf('+');
        return plus >= 0 ? ver[..plus] : ver;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRealUser))]
    [NotifyPropertyChangedFor(nameof(FooterStatus))]
    private bool _isGuest = true;

    public bool IsRealUser => !IsGuest;

    /// <summary>Bottom-of-pane line: version plus auth state. No username shown (privacy).</summary>
    public string FooterStatus => $"{VersionLabel} · {(IsGuest ? Resources.Strings.Nav_Footer_Guest : Resources.Strings.Nav_Footer_LoggedIn)}";

    [ObservableProperty] private bool _isSigningIn;
    [ObservableProperty] private string? _signInError;

    // ── Update Notification state ──────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateContent))]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    private bool _hasUpdate;

    [ObservableProperty] private bool _hasUnreadNotification;
    [ObservableProperty] private bool _isNotificationOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateContent))]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    [NotifyPropertyChangedFor(nameof(HasUpdateError))]
    private bool _isCheckingUpdate;

    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private string? _updateTitle;
    [ObservableProperty] private string? _updateChangelog;
    [ObservableProperty] private string _updateUrl = "https://baotools.baotranduy666666.workers.dev/";
    [ObservableProperty] private string _releaseNotesUrl = "https://github.com/DevBaor/BaoTools_1005/releases/latest";
    [ObservableProperty] private string? _updatePublishedAt;
    [ObservableProperty] private string _updateStatusMessage = Resources.Strings.Notification_UpToDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    [NotifyPropertyChangedFor(nameof(HasUpdateError))]
    private string? _updateError;

    private GitHubReleaseInfo? _latestReleaseInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private string _updateButtonText = Resources.Strings.Notification_UpdateNow;

    public bool CanUpdate => !IsDownloadingUpdate;

    public bool HasUpdateContent => HasUpdate && !IsCheckingUpdate;
    public bool IsUpToDate => !HasUpdate && !IsCheckingUpdate && string.IsNullOrEmpty(UpdateError);
    public bool HasUpdateError => !string.IsNullOrEmpty(UpdateError) && !IsCheckingUpdate;

    /// <summary>History of past updates displayed in the notification bell flyout.</summary>
    public System.Collections.ObjectModel.ObservableCollection<UpdateHistoryItem> UpdateHistory { get; } = new();

    [ObservableProperty]
    private bool _hasUpdateHistory;

    public MainViewModel(
        AuthService auth,
        SteamService steam,
        OnboardingViewModel onboarding,
        UpdateService updates,
        UpdateHistoryService historyService,
        ToastService toast)
    {
        _auth = auth;
        _steam = steam;
        Onboarding = onboarding;
        _updates = updates;
        _historyService = historyService;
        _toast = toast;
        _auth.AuthStateChanged += () => IsGuest = _auth.IsGuest;
        SettingsViewModel.LanguageChanged += OnLanguageChanged;

        LoadLocalHistory();
    }

    private void OnLanguageChanged()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            LoadLocalHistory();
            UpdateButtonText = Resources.Strings.Notification_UpdateNow;
            UpdateStatusMessage = HasUpdate
                ? $"{Resources.Strings.Notification_NewUpdateAvailable} ({LatestVersion})"
                : string.Format(Resources.Strings.Notification_UpToDateDesc, VersionLabel);
            OnPropertyChanged(nameof(FooterStatus));
        });
    }

    private void LoadLocalHistory()
    {
        try
        {
            var items = _historyService.LoadHistory(VersionLabel);
            UpdateHistory.Clear();
            foreach (var it in items)
            {
                UpdateHistory.Add(it);
            }
            HasUpdateHistory = UpdateHistory.Count > 0;
        }
        catch { }
    }

    public async Task InitializeAsync()
    {
        await _auth.InitializeAsync();
        IsGuest = _auth.IsGuest;
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsSigningIn) return;
        IsSigningIn = true;
        SignInError = null;
        try
        {
            await _auth.SignInAsync();
        }
        catch (Exception ex)
        {
            SignInError = ex.Message;
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    /// <summary>Confirm, then kill + relaunch Steam so newly added/removed luas take effect.</summary>
    [RelayCommand]
    private void RestartSteam()
    {
        var result = MessageBox.Show(
            Resources.Strings.Main_RestartSteam_Ask,
            Resources.Strings.Manage_RestartSteam_Title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        if (!_steam.RestartSteam())
            MessageBox.Show(
                Resources.Strings.Manage_RestartSteam_Failed,
                Resources.Strings.Manage_RestartSteam_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }

    [RelayCommand]
    private void ToggleNotification()
    {
        IsNotificationOpen = !IsNotificationOpen;
        if (IsNotificationOpen)
        {
            HasUnreadNotification = false;
        }
    }

    [RelayCommand]
    private void CloseNotification()
    {
        IsNotificationOpen = false;
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        await CheckForUpdatesInternalAsync(showToastIfUpToDate: true);
    }

    public async Task CheckForUpdatesInternalAsync(bool showToastIfUpToDate = false)
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        UpdateError = null;

        try
        {
            var info = await _updates.CheckGitHubReleaseFullAsync(VersionLabel);
            try
            {
                var remoteHistory = await _updates.FetchReleasesHistoryAsync(VersionLabel);
                var merged = _historyService.MergeWithRemoteReleases(remoteHistory, VersionLabel);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UpdateHistory.Clear();
                    foreach (var item in merged)
                    {
                        UpdateHistory.Add(item);
                    }
                    HasUpdateHistory = UpdateHistory.Count > 0;
                });
            }
            catch { /* history fetch error doesn't block update check */ }

            if (info is null)
            {
                UpdateError = Resources.Strings.Notification_CheckFailed;
                if (showToastIfUpToDate)
                {
                    _toast.Show(Resources.Strings.Notification_Title, Resources.Strings.Notification_CheckFailed, error: true);
                }
                return;
            }

            if (info.IsNewer)
            {
                _latestReleaseInfo = info;
                HasUpdate = true;
                HasUnreadNotification = true;
                LatestVersion = info.TagName;
                UpdateTitle = string.IsNullOrWhiteSpace(info.Title) ? $"Release {info.TagName}" : info.Title;
                UpdateChangelog = info.Body;
                UpdateUrl = string.IsNullOrWhiteSpace(info.DownloadUrl) ? "https://baotools.baotranduy666666.workers.dev/" : info.DownloadUrl;
                ReleaseNotesUrl = string.IsNullOrWhiteSpace(info.HtmlUrl) ? "https://github.com/DevBaor/BaoTools_1005/releases/latest" : info.HtmlUrl;
                UpdatePublishedAt = info.PublishedAt?.ToString("MMM dd, yyyy");
                UpdateStatusMessage = $"{Resources.Strings.Notification_NewUpdateAvailable} ({info.TagName})";

                _toast.ShowAction(
                    Resources.Strings.Notification_Title,
                    $"{Resources.Strings.Notification_NewUpdateAvailable} ({info.TagName})!",
                    Resources.Strings.Notification_UpdateNow,
                    () => _ = DownloadUpdateAsync());
            }
            else
            {
                HasUpdate = false;
                LatestVersion = info.TagName;
                UpdateStatusMessage = string.Format(Resources.Strings.Notification_UpToDateDesc, VersionLabel);
                if (showToastIfUpToDate)
                {
                    _toast.Show(Resources.Strings.Notification_Title, $"{Resources.Strings.Notification_UpToDate} ({VersionLabel})");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (IsDownloadingUpdate) return;
        if (_latestReleaseInfo is null)
        {
            OpenBrowserUrl(UpdateUrl);
            return;
        }

        IsDownloadingUpdate = true;
        UpdateButtonText = Resources.Strings.Notification_Downloading;

        try
        {
            var progress = new Progress<double?>(pct =>
            {
                if (pct.HasValue)
                {
                    double p = Math.Clamp(pct.Value * 100, 0, 100);
                    UpdateButtonText = $"{Resources.Strings.Notification_Downloading} {p:0}%";
                }
                else
                {
                    UpdateButtonText = Resources.Strings.Notification_Downloading;
                }
            });

            bool applied = await _updates.DownloadAndApplyUpdateAsync(_latestReleaseInfo, progress);
            if (!applied)
            {
                OpenBrowserUrl(UpdateUrl);
            }
        }
        catch (Exception ex)
        {
            _toast.Show(Resources.Strings.Notification_Title, ex.Message, error: true);
            OpenBrowserUrl(UpdateUrl);
        }
        finally
        {
            IsDownloadingUpdate = false;
            UpdateButtonText = Resources.Strings.Notification_UpdateNow;
        }
    }

    private void OpenBrowserUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenReleaseNotes()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ReleaseNotesUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
