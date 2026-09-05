using BaoToolsGui.Models;
using BaoToolsGui.Services;
using BaoToolsGui.ViewModels;
using Xunit;

namespace BaoToolsGui.Tests;

public class UpdateNotificationTests
{
    [Theory]
    [InlineData("v1006", "v1005", true)]
    [InlineData("v1005", "v1005", false)]
    [InlineData("v1004", "v1005", false)]
    [InlineData("1006", "1005", true)]
    [InlineData("1005", "1005", false)]
    [InlineData("v2000", "v1005", true)]
    [InlineData("v105.1", "v1005", true)]
    [InlineData("v105.1", "v105.1", false)]
    [InlineData("v105.2", "v105.1", true)]
    [InlineData("v105.1", "v105.2", false)]
    [InlineData("105.1", "105.1", false)]
    public void IsVersionNewer_DetectsNewerVersionsCorrectly(string latest, string current, bool expected)
    {
        bool result = UpdateService.IsVersionNewer(latest, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseGitHubRelease_ParsesValidJsonWithNewerVersion()
    {
        string json = """
        {
            "tag_name": "v1006",
            "name": "BaoTools 1006 - Big Update",
            "body": "- Added notification bell\n- Bug fixes and improvements",
            "html_url": "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1006",
            "published_at": "2026-09-05T12:00:00Z"
        }
        """;

        var info = UpdateService.ParseGitHubRelease(json, "v1005");
        Assert.NotNull(info);
        Assert.Equal("v1006", info.TagName);
        Assert.Equal("BaoTools 1006 - Big Update", info.Title);
        Assert.Contains("Added notification bell", info.Body);
        Assert.Equal("https://github.com/DevBaor/BaoTools_1005/releases/tag/v1006", info.HtmlUrl);
        Assert.NotNull(info.PublishedAt);
        Assert.True(info.IsNewer);
    }

    [Fact]
    public void ParseGitHubRelease_CurrentVersionIsNotNewer()
    {
        string json = """
        {
            "tag_name": "v1005",
            "name": "BaoTools 1005",
            "body": "Initial release",
            "html_url": "https://github.com/DevBaor/BaoTools_1005/releases/tag/v1005"
        }
        """;

        var info = UpdateService.ParseGitHubRelease(json, "v1005");
        Assert.NotNull(info);
        Assert.Equal("v1005", info.TagName);
        Assert.False(info.IsNewer);
    }

    [Fact]
    public void ParseGitHubRelease_InvalidJsonReturnsNull()
    {
        var info = UpdateService.ParseGitHubRelease("invalid json content", "v1005");
        Assert.Null(info);
    }

    [Fact]
    public void NotificationState_CalculatedPropertiesBehaveCorrectly()
    {
        var vm = (MainViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));

        // Initially: up to date
        Assert.True(vm.IsUpToDate);
        Assert.False(vm.HasUpdateContent);
        Assert.False(vm.HasUpdateError);

        // Simulate update available
        vm.HasUpdate = true;
        vm.HasUnreadNotification = true;
        Assert.True(vm.HasUpdateContent);
        Assert.False(vm.IsUpToDate);

        // Toggle open clears unread badge
        vm.ToggleNotificationCommand.Execute(null);
        Assert.True(vm.IsNotificationOpen);
        Assert.False(vm.HasUnreadNotification);

        // Close notification
        vm.CloseNotificationCommand.Execute(null);
        Assert.False(vm.IsNotificationOpen);

        // Checking update state hides content and up to date
        vm.IsCheckingUpdate = true;
        Assert.False(vm.HasUpdateContent);
        Assert.False(vm.IsUpToDate);
        Assert.False(vm.HasUpdateError);

        vm.IsCheckingUpdate = false;
        vm.HasUpdate = false;
        vm.UpdateError = "Network error";
        Assert.True(vm.HasUpdateError);
        Assert.False(vm.IsUpToDate);
    }

    [Theory]
    [InlineData("\"CellIDServerOverride\"\t\t\"71\"", "71")]
    [InlineData("\"CellID\"\t\t\"167\"", "167")]
    [InlineData("\"CellID\"\t\t\"0\"", null)]
    public void CellIdRegex_ExtractsValidIds(string snippet, string? expected)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            snippet, @"""(?:CellIDServerOverride|CellID)""\s+""(\d+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        string? result = (match.Success && match.Groups[1].Value is { Length: > 0 } id && id != "0") ? id : null;
        Assert.Equal(expected, result);
    }
}
