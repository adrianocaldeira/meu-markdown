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

        // Allow initial about:blank and data: navigations
        if (uri.StartsWith("about:") || uri.StartsWith("data:")) return;

        // Custom markdown navigation scheme
        if (uri.StartsWith("mdnav://"))
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
        if (uri.StartsWith("http://") || uri.StartsWith("https://"))
        {
            e.Cancel = true;
            ExternalLinkClicked?.Invoke(uri);
            return;
        }
    }
}
