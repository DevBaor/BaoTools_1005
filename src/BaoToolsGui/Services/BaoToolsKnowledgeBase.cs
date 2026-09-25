using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BaoToolsGui.Services;

public class KnowledgeArticle
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string Topic
    {
        get => string.IsNullOrEmpty(_topic) ? Category : _topic;
        set => _topic = value;
    }
    private string _topic = "";

    public string TitleVi { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string ContentVi { get; set; } = "";
    public string ContentEn { get; set; } = "";
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string[] IntentHints { get; set; } = Array.Empty<string>();
    public string[] RelatedTools { get; set; } = Array.Empty<string>();
    public int Priority { get; set; } = 10;
}

public static class BaoToolsKnowledgeBase
{
    public static bool IsVietnameseQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

        string q = query.ToLowerInvariant();

        // 1. Check Vietnamese accented characters
        const string viDiacritics = "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ";
        foreach (char c in q)
        {
            if (viDiacritics.IndexOf(c) >= 0) return true;
        }

        // 2. Check common Vietnamese words and phrasing (even without diacritics)
        string[] viWords = new[]
        {
            "chao", "xin chao", "giup", "khong", " k ", "duoc", "dc ", "the nao", "sao the",
            "la gi", "o dau", "kiem tra", "chan doan", "khoi dong", "cai dat", "tai ve", "tai game",
            "xoa game", "vang game", "bi out", "chua v", "chua vay", "minh", "ban oi", "bot oi",
            "e bot", "cam on", "tieng viet", "bao tools", "dung the nao", "lam sao", "lam the nao"
        };

        foreach (var w in viWords)
        {
            if (q.Contains(w)) return true;
        }

        // 3. Fallback to App UI Culture
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
    }

    public static readonly List<KnowledgeArticle> Articles = new()
    {
        // ── 1. HOME VIEW ──────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "home_dropzone",
            Category = "Home",
            TitleVi = "Vùng kéo thả (Drop Zone) ở Trang chủ",
            TitleEn = "Drop Zone on Home Page",
            Keywords = new[] { "kéo thả", "drop zone", "lua", "manifest", "zip", "cài nhanh", "thả file", "drag drop" },
            ContentVi = "Vùng kéo thả (Drop Zone) ở Trang chủ cho phép bạn cài đặt game hoặc cấu hình siêu tốc:\n" +
                        "• Kéo & thả file .lua: Tự động nạp cấu hình mở khóa game vào Steam.\n" +
                        "• Kéo & thả file .manifest: Tự nạp thông tin tải depot vào thư mục depotcache của Steam.\n" +
                        "• Kéo & thả file .zip: Tự động giải nén và nạp toàn bộ cấu hình bên trong vào Steam.\n" +
                        "Sau khi thả file xong, chỉ cần khởi động lại Steam là game sẽ xuất hiện trong Thư viện.",
            ContentEn = "The Drop Zone on the Home page lets you quickly install game configurations:\n" +
                        "• Drag & drop .lua: Automatically injects game unlock configurations into Steam.\n" +
                        "• Drag & drop .manifest: Injects depot manifests into Steam's depotcache.\n" +
                        "• Drag & drop .zip: Extracts and applies all configs directly.\n" +
                        "Simply restart Steam afterwards to see the game in your Steam Library.",
            RelatedTools = new[] { "navigate_home" }
        },

        // ── 2. MANAGE VIEW ────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "manage_overview",
            Category = "Manage",
            TitleVi = "Quản lý game đã nạp (Manage Tab)",
            TitleEn = "Managing Unlocked Games (Manage Tab)",
            Keywords = new[] { "quản lý", "manage", "xóa game", "danh sách game", "mở thư mục", "game folder", "launch options" },
            ContentVi = "Tab Quản lý (Manage) giúp bạn kiểm soát toàn bộ game đã nạp vào Steam qua BaoTools:\n" +
                        "• Xem danh sách: Tìm kiếm và lọc theo game đã cài trên máy hoặc yêu thích.\n" +
                        "• Xóa game (Biểu tượng thùng rác): Gỡ bỏ cấu hình .lua để game không còn hiển thị trên Steam.\n" +
                        "• Mở thư mục (Open Folder): Mở trực tiếp thư mục cài đặt của game trên ổ cứng.\n" +
                        "• Launch Options: Đặt tham số khởi chạy riêng cho từng game (ví dụ: -dx11, -novid, -windowed).",
            ContentEn = "The Manage tab gives you full control over games unlocked via BaoTools:\n" +
                        "• Game List: Search and filter by installed or favorite games.\n" +
                        "• Delete Game: Removes the .lua config so the game disappears from Steam.\n" +
                        "• Open Folder: Jumps directly to the game installation directory on your disk.\n" +
                        "• Launch Options: Configure custom launch arguments per game (e.g. -dx11, -novid).",
            RelatedTools = new[] { "navigate_manage" }
        },
        new KnowledgeArticle
        {
            Id = "manage_delete_game",
            Category = "Manage",
            TitleVi = "Cách gỡ / xóa game khỏi Steam",
            TitleEn = "How to Uninstall & Remove Games from Steam",
            Keywords = new[] { "xóa game", "xoa game", "gỡ game", "go game", "gỡ cài đặt", "go cai dat", "cách gỡ", "cách xóa", "uninstall", "delete game", "remove game", "xóa khỏi steam", "gỡ khỏi steam" },
            ContentVi = "Để gỡ hoặc xóa game đã nạp qua BaoTools khỏi Steam:\n" +
                        "1. Vào tab Quản lý (Manage) trong BaoTools.\n" +
                        "2. Bấm chọn tựa game muốn gỡ bỏ trong danh sách.\n" +
                        "3. Bấm vào biểu tượng Thùng rác (Xóa game) ở cột chi tiết bên phải để xóa file cấu hình .lua.\n" +
                        "4. Bấm Restart Steam ở góc trên để Steam cập nhật lại thư viện và loại bỏ game hoàn toàn.\n" +
                        "💡 Mẹo: Nếu muốn dọn sạch dung lượng ổ đĩa, bấm nút 'Mở thư mục' (Open Folder) để xóa file cài đặt game.",
            ContentEn = "To uninstall or remove an unlocked game from Steam via BaoTools:\n" +
                        "1. Navigate to the Manage tab in BaoTools.\n" +
                        "2. Select the game you wish to remove from the list.\n" +
                        "3. Click the Trash icon (Delete) in the right details panel to remove the .lua config.\n" +
                        "4. Click Restart Steam in the top bar to update and clean your Steam Library.\n" +
                        "💡 Tip: To free up disk space, click 'Open Folder' to delete the installed files on disk.",
            RelatedTools = new[] { "navigate_manage", "restart_steam" }
        },
        new KnowledgeArticle
        {
            Id = "manage_steamless_drm",
            Category = "Manage",
            TitleVi = "Gỡ Steam DRM (Steamless) chống văng game",
            TitleEn = "Removing Steam DRM with Steamless",
            Keywords = new[] { "steamless", "gỡ drm", "drm", "văng", "crash", "không mở được", "launch crash", "bị out" },
            ContentVi = "Khi game mở lên bị văng (crash) ngay lập tức hoặc không chạy, nguyên nhân chính là do SteamStub DRM:\n" +
                        "1. Vào tab Quản lý (Manage) trong BaoTools.\n" +
                        "2. Bấm vào game đang bị lỗi crash.\n" +
                        "3. Bấm nút 'Gỡ Steam DRM' (Steamless).\n" +
                        "BaoTools sẽ tự động giải mã file .exe gốc của game, gỡ bỏ lớp bảo vệ SteamStub để game khởi động mượt mà.",
            ContentEn = "If a game crashes immediately on launch, it is usually protected by SteamStub DRM:\n" +
                        "1. Go to the 'Manage' tab in BaoTools.\n" +
                        "2. Click on the affected game.\n" +
                        "3. Click 'Remove Steam DRM' (Steamless).\n" +
                        "BaoTools unpacks the game's executable, stripping SteamStub protection so the game runs cleanly.",
            RelatedTools = new[] { "navigate_manage" }
        },
        new KnowledgeArticle
        {
            Id = "manage_depots_dlc",
            Category = "Manage",
            TitleVi = "Kiểm tra Depots và DLC của Game",
            TitleEn = "Inspecting Game Depots and DLCs",
            Keywords = new[] { "depot", "dlc", "thiếu dlc", "missing dlc", "gói nội dung", "depots" },
            ContentVi = "Để kiểm tra DLC hoặc các file gói phụ (depots) của game:\n" +
                        "1. Vào tab Quản lý (Manage) -> Chọn game cần kiểm tra.\n" +
                        "2. Bạn sẽ thấy danh sách tất cả Depots và DLC kèm kích thước và trạng thái manifest.\n" +
                        "3. Nếu thiếu DLC nào, bạn có thể sang tab Bản dựng (Builds) để tải bổ sung manifest của DLC đó.",
            ContentEn = "To inspect DLCs or depot files for a game:\n" +
                        "1. Go to the Manage tab -> Select the game.\n" +
                        "2. View all Depots and DLCs along with their sizes and manifest statuses.\n" +
                        "3. If any DLC is missing, jump to the Builds tab to download its manifest."
        },

        // ── 3. FIXES VIEW ─────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "fixes_how_to_use",
            Category = "Fixes",
            TitleVi = "Cách sử dụng Bản sửa lỗi (Fixes)",
            TitleEn = "How to Use Game Fixes",
            Keywords = new[] { "fixes", "bản sửa lỗi", "sửa lỗi", "crack fix", "cài fix", "fix game", "patch", "tab fixes", "tab sửa lỗi", "cách dùng fix", "cài đặt fix" },
            ContentVi = "Tab Bản sửa lỗi (Fixes) cung cấp các bản vá tự động cho hàng trăm tựa game:\n" +
                        "1. Vào tab Bản sửa lỗi (Fixes) ở thanh menu bên trái.\n" +
                        "2. Nhập tên game hoặc AppID vào ô tìm kiếm.\n" +
                        "3. Bấm vào game để xem danh sách các bản fix có sẵn (Crack Fix, Update Patch, Denuvo Bypass...).\n" +
                        "4. Bấm 'Cài đặt Fix' - BaoTools sẽ tự động tải file vá và giải nén chuẩn xác vào thư mục cài game trên máy bạn.\n" +
                        "Mẹo: Bật công tắc 'Chỉ hiện game của tôi' để lọc nhanh các game đã có sẵn trên máy.",
            ContentEn = "The Fixes tab provides automated patches for hundreds of games:\n" +
                        "1. Click the 'Fixes' tab on the left sidebar.\n" +
                        "2. Search for your game title or AppID.\n" +
                        "3. Click on the game to view available fixes (Crack Fix, Update Patch, Denuvo Bypass).\n" +
                        "4. Click 'Install Fix' - BaoTools automatically downloads and extracts files into the game folder.\n" +
                        "Tip: Toggle 'Only show my games' to quickly filter games installed on your PC.",
            RelatedTools = new[] { "navigate_fixes" }
        },
        new KnowledgeArticle
        {
            Id = "fixes_online_coop",
            Category = "Fixes",
            TitleVi = "Chơi Online Co-op qua Steam (Online Fix)",
            TitleEn = "Online Co-op Multiplayer via Steam",
            Keywords = new[] { "online fix", "online", "coop", "co-op", "chơi chung", "multiplayer", "spacewar", "480", "bạn bè", "chơi với bạn", "chơi cùng bạn", "chơi mạng" },
            ContentVi = "Cách chơi Online Co-op cùng bạn bè qua mạng Steam:\n" +
                        "1. Vào tab Bản sửa lỗi (Fixes) -> Tìm game bạn muốn chơi -> Bấm 'Cài đặt Online Fix'.\n" +
                        "2. Cơ chế: Bản fix kết nối qua server Steam thông qua AppID 480 (Spacewar). Khi vào game, Steam sẽ hiển thị bạn đang chơi Spacewar.\n" +
                        "3. Để chơi cùng bạn bè: Bạn bè của bạn cũng cần mở Steam và cài cùng phiên bản Online Fix. Sau đó vào game tạo phòng và mời nhau qua Shift + Tab!",
            ContentEn = "Playing Online Co-op with friends via Steam:\n" +
                        "1. In Fixes, search for your game -> Click 'Install Online Fix'.\n" +
                        "2. Mechanism: Uses Steam AppID 480 (Spacewar) to tunnel through Steam multiplayer servers.\n" +
                        "3. Playing together: Friends must have Steam open and install the matching fix, then invite via the Steam Shift + Tab overlay!",
            RelatedTools = new[] { "navigate_fixes" }
        },

        // ── 4. DOWNLOADS VIEW ─────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "downloads_manager",
            Category = "Downloads",
            TitleVi = "Quản lý Tải xuống & Tăng tốc FastFetch",
            TitleEn = "Downloads Manager & FastFetch Acceleration",
            Keywords = new[] { "tải xuống", "downloads", "tốc độ", "chậm", "fastfetch", "tạm dừng", "tiếp tục", "pause", "resume", "tab tải xuống", "tab downloads", "tăng tốc" },
            ContentVi = "Tab Tải xuống (Downloads) giúp theo dõi tiến trình tải game/manifest:\n" +
                        "• Theo dõi trực quan: Phần trăm hoàn thành, tốc độ tải hiện tại, thời gian còn lại.\n" +
                        "• Tạm dừng / Tiếp tục: Bạn có thể tạm dừng để giải phóng băng thông rồi tiếp tục tải bất cứ lúc nào.\n" +
                        "• Tăng tốc FastFetch: Vào tab Cài đặt -> Bật 'FastFetch' để ứng dụng tự động tối ưu và tải đa luồng từ các CDN tốc độ cao.",
            ContentEn = "The Downloads tab manages live depot and manifest downloads:\n" +
                        "• Real-time progress: Progress percentage, current download speed, and estimated time.\n" +
                        "• Pause / Resume: Pause downloads at any time to free up bandwidth.\n" +
                        "• FastFetch Acceleration: In Settings, enable 'FastFetch' to download with multi-threaded speed from fast CDNs.",
            RelatedTools = new[] { "navigate_downloads" }
        },

        // ── 5. BUILDS VIEW ────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "builds_manifests",
            Category = "Builds",
            TitleVi = "Bản dựng & Tải Manifest (Builds Tab)",
            TitleEn = "Builds & Manifest Downloads",
            Keywords = new[] { "builds", "bản dựng", "manifest", "depotcache", "tải manifest", "version", "tab builds", "tab bản dựng" },
            ContentVi = "Tab Bản dựng (Builds) là kho lưu trữ manifest chính thức:\n" +
                        "• Tìm kiếm game: Nhập tên hoặc AppID để xem toàn bộ các bản build và manifest theo ngày phát hành.\n" +
                        "• Tải Manifest: Bấm tải manifest để BaoTools tự đưa file vào thư mục depotcache của Steam, giúp Steam nhận diện được phiên bản tải chính xác.",
            ContentEn = "The Builds tab provides access to verified game manifests:\n" +
                        "• Search: Find builds and manifest versions by game name or AppID.\n" +
                        "• Download Manifest: Automatically places manifest files into Steam's depotcache for precise download recognition.",
            RelatedTools = new[] { "navigate_builds" }
        },

        // ── 6. MODE VIEW ──────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "mode_unlockers",
            Category = "Mode",
            TitleVi = "Các Chế độ Mở khóa (BetterSteamTools, OpenSteamTools, Custom, CloudRedirect)",
            TitleEn = "Unlocker Modes (BetterSteamTools, OpenSteamTools Nightly, Custom, CloudRedirect)",
            Keywords = new[] { "mode", "chế độ", "bettersteamtools", "opensteamtools", "custom", "cloudredirect", "đổi mode", "mở khóa", "ost", "bst", "tab mode", "chọn mode", "dùng mode", "sử dụng mode" },
            ContentVi = "Tab Chế độ (Mode) cho phép bạn chọn backend mở khóa game cho Steam (chỉ 1 chế độ hoạt động tại một thời điểm):\n" +
                        "• BetterSteamTools (Được đề xuất): Bản fork mã nguồn mở do đội ngũ BaoTools phát triển và duy trì tích cực. Đây là chế độ ổn định nhất, hỗ trợ tốt nhất cho hầu hết game và tự động cập nhật.\n" +
                        "• OpenSteamTools (Bản thử nghiệm Nightly): Bản dựng mới nhất từ nhánh chính upstream. Thường có các bản vá sửa lỗi mới nhất và hỗ trợ tương thích hoàn hảo với CloudRedirect, nhưng có thể kém ổn định hơn.\n" +
                        "• Trình mở khóa tùy chỉnh (Custom): Dành cho bạn nếu muốn tự dùng giải pháp mở khóa của riêng mình (như SmokeAPI, Koaloader, GreenLuma...). Khi bật chế độ này, BaoTools sẽ không can thiệp hay ghi đè file của bạn.\n" +
                        "• Tiện ích CloudRedirect (Ở dưới cùng tab Mode): Giúp đồng bộ file save game lên các dịch vụ đám mây (Google Drive, OneDrive, Cloudflare R2...) thay thế Steam Cloud gốc. Có thể bấm 'Quản lý' để thiết lập sau khi bật.",
            ContentEn = "The Mode tab manages the backend unlocker for Steam (only one active at a time):\n" +
                        "• BetterSteamTools (Recommended): An open-source fork actively maintained by the BaoTools team. The most stable and reliable option for all games.\n" +
                        "• OpenSteamTools (Nightly / Experimental): Upstream build with the latest bleeding-edge fixes and native CloudRedirect support.\n" +
                        "• Custom Unlocker: Allows using your own custom unlockers (e.g. SmokeAPI, Koaloader, GreenLuma). BaoTools will not modify or overwrite your files.\n" +
                        "• CloudRedirect Add-on (Docked at bottom): Syncs game saves with third-party cloud storage (Google Drive, OneDrive, Cloudflare R2). Click 'Manage' to configure.",
            RelatedTools = new[] { "navigate_mode", "check_mode_status" }
        },

        // ── 7. PLUGIN VIEW ────────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "plugin_steam_store",
            Category = "Plugin",
            TitleVi = "Tiện ích mở rộng Plugin Steam Store",
            TitleEn = "Steam Store Plugin (BaoTools Extension)",
            Keywords = new[] { "plugin", "tiện ích", "thêm qua baotools", "nút tím", "cài plugin", "store plugin", "winmm.dll", "tab plugin", "plugin steam", "cài đặt plugin" },
            ContentVi = "Plugin BaoTools tích hợp trực tiếp vào giao diện Steam Store:\n" +
                        "• Tính năng: Khi lướt xem game trên Steam Store, bạn sẽ thấy nút màu tím 'Thêm qua BaoTools' hiển thị ngay cạnh nút Mua. Bấm vào là game tự nạp vào Steam Library mà không cần rời Steam!\n" +
                        "• Cách cài đặt: Vào tab Plugin ở menu bên trái -> Bấm 'Cài đặt Plugin' -> Khởi động lại Steam.\n" +
                        "• Nếu bị lỗi: Bấm 'Sửa lỗi' hoặc 'Cài đặt lại' trong tab Plugin để làm mới file winmm.dll và giao diện plugin.",
            ContentEn = "The BaoTools Plugin integrates directly into the Steam Store UI:\n" +
                        "• Feature: Browsing games on the Steam Store displays a purple 'Add with BaoTools' button next to the buy button.\n" +
                        "• How to Install: Go to the Plugin tab on the left -> Click 'Install Plugin' -> Restart Steam.\n" +
                        "• Troubleshooting: Click 'Repair' or 'Reinstall' in the Plugin tab to refresh winmm.dll and the UI files.",
            RelatedTools = new[] { "navigate_plugin", "check_plugin_status" }
        },

        // ── 8. SETTINGS VIEW ──────────────────────────────────────
        new KnowledgeArticle
        {
            Id = "settings_hubcap",
            Category = "Settings",
            TitleVi = "Key Hubcap là gì và cách kích hoạt (Hubcap Key)",
            TitleEn = "What is a Hubcap Key and How to Activate It",
            Keywords = new[] { "hubcap", "key hubcap", "hubcap key", "nhập key", "hubcapmanifest", "quota" },
            ContentVi = "Hubcap là hệ thống cung cấp manifest và depot game tốc độ cao:\n" +
                        "• Key Hubcap: Dùng để xác thực và tăng hạn mức tải manifest từ hệ thống Hubcap.\n" +
                        "• Cách lấy key: Truy cập trang web hubcapmanifest.com để nhận key cá nhân.\n" +
                        "• Cách nhập key: Vào tab Cài đặt -> Kéo đến mục 'Hubcap' -> Dán key vào ô và bấm 'Xác thực'. Sau khi lưu thành công, bạn sẽ theo dõi được phần trăm hạn mức sử dụng còn lại.",
            ContentEn = "Hubcap is the high-speed manifest and depot delivery network:\n" +
                        "• Hubcap Key: Authenticates and increases manifest download quotas.\n" +
                        "• Get a Key: Visit hubcapmanifest.com to obtain your key.\n" +
                        "• How to Use: In Settings -> Scroll to 'Hubcap' -> Paste your key and click 'Validate'.",
            RelatedTools = new[] { "navigate_settings" }
        },
        new KnowledgeArticle
        {
            Id = "settings_account_bot",
            Category = "Settings",
            TitleVi = "Tài khoản & Mã kích hoạt Bot Provision",
            TitleEn = "Account & Bot Provision Redeem Code",
            Keywords = new[] { "tài khoản", "account", "đăng nhập", "discord", "BaoTools", "bot provision", "code", "mã" },
            ContentVi = "Quản lý tài khoản trong tab Cài đặt:\n" +
                        "• Đăng nhập Discord: Liên kết tài khoản BaoTools để lưu trữ dữ liệu và đồng bộ game cá nhân.\n" +
                        "• Mã Bot Provision: Nếu bạn nhận được mã 6 chữ số từ Discord Bot, bạn nhập mã vào ô 'Redeem code' trong tab Cài đặt để kích hoạt tài khoản ngay tức thì.",
            ContentEn = "Account management in Settings:\n" +
                        "• Discord Login: Links your BaoTools account to sync personal game libraries.\n" +
                        "• Bot Provision: If you received a 6-digit code from Discord Bot, enter it in Settings to redeem your account.",
            RelatedTools = new[] { "navigate_settings" }
        },
        new KnowledgeArticle
        {
            Id = "settings_appearance_themes",
            Category = "Settings",
            TitleVi = "Giao diện và 16 Bộ màu Theme",
            TitleEn = "Appearance & 16 Vibrant Color Themes",
            Keywords = new[] { "theme", "giao diện", "màu", "đổi màu", "dracula", "tokyo night", "cyberpunk", "nord", "dark", "đổi theme" },
            ContentVi = "BaoTools hỗ trợ 16 theme màu sắc tuyệt đẹp (Default, Dracula, TokyoNight, Synthwave, Cyberpunk, Nord, Catppuccin, OneDark...):\n" +
                        "• Cách đổi theme: Vào Cài đặt -> Mục Giao diện (Appearance) -> Chọn theme yêu thích trong danh sách.\n" +
                        "• Màu sắc áp dụng ngay lập tức mà không cần khởi động lại ứng dụng.",
            ContentEn = "BaoTools features 16 curated dark themes (Dracula, TokyoNight, Cyberpunk, Nord, Catppuccin, etc.):\n" +
                        "• How to change: Go to Settings -> Appearance -> Select your desired theme.\n" +
                        "• Color updates apply dynamically without restarting the app.",
            RelatedTools = new[] { "navigate_settings" }
        },
        new KnowledgeArticle
        {
            Id = "settings_startup_tray",
            Category = "Settings",
            TitleVi = "Khởi động cùng Windows và Chế độ khay hệ thống (Tray)",
            TitleEn = "Windows Startup and System Tray Mode",
            Keywords = new[] { "khởi động cùng windows", "start with windows", "steam startup", "khay hệ thống", "tray", "minimize to tray", "chạy ngầm" },
            ContentVi = "Các tùy chọn khởi động và thu nhỏ trong tab Cài đặt:\n" +
                        "• Khởi động cùng Windows: Tự động chạy BaoTools ở chế độ ngầm khi bật máy tính.\n" +
                        "• Khởi động cùng Steam: Khi bạn mở Steam, BaoTools sẽ tự khởi chạy ở chế độ khay để phục vụ cục bộ cho plugin.\n" +
                        "• Thu nhỏ vào khay (Minimize to Tray): Khi bấm nút tắt (X) hoặc thu nhỏ, app sẽ ẩn xuống khay góc dưới bên phải thay vì tắt hẳn.",
            ContentEn = "Startup and tray options in Settings:\n" +
                        "• Start with Windows: Automatically launches BaoTools on Windows boot.\n" +
                        "• Start with Steam: Launches silently in tray when Steam starts.\n" +
                        "• Minimize to Tray: Minimizing or closing hides BaoTools to the system tray.",
            RelatedTools = new[] { "navigate_settings" }
        },

        // ── 9. TICKETS & DENUVO VIEW ─────────────────────────────
        new KnowledgeArticle
        {
            Id = "tickets_denuvo",
            Category = "Tickets",
            TitleVi = "Vé kích hoạt Steam & Denuvo Tickets (Tickets Tab)",
            TitleEn = "Steam & Denuvo Ticket Extraction (Tickets Tab)",
            Keywords = new[] { "ticket", "vé", "tickets", "denuvo", "appticket", "eticket", "bản quyền", "chia sẻ vé", "export ticket", "bảo vệ denuvo" },
            ContentVi = "Tab Vé kích hoạt (Tickets) chuyên dùng để xử lý game có bản quyền đặc biệt hoặc Denuvo:\n" +
                        "• Trích xuất App Ticket & Encrypted Ticket: Tự động lấy vé bản quyền hợp lệ trực tiếp từ phiên bản Steam đang chạy.\n" +
                        "• Tự động nạp vào .lua: Khi bật 'Tự động áp dụng vào .lua', vé sẽ được nhúng thẳng vào file cấu hình mở khóa game.\n" +
                        "• Danh sách game Denuvo: Tự động lọc và nhận diện các game có lớp bảo vệ Denuvo để trích xuất vé chính xác.\n" +
                        "• Xuất gói chia sẻ (Export ZIP): Đóng gói vé thành file zip để sao lưu hoặc chia sẻ cho bạn bè cùng chơi.",
            ContentEn = "The Tickets tab manages Steam App Tickets and Denuvo ticket extraction:\n" +
                        "• Extract App & Encrypted Tickets: Obtains valid ownership tickets directly from running Steam.\n" +
                        "• Auto-apply to .lua: Injects extracted tickets directly into the game's .lua unlock configuration.\n" +
                        "• Denuvo Game Filter: Automatically identifies games protected by Denuvo for accurate ticket extraction.\n" +
                        "• Export ZIP: Packages tickets into a shareable zip file for backup or sharing with friends."
        },

        // ── 10. STEAMAUTOCRACK & EMULATION ────────────────────────
        new KnowledgeArticle
        {
            Id = "tools_steamautocrack",
            Category = "Tools",
            TitleVi = "SteamAutoCrack vs Steamless (Giả lập & Bẻ khóa Offline)",
            TitleEn = "SteamAutoCrack vs Steamless (Emulation & Offline Play)",
            Keywords = new[] { "steamautocrack", "autocrack", "goldberg", "ali213", "emulator", "giả lập steam", "offline crack", "không cần steam" },
            ContentVi = "Sự khác biệt giữa SteamAutoCrack và Steamless trong BaoTools:\n" +
                        "• Steamless (trong tab Quản lý): Chỉ gỡ bỏ lớp mã hóa SteamStub của file .exe để chống văng game khi khởi chạy qua Steam.\n" +
                        "• SteamAutoCrack (trong tab Tải xuống): Tự động giả lập Steam API (bằng Goldberg Emulator / ALI213). Sau khi crack bằng tool này, bạn có thể chơi game hoàn toàn độc lập mà KHÔNG cần mở phần mềm Steam.\n" +
                        "• Cách mở: Vào tab Tải xuống (Downloads) -> Bấm nút 'SteamAutoCrack' ở góc phải để BaoTools tự tải runtime và mở phần mềm.",
            ContentEn = "Differences between SteamAutoCrack and Steamless in BaoTools:\n" +
                        "• Steamless (Manage tab): Only unpacks SteamStub DRM from the .exe to prevent launch crashes when opening via Steam.\n" +
                        "• SteamAutoCrack (Downloads tab): Emulates the Steam API (using Goldberg/ALI213 emulator), allowing completely standalone offline play WITHOUT running Steam.\n" +
                        "• How to run: Go to Downloads -> Click 'SteamAutoCrack' to automatically install runtime and launch the tool."
        },

        // ── 11. ONLINE FIXES VIEW ─────────────────────────────────
        new KnowledgeArticle
        {
            Id = "online_fixes_view",
            Category = "OnlineFixes",
            TitleVi = "Kho Bản sửa lỗi chơi Online (Online Fixes Tab)",
            TitleEn = "Online Multiplayer Fixes (Online Fixes Tab)",
            Keywords = new[] { "online-fix", "online fixes", "online-fix.me", "mục online", "multiplayer fix", "chơi mạng" },
            ContentVi = "Tab Online Fixes chuyên cung cấp các bản sửa lỗi chơi nhiều người từ trang online-fix.me:\n" +
                        "• Tìm kiếm game: Nhập tên game để xem danh sách các phiên bản fix online có sẵn.\n" +
                        "• Cài đặt 1-Click: Chọn game và bấm Cài đặt, BaoTools sẽ tải gói fix và chèn tự động vào thư mục game.\n" +
                        "• Lưu ý khi chơi: Bạn và bạn bè phải cùng cài một phiên bản fix giống nhau và đều đăng nhập Steam trước khi vào game.",
            ContentEn = "The Online Fixes tab provides multiplayer patches sourced from online-fix.me:\n" +
                        "• Search: Find available multiplayer patches for your games.\n" +
                        "• 1-Click Install: Downloads and deploys the fix files directly into the game folder.\n" +
                        "• Multiplayer tip: You and your friends must have the exact same fix installed and be logged into Steam."
        },

        // ── 12. LAUNCH OPTIONS & PRESETS ──────────────────────────
        new KnowledgeArticle
        {
            Id = "launch_options_presets",
            Category = "Manage",
            TitleVi = "Tham số Khởi chạy & Tối ưu Game (Launch Options)",
            TitleEn = "Launch Options Presets & Game Optimization",
            Keywords = new[] { "tham số khởi chạy", "launch options", "-novid", "-dx11", "-dx12", "-vulkan", "windowed", "cờ khởi chạy", "tối ưu đồ họa" },
            ContentVi = "Cửa sổ Cấu hình Khởi chạy (Launch Options) giúp tùy biến tham số cho từng game:\n" +
                        "• Cách mở: Vào tab Quản lý -> Bấm vào game -> Chọn 'Launch Options'.\n" +
                        "• Các thiết lập sẵn phổ biến:\n" +
                        "  - Direct3D 11 (-dx11): Khắc phục lỗi crash trên các card đồ họa cũ.\n" +
                        "  - DirectX 12 (-dx12): Tận dụng hiệu năng tối đa trên phần cứng mới.\n" +
                        "  - Vulkan (-vulkan): Giảm giật lag trên nhiều dòng máy AMD hoặc Linux/Proton.\n" +
                        "  - Bỏ qua video intro (-novid): Vào thẳng menu chính của game siêu nhanh.\n" +
                        "  - Chế độ cửa sổ (-windowed / -noborder): Chơi dạng cửa sổ không viền tiện chuyển tab.",
            ContentEn = "The Launch Options dialog allows customizing startup parameters per game:\n" +
                        "• How to open: Go to Manage tab -> Click on a game -> Select 'Launch Options'.\n" +
                        "• Common presets:\n" +
                        "  - DirectX 11 (-dx11): Fixes crashes on older GPUs.\n" +
                        "  - DirectX 12 (-dx12): Maximum performance on modern hardware.\n" +
                        "  - Vulkan (-vulkan): Smoother frame times on AMD or Steam Deck.\n" +
                        "  - Skip Intro Videos (-novid): Jumps straight into the game menu.\n" +
                        "  - Windowed (-windowed / -noborder): Borderless window mode for easy alt-tabbing."
        },

        // ── 13. SELECTIVE DEPOT DOWNLOADING ───────────────────────
        new KnowledgeArticle
        {
            Id = "download_selective_depots",
            Category = "Downloads",
            TitleVi = "Tải chọn lọc Ngôn ngữ & DLC (Selective Depots)",
            TitleEn = "Selective Depot & Language Downloading",
            Keywords = new[] { "chọn depot", "chọn ngôn ngữ", "tiết kiệm dung lượng", "bỏ ngôn ngữ", "tải dlc riêng", "selective download" },
            ContentVi = "Khi tải game qua trang Chi tiết Tải xuống của BaoTools:\n" +
                        "• Chọn lọc ngôn ngữ: Bạn có thể bỏ tích các gói ngôn ngữ không dùng đến (như tiếng Đức, Pháp, Nga, Bồ Đào Nha...) để tiết kiệm từ 10GB - 50GB ổ cứng và tải nhanh hơn gấp nhiều lần.\n" +
                        "• Chọn lọc DLC: Chỉ tích chọn các DLC bạn thực sự muốn chơi thay vì phải tải toàn bộ tất cả DLC.\n" +
                        "• So khớp SteamDB: Bấm vào từng depot để đối chiếu chi tiết tên file và dung lượng trên cơ sở dữ liệu SteamDB.",
            ContentEn = "In BaoTools detailed Game Download page:\n" +
                        "• Filter Languages: Uncheck unused language audio/text packs (French, Russian, German, etc.) to save 10GB-50GB of disk space and download much faster.\n" +
                        "• Selective DLC: Download only the specific expansions or DLCs you want.\n" +
                        "• SteamDB Inspection: Click any depot row to view its exact files and metadata on SteamDB."
        },

        // ── 14. DONATE KEYS & COMMUNITY ───────────────────────────
        new KnowledgeArticle
        {
            Id = "settings_donate_keys",
            Category = "Settings",
            TitleVi = "Đóng góp Key cộng đồng (Donate Keys)",
            TitleEn = "Community Donate Keys Feature",
            Keywords = new[] { "donate keys", "đóng góp key", "cộng đồng", "chia sẻ key", "donate" },
            ContentVi = "Tùy chọn 'Donate Keys' trong tab Cài đặt (Settings):\n" +
                        "• Mục đích: Khi bạn kích hoạt tính năng này, BaoTools sẽ tự động gửi ẩn danh các depot key và manifest đã xác thực từ máy bạn lên cơ sở dữ liệu chung của cộng đồng.\n" +
                        "• An toàn & Bảo mật: Hoàn toàn KHÔNG gửi bất kỳ thông tin cá nhân, mật khẩu hay tài khoản Steam nào của bạn. Việc này chỉ giúp cộng đồng có đủ dữ liệu manifest để tải game khi máy chủ chính bị quá tải.",
            ContentEn = "The 'Donate Keys' toggle in Settings:\n" +
                        "• Purpose: Automatically contributes anonymous verified depot decryption keys and manifests from your machine to the community database.\n" +
                        "• Privacy & Safety: Never transmits personal credentials, passwords, or Steam account tokens. It simply helps the community preserve working manifests."
        },

        // ── 15. CEF DEBUGGING & PLUGIN TROUBLESHOOTING ────────────
        new KnowledgeArticle
        {
            Id = "plugin_cef_debugging",
            Category = "Plugin",
            TitleVi = "Cơ chế Cổng CEF Debug 8080 của Plugin",
            TitleEn = "CEF Remote Debugging Port 8080 & Plugin Injection",
            Keywords = new[] { "8080", "cef", "mất nút tím", "remote debugging", "lỗi plugin", "không hiện nút", "sửa plugin" },
            ContentVi = "Cơ chế hoạt động của Plugin BaoTools trên Steam Store:\n" +
                        "• Cổng Debug 8080: Plugin giao tiếp với giao diện Steam thông qua cổng CEF Remote Debugging (port 8080) và file .cef-enable-remote-debugging.\n" +
                        "• Khi Steam cập nhật: Các bản cập nhật lớn của Steam có thể làm ghi đè hoặc vô hiệu hóa cổng debug, khiến nút tím 'Thêm qua BaoTools' biến mất.\n" +
                        "• Cách khắc phục: Mở BaoTools -> Vào tab Plugin -> Bấm 'Cài đặt lại' hoặc 'Sửa lỗi' -> Khởi động lại Steam là nút tím sẽ xuất hiện trở lại ngay lập tức.",
            ContentEn = "How the BaoTools Steam Store Plugin works:\n" +
                        "• CEF Port 8080: Communicates with Steam's web UI via CEF Remote Debugging (port 8080) and .cef-enable-remote-debugging.\n" +
                        "• Steam Client Updates: Large Steam updates may overwrite the hook or reset debugging flags, causing the purple button to disappear.\n" +
                        "• Fix: Open BaoTools -> Plugin tab -> Click 'Reinstall' or 'Repair' -> Restart Steam."
        },

        // ── 16. APP UPDATER & CHANGELOG ───────────────────────────
        new KnowledgeArticle
        {
            Id = "app_update_system",
            Category = "General",
            TitleVi = "Cập nhật ứng dụng BaoTools & Lịch sử phiên bản",
            TitleEn = "BaoTools Updates & Changelog History",
            Keywords = new[] { "cập nhật baotools", "update app", "phiên bản mới", "changelog", "lịch sử cập nhật", "tải bản mới" },
            ContentVi = "Hệ thống tự động cập nhật của BaoTools:\n" +
                        "• Kiểm tra tự động: Mỗi khi khởi động, BaoTools sẽ kiểm tra phiên bản phát hành mới nhất trên GitHub.\n" +
                        "• Cửa sổ cập nhật: Khi có bản mới, ứng dụng sẽ hiện thông báo kèm chi tiết các thay đổi (Changelog).\n" +
                        "• Cài đặt tự động: Bấm 'Cập nhật ngay', phần mềm sẽ tự tải bản vá, đóng ứng dụng cũ và cài đặt phiên bản mới mà bạn không cần phải tự giải nén thủ công.",
            ContentEn = "BaoTools Automated Update System:\n" +
                        "• Auto-check: BaoTools checks GitHub for new releases on startup.\n" +
                        "• Update Dialog: Displays detailed release notes and changelogs when a new version is available.\n" +
                        "• 1-Click Update: Downloads and applies the update smoothly without manual extraction."
        },

        // ── 17. AI ASSISTANT FEATURES ─────────────────────────────
        new KnowledgeArticle
        {
            Id = "ai_assistant_features",
            Category = "AI",
            TitleVi = "Trợ lý ảo AI & Chẩn đoán tự động (AI Assistant)",
            TitleEn = "AI Assistant & Automated System Diagnostics",
            Keywords = new[] { "ai", "trợ lý", "bot", "hỏi đáp", "chẩn đoán", "sao chép", "chat ai", "ai tool" },
            ContentVi = "Trợ lý ảo AI tích hợp sẵn trong BaoTools:\n" +
                        "• Hoàn toàn miễn phí & Không cần cấu hình: Sử dụng ngay mà không cần nhập API key hay tạo tài khoản.\n" +
                        "• Chẩn đoán hệ thống (Tool Calling): AI có khả năng tự động kiểm tra trạng thái Steam, tình trạng cài đặt plugin, đếm số game đã nạp trên máy bạn và tự bấm Khởi động lại Steam khi bạn yêu cầu.\n" +
                        "• Bật/Tắt Trợ lý: Nếu không muốn hiển thị bong bóng chat, bạn có thể tắt tại tab Cài đặt -> mục 'Trợ lý AI'.",
            ContentEn = "Built-in AI Assistant in BaoTools:\n" +
                        "• Completely Free & Zero Config: Ready to use instantly without API key or account creation.\n" +
                        "• System Diagnostics (Tool Calling): AI can check Steam status, verify plugin installation, count unlocked games, and restart Steam upon request.\n" +
                        "• Enable/Disable: Toggle the AI widget anytime in Settings -> 'AI Assistant'."
        },

        // ── 18. TROUBLESHOOTING & GENERAL HELP ────────────────────
        new KnowledgeArticle
        {
            Id = "troubleshooting_library_missing",
            Category = "Troubleshooting",
            TitleVi = "Thêm game rồi nhưng không thấy trong Thư viện Steam",
            TitleEn = "Game Added but Not Showing in Steam Library",
            Keywords = new[] { "không thấy game", "mất game", "không hiện", "thư viện", "library", "chưa thấy", "restart steam" },
            ContentVi = "Nếu bạn đã thêm game thành công trong BaoTools nhưng mở Steam chưa thấy:\n" +
                        "1. Khởi động lại Steam: Bấm nút 'Restart Steam' ở góc trên cùng bên phải cửa sổ BaoTools. Steam bắt buộc phải khởi động lại thì mới nạp danh sách game mới.\n" +
                        "2. Tắt bộ lọc Steam: Trong Thư viện Steam (Library), kiểm tra xem bạn có đang bật bộ lọc 'Ready to play only' (Chỉ game đã cài) không. Hãy tắt bộ lọc này đi.\n" +
                        "3. Kiểm tra đường dẫn: Vào Cài đặt xem đường dẫn thư mục Steam đã trỏ đúng vào thư mục cài Steam thật trên máy chưa.",
            ContentEn = "If a game was added in BaoTools but is missing from your Steam Library:\n" +
                        "1. Restart Steam: Click the 'Restart Steam' button at top-right of BaoTools.\n" +
                        "2. Clear Filters: Ensure 'Ready to play only' filter in Steam Library is turned off.\n" +
                        "3. Verify Path: Check in Settings that your Steam installation path is accurate."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_dll_errors",
            Category = "Troubleshooting",
            TitleVi = "Lỗi thiếu DLL (msvcp140.dll, vcruntime140.dll) hoặc 0xc000007b",
            TitleEn = "Missing DLL Errors (msvcp140.dll, vcruntime140.dll) & 0xc000007b",
            Keywords = new[] { "dll", "msvcp140", "vcruntime140", "0xc000007b", "thiếu file dll", "c++", "directx", "visual c++" },
            ContentVi = "Lỗi thiếu file DLL hoặc mã 0xc000007b xuất hiện khi hệ thống thiếu thư viện phụ trợ của Windows:\n" +
                        "1. Cài đặt Visual C++ Redistributable All-in-One (tổng hợp 2005 - 2022 cả bản x86 và x64).\n" +
                        "2. Cài đặt DirectX End-User Runtimes (June 2010).\n" +
                        "3. Khởi động lại máy tính sau khi cài đặt hoàn tất.",
            ContentEn = "Missing DLL errors or error 0xc000007b indicate missing system runtime libraries:\n" +
                        "1. Install Visual C++ Redistributable All-in-One (2005-2022, x86 and x64).\n" +
                        "2. Install DirectX End-User Runtimes (June 2010).\n" +
                        "3. Restart your computer after installation."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_restart_steam",
            Category = "Troubleshooting",
            TitleVi = "Cách Khởi động lại Steam nhanh",
            TitleEn = "Quick Steam Restart",
            Keywords = new[] { "khởi động lại steam", "restart steam", "tắt steam", "mở lại steam" },
            ContentVi = "Để khởi động lại Steam nhanh nhất:\n" +
                        "Bấm vào nút 'Restart Steam' (biểu tượng mũi tên xoay tròn) ở góc trên cùng bên phải thanh tiêu đề của BaoTools. Ứng dụng sẽ tự động đóng tiến trình Steam và bật lại Steam trong 2 giây.",
            ContentEn = "To restart Steam quickly:\n" +
                        "Click the 'Restart Steam' button (circular arrow icon) in the top-right header of BaoTools. It safely closes and restarts Steam in seconds."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_antivirus_false_positive",
            Category = "Troubleshooting",
            TitleVi = "Windows Defender / Diệt virus báo mã độc hoặc xóa file DLL",
            TitleEn = "Antivirus / Windows Defender False Positive & Missing DLLs",
            Keywords = new[] { "diệt virus", "windows defender", "antivirus", "báo virus", "trojan", "xóa file", "chặn dll", "bị xóa", "false positive" },
            ContentVi = "Khi Windows Defender hoặc phần mềm diệt virus báo nguy hiểm và xóa các file như winmm.dll, OpenSteamTool.dll:\n" +
                        "• Nguyên nhân: Đây là cảnh báo nhận nhầm (False Positive). Các công cụ unlocker sử dụng kỹ thuật can thiệp DLL hook vào Steam để mở khóa game nên bị trình diệt virus hiểu lầm là phần mềm can thiệp.\n" +
                        "• Cách xử lý:\n" +
                        "  1. Mở Windows Security (Bảo mật Windows) -> Virus & threat protection -> Protection history.\n" +
                        "  2. Tìm file bị chặn -> Chọn 'Restore' (Khôi phục) và 'Allow on device' (Cho phép trên thiết bị).\n" +
                        "  3. Vào phần Exclusions (Loại trừ) -> Thêm thư mục cài Steam và thư mục BaoTools vào danh sách loại trừ để không bị quét xóa lại.",
            ContentEn = "When Windows Defender or antivirus flags and deletes files like winmm.dll or OpenSteamTool.dll:\n" +
                        "• Cause: This is a False Positive. Game unlockers use DLL hooking techniques to interface with Steam, triggering heuristic alerts.\n" +
                        "• How to resolve:\n" +
                        "  1. Open Windows Security -> Virus & threat protection -> Protection history.\n" +
                        "  2. Locate blocked item -> Select 'Restore' and 'Allow on device'.\n" +
                        "  3. In Exclusions, add your Steam directory and BaoTools folder so they won't be quarantined again."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_download_stuck_slow",
            Category = "Troubleshooting",
            TitleVi = "Tải game bị đứng 0%, tốc độ 0 KB/s hoặc tải chậm",
            TitleEn = "Download Stuck at 0%, 0 KB/s Speed or Download Failed",
            Keywords = new[] { "đứng tải", "0 kb/s", "tải chậm", "0%", "không tải được", "download error", "download stuck", "mạng chậm" },
            ContentVi = "Nếu lượt tải game trong tab Tải xuống bị đứng hoặc tốc độ 0 KB/s:\n" +
                        "1. Bật FastFetch: Vào Cài đặt (Settings) -> Bật công tắc 'FastFetch'. Tính năng này sẽ tải đa luồng từ các CDN tốc độ cao.\n" +
                        "2. Tạm dừng & Tiếp tục: Bấm nút Pause rồi Resume lại để khởi tạo lại luồng tải dữ liệu mới.\n" +
                        "3. Kiểm tra Hubcap Key: Nếu tải qua nguồn Hubcap, hãy kiểm tra xem hạn mức (quota) trong tab Cài đặt có còn không.\n" +
                        "4. Tường lửa / Mạng: Đảm bảo mạng của bạn không chặn các dịch vụ đám mây CDN lưu trữ file game.",
            ContentEn = "If a game download is frozen at 0% or dropping to 0 KB/s:\n" +
                        "1. Enable FastFetch: In Settings, toggle on 'FastFetch' to enable multi-threaded CDN acceleration.\n" +
                        "2. Pause & Resume: Click Pause and then Resume to force the download queue to establish new sockets.\n" +
                        "3. Check Hubcap Quota: If downloading via Hubcap, check your remaining quota under Settings.\n" +
                        "4. Firewall / Network: Verify that Windows Firewall is not blocking download connections."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_missing_dlc_content",
            Category = "Troubleshooting",
            TitleVi = "Vào game nhưng không nhận DLC hoặc thiếu nội dung mở rộng",
            TitleEn = "DLC Not Detected or In-Game Expansions Missing",
            Keywords = new[] { "thiếu dlc", "không nhận dlc", "mất dlc", "không có dlc", "chưa mua dlc", "missing dlc", "no dlc" },
            ContentVi = "Nếu đã nạp game nhưng khi vào chơi không thấy các gói mở rộng (DLC):\n" +
                        "1. Kiểm tra Manifest DLC: Vào tab Bản dựng (Builds) -> Tìm tên game và tải đầy đủ manifest của các DLC tương ứng.\n" +
                        "2. Chuyển đổi Mode: Vào tab Chế độ (Mode) -> Đổi sang 'BetterSteamTools' hoặc 'OpenSteamTools' -> Khởi động lại Steam.\n" +
                        "3. Gỡ SteamStub (Steamless): Vào tab Quản lý -> Chọn game -> Bấm 'Gỡ Steam DRM (Steamless)' để tránh file .exe gốc kiểm tra DRM chặn DLC.\n" +
                        "4. Kiểm tra file .lua: Trong tab Quản lý, kiểm tra xem AppID của các DLC đã được liệt kê trong danh sách DLC của game chưa.",
            ContentEn = "If game runs but DLCs or expansions are missing:\n" +
                        "1. Manifest DLCs: Go to Builds tab -> Search game and download corresponding DLC manifests.\n" +
                        "2. Switch Unlocker Mode: In Mode tab, ensure 'BetterSteamTools' or 'OpenSteamTools' is active, then restart Steam.\n" +
                        "3. Remove DRM (Steamless): In Manage tab -> Select game -> Click 'Remove Steam DRM (Steamless)'.\n" +
                        "4. Verify Lua: In Manage tab, check that the DLC AppIDs are correctly registered in the game's .lua file."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_no_licenses_error",
            Category = "Troubleshooting",
            TitleVi = "Lỗi 'No licenses' hoặc 'An error occurred while launching'",
            TitleEn = "'No licenses' or 'An error occurred while launching' Error",
            Keywords = new[] { "no licenses", "chưa có bản quyền", "an error occurred while launching", "bắt mua", "license error" },
            ContentVi = "Khi bấm chơi game trên Steam nhưng Steam hiện thông báo lỗi 'No licenses' hoặc đòi mua game:\n" +
                        "1. Khởi động lại Steam: Nhấp 'Restart Steam' ở góc trên phải BaoTools. Nếu vừa nạp game mà chưa khởi động lại Steam thì Steam chưa có bản quyền ảo.\n" +
                        "2. Kiểm tra Plugin/Mode: Vào tab Mode kiểm tra xem BetterSteamTools đã ở trạng thái 'ĐANG HOẠT ĐỘNG' chưa. Nếu chưa hãy bấm Cài đặt.\n" +
                        "3. Nạp lại file cấu hình: Vào tab Quản lý, kiểm tra game có trong danh sách không. Bạn có thể thả lại file .lua hoặc .zip vào ô Drop Zone ở Trang chủ để nạp lại từ đầu.",
            ContentEn = "When Steam says 'No licenses' or asks you to purchase the game:\n" +
                        "1. Restart Steam: Click 'Restart Steam' in BaoTools top header to load the newly registered license.\n" +
                        "2. Verify Unlocker Mode: In Mode tab, verify that BetterSteamTools is 'ACTIVE'. Click Install if it isn't.\n" +
                        "3. Re-inject Config: Verify the game appears in the Manage tab. Drag and drop the .lua or .zip into the Home Drop Zone to re-inject."
        },

        // ── 7. SYSTEM, HARDWARE & TECHNICAL COPILOT ARTICLES ─────
        new KnowledgeArticle
        {
            Id = "system_specs_compatibility",
            Category = "Hardware & Specs",
            Topic = "Hardware Compatibility",
            TitleVi = "Kiểm tra Cấu hình & Khả năng Chơi Game",
            TitleEn = "System Specs & Game Compatibility",
            Keywords = new[] { "chơi được không", "chạy được không", "chơi dc k", "chay dc k", "cấu hình", "cau hinh", "rtx", "gtx", "radeon", "gpu", "vram", "can i run it", "pc specs", "system requirements" },
            IntentHints = new[] { "check_specs", "check_gpu", "check_ram" },
            RelatedTools = new[] { "check_gpu", "check_ram", "check_game_specs" },
            Priority = 35,
            ContentVi = "Khả năng chơi game phụ thuộc vào GPU, dung lượng VRAM và RAM hệ thống. Trợ lý BaoTools có thể đọc trực tiếp card đồ họa và RAM thực tế trên máy để so sánh với yêu cầu cấu hình tối thiểu và đề xuất của game.",
            ContentEn = "Gaming capability depends on your GPU, dedicated VRAM, and system RAM. BaoTools Assistant can inspect your actual hardware to evaluate minimum and recommended game requirements."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_dll_0xc000007b",
            Category = "Troubleshooting",
            Topic = "DLL & Runtime Error",
            TitleVi = "Sửa lỗi thiếu file DLL & Mã lỗi 0xc000007b / 0xc0000142",
            TitleEn = "Fixing Missing DLL & 0xc000007b / 0xc0000142 Launch Errors",
            Keywords = new[] { "0xc000007b", "0xc0000142", "msvcp", "vcruntime", "thiếu dll", "thieu dll", "missing dll", "dll error", "d3dcompiler", "xinput" },
            IntentHints = new[] { "fix_dll", "check_vcredist", "check_directx" },
            RelatedTools = new[] { "check_vcredist", "check_directx" },
            Priority = 40,
            ContentVi = "Lỗi 0xc000007b và thiếu DLL (như msvcp140.dll, vcruntime140.dll) xảy ra do máy thiếu hoặc bị xung đột gói Visual C++ Redistributable (2015-2022) hoặc DirectX. Cần cài đặt gói Visual C++ All-in-One x86 và x64, đồng thời chạy DirectX End-User Runtimes.",
            ContentEn = "Error 0xc000007b and missing DLLs (such as msvcp140.dll, vcruntime140.dll) occur when Visual C++ Redistributable runtimes (2015-2022) or DirectX components are missing or corrupted. Install both x86 and x64 Visual C++ packages."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_game_crash_launch",
            Category = "Troubleshooting",
            Topic = "Game Startup Crash",
            TitleVi = "Khắc phục game bị văng (Crash) ngay khi khởi chạy",
            TitleEn = "Troubleshooting Game Crashing Immediately on Launch",
            Keywords = new[] { "crash", "văng game", "vang game", "bị out", "bi out", "không mở được", "khong mo duoc", "không chạy", "khong chay", "launch crash", "won't start" },
            IntentHints = new[] { "fix_crash", "steamless" },
            RelatedTools = new[] { "diagnose_game_crash", "check_directx", "check_vcredist" },
            Priority = 35,
            ContentVi = "Game bị văng ngay lúc mở thường do: 1. Khóa bảo mật SteamStub DRM (giải pháp: dùng Steamless trong tab Quản lý để gỡ DRM); 2. Thiếu Visual C++ / DirectX runtime; 3. Đường dẫn game chứa dấu tiếng Việt hoặc cần chạy dưới quyền Administrator.",
            ContentEn = "Games crashing immediately on launch are typically caused by: 1. SteamStub DRM (fix via BaoTools Steamless in Manage tab); 2. Missing Visual C++ or DirectX; 3. Non-English folder path or lack of administrator permissions."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_steam_unrecognized_game",
            Category = "Troubleshooting",
            Topic = "Steam Synchronization",
            TitleVi = "Steam không nhận game / Mất game trong thư viện Steam",
            TitleEn = "Steam Not Detecting Game / Missing in Steam Library",
            Keywords = new[] { "steam không nhận", "steam khong nhan", "không thấy game", "khong thay game", "mất game", "mat game", "thư viện steam", "thu vien steam", "not showing in steam", "missing in library" },
            IntentHints = new[] { "steam_sync", "check_games" },
            RelatedTools = new[] { "check_games_status", "restart_steam" },
            Priority = 30,
            ContentVi = "Khi Steam không hiển thị game đã nạp: 1. Khởi động lại Steam (bấm Restart Steam ở góc trên phải BaoTools); 2. Kiểm tra tab Quản lý xem file cấu hình .lua của game còn trong stplug-in không; 3. Đảm bảo chế độ Unlocker trong tab Mode đang Hoạt động.",
            ContentEn = "When Steam does not display an injected game: 1. Restart Steam via BaoTools header button; 2. Check Manage tab to confirm the game's .lua exists in stplug-in; 3. Verify that the unlocker mode in Mode tab is Active."
        },
        new KnowledgeArticle
        {
            Id = "troubleshooting_fps_stutter",
            Category = "Optimization",
            Topic = "Performance Optimization",
            TitleVi = "Tối ưu FPS & Khắc phục giật lag khi chơi game",
            TitleEn = "FPS Optimization & Stutter Reduction",
            Keywords = new[] { "fps thấp", "fps thap", "tụt fps", "tut fps", "giật lag", "giat lag", "low fps", "drop fps", "stutter", "lag" },
            IntentHints = new[] { "optimize_fps", "check_gpu" },
            RelatedTools = new[] { "check_gpu", "check_ram" },
            Priority = 25,
            ContentVi = "Để tăng FPS và giảm giật lag: 1. Thêm tham số '-dx11' vào Launch Options trong tab Quản lý nếu game chạy DirectX 12 không mượt; 2. Bật Windows Game Mode và chọn High Performance trong Power Options; 3. Cập nhật driver card đồ họa mới nhất.",
            ContentEn = "To boost FPS and minimize stutter: 1. Add '-dx11' in Launch Options within Manage tab if DirectX 12 stutters; 2. Enable Windows Game Mode and High Performance power plan; 3. Keep GPU drivers updated."
        },
        new KnowledgeArticle
        {
            Id = "system_directx_inspection",
            Category = "Hardware & Specs",
            Topic = "DirectX & Graphics",
            TitleVi = "Kiểm tra & Chẩn đoán DirectX của hệ thống",
            TitleEn = "DirectX & Graphics Runtimes Inspection",
            Keywords = new[] { "directx", "dx11", "dx12", "d3d", "direct3d", "kiểm tra directx", "directx có vấn đề" },
            IntentHints = new[] { "check_directx" },
            RelatedTools = new[] { "check_directx" },
            Priority = 30,
            ContentVi = "BaoTools kiểm tra phiên bản DirectX và mức tính năng Direct3D (Feature Levels) được hỗ trợ bởi GPU của bạn để đảm bảo máy tương thích hoàn hảo với các game DirectX 11 và DirectX 12.",
            ContentEn = "BaoTools inspects the DirectX version and Direct3D Feature Levels supported by your GPU to ensure compatibility with DirectX 11 and DirectX 12 games."
        },
        new KnowledgeArticle
        {
            Id = "system_ram_inspection",
            Category = "Hardware & Specs",
            Topic = "Memory & Hardware",
            TitleVi = "Kiểm tra Dung lượng Bộ nhớ RAM hệ thống",
            TitleEn = "System RAM Capacity Inspection",
            Keywords = new[] { "ram bao nhiêu", "ram bao nhieu", "kiểm tra ram", "kiem tra ram", "dung lượng ram", "how much ram", "system memory" },
            IntentHints = new[] { "check_ram" },
            RelatedTools = new[] { "check_ram" },
            Priority = 30,
            ContentVi = "BaoTools đọc trực tiếp dung lượng RAM vật lý đang lắp trên máy tính của bạn và kiểm tra xem có đáp ứng đủ tiêu chuẩn tối thiểu (8GB) hoặc khuyến nghị (16GB+) cho các tựa game hiện đại không.",
            ContentEn = "BaoTools inspects your physical system RAM to verify whether it meets minimum (8GB) or recommended (16GB+) requirements for modern games."
        },
        new KnowledgeArticle
        {
            Id = "baotools_app_overview",
            Category = "General",
            Topic = "About BaoTools",
            TitleVi = "BaoTools là gì và các tính năng chính",
            TitleEn = "What is BaoTools & Core Capabilities",
            Keywords = new[] { "baotools là gì", "baotools la gi", "what is baotools", "giới thiệu baotools", "tính năng baotools", "bao tools la gi", "bao tools là gì" },
            IntentHints = new[] { "about_baotools" },
            RelatedTools = Array.Empty<string>(),
            Priority = 40,
            ContentVi = "BaoTools là bộ công cụ tối ưu và quản lý game Steam chuyên nghiệp: nạp game qua cấu hình .lua, tải game tốc độ cao, sửa lỗi crash (gỡ Steam DRM Steamless), quản lý plugin Steam Store và chẩn đoán sự cố kỹ thuật tự động.",
            ContentEn = "BaoTools is a professional Steam game manager and optimizer: inject games via .lua, high-speed game downloads, crash repair (Steamless DRM removal), Steam Store plugin integration, and automated technical diagnostics."
        }
    };

    /// <summary>
    /// Searches local knowledge articles based on query tokens, intent hints, and relevance scoring.
    /// Filters out articles below the confidence threshold to prevent false positives.
    /// </summary>
    public static List<KnowledgeArticle> SearchKnowledge(string query, int topN = 3)
    {
        return SearchKnowledgeWithScore(query, topN).Select(s => s.Article).ToList();
    }

    /// <summary>
    private static bool _systemKnowledgeIngested = false;
    private static readonly object _syncLock = new();

    public static void IngestSystemKnowledge()
    {
        if (_systemKnowledgeIngested) return;
        lock (_syncLock)
        {
            if (_systemKnowledgeIngested) return;
            try
            {
                var store = AI.Knowledge.SystemKnowledgeStore.Instance;
                // 1. Ingest Settings
                foreach (var s in store.Settings)
                {
                    Articles.Add(new KnowledgeArticle
                    {
                        Id = $"sys_setting_{s.Id}",
                        Category = "Settings",
                        TitleVi = $"Cài đặt: {s.NameVi} ({s.LocationVi})",
                        TitleEn = $"Setting: {s.NameEn} ({s.LocationEn})",
                        ContentVi = $"• **Tên cài đặt**: {s.NameVi}\n• **Vị trí**: {s.LocationVi}\n• **Mục đích**: {s.PurposeVi}\n• **Giá trị mặc định**: {s.DefaultValue}\n• **Thuộc tính code**: `{s.Property}`",
                        ContentEn = $"• **Setting**: {s.NameEn}\n• **Location**: {s.LocationEn}\n• **Purpose**: {s.PurposeEn}\n• **Default value**: {s.DefaultValue}\n• **Code property**: `{s.Property}`",
                        Keywords = new[] { s.Property.ToLowerInvariant(), s.NameVi.ToLowerInvariant(), s.NameEn.ToLowerInvariant(), "cài đặt", "setting", "chỉnh", "đổi", s.Id },
                        RelatedTools = new[] { "navigate_settings" },
                        Priority = 16
                    });
                }

                // 2. Ingest Screens
                foreach (var scr in store.Screens)
                {
                    string toolNav = scr.Id switch
                    {
                        "screen_home" => "navigate_home",
                        "screen_add" => "navigate_add",
                        "screen_manage" => "navigate_manage",
                        "screen_builds" => "navigate_builds",
                        "screen_mode" => "navigate_mode",
                        "screen_fixes" => "navigate_fixes",
                        "screen_plugin" => "navigate_plugin",
                        "screen_downloads" => "navigate_downloads",
                        "screen_settings" => "navigate_settings",
                        _ => "navigate_manage"
                    };

                    Articles.Add(new KnowledgeArticle
                    {
                        Id = $"sys_screen_{scr.Id}",
                        Category = "Screens",
                        TitleVi = $"Tab: {scr.NameVi}",
                        TitleEn = $"Tab: {scr.NameEn}",
                        ContentVi = $"• **Vị trí**: {scr.NameVi} ở thanh điều hướng bên trái.\n• **Chức năng chính**: {scr.PurposeVi}\n• **Bao gồm các mục**: {string.Join(", ", scr.Sections)}.",
                        ContentEn = $"• **Location**: {scr.NameEn} on the left navigation bar.\n• **Main Purpose**: {scr.PurposeEn}\n• **Key Sections**: {string.Join(", ", scr.Sections)}.",
                        Keywords = new[] { scr.NameVi.ToLowerInvariant(), scr.NameEn.ToLowerInvariant(), scr.Id, "màn hình", "tab", "trang", "giao diện", "screen", "view" },
                        RelatedTools = new[] { toolNav },
                        Priority = 10
                    });
                }

                // 3. Ingest Workflows
                foreach (var wf in store.Workflows)
                {
                    Articles.Add(new KnowledgeArticle
                    {
                        Id = $"sys_wf_{wf.Id}",
                        Category = "Workflows",
                        TitleVi = $"Quy trình: {wf.NameVi}",
                        TitleEn = $"Workflow: {wf.NameEn}",
                        ContentVi = $"{wf.SummaryVi}\n\n**Các bước thực hiện:**\n{string.Join("\n", wf.StepsVi)}",
                        ContentEn = $"{wf.SummaryEn}\n\n**Execution Steps:**\n{string.Join("\n", wf.StepsEn)}",
                        Keywords = new[] { wf.NameVi.ToLowerInvariant(), wf.NameEn.ToLowerInvariant(), wf.Id, "quy trình", "hướng dẫn", "cách làm", "workflow" },
                        Priority = 20
                    });
                }

                // 4. Ingest Limitations
                foreach (var lim in store.Limitations)
                {
                    Articles.Add(new KnowledgeArticle
                    {
                        Id = $"sys_limit_{lim.Id}",
                        Category = "Limitations",
                        TitleVi = $"Giới hạn hệ thống: {lim.Scope}",
                        TitleEn = $"System Limitation: {lim.Scope}",
                        ContentVi = $"• **Phạm vi**: {lim.Scope}\n• **Chi tiết**: {lim.DescriptionVi}\n• **Giải pháp**: {lim.WorkaroundVi}",
                        ContentEn = $"• **Scope**: {lim.Scope}\n• **Detail**: {lim.DescriptionEn}\n• **Workaround**: {lim.WorkaroundEn}",
                        Keywords = new[] { lim.Scope.ToLowerInvariant(), lim.Id, "giới hạn", "hạn chế", "limitation", "quota", "không được" },
                        Priority = 14
                    });
                }
            }
            catch { }
            _systemKnowledgeIngested = true;
        }
    }

    /// <summary>
    /// Searches local knowledge articles returning both the article and computed relevance score.
    /// </summary>
    public static List<(KnowledgeArticle Article, int Score)> SearchKnowledgeWithScore(string query, int topN = 3)
    {
        IngestSystemKnowledge();
        if (string.IsNullOrWhiteSpace(query)) return new List<(KnowledgeArticle, int)>();

        string normalized = query.Trim().ToLowerInvariant();
        var words = normalized.Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '/', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                              .Where(w => w.Length > 1)
                              .ToArray();

        var scored = new List<(KnowledgeArticle Article, int Score)>();

        foreach (var art in Articles)
        {
            int score = 0;

            // 1. Direct keyword match
            foreach (var kw in art.Keywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                string lkw = kw.ToLowerInvariant();
                if (normalized.Contains(lkw))
                {
                    // Exact keyword/phrase match
                    score += 35;
                }
                else
                {
                    foreach (var w in words)
                    {
                        if (lkw.Contains(w)) score += 6;
                    }
                }
            }

            // 2. Intent hints match
            foreach (var hint in art.IntentHints)
            {
                if (!string.IsNullOrWhiteSpace(hint) && normalized.Contains(hint.ToLowerInvariant()))
                {
                    score += 20;
                }
            }

            // 3. Bilingual Title match
            string titleVi = art.TitleVi.ToLowerInvariant();
            string titleEn = art.TitleEn.ToLowerInvariant();
            foreach (var w in words)
            {
                if (titleVi.Contains(w) || titleEn.Contains(w)) score += 8;
            }

            // 4. Bilingual Content match
            string contentVi = art.ContentVi.ToLowerInvariant();
            string contentEn = art.ContentEn.ToLowerInvariant();
            foreach (var w in words)
            {
                if (contentVi.Contains(w) || contentEn.Contains(w)) score += 2;
            }

            // 5. Priority boost
            if (score > 0)
            {
                score += art.Priority;
            }

            // Confidence threshold: at least 18 points required to prevent unrelated noise
            if (score >= 18)
            {
                scored.Add((art, score));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Builds a formatted context string of relevant knowledge articles for injection into prompt.
    /// Automatically adapts to the user's query language (Vietnamese vs English).
    /// </summary>
    public static string BuildRagContext(string query, int topN = 2, bool? forceVietnamese = null)
    {
        var relevant = SearchKnowledge(query, topN);
        if (relevant.Count == 0) return "";

        bool isVi = forceVietnamese ?? IsVietnameseQuery(query);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(isVi ? "=== TÀI LIỆU HƯỚNG DẪN BAOTOOLS LIÊN QUAN ===" : "=== RELEVANT BAOTOOLS DOCUMENTATION ===");

        foreach (var art in relevant)
        {
            sb.AppendLine($"• [{(isVi ? art.TitleVi : art.TitleEn)}]:");
            sb.AppendLine(isVi ? art.ContentVi : art.ContentEn);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Searches and returns the top relevant article formatted as a standalone answer.
    /// </summary>
    public static string FindRelevantAnswer(string query, bool isVi)
    {
        var relevant = SearchKnowledge(query, 1);
        if (relevant.Count > 0)
        {
            var art = relevant[0];
            return isVi
                ? $"### {art.TitleVi}\n\n{art.ContentVi}"
                : $"### {art.TitleEn}\n\n{art.ContentEn}";
        }
        return "";
    }
}

