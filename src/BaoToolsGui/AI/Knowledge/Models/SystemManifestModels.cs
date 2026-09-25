using System;
using System.Collections.Generic;

namespace BaoToolsGui.AI.Knowledge.Models;

public class SystemManifest
{
    public string Application { get; set; } = "BaoTools";
    public string Version { get; set; } = "105.6";
    public string Description { get; set; } = "Professional Steam game management, optimization, and DRM diagnosis toolkit.";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<FeatureItem> Features { get; set; } = new();
    public List<ScreenItem> Screens { get; set; } = new();
    public List<SettingItem> Settings { get; set; } = new();
    public List<CommandItem> Commands { get; set; } = new();
    public List<ToolItem> Tools { get; set; } = new();
    public List<ServiceItem> Services { get; set; } = new();
    public List<WorkflowItem> Workflows { get; set; } = new();
    public List<LimitationItem> Limitations { get; set; } = new();
}

public class FeatureItem
{
    public string Id { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DescriptionVi { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public List<string> Keywords { get; set; } = new();
    public List<string> RelatedScreens { get; set; } = new();
    public List<string> RelatedSettings { get; set; } = new();
    public List<string> RelatedCommands { get; set; } = new();
    public List<string> RelatedTools { get; set; } = new();
    public List<string> RelatedServices { get; set; } = new();
    public List<string> Workflows { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
    public List<SourceReference> SourceReferences { get; set; } = new();
}

public class ScreenItem
{
    public string Id { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string PurposeVi { get; set; } = "";
    public string PurposeEn { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public string NavigationRoute { get; set; } = "";
    public string SidebarIcon { get; set; } = "";
    public List<string> Sections { get; set; } = new();
    public List<string> PrimaryCommands { get; set; } = new();
    public List<string> RelatedFeatures { get; set; } = new();
}

public class SettingItem
{
    public string Id { get; set; } = "";
    public string Property { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string PurposeVi { get; set; } = "";
    public string PurposeEn { get; set; } = "";
    public string Type { get; set; } = "bool";
    public string DefaultValue { get; set; } = "";
    public string LocationVi { get; set; } = "";
    public string LocationEn { get; set; } = "";
    public List<string> AvailableValues { get; set; } = new();
    public string SourceFile { get; set; } = "SettingsService.cs";
}

public class CommandItem
{
    public string Name { get; set; } = "";
    public string OwnerViewModel { get; set; } = "";
    public string PurposeVi { get; set; } = "";
    public string PurposeEn { get; set; } = "";
    public bool IsMutating { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string RelatedFeature { get; set; } = "";
    public string SourceFile { get; set; } = "";
}

public class ToolItem
{
    public string Name { get; set; } = "";
    public string DescriptionVi { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public bool ReadOnly { get; set; }
    public bool RequiresConfirmation { get; set; }
    public List<string> Parameters { get; set; } = new();
    public List<string> RelatedIntents { get; set; } = new();
    public List<string> RelatedFeatures { get; set; } = new();
    public string SourceFile { get; set; } = "AiToolDefinition.cs";
}

public class ServiceItem
{
    public string Name { get; set; } = "";
    public string ResponsibilityVi { get; set; } = "";
    public string ResponsibilityEn { get; set; } = "";
    public List<string> Capabilities { get; set; } = new();
    public string SourceFile { get; set; } = "";
}

public class WorkflowItem
{
    public string Id { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string SummaryVi { get; set; } = "";
    public string SummaryEn { get; set; } = "";
    public List<string> StepsVi { get; set; } = new();
    public List<string> StepsEn { get; set; } = new();
    public List<string> RequiredTools { get; set; } = new();
    public string TargetScreen { get; set; } = "";
}

public class LimitationItem
{
    public string Id { get; set; } = "";
    public string DescriptionVi { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string Scope { get; set; } = "";
    public string WorkaroundVi { get; set; } = "";
    public string WorkaroundEn { get; set; } = "";
}

public class SourceReference
{
    public string File { get; set; } = "";
    public string? Symbol { get; set; }
}
