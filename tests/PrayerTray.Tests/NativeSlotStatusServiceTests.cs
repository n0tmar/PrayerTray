using PrayerTray.Services;

namespace PrayerTray.Tests;

public sealed class NativeSlotStatusServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PrayerTrayTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void IsVisibleAndFresh_ReturnsTrue_ForFreshVisibleStatus()
    {
        var path = CreateStatusFile("INJECTED=1\nVISIBLE=1\n");
        var service = new NativeSlotStatusService(path);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

        Assert.True(service.IsVisibleAndFresh(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void IsVisibleAndFresh_ReturnsFalse_ForStaleVisibleStatus()
    {
        var path = CreateStatusFile("INJECTED=1\nVISIBLE=1\n");
        var service = new NativeSlotStatusService(path);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-30));

        Assert.False(service.IsVisibleAndFresh(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void IsVisibleAndFresh_ReturnsFalse_WhenNativeSlotIsHidden()
    {
        var path = CreateStatusFile("INJECTED=1\nVISIBLE=0\n");
        var service = new NativeSlotStatusService(path);

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);

        Assert.False(service.IsVisibleAndFresh(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void Clear_RemovesStatusFile()
    {
        var path = CreateStatusFile("INJECTED=1\nVISIBLE=1\n");
        var service = new NativeSlotStatusService(path);

        service.Clear();

        Assert.False(File.Exists(path));
    }

    private string CreateStatusFile(string content)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, NativeSlotStatusService.StatusFileName);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
