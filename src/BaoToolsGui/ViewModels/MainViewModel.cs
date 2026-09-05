using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BaoToolsGui.Services;

namespace BaoToolsGui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SteamService _steam;
    private readonly UpdateService _updates;
    private readonly ToastService _toast;

    /// <summary>The first-run welcome overlay VM (hosted at the window root, shown via its IsOpen).</summary>
    public OnboardingViewModel Onboarding { get; }

    /// <summary>App version shown in the nav pane footer, e.g. "v1.0.1". Read from the assembly.</summary>
    public string VersionLabel { get; } = "v105.1";

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
    [ObservableProperty] private string _updateStatusMessage = "You're up to date!";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    [NotifyPropertyChangedFor(nameof(HasUpdateError))]
    private string? _updateError;

    public bool HasUpdateContent => HasUpdate && !IsCheckingUpdate;
    public bool IsUpToDate => !HasUpdate && !IsCheckingUpdate && string.IsNullOrEmpty(UpdateError);
    public bool HasUpdateError => !string.IsNullOrEmpty(UpdateError) && !IsCheckingUpdate;

    public MainViewModel(
        AuthService auth,
        SteamService steam,
        OnboardingViewModel onboarding,
        UpdateService updates,
        ToastService toast)
    {
        _auth = auth;
        _steam = steam;
        Onboarding = onboarding;
        _updates = updates;
        _toast = toast;
        _auth.AuthStateChanged += () => IsGuest = _auth.IsGuest;
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
            if (info is null)
            {
                UpdateError = "Unable to reach GitHub. Please check your internet connection.";
                if (showToastIfUpToDate)
                {
                    _toast.Show("BaoTools Update", "Unable to check for updates. Please try again later.", error: true);
                }
                return;
            }

            if (info.IsNewer)
            {
                HasUpdate = true;
                HasUnreadNotification = true;
                LatestVersion = info.TagName;
                UpdateTitle = string.IsNullOrWhiteSpace(info.Title) ? $"Release {info.TagName}" : info.Title;
                UpdateChangelog = info.Body;
                UpdateUrl = string.IsNullOrWhiteSpace(info.DownloadUrl) ? "https://baotools.baotranduy666666.workers.dev/" : info.DownloadUrl;
                ReleaseNotesUrl = string.IsNullOrWhiteSpace(info.HtmlUrl) ? "https://github.com/DevBaor/BaoTools_1005/releases/latest" : info.HtmlUrl;
                UpdatePublishedAt = info.PublishedAt?.ToString("MMM dd, yyyy");
                UpdateStatusMessage = $"New update available ({info.TagName})";

                _toast.ShowAction(
                    "BaoTools Update",
                    $"A new update is available ({info.TagName})! Click to download.",
                    "Update Now",
                    DownloadUpdate);
            }
            else
            {
                HasUpdate = false;
                LatestVersion = info.TagName;
                UpdateStatusMessage = $"BaoTools {VersionLabel} is up to date.";
                if (showToastIfUpToDate)
                {
                    _toast.Show("BaoTools Update", $"You're all up to date! ({VersionLabel})");
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
    private void DownloadUpdate()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = UpdateUrl,
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
