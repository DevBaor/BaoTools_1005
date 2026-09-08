using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BaoToolsGui.Resources;
using BaoToolsGui.Services;

namespace BaoToolsGui.ViewModels;

/// <summary>
/// ViewModel for the dedicated Tickets page.
/// Manages Steam & Denuvo ticket extraction, auto-filters games for Denuvo protection,
/// and exports BaoTools_ticket zip sharing packages.
/// </summary>
public partial class TicketsViewModel : ObservableObject
{
    private readonly SteamTicketService _ticketService;
    private readonly SteamService _steam;
    private readonly ToastService _toast;

    private byte[]? _lastRawAppTicket;
    private byte[]? _lastRawETicket;

    public TicketsViewModel(SteamTicketService ticketService, SteamService steam, ToastService toast)
    {
        _ticketService = ticketService;
        _steam = steam;
        _toast = toast;

        CheckSteamStatus();
        _ = LoadDenuvoGamesAsync();
    }

    [ObservableProperty] private string _appIdInput = "";
    [ObservableProperty] private DenuvoGameOption? _selectedGame;
    [ObservableProperty] private ObservableCollection<DenuvoGameOption> _denuvoGames = [];
    [ObservableProperty] private bool _isLoadingGames;
    [ObservableProperty] private bool _isExtracting;
    [ObservableProperty] private bool _autoApplyToLua = true;
    [ObservableProperty] private bool _saveFiles = true;
    [ObservableProperty] private bool _isSteamRunning;

    [ObservableProperty] private string? _ownershipTicketHex;
    [ObservableProperty] private int _ownershipTicketSize;
    [ObservableProperty] private string? _encryptedTicketHex;
    [ObservableProperty] private int _encryptedTicketSize;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasExtracted;
    [ObservableProperty] private string? _savedFolder;
    [ObservableProperty] private string? _lastExportedZipPath;

    public bool HasOwnershipTicket => !string.IsNullOrWhiteSpace(OwnershipTicketHex);
    public bool HasEncryptedTicket => !string.IsNullOrWhiteSpace(EncryptedTicketHex);

    public string SteamStatusText => IsSteamRunning ? Strings.Tickets_SteamRunning : Strings.Tickets_SteamNotRunning;
    public string SteamStatusHint => IsSteamRunning ? Strings.Tickets_SteamReadyHint : Strings.Tickets_SteamClosedHint;
    public string SteamStatusColor => IsSteamRunning ? "#10b981" : "#f59e0b";
    public string SteamStatusSymbol => IsSteamRunning ? "CheckmarkCircle24" : "Warning24";
    public bool CanExtract => !IsExtracting;

    partial void OnIsSteamRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(SteamStatusText));
        OnPropertyChanged(nameof(SteamStatusHint));
        OnPropertyChanged(nameof(SteamStatusColor));
        OnPropertyChanged(nameof(SteamStatusSymbol));
    }

    partial void OnIsExtractingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExtract));
    }

    partial void OnSelectedGameChanged(DenuvoGameOption? value)
    {
        if (value is not null)
        {
            AppIdInput = value.AppId.ToString();
        }
    }

    partial void OnOwnershipTicketHexChanged(string? value) => OnPropertyChanged(nameof(HasOwnershipTicket));
    partial void OnEncryptedTicketHexChanged(string? value) => OnPropertyChanged(nameof(HasEncryptedTicket));

    [RelayCommand]
    public void CheckSteamStatus()
    {
        IsSteamRunning = _ticketService.IsSteamRunning;
    }

    [RelayCommand]
    public async Task LoadDenuvoGamesAsync()
    {
        if (IsLoadingGames) return;
        IsLoadingGames = true;

        try
        {
            var games = await _ticketService.GetInstalledDenuvoGamesAsync();
            DenuvoGames.Clear();
            foreach (var g in games)
            {
                DenuvoGames.Add(g);
            }
        }
        catch { }
        finally
        {
            IsLoadingGames = false;
        }
    }

    [RelayCommand]
    public async Task ExtractTicketsAsync()
    {
        if (IsExtracting) return;

        CheckSteamStatus();
        if (!IsSteamRunning)
        {
            _toast.Show(Strings.Tickets_Title, Strings.Tickets_SteamNotRunning, error: true);
            StatusMessage = Strings.Tickets_SteamNotRunning;
            return;
        }

        if (!uint.TryParse(AppIdInput.Trim(), out uint appId) || appId == 0)
        {
            _toast.Show(Strings.Tickets_Title, Strings.Tickets_InvalidAppId, error: true);
            StatusMessage = Strings.Tickets_InvalidAppId;
            return;
        }

        IsExtracting = true;
        StatusMessage = Strings.Tickets_Extracting;

        try
        {
            var result = await _ticketService.ExtractTicketsAsync(appId, AutoApplyToLua, SaveFiles);

            if (result.Success)
            {
                OwnershipTicketHex = result.AppTicketHex;
                OwnershipTicketSize = result.AppTicketBytes;
                EncryptedTicketHex = result.ETicketHex;
                EncryptedTicketSize = result.ETicketBytes;
                SavedFolder = result.SavedFolderPath;
                _lastRawAppTicket = result.RawAppTicket;
                _lastRawETicket = result.RawETicket;
                HasExtracted = true;

                string msg = string.Format(Strings.Tickets_ExtractSuccess, appId);
                StatusMessage = msg;
                _toast.Show(Strings.Tickets_Title, msg);
            }
            else
            {
                StatusMessage = result.Error ?? Strings.Tickets_ExtractFailed;
                _toast.Show(Strings.Tickets_Title, StatusMessage, error: true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _toast.Show(Strings.Tickets_Title, ex.Message, error: true);
        }
        finally
        {
            IsExtracting = false;
        }
    }

    [RelayCommand]
    public void ExportZipPackage()
    {
        if (!uint.TryParse(AppIdInput.Trim(), out uint appId)) return;
        if (!HasOwnershipTicket && !HasEncryptedTicket) return;

        try
        {
            string zipPath = _ticketService.ExportSharingZip(
                appId,
                OwnershipTicketHex,
                EncryptedTicketHex,
                _lastRawAppTicket,
                _lastRawETicket);

            LastExportedZipPath = zipPath;
            string msg = string.Format(Strings.Tickets_ExportSuccess, Path.GetFileName(zipPath));
            _toast.Show(Strings.Tickets_Title, msg);

            SteamService.RevealInExplorer(zipPath);
        }
        catch (Exception ex)
        {
            _toast.Show(Strings.Tickets_Title, $"Export failed: {ex.Message}", error: true);
        }
    }

    [RelayCommand]
    public void CopyAppTicketHex()
    {
        if (string.IsNullOrWhiteSpace(OwnershipTicketHex)) return;
        Clipboard.SetText(OwnershipTicketHex);
        _toast.Show(Strings.Tickets_Title, Strings.Tickets_CopiedAppTicket);
    }

    [RelayCommand]
    public void CopyETicketHex()
    {
        if (string.IsNullOrWhiteSpace(EncryptedTicketHex)) return;
        Clipboard.SetText(EncryptedTicketHex);
        _toast.Show(Strings.Tickets_Title, Strings.Tickets_CopiedETicket);
    }

    [RelayCommand]
    public void CopyLuaSnippet()
    {
        if (!uint.TryParse(AppIdInput.Trim(), out uint appId)) return;

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(OwnershipTicketHex))
            lines.Add($"setAppTicket({appId}, \"{OwnershipTicketHex}\")");
        if (!string.IsNullOrWhiteSpace(EncryptedTicketHex))
            lines.Add($"setETicket({appId}, \"{EncryptedTicketHex}\")");

        if (lines.Count == 0) return;

        Clipboard.SetText(string.Join("\n", lines));
        _toast.Show(Strings.Tickets_Title, Strings.Tickets_CopiedLua);
    }

    [RelayCommand]
    public void OpenSavedFolder()
    {
        if (!string.IsNullOrWhiteSpace(SavedFolder) && Directory.Exists(SavedFolder))
        {
            SteamService.ShowInExplorer(SavedFolder);
        }
    }
}
