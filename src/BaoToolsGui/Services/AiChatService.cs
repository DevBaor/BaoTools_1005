using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BaoToolsGui.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using BaoToolsGui.Services.Ai;

namespace BaoToolsGui.Services;

public partial class AiChatMessage : ObservableObject
{
    public string Role { get; set; } = "user"; // "user" or "model" / "assistant"
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsUser => Role == "user";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyIcon))]
    [NotifyPropertyChangedFor(nameof(CopyTooltip))]
    private bool _isCopied;

    public string CopyIcon => IsCopied ? "Checkmark24" : "Copy24";
    public string CopyTooltip => IsCopied
        ? Resources.Strings.AiChat_Copied
        : Resources.Strings.AiChat_CopyMessage;

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(CopyTooltip));
    }

    // Technical Copilot rich elements
    public string? DiagnosisTitle { get; set; }
    public List<AiChecklistItem> Checklist { get; set; } = new();
    public List<AiChatAction> ActionButtons { get; set; } = new();

    public bool HasDiagnosisTitle => !string.IsNullOrWhiteSpace(DiagnosisTitle);
    public bool HasChecklist => Checklist != null && Checklist.Count > 0;
    public bool HasActionButtons => ActionButtons != null && ActionButtons.Count > 0;
}

public class AiChatService
{
    private readonly HttpClient _http = AppHttp.Create(TimeSpan.FromSeconds(15));
    private readonly SettingsService _settings;
    private readonly SteamService _steam;
    private readonly LuaVault _vault;
    private readonly UnlockerService? _unlocker;
    private readonly AiToolExecutor _toolExecutor;
    private readonly IAiProvider _localExpert;

    public AiToolExecutor ToolExecutor => _toolExecutor;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string response, DateTime expiry)> _cache = new();

    private static bool IsVietnamese =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    public AiChatService(SettingsService settings, SteamService steam, LuaVault vault, UnlockerService? unlocker = null)
    {
        _settings = settings;
        _steam = steam;
        _vault = vault;
        _unlocker = unlocker;
        _toolExecutor = new AiToolExecutor(steam, vault, settings, unlocker);
        _localExpert = new LocalExpertAiProvider(_toolExecutor);

        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        }
    }

    public async Task<string> SendMessageAsync(List<AiChatMessage> conversationHistory, string userMessage, string? currentContext = null, uint? selectedAppId = null)
    {
        var msg = await SendMessageFullAsync(conversationHistory, userMessage, currentContext, null, selectedAppId);
        return msg.Text;
    }

    public async Task<AiChatMessage> SendMessageFullAsync(
        List<AiChatMessage> conversationHistory,
        string userMessage,
        string? currentContext = null,
        Action<string>? onProgressStep = null,
        uint? selectedAppId = null)
    {
        bool isVi = BaoToolsKnowledgeBase.IsVietnameseQuery(userMessage);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new AiChatMessage
            {
                Role = "model",
                Text = isVi
                    ? "Bạn cần mình hỗ trợ điều gì nè? Hãy nhập câu hỏi hoặc chọn các gợi ý bên dưới nhé!"
                    : "How can I help you? Please enter your question or pick a topic below!"
            };
        }

        string cleanQuery = userMessage.Trim();

        // 1. Capture live application context
        var appContext = BaoToolsContext.CollectLive(
            _steam,
            _vault,
            _settings,
            _unlocker,
            currentContext ?? "Home",
            selectedAppId: selectedAppId);

        var promptContext = new AiPromptContext
        {
            UserMessage = cleanQuery,
            ConversationHistory = conversationHistory,
            AppContext = appContext,
            IsVietnamese = isVi
        };

        // 2. LocalExpertAiProvider (100% offline, privacy-first technical copilot orchestrator)
        if (_localExpert is LocalExpertAiProvider localEngine)
        {
            var localMsg = await localEngine.GenerateResponseFullAsync(promptContext, onProgressStep);
            localMsg.Text = CleanText(localMsg.Text);
            return localMsg;
        }

        // 4. Fall back to standard response synthesis if necessary
        string rawResponse = await GenerateResponseAsync(conversationHistory, cleanQuery, currentContext, appContext);
        string finalClean = CleanText(rawResponse);

        var finalMessage = new AiChatMessage
        {
            Role = "model",
            Text = finalClean
        };

        AttachContextualActions(finalMessage, cleanQuery, isVi);
        return finalMessage;
    }

    private void AttachContextualActions(AiChatMessage msg, string query, bool isVi)
    {
        string q = query.ToLowerInvariant();
        if (q.Contains("plugin"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🧩 Quản lý Plugin" : "🧩 Manage Plugin",
                ToolName = "navigate_plugin",
                IconSymbol = "PlugDisconnected24"
            });
        }
        else if (q.Contains("fix") || q.Contains("sửa") || q.Contains("sua"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🛠 Bản sửa lỗi (Fixes)" : "🛠 Game Fixes",
                ToolName = "navigate_fixes",
                IconSymbol = "Wrench24"
            });
        }
        else if (q.Contains("game") || q.Contains("quản lý") || q.Contains("quan ly"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
        }
    }

    private async Task<string> GenerateResponseAsync(
        List<AiChatMessage> conversationHistory,
        string userMessage,
        string? currentContext = null,
        BaoToolsContext? appContext = null)
    {
        string query = userMessage.Trim().ToLowerInvariant();
        bool isVi = BaoToolsKnowledgeBase.IsVietnameseQuery(userMessage);

        // 1. Tool Calling execution (check plugin, restart steam, diagnose system, app summary)
        var toolResult = await _toolExecutor.DetectAndExecuteToolAsync(userMessage);
        if (toolResult.Handled && !string.IsNullOrWhiteSpace(toolResult.Output))
        {
            return toolResult.Output;
        }

        // 2. Chào hỏi / Xã giao
        if (query == "chào" || query == "chao" || query == "xin chào" || query == "xin chao" ||
            query == "hello" || query == "hi" || query == "hey" || query == "alo" ||
            query == "ê bot" || query == "e bot" || query.Contains("bạn là ai") || query.Contains("ban la ai"))
        {
            await Task.Delay(150);
            string persona = SteamService.SteamPersonaName ?? "";
            string greetName = !string.IsNullOrWhiteSpace(persona) ? $" {persona}" : "";
            return isVi
                ? $"Chào{greetName}! Mình là Trợ lý Kỹ thuật & Chẩn đoán của BaoTools.\n\nMình có thể hỗ trợ bạn:\n• Hướng dẫn cách thêm và tải game vào Steam\n• Kiểm tra tình trạng Plugin và chẩn đoán hệ thống Steam\n• Cách xử lý game bị văng / crash (gỡ Steam DRM Steamless)\n• Sửa lỗi thiếu DLC hoặc depot\n• Hướng dẫn chơi Online Co-op qua Steam\n• Tối ưu FPS và sửa các lỗi thiếu file DLL\n\nBạn đang cần hỗ trợ phần nào, cứ nhắn cho mình nhé!"
                : $"Hello{greetName}! I am the BaoTools AI Technical Assistant.\n\nI can help you with:\n• Adding and downloading games into Steam\n• Checking plugin status & Steam system diagnostics\n• Fixing launch crashes (removing Steam DRM with Steamless)\n• Resolving missing DLC or depots\n• Setting up Online Co-op multiplayer via Steam\n• Optimizing FPS and fixing missing DLL errors\n\nWhat can I assist you with today?";
        }

        // 3. Cảm ơn
        if (query == "cảm ơn" || query == "cam on" || query == "thanks" || query == "thank you" ||
            query.StartsWith("cảm ơn") || query.StartsWith("cam on") || query.StartsWith("thank"))
        {
            await Task.Delay(150);
            return isVi
                ? "Dạ không có gì ạ! Rất vui được hỗ trợ bạn. Chúc bạn có những giờ phút chơi game thật vui vẻ nhé! Cần gì thêm bạn cứ nhắn mình. 😊"
                : "You're very welcome! Have fun gaming. Feel free to ask if you ever need anything else! 😊";
        }

        // 3. Chẩn đoán trực tiếp tình trạng Steam
        if (query.Contains("chẩn đoán") || query.Contains("chan doan") ||
            query.Contains("kiểm tra steam") || query.Contains("kiem tra steam") ||
            query.Contains("check steam") || query.Contains("diagnostic") || query.Contains("diagnose") ||
            query.Contains("kiểm tra hệ thống") || query.Contains("system check") ||
            query.Contains("sức khỏe hệ thống"))
        {
            await Task.Delay(200);
            return RunSystemDiagnostics(isVi);
        }

        // 4. Kiểm tra hoặc Hướng dẫn Plugin BaoTools (Nút thêm game trên Steam Store)
        if (query.Contains("plugin") || query.Contains("nút tím") || query.Contains("nut tim") ||
            query.Contains("nút store") || query.Contains("nut store") || query.Contains("nút thêm game") ||
            query.Contains("winmm.dll") || query.Contains("tiện ích"))
        {
            await Task.Delay(200);
            return CheckPluginStatus(isVi);
        }

        // 5. Lỗi thiếu DLL, 0xc000007b, Visual C++, DirectX (Kiểm tra trước khi check crash)
        if (query.Contains("dll") || query.Contains("0xc000007b") || query.Contains("visual c") ||
            query.Contains("directx") || query.Contains("vcredist") || query.Contains("mfc140") ||
            query.Contains("msvcp") || query.Contains("d3d") || query.Contains("thiếu file hệ thống"))
        {
            await Task.Delay(250);
            return GetGuideDllFix(isVi);
        }

        // 6. Không thấy game trong thư viện Steam
        if ((query.Contains("không thấy") || query.Contains("khong thay") ||
             query.Contains("không hiện") || query.Contains("khong hien") ||
             query.Contains("không nhận") || query.Contains("khong nhan") ||
             query.Contains("mất game") || query.Contains("mat game") ||
             query.Contains("not show") || query.Contains("missing in library")) &&
            (query.Contains("thư viện") || query.Contains("thu vien") || query.Contains("library") || query.Contains("steam")))
        {
            await Task.Delay(250);
            return GetTroubleshootingMissingLibrary(isVi);
        }

        // 7. Sửa lỗi thiếu DLC / Thiếu Depot
        if (query.Contains("thiếu dlc") || query.Contains("thieu dlc") ||
            query.Contains("thiếu depot") || query.Contains("thieu depot") ||
            query.Contains("chưa có dlc") || query.Contains("chua co dlc") ||
            query.Contains("missing dlc") || query.Contains("missing depot"))
        {
            await Task.Delay(250);
            return GetTroubleshootingDepots(isVi);
        }

        // 8. Bản sửa lỗi / Bản sửa chữa (Tab Fixes / Denuvo)
        if (query.Contains("sửa chữa") || query.Contains("sua chua") ||
            query.Contains("bản sửa") || query.Contains("ban sua") ||
            query.Contains("bản fix") || query.Contains("ban fix") ||
            query.Contains("tab fix") || query.Contains("denuvo") ||
            query.Contains("crack fix") || query.Contains("patch fix") ||
            (query.Contains("fix") && !query.Contains("online")))
        {
            await Task.Delay(250);
            return GetGuideFixes(isVi);
        }

        // 9. Crash / Văng game / Không mở được / Steamless DRM
        if (query.Contains("crash") || query.Contains("văng") || query.Contains("vang") ||
            query.Contains("steamless") || query.Contains("không mở được") || query.Contains("khong mo duoc") ||
            query.Contains("mở không lên") || query.Contains("mo khong len") ||
            query.Contains("không vào được") || query.Contains("khong vao duoc") ||
            query.Contains("đen màn hình") || query.Contains("den man hinh") ||
            query.Contains("bị văng") || query.Contains("bị tắt") ||
            query.Contains("won't open") || query.Contains("won't launch"))
        {
            await Task.Delay(250);
            return GetTroubleshootingCrash(isVi);
        }

        // 10. Chơi Online Co-op / Spacewar / OnlineFix
        if (query.Contains("online") || query.Contains("coop") || query.Contains("co-op") ||
            query.Contains("multiplayer") || query.Contains("chơi chung") || query.Contains("choi chung") ||
            query.Contains("chơi cùng bạn") || query.Contains("choi cung ban") ||
            query.Contains("spacewar") || query.Contains("480") || query.Contains("onlinefix"))
        {
            await Task.Delay(250);
            return GetGuideOnlineFix(isVi);
        }

        // 11. Hướng dẫn cách thêm và tải game
        if (query.Contains("thêm game") || query.Contains("them game") ||
            query.Contains("cách tải game") || query.Contains("cach tai game") ||
            query.Contains("hướng dẫn thêm") || query.Contains("huong dan them") ||
            query.Contains("hướng dẫn tải") || query.Contains("huong dan tai") ||
            query.Contains("cách dùng baotools") || query.Contains("cach dung baotools") ||
            query.Contains("cách lấy game") || query.Contains("cach lay game") ||
            query.Contains("nạp lua") || query.Contains("nap lua") ||
            query.Contains("how to add game") || query.Contains("how to download game"))
        {
            await Task.Delay(250);
            return GetGuideAddGame(isVi);
        }

        // 12. Tải chậm / Tiến trình tải
        if (query.Contains("tải chậm") || query.Contains("tai cham") ||
            query.Contains("tốc độ tải") || query.Contains("toc do tai") ||
            query.Contains("tải lâu") || query.Contains("tai lau") ||
            query.Contains("slow download") || query.Contains("download progress"))
        {
            await Task.Delay(250);
            return GetGuideDownloads(isVi);
        }

        // 13. Khởi động lại Steam
        if (query.Contains("restart steam") || query.Contains("khởi động lại steam") ||
            query.Contains("khoi dong lai steam") || query.Contains("reboot steam") ||
            query.Contains("bật lại steam") || query.Contains("bat lai steam"))
        {
            await Task.Delay(250);
            return GetGuideRestartSteam(isVi);
        }

        // 14. Quản lý & Xóa game khỏi Steam
        if (query.Contains("xóa game") || query.Contains("xoa game") ||
            query.Contains("gỡ game") || query.Contains("go game") ||
            query.Contains("gỡ cài đặt") || query.Contains("go cai dat") ||
            query.Contains("xóa khỏi steam") || query.Contains("xoa khoi steam") ||
            query.Contains("gỡ khỏi steam") || query.Contains("go khoi steam") ||
            query.Contains("delete game") || query.Contains("uninstall game") ||
            query.Contains("remove game") || query.Contains("how to delete") || query.Contains("how to uninstall") ||
            ((query.Contains("xóa") || query.Contains("xoa") || query.Contains("gỡ") || query.Contains("go")) && (query.Contains("game") || query.Contains("trò chơi"))))
        {
            await Task.Delay(250);
            return GetGuideManageGame(isVi);
        }

        // 15. Tối ưu FPS, giảm giật lag khi chơi game
        if (query.Contains("fps") || query.Contains("lag") || query.Contains("giật") || query.Contains("giat") ||
            query.Contains("tối ưu") || query.Contains("mượt") || query.Contains("optimize") || query.Contains("stutter"))
        {
            await Task.Delay(250);
            return GetGuideOptimization(isVi);
        }

        // 16. Chế độ Mode / Unlocker
        if (query.Contains("tab mode") || query.Contains("chế độ unlocker") || query.Contains("smokeapi") ||
            query.Contains("koaloader") || query.Contains("creamapi") || query.Contains("bettersteamtools") ||
            query.Contains("opensteamtools") || query.Contains("cloudredirect"))
        {
            await Task.Delay(250);
            return GetGuideModes(isVi);
        }

        // 17. Chức năng / Tính năng của bot
        if (query.Contains("làm được gì") || query.Contains("lam duoc gi") ||
            query.Contains("chức năng") || query.Contains("tính năng") ||
            query.Contains("giúp được gì") || query.Contains("what can you do"))
        {
            await Task.Delay(200);
            return GetSmartGeneralHelp(isVi);
        }

        // ── 18. Local RAG Retrieval & Model Router ─────────────────────
        string ragContext = BaoToolsKnowledgeBase.BuildRagContext(userMessage, topN: 2, forceVietnamese: isVi);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

            // Free AI (Pollinations with RAG context)
            string fastAiReply = await CallFastAiAsync(userMessage, ragContext, cts.Token, isVi);
            if (!string.IsNullOrWhiteSpace(fastAiReply))
            {
                return fastAiReply;
            }
        }
        catch { }

        // Tier 3: Local Offline RAG Engine (Guarantees every BaoTools question has an accurate answer)
        var localMatches = BaoToolsKnowledgeBase.SearchKnowledge(userMessage, topN: 1);
        if (localMatches.Count > 0)
        {
            var match = localMatches[0];
            return isVi
                ? $"Hướng dẫn về {match.TitleVi}:\n\n{match.ContentVi}"
                : $"Guide for {match.TitleEn}:\n\n{match.ContentEn}";
        }

        // Phản hồi thông minh nếu câu hỏi không xác định
        return GetSmartGeneralHelp(isVi);
    }
    private string GetSystemPrompt(string? ragContext = null, bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        var sb = new StringBuilder();

        if (isVi)
        {
            sb.AppendLine("Bạn là Trợ lý AI Kỹ thuật thông minh và thân thiện của phần mềm BaoTools (công cụ hỗ trợ và quản lý game Steam trên Windows).");
            sb.AppendLine();
            sb.AppendLine("Quy tắc trả lời:");
            sb.AppendLine("1. Trả lời bằng TIẾNG VIỆT tự nhiên, ngắn gọn, súc tích, đi thẳng vào câu hỏi của người dùng.");
            sb.AppendLine("2. Sử dụng các gạch đầu dòng rõ ràng (• hoặc 1., 2.).");
            sb.AppendLine("3. Tuyệt đối KHÔNG dùng ký tự markdown thô (như **, `) và không dùng tiêu đề in hoa giật gân.");
            sb.AppendLine("4. Giữ phong cách thân thiện, chu đáo và nhiệt tình.");
            sb.AppendLine("5. Luôn trả lời câu văn trọn vẹn, đầy đủ ý, không bao giờ ngắt lửng câu giữa chừng.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(ragContext))
            {
                sb.AppendLine(ragContext);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Kiến thức hệ thống BaoTools:");
                sb.AppendLine("• Tab Thêm game (Add): Tìm kiếm theo tên hoặc AppID Steam, bấm 'Thêm game' để nạp file .lua vào Steam.");
                sb.AppendLine("• Nút Restart Steam: Ở góc trên bên phải BaoTools, bấm sau khi thêm game để Steam nhận game vào Library.");
                sb.AppendLine("• Tab Quản lý (Manage): Quản lý game đã nạp, mở thư mục game, xóa game (gỡ .lua), và có tính năng 'Gỡ Steam DRM' (Steamless) để sửa lỗi game bị văng / crash khi khởi động, kèm thiết lập Launch Options (-dx11, -novid).");
                sb.AppendLine("• Tab Bản sửa lỗi (Fixes): Cung cấp crack fix, online fix, Denuvo bypass, update patch cho từng game.");
                sb.AppendLine("• Tab Online Fix: Hướng dẫn chơi multiplayer co-op qua Steam sử dụng AppID 480 (Spacewar).");
                sb.AppendLine("• Tab Mode: Chuyển đổi giữa BetterSteamTools (mặc định), OpenSteamTools (Nightly), Custom Unlocker, và CloudRedirect.");
                sb.AppendLine("• Tab Builds: Quản lý và tải manifest / depot game.");
                sb.AppendLine("• Plugin BaoTools: Tiện ích tạo nút màu tím 'Thêm qua BaoTools' trực tiếp trên trang Steam Store.");
                sb.AppendLine("• Sửa lỗi phổ biến: Game văng -> Dùng Gỡ Steam DRM; Không thấy game -> Bấm Restart Steam; Thiếu file -> Cài Visual C++ 2015-2022 & DirectX.");
            }
        }
        else
        {
            string currentLang = CultureInfo.CurrentUICulture.DisplayName;
            sb.AppendLine("You are the friendly and intelligent Technical Assistant for BaoTools (Steam game management and unlocker tool for Windows).");
            sb.AppendLine();
            sb.AppendLine("Core Response Rules:");
            sb.AppendLine("1. Language: Always respond in the EXACT same language that the user wrote their message in (e.g., if the user writes in Russian, reply in Russian; if Chinese, reply in Chinese; if Spanish, reply in Spanish; if Portuguese, reply in Portuguese; if Japanese, reply in Japanese; etc.). If the language cannot be identified, reply in " + currentLang + " or English.");
            sb.AppendLine("2. Style: Answer concisely, helpfully, and naturally in plain text with clean bullet points (• or 1., 2.).");
            sb.AppendLine("3. Formatting: Absolutely avoid raw markdown symbols like ** or ` and avoid sensational screaming headers.");
            sb.AppendLine("4. Tone: Helpful, polite, and technically accurate.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(ragContext))
            {
                sb.AppendLine(ragContext);
            }
        }

        return sb.ToString();
    }

    private async Task<string> CallFastAiAsync(string userMessage, string? ragContext, CancellationToken ct, bool isVi = true)
    {
        string sysMsg = GetSystemPrompt(ragContext, isVi);

        // Thử POST JSON
        try
        {
            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = sysMsg },
                    new { role = "user", content = userMessage }
                },
                model = "openai"
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("https://text.pollinations.ai/", content, ct);
            if (response.IsSuccessStatusCode)
            {
                string reply = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(reply) && !reply.Contains("error code:") && !reply.Contains("cloudflare"))
                    return reply.Trim();
            }
        }
        catch { }

        // Thử GET trực tiếp
        try
        {
            string prompt = $"{sysMsg}\nUser: {userMessage}";
            string encoded = Uri.EscapeDataString(prompt);
            var response = await _http.GetAsync($"https://text.pollinations.ai/{encoded}?model=openai-fast", ct);
            if (response.IsSuccessStatusCode)
            {
                string reply = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(reply) && !reply.Contains("error code:"))
                    return reply.Trim();
            }
        }
        catch { }

        return "";
    }

    public string RunSystemDiagnostics(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        var sb = new StringBuilder();

        bool steamValid = _steam.IsValid;
        string? steamPath = _steam.EffectivePath;
        bool steamRunning = SteamService.IsSteamRunning();
        string? pluginDir = _steam.StPlugInDir;
        int luaCount = !string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir)
            ? Directory.EnumerateFiles(pluginDir, "*.lua").Count()
            : 0;
        string activeMode = _settings.SelectedMode ?? (isVi ? "Mặc định (BetterSteamTools)" : "Default (BetterSteamTools)");

        if (isVi)
        {
            sb.AppendLine("Báo cáo kiểm tra hệ thống Steam & BaoTools:");
            sb.AppendLine();
            if (steamValid && !string.IsNullOrEmpty(steamPath))
            {
                sb.AppendLine($"• Thư mục Steam: {steamPath}");
            }
            else
            {
                sb.AppendLine("• Thư mục Steam: Chưa nhận diện được thư mục cài đặt");
                sb.AppendLine("  (Khắc phục: Vào Cài đặt để chọn đúng thư mục chứa steam.exe)");
            }

            if (steamRunning)
            {
                sb.AppendLine("• Trạng thái Steam: Đang hoạt động bình thường");
            }
            else
            {
                sb.AppendLine("• Trạng thái Steam: Đang tắt (Hãy mở Steam để nạp game)");
            }

            if (!string.IsNullOrEmpty(pluginDir) && Directory.Exists(pluginDir))
            {
                sb.AppendLine($"• Số game đã nạp vào Steam: {luaCount} game (.lua)");
            }
            else
            {
                sb.AppendLine("• Thư mục Plugin: Chưa có game nào được nạp");
            }

            sb.AppendLine($"• Chế độ Unlocker: {activeMode}");
            sb.AppendLine();

            if (steamValid && luaCount > 0)
            {
                sb.AppendLine("Đánh giá: Mọi thành phần đều hoạt động tốt! Nếu game mở lên bị văng, bạn hãy dùng tính năng 'Gỡ Steam DRM' trong tab Quản lý nhé.");
            }
            else if (!steamValid)
            {
                sb.AppendLine("Cần xử lý: Vui lòng vào tab Cài đặt để chọn lại thư mục Steam.");
            }
            else
            {
                sb.AppendLine("Gợi ý: Hãy vào tab Thêm game (Add) để bắt đầu tìm và nạp game yêu thích vào Steam.");
            }
        }
        else
        {
            sb.AppendLine("Steam & BaoTools Diagnostic Report:");
            sb.AppendLine();
            if (steamValid && !string.IsNullOrEmpty(steamPath))
            {
                sb.AppendLine($"• Steam Directory: {steamPath}");
            }
            else
            {
                sb.AppendLine("• Steam Directory: Not detected (Please configure in Settings)");
            }

            sb.AppendLine($"• Steam Process: {(steamRunning ? "Running" : "Offline")}");
            sb.AppendLine($"• Games added to Steam: {luaCount} titles");
            sb.AppendLine($"• Unlocker Mode: {activeMode}");
            sb.AppendLine();
            sb.AppendLine(steamValid && luaCount > 0
                ? "Assessment: System is ready! If a game crashes, use 'Remove Steam DRM' in the Manage tab."
                : "Tip: Head to the Add tab to search and add your favorite games.");
        }

        return sb.ToString();
    }

    public string CheckPluginStatus(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string frontendPath = Path.Combine(appData, "BaoToolsGui", "plugin");
        bool frontendInstalled = Directory.Exists(frontendPath);

        string? steamPath = _steam.EffectivePath;
        bool dllInstalled = !string.IsNullOrEmpty(steamPath) && File.Exists(Path.Combine(steamPath, "winmm.dll"));

        var sb = new StringBuilder();
        if (isVi)
        {
            sb.AppendLine("Tình trạng Plugin BaoTools trên Steam Store:");
            sb.AppendLine();

            if (frontendInstalled && dllInstalled)
            {
                sb.AppendLine("• Trạng thái: Đã cài đặt hoàn tất và sẵn sàng!");
                sb.AppendLine("• Giao diện Plugin: Đã cài (%AppData%\\BaoToolsGui\\plugin)");
                sb.AppendLine("• DLL Loader (winmm.dll): Đã nạp vào Steam");
                sb.AppendLine();
                sb.AppendLine("Khi bạn mở Steam và duyệt trang Cửa hàng (Steam Store), bạn sẽ thấy nút màu tím 'Thêm qua BaoTools' hiển thị ngay trên trang game để thêm game 1-click rất tiện lợi.");
            }
            else if (frontendInstalled || dllInstalled)
            {
                sb.AppendLine("• Trạng thái: Cài đặt chưa đầy đủ");
                sb.AppendLine($"• Giao diện: {(frontendInstalled ? "Đã có" : "Thiếu")} | DLL Loader: {(dllInstalled ? "Đã có" : "Thiếu")}");
                sb.AppendLine();
                sb.AppendLine("Khắc phục: Bạn vào tab Plugin ở menu bên trái rồi bấm 'Cài đặt lại' nhé.");
            }
            else
            {
                sb.AppendLine("• Trạng thái: Bạn chưa cài đặt Plugin BaoTools");
                sb.AppendLine();
                sb.AppendLine("Cách cài đặt rất đơn giản:");
                sb.AppendLine("1. Bấm vào tab Plugin ở menu bên trái.");
                sb.AppendLine("2. Bấm nút 'Cài đặt Plugin'.");
                sb.AppendLine("3. Khởi động lại Steam là nút thêm game màu tím sẽ tự xuất hiện trên Steam Store!");
            }
        }
        else
        {
            sb.AppendLine("BaoTools Steam Store Plugin Status:");
            sb.AppendLine();

            if (frontendInstalled && dllInstalled)
            {
                sb.AppendLine("• Status: Fully installed and active!");
                sb.AppendLine("When browsing the Steam Store, the purple 'Add with BaoTools' button will be available on store pages.");
            }
            else
            {
                sb.AppendLine("• Status: Not installed yet");
                sb.AppendLine("Go to the Plugin tab on the left sidebar and click 'Install Plugin', then restart Steam.");
            }
        }

        return sb.ToString();
    }

    private string GetGuideFixes(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Để sử dụng tab Bản sửa lỗi (Fixes), bạn làm theo các bước này nhé:\n\n" +
                "1. Vào tab Bản sửa lỗi ở menu bên trái.\n" +
                "2. Gõ tên game hoặc AppID vào thanh tìm kiếm.\n" +
                "3. Bấm vào game để xem các bản vá có sẵn (Crack Fix, Online Fix, Denuvo Bypass, Update Patch...).\n" +
                "4. Bấm 'Cài đặt Fix', BaoTools sẽ tự động tải và chép các file sửa lỗi vào đúng thư mục game trên máy bạn.\n\n" +
                "Mẹo: Bạn có thể bật công tắc 'Chỉ hiện game của tôi' để lọc nhanh các bản sửa lỗi cho những game đã cài sẵn trên máy.";
        }
        else
        {
            return
                "How to use the Game Fixes tab:\n\n" +
                "1. Click the 'Fixes' tab on the left sidebar.\n" +
                "2. Search for your game title or AppID.\n" +
                "3. Click on the game to view available fixes (Crack Fix, Online Fix, Denuvo Bypass, Update Patch).\n" +
                "4. Click 'Install Fix' - BaoTools automatically downloads and extracts the repair files into your game directory.\n\n" +
                "Tip: Turn on 'Only show my games' to quickly filter fixes for games installed on your PC.";
        }
    }

    private string GetGuideAddGame(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Để thêm và tải game mới vào Steam, bạn làm theo 3 bước sau nhé:\n\n" +
                "1. Tìm game: Vào tab Thêm game (Add) ở menu bên trái, gõ tên game hoặc Steam AppID rồi nhấn Enter.\n" +
                "2. Nạp game: Bấm vào game bạn muốn -> Chọn 'Thêm game' (Tải Lua). BaoTools sẽ tự nạp cấu hình mở khóa vào Steam.\n" +
                "3. Tải trên Steam: Mở Steam lên (hoặc bấm nút 'Restart Steam' ở góc trên bên phải). Vào Thư viện (Library) của Steam, game sẽ xuất hiện và có sẵn nút Cài đặt để bạn tải về bình thường!\n\n" +
                "Mẹo: Nếu đã cài Plugin, bạn có thể bấm trực tiếp nút màu tím 'Thêm qua BaoTools' ngay trên trang Steam Store của game.";
        }
        else
        {
            return
                "How to add and download games into Steam:\n\n" +
                "1. Search: Go to the 'Add' tab on the left, type the game name or AppID, and hit Enter.\n" +
                "2. Add: Click on the game -> Select 'Add Game' (Download Lua). BaoTools injects the configuration into Steam.\n" +
                "3. Download in Steam: Restart Steam (or click the Restart Steam button at top-right). Go to your Steam Library, and click Install / Download!\n\n" +
                "Tip: With the Plugin installed, you can also use the purple 'Add with BaoTools' button directly on Steam Store pages.";
        }
    }

    private string GetTroubleshootingCrash(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Nếu game bị văng (crash) hoặc không mở được, nguyên nhân phổ biến nhất là do lớp bảo vệ SteamStub DRM của game. Bạn xử lý theo các bước sau nhé:\n\n" +
                "1. Gỡ Steam DRM (Steamless) - Quan trọng nhất:\n" +
                "• Vào tab Quản lý (Manage) trong BaoTools.\n" +
                "• Bấm vào game đang bị lỗi -> Chọn 'Gỡ Steam DRM' (Steamless). Tool sẽ tự giải mã file .exe của game.\n\n" +
                "2. Khởi động lại Steam:\n" +
                "• Bấm nút 'Restart Steam' ở góc trên cùng bên phải để Steam tải lại toàn bộ plugin.\n\n" +
                "3. Đổi Chế độ Mở khóa (Mode):\n" +
                "• Vào tab Mode -> Thử chuyển sang BetterSteamTools hoặc OpenSteamTools xem chế độ nào tương thích tốt nhất với game đó.\n\n" +
                "4. Cài đủ thư viện hệ thống:\n" +
                "• Cài đặt Visual C++ Redistributable (2015-2022) và DirectX End-User Runtimes.";
        }
        else
        {
            return
                "If a game crashes on launch or closes immediately, it is usually caused by SteamStub DRM. Follow these steps:\n\n" +
                "1. Remove Steam DRM (Steamless) - Most Important:\n" +
                "• Go to the 'Manage' tab in BaoTools.\n" +
                "• Click on the game -> Select 'Remove Steam DRM' (Steamless) to unpack the game executable.\n\n" +
                "2. Restart Steam:\n" +
                "• Click 'Restart Steam' at the top-right to reload all plugins.\n\n" +
                "3. Switch Unlocker Mode:\n" +
                "• Go to the 'Mode' tab -> Try switching to BetterSteamTools or OpenSteamTools.\n\n" +
                "4. Install System Runtimes:\n" +
                "• Ensure Visual C++ (2015-2022) and DirectX runtimes are installed.";
        }
    }

    private string GetTroubleshootingDepots(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Khi game bị thiếu DLC hoặc thiếu file depot (Steam tải dung lượng quá nhỏ hoặc vào game không có DLC):\n\n" +
                "1. Kiểm tra Depots: Vào tab Quản lý (Manage) -> Chọn game -> Xem danh sách Depots / DLC xem gói nào chưa có manifest.\n" +
                "2. Tải Manifest: Vào tab Builds -> Tìm game đó để tải về file manifest chuẩn của các DLC/Depot còn thiếu.\n" +
                "3. Sau khi tải xong: Bấm nút 'Restart Steam' ở góc trên bên phải để Steam nạp lại nội dung.";
        }
        else
        {
            return
                "If a game is missing DLCs or depot files:\n\n" +
                "1. Check Depots: Go to the 'Manage' tab -> Select the game -> Inspect the Depots/DLC list.\n" +
                "2. Download Manifests: Go to the 'Builds' tab -> Search for the game and download missing manifest files.\n" +
                "3. Restart Steam: Click 'Restart Steam' at the top-right to let Steam refresh.";
        }
    }

    private string GetGuideOnlineFix(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Để chơi Online Co-op cùng bạn bè qua Steam:\n\n" +
                "1. Cài đặt Online Fix: Vào tab Fixes (hoặc Online Fix) -> Tìm game bạn muốn chơi -> Bấm 'Cài đặt Online Fix'.\n" +
                "2. Cơ chế hoạt động: Bản fix sử dụng Steam AppID 480 (Spacewar) làm cổng kết nối qua máy chủ Steam. Khi vào game, Steam sẽ hiển thị bạn đang chơi Spacewar.\n" +
                "3. Kết nối với bạn bè: Cả bạn và bạn bè đều phải mở Steam và cài cùng một bản Online Fix. Sau đó vào game tạo phòng và mời bạn qua Shift + Tab của Steam!";
        }
        else
        {
            return
                "How to play Online Co-op with friends:\n\n" +
                "1. Install Online Fix: In BaoTools, go to Fixes (or Online Fix) -> Search game -> Click 'Install Online Fix'.\n" +
                "2. How it works: It connects via Steam servers using AppID 480 (Spacewar).\n" +
                "3. Playing together: Both players must have Steam running and install the same fix version, then invite via Steam Shift + Tab overlay!";
        }
    }

    private string GetTroubleshootingMissingLibrary(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Nếu bạn đã thêm game vào BaoTools nhưng mở Steam vẫn chưa thấy trong Thư viện (Library):\n\n" +
                "1. Bấm nút 'Restart Steam': Nút tròn ở góc trên cùng bên phải BaoTools (Steam bắt buộc phải khởi động lại mới nạp game mới).\n" +
                "2. Kiểm tra bộ lọc Steam: Trong Steam Library, hãy đảm bảo bạn không bật bộ lọc 'Ready to play only' (Chỉ hiện game đã cài).\n" +
                "3. Kiểm tra đường dẫn Steam: Vào tab Cài đặt kiểm tra xem đường dẫn thư mục Steam đã trỏ đúng vào nơi có file steam.exe chưa.\n" +
                "4. Kiểm tra tab Quản lý: Xem game đó đã có trong danh sách tab Quản lý chưa, nếu chưa hãy bấm thêm lại nhé.";
        }
        else
        {
            return
                "If a game was added in BaoTools but does not show in your Steam Library:\n\n" +
                "1. Click 'Restart Steam': Top-right corner of BaoTools (Steam must restart to detect new manifests).\n" +
                "2. Steam Filter: Make sure the 'Ready to play only' filter in Steam Library is turned off.\n" +
                "3. Steam Path: In BaoTools Settings, verify that your Steam folder path is correctly set.\n" +
                "4. Check Manage Tab: Verify that the game is listed in the Manage tab.";
        }
    }

    private string GetGuideDllFix(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Lỗi thiếu file DLL (như msvcp140.dll, vcruntime140.dll) hoặc lỗi 0xc000007b xuất hiện do máy tính thiếu thư viện C++ hoặc DirectX của hệ thống.\n\n" +
                "Cách khắc phục triệt để:\n" +
                "1. Tải và cài đặt gói Visual C++ Redistributable All-in-One (tổng hợp từ 2005 đến 2022 cả bản x86 và x64).\n" +
                "2. Cài đặt bộ DirectX End-User Runtimes (June 2010) từ Microsoft.\n" +
                "3. Khởi động lại máy tính sau khi cài xong.\n\n" +
                "Lưu ý: Không nên tải các file .dll lẻ tẻ từ các trang web lạ trên mạng để dán vào System32 vì rất dễ dính mã độc.";
        }
        else
        {
            return
                "Missing DLL errors or error 0xc000007b occur due to missing Visual C++ runtimes or DirectX libraries.\n\n" +
                "To fix:\n" +
                "1. Download and install Visual C++ Redistributable All-in-One (2005-2022, both x86 and x64).\n" +
                "2. Install DirectX End-User Runtimes (June 2010).\n" +
                "3. Restart your computer.\n\n" +
                "Note: Avoid downloading loose .dll files from unfamiliar websites into System32.";
        }
    }

    private string GetGuideManageGame(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Tab Quản lý (Manage) giúp bạn kiểm soát toàn bộ game đã nạp vào Steam:\n\n" +
                "• Xóa game: Bấm vào game muốn xóa -> Bấm nút 'Xóa' (biểu tượng thùng rác). Tool sẽ gỡ file .lua tương ứng để game biến mất khỏi Steam.\n" +
                "• Mở thư mục game: Bấm nút 'Mở thư mục' để vào thẳng nơi lưu trữ game trên ổ cứng.\n" +
                "• Gỡ Steam DRM: Khắc phục lỗi game mở lên bị văng.\n" +
                "• Launch Options: Đặt tham số khởi chạy như -dx11, -novid trực tiếp cho game.";
        }
        else
        {
            return
                "The Manage tab gives you control over unlocked Steam games:\n\n" +
                "• Delete game: Select the game -> Click 'Delete' (trash icon) to remove the .lua configuration.\n" +
                "• Open Folder: Jump directly to the game installation folder.\n" +
                "• Remove Steam DRM: Fixes launch crashes.\n" +
                "• Launch Options: Configure parameters like -dx11, -novid.";
        }
    }

    private string GetGuideDownloads(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Nếu bạn thấy tốc độ tải chậm hoặc muốn quản lý tiến trình tải:\n\n" +
                "• Kiểm tra tab Downloads: Theo dõi phần trăm và tốc độ tải thực tế.\n" +
                "• Tạm dừng / Tiếp tục: Bạn có thể bấm nút tạm dừng rồi tiếp tục tải để làm mới kết nối mạng.\n" +
                "• Bật FastFetch: Trong tab Cài đặt, gạt bật 'FastFetch' để tool tự động chọn nguồn tải nhanh nhất.\n" +
                "• Kiểm tra đường truyền: Tạm tắt các ứng dụng chiếm băng thông như torrent, xem video 4K để đạt tốc độ cao nhất.";
        }
        else
        {
            return
                "If download speed is slow or you want to track downloads:\n\n" +
                "• Check the Downloads tab: Monitor live progress and speed.\n" +
                "• Pause / Resume: Refresh the connection by pausing and resuming.\n" +
                "• Enable FastFetch: In Settings, enable FastFetch to automatically choose the fastest source.\n" +
                "• Bandwidth: Close heavy background downloads like torrents.";
        }
    }

    private string GetGuideRestartSteam(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Để khởi động lại Steam nhanh nhất:\n\n" +
                "Bấm vào nút 'Restart Steam' (biểu tượng mũi tên xoay vòng) ở góc trên cùng bên phải cửa sổ BaoTools.\n" +
                "Ứng dụng sẽ tự động đóng tiến trình Steam đang chạy và mở lại Steam cho bạn trong 2 giây!";
        }
        else
        {
            return
                "To restart Steam quickly:\n\n" +
                "Click the 'Restart Steam' button (revolving arrow icon) in the top-right header of BaoTools.\n" +
                "The tool safely closes Steam and relaunches it automatically in seconds!";
        }
    }

    private string GetGuideOptimization(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Mẹo tăng FPS và giảm giật lag khi chơi game:\n\n" +
                "1. Bật Game Mode của Windows: Vào Cài đặt Windows -> Gaming -> Bật Game Mode lên On.\n" +
                "2. Chỉnh Power Plan: Vào Control Panel -> Power Options -> Chọn chế độ High Performance.\n" +
                "3. Cập nhật Driver Card màn hình: Luôn cài driver mới nhất từ NVIDIA hoặc AMD.\n" +
                "4. Thêm tham số -dx11: Trong tab Quản lý của BaoTools, đặt Launch Option là -dx11 nếu game bị giật lag trên DirectX 12.\n" +
                "5. Tắt app chạy ngầm: Tắt Chrome, Discord hardware acceleration khi chơi game nặng.";
        }
        else
        {
            return
                "Tips to boost FPS and reduce stuttering:\n\n" +
                "1. Enable Windows Game Mode (Windows Settings -> Gaming).\n" +
                "2. Choose High Performance Power Plan in Control Panel.\n" +
                "3. Update your GPU graphics drivers.\n" +
                "4. Use -dx11 in Launch Options (in Manage tab) if DirectX 12 stutters.\n" +
                "5. Close memory-heavy background applications.";
        }
    }

    private string GetGuideModes(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Các chế độ Unlocker trong tab Mode:\n\n" +
                "• BetterSteamTools (Đề xuất): Hoạt động ổn định và tương thích tốt nhất với đa số game, được nhóm BaoTools duy trì tích cực.\n" +
                "• OpenSteamTools (Nightly): Bản dựng mới nhất từ nhánh upstream, có các bản vá nóng hổi và hỗ trợ gốc cho CloudRedirect.\n" +
                "• Trình mở khóa tùy chỉnh (Custom): Dành cho bạn muốn tự quản lý unlocker riêng (SmokeAPI, Koaloader, CreamAPI...). BaoTools sẽ không can thiệp vào file của bạn.\n" +
                "• Tiện ích CloudRedirect (Ở dock dưới cùng): Giúp đồng bộ save game lên Google Drive / OneDrive thay thế Steam Cloud gốc.\n\n" +
                "Mẹo: Bạn có thể vào tab Mode để đổi chế độ rồi bấm Restart Steam để áp dụng!";
        }
        else
        {
            return
                "Unlocker Modes in the Mode tab:\n\n" +
                "• BetterSteamTools (Recommended): Most stable and compatible with all games, maintained by the BaoTools team.\n" +
                "• OpenSteamTools (Nightly / Experimental): Upstream build with latest fixes and native CloudRedirect support.\n" +
                "• Custom Unlocker: Use your own unlocker (SmokeAPI, Koaloader, CreamAPI). BaoTools will not modify your files.\n" +
                "• CloudRedirect Add-on (Docked at bottom): Syncs game saves with Google Drive / OneDrive.\n\n" +
                "Tip: Switch modes anytime in the Mode tab and click Restart Steam to apply!";
        }
    }

    private string GetSmartGeneralHelp(bool? overrideIsVi = null)
    {
        bool isVi = overrideIsVi ?? IsVietnamese;
        if (isVi)
        {
            return
                "Mình là Trợ lý Kỹ thuật BaoTools. Bạn có thể hỏi mình bất cứ điều gì về:\n\n" +
                "• Cách thêm và tải game vào Steam (.lua / AppID)\n" +
                "• Kiểm tra và cài đặt Plugin Steam Store\n" +
                "• Dùng tab Bản sửa lỗi (Fixes / Denuvo bypass)\n" +
                "• Sửa lỗi game bị văng / crash (Gỡ Steam DRM Steamless)\n" +
                "• Sửa lỗi không thấy game trong Thư viện Steam\n" +
                "• Cách chơi Online Co-op qua Steam\n" +
                "• Khắc phục lỗi thiếu file DLL và tối ưu FPS\n\n" +
                "Bạn đang gặp khó khăn ở bước nào? Cứ miêu tả chi tiết để mình hướng dẫn nhé!";
        }
        else
        {
            return
                "I am your BaoTools Technical Assistant. You can ask me about:\n\n" +
                "• Adding & downloading games into Steam (.lua / AppID)\n" +
                "• Checking & installing the Steam Store Plugin\n" +
                "• Using the Fixes tab (Denuvo bypass & crack fixes)\n" +
                "• Fixing game launch crashes (Steamless DRM)\n" +
                "• Troubleshooting missing games in Steam Library\n" +
                "• Playing Online Co-op via Steam\n" +
                "• Fixing missing DLL errors & FPS boost\n\n" +
                "Feel free to describe what issue you are experiencing!";
        }
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        return text
            .Replace("**", "")
            .Replace("`", "")
            .Replace("###", "")
            .Replace("##", "")
            .Replace("#", "")
            .Replace("━━━━━━━━━━━━━━━━━━━━━━━━━━━━", "")
            .Replace("━━━━━━━━━━━━", "")
            .Replace("----------------------------------------", "")
            .Replace("-------------------------", "")
            .Replace("↳", "-")
            .Trim();
    }
}
