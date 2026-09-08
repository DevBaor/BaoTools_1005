using BaoToolsGui.Services;
using Xunit;

namespace BaoToolsGui.Tests;

public class QuotaSummaryTests
{
    [Fact]
    public void QuotaSummary_StandardUser_FormatsCorrectly()
    {
        var quota = new QuotaSummary(Used: 5, Limit: AppConfig.AppDailyDownloadLimit, IsSupporter: false);
        Assert.Equal("5/10", quota.DisplayText);
        Assert.False(quota.IsSupporter);
        Assert.Equal(5, quota.Used);
        Assert.Equal(10, quota.Limit);
    }

    [Fact]
    public void QuotaSummary_SupporterUser_FormatsUnlimited()
    {
        var quota = new QuotaSummary(Used: 12, Limit: AppConfig.AppDailyDownloadLimit, IsSupporter: true);
        Assert.Equal("12 / Unlimited", quota.DisplayText);
        Assert.True(quota.IsSupporter);
    }

    [Fact]
    public void QuotaSummary_ZeroUsed_FormatsCorrectly()
    {
        var quota = new QuotaSummary(Used: 0, Limit: AppConfig.AppDailyDownloadLimit, IsSupporter: false);
        Assert.Equal("0/10", quota.DisplayText);
    }
}
