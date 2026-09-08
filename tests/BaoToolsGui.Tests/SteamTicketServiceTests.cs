using System.IO;
using System.IO.Compression;
using BaoToolsGui.Services;
using Xunit;

namespace BaoToolsGui.Tests;

public class SteamTicketServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _settings;
    private readonly string? _originalOverride;
    private readonly SteamService _steam;

    public SteamTicketServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BaoTools_TicketTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settings = new SettingsService();
        _originalOverride = _settings.SteamPathOverride;
        _settings.SteamPathOverride = _tempDir;
        _steam = new SteamService(_settings);
    }

    public void Dispose()
    {
        try
        {
            _settings.SteamPathOverride = _originalOverride;
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void ApplyTicketsToLua_CreatesNewFile_WhenNotExisting()
    {
        var service = new SteamTicketService(_steam, null!, null!, null!, null!);
        uint appId = 1361510;
        string appTicket = "0100000000000000aabbcc";
        string eTicket = "11223344556677889900";

        service.ApplyTicketsToLua(appId, appTicket, eTicket);

        string luaFile = Path.Combine(_steam.StPlugInDir!, $"{appId}.lua");
        Assert.True(File.Exists(luaFile));

        string content = File.ReadAllText(luaFile);
        Assert.Contains($"addappid({appId})", content);
        Assert.Contains($"setAppTicket({appId}, \"{appTicket}\")", content);
        Assert.Contains($"setETicket({appId}, \"{eTicket}\")", content);
    }

    [Fact]
    public void ApplyTicketsToLua_UpdatesExistingValues_WithoutDuplicate()
    {
        var service = new SteamTicketService(_steam, null!, null!, null!, null!);
        uint appId = 1361510;
        Directory.CreateDirectory(_steam.StPlugInDir!);
        string luaFile = Path.Combine(_steam.StPlugInDir!, $"{appId}.lua");

        File.WriteAllText(luaFile, $"addappid({appId})\nsetAppTicket({appId}, \"oldticket\")\n");

        string newAppTicket = "newappticket123456";
        string newETicket = "neweticket654321";

        service.ApplyTicketsToLua(appId, newAppTicket, newETicket);

        string content = File.ReadAllText(luaFile);
        Assert.Contains($"setAppTicket({appId}, \"{newAppTicket}\")", content);
        Assert.Contains($"setETicket({appId}, \"{newETicket}\")", content);
        Assert.DoesNotContain("oldticket", content);
    }

    [Fact]
    public void ExportSharingZip_CreatesValidZipPackage_WithBaoToolsTicketBat()
    {
        var service = new SteamTicketService(_steam, null!, null!, null!, null!);
        uint appId = 1361510;
        string appTicketHex = "aabb1122";
        string eTicketHex = "ccdd3344";

        string zipPath = service.ExportSharingZip(
            appId,
            appTicketHex,
            eTicketHex,
            [0xAA, 0xBB, 0x11, 0x22],
            [0xCC, 0xDD, 0x33, 0x44],
            _tempDir);

        Assert.True(File.Exists(zipPath));
        Assert.Equal($"BaoTools_ticket_{appId}.zip", Path.GetFileName(zipPath));

        using var archive = ZipFile.OpenRead(zipPath);

        // Verify BaoTools_ticket.bat exists
        var batEntry = archive.GetEntry("BaoTools_ticket.bat");
        Assert.NotNull(batEntry);
        using (var reader = new StreamReader(batEntry!.Open()))
        {
            string batText = reader.ReadToEnd();
            Assert.Contains($"{appId}.lua", batText);
            Assert.Contains("stplug-in", batText);
        }

        // Verify <appId>.lua exists
        var luaEntry = archive.GetEntry($"{appId}.lua");
        Assert.NotNull(luaEntry);
        using (var reader = new StreamReader(luaEntry!.Open()))
        {
            string luaText = reader.ReadToEnd();
            Assert.Contains($"addappid({appId})", luaText);
            Assert.Contains($"setAppTicket({appId}, \"{appTicketHex}\")", luaText);
            Assert.Contains($"setETicket({appId}, \"{eTicketHex}\")", luaText);
        }

        // Verify ReadMe.txt exists
        var readmeEntry = archive.GetEntry("ReadMe.txt");
        Assert.NotNull(readmeEntry);

        // Verify tickets/tickets.txt exists
        var ticketTxtEntry = archive.GetEntry("tickets/tickets.txt");
        Assert.NotNull(ticketTxtEntry);
    }
    [Fact]
    public void CheckAppDetailsForDenuvo_ReturnsTrue_WhenDrmNoticeHasDenuvo()
    {
        string detailsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BaoToolsGui", "details");
        Directory.CreateDirectory(detailsDir);
        long testAppId = 999999001;
        string testFile = Path.Combine(detailsDir, $"{testAppId}.json");
        try
        {
            File.WriteAllText(testFile, "{\"name\":\"Test Denuvo Game\",\"drm_notice\":\"Denuvo Anti-tamper 5 activations\"}");
            bool isDenuvo = SteamTicketService.CheckAppDetailsForDenuvo(testAppId);
            Assert.True(isDenuvo);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public void CheckAppDetailsForDenuvo_ReturnsFalse_WhenNoDrmNoticeOrNotDenuvo()
    {
        string detailsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BaoToolsGui", "details");
        Directory.CreateDirectory(detailsDir);
        long testAppId = 999999002;
        string testFile = Path.Combine(detailsDir, $"{testAppId}.json");
        try
        {
            File.WriteAllText(testFile, "{\"name\":\"Test Clean Game\",\"description\":\"This game has no DRM\"}");
            bool isDenuvo = SteamTicketService.CheckAppDetailsForDenuvo(testAppId);
            Assert.False(isDenuvo);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }
}