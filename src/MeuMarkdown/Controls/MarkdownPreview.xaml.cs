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
