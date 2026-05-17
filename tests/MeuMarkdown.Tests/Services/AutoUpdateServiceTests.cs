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

    [Fact]
    public async Task UpdateAsync_HashMismatch_DeletesFileAndReturnsIntegrityError()
    {
        var (bytes, _) = MakePayload();
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var http = new HttpClient(stub);

        // Digest errado de propósito
        var info = MakeInfo("sha256:0000000000000000000000000000000000000000000000000000000000000000", bytes.Length);

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")), launcher: _ => true);
        var result = await service.UpdateAsync(info, () => Task.FromResult(true),
            new Progress<AutoUpdateProgress>(_ => { }), CancellationToken.None);

        Assert.Equal(AutoUpdateStatus.IntegrityError, result.Status);
        Assert.False(File.Exists(Path.Combine(_tempDir, "MeuMarkdown-Setup-v1.3.0.exe")));
    }

    [Fact]
    public async Task UpdateAsync_SizeMismatch_ReturnsIntegrityError()
    {
        var (bytes, digest) = MakePayload(size: 1024);
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var http = new HttpClient(stub);

        // Diz que esperava 9999 bytes mas vai receber 1024
        var info = MakeInfo(digest, 9999);

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")), launcher: _ => true);
        var result = await service.UpdateAsync(info, () => Task.FromResult(true),
            new Progress<AutoUpdateProgress>(_ => { }), CancellationToken.None);

        Assert.Equal(AutoUpdateStatus.IntegrityError, result.Status);
        Assert.False(File.Exists(Path.Combine(_tempDir, "MeuMarkdown-Setup-v1.3.0.exe")));
    }

    [Fact]
    public async Task UpdateAsync_StaleFileInDir_DeletesBeforeNewDownload()
    {
        // Cria um arquivo .exe stale no dir
        var staleFile = Path.Combine(_tempDir, "MeuMarkdown-Setup-v0.0.1.exe");
        File.WriteAllText(staleFile, "stale");

        var (bytes, digest) = MakePayload();
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var http = new HttpClient(stub);
        var info = MakeInfo(digest, bytes.Length);

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")), launcher: _ => true);
        await service.UpdateAsync(info, () => Task.FromResult(true),
            new Progress<AutoUpdateProgress>(_ => { }), CancellationToken.None);

        Assert.False(File.Exists(staleFile), "Stale file deveria ter sido apagado");
        Assert.True(File.Exists(Path.Combine(_tempDir, "MeuMarkdown-Setup-v1.3.0.exe")));
    }

    [Fact]
    public async Task UpdateAsync_ShutdownDenied_ReturnsUserCancelled()
    {
        var (bytes, digest) = MakePayload();
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var http = new HttpClient(stub);
        var info = MakeInfo(digest, bytes.Length);

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")), launcher: _ => true);
        var result = await service.UpdateAsync(info,
            requestShutdown: () => Task.FromResult(false), // user negou
            new Progress<AutoUpdateProgress>(_ => { }), CancellationToken.None);

        Assert.Equal(AutoUpdateStatus.UserCancelled, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_LauncherReturnsFalse_ReturnsLaunchError()
    {
        var (bytes, digest) = MakePayload();
        var stub = new StubHttpHandler().Map("https://example.com/setup.exe",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var http = new HttpClient(stub);
        var info = MakeInfo(digest, bytes.Length);

        var service = new AutoUpdateService(http, _tempDir,
            new UpdateLogger(Path.Combine(_tempDir, "log.txt")),
            launcher: _ => false); // launcher falha

        var result = await service.UpdateAsync(info, () => Task.FromResult(true),
            new Progress<AutoUpdateProgress>(_ => { }), CancellationToken.None);

        Assert.Equal(AutoUpdateStatus.LaunchError, result.Status);
        Assert.NotNull(result.DownloadedSetupPath);
    }
}
