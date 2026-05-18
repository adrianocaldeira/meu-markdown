using System.IO;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MeuMarkdown.Controls;

public partial class MarkdownPreview : UserControl
{
    private bool _isInitialized;
    private string? _pendingHtml;
    private bool _isDarkTheme;

    public event Action<string>? LinkClicked;
    public event Action<string>? ExternalLinkClicked;
    public event Action<int>? PreviewScrolled;

    public MarkdownPreview()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeuMarkdown", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _isInitialized = true;
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            loadingText.Visibility = Visibility.Collapsed;

            if (_pendingHtml != null)
            {
                webView.NavigateToString(_pendingHtml);
                _pendingHtml = null;
            }

            // `SetDarkTheme` chamado antes do WebView2 inicializar é descartado
            // (só armazena `_isDarkTheme`). Aplicar o tema agora — depois que a
            // navegação inicial completar, pra garantir que o JS da página esteja pronto.
            if (_isDarkTheme)
            {
                void OnNavCompletedOnce(object? s, CoreWebView2NavigationCompletedEventArgs ev)
                {
                    webView.CoreWebView2.NavigationCompleted -= OnNavCompletedOnce;
                    _ = webView.CoreWebView2.ExecuteScriptAsync("setTheme(true)");
                }
                webView.CoreWebView2.NavigationCompleted += OnNavCompletedOnce;
            }
        }
        catch (Exception ex)
        {
            loadingText.Text = $"Erro ao inicializar preview: {ex.Message}";
        }
    }

    public void SetFullHtml(string html)
    {
        if (_isInitialized)
            webView.NavigateToString(html);
        else
            _pendingHtml = html;
    }

    public async void UpdateContentFragment(string htmlFragment)
    {
        if (!_isInitialized) return;
        var escaped = htmlFragment
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
        await webView.CoreWebView2.ExecuteScriptAsync($"updateContent('{escaped}')");
    }

    public async void SetDarkTheme(bool dark)
    {
        _isDarkTheme = dark;
        if (!_isInitialized) return;
        await webView.CoreWebView2.ExecuteScriptAsync($"setTheme({(dark ? "true" : "false")})");
    }

    public void SetVirtualHostMapping(string directory)
    {
        if (!_isInitialized || string.IsNullOrEmpty(directory)) return;
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "local.assets", directory,
            CoreWebView2HostResourceAccessKind.Allow);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;
            var msg = System.Text.Json.JsonSerializer.Deserialize<ScrollMessage>(json);
            if (msg?.Type == "scroll")
                PreviewScrolled?.Invoke(msg.Line);
        }
        catch
        {
            // ignora mensagens malformadas
        }
    }

    public async void ScrollToLine(int line)
    {
        if (!_isInitialized) return;
        await webView.CoreWebView2.ExecuteScriptAsync($"syncScrollToLine({line})");
    }

    public async Task<bool> PrintToPdfAsync(string sourceHtmlPath, string destPdfPath, string pageSize = "A4", string orientation = "Portrait")
    {
        if (!_isInitialized) return false;

        var originalHtml = _pendingHtml;

        try
        {
            var tcs = new TaskCompletionSource<bool>();
            void OnNavCompleted(object? s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
            {
                webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                tcs.TrySetResult(e.IsSuccess);
            }
            webView.CoreWebView2.NavigationCompleted += OnNavCompleted;
            webView.CoreWebView2.Navigate("file:///" + sourceHtmlPath.Replace("\\", "/"));
            await tcs.Task;

            var printSettings = webView.CoreWebView2.Environment.CreatePrintSettings();
            printSettings.Orientation = orientation == "Landscape"
                ? Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Landscape
                : Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Portrait;

            (printSettings.PageWidth, printSettings.PageHeight) = pageSize switch
            {
                "Letter" => (8.5, 11.0),
                "Legal" => (8.5, 14.0),
                _ => (8.27, 11.69)
            };
            printSettings.MarginTop = 0.79;
            printSettings.MarginBottom = 0.79;
            printSettings.MarginLeft = 0.79;
            printSettings.MarginRight = 0.79;
            printSettings.ShouldPrintBackgrounds = true;

            var success = await webView.CoreWebView2.PrintToPdfAsync(destPdfPath, printSettings);
            return success;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (originalHtml != null)
            {
                webView.NavigateToString(originalHtml);
            }
        }
    }

    private record ScrollMessage(string Type, int Line);

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri;

        // Allowlist explícita — schemes não listados são cancelados no final.
        // Em particular: javascript:, file:, vbscript:, blob:, ftp: e demais
        // são bloqueados pra prevenir XSS e leitura arbitrária de arquivos locais.

        // Navegações internas seguras do WebView2 (init e conteúdo via NavigateToString)
        if (uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase)) return;

        // Conteúdo do preview vem via NavigateToString — o WebView2 sintetiza
        // URIs no padrão "data:text/html;charset=utf-16le;base64,..." pra esse caso.
        // Liberamos APENAS data: que NÃO contenha "<script" pra evitar abuso via
        // [link](data:text/html,<script>...) em markdown malicioso.
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            // Aceita data: apenas se for a navegação inicial do NavigateToString
            // (e não um clique do usuário em link data: malicioso)
            if (e.IsUserInitiated)
            {
                e.Cancel = true;
                return;
            }
            return;
        }

        // Virtual host mapping pra assets locais (imagens do markdown)
        if (uri.StartsWith("https://local.assets/", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("http://local.assets/", StringComparison.OrdinalIgnoreCase))
            return;

        // file:// usado internamente pelo Export PDF (temp HTML)
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && !e.IsUserInitiated)
            return;

        // Custom markdown navigation scheme
        if (uri.StartsWith("mdnav://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            var queryString = new Uri(uri).Query;
            var path = HttpUtility.ParseQueryString(queryString)["path"];
            if (!string.IsNullOrEmpty(path))
            {
                var decodedPath = Uri.UnescapeDataString(path);
                LinkClicked?.Invoke(decodedPath);
            }
            return;
        }

        // External links - open in browser
        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            ExternalLinkClicked?.Invoke(uri);
            return;
        }

        // Default deny — qualquer outro scheme (javascript:, file: iniciado pelo usuário,
        // vbscript:, blob:, etc.) é cancelado.
        e.Cancel = true;
    }
}
