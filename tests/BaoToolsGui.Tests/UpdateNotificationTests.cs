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
    [InlineData("v105.3", "v105.2", true)]
    [InlineData("v105.2", "v105.3", false)]
    [InlineData("v105.3", "v105.3", false)]
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

    [Fact]
    public void ParseGitHubReleasesList_ParsesMultipleReleasesCorrectly()
    {
        string json = """
        [
            {
                "tag_name": "v105.3",
                "name": "BaoTools v105.3 - Revert Update",
                "body": "Revert fixes cleanly and My Games filter",
                "html_url": "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.3",
                "published_at": "2026-09-10T10:00:00Z"
            },
            {
                "tag_name": "v105.2",
                "name": "BaoTools v105.2 - Tickets Extractor",
                "body": "Extract tickets from steam client",
                "html_url": "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.2",
                "published_at": "2026-09-07T10:00:00Z"
            }
        ]
        """;

        var list = UpdateService.ParseGitHubReleasesList(json, "v105.2");
        Assert.Equal(2, list.Count);
        Assert.Equal("v105.3", list[0].TagName);
        Assert.True(list[0].IsNewer);
        Assert.Equal("v105.2", list[1].TagName);
        Assert.False(list[1].IsNewer);
    }

    [Fact]
    public void UpdateHistoryService_LoadsDefaultsAndSetsFlags()
    {
        var service = new UpdateHistoryService();
        var history = service.LoadHistory("v105.3");

        Assert.NotEmpty(history);
        var v105_3 = history.FirstOrDefault(x => x.TagName == "v105.3");
        Assert.NotNull(v105_3);
        Assert.True(v105_3.IsCurrent);
        Assert.False(v105_3.IsNew);

        var v105_2 = history.FirstOrDefault(x => x.TagName == "v105.2");
        Assert.NotNull(v105_2);
        Assert.False(v105_2.IsCurrent);
        Assert.False(v105_2.IsNew);
    }

    [Fact]
    public void UpdateHistoryService_DetectsNewerWhenOnOlderVersion()
    {
        var service = new UpdateHistoryService();
        var history = service.LoadHistory("v105.2");

        var v105_3 = history.FirstOrDefault(x => x.TagName == "v105.3");
        Assert.NotNull(v105_3);
        Assert.False(v105_3.IsCurrent);
        Assert.True(v105_3.IsNew);
    }

    [Fact]
    public void UpdateHistoryService_SyncsLanguageWithCurrentCulture()
    {
        var prevCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            // Test Vietnamese culture
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            var viHistory = UpdateHistoryService.GetDefaultHistory();
            Assert.NotEmpty(viHistory);
            Assert.Contains("Gỡ Fix", viHistory[0].Title);
            Assert.Contains("Hỗ trợ gỡ Fix sạch sẽ", viHistory[0].Body);

            // Test English / international culture
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            var enHistory = UpdateHistoryService.GetDefaultHistory();
            Assert.NotEmpty(enHistory);
            Assert.Contains("Reverting Fixes Cleanly", enHistory[0].Title);
            Assert.Contains("Clean fix reversion", enHistory[0].Body);

            // Test LoadHistory updates cached default items when language changes
            var service = new UpdateHistoryService();
            var loadedEn = service.LoadHistory("v105.3");
            var itemEn = loadedEn.FirstOrDefault(x => x.TagName == "v105.3");
            Assert.NotNull(itemEn);
            Assert.Contains("Reverting Fixes Cleanly", itemEn.Title);

            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            var loadedVi = service.LoadHistory("v105.3");
            var itemVi = loadedVi.FirstOrDefault(x => x.TagName == "v105.3");
            Assert.NotNull(itemVi);
            Assert.Contains("Gỡ Fix", itemVi.Title);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void NotificationStrings_ExistInResources()
    {
        var prevCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            Assert.Equal("Thông báo", BaoToolsGui.Resources.Strings.Notification_Title);
            Assert.Equal("Cập nhật ngay", BaoToolsGui.Resources.Strings.Notification_UpdateNow);
            Assert.Equal("Đang tải...", BaoToolsGui.Resources.Strings.Notification_Downloading);

            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            Assert.Equal("Notifications", BaoToolsGui.Resources.Strings.Notification_Title);
            Assert.Equal("Update Now", BaoToolsGui.Resources.Strings.Notification_UpdateNow);
            Assert.Equal("Downloading...", BaoToolsGui.Resources.Strings.Notification_Downloading);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void MergeWithRemoteReleases_DoesNotOverwriteDefaultLocalizedContent()
    {
        var prevCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("vi-VN");
            var service = new UpdateHistoryService();

            var remoteReleases = new List<GitHubReleaseInfo>
            {
                new()
                {
                    TagName = "v105.3",
                    Title = "Revert Fixes Cleanly English Title From GitHub",
                    Body = "English markdown body from GitHub that should not overwrite Vietnamese",
                    HtmlUrl = "https://github.com/DevBaor/BaoTools_1005/releases/tag/v105.3",
                    PublishedAt = DateTimeOffset.UtcNow
                }
            };

            var merged = service.MergeWithRemoteReleases(remoteReleases, "v105.3");
            var item = merged.FirstOrDefault(x => x.TagName == "v105.3");
            Assert.NotNull(item);
            Assert.Contains("Gỡ Fix", item.Title);
            Assert.Contains("Hỗ trợ gỡ Fix", item.Body);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = prevCulture;
        }
    }

    [Fact]
    public void BuildSetupBatchScript_IncludesPidWaitAndRelaunch()
    {
        string script = UpdateService.BuildSetupBatchScript(12345, @"C:\Temp\Setup.exe", @"C:\Program Files\BaoTools", @"C:\Program Files\BaoTools\BaoTools.exe");

        Assert.Contains("PID eq 12345", script);
        Assert.Contains(@"C:\Temp\Setup.exe", script);
        Assert.Contains("/SILENT", script);
        Assert.Contains(@"cd /d ""C:\Program Files\BaoTools""", script);
        Assert.Contains(@"start """" ""C:\Program Files\BaoTools\BaoTools.exe""", script);
        Assert.Contains(@"del ""C:\Temp\Setup.exe""", script);
    }

    [Fact]
    public void BuildSingleExeBatchScript_IncludesPidWaitAndRelaunch()
    {
        string script = UpdateService.BuildSingleExeBatchScript(12345, @"C:\Temp\BaoTools_v105.3.exe", @"C:\Games\BaoTools", @"C:\Games\BaoTools\BaoTools.exe");

        Assert.Contains("PID eq 12345", script);
        Assert.Contains(@"C:\Temp\BaoTools_v105.3.exe", script);
        Assert.Contains(@"C:\Games\BaoTools\BaoTools.exe", script);
        Assert.Contains(@"cd /d ""C:\Games\BaoTools""", script);
        Assert.Contains(@"start """" ""C:\Games\BaoTools\BaoTools.exe""", script);
    }

    [Fact]
    public void BuildPortableBatchScript_IncludesPidWaitAndRelaunch()
    {
        string script = UpdateService.BuildPortableBatchScript(12345, @"C:\Temp\Staging", @"C:\Temp\Zip.zip", @"C:\Games\BaoTools", @"C:\Games\BaoTools\BaoTools.exe");

        Assert.Contains("PID eq 12345", script);
        Assert.Contains(@"C:\Temp\Staging", script);
        Assert.Contains(@"cd /d ""C:\Games\BaoTools""", script);
        Assert.Contains(@"start """" ""C:\Games\BaoTools\BaoTools.exe""", script);
        Assert.Contains(@"rd /s /q ""C:\Temp\Staging""", script);
    }
}

