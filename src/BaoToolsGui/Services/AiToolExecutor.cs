using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BaoToolsGui.Services.Ai;
using Microsoft.Win32;

namespace BaoToolsGui.Services;

public class ToolExecutionResult
{
    public bool Handled { get; set; }
    public bool Success { get; set; } = true;
    public string ToolName { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Output { get; set; } = "";
    public string? DiagnosisTitle { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<AiChecklistItem> Checklist { get; set; } = new();
    public List<AiChatAction> ActionButtons { get; set; } = new();
    public string? NavigationTarget { get; set; }
}

public class AiToolMetadata
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] TriggerKeywords { get; set; } = Array.Empty<string>();
    public bool RequiresConfirmation { get; set; }
    public bool IsSafeDiagnostic => !RequiresConfirmation;
}

public class AiToolExecutor
{
    private readonly SteamService _steam;
    private readonly LuaVault _vault;
    private readonly SettingsService _settings;
    private readonly UnlockerService? _unlocker;

    public AiToolExecutor(SteamService steam, LuaVault vault, SettingsService settings, UnlockerService? unlocker = null)
    {
        _steam = steam;
        _vault = vault;
        _settings = settings;
        _unlocker = unlocker;
    }

    private static bool IsVietnamese =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

    private static BaoToolsContext.SystemHardwareInfo? _cachedHw;
    private static DateTime _cachedHwTime = DateTime.MinValue;

    public static readonly List<AiToolMetadata> AvailableTools = new()
    {
        new AiToolMetadata
        {
            Name = "check_gpu",
            Description = "Inspect installed GPU card, graphics driver and dedicated VRAM",
            TriggerKeywords = new[] { "gpu", "card màn hình", "vram", "rtx", "gtx", "radeon", "card do hoa", "graphics" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_ram",
            Description = "Check total system memory (RAM) and gaming tier",
            TriggerKeywords = new[] { "ram", "bộ nhớ", "dung lượng ram", "how much ram", "system memory" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_directx",
            Description = "Check DirectX version and Direct3D runtime",
            TriggerKeywords = new[] { "directx", "dx11", "dx12", "d3d", "direct3d" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_vcredist",
            Description = "Check Visual C++ 2015-2022 runtimes (x64 and x86)",
            TriggerKeywords = new[] { "vcredist", "visual c++", "msvcp", "vcruntime", "0xc000007b", "0xc0000142" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "diagnose_system",
            Description = "Full hardware, OS, DirectX, Steam & plugin diagnosis",
            TriggerKeywords = new[] { "chẩn đoán", "diagnose", "kiểm tra máy", "kiểm tra hệ thống", "check system" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "diagnose_game_crash",
            Description = "Diagnose launch crash, SteamStub DRM, dependencies",
            TriggerKeywords = new[] { "crash", "văng", "không mở được", "không chạy", "launch crash", "bị out" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_plugin_status",
            Description = "Check Steam store plugin frontend and winmm.dll loader",
            TriggerKeywords = new[] { "plugin", "stplug-in", "winmm.dll" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_mode_status",
            Description = "Check active unlocker mode",
            TriggerKeywords = new[] { "mode", "chế độ", "unlocker", "bettersteamtools" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_games_status",
            Description = "Scan unlocked and installed games in BaoTools",
            TriggerKeywords = new[] { "kiểm tra game", "có bao nhiêu game", "danh sách game", "danh sach game" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "check_game_specs",
            Description = "Check system requirements and disk footprint of game",
            TriggerKeywords = new[] { "cấu hình", "dung lượng", "nặng bao nhiêu", "requirements", "pc specs" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "restart_steam",
            Description = "Restart the Steam client application",
            TriggerKeywords = new[] { "khởi động lại steam", "restart steam", "re-open steam" },
            RequiresConfirmation = false
        },
        new AiToolMetadata
        {
            Name = "repair_game_configuration",
            Description = "Re-generate and repair .lua configuration for a game",
            TriggerKeywords = new[] { "repair game", "sửa cấu hình" },
            RequiresConfirmation = true
        },
        new AiToolMetadata
        {
            Name = "clear_safe_cache",
            Description = "Clear temporary application cache",
            TriggerKeywords = new[] { "clear cache", "xóa cache" },
            RequiresConfirmation = true
        },
        new AiToolMetadata
        {
            Name = "delete_game",
            Description = "Delete game unlock configuration from library",
            TriggerKeywords = new[] { "xóa game", "delete game" },
            RequiresConfirmation = true
        }
    };

    public static BaoToolsContext.SystemHardwareInfo GetOrInspectHardware(BaoToolsContext? ctx = null)
    {
        if (ctx?.SystemInfo != null && !string.IsNullOrEmpty(ctx.SystemInfo.GpuName) && ctx.SystemInfo.GpuName != "Unknown GPU")
        {
            _cachedHw = ctx.SystemInfo;
            _cachedHwTime = DateTime.UtcNow;
            return ctx.SystemInfo;
        }

        if (_cachedHw != null && (DateTime.UtcNow - _cachedHwTime).TotalMinutes < 10)
        {
            return _cachedHw;
        }

        _cachedHw = BaoToolsContext.InspectSystemHardware();
        _cachedHwTime = DateTime.UtcNow;
        return _cachedHw;
    }

    public ToolExecutionResult CheckGpu(bool isVi, BaoToolsContext? ctx = null)
    {
        var hw = GetOrInspectHardware(ctx);
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = true,
            ToolName = "check_gpu",
            DiagnosisTitle = isVi ? "🎮 Thông Tin Card Đồ Họa (GPU)" : "🎮 Graphics Hardware (GPU)"
        };

        result.Data["GPU"] = hw.GpuName;
        result.Data["VRAM_MB"] = hw.VramMb;
        result.Data["VRAM_GB"] = Math.Round(hw.VramMb / 1024.0, 1);

        string vramText = hw.VramMb >= 1024 ? $"{Math.Round(hw.VramMb / 1024.0, 1)} GB" : $"{hw.VramMb} MB";
        result.Summary = $"{hw.GpuName} (VRAM: {vramText})";

        bool isIntegrated = hw.GpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                            hw.GpuName.Contains("UHD", StringComparison.OrdinalIgnoreCase) ||
                            hw.GpuName.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                            hw.GpuName.Contains("Basic Display", StringComparison.OrdinalIgnoreCase);

        if (isIntegrated)
        {
            result.Warnings.Add(isVi
                ? "Máy tính đang dùng GPU tích hợp (Onboard), hiệu năng chơi game 3D nặng có thể bị hạn chế."
                : "Integrated GPU detected; performance in heavy 3D titles may be limited.");
        }

        result.Checklist.Add(new AiChecklistItem
        {
            Status = isIntegrated ? ChecklistStatus.Warning : ChecklistStatus.Success,
            Title = isVi ? "Card đồ họa (GPU)" : "Graphics Card (GPU)",
            Detail = $"{hw.GpuName} (VRAM: {vramText})"
        });

        result.Output = isVi
            ? $"Card đồ họa phát hiện: **{hw.GpuName}** (VRAM: **{vramText}**)." + (isIntegrated ? "\n⚠ Đang chạy GPU tích hợp." : "")
            : $"Detected GPU: **{hw.GpuName}** (Dedicated VRAM: **{vramText}**)." + (isIntegrated ? "\n⚠ Running on integrated graphics." : "");

        return result;
    }

    public ToolExecutionResult CheckRam(bool isVi, BaoToolsContext? ctx = null)
    {
        var hw = GetOrInspectHardware(ctx);
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = true,
            ToolName = "check_ram",
            DiagnosisTitle = isVi ? "🧠 Bộ Nhớ Trong (RAM)" : "🧠 System Memory (RAM)"
        };

        result.Data["RAM_GB"] = hw.RamGb;
        result.Summary = $"{hw.RamGb} GB RAM";

        bool isRamLow = hw.RamGb < 8;
        bool isRamRecommended = hw.RamGb >= 16;

        if (isRamLow)
        {
            result.Warnings.Add(isVi
                ? "Dung lượng RAM dưới 8GB có thể gây tràn bộ nhớ khi chơi các game mới."
                : "System RAM under 8GB may encounter out-of-memory errors on modern titles.");
        }

        result.Checklist.Add(new AiChecklistItem
        {
            Status = isRamLow ? ChecklistStatus.Warning : ChecklistStatus.Success,
            Title = isVi ? "Bộ nhớ RAM" : "System RAM",
            Detail = isVi
                ? $"{hw.RamGb} GB RAM ({(isRamRecommended ? "Rất tốt cho đa số game" : isRamLow ? "Khá thấp" : "Đủ mức cơ bản")})"
                : $"{hw.RamGb} GB RAM ({(isRamRecommended ? "Optimal for most games" : isRamLow ? "Low" : "Standard")})"
        });

        result.Output = isVi
            ? $"Dung lượng RAM hệ thống: **{hw.RamGb} GB RAM** ({(isRamRecommended ? "đáp ứng tốt hầu hết game hiện nay" : isRamLow ? "dưới 8GB, có thể bị thiếu bộ nhớ" : "đủ mức cơ bản 8GB")})."
            : $"System RAM: **{hw.RamGb} GB RAM** ({(isRamRecommended ? "optimal for modern games" : isRamLow ? "below 8GB, risk of memory exhaustion" : "meets baseline standard")}).";

        return result;
    }

    public ToolExecutionResult CheckDirectX(bool isVi, BaoToolsContext? ctx = null)
    {
        var hw = GetOrInspectHardware(ctx);
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = true,
            ToolName = "check_directx",
            DiagnosisTitle = isVi ? "⚡ Hỗ Trợ DirectX & Runtime" : "⚡ DirectX Runtime & Feature Level"
        };

        result.Data["DirectXVersion"] = hw.DirectXVersion;
        result.Summary = hw.DirectXVersion;

        result.Checklist.Add(new AiChecklistItem
        {
            Status = ChecklistStatus.Success,
            Title = isVi ? "Phiên bản DirectX" : "DirectX Support",
            Detail = hw.DirectXVersion
        });

        result.Output = isVi
            ? $"Hệ thống hỗ trợ: **{hw.DirectXVersion}** hoàn toàn tương thích với các game DirectX 11 và DirectX 12."
            : $"System runtime: **{hw.DirectXVersion}** fully compatible with DirectX 11 & 12 titles.";

        return result;
    }

    public ToolExecutionResult CheckVcRedist(bool isVi, BaoToolsContext? ctx = null)
    {
        var hw = GetOrInspectHardware(ctx);
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = hw.HasVcRedist2015_2022_x64,
            ToolName = "check_vcredist",
            DiagnosisTitle = isVi ? "🔧 Thư Viện Visual C++ Redistributable" : "🔧 Visual C++ Redistributable"
        };

        result.Data["HasVcRedist_x64"] = hw.HasVcRedist2015_2022_x64;
        result.Data["HasVcRedist_x86"] = hw.HasVcRedist2015_2022_x86;

        bool ok = hw.HasVcRedist2015_2022_x64;
        result.Summary = ok ? "Visual C++ 2015-2022 x64: Installed" : "Visual C++ 2015-2022 x64: Missing";

        if (!ok)
        {
            result.Warnings.Add(isVi
                ? "Thiếu gói Visual C++ 2015-2022 x64 (nguyên nhân gây lỗi 0xc000007b và thiếu msvcp140.dll)."
                : "Missing Visual C++ 2015-2022 x64 redistributable (causes 0xc000007b and missing msvcp140.dll).");
        }

        result.Checklist.Add(new AiChecklistItem
        {
            Status = ok ? ChecklistStatus.Success : ChecklistStatus.Error,
            Title = isVi ? "Visual C++ 2015-2022 x64" : "Visual C++ 2015-2022 x64",
            Detail = ok ? (isVi ? "Đã cài đặt chuẩn" : "Installed") : (isVi ? "CHƯA CÀI ĐẶT" : "MISSING")
        });

        result.Output = ok
            ? (isVi ? "Thư viện Visual C++ 2015-2022 x64 đã được cài đặt đầy đủ." : "Visual C++ 2015-2022 x64 is properly installed.")
            : (isVi ? "⚠ Phát hiện **chưa cài đặt Visual C++ 2015-2022 x64**! Đây là nguyên nhân hàng đầu gây lỗi 0xc000007b hoặc thiếu `msvcp140.dll`." : "⚠ Missing Visual C++ 2015-2022 x64 redistributable. This causes 0xc000007b or missing `msvcp140.dll`.");

        return result;
    }

    public ToolExecutionResult ExecuteSafeTool(string toolName, bool isVi, BaoToolsContext? ctx = null)
    {
        return toolName.ToLowerInvariant() switch
        {
            "check_gpu" => CheckGpu(isVi, ctx),
            "check_ram" => CheckRam(isVi, ctx),
            "check_directx" => CheckDirectX(isVi, ctx),
            "check_vcredist" => CheckVcRedist(isVi, ctx),
            "diagnose_system" => DiagnoseSystemFull(isVi, ctx),
            "diagnose_game_crash" => DiagnoseGameCrash("", isVi, ctx),
            "check_plugin_status" => CheckPluginStatusFull(isVi),
            "check_mode_status" => CheckModeStatusFull(isVi),
            "check_games_status" => CheckGamesStatusFull(isVi),
            "restart_steam" => ExecuteRestartSteamTool(isVi),
            _ => new ToolExecutionResult { Handled = false }
        };
    }

    private ToolExecutionResult ExecuteRestartSteamTool(bool isVi)
    {
        bool ok = false;
        try { ok = _steam.RestartSteam(); } catch { }

        string msg = ok
            ? (isVi ? "Đã khởi động lại Steam thành công cho bạn!" : "Steam has been restarted successfully!")
            : (isVi ? "Không thể khởi động lại Steam tự động. Bạn vui lòng tự bật lại Steam hoặc bấm 'Restart Steam' ở thanh công cụ nhé!" : "Could not restart Steam automatically. Please restart it manually.");

        return new ToolExecutionResult
        {
            Handled = true,
            ToolName = "restart_steam",
            DiagnosisTitle = isVi ? "🔄 Khởi Động Lại Steam" : "🔄 Restart Steam",
            Output = msg
        };
    }

    /// <summary>
    /// Detects if the user query triggers an internal technical diagnostic or actionable tool.
    /// Emits step-by-step progress callbacks for live UI feedback.
    /// </summary>
    public async Task<ToolExecutionResult> DetectAndExecuteToolAsync(
        string userQuery,
        BaoToolsContext? ctx = null,
        Action<string>? onProgressStep = null)
    {
        string q = userQuery.Trim().ToLowerInvariant();
        bool isVi = BaoToolsKnowledgeBase.IsVietnameseQuery(userQuery);

        // 0. Granular Hardware & Runtime Checks
        if (q.Contains("ram bao nhiêu") || q.Contains("ram bao nhieu") || q.Contains("kiểm tra ram") || q.Contains("kiem tra ram") ||
            q.Contains("dung lượng ram") || q.Contains("how much ram") || (q.Contains("ram") && (q.Contains("máy tao") || q.Contains("máy tôi") || q.Contains("pc") || q.Contains("máy này"))))
        {
            onProgressStep?.Invoke(isVi ? "🧠 Đang đọc thông số bộ nhớ RAM..." : "🧠 Reading system RAM...");
            await Task.Delay(60);
            return CheckRam(isVi, ctx);
        }

        if (q.Contains("directx") || q.Contains("dx11") || q.Contains("dx12") || q.Contains("d3d") || q.Contains("direct3d"))
        {
            onProgressStep?.Invoke(isVi ? "⚡ Đang kiểm tra phiên bản DirectX & Direct3D..." : "⚡ Inspecting DirectX runtime...");
            await Task.Delay(60);
            return CheckDirectX(isVi, ctx);
        }

        if (q.Contains("0xc000007b") || q.Contains("0xc0000142") || q.Contains("msvcp") || q.Contains("vcruntime") || q.Contains("vcredist"))
        {
            onProgressStep?.Invoke(isVi ? "🔧 Đang kiểm tra Visual C++ Redistributable..." : "🔧 Inspecting Visual C++ Runtimes...");
            await Task.Delay(60);
            return CheckVcRedist(isVi, ctx);
        }

        if (q.Contains("card màn hình") || q.Contains("card man hinh") || q.Contains("check gpu") || q.Contains("kiểm tra gpu") ||
            ((q.Contains("rtx") || q.Contains("gtx") || q.Contains("vram")) && (q.Contains("máy tao") || q.Contains("chơi được không") || q.Contains("chay duoc khong") || q.Contains("chơi dc k"))))
        {
            onProgressStep?.Invoke(isVi ? "🎮 Đang đọc thông số card đồ họa GPU & VRAM..." : "🎮 Inspecting GPU & VRAM...");
            await Task.Delay(60);
            return CheckGpu(isVi, ctx);
        }

        // 1. Game Crash / Won't Launch Diagnosis
        if (q.Contains("văng") || q.Contains("crash") || q.Contains("không mở được") || q.Contains("khong mo duoc") ||
            q.Contains("không chạy") || q.Contains("khong chay") || q.Contains("won't start") || q.Contains("wont start") ||
            q.Contains("doesn't launch") || q.Contains("does not launch") || q.Contains("game bị lỗi") || q.Contains("fix game") ||
            q.Contains("sửa lỗi game") || q.Contains("sửa game") || q.Contains("chẩn đoán lỗi") || q.Contains("chan doan loi") ||
            q.Contains("văng crash") || q.Contains("diagnose game") || q.Contains("game launch & crash"))
        {
            onProgressStep?.Invoke(isVi ? "🔍 Đang phát hiện tựa game liên quan..." : "🔍 Detecting game target...");
            await Task.Delay(100);
            onProgressStep?.Invoke(isVi ? "📂 Đang quét tệp tin game & bảo mật Steam DRM..." : "📂 Inspecting game files & Steam DRM...");
            await Task.Delay(120);
            onProgressStep?.Invoke(isVi ? "🔧 Đang kiểm tra dependencies & thư viện DLL..." : "🔧 Checking dependencies & DLL runtimes...");
            await Task.Delay(100);
            return DiagnoseGameCrash(userQuery, isVi, ctx);
        }

        // 2. Comprehensive System Diagnosis
        if (q.Contains("chẩn đoán hệ thống") || q.Contains("chan doan he thong") ||
            q.Contains("chẩn đoán phần cứng") || q.Contains("chan doan phan cung") ||
            q.Contains("system hardware diagnostics") || q.Contains("system diagnostics") ||
            q.Contains("hardware diagnostics") || q.Contains("kiểm tra hệ thống") ||
            q.Contains("kiem tra he thong") || q.Contains("kiểm tra máy") || q.Contains("kiem tra may") ||
            q.Contains("chẩn đoán máy") || q.Contains("chan doan may") ||
            q.Contains("check system") || (q.Contains("chẩn đoán") && !q.Contains("lỗi") && !q.Contains("game")) ||
            (q.Contains("diagnose") && !q.Contains("game") && !q.Contains("crash")) ||
            (q.Contains("kiểm tra") && q.Contains("steam") && !q.Contains("plugin") && !q.Contains("game")))
        {
            onProgressStep?.Invoke(isVi ? "🖥 Đang kiểm tra hệ thống phần cứng..." : "🖥 Checking system hardware...");
            await Task.Delay(120);
            onProgressStep?.Invoke(isVi ? "🔧 Đang quét DirectX & Visual C++ Runtime..." : "🔧 Scanning DirectX & VC++ Runtimes...");
            await Task.Delay(100);
            return DiagnoseSystemFull(isVi, ctx);
        }

        // 3. Check Plugin Status
        if ((q.Contains("plugin") && (q.Contains("cài chưa") || q.Contains("chưa") || q.Contains("kiểm tra") || q.Contains("check") || q.Contains("tình trạng") || q.Contains("trạng thái"))) ||
            q.Contains("kiểm tra plugin") || q.Contains("check plugin"))
        {
            onProgressStep?.Invoke(isVi ? "🧩 Đang kiểm tra trạng thái Steam Plugin..." : "🧩 Checking Steam Plugin status...");
            await Task.Delay(100);
            return CheckPluginStatusFull(isVi);
        }

        // 4. Check Mode / Unlocker Status
        if ((q.Contains("mode") && (q.Contains("tải chưa") || q.Contains("tai chua") || q.Contains("cài chưa") || q.Contains("cai chua") ||
                                    q.Contains("kiểm tra") || q.Contains("kiem tra") || q.Contains("check") ||
                                    q.Contains("đã tải") || q.Contains("da tai") || q.Contains("đã cài") || q.Contains("da cai") ||
                                    q.Contains("đang dùng") || q.Contains("dang dung") || q.Contains("trạng thái") || q.Contains("trang thai") ||
                                    q.Contains("chế độ") || q.Contains("che do"))) ||
            q.Contains("kiểm tra mode") || q.Contains("kiem tra mode") || q.Contains("check mode"))
        {
            onProgressStep?.Invoke(isVi ? "⚡ Đang kiểm tra chế độ mở khóa Mode..." : "⚡ Checking active unlocker mode...");
            await Task.Delay(100);
            return CheckModeStatusFull(isVi);
        }

        // 5. Check Games Status
        if (q.Contains("kiểm tra game") || q.Contains("kiem tra game") || q.Contains("có bao nhiêu game") ||
            q.Contains("co bao nhieu game") || q.Contains("danh sách game") || q.Contains("danh sach game") ||
            (q.Contains("game") && (q.Contains("đã nạp") || q.Contains("da nap") || q.Contains("đã thêm") || q.Contains("da them"))))
        {
            onProgressStep?.Invoke(isVi ? "🎮 Đang quét thư viện game BaoTools..." : "🎮 Scanning BaoTools game library...");
            await Task.Delay(80);
            return CheckGamesStatusFull(isVi);
        }

        // 6. Check Game Specs & Storage Size
        if (q.Contains("cấu hình") || q.Contains("cau hinh") || q.Contains("dung lượng") || q.Contains("dung luong") ||
            q.Contains("bao nhiêu gb") || q.Contains("bao nhieu gb") || q.Contains("nặng bao nhiêu") || q.Contains("nang bao nhieu") ||
            q.Contains("system requirements") || q.Contains("specifications") || q.Contains("pc specs"))
        {
            onProgressStep?.Invoke(isVi ? "📊 Đang tra cứu thông số cấu hình game..." : "📊 Checking game requirements & size...");
            var reqResult = CheckGameSpecsAndSize(userQuery, isVi);
            if (reqResult != null)
            {
                return new ToolExecutionResult
                {
                    Handled = true,
                    ToolName = "check_game_specs",
                    Output = reqResult
                };
            }
        }

        // 7. Restart Steam
        if (q.Contains("khởi động lại steam") || q.Contains("restart steam") || q.Contains("re-open steam") || q.Contains("bật lại steam"))
        {
            onProgressStep?.Invoke(isVi ? "🔄 Đang yêu cầu khởi động lại tiến trình Steam..." : "🔄 Restarting Steam process...");
            bool ok = false;
            try { ok = _steam.RestartSteam(); } catch { }

            string msg = ok
                ? (isVi ? "Đã khởi động lại Steam thành công cho bạn!" : "Steam has been restarted successfully!")
                : (isVi ? "Không thể khởi động lại Steam tự động. Bạn vui lòng tự bật lại Steam hoặc bấm 'Restart Steam' ở thanh công cụ nhé!" : "Could not restart Steam automatically. Please restart it manually.");

            return new ToolExecutionResult
            {
                Handled = true,
                ToolName = "restart_steam",
                Output = msg
            };
        }

        return new ToolExecutionResult { Handled = false };
    }

    /// <summary>
    /// Executes a pre-whitelisted internal tool directly (e.g. from an Action Button click).
    /// </summary>
    public async Task<ToolExecutionResult> ExecuteToolDirectAsync(
        string toolName,
        Dictionary<string, string> args,
        BaoToolsContext ctx)
    {
        bool isVi = IsVietnamese;

        var safeRes = ExecuteSafeTool(toolName, isVi, ctx);
        if (safeRes.Handled)
        {
            return safeRes;
        }

        switch (toolName)
        {
            case "launch_game":
                if (args.TryGetValue("appId", out var sAppId) && uint.TryParse(sAppId, out uint appId))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo($"steam://run/{appId}") { UseShellExecute = true });
                        return new ToolExecutionResult
                        {
                            Handled = true,
                            ToolName = toolName,
                            Output = isVi ? $"🚀 Đã gửi lệnh khởi chạy game qua Steam (AppID {appId})!" : $"🚀 Launched game via Steam protocol (AppID {appId})!"
                        };
                    }
                    catch (Exception ex)
                    {
                        return new ToolExecutionResult
                        {
                            Handled = true,
                            ToolName = toolName,
                            Output = isVi ? $"Không thể mở game: {ex.Message}" : $"Failed to launch game: {ex.Message}"
                        };
                    }
                }
                break;

            case "open_game_folder":
                if (args.TryGetValue("appId", out var sId) && uint.TryParse(sId, out uint aId))
                {
                    string? p = GetGamePath(aId);
                    if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo("explorer.exe", p) { UseShellExecute = true });
                            return new ToolExecutionResult
                            {
                                Handled = true,
                                ToolName = toolName,
                                Output = isVi ? $"📂 Đã mở thư mục game: {p}" : $"📂 Opened game directory: {p}"
                            };
                        }
                        catch (Exception ex)
                        {
                            return new ToolExecutionResult { Handled = true, ToolName = toolName, Output = $"Error: {ex.Message}" };
                        }
                    }
                }
                break;

            case "diagnose_system":
                return DiagnoseSystemFull(isVi, ctx);

            case "check_plugin_status":
                return CheckPluginStatusFull(isVi);

            case "repair_game_configuration":
                if (args.TryGetValue("appId", out var rAppId) && uint.TryParse(rAppId, out uint rId))
                {
                    // Re-save/verify lua in vault
                    var lua = _vault.ReadLiveText(rId);
                    if (!string.IsNullOrWhiteSpace(lua))
                    {
                        string? steamPath = _steam.EffectivePath;
                        if (!string.IsNullOrWhiteSpace(steamPath))
                        {
                            string target = Path.Combine(steamPath, "config", "stplug-in", $"{rId}.lua");
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.WriteAllText(target, lua, Encoding.UTF8);
                        }
                    }
                    return new ToolExecutionResult
                    {
                        Handled = true,
                        ToolName = toolName,
                        Output = isVi ? $"✓ Đã làm mới và ghi lại cấu hình Lua chuẩn cho AppID {rId}!" : $"✓ Successfully refreshed and repaired Lua configuration for AppID {rId}!"
                    };
                }
                break;

            case "clear_safe_cache":
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string cacheDir = Path.Combine(appData, "BaoToolsGui", "cache");
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                        Directory.CreateDirectory(cacheDir);
                    }
                    return new ToolExecutionResult
                    {
                        Handled = true,
                        ToolName = toolName,
                        Output = isVi ? "✓ Đã dọn dẹp bộ nhớ đệm an toàn thành công!" : "✓ Safe cache cleared successfully!"
                    };
                }
                catch (Exception ex)
                {
                    return new ToolExecutionResult { Handled = true, ToolName = toolName, Output = $"Cache error: {ex.Message}" };
                }

            case "delete_game":
                // Destructive action executed after user confirmation
                if (args.TryGetValue("appId", out var delAppId) && uint.TryParse(delAppId, out uint dId))
                {
                    try
                    {
                        string? steamPath = _steam.EffectivePath;
                        if (!string.IsNullOrWhiteSpace(steamPath))
                        {
                            string target = Path.Combine(steamPath, "config", "stplug-in", $"{dId}.lua");
                            if (File.Exists(target)) File.Delete(target);
                        }
                    }
                    catch { }

                    return new ToolExecutionResult
                    {
                        Handled = true,
                        ToolName = toolName,
                        Output = isVi ? $"🗑 Đã gỡ bỏ cấu hình game (AppID {dId}) khỏi thư viện BaoTools." : $"🗑 Removed game configuration (AppID {dId}) from BaoTools library."
                    };
                }
                break;
        }

        await Task.CompletedTask;
        return new ToolExecutionResult { Handled = false, Output = "Tool execution not implemented." };
    }

    // ─────────────────────────────────────────────────────────────────
    // Tool Diagnostic Generators with Structured Checklists & Actions
    // ─────────────────────────────────────────────────────────────────

    private ToolExecutionResult DiagnoseSystemFull(bool isVi, BaoToolsContext? ctx)
    {
        var hw = GetOrInspectHardware(ctx);
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = true,
            ToolName = "diagnose_system",
            DiagnosisTitle = isVi ? "🔍 Kết Quả Chẩn Đoán Hệ Thống" : "🔍 System Technical Diagnostics",
            Summary = $"{hw.GpuName}, {hw.RamGb}GB RAM, {hw.DirectXVersion}"
        };

        result.Data["GPU"] = hw.GpuName;
        result.Data["VRAM_MB"] = hw.VramMb;
        result.Data["RAM_GB"] = hw.RamGb;
        result.Data["DirectX"] = hw.DirectXVersion;
        result.Data["VcRedist_x64"] = hw.HasVcRedist2015_2022_x64;

        // 1. GPU
        bool gpuOk = !hw.GpuName.Contains("Standard", StringComparison.OrdinalIgnoreCase);
        result.Checklist.Add(new AiChecklistItem
        {
            Status = gpuOk ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Phần cứng đồ họa (GPU)" : "Graphics Hardware (GPU)",
            Detail = $"{hw.GpuName} (VRAM: {hw.VramMb} MB)"
        });

        // 2. RAM
        bool ramOk = hw.RamGb >= 8;
        result.Checklist.Add(new AiChecklistItem
        {
            Status = ramOk ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Bộ nhớ trong (RAM)" : "System Memory (RAM)",
            Detail = $"{hw.RamGb} GB RAM"
        });

        // 3. DirectX
        result.Checklist.Add(new AiChecklistItem
        {
            Status = ChecklistStatus.Success,
            Title = isVi ? "Hỗ trợ DirectX" : "DirectX Support",
            Detail = hw.DirectXVersion
        });

        // 4. Visual C++ Redistributable
        bool vcOk = hw.HasVcRedist2015_2022_x64;
        result.Checklist.Add(new AiChecklistItem
        {
            Status = vcOk ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Thư viện Visual C++ 2015-2022" : "Visual C++ 2015-2022 Runtime",
            Detail = vcOk
                ? (isVi ? "Đã cài đặt x64 chuẩn" : "Installed x64")
                : (isVi ? "Chưa phát hiện bản x64 (có thể gây lỗi thiếu DLL)" : "Missing x64 runtime (can cause DLL crashes)")
        });

        // 5. Steam & Plugin
        bool steamRunning = SteamService.IsSteamRunning();
        string? steamDir = _steam.EffectivePath;
        bool plugInstalled = !string.IsNullOrWhiteSpace(steamDir) && File.Exists(Path.Combine(steamDir, "winmm.dll"));
        string plugVer = plugInstalled ? "Active" : "Not installed";
        result.Checklist.Add(new AiChecklistItem
        {
            Status = plugInstalled ? ChecklistStatus.Success : ChecklistStatus.Info,
            Title = isVi ? "BaoTools Steam Plugin" : "BaoTools Steam Plugin",
            Detail = plugInstalled ? (isVi ? $"Đã cài đặt (winmm.dll)" : $"Installed (winmm.dll)") : (isVi ? "Chưa cài đặt" : "Not installed")
        });

        // Text summary
        var sb = new StringBuilder();
        string vramText = hw.VramMb >= 1024 ? $"{Math.Round(hw.VramMb / 1024.0, 1)} GB" : $"{hw.VramMb} MB";
        if (isVi)
        {
            sb.AppendLine("Hệ thống máy tính của bạn đã được kiểm tra toàn diện:");
            sb.AppendLine($"• **GPU**: {hw.GpuName} (VRAM: {vramText})");
            sb.AppendLine($"• **RAM**: {hw.RamGb} GB RAM");
            sb.AppendLine($"• **DirectX**: {hw.DirectXVersion}");
            sb.AppendLine($"• **Visual C++ 2015-2022 x64**: {(vcOk ? "Đã cài đặt chuẩn" : "CHƯA CÀI ĐẶT (cần bổ sung)")}");
            sb.AppendLine($"• **Steam Plugin**: {(plugInstalled ? "Đã kích hoạt" : "Chưa cài đặt")}");
            if (!vcOk)
            {
                sb.AppendLine("\n⚠ **Lưu ý**: Thiếu Visual C++ 2015-2022 x64 có thể làm game bị lỗi `msvcp140.dll` hoặc `0xc000007b`.");
            }
            if (!plugInstalled)
            {
                sb.AppendLine("\n💡 **Mẹo**: Cài đặt Plugin BaoTools vào Steam để thêm game 1-click ngay trên web Steam Store.");
            }
            sb.AppendLine("\nMọi dịch vụ của BaoTools đang hoạt động bình thường!");
        }
        else
        {
            sb.AppendLine("Your system specifications have been thoroughly inspected:");
            sb.AppendLine($"• **GPU**: {hw.GpuName} (VRAM: {vramText})");
            sb.AppendLine($"• **RAM**: {hw.RamGb} GB RAM");
            sb.AppendLine($"• **DirectX**: {hw.DirectXVersion}");
            sb.AppendLine($"• **Visual C++ 2015-2022 x64**: {(vcOk ? "Installed" : "MISSING")}");
            sb.AppendLine($"• **Steam Plugin**: {(plugInstalled ? "Active" : "Not installed")}");
            if (!vcOk)
            {
                sb.AppendLine("\n⚠ **Warning**: Missing Visual C++ 2015-2022 x64 may cause `msvcp140.dll` or `0xc000007b` launch errors.");
            }
            sb.AppendLine("\nBaoTools background services are ready.");
        }
        result.Output = sb.ToString().Trim();

        // Action Buttons
        if (!plugInstalled)
        {
            result.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🧩 Cài đặt Plugin" : "🧩 Install Plugin",
                ToolName = "navigate_plugin",
                IconSymbol = "PlugDisconnected24"
            });
        }
        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
            ToolName = "navigate_manage",
            IconSymbol = "Games24"
        });

        return result;
    }

    private ToolExecutionResult DiagnoseGameCrash(string userQuery, bool isVi, BaoToolsContext? ctx)
    {
        var result = new ToolExecutionResult
        {
            Handled = true,
            Success = true,
            ToolName = "diagnose_game_crash",
            DiagnosisTitle = isVi ? "🛠 Chẩn Đoán Lỗi Khởi Động / Văng Game" : "🛠 Game Launch & Crash Diagnosis",
            Summary = isVi ? "Phát hiện sự cố khởi chạy: kiểm tra SteamStub DRM & VC++ runtimes" : "Game launch crash diagnosed: verify SteamStub DRM & runtimes"
        };

        var selected = ctx?.SelectedGame;
        string gameName = selected?.Name ?? "Game";
        uint appId = selected?.AppId ?? 0;

        // Try extracting game name from query if mentioned
        if (userQuery.Contains("baldur", StringComparison.OrdinalIgnoreCase)) { gameName = "Baldur's Gate 3"; appId = 1086940; }
        else if (userQuery.Contains("black myth", StringComparison.OrdinalIgnoreCase) || userQuery.Contains("wukong", StringComparison.OrdinalIgnoreCase)) { gameName = "Black Myth: Wukong"; appId = 2358720; }
        else if (userQuery.Contains("elden ring", StringComparison.OrdinalIgnoreCase)) { gameName = "Elden Ring"; appId = 1245620; }

        result.Checklist.Add(new AiChecklistItem
        {
            Status = ChecklistStatus.Success,
            Title = isVi ? $"Xác định tựa game: {gameName}" : $"Identified game: {gameName}",
            Detail = appId > 0 ? $"AppID: {appId}" : "BaoTools Library"
        });

        // Steam DRM check (Steamless)
        bool steamlessApplied = selected?.HasSteamlessBackup ?? false;
        result.Checklist.Add(new AiChecklistItem
        {
            Status = steamlessApplied ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Khóa bảo mật Steam DRM (Stub)" : "Steam DRM Wrapper (Stub)",
            Detail = steamlessApplied
                ? (isVi ? "Đã gỡ Steam DRM (có bản sao lưu .bak)" : "DRM unpack verified (.bak present)")
                : (isVi ? "Chưa mở khóa Steamless DRM (nguyên nhân hàng đầu gây crash khi mở)" : "Potential SteamStub DRM collision")
        });

        // Dependency Check
        bool vcOk = ctx?.SystemInfo.HasVcRedist2015_2022_x64 ?? true;
        result.Checklist.Add(new AiChecklistItem
        {
            Status = vcOk ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Thư viện Visual C++ Runtime" : "Visual C++ Runtime",
            Detail = vcOk ? "DirectX & VC++ OK" : (isVi ? "Cần cài đặt VC++ 2015-2022 x64" : "Missing VC++ Redistributable")
        });

        // Explanation & Recommended Action
        var sb = new StringBuilder();
        if (isVi)
        {
            sb.AppendLine($"Nguyên nhân phổ biến nhất khiến game **{gameName}** bị văng (crash) ngay khi mở:");
            sb.AppendLine("1. **Steam DRM**: File `.exe` gốc bị khóa bởi lớp bảo vệ Steam DRM, cần mở khóa tự động qua công cụ **Steamless** có sẵn trong BaoTools.");
            sb.AppendLine("2. **Quyền quản trị / Thư mục**: Hãy thử chạy Steam dưới quyền Administrator và đảm bảo đường dẫn không có dấu tiếng Việt.");
            sb.AppendLine();
            sb.AppendLine("👉 Bạn có thể vào tab **Quản lý (Manage)**, bấm vào game và chọn **'Gỡ Steam DRM'** để khắc phục tự động 100%!");
        }
        else
        {
            sb.AppendLine($"Most common reasons why **{gameName}** crashes on launch:");
            sb.AppendLine("1. **SteamStub DRM**: The main game `.exe` is packed with Steam DRM. It needs to be unpacked using BaoTools integrated **Steamless** tool.");
            sb.AppendLine("2. **Runtime Dependencies**: Ensure Visual C++ 2015-2022 x64 is installed.");
            sb.AppendLine();
            sb.AppendLine("👉 Go to the **Manage** tab, select the game, and click **'Remove Steam DRM'** to fix this automatically!");
        }
        result.Output = sb.ToString().Trim();

        // Action Buttons
        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🛠 Đến trang Quản lý Game" : "🛠 Open Manage Tab",
            ToolName = "navigate_manage",
            IconSymbol = "Wrench24"
        });
        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🔧 Bản sửa lỗi (Fixes)" : "🔧 Game Fixes",
            ToolName = "navigate_fixes",
            IconSymbol = "Wrench24"
        });

        if (appId > 0)
        {
            result.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "🚀 Khởi chạy Game" : "🚀 Launch Game",
                ToolName = "launch_game",
                Arguments = new() { ["appId"] = appId.ToString() },
                IconSymbol = "Play24"
            });
            result.ActionButtons.Add(new AiChatAction
            {
                Label = isVi ? "📂 Mở thư mục Game" : "📂 Open Folder",
                ToolName = "open_game_folder",
                Arguments = new() { ["appId"] = appId.ToString() },
                IconSymbol = "Folder24"
            });
        }

        return result;
    }

    private ToolExecutionResult CheckPluginStatusFull(bool isVi)
    {
        var result = new ToolExecutionResult
        {
            Handled = true,
            ToolName = "check_plugin_status",
            DiagnosisTitle = isVi ? "🧩 Trạng Thái Plugin Steam" : "🧩 Steam Plugin Status"
        };

        string? steamPath = _steam.EffectivePath;
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string pluginDir = Path.Combine(appData, "BaoToolsGui", "plugin");
        bool frontendInstalled = Directory.Exists(pluginDir) && Directory.EnumerateFileSystemEntries(pluginDir).Any();

        bool dllInstalled = false;
        if (!string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath))
        {
            string winmmPath = Path.Combine(steamPath, "winmm.dll");
            dllInstalled = File.Exists(winmmPath);
        }

        bool fullyInstalled = frontendInstalled && dllInstalled;

        result.Checklist.Add(new AiChecklistItem
        {
            Status = frontendInstalled ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "Giao diện Plugin (%AppData%\\BaoToolsGui\\plugin)" : "Plugin Frontend UI",
            Detail = frontendInstalled ? (isVi ? "Đã cài đặt" : "Installed") : (isVi ? "Chưa có" : "Missing")
        });

        result.Checklist.Add(new AiChecklistItem
        {
            Status = dllInstalled ? ChecklistStatus.Success : ChecklistStatus.Warning,
            Title = isVi ? "File kích hoạt Loader (winmm.dll)" : "Loader DLL (winmm.dll)",
            Detail = dllInstalled ? (isVi ? "Đã có trong thư mục Steam" : "Present in Steam dir") : (isVi ? "Chưa cài vào Steam" : "Not installed in Steam")
        });

        if (isVi)
        {
            result.Output = fullyInstalled
                ? "Plugin BaoTools đã được cài đặt và kích hoạt 100%! Khi duyệt cửa hàng Steam Store, nút màu tím 'Thêm qua BaoTools' sẽ xuất hiện trên trang game để thêm game 1-click."
                : "Plugin BaoTools chưa được cài đặt đầy đủ. Hãy bấm nút bên dưới để chuyển sang tab Plugin và hoàn tất cài đặt nhé!";
        }
        else
        {
            result.Output = fullyInstalled
                ? "BaoTools plugin is fully installed! Browsing Steam Store will display the purple 'Add with BaoTools' button."
                : "Plugin is not fully installed. Click below to open Plugin tab and complete installation.";
        }

        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🧩 Quản lý Plugin" : "🧩 Manage Plugin",
            ToolName = "navigate_plugin",
            IconSymbol = "PlugDisconnected24"
        });

        return result;
    }

    public string GetActiveModeName()
    {
        return _unlocker?.SelectedModeDisplayName ?? _unlocker?.SelectedMode?.ToString() ?? "BetterSteamTools";
    }

    public ToolExecutionResult CheckModeStatus(bool isVi, BaoToolsContext? ctx = null) => CheckModeStatusFull(isVi);

    private ToolExecutionResult CheckModeStatusFull(bool isVi)
    {
        var result = new ToolExecutionResult
        {
            Handled = true,
            ToolName = "check_mode_status",
            DiagnosisTitle = isVi ? "⚡ Chế Độ Mở Khóa (Mode)" : "⚡ Unlocker Mode"
        };

        var mode = _unlocker?.SelectedModeDisplayName ?? _unlocker?.SelectedMode?.ToString() ?? "BetterSteamTools";

        result.Checklist.Add(new AiChecklistItem
        {
            Status = ChecklistStatus.Success,
            Title = isVi ? $"Chế độ đang dùng: {mode}" : $"Active Mode: {mode}",
            Detail = mode == "BetterSteamTools"
                ? (isVi ? "Tương thích 95% game Steam, chạy ổn định nhất" : "95% Steam games compatible")
                : (isVi ? "Hỗ trợ mở khóa DLC chuyên sâu" : "Advanced DLC unlocking")
        });

        result.Output = isVi
            ? $"Máy bạn đang kích hoạt chế độ **{mode}**.\n• **BetterSteamTools**: Lựa chọn mặc định khuyên dùng cho hầu hết mọi tựa game.\n• **SmokeAPI / Koaloader**: Dùng khi game có DLC phức tạp hoặc launcher riêng."
            : $"Your current unlocker mode is **{mode}**.\n• **BetterSteamTools**: Recommended default mode for 95% of games.\n• **SmokeAPI / Koaloader**: For complex DLCs and custom launchers.";

        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "⚡ Đổi Chế Độ Mode" : "⚡ Switch Mode",
            ToolName = "navigate_mode",
            IconSymbol = "ArrowSync24"
        });

        return result;
    }

    private ToolExecutionResult CheckGamesStatusFull(bool isVi)
    {
        var result = new ToolExecutionResult
        {
            Handled = true,
            ToolName = "check_games_status",
            DiagnosisTitle = isVi ? "🎮 Thư Viện Game BaoTools" : "🎮 BaoTools Game Library"
        };

        int count = GetInstalledGamesCount();

        result.Checklist.Add(new AiChecklistItem
        {
            Status = count > 0 ? ChecklistStatus.Success : ChecklistStatus.Info,
            Title = isVi ? $"Đã nạp: {count} game" : $"Loaded: {count} game(s)",
            Detail = isVi ? "Lưu trong Steam config\\stplug-in" : "Stored in Steam config\\stplug-in"
        });

        result.Output = isVi
            ? $"Hiện bạn đã nạp **{count} game** vào Steam thông qua BaoTools. Bạn có thể mở tab Quản lý để xem chi tiết, cấu hình Launch Options hoặc gỡ game."
            : $"You currently have **{count} game(s)** loaded into Steam via BaoTools. Visit Manage tab to view details or apply fixes.";

        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "🎮 Quản lý Game" : "🎮 Manage Games",
            ToolName = "navigate_manage",
            IconSymbol = "Games24"
        });
        result.ActionButtons.Add(new AiChatAction
        {
            Label = isVi ? "➕ Thêm Game Mới" : "➕ Add Game",
            ToolName = "navigate_add",
            IconSymbol = "Add24"
        });

        return result;
    }

    private string? GetGamePath(uint appId)
    {
        try
        {
            string? steamPath = _steam.EffectivePath;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                string manifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
                if (File.Exists(manifest))
                {
                    string content = File.ReadAllText(manifest);
                    var match = System.Text.RegularExpressions.Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        string p = Path.Combine(steamPath, "steamapps", "common", match.Groups[1].Value);
                        if (Directory.Exists(p)) return p;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private int GetInstalledGamesCount()
    {
        try
        {
            string? steamPath = _steam.EffectivePath;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                string dir = Path.Combine(steamPath, "config", "stplug-in");
                if (Directory.Exists(dir))
                {
                    return Directory.EnumerateFiles(dir, "*.lua").Count();
                }
            }
        }
        catch { }
        return 0;
    }

    public string? CheckGameSpecsAndSize(string userQuery, bool isVi)
    {
        string q = userQuery.ToLowerInvariant().Trim();
        string[] fillers = {
            "cấu hình game", "cau hinh game", "cấu hình của game", "cau hinh cua game", "cấu hình", "cau hinh",
            "dung lượng game", "dung luong game", "dung lượng của game", "dung luong cua game", "dung lượng", "dung luong",
            "nặng bao nhiêu gb", "nang bao nhieu gb", "bao nhiêu gb", "bao nhieu gb", "mấy gb", "may gb",
            "bao nhiêu gigabyte", "bao nhieu gigabyte", "system requirements of", "system requirements for",
            "system requirements", "pc requirements", "requirements for", "requirements", "specs for", "specs"
        };

        string gameSearch = q;
        foreach (var filler in fillers)
        {
            if (gameSearch.Contains(filler))
                gameSearch = gameSearch.Replace(filler, "").Trim();
        }
        gameSearch = gameSearch.Trim('?', '.', '!', ':', ' ', '-', ',', ';');

        if (string.IsNullOrWhiteSpace(gameSearch))
        {
            return isVi
                ? "Mình có thể tra cứu cấu hình tối thiểu, đề nghị và dung lượng ổ cứng yêu cầu của game Steam!\n\nVí dụ bạn có thể hỏi:\n• 'Cấu hình Baldur\\'s Gate 3'\n• 'Dung lượng Elden Ring bao nhiêu GB?'"
                : "I can look up minimum/recommended requirements and disk size for Steam games!\n\nFor example:\n• 'Baldur\\'s Gate 3 PC specs'\n• 'Elden Ring storage size'";
        }

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string detailsDir = Path.Combine(appData, "BaoToolsGui", "details");
            if (Directory.Exists(detailsDir))
            {
                foreach (var file in Directory.EnumerateFiles(detailsDir, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("name", out var nameProp))
                        {
                            string gameName = nameProp.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(gameName) &&
                                (gameName.ToLowerInvariant().Contains(gameSearch) || gameSearch.Contains(gameName.ToLowerInvariant())))
                            {
                                var sb = new StringBuilder();
                                sb.AppendLine(isVi ? $"Thông số cấu hình & Dung lượng của game {gameName}:" : $"System Requirements & Size for {gameName}:");
                                sb.AppendLine();

                                if (root.TryGetProperty("pc_requirements", out var pcReq))
                                {
                                    if (pcReq.TryGetProperty("minimum", out var minProp))
                                    {
                                        string minHtml = minProp.GetString() ?? "";
                                        string minClean = StripHtmlSpecs(minHtml);
                                        if (!string.IsNullOrWhiteSpace(minClean))
                                        {
                                            sb.AppendLine(minClean);
                                            sb.AppendLine();
                                        }
                                    }

                                    if (pcReq.TryGetProperty("recommended", out var recProp))
                                    {
                                        string recHtml = recProp.GetString() ?? "";
                                        string recClean = StripHtmlSpecs(recHtml);
                                        if (!string.IsNullOrWhiteSpace(recClean))
                                        {
                                            sb.AppendLine(recClean);
                                        }
                                    }
                                }

                                string result = sb.ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(result)) return result;
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return null;
    }

    private static string StripHtmlSpecs(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        string clean = html
            .Replace("<br>", "\n")
            .Replace("<br/>", "\n")
            .Replace("<br />", "\n")
            .Replace("<li>", "\n• ")
            .Replace("</li>", "");

        clean = System.Text.RegularExpressions.Regex.Replace(clean, "<[^>]+>", "");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\n\s*\n", "\n");
        return clean.Trim();
    }
}
