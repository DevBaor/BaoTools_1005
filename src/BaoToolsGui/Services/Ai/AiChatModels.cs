using System;
using System.Collections.Generic;

namespace BaoToolsGui.Services.Ai;

public enum ChecklistStatus
{
    Success,
    Warning,
    Error,
    Info
}

public class AiChecklistItem
{
    public ChecklistStatus Status { get; set; } = ChecklistStatus.Success;
    public string Title { get; set; } = "";
    public string? Detail { get; set; }

    public string IconText => Status switch
    {
        ChecklistStatus.Success => "✓",
        ChecklistStatus.Warning => "⚠",
        ChecklistStatus.Error => "✗",
        _ => "ℹ"
    };

    public string StatusColorHex => Status switch
    {
        ChecklistStatus.Success => "#34d399", // Emerald green
        ChecklistStatus.Warning => "#fbbf24", // Amber
        ChecklistStatus.Error => "#f87171",   // Coral red
        _ => "#60a5fa"                        // Blue
    };
}

public class AiChatAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "";
    public string ToolName { get; set; } = "";
    public Dictionary<string, string> Arguments { get; set; } = new();
    public bool IsDestructive { get; set; }
    public string? IconSymbol { get; set; }
    public bool IsPrimary { get; set; } = true;
    public bool IsExecuted { get; set; }
}

public class AiPromptContext
{
    public string UserMessage { get; set; } = "";
    public List<AiChatMessage> ConversationHistory { get; set; } = new();
    public BaoToolsContext AppContext { get; set; } = new();
    public string KnowledgeBaseSnippet { get; set; } = "";
    public bool IsVietnamese { get; set; }
}

public interface IAiProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    Task<string> GenerateResponseAsync(AiPromptContext promptContext, CancellationToken ct = default);
}
