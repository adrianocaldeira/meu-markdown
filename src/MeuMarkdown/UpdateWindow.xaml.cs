using System.Diagnostics;
using System.Windows;
using MeuMarkdown.Services;

namespace MeuMarkdown;

public partial class UpdateWindow : Window
{
    private string? _downloadUrl;
    private string? _releaseUrl;

    public UpdateWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await CheckAsync();
    }

    private async Task CheckAsync()
    {
        var service = new UpdateService();
        var result = await service.CheckForUpdatesAsync();

        LoadingPanel.Visibility = Visibility.Collapsed;

        switch (result.Status)
        {
            case UpdateCheckStatus.UpToDate:
                UpToDatePanel.Visibility = Visibility.Visible;
                UpToDateVersionText.Text = $"v{result.Info?.CurrentVersion ?? "?"}";
                break;

            case UpdateCheckStatus.UpdateAvailable when result.Info != null:
                UpdateAvailablePanel.Visibility = Visibility.Visible;
                UpdateVersionText.Text =
                    $"v{result.Info.CurrentVersion}  →  v{result.Info.LatestVersion}";
                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(result.Info.ReleaseNotes)
                    ? "(sem release notes)"
                    : result.Info.ReleaseNotes;
                _downloadUrl = result.Info.DownloadUrl;
                _releaseUrl = result.Info.ReleaseUrl;
                DownloadBtn.Visibility = string.IsNullOrEmpty(_downloadUrl)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ReleaseNotesBtn.Visibility = string.IsNullOrEmpty(_releaseUrl)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                break;

            default:
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorMessageText.Text = result.ErrorMessage ?? "Erro desconhecido.";
                break;
        }
    }

    private void OnDownload(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadUrl)) return;
        OpenUrl(_downloadUrl);
    }

    private void OnOpenReleasePage(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_releaseUrl)) return;
        OpenUrl(_releaseUrl);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignora — usuário pode copiar o link da modal se quiser
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
