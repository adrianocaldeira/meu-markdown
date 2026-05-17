using System.Diagnostics;
using MeuMarkdown.Services;

namespace MeuMarkdown.Tests.Services;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_RealGitHub_RespondsQuickly()
    {
        // Teste de integração: bate na API real do GitHub.
        // Útil pra detectar problemas de proxy/timeout que só aparecem em runtime.
        // Pode falhar offline — nesse caso é esperado, sinaliza problema de rede.
        var sw = Stopwatch.StartNew();
        var service = new UpdateService();
        var result = await service.CheckForUpdatesAsync();
        sw.Stop();

        Assert.True(
            result.Status is UpdateCheckStatus.UpToDate or UpdateCheckStatus.UpdateAvailable,
            $"Esperava UpToDate ou UpdateAvailable, recebi {result.Status}: {result.ErrorMessage}");

        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Resposta muito lenta ({sw.ElapsedMilliseconds}ms) — investigar WPAD/proxy.");

        Assert.False(string.IsNullOrEmpty(result.Info?.LatestVersion),
            "LatestVersion deveria estar preenchido em resultado de sucesso.");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ParsesDigestAndSize()
    {
        var json = """
        {
            "tag_name": "v999.0.0",
            "html_url": "https://github.com/test/test/releases/tag/v999.0.0",
            "body": "Release notes here",
            "assets": [
                {
                    "name": "MeuMarkdown-Setup-v999.0.0.exe",
                    "browser_download_url": "https://example.com/setup.exe",
                    "size": 51234567,
                    "digest": "sha256:abc123def456"
                }
            ]
        }
        """;
        var stub = new MeuMarkdown.Tests.TestHelpers.StubHttpHandler()
            .Map("https://api.github.com/repos/", new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        var http = new System.Net.Http.HttpClient(stub);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Test");

        var service = new UpdateService(http);
        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.NotNull(result.Info);
        Assert.Equal("sha256:abc123def456", result.Info!.AssetDigest);
        Assert.Equal(51234567L, result.Info.AssetSize);
        Assert.Equal("https://example.com/setup.exe", result.Info.DownloadUrl);
    }
}
