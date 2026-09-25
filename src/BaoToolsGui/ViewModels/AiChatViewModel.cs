using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BaoToolsGui.Resources;
using BaoToolsGui.Services;
using BaoToolsGui.Services.Ai;

namespace BaoToolsGui.ViewModels;

public partial class InspectedGameContext : ObservableObject
{
    public uint AppId { get; }
    public string Name { get; }

    public InspectedGameContext(uint appId, string name)
    {
        AppId = appId;
        Name = name;
    }

    public string DisplayText => string.Format(Strings.AiChat_InspectingGame, $"{Name} ({AppId})");
    public string ClearTooltip => Strings.AiChat_ClearGameContext;

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ClearTooltip));
    }
}

public partial class AiChatViewModel : ObservableObject
{
    private readonly AiChatService _ai;
    private readonly SettingsService _settings;
    private readonly AuthService? _auth;
    private readonly CacheService? _cache;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLauncherState))]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _currentProgressStep = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotification))]
    [NotifyPropertyChangedFor(nameof(LauncherTooltipSubtitle))]
    [NotifyPropertyChangedFor(nameof(CurrentLauncherState))]
    private int _unreadCount;

    public bool HasNotification => UnreadCount > 0 && !IsOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLauncherState))]
    private bool _showFirstTimeHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLauncherState))]
    private bool _isLauncherHovered;

    // ── Rotating Assistant Discovery Messages ───────────────────────────
    private int _discoveryMessageIndex = 0;

    public string[] GetDiscoveryMessages() =>
    [
        Strings.AssistantAskAnything,
        Strings.AssistantNeedHelp,
        Strings.AssistantGameNotWorking,
        Strings.AssistantNeedFix
    ];

    [ObservableProperty]
    private string _currentDiscoveryMessage = Strings.AssistantAskAnything;

    public bool IsDiscoveryBubbleVisible => !IsOpen;

    public void AdvanceDiscoveryMessage()
    {
        var msgs = GetDiscoveryMessages();
        if (msgs.Length == 0) return;
        _discoveryMessageIndex = (_discoveryMessageIndex + 1) % msgs.Length;
        CurrentDiscoveryMessage = msgs[_discoveryMessageIndex];
    }

    public string CurrentLauncherState
    {
        get
        {
            if (IsOpen) return "AssistantOpen";
            if (ShowFirstTimeHint) return "AssistantFirstTimeHint";
            if (HasNotification) return "AssistantNotification";
            if (IsLauncherHovered) return "AssistantHovered";
            return "AssistantClosed";
        }
    }

    public string LauncherTooltipSubtitle
    {
        get
        {
            bool isVi = IsVietnameseCulture();
            if (HasNotification)
            {
                return isVi
                    ? (UnreadCount == 1 ? "Phát hiện 1 vấn đề" : $"Phát hiện {UnreadCount} vấn đề")
                    : (UnreadCount == 1 ? "1 issue detected" : $"{UnreadCount} issues detected");
            }
            return isVi ? "Hỏi đáp, chẩn đoán & sửa lỗi" : "Ask, diagnose & fix";
        }
    }

    public string LauncherAccessibilityName
    {
        get
        {
            bool isVi = IsVietnameseCulture();
            if (IsOpen)
                return isVi ? "Đóng Trợ lý BaoTools" : "Close BaoTools Assistant";
            return isVi ? "Mở Trợ lý BaoTools" : "Open BaoTools Assistant";
        }
    }

    partial void OnIsOpenChanged(bool value)
    {
        if (value)
        {
            UnreadCount = 0;
            DismissFirstTimeHint();
            if (_cache != null)
            {
                _cache.AssistantHasBeenOpened = true;
                _cache.AssistantHintDismissed = true;
            }
        }
        OnPropertyChanged(nameof(IsDiscoveryBubbleVisible));
        OnPropertyChanged(nameof(HasNotification));
        OnPropertyChanged(nameof(LauncherTooltipSubtitle));
        OnPropertyChanged(nameof(LauncherAccessibilityName));
        OnPropertyChanged(nameof(CurrentLauncherState));
    }

    [ObservableProperty]
    private string _headerStatus = "● Online";

    // Dynamic localized properties
    [ObservableProperty] private string _title = Strings.AiChat_Title;
    [ObservableProperty] private string _inputPlaceholder = "Ask BaoTools anything...";
    [ObservableProperty] private string _thinkingText = Strings.AiChat_Thinking;
    [ObservableProperty] private string _clearChatTooltip = Strings.AiChat_ClearChat;
    [ObservableProperty] private string _closeTooltip = Strings.AiChat_Close;
    [ObservableProperty] private string _sendTooltip = Strings.AiChat_Send;
    [ObservableProperty] private string _suggestionsHeader = Strings.AiChat_SuggestionsHeader;

    [ObservableProperty] private string _hintTitle = "👋 Need help?";
    [ObservableProperty] private string _hintBody = "BaoTools Assistant can diagnose and fix common game issues.";
    [ObservableProperty] private string _hintDismissButton = "Got it";

    // Active Inspected Game Context Chip
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveContextGame))]
    private InspectedGameContext? _activeContextGame;

    public bool HasActiveContextGame => ActiveContextGame != null;

    [RelayCommand]
    private void ClearActiveContextGame()
    {
        ActiveContextGame = null;
    }

    private static Action<uint, string>? _globalGameSelectedHandler;
    public static void SetGlobalInspectedGame(uint appId, string name) => _globalGameSelectedHandler?.Invoke(appId, name);

    public bool IsEnabled => _settings.EnableAiAssistant;

    public bool ShowSuggestions => Messages.Count <= 1;

    public ObservableCollection<AiChatMessage> Messages { get; } = new();

    public ObservableCollection<string> QuickPrompts { get; } = new();

    public event Action? RequestScrollToBottom;
    public event Action? RequestScrollToTop;

    public AiChatViewModel(AiChatService ai, SettingsService settings, AuthService? auth = null, CacheService? cache = null)
    {
        _ai = ai;
        _settings = settings;
        _auth = auth;
        _cache = cache;

        _globalGameSelectedHandler = (appId, name) =>
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ActiveContextGame = new InspectedGameContext(appId, name);
            });
        };
        SettingsViewModel.AiAssistantChanged += RefreshState;
        SettingsViewModel.LanguageChanged += OnLanguageChanged;

        if (_auth is not null)
        {
            _auth.AuthStateChanged += () =>
            {
                if (Messages.Count <= 1)
                {
                    Messages.Clear();
                    AddBotMessage(GetWelcomeMessage());
                    OnPropertyChanged(nameof(ShowSuggestions));
                }
            };
        }

        UpdateLocalizedTexts();
        UpdateStatusLabel();
        RefreshPrompts();
        AddBotMessage(GetWelcomeMessage());
        CheckFirstTimeHint();
    }

    private void CheckFirstTimeHint()
    {
        // Old bulky onboarding popup replaced by sleek rotating discovery speech bubble.
    }

    [RelayCommand]
    public void DismissFirstTimeHint()
    {
        ShowFirstTimeHint = false;
        if (_cache != null)
        {
            _cache.AssistantHintDismissed = true;
        }
        OnPropertyChanged(nameof(CurrentLauncherState));
    }

    public void NotifyAssistantEvent(string? reason = null)
    {
        if (!IsOpen)
        {
            UnreadCount++;
            OnPropertyChanged(nameof(HasNotification));
            OnPropertyChanged(nameof(LauncherTooltipSubtitle));
            OnPropertyChanged(nameof(CurrentLauncherState));
        }
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(IsEnabled));
    }

    private static bool IsVietnameseCulture()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
    }

    private string GetEffectiveUserName()
    {
        if (!string.IsNullOrWhiteSpace(_auth?.DisplayName))
            return _auth.DisplayName.Trim();

        string? steamPersona = SteamService.SteamPersonaName;
        if (!string.IsNullOrWhiteSpace(steamPersona))
            return steamPersona.Trim();

        string winName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(winName) &&
            !winName.Equals("admin", StringComparison.OrdinalIgnoreCase) &&
            !winName.Equals("user", StringComparison.OrdinalIgnoreCase) &&
            !winName.Equals("administrator", StringComparison.OrdinalIgnoreCase))
        {
            return winName.Trim();
        }

        return "";
    }

    private void UpdateLocalizedTexts()
    {
        bool isVi = IsVietnameseCulture();
        Title = Strings.AiChat_Title;
        InputPlaceholder = isVi ? "Hỏi BaoTools bất kỳ điều gì..." : "Ask BaoTools anything...";
        ThinkingText = Strings.AiChat_Thinking;
        ClearChatTooltip = Strings.AiChat_ClearChat;
        CloseTooltip = Strings.AiChat_Close;
        SendTooltip = Strings.AiChat_Send;
        SuggestionsHeader = Strings.AiChat_SuggestionsHeader;

        HintTitle = isVi ? "👋 Cần hỗ trợ?" : "👋 Need help?";
        HintBody = isVi ? "Trợ lý BaoTools có thể chẩn đoán và sửa các lỗi game thường gặp." : "BaoTools Assistant can diagnose and fix common game issues.";
        HintDismissButton = isVi ? "Đã hiểu" : "Got it";

        var msgs = GetDiscoveryMessages();
        if (msgs.Length > 0)
        {
            CurrentDiscoveryMessage = msgs[_discoveryMessageIndex % msgs.Length];
        }

        OnPropertyChanged(nameof(LauncherTooltipSubtitle));
        OnPropertyChanged(nameof(LauncherAccessibilityName));
        ActiveContextGame?.NotifyLanguageChanged();
        foreach (var msg in Messages)
        {
            msg.NotifyLanguageChanged();
        }
    }

    private void OnLanguageChanged()
    {
        UpdateLocalizedTexts();
        UpdateStatusLabel();
        RefreshPrompts();

        if (Messages.Count <= 1)
        {
            Messages.Clear();
            AddBotMessage(GetWelcomeMessage());
            OnPropertyChanged(nameof(ShowSuggestions));
        }
    }

    private void UpdateStatusLabel()
    {
        if (IsLoading)
        {
            HeaderStatus = IsVietnameseCulture() ? "● Đang xử lý..." : "● Analyzing...";
        }
        else
        {
            HeaderStatus = "● Online";
        }
    }

    private void RefreshPrompts()
    {
        QuickPrompts.Clear();
        if (IsVietnameseCulture())
        {
            QuickPrompts.Add("🎮 Hướng dẫn Thêm & Tải game");
            QuickPrompts.Add("🔍 Chẩn đoán lỗi game / Văng crash");
            QuickPrompts.Add("🛠️ Cách dùng Bản sửa lỗi (Fixes)");
            QuickPrompts.Add("🧩 Kiểm tra trạng thái Steam Plugin");
            QuickPrompts.Add("💻 Chẩn đoán hệ thống phần cứng");
        }
        else
        {
            QuickPrompts.Add("🎮 How to Add & Download games");
            QuickPrompts.Add("🔍 Diagnose game launch & crash issues");
            QuickPrompts.Add("🛠️ How to use Game Fixes");
            QuickPrompts.Add("🧩 Check Steam Plugin status");
            QuickPrompts.Add("💻 System hardware diagnostics");
        }
    }

    private string GetWelcomeMessage()
    {
        string userName = GetEffectiveUserName();
        string nameSuffix = !string.IsNullOrWhiteSpace(userName) ? $" {userName}" : "";

        return IsVietnameseCulture()
            ? $"👋 Xin chào{nameSuffix}! Tôi là Trợ lý Kỹ thuật & Chẩn đoán BaoTools.\n\nTôi có thể giúp bạn chẩn đoán cấu hình, kiểm tra game, gỡ lỗi crash và sửa lỗi hoàn toàn tự động.\n\nHãy chọn gợi ý bên dưới hoặc gửi yêu cầu cho tôi nhé!"
            : $"👋 Hello{nameSuffix}! I am the BaoTools Technical & Diagnostic Copilot.\n\nI can help you diagnose system specs, verify games, resolve crash errors, and apply safe fixes automatically.\n\nPick a quick action below or ask me anything!";
    }

    [RelayCommand]
    private void ToggleOpen()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            if (Messages.Count <= 1)
            {
                RequestScrollToTop?.Invoke();
            }
            else
            {
                RequestScrollToBottom?.Invoke();
            }
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        string clearedMsg = IsVietnameseCulture()
            ? "Đã làm mới cuộc trò chuyện. Bạn cần mình hỗ trợ điều gì nè? Hãy nhập câu hỏi hoặc chọn các gợi ý bên dưới nhé!"
            : "Chat conversation refreshed. How can I help you? Please enter your question or pick a topic below!";

        AddBotMessage(clearedMsg);
        OnPropertyChanged(nameof(ShowSuggestions));
        RequestScrollToTop?.Invoke();
    }

    [RelayCommand]
    private async Task CopyMessageAsync(AiChatMessage? message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Text)) return;
        try
        {
            Clipboard.SetText(message.Text);
            message.IsCopied = true;
            await Task.Delay(2000);
            message.IsCopied = false;
        }
        catch { }
    }



    [RelayCommand]
    private async Task SendQuickPromptAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        InputText = prompt;
        await SendMessageAsync();
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        string text = InputText?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text) || IsLoading) return;

        InputText = "";
        StatusMessage = null;

        var userMsg = new AiChatMessage
        {
            Role = "user",
            Text = text,
            Timestamp = DateTime.Now
        };
        Messages.Add(userMsg);
        OnPropertyChanged(nameof(ShowSuggestions));
        RequestScrollToBottom?.Invoke();

        IsLoading = true;
        UpdateStatusLabel();
        CurrentProgressStep = IsVietnameseCulture() ? "Đang phân tích yêu cầu..." : "Analyzing request...";

        try
        {
            var history = Messages.Take(Messages.Count - 1).ToList();
            var replyMessage = await _ai.SendMessageFullAsync(
                history,
                text,
                onProgressStep: step =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentProgressStep = step;
                    });
                },
                selectedAppId: ActiveContextGame?.AppId);

            Messages.Add(replyMessage);
            if (!IsOpen)
            {
                UnreadCount++;
            }
        }
        catch (Exception ex)
        {
            string errorPrefix = IsVietnameseCulture() ? "Lỗi: " : "Error: ";
            AddBotMessage($"{errorPrefix}{ex.Message}");
        }
        finally
        {
            IsLoading = false;
            UpdateStatusLabel();
            CurrentProgressStep = "";
            RequestScrollToBottom?.Invoke();
        }
    }

    [RelayCommand]
    private async Task ExecuteActionAsync(AiChatAction action)
    {
        if (action == null || action.IsExecuted) return;

        bool isVi = IsVietnameseCulture();

        // 1. Navigation actions
        if (action.ToolName.StartsWith("navigate_"))
        {
            NavigateToPage(action.ToolName);
            action.IsExecuted = true;
            return;
        }

        // 2. Destructive confirmation dialog
        if (action.IsDestructive)
        {
            string confirmTitle = isVi ? "Xác nhận thực hiện" : "Confirm Action";
            string confirmText = isVi
                ? $"Bạn có chắc chắn muốn thực hiện hành động này không:\n{action.Label}?"
                : $"Are you sure you want to execute:\n{action.Label}?";

            var result = MessageBox.Show(confirmText, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        action.IsExecuted = true;
        IsLoading = true;
        UpdateStatusLabel();
        CurrentProgressStep = isVi ? "Đang thực thi tác vụ..." : "Executing action...";

        try
        {
            var ctx = BaoToolsContext.CollectLive(null, null, _settings);
            var res = await _ai.ToolExecutor.ExecuteToolDirectAsync(action.ToolName, action.Arguments, ctx);

            var botReply = new AiChatMessage
            {
                Role = "model",
                Text = res.Output,
                DiagnosisTitle = res.DiagnosisTitle,
                Checklist = res.Checklist,
                ActionButtons = res.ActionButtons
            };
            Messages.Add(botReply);
        }
        catch (Exception ex)
        {
            AddBotMessage($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            UpdateStatusLabel();
            CurrentProgressStep = "";
            RequestScrollToBottom?.Invoke();
        }
    }

    private void NavigateToPage(string target)
    {
        var mainWin = Application.Current.MainWindow as MainWindow;
        if (mainWin == null) return;

        switch (target)
        {
            case "navigate_home":
                mainWin.NavigateToHome();
                break;
            case "navigate_manage":
                mainWin.NavigateToManage();
                break;
            case "navigate_plugin":
                mainWin.NavigateToPlugin();
                break;
            case "navigate_fixes":
                mainWin.NavigateToFixes();
                break;
            case "navigate_add":
                mainWin.NavigateToAdd();
                break;
            case "navigate_mode":
                mainWin.NavigateToMode();
                break;
            case "navigate_settings":
                mainWin.NavigateToSettings();
                break;
            case "navigate_downloads":
                mainWin.NavigateToDownloads();
                break;
            case "navigate_builds":
                mainWin.NavigateToBuilds();
                break;
        }
    }

    private void AddBotMessage(string text)
    {
        Messages.Add(new AiChatMessage
        {
            Role = "model",
            Text = text,
            Timestamp = DateTime.Now
        });
        RequestScrollToBottom?.Invoke();
    }
}
