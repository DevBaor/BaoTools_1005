using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BaoToolsGui.AI.Knowledge.Models;
using BaoToolsGui.Services.Ai;

namespace BaoToolsGui.AI.Discovery;

/// <summary>
/// Scans the real BaoTools codebase and models to produce a grounded, verifiable system inventory.
/// Designed for build-time or developer CLI generation.
/// </summary>
public static class CodebaseScanner
{
    public static SystemManifest ScanSystem()
    {
        var manifest = new SystemManifest
        {
            Application = "BaoTools",
            Version = "105.6",
            Description = "BaoTools Professional Steam Game Management & Optimization Toolkit",
            GeneratedAt = DateTime.UtcNow
        };

        manifest.Screens = ScanScreens();
        manifest.Settings = ScanSettings();
        manifest.Tools = ScanTools();
        manifest.Commands = ScanCommands();
        manifest.Services = ScanServices();
        manifest.Features = ScanFeatures();
        manifest.Workflows = ScanWorkflows();
        manifest.Limitations = ScanLimitations();

        return manifest;
    }

    public static List<ScreenItem> ScanScreens()
    {
        return new List<ScreenItem>
        {
            new()
            {
                Id = "screen_home",
                NameVi = "Trang chủ (Home)",
                NameEn = "Home Page",
                PurposeVi = "Vùng kéo thả nạp nhanh file .lua, .manifest, .zip; hiển thị game vừa thêm và trạng thái hệ thống.",
                PurposeEn = "Drag-and-drop drop zone for .lua, .manifest, .zip; displays recently added games and system overview.",
                SourceFile = "HomeView.xaml",
                NavigationRoute = "MainWindow -> HomeView",
                SidebarIcon = "Home24",
                Sections = new() { "Drop Zone", "Recent Games", "Status Tiles" },
                PrimaryCommands = new() { "InstallDroppedItemCommand", "BrowseFilesCommand" },
                RelatedFeatures = new() { "home_injection", "game_management" }
            },
            new()
            {
                Id = "screen_add",
                NameVi = "Thêm game (Add / Download)",
                NameEn = "Add Game (Download Catalog)",
                PurposeVi = "Tìm kiếm và nạp game mới từ kho lưu trữ trực tuyến vào Steam.",
                PurposeEn = "Search and inject new games from online catalog into Steam.",
                SourceFile = "DownloadView.xaml",
                NavigationRoute = "MainWindow -> DownloadView",
                SidebarIcon = "Add24",
                Sections = new() { "Search Bar", "Featured Grid", "Catalog List", "Filter Tags" },
                PrimaryCommands = new() { "SearchCommand", "AddGameCommand", "DirectDownloadCommand" },
                RelatedFeatures = new() { "fast_downloads", "home_injection" }
            },
            new()
            {
                Id = "screen_manage",
                NameVi = "Quản lý game (Manage)",
                NameEn = "Manage Games",
                PurposeVi = "Quản lý danh sách game đã nạp: Gỡ Steam DRM (Steamless), mở thư mục, đặt Launch Options, xóa cấu hình .lua.",
                PurposeEn = "Manage unlocked games: Remove Steam DRM (Steamless), open folder, configure Launch Options, delete .lua config.",
                SourceFile = "ManageView.xaml",
                NavigationRoute = "MainWindow -> ManageView",
                SidebarIcon = "Games24",
                Sections = new() { "Game Grid/List", "Detail Sidebar", "Depots & DLC List", "Action Bar" },
                PrimaryCommands = new() { "RemoveDrmCommand", "OpenFolderCommand", "DeleteGameCommand", "ConfigureLaunchOptionsCommand", "ManageBuildCommand" },
                RelatedFeatures = new() { "game_management", "steamless_drm", "launch_options" }
            },
            new()
            {
                Id = "screen_builds",
                NameVi = "Bản dựng (Builds)",
                NameEn = "Game Builds & Depots",
                PurposeVi = "Xem chi tiết manifests, các nhánh build (branches) và depots của từng tựa game và DLC.",
                PurposeEn = "Inspect manifests, build branches, and depot files for games and DLCs.",
                SourceFile = "BuildsView.xaml",
                NavigationRoute = "MainWindow -> BuildsView",
                SidebarIcon = "Database24",
                Sections = new() { "Builds Sidebar", "Depot Manifest List", "Branch Selector", "Download Manifest Action" },
                PrimaryCommands = new() { "DownloadManifestCommand", "SelectBranchCommand" },
                RelatedFeatures = new() { "depot_builds", "game_management" }
            },
            new()
            {
                Id = "screen_mode",
                NameVi = "Chế độ Unlocker (Modes)",
                NameEn = "Unlocker Modes",
                PurposeVi = "Lựa chọn công cụ mở khóa backend: BetterSteamTools (BST), OpenSteamTools (OST) hoặc cấu hình Custom.",
                PurposeEn = "Select backend unlocker engine: BetterSteamTools (BST), OpenSteamTools (OST), or Custom setup.",
                SourceFile = "ModeView.xaml",
                NavigationRoute = "MainWindow -> ModeView",
                SidebarIcon = "Options24",
                Sections = new() { "Active Mode Badge", "Mode Selection Cards", "Migration Status" },
                PrimaryCommands = new() { "SelectModeCommand", "MigrateModeCommand" },
                RelatedFeatures = new() { "unlocker_modes" }
            },
            new()
            {
                Id = "screen_fixes",
                NameVi = "Bản sửa lỗi (Fixes)",
                NameEn = "Game Fixes",
                PurposeVi = "Kho bản vá lỗi crash game, thiếu DLL, gỡ DRM Steamless, sửa màn hình đen.",
                PurposeEn = "Repository of crash fixes, missing DLL patches, Steamless DRM unpacks, and black screen fixes.",
                SourceFile = "FixesView.xaml",
                NavigationRoute = "MainWindow -> FixesView",
                SidebarIcon = "Wrench24",
                Sections = new() { "Search Fixes", "Fix Category Filter", "Fix Details Markdown", "Apply/Revert Actions" },
                PrimaryCommands = new() { "ApplyFixCommand", "RevertFixCommand", "FilterMyGamesCommand" },
                RelatedFeatures = new() { "game_fixes", "steamless_drm" }
            },
            new()
            {
                Id = "screen_online_fixes",
                NameVi = "Chơi Online (Online Fixes)",
                NameEn = "Online Multiplayer Fixes",
                PurposeVi = "Tích hợp bản patch Online-Fix cho phép kết nối chơi co-op và multiplayer qua Steam Spacewar.",
                PurposeEn = "Integrated Online-Fix patches allowing multiplayer/co-op via Steam Spacewar emulation.",
                SourceFile = "OnlineFixesView.xaml",
                NavigationRoute = "MainWindow -> OnlineFixesView",
                SidebarIcon = "Globe24",
                Sections = new() { "Online Fix List", "Installation Guide", "Apply Action" },
                PrimaryCommands = new() { "ApplyOnlineFixCommand" },
                RelatedFeatures = new() { "online_coop_fixes" }
            },
            new()
            {
                Id = "screen_tickets",
                NameVi = "Vé Steam (Tickets)",
                NameEn = "Steam Tickets",
                PurposeVi = "Quản lý và kích hoạt vé bảo mật DRM của Steam (Steam App Tickets).",
                PurposeEn = "Manage and inject Steam DRM App Tickets.",
                SourceFile = "TicketsView.xaml",
                NavigationRoute = "MainWindow -> TicketsView",
                SidebarIcon = "TicketDiagonal24",
                Sections = new() { "Ticket Status List", "Inject Ticket Button" },
                PrimaryCommands = new() { "InjectTicketCommand", "RefreshTicketsCommand" },
                RelatedFeatures = new() { "steam_tickets" }
            },
            new()
            {
                Id = "screen_plugin",
                NameVi = "Steam Plugin",
                NameEn = "Steam Store Plugin",
                PurposeVi = "Cài đặt và quản lý Millennium Plugin trên Steam Store để thêm game trực tiếp 1-click từ trình duyệt.",
                PurposeEn = "Install and manage Millennium Plugin on Steam Store for 1-click game additions from web browser.",
                SourceFile = "PluginView.xaml",
                NavigationRoute = "MainWindow -> PluginView",
                SidebarIcon = "PuzzlePiece24",
                Sections = new() { "Status Card", "Installation Steps", "Install/Uninstall Action Buttons" },
                PrimaryCommands = new() { "InstallPluginCommand", "UninstallPluginCommand", "RestartSteamCommand" },
                RelatedFeatures = new() { "steam_plugin" }
            },
            new()
            {
                Id = "screen_downloads",
                NameVi = "Hàng đợi tải game (Downloads)",
                NameEn = "Downloads Queue",
                PurposeVi = "Theo dõi tiến độ, tốc độ, tạm dừng hoặc huỷ các tác vụ tải game và depot.",
                PurposeEn = "Track download progress, speed, pause, or cancel active game and depot download jobs.",
                SourceFile = "DownloadsView.xaml",
                NavigationRoute = "MainWindow -> DownloadsView",
                SidebarIcon = "ArrowDownload24",
                Sections = new() { "Active Queue", "Speed Meter", "Completed Items" },
                PrimaryCommands = new() { "PauseDownloadCommand", "ResumeDownloadCommand", "CancelDownloadCommand" },
                RelatedFeatures = new() { "fast_downloads" }
            },
            new()
            {
                Id = "screen_settings",
                NameVi = "Cài đặt (Settings)",
                NameEn = "Settings",
                PurposeVi = "Cấu hình tài khoản, thư mục Steam, Hubcap API, giao diện (Theme), khởi động và Trợ lý AI.",
                PurposeEn = "Configure account, Steam directory, Hubcap API, UI Theme, startup behavior, and AI Assistant.",
                SourceFile = "SettingsView.xaml",
                NavigationRoute = "MainWindow -> SettingsView (Footer)",
                SidebarIcon = "Settings24",
                Sections = new() { "Account", "Steam Path", "Hubcap API", "Appearance / Theme", "Startup & Tray", "AI Assistant", "Community" },
                PrimaryCommands = new() { "ChangeSteamFolderCommand", "ResetSteamFolderCommand", "SignInDiscordCommand", "SignOutCommand" },
                RelatedFeatures = new() { "settings_configuration", "ai_assistant" }
            },
            new()
            {
                Id = "screen_ai_assistant",
                NameVi = "Trợ lý AI BaoTools (AiChatWidget)",
                NameEn = "BaoTools AI Assistant",
                PurposeVi = "Trợ lý kỹ thuật floating hỗ trợ chẩn đoán cấu hình, kiểm tra lỗi crash, gỡ game, tối ưu FPS và hướng dẫn sử dụng.",
                PurposeEn = "Floating technical copilot for hardware diagnosis, crash troubleshooting, uninstallation, FPS optimization, and usage help.",
                SourceFile = "AiChatWidget.xaml",
                NavigationRoute = "Floating widget on bottom-right corner of MainWindow",
                SidebarIcon = "Bot / Sparkle",
                Sections = new() { "Chat Header", "Messages Scroll", "Suggested Topics", "Context Chip", "Input Bar" },
                PrimaryCommands = new() { "SendMessageCommand", "ClearChatCommand", "CopyMessageCommand", "ClearActiveContextGameCommand" },
                RelatedFeatures = new() { "ai_assistant" }
            }
        };
    }

    public static List<SettingItem> ScanSettings()
    {
        return new List<SettingItem>
        {
            new()
            {
                Id = "setting_steam_path",
                Property = "SteamPathOverride",
                NameVi = "Thư mục cài đặt Steam",
                NameEn = "Steam Installation Folder",
                PurposeVi = "Chỉ định thủ công đường dẫn thư mục Steam nếu hệ thống không tự phát hiện được qua Registry.",
                PurposeEn = "Manually specify Steam folder path if registry auto-detection fails.",
                Type = "string?",
                DefaultValue = "Auto-detected from Windows Registry",
                LocationVi = "Cài đặt (Settings) -> Mục Steam",
                LocationEn = "Settings -> Steam Section",
                AvailableValues = new() { "Valid Steam directory path containing steam.exe" }
            },
            new()
            {
                Id = "setting_language",
                Property = "Language",
                NameVi = "Ngôn ngữ giao diện",
                NameEn = "Interface Language",
                PurposeVi = "Chuyển đổi ngôn ngữ hiển thị toàn bộ giao diện BaoTools.",
                PurposeEn = "Switch display language across the entire BaoTools UI.",
                Type = "string?",
                DefaultValue = "Follow Windows Display Language",
                LocationVi = "Cài đặt (Settings) -> Mục Giao diện (Appearance)",
                LocationEn = "Settings -> Appearance Section",
                AvailableValues = new() { "en (English)", "vi (Tiếng Việt)", "zh-Hans", "zh-Hant", "ru", "es", "fr", "de", "ja", "ko", "pt-BR" }
            },
            new()
            {
                Id = "setting_theme",
                Property = "Theme",
                NameVi = "Chủ đề giao diện (Theme)",
                NameEn = "UI Theme",
                PurposeVi = "Chọn phối màu cho giao diện ứng dụng.",
                PurposeEn = "Select color palette for the app interface.",
                Type = "string?",
                DefaultValue = "Default (Dark Purple)",
                LocationVi = "Cài đặt (Settings) -> Mục Giao diện (Appearance)",
                LocationEn = "Settings -> Appearance Section",
                AvailableValues = new() { "Default", "Dracula", "Nord", "Catppuccin", "Cyberpunk", "Midnight" }
            },
            new()
            {
                Id = "setting_enable_ai_assistant",
                Property = "EnableAiAssistant",
                NameVi = "Bật Trợ lý AI BaoTools",
                NameEn = "Enable AI Assistant",
                PurposeVi = "Bật hoặc ẩn biểu tượng Trợ lý AI nổi ở góc phải dưới.",
                PurposeEn = "Toggle visibility of the floating AI Assistant on the bottom right.",
                Type = "bool?",
                DefaultValue = "true",
                LocationVi = "Cài đặt (Settings) -> Mục Trợ lý AI (AI Assistant)",
                LocationEn = "Settings -> AI Assistant Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_hubcap_api_key",
                Property = "HubcapApiKey",
                NameVi = "Mã API Hubcap Manifest",
                NameEn = "Hubcap Manifest API Key",
                PurposeVi = "Mã API dịch vụ Hubcap Manifest (hubcapmanifest.com) để mở khóa các nguồn tải depot riêng.",
                PurposeEn = "Hubcap Manifest service API key for accessing restricted depot sources.",
                Type = "string?",
                DefaultValue = "null",
                LocationVi = "Cài đặt (Settings) -> Mục Hubcap",
                LocationEn = "Settings -> Hubcap Section",
                AvailableValues = new() { "smm_..." }
            },
            new()
            {
                Id = "setting_auto_update_apps",
                Property = "AutoUpdateApps",
                NameVi = "Tự động cập nhật game qua Steam",
                NameEn = "Keep Games Updated via Steam",
                PurposeVi = "Khi bật, file .lua không ghim manifest cố định để Steam tự động cập nhật game lên phiên bản mới nhất.",
                PurposeEn = "When true, .lua configs avoid pinning manifests so Steam keeps games up-to-date.",
                Type = "bool?",
                DefaultValue = "true",
                LocationVi = "Cài đặt (Settings) -> Mục Cài đặt chung",
                LocationEn = "Settings -> General Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_donate_keys",
                Property = "DonateKeys",
                NameVi = "Đóng góp key giải mã Steam",
                NameEn = "Donate Steam Decryption Keys",
                PurposeVi = "Chia sẻ key giải mã game dư thừa ẩn danh vào kho dữ liệu chung của cộng đồng.",
                PurposeEn = "Anonymously contribute spare Steam decryption keys to the community pool.",
                Type = "bool?",
                DefaultValue = "true",
                LocationVi = "Cài đặt (Settings) -> Mục Cài đặt chung",
                LocationEn = "Settings -> General Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_start_with_windows",
                Property = "StartWithWindows",
                NameVi = "Khởi động cùng Windows",
                NameEn = "Start with Windows",
                PurposeVi = "Tự động chạy ứng dụng khi đăng nhập vào hệ điều hành Windows.",
                PurposeEn = "Automatically launch the application on Windows login.",
                Type = "bool?",
                DefaultValue = "false",
                LocationVi = "Cài đặt (Settings) -> Mục Khởi động (Startup)",
                LocationEn = "Settings -> Startup Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_start_with_steam",
                Property = "StartWithSteam",
                NameVi = "Khởi động cùng Steam",
                NameEn = "Start with Steam",
                PurposeVi = "Chạy BaoTools ẩn dưới khay hệ thống mỗi khi Steam được mở thông qua winmm.dll loader.",
                PurposeEn = "Silently launch BaoTools into tray when Steam starts via winmm.dll loader.",
                Type = "bool?",
                DefaultValue = "false",
                LocationVi = "Cài đặt (Settings) -> Mục Khởi động (Startup)",
                LocationEn = "Settings -> Startup Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_minimize_to_tray",
                Property = "MinimizeToTray",
                NameVi = "Thu nhỏ xuống khay hệ thống",
                NameEn = "Minimize to System Tray",
                PurposeVi = "Khi đóng hoặc thu nhỏ, ẩn ứng dụng vào System Tray thay vì taskbar.",
                PurposeEn = "Hides the app window into system tray instead of taskbar on close/minimize.",
                Type = "bool?",
                DefaultValue = "false",
                LocationVi = "Cài đặt (Settings) -> Mục Khởi động & Khay",
                LocationEn = "Settings -> Startup & Tray Section",
                AvailableValues = new() { "true", "false" }
            },
            new()
            {
                Id = "setting_fast_fetch",
                Property = "FastFetch",
                NameVi = "Tự động chọn nguồn tải nhanh (FastFetch)",
                NameEn = "FastFetch Automatic Source",
                PurposeVi = "Tự động chọn nguồn tải nhanh nhất có sẵn và bắt đầu tải tức thì.",
                PurposeEn = "Automatically pick first available download source and start immediately.",
                Type = "bool?",
                DefaultValue = "false",
                LocationVi = "Cài đặt (Settings) -> Mục Tải game",
                LocationEn = "Settings -> Download Section",
                AvailableValues = new() { "true", "false" }
            }
        };
    }

    public static List<ToolItem> ScanTools()
    {
        var list = new List<ToolItem>();
        foreach (var kvp in AiToolRegistry.Tools)
        {
            var def = kvp.Value;
            list.Add(new ToolItem
            {
                Name = def.Name,
                DescriptionVi = GetToolDescriptionVi(def.Name, def.Description),
                DescriptionEn = def.Description,
                ReadOnly = def.Permission == ToolPermissionLevel.Safe && !def.Name.StartsWith("apply_") && !def.Name.StartsWith("repair_") && !def.Name.StartsWith("clear_") && !def.Name.StartsWith("launch_"),
                RequiresConfirmation = def.Permission == ToolPermissionLevel.DestructiveRequiresConfirmation,
                Parameters = def.Parameters.ToList(),
                RelatedFeatures = GetToolFeatures(def.Name),
                RelatedIntents = GetToolIntents(def.Name),
                SourceFile = "AiToolDefinition.cs"
            });
        }
        return list;
    }

    private static string GetToolDescriptionVi(string toolName, string fallbackEn)
    {
        return toolName switch
        {
            "get_selected_game" => "Đọc thông tin game hiện đang được chọn trong thư viện BaoTools.",
            "get_game_info" => "Tra cứu chi tiết cấu hình và cấu trúc tệp của game theo AppID.",
            "get_game_path" => "Xác định đường dẫn cài đặt game trong thư mục Steam common.",
            "get_installed_games" => "Liệt kê danh sách tất cả các game đã nạp trong thư viện BaoTools.",
            "check_game_files" => "Kiểm tra file thực thi (.exe), DLC và depot của game có đầy đủ không.",
            "scan_game_dependencies" => "Quét các runtime phụ thuộc cần thiết (DirectX, VC++ Redistributable, .NET).",
            "check_directx" => "Kiểm tra phiên bản DirectX và Direct3D feature level của máy.",
            "check_vcredist" => "Kiểm tra phiên bản Visual C++ 2015-2022 (x64 và x86) đã cài chưa.",
            "get_gpu_info" => "Đọc thông tin card màn hình (GPU), VRAM và phiên bản Driver.",
            "get_system_info" => "Đọc toàn bộ cấu hình phần cứng OS, CPU, RAM, GPU của máy tính.",
            "diagnose_game" => "Chạy quy trình chẩn đoán tự động toàn diện về đường dẫn, Steam DRM, runtime và patch.",
            "get_plugin_status" => "Kiểm tra tình trạng hoạt động của Steam Store Plugin.",
            "get_download_status" => "Kiểm tra hàng đợi tải và giới hạn quota tải trong ngày.",
            "verify_game_files" => "Xác minh tính toàn vẹn của tệp game và kiểm tra tệp depot bị thiếu.",
            "repair_game_configuration" => "Sửa chữa cấu hình .lua hoặc cấu hình unlocker bị lỗi.",
            "clear_safe_cache" => "Dọn dẹp bộ nhớ đệm tạm thời an toàn (manifests và fastfetch cache).",
            "apply_game_fix" => "Áp dụng bản vá gỡ DRM Steamless hoặc patch sửa lỗi tương thích.",
            "launch_game" => "Khởi động game thông qua giao thức Steam (steam://run/{appId}).",
            "delete_game" => "Xóa cấu hình game khỏi thư viện BaoTools (yêu cầu xác nhận).",
            "remove_plugin" => "Gỡ cài đặt hoàn toàn plugin BaoTools khỏi Steam (yêu cầu xác nhận).",
            _ => fallbackEn
        };
    }

    private static List<string> GetToolFeatures(string toolName)
    {
        if (toolName.Contains("plugin")) return new() { "steam_plugin" };
        if (toolName.Contains("download")) return new() { "fast_downloads" };
        if (toolName.Contains("fix") || toolName.Contains("repair")) return new() { "game_fixes", "steamless_drm" };
        if (toolName.Contains("gpu") || toolName.Contains("directx") || toolName.Contains("vcredist") || toolName.Contains("system")) return new() { "system_diagnostics" };
        return new() { "game_management" };
    }

    private static List<string> GetToolIntents(string toolName)
    {
        if (toolName.Contains("plugin")) return new() { "PLUGIN" };
        if (toolName.Contains("download")) return new() { "DOWNLOAD" };
        if (toolName.Contains("gpu") || toolName.Contains("system") || toolName.Contains("vcredist") || toolName.Contains("directx")) return new() { "SYSTEM_DIAGNOSTIC", "PERFORMANCE" };
        if (toolName.Contains("fix") || toolName.Contains("diagnose")) return new() { "GAME_DIAGNOSTIC", "GAME_FIX" };
        return new() { "GAME_CONFIGURATION", "GAME_COMPATIBILITY" };
    }

    public static List<CommandItem> ScanCommands()
    {
        return new List<CommandItem>
        {
            new() { Name = "RestartSteamCommand", OwnerViewModel = "MainViewModel", PurposeVi = "Khởi động lại tiến trình Steam để áp dụng cấu hình game hoặc plugin.", PurposeEn = "Restarts Steam process to apply new game configs or plugins.", IsMutating = true, SourceFile = "MainViewModel.cs" },
            new() { Name = "SignInCommand", OwnerViewModel = "MainViewModel", PurposeVi = "Đăng nhập tài khoản Discord để đồng bộ quyền lợi và hạn mức.", PurposeEn = "Signs in via Discord to sync privileges and download quotas.", IsMutating = false, SourceFile = "MainViewModel.cs" },
            new() { Name = "SignOutCommand", OwnerViewModel = "MainViewModel", PurposeVi = "Đăng xuất tài khoản người dùng về chế độ khách (Guest).", PurposeEn = "Signs out current user to Guest mode.", IsMutating = true, SourceFile = "MainViewModel.cs" },
            new() { Name = "RemoveDrmCommand", OwnerViewModel = "ManageViewModel", PurposeVi = "Chạy Steamless giải mã file .exe gốc để gỡ Steam DRM chống văng crash.", PurposeEn = "Runs Steamless unpacker on game exe to strip Steam DRM and prevent crashes.", IsMutating = true, SourceFile = "ManageViewModel.cs" },
            new() { Name = "DeleteGameCommand", OwnerViewModel = "ManageViewModel", PurposeVi = "Xóa cấu hình .lua của game để gỡ game khỏi Steam.", PurposeEn = "Deletes game .lua config to unlink game from Steam.", IsMutating = true, RequiresConfirmation = true, SourceFile = "ManageViewModel.cs" },
            new() { Name = "OpenFolderCommand", OwnerViewModel = "ManageViewModel", PurposeVi = "Mở thư mục cài đặt thực tế của game trên ổ cứng trong Windows Explorer.", PurposeEn = "Opens game installation folder on disk in Windows Explorer.", IsMutating = false, SourceFile = "ManageViewModel.cs" },
            new() { Name = "ConfigureLaunchOptionsCommand", OwnerViewModel = "ManageViewModel", PurposeVi = "Mở hộp thoại đặt tham số khởi chạy game (-dx11, -novid, v.v.).", PurposeEn = "Opens dialog to set game launch options (-dx11, -novid, etc.).", IsMutating = true, SourceFile = "ManageViewModel.cs" },
            new() { Name = "InstallPluginCommand", OwnerViewModel = "PluginViewModel", PurposeVi = "Cài đặt Millennium Loader và BaoTools Plugin vào Steam.", PurposeEn = "Installs Millennium Loader and BaoTools Plugin into Steam.", IsMutating = true, SourceFile = "PluginViewModel.cs" },
            new() { Name = "UninstallPluginCommand", OwnerViewModel = "PluginViewModel", PurposeVi = "Gỡ bỏ hoàn toàn Millennium Loader và BaoTools Plugin khỏi Steam.", PurposeEn = "Completely uninstalls Millennium Loader and BaoTools Plugin from Steam.", IsMutating = true, RequiresConfirmation = true, SourceFile = "PluginViewModel.cs" },
            new() { Name = "ApplyFixCommand", OwnerViewModel = "FixesViewModel", PurposeVi = "Cài đặt bản vá lỗi crash hoặc thiếu file DLL vào thư mục game.", PurposeEn = "Applies crash fix or missing DLL patch into game folder.", IsMutating = true, SourceFile = "FixesViewModel.cs" },
            new() { Name = "RevertFixCommand", OwnerViewModel = "FixesViewModel", PurposeVi = "Gỡ bỏ bản vá lỗi và khôi phục lại các file gốc nguyên bản của game.", PurposeEn = "Reverts applied fix and restores original pristine game files.", IsMutating = true, SourceFile = "FixesViewModel.cs" },
            new() { Name = "ChangeSteamFolderCommand", OwnerViewModel = "SettingsViewModel", PurposeVi = "Mở hộp thoại chọn thư mục Steam thủ công trên máy.", PurposeEn = "Opens folder dialog to pick custom Steam folder path.", IsMutating = true, SourceFile = "SettingsViewModel.cs" },
            new() { Name = "ClearChatCommand", OwnerViewModel = "AiChatViewModel", PurposeVi = "Làm mới toàn bộ cuộc trò chuyện với Trợ lý AI.", PurposeEn = "Refreshes the conversation with the AI Assistant.", IsMutating = false, SourceFile = "AiChatViewModel.cs" },
            new() { Name = "CopyMessageCommand", OwnerViewModel = "AiChatViewModel", PurposeVi = "Sao chép nội dung tin nhắn bot vào Clipboard.", PurposeEn = "Copies bot message text to clipboard.", IsMutating = false, SourceFile = "AiChatViewModel.cs" }
        };
    }

    public static List<ServiceItem> ScanServices()
    {
        return new List<ServiceItem>
        {
            new() { Name = "SteamService", ResponsibilityVi = "Quản lý tiến trình Steam, phát hiện đường dẫn cài đặt Steam, đọc thư viện Steam và khởi động lại Steam.", ResponsibilityEn = "Manages Steam process, detects Steam installation path, reads Steam libraries, and restarts Steam.", SourceFile = "SteamService.cs" },
            new() { Name = "LuaVault", ResponsibilityVi = "Quản lý kho tệp cấu hình .lua của các game đã nạp trong BaoTools.", ResponsibilityEn = "Manages repository of .lua game configuration files in BaoTools.", SourceFile = "LuaVault.cs" },
            new() { Name = "SteamlessService", ResponsibilityVi = "Giải mã lớp bảo vệ SteamStub DRM trên tệp thực thi .exe của game để chống văng lúc khởi động.", ResponsibilityEn = "Unpacks SteamStub DRM on game executables to prevent launch crashes.", SourceFile = "SteamlessService.cs" },
            new() { Name = "UnlockerService", ResponsibilityVi = "Điều phối các công cụ mở khóa backend (BetterSteamTools, OpenSteamTools, SmokeAPI, Koaloader).", ResponsibilityEn = "Orchestrates backend unlocker engines (BetterSteamTools, OpenSteamTools, SmokeAPI, Koaloader).", SourceFile = "UnlockerService.cs" },
            new() { Name = "PluginInstallerService", ResponsibilityVi = "Tự động cài đặt, gỡ bỏ và cập nhật Millennium loader và BaoTools Plugin cho Steam Store.", ResponsibilityEn = "Installs, uninstalls, and updates Millennium loader and BaoTools Plugin for Steam Store.", SourceFile = "PluginInstallerService.cs" },
            new() { Name = "SettingsService", ResponsibilityVi = "Lưu trữ và đồng bộ an toàn các thiết lập cấu hình của ứng dụng trong %AppData%/BaoToolsGui/settings.json.", ResponsibilityEn = "Stores and synchronizes app configuration settings in %AppData%/BaoToolsGui/settings.json.", SourceFile = "SettingsService.cs" },
            new() { Name = "AiChatService", ResponsibilityVi = "Điều phối cuộc trò chuyện giữa giao diện Chat, mô hình chuyên gia cục bộ LocalExpert và kho tri thức SystemKnowledgeStore.", ResponsibilityEn = "Orchestrates chat conversations between Chat UI, LocalExpert engine, and SystemKnowledgeStore.", SourceFile = "AiChatService.cs" },
            new() { Name = "AiToolExecutor", ResponsibilityVi = "Thực thi an toàn các công cụ chẩn đoán phần cứng, quét lỗi game và thao tác hệ thống theo whitelist.", ResponsibilityEn = "Safely executes hardware diagnostics, game crash scanning, and whitelisted system actions.", SourceFile = "AiToolExecutor.cs" }
        };
    }

    public static List<FeatureItem> ScanFeatures()
    {
        return new List<FeatureItem>
        {
            new()
            {
                Id = "home_injection",
                NameVi = "Nạp game siêu tốc (Drop Zone)",
                NameEn = "Fast Game Injection (Drop Zone)",
                DescriptionVi = "Kéo thả file .lua, .manifest hoặc .zip vào Trang chủ để tự động nạp cấu hình bản quyền vào Steam.",
                DescriptionEn = "Drag and drop .lua, .manifest, or .zip into Home to automatically inject licenses into Steam.",
                Keywords = new() { "nạp game", "kéo thả", "drop zone", "thêm game", "inject", "lua", "zip" },
                RelatedScreens = new() { "screen_home" },
                RelatedCommands = new() { "InstallDroppedItemCommand" },
                SourceReferences = new() { new() { File = "HomeView.xaml" }, new() { File = "LuaVault.cs" } }
            },
            new()
            {
                Id = "game_management",
                NameVi = "Quản lý Game đã nạp (Manage)",
                NameEn = "Unlocked Games Management",
                DescriptionVi = "Xem danh sách game, lọc game đã cài, mở thư mục game, đặt Launch Options (-dx11, -novid) và xóa game.",
                DescriptionEn = "View game list, filter installed titles, open game folders, set Launch Options (-dx11, -novid), and delete games.",
                Keywords = new() { "quản lý", "manage", "danh sách game", "mở thư mục", "launch options", "xóa game" },
                RelatedScreens = new() { "screen_manage" },
                RelatedCommands = new() { "OpenFolderCommand", "DeleteGameCommand", "ConfigureLaunchOptionsCommand" },
                SourceReferences = new() { new() { File = "ManageView.xaml" }, new() { File = "ManageViewModel.cs" } }
            },
            new()
            {
                Id = "steamless_drm",
                NameVi = "Gỡ Steam DRM (Steamless)",
                NameEn = "Remove Steam DRM (Steamless)",
                DescriptionVi = "Tự động giải mã file .exe của game để gỡ bỏ lớp bảo vệ SteamStub DRM, khắc phục triệt để lỗi game mở lên bị văng.",
                DescriptionEn = "Automatically unpacks game .exe to strip SteamStub DRM, permanently solving launch crash issues.",
                Keywords = new() { "steamless", "gỡ drm", "văng", "crash", "steamstub", "không mở được" },
                RelatedScreens = new() { "screen_manage", "screen_fixes" },
                RelatedCommands = new() { "RemoveDrmCommand" },
                RelatedTools = new() { "apply_game_fix" },
                RelatedServices = new() { "SteamlessService" },
                SourceReferences = new() { new() { File = "SteamlessService.cs" } }
            },
            new()
            {
                Id = "steam_plugin",
                NameVi = "Steam Store Plugin (Millennium)",
                NameEn = "Steam Store Plugin (Millennium)",
                DescriptionVi = "Tích hợp nút bấm nạp game 1-click trực tiếp trên giao diện web Steam Store thông qua Millennium.",
                DescriptionEn = "Integrates 1-click game addition button directly on Steam Store web interface via Millennium.",
                Keywords = new() { "plugin", "millennium", "steam store", "cài plugin", "thêm 1-click" },
                RelatedScreens = new() { "screen_plugin" },
                RelatedCommands = new() { "InstallPluginCommand", "UninstallPluginCommand" },
                RelatedTools = new() { "get_plugin_status", "remove_plugin" },
                RelatedServices = new() { "PluginInstallerService" },
                SourceReferences = new() { new() { File = "PluginInstallerService.cs" } }
            },
            new()
            {
                Id = "fast_downloads",
                NameVi = "Tải game tốc độ cao (Downloads)",
                NameEn = "High-Speed Game Downloads",
                DescriptionVi = "Tải dữ liệu game và depot tốc độ cao trực tiếp từ kho lưu trữ và tự động giải nén vào thư mục Steam.",
                DescriptionEn = "High-speed direct game and depot downloads automatically extracted into Steam library.",
                Keywords = new() { "tải game", "download", "hàng đợi", "depot", "tốc độ cao" },
                RelatedScreens = new() { "screen_add", "screen_downloads" },
                RelatedCommands = new() { "AddGameCommand", "PauseDownloadCommand" },
                RelatedTools = new() { "get_download_status" },
                SourceReferences = new() { new() { File = "DownloadView.xaml" }, new() { File = "DownloadsView.xaml" } }
            },
            new()
            {
                Id = "game_fixes",
                NameVi = "Kho Bản sửa lỗi (Fixes)",
                NameEn = "Game Fixes & Patches",
                DescriptionVi = "Kho tổng hợp các bản patch sửa lỗi crash, lỗi thiếu file DLL, lỗi DirectX và fix màn hình đen cho từng tựa game.",
                DescriptionEn = "Comprehensive repository of crash fixes, missing DLLs, DirectX repairs, and black screen patches.",
                Keywords = new() { "fixes", "bản sửa lỗi", "patch", "thiếu file", "dll", "màn hình đen" },
                RelatedScreens = new() { "screen_fixes" },
                RelatedCommands = new() { "ApplyFixCommand", "RevertFixCommand" },
                RelatedTools = new() { "apply_game_fix" },
                SourceReferences = new() { new() { File = "FixesView.xaml" } }
            },
            new()
            {
                Id = "online_coop_fixes",
                NameVi = "Chơi Online Co-op (Online-Fix)",
                NameEn = "Online Co-op Multiplayer Fixes",
                DescriptionVi = "Bản patch giả lập Steam Spacewar để kết nối chơi nhiều người trực tuyến miễn phí.",
                DescriptionEn = "Online-Fix patches using Steam Spacewar emulation for free co-op and multiplayer gaming.",
                Keywords = new() { "online", "chơi online", "online-fix", "multiplayer", "co-op", "spacewar" },
                RelatedScreens = new() { "screen_online_fixes" },
                RelatedCommands = new() { "ApplyOnlineFixCommand" },
                SourceReferences = new() { new() { File = "OnlineFixesView.xaml" } }
            },
            new()
            {
                Id = "ai_assistant",
                NameVi = "Trợ lý AI Kỹ thuật BaoTools",
                NameEn = "BaoTools AI Technical Assistant",
                DescriptionVi = "Trợ lý kỹ thuật chẩn đoán phần cứng, kiểm tra lỗi game, hướng dẫn tính năng ứng dụng và đề xuất giải pháp sửa lỗi an toàn.",
                DescriptionEn = "Technical assistant diagnosing hardware specs, troubleshooting game errors, guiding features, and applying safe fixes.",
                Keywords = new() { "ai", "trợ lý", "bot", "chẩn đoán", "hỏi đáp", "hỗ trợ" },
                RelatedScreens = new() { "screen_ai_assistant" },
                RelatedCommands = new() { "SendMessageCommand", "ClearChatCommand", "CopyMessageCommand" },
                RelatedTools = new() { "diagnose_game", "get_system_info", "check_game_files" },
                RelatedServices = new() { "AiChatService", "AiToolExecutor" },
                SourceReferences = new() { new() { File = "AiChatWidget.xaml" }, new() { File = "AiChatService.cs" } }
            }
        };
    }

    public static List<WorkflowItem> ScanWorkflows()
    {
        return new List<WorkflowItem>
        {
            new()
            {
                Id = "workflow_inject_game",
                NameVi = "Quy trình nạp game mới vào Steam",
                NameEn = "Game Injection Workflow",
                SummaryVi = "Nạp bản quyền game thông qua cấu hình .lua để game xuất hiện trong Thư viện Steam.",
                SummaryEn = "Inject game license via .lua configuration so it appears in Steam Library.",
                StepsVi = new()
                {
                    "1. Chuẩn bị file .lua, .manifest hoặc file nén .zip của game.",
                    "2. Mở Trang chủ (Home) BaoTools, kéo và thả file vào ô Drop Zone.",
                    "3. Chờ BaoTools xử lý cấu hình bản quyền tự động.",
                    "4. Bấm 'Restart Steam' ở thanh trên cùng để nạp game vào Thư viện Steam."
                },
                StepsEn = new()
                {
                    "1. Prepare the game's .lua, .manifest, or .zip package.",
                    "2. Open BaoTools Home tab, drag and drop the file into the Drop Zone.",
                    "3. Wait for BaoTools to process and inject the license.",
                    "4. Click 'Restart Steam' on the top bar to refresh your Steam Library."
                },
                TargetScreen = "screen_home"
            },
            new()
            {
                Id = "workflow_remove_drm",
                NameVi = "Quy trình gỡ Steam DRM (Steamless) chống văng game",
                NameEn = "Steamless DRM Unpacking Workflow",
                SummaryVi = "Giải mã file .exe gốc để khắc phục lỗi game mở lên bị văng (crash) do Steam DRM.",
                SummaryEn = "Unpack the game's main .exe to strip Steam DRM and resolve launch crashes.",
                StepsVi = new()
                {
                    "1. Vào tab Quản lý (Manage) trên thanh menu bên trái.",
                    "2. Bấm chọn tựa game đang bị lỗi văng trong danh sách.",
                    "3. Tại cột chi tiết bên phải, bấm nút 'Gỡ Steam DRM' (Steamless).",
                    "4. BaoTools sẽ tự động sao lưu file gốc (.bak) và tạo file .exe đã giải mã.",
                    "5. Khởi động lại game qua Steam hoặc BaoTools."
                },
                StepsEn = new()
                {
                    "1. Open the Manage tab from the left sidebar.",
                    "2. Select the game that crashes on startup from the list.",
                    "3. In the right details panel, click 'Remove Steam DRM' (Steamless).",
                    "4. BaoTools creates a backup (.bak) and unpacks the clean executable.",
                    "5. Launch the game cleanly via Steam or BaoTools."
                },
                RequiredTools = new() { "apply_game_fix" },
                TargetScreen = "screen_manage"
            },
            new()
            {
                Id = "workflow_delete_game",
                NameVi = "Quy trình gỡ / xóa game khỏi Steam",
                NameEn = "Uninstall / Delete Game Workflow",
                SummaryVi = "Xóa cấu hình bản quyền .lua để loại bỏ game khỏi Steam và tuỳ chọn dọn sạch ổ cứng.",
                SummaryEn = "Delete .lua configuration to unlink game from Steam and optionally reclaim disk space.",
                StepsVi = new()
                {
                    "1. Mở tab Quản lý (Manage).",
                    "2. Chọn game cần xóa trong danh sách.",
                    "3. Bấm biểu tượng Thùng rác (Xóa game) ở cột chi tiết bên phải.",
                    "4. Bấm 'Restart Steam' để Steam gỡ game khỏi Thư viện.",
                    "5. Mẹo: Bấm 'Mở thư mục' để xóa các file cài đặt nặng trên ổ đĩa nếu cần giải phóng dung lượng."
                },
                StepsEn = new()
                {
                    "1. Open the Manage tab.",
                    "2. Select the game to delete from the list.",
                    "3. Click the Trash icon (Delete) in the right detail panel.",
                    "4. Click 'Restart Steam' to remove the game from your Steam Library.",
                    "5. Tip: Click 'Open Folder' to delete physical files if you want to reclaim storage."
                },
                TargetScreen = "screen_manage"
            },
            new()
            {
                Id = "workflow_override_steam_folder",
                NameVi = "Quy trình đổi đường dẫn thư mục Steam",
                NameEn = "Change Steam Folder Workflow",
                SummaryVi = "Chỉ định thủ công vị trí thư mục cài đặt Steam khi ứng dụng không tự nhận diện được.",
                SummaryEn = "Manually point BaoTools to your custom Steam installation directory.",
                StepsVi = new()
                {
                    "1. Vào tab Cài đặt (Settings) ở góc dưới thanh menu.",
                    "2. Tìm đến mục 'Steam'.",
                    "3. Bấm nút 'Thay đổi thư mục Steam' và trỏ tới thư mục chứa steam.exe (ví dụ: C:\\Program Files (x86)\\Steam).",
                    "4. BaoTools sẽ tự động kiểm tra và lưu lại thiết lập."
                },
                StepsEn = new()
                {
                    "1. Open the Settings tab in the bottom-left menu.",
                    "2. Navigate to the 'Steam' section.",
                    "3. Click 'Change Steam Folder' and select the directory containing steam.exe.",
                    "4. BaoTools validates and persists the new path automatically."
                },
                TargetScreen = "screen_settings"
            }
        };
    }

    public static List<LimitationItem> ScanLimitations()
    {
        return new List<LimitationItem>
        {
            new()
            {
                Id = "limit_daily_quota",
                DescriptionVi = "Người dùng miễn phí có hạn mức nạp/tải tối đa 10 game mỗi ngày.",
                DescriptionEn = "Free guest users have a daily limit of 10 game additions per day.",
                Scope = "Add / Download",
                WorkaroundVi = "Đăng nhập tài khoản Discord để mở rộng hạn mức hoặc chờ sang ngày hôm sau.",
                WorkaroundEn = "Sign in via Discord or wait until the next day."
            },
            new()
            {
                Id = "limit_steam_running_required",
                DescriptionVi = "Cần có Steam đã cài đặt trên máy để chơi và đồng bộ game.",
                DescriptionEn = "Steam must be installed on the host PC to sync and play games.",
                Scope = "All game operations",
                WorkaroundVi = "Cài đặt ứng dụng Steam chính thức từ store.steampowered.com.",
                WorkaroundEn = "Install official Steam client from store.steampowered.com."
            },
            new()
            {
                Id = "limit_admin_permission_fixes",
                DescriptionVi = "Một số bản sửa lỗi can thiệp vào thư mục Program Files cần quyền Administrator để ghi file.",
                DescriptionEn = "Certain game patches modifying Program Files directories require Administrator privileges.",
                Scope = "Game Fixes",
                WorkaroundVi = "Chạy BaoTools dưới quyền 'Run as Administrator'.",
                WorkaroundEn = "Launch BaoTools with 'Run as Administrator'."
            },
            new()
            {
                Id = "limit_offline_ai",
                DescriptionVi = "Trợ lý AI hoạt động 100% offline cục bộ trên thiết bị, bảo đảm quyền riêng tư tuyệt đối.",
                DescriptionEn = "The AI Assistant operates 100% locally offline on your device, ensuring complete privacy.",
                Scope = "AI Assistant",
                WorkaroundVi = "Trợ lý được nạp sẵn kho tri thức hệ thống toàn diện của BaoTools và bộ công cụ chẩn đoán chuyên sâu trực tiếp.",
                WorkaroundEn = "The assistant is pre-grounded with BaoTools' comprehensive system knowledge base and native diagnostic tools."
            }
        };
    }
}
