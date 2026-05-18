using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace MeuMarkdown.Services;

public enum AutoUpdateStatus
{
    Downloading,
    Validating,
    PreparingShutdown,
    Launching,
    Completed,
    NetworkError,
    IntegrityError,
    LaunchError,
    UserCancelled
}

public sealed record AutoUpdateProgress(
    AutoUpdateStatus Status,
    double PercentComplete,
    string Message);

public sealed record AutoUpdateResult(
    AutoUpdateStatus Status,
    string? DownloadedSetupPath,
    string? ErrorMessage);

public class AutoUpdateService
{
    private readonly HttpClient? _injectedHttp;
    private readonly string _downloadDir;
    private readonly UpdateLogger _logger;
    private readonly Func<string, bool>? _launcher;

    public AutoUpdateService()
        : this(http: null, downloadDir: null, logger: null, launcher: null) { }

    public AutoUpdateService(HttpClient? http, string? downloadDir, UpdateLogger? logger, Func<string, bool>? launcher = null)
    {
        _injectedHttp = http;
        _downloadDir = downloadDir ?? DefaultDownloadDir();
        _logger = logger ?? new UpdateLogger();
        _launcher = launcher;
    }

    private static string DefaultDownloadDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeuMarkdown", "Updates");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<AutoUpdateResult> UpdateAsync(
        UpdateInfo info,
        Func<Task<bool>> requestShutdown,
        IProgress<AutoUpdateProgress> progress,
        CancellationToken ct)
    {
        _logger.Log($"START update {info.CurrentVersion} -> {info.LatestVersion}");

        // 1) Limpar stale
        try
        {
            foreach (var f in Directory.EnumerateFiles(_downloadDir, "*.exe"))
                File.Delete(f);
        }
        catch (Exception ex) { _logger.Log($"WARN cleanup: {ex.Message}"); }

        // 2) Download
        var fileName = $"MeuMarkdown-Setup-v{info.LatestVersion}.exe";
        var path = Path.Combine(_downloadDir, fileName);

        HttpClient? owned = null;
        HttpClient http;
        if (_injectedHttp != null)
        {
            http = _injectedHttp;
        }
        else
        {
            owned = new HttpClient(new HttpClientHandler { UseProxy = false });
            owned.Timeout = TimeSpan.FromMinutes(5);
            http = owned;
        }
        try
        {
            progress.Report(new AutoUpdateProgress(AutoUpdateStatus.Downloading, 0, "Baixando atualização..."));
            using (var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? info.AssetSize;
                using var src = await response.Content.ReadAsStreamAsync(ct);
                using var dst = File.Create(path);
                var buf = new byte[81920];
                long copied = 0;
                int read;
                while ((read = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read), ct);
                    copied += read;
                    if (total > 0)
                    {
                        var pct = copied * 100.0 / total;
                        progress.Report(new AutoUpdateProgress(AutoUpdateStatus.Downloading, pct, $"Baixando... {pct:F0}%"));
                    }
                }
            }

            // 3) Validar tamanho
            var fileSize = new FileInfo(path).Length;
            if (info.AssetSize > 0 && fileSize != info.AssetSize)
            {
                TryDelete(path);
                _logger.Log($"FAIL size mismatch: expected={info.AssetSize}, got={fileSize}");
                return new AutoUpdateResult(AutoUpdateStatus.IntegrityError, null,
                    "Arquivo baixado tem tamanho inesperado.");
            }

            // 4) Validar SHA-256
            progress.Report(new AutoUpdateProgress(AutoUpdateStatus.Validating, 100, "Verificando integridade..."));
            var digest = await ComputeSha256Async(path, ct);
            var expected = info.AssetDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? info.AssetDigest[7..]
                : info.AssetDigest;
            if (string.IsNullOrEmpty(expected))
            {
                _logger.Log("WARN no digest available, integrity not verified");
            }
            else if (!string.Equals(digest, expected, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(path);
                _logger.Log($"FAIL hash mismatch: expected={expected}, got={digest}");
                return new AutoUpdateResult(AutoUpdateStatus.IntegrityError, null,
                    "Arquivo baixado falhou na verificação de integridade.");
            }

            // 5) Shutdown
            progress.Report(new AutoUpdateProgress(AutoUpdateStatus.PreparingShutdown, 100, "Preparando para instalar..."));
            var canShutdown = await requestShutdown();
            if (!canShutdown)
            {
                _logger.Log("ABORT user cancelled shutdown");
                return new AutoUpdateResult(AutoUpdateStatus.UserCancelled, path, "Atualização cancelada pelo usuário.");
            }

            // 6) Launch
            progress.Report(new AutoUpdateProgress(AutoUpdateStatus.Launching, 100, "Iniciando instalador..."));
            var launched = (_launcher ?? DefaultLauncher)(path);
            if (!launched)
            {
                _logger.Log("FAIL launch returned false");
                return new AutoUpdateResult(AutoUpdateStatus.LaunchError, path,
                    "Não foi possível iniciar o instalador.");
            }

            _logger.Log("OK launched setup");
            return new AutoUpdateResult(AutoUpdateStatus.Completed, path, null);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            _logger.Log("ABORT cancelled mid-download");
            return new AutoUpdateResult(AutoUpdateStatus.UserCancelled, null, "Cancelado.");
        }
        catch (HttpRequestException ex)
        {
            TryDelete(path);
            _logger.Log($"FAIL network: {ex.Message}");
            return new AutoUpdateResult(AutoUpdateStatus.NetworkError, null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Log($"FAIL unexpected: {ex.Message}");
            return new AutoUpdateResult(AutoUpdateStatus.LaunchError, path, ex.Message);
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static bool DefaultLauncher(string setupPath)
    {
        try
        {
            // /SILENT: install sem UI mas com barra de progresso minimalista (mantém feedback se demorar).
            // /NORESTART: nunca reinicia o Windows mesmo que algum DLL peça.
            // O relaunch do app após o install é feito pelo [Run] Check:WizardSilent no MeuMarkdown.iss
            // — NÃO via /RESTARTAPPLICATIONS (que depende de WTSRegisterApplicationRestart, não usado).
            var psi = new ProcessStartInfo(setupPath, "/SILENT /NORESTART")
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(setupPath) ?? "",
            };
            var p = Process.Start(psi);
            return p != null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
