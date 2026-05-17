using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace MeuMarkdown.Services;

public record UpdateInfo(
    string LatestVersion,
    string CurrentVersion,
    string DownloadUrl,
    string ReleaseUrl,
    string ReleaseNotes,
    string AssetDigest,
    long AssetSize);

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NetworkError,
    ParseError
}

public record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Info, string? ErrorMessage);

public class UpdateService
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/adrianocaldeira/meu-markdown/releases/latest";

    private readonly HttpClient? _injectedHttp;

    public UpdateService() { }
    public UpdateService(HttpClient http) { _injectedHttp = http; }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        string currentVersion = GetCurrentVersion();

        HttpClient? owned = null;
        HttpClient http;
        if (_injectedHttp != null)
        {
            http = _injectedHttp;
        }
        else
        {
            // UseProxy=false desabilita a auto-detecção WPAD do Windows.
            // Sem isso, o HttpClient num app WPF instalado pode demorar 30s+ na primeira
            // requisição enquanto procura PAC file na rede — mesmo se o usuário não tem
            // proxy configurado. github.com é acessível direto, então skip WPAD.
            var handler = new HttpClientHandler { UseProxy = false };
            owned = new HttpClient(handler);
            owned.DefaultRequestHeaders.UserAgent.ParseAdd("MeuMarkdown-UpdateChecker");
            owned.Timeout = TimeSpan.FromSeconds(30);
            http = owned;
        }

        try
        {
            var release = await http.GetFromJsonAsync<GitHubRelease>(LatestReleaseApi, ct);
            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                return new UpdateCheckResult(UpdateCheckStatus.ParseError, null,
                    "Resposta inválida do GitHub.");
            }

            var latestTag = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(latestTag, out var latest))
            {
                return new UpdateCheckResult(UpdateCheckStatus.ParseError, null,
                    $"Não consegui interpretar a versão remota '{release.TagName}'.");
            }

            if (!Version.TryParse(currentVersion, out var current))
            {
                current = new Version(0, 0, 0, 0);
            }

            var installer = release.Assets?
                .FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

            var info = new UpdateInfo(
                LatestVersion: latest.ToString(3),
                CurrentVersion: current.ToString(3),
                DownloadUrl: installer?.BrowserDownloadUrl ?? release.HtmlUrl ?? "",
                ReleaseUrl: release.HtmlUrl ?? "",
                ReleaseNotes: release.Body ?? "",
                AssetDigest: installer?.Digest ?? "",
                AssetSize: installer?.Size ?? 0);

            return latest > current
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, info, null)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, info, null);
        }
        catch (HttpRequestException ex)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NetworkError, null, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NetworkError, null, "Tempo esgotado.");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateCheckStatus.ParseError, null, ex.Message);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static string GetCurrentVersion() => VersionInfo.Current;

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("assets")] public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
