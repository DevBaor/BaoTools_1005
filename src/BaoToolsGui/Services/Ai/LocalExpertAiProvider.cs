using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaoToolsGui.Services.Ai;

/// <summary>
/// BaoTools Local Expert Engine — 100% offline, privacy-first technical copilot orchestrator.
/// Pipeline: Normalize query -> Retrieve targeted knowledge -> Determine & execute relevant tools
/// -> Prioritize context (Tool results > App context > KnowledgeBase) -> Generate concise direct answer.
/// </summary>
public class LocalExpertAiProvider : IAiProvider
{
    private readonly AiToolExecutor? _toolExecutor;

    public string ProviderName => "BaoTools Local Expert Engine";
    public bool IsAvailable => true;

    public LocalExpertAiProvider(AiToolExecutor? toolExecutor = null)
    {
        _toolExecutor = toolExecutor;
    }

    public async Task<string> GenerateResponseAsync(AiPromptContext promptContext, CancellationToken ct = default)
    {
        var msg = await GenerateResponseFullAsync(promptContext, null, ct);
        return msg.Text;
    }

    public async Task<AiChatMessage> GenerateResponseFullAsync(
        AiPromptContext promptContext,
        Action<string>? onProgressStep = null,
        CancellationToken ct = default)
    {
        bool isVi = promptContext.IsVietnamese;
        string userQuery = promptContext.UserMessage?.Trim() ?? "";
        string q = userQuery.ToLowerInvariant();
        var ctx = promptContext.AppContext;

        var message = new AiChatMessage
        {
            Role = "model"
        };

        // 1. Check simple greetings
        if (IsGreeting(q))
        {
            message.Text = isVi
                ? "Chào bạn! Mình là Trợ lý Kỹ thuật BaoTools. Bạn cần kiểm tra cấu hình máy, chẩn đoán lỗi game hay tối ưu FPS?"
                : "Hello! I am your BaoTools Technical Assistant. How can I help you diagnose, optimize, or repair your games today?";
            AttachGeneralActions(message, isVi);
            return message;
        }

        // 2. Check completely unrelated non-technical questions
        if (IsUnrelatedQuery(q))
        {
            message.Text = isVi
                ? "Mình là Trợ lý Kỹ thuật BaoTools, chuyên hỗ trợ về game, cấu hình máy, Steam và sửa lỗi phần mềm. Bạn có câu hỏi nào liên quan đến game hoặc tính năng của BaoTools không?"
                : "I am the BaoTools Technical Assistant, focused on Steam games, hardware diagnostics, and crash troubleshooting. Do you have a question regarding a game or BaoTools?";
            AttachGeneralActions(message, isVi);
            return message;
        }

        // 2.1 Anti-hallucination check for unsupported capabilities
        var (isUnsupported, unsupportedExpl) = AI.Knowledge.SystemKnowledgeStore.Instance.CheckUnsupportedCapability(userQuery, isVi);
        if (isUnsupported && !string.IsNullOrWhiteSpace(unsupportedExpl))
        {
            message.Text = unsupportedExpl;
            AttachGeneralActions(message, isVi);
            return message;
        }

        // 2.2 Inquiry about Gemini or Gemini API key
        if (q.Contains("gemini") || q.Contains("key gemini") || q.Contains("api gemini"))
        {
            message.Text = isVi
                ? "**Trợ lý AI BaoTools chạy 100% Cục bộ & Offline:**\n\n" +
                  "• BaoTools hiện **không cần và không sử dụng Google Gemini hay API key ngoài**.\n" +
                  "• Mọi câu trả lời, kiến thức hướng dẫn và công cụ chẩn đoán đều được tích hợp sẵn trực tiếp bên trong ứng dụng, bảo đảm tối đa tính riêng tư và hoạt động mượt mà kể cả khi không có mạng.\n" +
                  "• Bạn có thể bật / tắt Trợ lý AI bất cứ lúc nào trong mục **Cài đặt (Settings)**."
                : "**BaoTools AI Assistant is 100% Local & Offline:**\n\n" +
                  "• BaoTools **no longer requires or uses Google Gemini or external cloud API keys**.\n" +
                  "• All answers, system knowledge, and diagnostic capabilities run entirely on-device for maximum privacy and zero latency, even without an internet connection.\n" +
                  "• You can enable or disable the AI Assistant anytime in **Settings**.";
            AttachGeneralActions(message, isVi);
            return message;
        }

        // 2.3 System Settings inquiry (e.g., "đổi thư mục Steam", "đổi theme", "đổi ngôn ngữ", "cấu hình api")
        if (q.Contains("thư mục steam") || q.Contains("steam folder") || q.Contains("đổi theme") || q.Contains("đổi giao diện") ||
            q.Contains("đổi ngôn ngữ") || q.Contains("change language") || q.Contains("khởi động cùng") || q.Contains("start with") ||
            q.Contains("hubcap api") || q.Contains("cài đặt steam") || q.Contains("setting steam"))
        {
            var matchedSetting = AI.Knowledge.SystemKnowledgeStore.Instance.FindSetting(userQuery);
            if (matchedSetting != null)
            {
                message.Text = isVi
                    ? $"**Cài đặt: {matchedSetting.NameVi}**\n\n" +
                      $"• **Vị trí trong ứng dụng**: `{matchedSetting.LocationVi}`\n" +
                      $"• **Mục đích**: {matchedSetting.PurposeVi}\n" +
                      $"• **Giá trị mặc định**: `{matchedSetting.DefaultValue}`\n\n" +
                      "👉 Bạn có thể bấm nút bên dưới để mở ngay trang Cài đặt (Settings)."
                    : $"**Setting: {matchedSetting.NameEn}**\n\n" +
                      $"• **UI Location**: `{matchedSetting.LocationEn}`\n" +
                      $"• **Purpose**: {matchedSetting.PurposeEn}\n" +
                      $"• **Default**: `{matchedSetting.DefaultValue}`\n\n" +
                      "👉 Click the action button below to open Settings directly.";

                message.ActionButtons.Add(new AiChatAction
                {
                    Label = isVi ? "⚙️ Mở Cài đặt (Settings)" : "⚙️ Open Settings",
                    ToolName = "navigate_settings",
                    IconSymbol = "Settings24"
                });
                return message;
            }
        }

        // 2.3 Screen & Tab location inquiry (e.g., "tab Manage ở đâu", "tab Fixes dùng làm gì", "tab Mode là gì")
        if ((q.Contains("tab ") || q.Contains("màn hình ") || q.Contains("trang ")) &&
            (q.Contains("ở đâu") || q.Contains("dùng để") || q.Contains("là gì") || q.Contains("chức năng") || q.Contains("where") || q.Contains("what is")))
        {
            var matchedScreen = AI.Knowledge.SystemKnowledgeStore.Instance.FindScreen(userQuery);
            if (matchedScreen != null)
            {
                message.Text = isVi
                    ? $"**{matchedScreen.NameVi}**\n\n" +
                      $"• **Vị trí**: Mục **{matchedScreen.NameVi}** trên thanh điều hướng bên trái.\n" +
                      $"• **Chức năng chính**: {matchedScreen.PurposeVi}\n" +
                      $"• **Bao gồm các mục**: {string.Join(", ", matchedScreen.Sections)}.\n\n" +
                      $"👉 Bạn có thể bấm nút bên dưới để chuyển ngay tới trang này."
                    : $"**{matchedScreen.NameEn}**\n\n" +
                      $"• **Location**: Click **{matchedScreen.NameEn}** on the left navigation sidebar.\n" +
                      $"• **Primary Purpose**: {matchedScreen.PurposeEn}\n" +
                      $"• **Key sections**: {string.Join(", ", matchedScreen.Sections)}.\n\n" +
                      $"👉 Click the button below to navigate directly to this tab.";

                string toolNav = matchedScreen.Id switch
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

                message.ActionButtons.Add(new AiChatAction
                {
                    Label = isVi ? $"👉 Đến {matchedScreen.NameVi}" : $"👉 Go to {matchedScreen.NameEn}",
                    ToolName = toolNav,
                    IconSymbol = "Games24"
                });
                return message;
            }
        }

        // 3. Check ambiguous / insufficient-information queries
        if (IsAmbiguousQuery(q))
        {
            message.Text = isVi
                ? "Chưa đủ thông tin để xác định chính xác. Bạn hãy cho mình biết tên game và thông báo lỗi cụ thể đang gặp nhé!"
                : "Not enough information to identify the issue. Please specify the game name and the exact error message you are seeing.";
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🔍 Chẩn đoán Hệ thống" : "🔍 System Diagnostics",
                ToolName = "diagnose_system",
                IconSymbol = "Search24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
            return message;
        }

        // 4. Retrieve targeted knowledge with scoring
        var matchedKnowledge = BaoToolsKnowledgeBase.SearchKnowledgeWithScore(userQuery, topN: 2);
        var primaryArticle = matchedKnowledge.FirstOrDefault().Article;
        int topScore = matchedKnowledge.FirstOrDefault().Score;

        var executedTools = new List<string>();

        // ─────────────────────────────────────────────────────────────
        // 5. Targeted Intent Handling with Actual Tool Results
        // ─────────────────────────────────────────────────────────────

        // SCENARIO A: RAM query ("RAM máy tao bao nhiêu?", "kiểm tra ram")
        if (q.Contains("ram bao nhiêu") || q.Contains("ram bao nhieu") || q.Contains("kiểm tra ram") || q.Contains("kiem tra ram") ||
            q.Contains("dung lượng ram") || q.Contains("how much ram") ||
            (q.Contains("ram") && (q.Contains("máy tao") || q.Contains("máy tôi") || q.Contains("pc") || q.Contains("máy này")) && !q.Contains("vram")))
        {
            onProgressStep?.Invoke(isVi ? "🧠 Đang đọc thông số bộ nhớ RAM..." : "🧠 Reading system RAM...");
            var toolRes = _toolExecutor?.CheckRam(isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_ram");

            int ramGb = toolRes.Data.TryGetValue("RAM_GB", out var rVal) && rVal is int gb ? gb : (ctx.SystemInfo.RamGb > 0 ? ctx.SystemInfo.RamGb : 16);
            string tierDesc = ramGb >= 16
                ? (isVi ? "rất thoải mái cho hầu hết các tựa game hiện nay" : "optimal for all modern titles")
                : (ramGb >= 8 ? (isVi ? "đủ mức tiêu chuẩn cơ bản" : "meets baseline requirements") : (isVi ? "khá thấp, có nguy cơ tràn RAM" : "low, risk of memory exhaustion"));

            message.Text = isVi
                ? $"Dung lượng bộ nhớ RAM của bạn là **{ramGb} GB RAM** ({tierDesc})."
                : $"Your system memory capacity is **{ramGb} GB RAM** ({tierDesc}).";

            AddToolDetailsToMessage(message, toolRes);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO B: DirectX query ("DirectX của tao có vấn đề không?")
        if (q.Contains("directx") || q.Contains("dx11") || q.Contains("dx12") || q.Contains("d3d") || q.Contains("direct3d"))
        {
            onProgressStep?.Invoke(isVi ? "⚡ Đang kiểm tra phiên bản DirectX..." : "⚡ Checking DirectX runtime...");
            var toolRes = _toolExecutor?.CheckDirectX(isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_directx");

            string dxVer = toolRes.Data.TryGetValue("DirectXVersion", out var dVal) && dVal is string sDx ? sDx : ctx.SystemInfo.DirectXVersion;
            message.Text = isVi
                ? $"Hệ thống của bạn hỗ trợ **{dxVer}** hoàn toàn bình thường, tương thích tốt với các game Direct3D 11 và 12."
                : $"Your system supports **{dxVer}** properly, fully compatible with DirectX 11 and 12 titles.";

            AddToolDetailsToMessage(message, toolRes);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO C: 0xc000007b / Missing DLL ("Lỗi 0xc000007b là gì?")
        if (q.Contains("0xc000007b") || q.Contains("0xc0000142") || q.Contains("msvcp") || q.Contains("vcruntime") || q.Contains("thiếu dll") || q.Contains("missing dll"))
        {
            onProgressStep?.Invoke(isVi ? "🔧 Đang kiểm tra Visual C++ Runtimes..." : "🔧 Inspecting VC++ Runtimes...");
            var toolRes = _toolExecutor?.CheckVcRedist(isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_vcredist");

            bool vcInstalled = toolRes.Data.TryGetValue("HasVcRedist_x64", out var vVal) && vVal is bool b && b;
            var sb = new StringBuilder();

            if (isVi)
            {
                sb.AppendLine("Lỗi **0xc000007b** và thiếu DLL xảy ra do thiếu hoặc xung đột thư viện **Visual C++ Redistributable (2015-2022)** hoặc **DirectX**.");
                if (!vcInstalled)
                {
                    sb.AppendLine("\n⚠ **Thực tế trên máy của bạn**: BaoTools phát hiện hệ thống **chưa cài đặt Visual C++ 2015-2022 x64**.");
                    sb.AppendLine("👉 **Giải pháp**: Cài đặt gói Visual C++ All-in-One x86 và x64 rồi khởi động lại máy.");
                }
                else
                {
                    sb.AppendLine("\n✓ **Thực tế trên máy của bạn**: Gói VC++ 2015-2022 x64 đã có. Hãy cài thêm bản **x86 (32-bit)** hoặc chạy bộ DirectX End-User Runtime để bổ sung các file d3dcompiler/xinput.");
                }
            }
            else
            {
                sb.AppendLine("Error **0xc000007b** and missing DLLs occur due to missing or mismatched **Visual C++ (2015-2022)** or **DirectX** runtimes.");
                if (!vcInstalled)
                {
                    sb.AppendLine("\n⚠ **Detected on your system**: Visual C++ 2015-2022 x64 is **NOT installed**.");
                    sb.AppendLine("👉 **Action**: Install the Visual C++ All-in-One package (both x86 & x64) and reboot.");
                }
                else
                {
                    sb.AppendLine("\n✓ **Detected on your system**: VC++ x64 is present. Ensure the **x86 (32-bit)** runtime and DirectX runtimes are also installed.");
                }
            }

            message.Text = sb.ToString().Trim();
            AddToolDetailsToMessage(message, toolRes);
            AttachFixesAction(message, isVi);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO D: Hardware Specs & "Chơi game này được không?" ("máy tao có thể chơi được game GTA 5 không", "chơi được không", "can i run")
        bool isPlayabilityQuery =
            q.Contains("chơi được") || q.Contains("choi duoc") || q.Contains("chơi dc") || q.Contains("choi dc") ||
            q.Contains("chạy được") || q.Contains("chay duoc") || q.Contains("chạy dc") || q.Contains("chay dc") ||
            q.Contains("chơi nổi") || q.Contains("choi noi") || q.Contains("cân được") || q.Contains("can duoc") ||
            q.Contains("gánh được") || q.Contains("ganh duoc") || q.Contains("chơi mượt") || q.Contains("choi muot") ||
            q.Contains("can i run") || q.Contains("can my pc run") || q.Contains("can it run") || q.Contains("can this run") ||
            ((q.Contains("máy") || q.Contains("pc") || q.Contains("cấu hình")) && (q.Contains("chơi") || q.Contains("choi") || q.Contains("chạy") || q.Contains("chay"))) ||
            q.Contains("cấu hình") || q.Contains("specs") ||
            ((q.Contains("rtx") || q.Contains("gtx") || q.Contains("radeon") || q.Contains("gpu")) && (q.Contains("game") || q.Contains("máy tao") || q.Contains("chơi")));

        if (isPlayabilityQuery)
        {
            onProgressStep?.Invoke(isVi ? "🎮 Đang đọc thông số GPU & RAM..." : "🎮 Reading GPU & RAM specs...");
            var gpuRes = _toolExecutor?.CheckGpu(isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            var ramRes = _toolExecutor?.CheckRam(isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_gpu");
            executedTools.Add("check_ram");

            string gpuName = gpuRes.Data.TryGetValue("GPU", out var gVal) && gVal is string sG ? sG : ctx.SystemInfo.GpuName;
            int vramMb = gpuRes.Data.TryGetValue("VRAM_MB", out var vmVal) && vmVal is int vm ? vm : ctx.SystemInfo.VramMb;
            int ramGb = ramRes.Data.TryGetValue("RAM_GB", out var rVal) && rVal is int rm ? rm : ctx.SystemInfo.RamGb;
            string vramStr = vramMb >= 1024 ? $"{Math.Round(vramMb / 1024.0, 1)}GB VRAM" : $"{vramMb}MB VRAM";

            bool isDedicatedGpu = !gpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                                  !gpuName.Contains("UHD", StringComparison.OrdinalIgnoreCase) &&
                                  !gpuName.Contains("Basic Display", StringComparison.OrdinalIgnoreCase);

            // Try extracting game name if specified in query or context
            string? targetGame = ctx?.SelectedGame?.Name;
            if (string.IsNullOrWhiteSpace(targetGame))
            {
                var gameMatch = System.Text.RegularExpressions.Regex.Match(userQuery, @"(?:game|trò chơi)\s+([A-Za-z0-9\s:_\-\+]+?)(?:\s+(?:không|k|ko|được|dc|nổi|mượt|hả|nhỉ|\?)|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (gameMatch.Success && !string.IsNullOrWhiteSpace(gameMatch.Groups[1].Value))
                {
                    targetGame = gameMatch.Groups[1].Value.Trim();
                }
                else if (q.Contains("gta")) targetGame = "Grand Theft Auto V (GTA V)";
                else if (q.Contains("cyberpunk")) targetGame = "Cyberpunk 2077";
                else if (q.Contains("wukong") || q.Contains("black myth")) targetGame = "Black Myth: Wukong";
                else if (q.Contains("elden ring")) targetGame = "Elden Ring";
                else if (q.Contains("valorant")) targetGame = "Valorant";
                else if (q.Contains("cs2") || q.Contains("csgo") || q.Contains("counter-strike")) targetGame = "Counter-Strike 2";
                else if (q.Contains("genshin")) targetGame = "Genshin Impact";
                else if (q.Contains("fifa") || q.Contains("fc 24") || q.Contains("fc 25")) targetGame = "EA SPORTS FC";
                else if (q.Contains("red dead") || q.Contains("rdr2")) targetGame = "Red Dead Redemption 2";
                else if (q.Contains("god of war")) targetGame = "God of War";
            }

            var sb = new StringBuilder();
            if (isVi)
            {
                if (!string.IsNullOrWhiteSpace(targetGame))
                {
                    if (isDedicatedGpu)
                    {
                        sb.AppendLine($"✓ **Với game {targetGame}:** Cấu hình máy bạn (**{gpuName}**, **{ramGb}GB RAM**) hoàn toàn **chơi tốt và mượt mà** ở độ phân giải 1080p (thiết lập Medium/High)!");
                    }
                    else
                    {
                        sb.AppendLine($"⚠ **Với game {targetGame}:** Máy bạn đang dùng GPU tích hợp (**{gpuName}**). Có thể chơi được ở mức thiết lập thấp (720p/1080p Low), nhưng các game 3D nặng đồ họa sẽ khó đạt FPS cao.");
                    }
                }
                else
                {
                    if (isDedicatedGpu)
                    {
                        sb.AppendLine($"Card **{gpuName}** ({vramStr}) và **{ramGb}GB RAM** của bạn đáp ứng tốt hầu hết các tựa game hiện nay ở mức thiết lập 1080p Medium/High.");
                    }
                    else
                    {
                        sb.AppendLine($"Máy bạn đang dùng GPU tích hợp **{gpuName}** ({vramStr}), sẽ phù hợp nhất với các game eSports nhẹ ở mức 720p/1080p Low.");
                    }
                }

                sb.AppendLine("\n**Thông số máy thực tế ghi nhận:**");
                sb.AppendLine($"• GPU: **{gpuName}** ({vramStr})");
                sb.AppendLine($"• RAM: **{ramGb} GB**");
                sb.AppendLine($"• DirectX: **{ctx?.SystemInfo?.DirectXVersion ?? "DirectX 12"}**");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(targetGame))
                {
                    if (isDedicatedGpu)
                    {
                        sb.AppendLine($"✓ **For {targetGame}:** Your hardware (**{gpuName}**, **{ramGb}GB RAM**) can comfortably run this game smoothly at 1080p Medium/High settings!");
                    }
                    else
                    {
                        sb.AppendLine($"⚠ **For {targetGame}:** Your system is using an integrated GPU (**{gpuName}**). It can run lighter titles at 720p/1080p Low, but heavy 3D titles may struggle.");
                    }
                }
                else
                {
                    if (isDedicatedGpu)
                    {
                        sb.AppendLine($"Your **{gpuName}** ({vramStr}) and **{ramGb}GB RAM** can comfortably run modern titles at 1080p Medium/High settings.");
                    }
                    else
                    {
                        sb.AppendLine($"Your system uses an integrated GPU (**{gpuName}**, {vramStr}), best suited for lightweight games at 720p/1080p Low.");
                    }
                }

                sb.AppendLine("\n**Detected Hardware Specifications:**");
                sb.AppendLine($"• GPU: **{gpuName}** ({vramStr})");
                sb.AppendLine($"• RAM: **{ramGb} GB**");
                sb.AppendLine($"• DirectX: **{ctx?.SystemInfo?.DirectXVersion ?? "DirectX 12"}**");
            }

            message.Text = sb.ToString().Trim();
            AddToolDetailsToMessage(message, gpuRes);
            AddToolDetailsToMessage(message, ramRes);
            AttachManageAction(message, isVi);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO MODE: Unlocker Mode selection & recommendation ("trong tab modes sử dụng mode nào", "nên dùng mode nào", "chọn mode nào")
        bool isModeRecommendationQuery =
            (q.Contains("mode") || q.Contains("chế độ") || q.Contains("unlocker")) &&
            (q.Contains("nào") || q.Contains("dùng") || q.Contains("chọn") || q.Contains("sử dụng") ||
             q.Contains("tốt nhất") || q.Contains("khuyên") || q.Contains("đề xuất") ||
             q.Contains("which") || q.Contains("what") || q.Contains("recommend") || q.Contains("bettersteamtools") ||
             q.Contains("opensteamtools") || q.Contains("ost") || q.Contains("bst") || q.Contains("khác nhau"));

        if (isModeRecommendationQuery)
        {
            onProgressStep?.Invoke(isVi ? "⚡ Đang kiểm tra cấu hình Chế độ Unlocker..." : "⚡ Checking Unlocker Mode configuration...");
            var toolRes = _toolExecutor?.ExecuteSafeTool("check_mode_status", isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_mode_status");

            string currentMode = ctx?.PluginStatus?.ActiveMode ?? (_toolExecutor?.GetActiveModeName() ?? "BetterSteamTools");

            var sb = new StringBuilder();
            if (isVi)
            {
                sb.AppendLine("Trong tab **Chế độ (Modes)**, bạn **nên sử dụng BetterSteamTools (BST)**:");
                sb.AppendLine();
                sb.AppendLine("• **BetterSteamTools (BST) — Khuyên dùng (Mặc định)**: Đây là chế độ ổn định nhất, tương thích tốt nhất với hơn 95% game Steam, tự động đồng bộ DLC và được đội ngũ BaoTools tối ưu cập nhật thường xuyên.");
                sb.AppendLine("• **OpenSteamTools (OST)**: Phiên bản mở khóa mã nguồn mở gốc, nhẹ gọn. Bạn chỉ nên chuyển sang OST nếu game bạn đang chơi gặp lỗi đặc thù không tương thích với BST.");
                sb.AppendLine("• **Custom (Tùy chỉnh)**: Dành cho người dùng chuyên sâu muốn tự nạp DLL unlocker riêng (như SmokeAPI, Koaloader, CreamAPI...).");
                sb.AppendLine();
                sb.AppendLine($"👉 **Trạng thái trên máy của bạn**: Đang dùng chế độ **{currentMode}**. Sau khi đổi chế độ, bạn chỉ cần **khởi động lại Steam** để có hiệu lực!");
            }
            else
            {
                sb.AppendLine("In the **Modes** tab, we recommend using **BetterSteamTools (BST)**:");
                sb.AppendLine();
                sb.AppendLine("• **BetterSteamTools (BST) — Recommended (Default)**: The most reliable and compatible unlocker for 95%+ Steam titles, featuring automated DLC synchronization and active updates from the BaoTools team.");
                sb.AppendLine("• **OpenSteamTools (OST)**: The classic lightweight open-source engine. Recommended as a fallback if a specific game experiences conflicts with BST.");
                sb.AppendLine("• **Custom**: For power users wishing to configure their own custom unlocker DLLs (SmokeAPI, Koaloader, CreamAPI...).");
                sb.AppendLine();
                sb.AppendLine($"👉 **Current System Mode**: Active mode is **{currentMode}**. Remember to **restart Steam** after switching modes to apply changes!");
            }

            message.Text = sb.ToString().Trim();
            AddToolDetailsToMessage(message, toolRes);

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "⚡ Đến tab Chế độ (Modes)" : "⚡ Open Modes Tab",
                ToolName = "navigate_mode",
                IconSymbol = "Options24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🔍 Kiểm tra Mode hiện tại" : "🔍 Check Current Mode",
                ToolName = "check_mode_status",
                IconSymbol = "Checkmark24"
            });

            LogDebug(userQuery, "unlocker_modes_recommendation", 100, executedTools);
            return message;
        }

        // SCENARIO E: Launch Crash ("Game bị crash lúc mở", "Chẩn đoán lỗi game / Văng crash", "Diagnose game launch & crash issues")
        if (q.Contains("crash") || q.Contains("văng") || q.Contains("không mở được") || q.Contains("không chạy") ||
            q.Contains("launch crash") || q.Contains("bị out") || q.Contains("chẩn đoán lỗi") || q.Contains("chan doan loi") ||
            q.Contains("văng crash") || q.Contains("diagnose game") || q.Contains("game launch & crash"))
        {
            onProgressStep?.Invoke(isVi ? "🛠 Đang chẩn đoán nguyên nhân crash..." : "🛠 Diagnosing crash causes...");
            var crashRes = _toolExecutor != null ? await _toolExecutor.DetectAndExecuteToolAsync(userQuery, ctx) : new ToolExecutionResult { Handled = false };
            executedTools.Add("diagnose_game_crash");

            if (crashRes.Handled && !string.IsNullOrWhiteSpace(crashRes.Output))
            {
                message.Text = crashRes.Output;
                message.DiagnosisTitle = crashRes.DiagnosisTitle;
                message.Checklist = crashRes.Checklist;
                message.ActionButtons = crashRes.ActionButtons;
                LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
                return message;
            }
        }

        // SCENARIO F: Steam library not recognizing game ("Steam không nhận game")
        if (q.Contains("steam không nhận") || q.Contains("steam khong nhan") || q.Contains("không thấy game") || q.Contains("khong thay game") ||
            q.Contains("mất game") || q.Contains("thư viện steam") || q.Contains("not showing"))
        {
            onProgressStep?.Invoke(isVi ? "🎮 Đang kiểm tra thư viện game..." : "🎮 Scanning library...");
            var gamesRes = _toolExecutor?.ExecuteSafeTool("check_games_status", isVi, ctx) ?? new ToolExecutionResult { Handled = false };
            executedTools.Add("check_games_status");

            if (isVi)
            {
                message.Text =
                    "Khi Steam không nhận game đã nạp:\n\n" +
                    "1. **Khởi động lại Steam**: Bấm nút 'Restart Steam' ở góc trên bên phải BaoTools để nạp cấu hình mới.\n" +
                    "2. **Kiểm tra tab Quản lý**: Xác nhận game có hiển thị trong danh sách BaoTools không.\n" +
                    "3. **Nạp lại nhanh**: Thả lại file `.lua` hoặc `.zip` vào ô Trang chủ (Drop Zone).";
            }
            else
            {
                message.Text =
                    "When Steam does not display an injected game:\n\n" +
                    "1. **Restart Steam**: Click the 'Restart Steam' button in BaoTools header to load new licenses.\n" +
                    "2. **Verify Manage tab**: Check whether the game is listed in BaoTools.\n" +
                    "3. **Re-inject**: Drag and drop the `.lua` or `.zip` into Home Drop Zone.";
            }

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🔄 Khởi động lại Steam" : "🔄 Restart Steam",
                ToolName = "restart_steam",
                IconSymbol = "ArrowSync24"
            });
            AttachManageAction(message, isVi);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO G: FPS optimization ("FPS game này thấp")
        if (q.Contains("fps thấp") || q.Contains("fps thap") || q.Contains("tụt fps") || q.Contains("tut fps") ||
            q.Contains("giật lag") || q.Contains("giat lag") || q.Contains("low fps") || q.Contains("stutter") || q.Contains("lag"))
        {
            var hw = AiToolExecutor.GetOrInspectHardware(ctx);
            if (isVi)
            {
                message.Text =
                    $"Mẹo tối ưu FPS cho máy tính ({hw.GpuName}, {hw.RamGb}GB RAM):\n\n" +
                    "1. **Thêm tham số `-dx11`**: Trong tab Quản lý của BaoTools, đặt Launch Option là `-dx11` nếu game bị giật lag trên DirectX 12.\n" +
                    "2. **Bật Windows Game Mode**: Mở Windows Settings -> Gaming -> Bật Game Mode.\n" +
                    "3. **Chỉnh Power Plan**: Vào Control Panel -> Power Options -> Chọn High Performance.\n" +
                    $"4. **Cập nhật Driver GPU**: Đảm bảo driver card {hw.GpuName} được cập nhật bản mới nhất.";
            }
            else
            {
                message.Text =
                    $"FPS optimization tips for your hardware ({hw.GpuName}, {hw.RamGb}GB RAM):\n\n" +
                    "1. **Add `-dx11` parameter**: In BaoTools Manage tab, set Launch Option to `-dx11` if DirectX 12 stutters.\n" +
                    "2. **Enable Windows Game Mode**: Windows Settings -> Gaming -> Game Mode.\n" +
                    "3. **Power Plan**: Select High Performance in Windows Power Options.\n" +
                    $"4. **Update GPU Drivers**: Ensure latest drivers for {hw.GpuName}.";
            }

            AttachManageAction(message, isVi);
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO H: "BaoTools là gì?"
        if (q.Contains("baotools là gì") || q.Contains("baotools la gi") || q.Contains("what is baotools") || q.Contains("bao tools là gì") || q.Contains("bao tools la gi"))
        {
            message.Text = isVi
                ? "**BaoTools** là bộ công cụ tối ưu và quản lý game Steam chuyên nghiệp:\n\n" +
                  "• Nạp game Steam miễn phí qua cấu hình `.lua` và AppID.\n" +
                  "• Tải game tốc độ cao trực tiếp từ kho lưu trữ.\n" +
                  "• Sửa lỗi game văng bằng công cụ gỡ Steam DRM (Steamless).\n" +
                  "• Tích hợp Plugin trên web Steam Store để thêm game 1-click.\n" +
                  "• Chẩn đoán phần cứng và sự cố kỹ thuật tự động."
                : "**BaoTools** is a professional Steam game management & optimization toolkit:\n\n" +
                  "• Inject games into Steam via `.lua` configs & AppIDs.\n" +
                  "• High-speed game downloads.\n" +
                  "• Fix game launch crashes via Steamless DRM unpacker.\n" +
                  "• Integrated Steam Store browser plugin for 1-click additions.\n" +
                  "• Automated technical diagnostics.";

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🧩 Kiểm tra Plugin" : "🧩 Plugin Status",
                ToolName = "navigate_plugin",
                IconSymbol = "PlugDisconnected24"
            });
            LogDebug(userQuery, primaryArticle?.Id, topScore, executedTools);
            return message;
        }

        // SCENARIO I: Add & Download games ("Hướng dẫn Thêm & Tải game", "how to add & download games", "cách tải game", "thêm game thế nào")
        if (q.Contains("thêm & tải game") || q.Contains("them & tai game") ||
            q.Contains("add & download") || q.Contains("cách tải game") || q.Contains("cach tai game") ||
            q.Contains("thêm game") || q.Contains("them game") || q.Contains("tải game") || q.Contains("tai game") ||
            q.Contains("how to add") || q.Contains("how to download"))
        {
            if (isVi)
            {
                message.Text =
                    "**Hướng dẫn Thêm & Tải game vào Steam:**\n\n" +
                    "1. **Thêm bản quyền game (Inject .lua)**:\n" +
                    "   • Kéo thả file cấu hình `.lua` hoặc file `.zip` vào ô nạp ở **Trang chủ**.\n" +
                    "   • Hoặc cài đặt **Steam Plugin** để bấm thêm game trực tiếp từ trang web Steam Store.\n\n" +
                    "2. **Tải dữ liệu game**:\n" +
                    "   • Mở tab **Tải game (Downloads)** hoặc vào **Quản lý (Manage)** bấm nút tải cạnh game.\n" +
                    "   • BaoTools sẽ tự động kết nối nguồn tải tốc độ cao và giải nén vào thư mục Steam.\n\n" +
                    "3. **Vào chơi**:\n" +
                    "   • Khởi động lại Steam và game sẽ xuất hiện sẵn sàng trong Thư viện Steam của bạn!";
            }
            else
            {
                message.Text =
                    "**How to Add & Download games to Steam:**\n\n" +
                    "1. **Add Game License (Inject .lua)**:\n" +
                    "   • Drag and drop your game `.lua` config or `.zip` archive into the **Home** drop zone.\n" +
                    "   • Or install the **Steam Store Plugin** to add games with 1-click directly from the Steam Store.\n\n" +
                    "2. **Download Game Files**:\n" +
                    "   • Open the **Downloads** tab or click the download button next to the game in **Manage**.\n" +
                    "   • BaoTools will fetch files at high speed and unpack them into your Steam library folder.\n\n" +
                    "3. **Ready to Play**:\n" +
                    "   • Restart Steam and the game will appear in your Steam Library ready to launch!";
            }

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "📥 Tải game (Downloads)" : "📥 Downloads Tab",
                ToolName = "navigate_downloads",
                IconSymbol = "ArrowDownload24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🏠 Trang chủ (Nạp game)" : "🏠 Home (Drop Zone)",
                ToolName = "navigate_home",
                IconSymbol = "Home24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
            LogDebug(userQuery, "how_to_add_and_download", 100, executedTools);
            return message;
        }

        // SCENARIO J: How to use Game Fixes ("Cách dùng Bản sửa lỗi", "how to use game fixes", "bản sửa lỗi", "online fix")
        if (q.Contains("cách dùng bản sửa lỗi") || q.Contains("cach dung ban sua loi") ||
            q.Contains("how to use game fixes") || q.Contains("bản sửa lỗi") || q.Contains("ban sua loi") ||
            q.Contains("game fixes") || q.Contains("online fix") || q.Contains("online-fix"))
        {
            if (isVi)
            {
                message.Text =
                    "**Cách sử dụng Bản sửa lỗi (Fixes):**\n\n" +
                    "1. **Sửa lỗi Crash / Không mở được game**:\n" +
                    "   • Vào tab **Bản sửa lỗi (Fixes)**, tìm kiếm game bạn cần sửa.\n" +
                    "   • Chọn bản patch phù hợp (gỡ DRM Steamless, sửa thiếu file DLL, fix màn hình đen).\n\n" +
                    "2. **Chơi Online nhiều người (Online-Fix)**:\n" +
                    "   • Các bản Online-Fix giúp bạn kết nối co-op/multiplayer qua Steam Spacewar.\n" +
                    "   • Bấm **Áp dụng (Apply)**, BaoTools sẽ tự động cài file vào đúng thư mục game.";
            }
            else
            {
                message.Text =
                    "**How to use Game Fixes:**\n\n" +
                    "1. **Crash & Launch Issue Repairs**:\n" +
                    "   • Go to the **Fixes** tab and search for your game.\n" +
                    "   • Choose the appropriate patch (Steamless DRM removal, missing DLLs, black screen fixes).\n\n" +
                    "2. **Multiplayer & Co-op (Online-Fix)**:\n" +
                    "   • Online-Fix patches enable online multiplayer via Steam Spacewar emulation.\n" +
                    "   • Click **Apply Fix**, and BaoTools will safely inject the files into the game directory.";
            }

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🛠 Bản sửa lỗi (Fixes)" : "🛠 Game Fixes",
                ToolName = "navigate_fixes",
                IconSymbol = "Wrench24"
            });
            LogDebug(userQuery, "how_to_use_fixes", 100, executedTools);
            return message;
        }

        // SCENARIO K: Check Steam Plugin status ("Kiểm tra trạng thái Steam Plugin", "check steam plugin status", "plugin status", "trạng thái plugin")
        if (q.Contains("kiểm tra trạng thái steam plugin") || q.Contains("kiem tra trang thai steam plugin") ||
            q.Contains("check steam plugin status") || q.Contains("trạng thái plugin") || q.Contains("trang thai plugin") ||
            q.Contains("plugin status") || (q.Contains("plugin") && (q.Contains("trạng thái") || q.Contains("status") || q.Contains("kiểm tra") || q.Contains("check"))))
        {
            onProgressStep?.Invoke(isVi ? "🧩 Đang kiểm tra trạng thái Steam Plugin..." : "🧩 Checking Steam Plugin status...");
            var pluginRes = _toolExecutor?.ExecuteSafeTool("check_plugin_status", isVi, ctx);
            executedTools.Add("check_plugin_status");

            bool isInstalled = ctx.PluginStatus.IsInstalled;
            if (pluginRes != null && pluginRes.Handled && pluginRes.Checklist.Count > 0)
            {
                message.Checklist.AddRange(pluginRes.Checklist);
            }

            if (isVi)
            {
                message.Text = isInstalled
                    ? "✓ **Steam Plugin đã được cài đặt và đang hoạt động tốt!**\n\nBạn có thể mở Steam Store trên trình duyệt để thêm game 1-click vào BaoTools."
                    : "⚠ **Steam Plugin chưa được cài đặt hoặc chưa được kích hoạt.**\n\nHãy vào tab Plugin để cài đặt Millennium & BaoTools Plugin, sau đó khởi động lại Steam.";
            }
            else
            {
                message.Text = isInstalled
                    ? "✓ **Steam Plugin is installed and operating normally!**\n\nYou can browse the Steam Store web page to 1-click add games directly to BaoTools."
                    : "⚠ **Steam Plugin is not installed or inactive.**\n\nOpen the Plugin tab to install Millennium & BaoTools Plugin, then restart Steam.";
            }

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🧩 Quản lý Plugin" : "🧩 Manage Plugin",
                ToolName = "navigate_plugin",
                IconSymbol = "PlugDisconnected24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🔄 Khởi động lại Steam" : "🔄 Restart Steam",
                ToolName = "restart_steam",
                IconSymbol = "ArrowSync24"
            });
            LogDebug(userQuery, "check_plugin_status", 100, executedTools);
            return message;
        }

        // SCENARIO L: How to uninstall / remove / delete games ("cách xóa game", "gỡ game", "xóa game", "uninstall game", "delete game")
        bool isDrmRemoval = q.Contains("gỡ drm") || q.Contains("go drm") || q.Contains("steamless") || q.Contains("steamstub");
        if (!isDrmRemoval && (
            q.Contains("xóa game") || q.Contains("xoa game") ||
            q.Contains("gỡ game") || q.Contains("go game") ||
            q.Contains("gỡ cài đặt") || q.Contains("go cai dat") ||
            q.Contains("xóa khỏi steam") || q.Contains("xoa khoi steam") ||
            q.Contains("gỡ khỏi steam") || q.Contains("go khoi steam") ||
            q.Contains("uninstall game") || q.Contains("delete game") ||
            q.Contains("remove game") || q.Contains("how to uninstall") || q.Contains("how to delete") ||
            ((q.Contains("xóa") || q.Contains("xoa") || q.Contains("gỡ") || q.Contains("go")) && (q.Contains("game") || q.Contains("trò chơi") || q.Contains("tro choi")))))
        {
            if (isVi)
            {
                message.Text =
                    "**Hướng dẫn gỡ / xóa game khỏi Steam bằng BaoTools:**\n\n" +
                    "1. **Mở tab Quản lý (Manage)**:\n" +
                    "   • Bấm vào tab **Quản lý (Manage)** trên thanh điều hướng bên trái.\n" +
                    "   • Chọn tựa game bạn muốn xóa khỏi danh sách.\n\n" +
                    "2. **Xóa cấu hình bản quyền game (.lua)**:\n" +
                    "   • Ở thanh chi tiết game bên phải, bấm nút **'Xóa' (biểu tượng Thùng rác)**.\n" +
                    "   • BaoTools sẽ tự động gỡ file `.lua` tương ứng để game không còn nạp vào Steam nữa.\n\n" +
                    "3. **Khởi động lại Steam**:\n" +
                    "   • Bấm nút **'Restart Steam'** ở góc trên ứng dụng để Steam làm mới và xóa game khỏi Thư viện.\n\n" +
                    "💡 *Mẹo: Nếu muốn giải phóng dung lượng ổ cứng, hãy bấm **'Mở thư mục' (Open Folder)** để xóa sạch các file cài đặt của game.*";
            }
            else
            {
                message.Text =
                    "**How to uninstall / delete games from Steam via BaoTools:**\n\n" +
                    "1. **Open the Manage tab**:\n" +
                    "   • Click the **Manage** tab on the left navigation bar.\n" +
                    "   • Select the game you want to remove from your library list.\n\n" +
                    "2. **Delete Game Configuration (.lua)**:\n" +
                    "   • In the right detail panel, click **'Delete' (Trash icon)**.\n" +
                    "   • BaoTools removes the `.lua` file so Steam no longer loads the game.\n\n" +
                    "3. **Restart Steam**:\n" +
                    "   • Click **'Restart Steam'** on the top header to refresh your Steam library.\n\n" +
                    "💡 *Tip: To free up disk storage, click **'Open Folder'** to delete the physical installation files.*";
            }

            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Đến Tab Quản lý Game" : "🎮 Manage Games Tab",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
            message.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🔄 Khởi động lại Steam" : "🔄 Restart Steam",
                ToolName = "restart_steam",
                IconSymbol = "ArrowSync24"
            });
            LogDebug(userQuery, "how_to_delete_game", 100, executedTools);
            return message;
        }

        // SCENARIO M: Full System & Hardware Diagnostics ("Chẩn đoán hệ thống phần cứng", "System hardware diagnostics", "kiểm tra hệ thống", "kiểm tra máy")
        if (q.Contains("chẩn đoán hệ thống") || q.Contains("chan doan he thong") ||
            q.Contains("chẩn đoán phần cứng") || q.Contains("chan doan phan cung") ||
            q.Contains("system hardware diagnostics") || q.Contains("system diagnostics") ||
            q.Contains("hardware diagnostics") || q.Contains("kiểm tra hệ thống") ||
            q.Contains("kiem tra he thong") || q.Contains("kiểm tra máy") || q.Contains("kiem tra may") ||
            q.Contains("chẩn đoán máy") || q.Contains("chan doan may") ||
            (q.Contains("chẩn đoán") && (q.Contains("hệ thống") || q.Contains("phần cứng") || q.Contains("máy"))))
        {
            onProgressStep?.Invoke(isVi ? "🖥 Đang kiểm tra hệ thống phần cứng..." : "🖥 Checking system hardware...");
            var diagRes = _toolExecutor?.ExecuteSafeTool("diagnose_system", isVi, ctx);
            executedTools.Add("diagnose_system");
            if (diagRes != null && diagRes.Handled)
            {
                message.Text = diagRes.Output;
                message.DiagnosisTitle = diagRes.DiagnosisTitle;
                message.Checklist = diagRes.Checklist;
                message.ActionButtons = diagRes.ActionButtons;
            }
            else
            {
                var hw = ctx?.SystemInfo ?? BaoToolsContext.InspectSystemHardware();
                message.DiagnosisTitle = isVi ? "🔍 Kết Quả Chẩn Đoán Hệ Thống" : "🔍 System Technical Diagnostics";
                message.Text = isVi
                    ? $"Hệ thống máy tính của bạn đã được kiểm tra:\n• GPU: **{hw.GpuName}** ({hw.VramMb} MB VRAM)\n• RAM: **{hw.RamGb} GB RAM**\n• DirectX: **{hw.DirectXVersion}**\n\nMọi dịch vụ của BaoTools đang hoạt động bình thường!"
                    : $"System specifications inspected:\n• GPU: **{hw.GpuName}** ({hw.VramMb} MB VRAM)\n• RAM: **{hw.RamGb} GB RAM**\n• DirectX: **{hw.DirectXVersion}**\n\nBaoTools services are operational.";
                message.ActionButtons.Add(new AiChatAction
                {
                    Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
                    ToolName = "navigate_manage",
                    IconSymbol = "Games24"
                });
            }
            LogDebug(userQuery, "diagnose_system_full", 100, executedTools);
            return message;
        }

        // ─────────────────────────────────────────────────────────────
        // 6. High-Confidence KnowledgeBase Match (Score >= 18)
        // ─────────────────────────────────────────────────────────────
        if (primaryArticle != null && topScore >= 18)
        {
            string title = isVi ? primaryArticle.TitleVi : primaryArticle.TitleEn;
            string body = isVi ? primaryArticle.ContentVi : primaryArticle.ContentEn;

            message.Text = $"### {title}\n\n{body}";

            // Execute related tool if any
            if (primaryArticle.RelatedTools.Length > 0 && _toolExecutor != null)
            {
                string firstTool = primaryArticle.RelatedTools[0];
                var toolRes = _toolExecutor.ExecuteSafeTool(firstTool, isVi, ctx);
                if (toolRes.Handled)
                {
                    executedTools.Add(firstTool);
                    AddToolDetailsToMessage(message, toolRes);
                }
            }

            AttachContextualActionsByTopic(message, primaryArticle.Category, isVi, primaryArticle.Id, primaryArticle.RelatedTools);
            LogDebug(userQuery, primaryArticle.Id, topScore, executedTools);
            return message;
        }

        // ─────────────────────────────────────────────────────────────
        // 7. Low Confidence Fallback — Avoid Hallucination
        // ─────────────────────────────────────────────────────────────
        message.Text = isVi
            ? "Chưa đủ thông tin để chẩn đoán chính xác. Bạn vui lòng miêu tả chi tiết hơn tên game hoặc sự cố đang gặp để mình hỗ trợ nhé!"
            : "Insufficient details to diagnose accurately. Please provide the game name or describe the specific error you are experiencing.";

        message.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🔍 Chẩn đoán Hệ thống" : "🔍 System Diagnostics",
            ToolName = "diagnose_system",
            IconSymbol = "Search24"
        });
        message.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
            ToolName = "navigate_manage",
            IconSymbol = "Games24"
        });

        LogDebug(userQuery, "none", topScore, executedTools);
        return message;
    }

    private static bool IsGreeting(string q)
    {
        return q == "chào" || q == "chao" || q == "xin chào" || q == "xin chao" ||
               q == "hi" || q == "hello" || q == "alo" || q == "ê bot" || q == "e bot" ||
               q == "bạn ơi" || q == "ban oi" || q == "hey";
    }

    private static bool IsUnrelatedQuery(string q)
    {
        string[] unrelatedTokens = new[]
        {
            "thời tiết", "thoi tiet", "weather", "nấu ăn", "nau an", "nấu cơm", "nau com",
            "chính trị", "chinh tri", "bài thơ", "bai tho", "kể chuyện", "ke chuyen",
            "tình yêu", "tinh yeu", "xem bói", "xem boi", "giá vàng", "gia vang"
        };
        return unrelatedTokens.Any(q.Contains);
    }

    private static bool IsAmbiguousQuery(string q)
    {
        return q == "lỗi" || q == "loi" || q == "bị lỗi" || q == "bi loi" ||
               q == "game lỗi" || q == "game loi" || q == "hư rồi" || q == "hu roi" ||
               q == "không được" || q == "khong duoc";
    }

    private static void AddToolDetailsToMessage(AiChatMessage msg, ToolExecutionResult toolRes)
    {
        if (!toolRes.Handled) return;

        if (string.IsNullOrEmpty(msg.DiagnosisTitle) && !string.IsNullOrEmpty(toolRes.DiagnosisTitle))
        {
            msg.DiagnosisTitle = toolRes.DiagnosisTitle;
        }

        foreach (var item in toolRes.Checklist)
        {
            if (!msg.Checklist.Any(c => c.Title == item.Title))
            {
                msg.Checklist.Add(item);
            }
        }

        foreach (var act in toolRes.ActionButtons)
        {
            if (!msg.ActionButtons.Any(a => a.ToolName == act.ToolName))
            {
                msg.ActionButtons.Add(act);
            }
        }
    }

    private static void AttachGeneralActions(AiChatMessage msg, bool isVi)
    {
        msg.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🔍 Chẩn đoán Hệ thống" : "🔍 System Diagnostics",
            ToolName = "diagnose_system",
            IconSymbol = "Search24"
        });
        msg.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
            ToolName = "navigate_manage",
            IconSymbol = "Games24"
        });
    }

    private static void AttachManageAction(AiChatMessage msg, bool isVi)
    {
        if (!msg.ActionButtons.Any(a => a.ToolName == "navigate_manage"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🎮 Đến trang Quản lý" : "🎮 Manage Tab",
                ToolName = "navigate_manage",
                IconSymbol = "Games24"
            });
        }
    }

    private static void AttachFixesAction(AiChatMessage msg, bool isVi)
    {
        if (!msg.ActionButtons.Any(a => a.ToolName == "navigate_fixes"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🛠 Bản sửa lỗi (Fixes)" : "🛠 Game Fixes",
                ToolName = "navigate_fixes",
                IconSymbol = "Wrench24"
            });
        }
    }

    private static void AttachContextualActionsByTopic(AiChatMessage msg, string topic, bool isVi, string? articleId = null, string[]? relatedTools = null)
    {
        // 1. If the matched knowledge article has explicit related tools, attach them directly!
        if (relatedTools != null && relatedTools.Length > 0)
        {
            foreach (var tool in relatedTools)
            {
                if (msg.ActionButtons.Any(a => a.ToolName == tool)) continue;
                var action = GetActionForTool(tool, isVi);
                if (action != null)
                {
                    msg.ActionButtons.Add(action);
                }
            }
            if (msg.ActionButtons.Count > 0) return;
        }

        string t = topic.ToLowerInvariant();
        string id = (articleId ?? "").ToLowerInvariant();

        if (id.Contains("mode") || t.Contains("mode") || t.Contains("chế độ") || t.Contains("unlocker"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "⚡ Đến tab Chế độ (Modes)" : "⚡ Unlocker Modes",
                ToolName = "navigate_mode",
                IconSymbol = "Options24"
            });
        }
        else if (id.Contains("setting") || t.Contains("setting") || t.Contains("cài đặt"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "⚙️ Mở Cài đặt" : "⚙️ Open Settings",
                ToolName = "navigate_settings",
                IconSymbol = "Settings24"
            });
        }
        else if (id.Contains("download") || t.Contains("download") || t.Contains("tải"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "📥 Quản lý Tải xuống" : "📥 Downloads Tab",
                ToolName = "navigate_downloads",
                IconSymbol = "ArrowDownload24"
            });
        }
        else if (id.Contains("build") || t.Contains("build") || t.Contains("manifest"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "📦 Bản dựng (Builds)" : "📦 Builds Tab",
                ToolName = "navigate_builds",
                IconSymbol = "Database24"
            });
        }
        else if (id.Contains("home") || t.Contains("home") || t.Contains("trang chủ"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🏠 Về Trang chủ" : "🏠 Go to Home",
                ToolName = "navigate_home",
                IconSymbol = "Home24"
            });
        }
        else if (id.Contains("plugin") || t.Contains("plugin"))
        {
            msg.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🧩 Cài đặt Plugin" : "🧩 Install Plugin",
                ToolName = "navigate_plugin",
                IconSymbol = "PlugDisconnected24"
            });
        }
        else if (t.Contains("fix") || t.Contains("troubleshoot"))
        {
            AttachFixesAction(msg, isVi);
        }
        else
        {
            AttachManageAction(msg, isVi);
        }
    }

    private static AiChatAction? GetActionForTool(string toolName, bool isVi) => toolName switch
    {
        "navigate_home" => new AiChatAction { Label = isVi ? "🏠 Về Trang chủ" : "🏠 Go to Home", ToolName = "navigate_home", IconSymbol = "Home24" },
        "navigate_manage" => new AiChatAction { Label = isVi ? "🎮 Đến trang Quản lý" : "🎮 Manage Tab", ToolName = "navigate_manage", IconSymbol = "Games24" },
        "navigate_mode" => new AiChatAction { Label = isVi ? "⚡ Đến tab Chế độ (Modes)" : "⚡ Unlocker Modes", ToolName = "navigate_mode", IconSymbol = "Options24" },
        "navigate_fixes" => new AiChatAction { Label = isVi ? "🛠 Bản sửa lỗi (Fixes)" : "🛠 Game Fixes", ToolName = "navigate_fixes", IconSymbol = "Wrench24" },
        "navigate_plugin" => new AiChatAction { Label = isVi ? "🧩 Quản lý Plugin" : "🧩 Manage Plugin", ToolName = "navigate_plugin", IconSymbol = "PlugDisconnected24" },
        "navigate_downloads" => new AiChatAction { Label = isVi ? "📥 Quản lý Tải xuống" : "📥 Downloads Tab", ToolName = "navigate_downloads", IconSymbol = "ArrowDownload24" },
        "navigate_builds" => new AiChatAction { Label = isVi ? "📦 Bản dựng (Builds)" : "📦 Builds Tab", ToolName = "navigate_builds", IconSymbol = "Database24" },
        "navigate_settings" => new AiChatAction { Label = isVi ? "⚙️ Mở Cài đặt" : "⚙️ Open Settings", ToolName = "navigate_settings", IconSymbol = "Settings24" },
        "navigate_add" => new AiChatAction { Label = isVi ? "➕ Thêm Game Mới" : "➕ Add New Game", ToolName = "navigate_add", IconSymbol = "Add24" },
        "restart_steam" => new AiChatAction { Label = isVi ? "🔄 Khởi động lại Steam" : "🔄 Restart Steam", ToolName = "restart_steam", IconSymbol = "ArrowClockwise24" },
        "check_mode_status" => new AiChatAction { Label = isVi ? "🔍 Kiểm tra Mode" : "🔍 Check Mode", ToolName = "check_mode_status", IconSymbol = "Checkmark24" },
        "check_plugin_status" => new AiChatAction { Label = isVi ? "🔍 Kiểm tra Plugin" : "🔍 Check Plugin", ToolName = "check_plugin_status", IconSymbol = "Checkmark24" },
        _ => null
    };

    private static void LogDebug(string query, string? matchedArticleId, int score, List<string> tools)
    {
        string toolList = tools.Count > 0 ? string.Join(", ", tools) : "none";
        Debug.WriteLine($"[Assistant] Question: \"{query}\" | Matched: {matchedArticleId ?? "none"} (Score: {score}) | Tools: {toolList}");
    }
}
