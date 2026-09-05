namespace BaoToolsGui.Models;

/// <summary>
/// Information about a GitHub release fetched for app update notifications.
/// </summary>
public class GitHubReleaseInfo
{
    public string TagName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public string DownloadUrl { get; set; } = "https://baotools.baotranduy666666.workers.dev/";
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsNewer { get; set; }
}
