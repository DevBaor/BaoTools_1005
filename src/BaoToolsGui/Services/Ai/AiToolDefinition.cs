using System;
using System.Collections.Generic;

namespace BaoToolsGui.Services.Ai;

public enum ToolPermissionLevel
{
    Safe,
    DestructiveRequiresConfirmation
}

public class AiToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ToolPermissionLevel Permission { get; set; } = ToolPermissionLevel.Safe;
    public List<string> Parameters { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
}

public static class AiToolRegistry
{
    public static readonly Dictionary<string, AiToolDefinition> Tools = new()
    {
        // ── Informational / Diagnostic (Safe) ─────────────────────────
        ["get_selected_game"] = new AiToolDefinition
        {
            Name = "get_selected_game",
            Description = "Gets the currently selected game info in BaoTools library.",
            Permission = ToolPermissionLevel.Safe
        },
        ["get_game_info"] = new AiToolDefinition
        {
            Name = "get_game_info",
            Description = "Gets information and file structure of a game by AppID.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["get_game_path"] = new AiToolDefinition
        {
            Name = "get_game_path",
            Description = "Finds the local install path of a game by AppID in Steam common directory.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["get_installed_games"] = new AiToolDefinition
        {
            Name = "get_installed_games",
            Description = "Lists all games loaded into BaoTools Lua library.",
            Permission = ToolPermissionLevel.Safe
        },
        ["check_game_files"] = new AiToolDefinition
        {
            Name = "check_game_files",
            Description = "Checks if executable, DLCs, and depots exist for the game.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["scan_game_dependencies"] = new AiToolDefinition
        {
            Name = "scan_game_dependencies",
            Description = "Scans required game runtimes (DirectX, VC++ Redistributable, .NET).",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["check_directx"] = new AiToolDefinition
        {
            Name = "check_directx",
            Description = "Checks the system DirectX version and Direct3D feature levels.",
            Permission = ToolPermissionLevel.Safe
        },
        ["check_vcredist"] = new AiToolDefinition
        {
            Name = "check_vcredist",
            Description = "Checks installed Visual C++ Runtimes (2015-2022 x64 and x86).",
            Permission = ToolPermissionLevel.Safe
        },
        ["get_gpu_info"] = new AiToolDefinition
        {
            Name = "get_gpu_info",
            Description = "Retrieves GPU model name, dedicated VRAM, and driver details.",
            Permission = ToolPermissionLevel.Safe
        },
        ["get_system_info"] = new AiToolDefinition
        {
            Name = "get_system_info",
            Description = "Retrieves full OS, RAM, GPU, and architecture diagnostic information.",
            Permission = ToolPermissionLevel.Safe
        },
        ["diagnose_game"] = new AiToolDefinition
        {
            Name = "diagnose_game",
            Description = "Runs comprehensive automated diagnostic on game path, Steam DRM, dependencies, and fixes.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["get_plugin_status"] = new AiToolDefinition
        {
            Name = "get_plugin_status",
            Description = "Checks if BaoTools Steam Plugin is installed and working.",
            Permission = ToolPermissionLevel.Safe
        },
        ["get_download_status"] = new AiToolDefinition
        {
            Name = "get_download_status",
            Description = "Checks active download queue and daily quota limit.",
            Permission = ToolPermissionLevel.Safe
        },

        // ── Safe Action Tools ─────────────────────────────────────────
        ["verify_game_files"] = new AiToolDefinition
        {
            Name = "verify_game_files",
            Description = "Verifies game integrity and checks for missing depot files.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["repair_game_configuration"] = new AiToolDefinition
        {
            Name = "repair_game_configuration",
            Description = "Repairs corrupted Lua or unlocker configuration for the game.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },
        ["clear_safe_cache"] = new AiToolDefinition
        {
            Name = "clear_safe_cache",
            Description = "Clears temporary manifests and fastfetch cache safely.",
            Permission = ToolPermissionLevel.Safe
        },
        ["apply_game_fix"] = new AiToolDefinition
        {
            Name = "apply_game_fix",
            Description = "Applies Steamless DRM unpacking or game compatibility fix.",
            Parameters = new() { "appId", "fixType" },
            Permission = ToolPermissionLevel.Safe
        },
        ["launch_game"] = new AiToolDefinition
        {
            Name = "launch_game",
            Description = "Launches the game via Steam protocol (steam://run/{appId}).",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.Safe
        },

        // ── Destructive Actions (Confirmation Required) ───────────────
        ["delete_game"] = new AiToolDefinition
        {
            Name = "delete_game",
            Description = "Deletes game unlock configuration from BaoTools Lua library.",
            Parameters = new() { "appId" },
            Permission = ToolPermissionLevel.DestructiveRequiresConfirmation
        },
        ["remove_plugin"] = new AiToolDefinition
        {
            Name = "remove_plugin",
            Description = "Uninstalls the BaoTools plugin from Steam.",
            Permission = ToolPermissionLevel.DestructiveRequiresConfirmation
        }
    };
}
