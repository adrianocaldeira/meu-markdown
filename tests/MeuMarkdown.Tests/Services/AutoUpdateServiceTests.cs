using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MeuMarkdown.Services;
using MeuMarkdown.Tests.TestHelpers;

namespace MeuMarkdown.Tests.Services;

public class AutoUpdateServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AutoUpdateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeuMarkdownAutoUpdateTests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static (byte[] bytes, string digest) MakePayload(int size = 1024)
    {
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return (bytes, $"sha256:{hex}");
    }

    private static UpdateInfo MakeInfo(string digest, long size, string url = "https://example.com/setup.exe")
        => new(
            LatestVersion: "1.3.0",
            CurrentVersion: "1.2.1",
            DownloadUrl: url,
            ReleaseUrl: "https://example.com/release",
            ReleaseNotes: "notes",
            AssetDigest: digest,
            AssetSize: size);

    [Fact]
    public async Task UpdateAsync_HappyPath_DownloadsAndValidatesAndPreparesShutdown()
    {
        var (bytes, digest) = MakePayload();
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        var http = new HttpClient(stub);

        var info = MakeInfo(digest, bytes.Length);
        var shutdownCalled = false;
        var progressEvents = new List<AutoUpdateProgress>();
        var progress = new Progress<AutoUpdateProgress>(p => progressEvents.Add(p));

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")),
            launcher: _ => true);
        var result = await service.UpdateAsync(
            info,
            requestShutdown: () => { shutdownCalled = true; return Task.FromResult(true); },
            progress,
            CancellationToken.None);

        Assert.Equal(AutoUpdateStatus.Completed, result.Status);
        Assert.True(shutdownCalled);
        Assert.NotNull(result.DownloadedSetupPath);
        Assert.True(File.Exists(result.DownloadedSetupPath));
        Assert.Contains(progressEvents, p => p.Status == AutoUpdateStatus.Downloading);
        Assert.Contains(progressEvents, p => p.Status == AutoUpdateStatus.Validating);
    }
}
