using System.Diagnostics;
using System.Threading;
using System.Windows;
using MeuMarkdown.Services;

namespace MeuMarkdown;

public partial class UpdateWindow : Window
{
    private string? _downloadUrl;
    private string? _releaseUrl;
    private UpdateInfo? _info;
    private InstallContext? _installContext;
    private CancellationTokenSource? _installCts;
    private bool _installing = false;

    public UpdateWindow()
    {
        InitializeComponent();
        _installContext = InstallContext.Detect();
        Loaded += async (_, _) => await CheckAsync();
    }

    private async Task CheckAsync()
    {
        ShowOnly(LoadingPanel);
        DownloadBtn.Visibility = Visibility.Collapsed;
        ReleaseNotesBtn.Visibility = Visibility.Collapsed;
        CancelInstallBtn.Visibility = Visibility.Collapsed;
        UacWarningPanel.Visibility = Visibility.Collapsed;

        var service = new UpdateService();
        var result = await service.CheckForUpdatesAsync();

        switch (result.Status)
        {
            case UpdateCheckStatus.UpToDate:
                ShowOnly(UpToDatePanel);
                UpToDateVersionText.Text = $"v{result.Info?.CurrentVersion ?? "?"}";
                break;

            case UpdateCheckStatus.UpdateAvailable when result.Info != null:
                _info = result.Info;
                _downloadUrl = result.Info.DownloadUrl;
                _releaseUrl = result.Info.ReleaseUrl;

                ShowOnly(UpdateAvailablePanel);
                UpdateVersionText.Text = $"v{result.Info.CurrentVersion}  →  v{result.Info.LatestVersion}";
                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(result.Info.ReleaseNotes)
                    ? "(sem release notes)"
                    : result.Info.ReleaseNotes;

                var ctx = _installContext ?? InstallContext.Detect();
                if (ctx.SupportsAutoUpdate)
                {
                    DownloadBtn.Content = "Atualizar agora";
                    DownloadBtn.Visibility = string.IsNullOrEmpty(_downloadUrl) ? Visibility.Collapsed : Visibility.Visible;
                    UacWarningPanel.Visibility = ctx.RequiresElevation ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    // Portable: mantém comportamento antigo (abre browser)
                    DownloadBtn.Content = "Baixar instalador";
                    DownloadBtn.Visibility = string.IsNullOrEmpty(_downloadUrl) ? Visibility.Collapsed : Visibility.Visible;
                }
                ReleaseNotesBtn.Visibility = string.IsNullOrEmpty(_releaseUrl) ? Visibility.Collapsed : Visibility.Visible;
                break;

            default:
                ShowOnly(ErrorPanel);
                ErrorMessageText.Text = result.ErrorMessage ?? "Erro desconhecido.";
                break;
        }
    }

    private void ShowOnly(FrameworkElement panel)
    {
        LoadingPanel.Visibility = panel == LoadingPanel ? Visibility.Visible : Visibility.Collapsed;
        UpToDatePanel.Visibility = panel == UpToDatePanel ? Visibility.Visible : Visibility.Collapsed;
        UpdateAvailablePanel.Visibility = panel == UpdateAvailablePanel ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = panel == ErrorPanel ? Visibility.Visible : Visibility.Collapsed;
        InstallingPanel.Visibility = panel == InstallingPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_info == null || string.IsNullOrEmpty(_downloadUrl)) return;
        if (_installing) return;

        var ctx = _installContext ?? InstallContext.Detect();
        if (!ctx.SupportsAutoUpdate)
        {
            // Portable: fallback antigo
            OpenUrl(_downloadUrl);
            return;
        }

        await StartAutoUpdate(_info);
    }

    private async Task StartAutoUpdate(UpdateInfo info)
    {
        _installing = true;
        _installCts = new CancellationTokenSource();

        ShowOnly(InstallingPanel);
        DownloadBtn.Visibility = Visibility.Collapsed;
        ReleaseNotesBtn.Visibility = Visibility.Collapsed;
        CancelInstallBtn.Visibility = Visibility.Visible;
        InstallingProgressBar.Value = 0;
        InstallingStatusText.Text = "Baixando atualização...";
        InstallingDetailText.Text = "";

        var progress = new Progress<AutoUpdateProgress>(p =>
        {
            InstallingStatusText.Text = p.Message;
            InstallingProgressBar.IsIndeterminate = p.Status is AutoUpdateStatus.Validating
                or AutoUpdateStatus.PreparingShutdown or AutoUpdateStatus.Launching;
            if (!InstallingProgressBar.IsIndeterminate)
                InstallingProgressBar.Value = p.PercentComplete;
        });

        var service = new AutoUpdateService();
        var result = await service.UpdateAsync(
            info,
            requestShutdown: RequestShutdownAsync,
            progress,
            _installCts.Token);

        _installing = false;

        if (result.Status == AutoUpdateStatus.Completed)
        {
            // Setup foi disparado. Sinaliza pra MainWindow fechar via Close() (e não
            // Application.Shutdown(), que NÃO dispara Window.Closing e por isso pula
            // a persistência do state.json — perdendo abas, workspace, layout etc).
            DialogResult = true;
            Close();
            return;
        }

        // Erro ou cancelamento — voltar ao estado "Update Available" com mensagem
        ShowOnly(UpdateAvailablePanel);
        CancelInstallBtn.Visibility = Visibility.Collapsed;
        DownloadBtn.Visibility = Visibility.Visible;
        ReleaseNotesBtn.Visibility = Visibility.Visible;

        if (result.Status != AutoUpdateStatus.UserCancelled)
        {
            MessageBox.Show(
                $"{result.ErrorMessage}\n\nVocê pode abrir a página do release no GitHub e baixar manualmente.",
                "Falha na atualização", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Task<bool> RequestShutdownAsync()
    {
        // Chamado no thread de pool — voltar pro UI thread
        return Dispatcher.InvokeAsync(() =>
        {
            if (Application.Current.MainWindow is MainWindow main)
                return main.TryShutdownForUpdate();
            return true;
        }).Task;
    }

    private void OnCancelInstall(object sender, RoutedEventArgs e)
    {
        _installCts?.Cancel();
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
        catch { }
    }

    private async void OnRetry(object sender, RoutedEventArgs e)
    {
        await CheckAsync();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (_installing) _installCts?.Cancel();
        Close();
    }
}
