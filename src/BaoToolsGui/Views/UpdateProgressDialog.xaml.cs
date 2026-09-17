using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BaoToolsGui.Models;
using BaoToolsGui.Resources;
using BaoToolsGui.Services;

namespace BaoToolsGui.Views;

public partial class UpdateProgressDialog : Window
{
    private readonly GitHubReleaseInfo _releaseInfo;
    private readonly UpdateService _updateService;
    private readonly string _browserFallbackUrl;
    private CancellationTokenSource? _cts;
    private PreparedUpdate? _preparedUpdate;
    private bool _isReady = false;
    private bool _isFailed = false;

    public PreparedUpdate? ResultPreparedUpdate => _preparedUpdate;

    public UpdateProgressDialog(
        GitHubReleaseInfo releaseInfo,
        UpdateService updateService,
        PreparedUpdate? existingPreparedUpdate = null,
        string? browserFallbackUrl = null)
    {
        InitializeComponent();

        _releaseInfo = releaseInfo;
        _updateService = updateService;
        _preparedUpdate = existingPreparedUpdate;
        _browserFallbackUrl = !string.IsNullOrWhiteSpace(browserFallbackUrl)
            ? browserFallbackUrl
            : (!string.IsNullOrWhiteSpace(releaseInfo.DownloadUrl) ? releaseInfo.DownloadUrl : "https://github.com/DevBaor/BaoTools_1005/releases/latest");

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_preparedUpdate != null && _preparedUpdate.IsValid)
        {
            ShowReadyState(_preparedUpdate);
            return;
        }

        await StartDownloadAsync();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isReady && !_isFailed && _cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
    }

    private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void BtnCloseX_Click(object sender, RoutedEventArgs e)
    {
        if (!_isReady && !_isFailed)
        {
            _cts?.Cancel();
        }
        Close();
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        if (!_isReady && !_isFailed)
        {
            _cts?.Cancel();
        }
        Close();
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (_isReady && _preparedUpdate != null && _preparedUpdate.IsValid)
        {
            BtnPrimary.IsEnabled = false;
            BtnSecondary.IsEnabled = false;
            BtnCloseX.IsEnabled = false;
            BtnPrimaryText.Text = Strings.UpdateDialog_Restarting;

            // Apply prepared update, launch batch script, and shutdown cleanly
            _updateService.ApplyPreparedUpdate(_preparedUpdate);
        }
        else if (_isFailed)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _browserFallbackUrl,
                    UseShellExecute = true
                });
            }
            catch { }
            Close();
        }
    }

    private async Task StartDownloadAsync()
    {
        _cts = new CancellationTokenSource();
        _isReady = false;
        _isFailed = false;

        HeaderBlock.Text = Strings.UpdateDialog_DownloadingHeader;
        VersionTagText.Text = $"{_releaseInfo.TagName} • {Strings.UpdateDialog_DownloadingStatus}";
        VersionPill.Background = new SolidColorBrush(Color.FromRgb(0x1c, 0x25, 0x36));
        VersionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x25, 0x63, 0xeb));
        VersionTagText.Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xa5, 0xfa));

        DownloadingMessageBlock.Text = string.Format(Strings.UpdateDialog_DownloadingSub, _releaseInfo.TagName);
        BtnSecondary.Content = Strings.UpdateDialog_Cancel;
        BtnPrimary.Visibility = Visibility.Collapsed;
        PanelDownloading.Visibility = Visibility.Visible;
        PanelReady.Visibility = Visibility.Collapsed;
        PanelFailed.Visibility = Visibility.Collapsed;

        LogoImage.Visibility = Visibility.Visible;
        SuccessIcon.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Collapsed;

        IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x24));
        IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x36, 0x36, 0x4a));

        var progress = new Progress<double?>(pct =>
        {
            if (pct.HasValue)
            {
                DownloadProgressBar.IsIndeterminate = false;
                double p = Math.Clamp(pct.Value * 100, 0, 100);
                DownloadProgressBar.Value = p;
                PercentText.Text = $"{p:0}%";
                DownloadDetailText.Text = $"{p:0}% / 100%";
            }
            else
            {
                DownloadProgressBar.IsIndeterminate = true;
                PercentText.Text = string.Empty;
                DownloadDetailText.Text = Strings.Notification_Downloading;
            }
        });

        try
        {
            var prep = await _updateService.PrepareUpdateAsync(_releaseInfo, progress, _cts.Token);
            if (prep != null && prep.IsValid)
            {
                _preparedUpdate = prep;
                ShowReadyState(prep);
            }
            else
            {
                ShowFailedState("File download failed or verified size mismatch.");
            }
        }
        catch (OperationCanceledException)
        {
            // User pressed cancel, close quietly
            Close();
        }
        catch (Exception ex)
        {
            ShowFailedState(ex.Message);
        }
    }

    private void ShowReadyState(PreparedUpdate prep)
    {
        _isReady = true;
        _isFailed = false;

        HeaderBlock.Text = Strings.UpdateDialog_ReadyHeader;
        VersionTagText.Text = $"{prep.TagName} • {Strings.UpdateDialog_ReadyStatus}";
        VersionPill.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x26, 0x1d));
        VersionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x10, 0xb9, 0x81));
        VersionTagText.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xd3, 0x99));

        ReadyTitleBlock.Text = Strings.UpdateDialog_ReadyHeader;
        ReadyDetailBlock.Text = $"{prep.TagName} - {Strings.UpdateDialog_ReadyStatus}";
        ReadyMessageBlock.Text = string.Format(Strings.UpdateDialog_ReadySub, prep.TagName);

        LogoImage.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Collapsed;
        SuccessIcon.Visibility = Visibility.Visible;

        IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x26, 0x1d));
        IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0xb9, 0x81));

        PanelDownloading.Visibility = Visibility.Collapsed;
        PanelFailed.Visibility = Visibility.Collapsed;
        PanelReady.Visibility = Visibility.Visible;

        BtnSecondary.Content = Strings.UpdateDialog_Later;
        BtnPrimaryText.Text = Strings.UpdateDialog_RestartNow;
        BtnPrimary.Visibility = Visibility.Visible;
    }

    private void ShowFailedState(string errorMessage)
    {
        _isReady = false;
        _isFailed = true;

        HeaderBlock.Text = Strings.UpdateDialog_FailedHeader;
        VersionTagText.Text = $"{_releaseInfo.TagName} • Error";
        VersionPill.Background = new SolidColorBrush(Color.FromRgb(0x2b, 0x14, 0x14));
        VersionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xef, 0x44, 0x44));
        VersionTagText.Foreground = new SolidColorBrush(Color.FromRgb(0xf8, 0x71, 0x71));

        FailedMessageBlock.Text = errorMessage;

        LogoImage.Visibility = Visibility.Collapsed;
        SuccessIcon.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Visible;

        IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x2b, 0x14, 0x14));
        IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xef, 0x44, 0x44));

        PanelDownloading.Visibility = Visibility.Collapsed;
        PanelReady.Visibility = Visibility.Collapsed;
        PanelFailed.Visibility = Visibility.Visible;

        BtnSecondary.Content = Strings.Notification_Close;
        BtnPrimaryText.Text = Strings.UpdateDialog_OpenBrowser;
        BtnPrimary.Visibility = Visibility.Visible;
    }


    /// <summary>
    /// Displays the UpdateProgressDialog modally over the active window with background dimming scrim.
    /// </summary>
    public static PreparedUpdate? ShowDialog(
        GitHubReleaseInfo releaseInfo,
        UpdateService updateService,
        PreparedUpdate? existing = null,
        string? fallbackUrl = null,
        Window? owner = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => ShowDialog(releaseInfo, updateService, existing, fallbackUrl, owner));
        }

        var dialog = new UpdateProgressDialog(releaseInfo, updateService, existing, fallbackUrl);

        var targetOwner = owner
            ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
            ?? Application.Current?.MainWindow;

        if (targetOwner != null && targetOwner.IsVisible)
        {
            dialog.Owner = targetOwner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Topmost = true;
        }

        MainWindow? mainWindow = targetOwner as MainWindow ?? Application.Current?.MainWindow as MainWindow;
        bool usedScrim = false;
        double origOwnerOpacity = targetOwner?.Opacity ?? 1.0;

        try
        {
            if (mainWindow != null && mainWindow.IsVisible && mainWindow.DialogDimScrim != null)
            {
                mainWindow.DialogDimScrim.Visibility = Visibility.Visible;
                usedScrim = true;
            }
            else if (targetOwner != null && targetOwner != mainWindow)
            {
                targetOwner.Opacity = 0.65;
            }

            dialog.ShowDialog();
        }
        finally
        {
            if (usedScrim && mainWindow?.DialogDimScrim != null)
            {
                mainWindow.DialogDimScrim.Visibility = Visibility.Collapsed;
            }
            if (targetOwner != null && targetOwner != mainWindow)
            {
                targetOwner.Opacity = origOwnerOpacity;
            }
        }

        return dialog.ResultPreparedUpdate;
    }
}
