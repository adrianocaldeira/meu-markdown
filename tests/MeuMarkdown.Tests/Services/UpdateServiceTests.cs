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
}
