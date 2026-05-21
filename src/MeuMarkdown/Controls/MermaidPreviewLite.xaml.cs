using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MeuMarkdown.Controls;

public partial class MermaidPreviewLite : UserControl
{
    private bool _isInitialized;
    private string? _pendingCode;
    private string? _assetsDir;
    private bool _pendingDark;

    public event Action<string>? MermaidError;

    public MermaidPreviewLite()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetAssetsDirectory(string assetsDir)
    {
        _assetsDir = assetsDir;
    }

    public void SetDarkTheme(bool dark)
    {
        _pendingDark = dark;
        if (_isInitialized)
            _ = webView.CoreWebView2.ExecuteScriptAsync($"setBuilderTheme({(dark ? "true" : "false")})");
    }

    public async Task RenderAsync(string mermaidCode)
    {
        if (!_isInitialized)
        {
            _pendingCode = mermaidCode;
            return;
        }
        var encoded = JsonSerializer.Serialize(mermaidCode);
        await webView.CoreWebView2.ExecuteScriptAsync($"render({encoded})");
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

            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            if (!string.IsNullOrEmpty(_assetsDir))
            {
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "mm.local", _assetsDir,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var html = LoadEmbeddedHtml();
            webView.NavigateToString(html);

            _isInitialized = true;
            loadingText.Visibility = Visibility.Collapsed;

            void OnNavCompletedOnce(object? s, CoreWebView2NavigationCompletedEventArgs ev)
            {
                webView.CoreWebView2.NavigationCompleted -= OnNavCompletedOnce;
                if (_pendingDark)
                    _ = webView.CoreWebView2.ExecuteScriptAsync("setBuilderTheme(true)");
                if (_pendingCode != null)
                {
                    _ = RenderAsync(_pendingCode);
                    _pendingCode = null;
                }
            }
            webView.CoreWebView2.NavigationCompleted += OnNavCompletedOnce;
        }
        catch (Exception ex)
        {
            loadingText.Text = $"Erro: {ex.Message}";
        }
    }

    private static string LoadEmbeddedHtml()
    {
        var asm = typeof(MermaidPreviewLite).Assembly;
        const string resourceName = "MeuMarkdown.Resources.mermaid-preview-lite.html";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Recurso não encontrado: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "mermaidError")
            {
                var msg = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                MermaidError?.Invoke(msg);
            }
        }
        catch { }
    }
}
