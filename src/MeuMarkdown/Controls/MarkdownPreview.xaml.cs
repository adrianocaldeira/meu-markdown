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
    private string? _pendingEnhancementDir;
    private string? _findTerm;
    private bool _findCaseSensitive;
    private bool _findWholeWord;

    public event Action<string, string?>? LinkClicked;
    public event Action<string>? ExternalLinkClicked;
    public event Action<int>? PreviewScrolled;
    public event Action? ExportHtmlRequested;
    public event Action? ExportPdfRequested;

    /// <summary>Dispara quando o contador de matches da busca no preview muda: (índice ativo, total).</summary>
    public event EventHandler<(int activeIndex, int total)>? FindMatchesChanged;

    /// <summary>True quando o conteúdo web do preview está com foco de teclado.</summary>
    public bool IsPreviewFocused { get; private set; }

    public MarkdownPreview()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        webView.GotFocus += (_, _) => IsPreviewFocused = true;
        webView.LostFocus += (_, _) => IsPreviewFocused = false;
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

            // Virtual host pra servir Mermaid/KaTeX (scripts grandes contornam o
            // cap de ~2MB do NavigateToString). O diretório vem do MainWindow,
            // que extrai os assets via MarkdownService.ExtractEnhancementAssetsTo.
            if (!string.IsNullOrEmpty(_pendingEnhancementDir))
            {
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "mm.local", _pendingEnhancementDir,
                    CoreWebView2HostResourceAccessKind.Allow);
                _pendingEnhancementDir = null;
            }
            // Habilitamos o context menu pra ter "Copiar" quando há texto selecionado.
            // O handler ContextMenuRequested filtra pra deixar SÓ os itens de cópia
            // (sem print, reload, saveAs, etc).
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            _isInitialized = true;
            webView.CoreWebView2.Find.MatchCountChanged += OnFindStatusChanged;
            webView.CoreWebView2.Find.ActiveMatchIndexChanged += OnFindStatusChanged;
            // Re-aplica a busca ativa após cada navegação (re-render via NavigateToString),
            // para o highlight sobreviver à digitação/reload enquanto a barra está aberta.
            webView.CoreWebView2.NavigationCompleted += OnNavCompletedReapplyFind;
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
        // Cada NavigateToString cria uma nova "página". Pre-aplicamos o tema dark
        // no body class antes da navegação pra evitar flash de light->dark (que
        // acontecia quando setTheme() era chamado só após NavigationCompleted).
        if (_isDarkTheme)
        {
            html = html.Replace("<body>", "<body class=\"dark\">");
        }
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

    /// <summary>
    /// Registra o diretório com Mermaid/KaTeX como virtual host "mm.local".
    /// Pode ser chamado antes ou depois do WebView2 inicializar.
    /// </summary>
    public void RegisterEnhancementAssetsHost(string directory)
    {
        if (string.IsNullOrEmpty(directory)) return;
        if (_isInitialized)
        {
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mm.local", directory,
                CoreWebView2HostResourceAccessKind.Allow);
        }
        else
        {
            _pendingEnhancementDir = directory;
        }
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

    /// <summary>
    /// Inicia (ou atualiza) a busca no preview via API nativa do WebView2. Term vazio para a busca.
    /// </summary>
    public async void StartFind(string term, bool caseSensitive, bool wholeWord)
    {
        if (!_isInitialized) return;
        if (string.IsNullOrEmpty(term))
        {
            StopFind();
            return;
        }
        _findTerm = term;
        _findCaseSensitive = caseSensitive;
        _findWholeWord = wholeWord;

        var options = webView.CoreWebView2.Environment.CreateFindOptions();
        options.FindTerm = term;
        options.IsCaseSensitive = caseSensitive;
        options.ShouldMatchWord = wholeWord;
        options.ShouldHighlightAllMatches = true;
        options.SuppressDefaultFindDialog = true;
        try
        {
            await webView.CoreWebView2.Find.StartAsync(options);
        }
        catch
        {
            // Find indisponível (runtime Edge antigo): ignora silenciosamente.
        }
    }

    /// <summary>Vai para o próximo match.</summary>
    public void FindNext()
    {
        if (_isInitialized && _findTerm != null) webView.CoreWebView2.Find.FindNext();
    }

    /// <summary>Vai para o match anterior.</summary>
    public void FindPrevious()
    {
        if (_isInitialized && _findTerm != null) webView.CoreWebView2.Find.FindPrevious();
    }

    /// <summary>Encerra a busca e limpa os highlights.</summary>
    public void StopFind()
    {
        _findTerm = null;
        if (_isInitialized) webView.CoreWebView2.Find.Stop();
    }

    private void OnFindStatusChanged(object? sender, object e)
    {
        if (!_isInitialized) return;
        var f = webView.CoreWebView2.Find;
        FindMatchesChanged?.Invoke(this, (f.ActiveMatchIndex, f.MatchCount));
    }

    private void OnNavCompletedReapplyFind(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_findTerm != null)
            StartFind(_findTerm, _findCaseSensitive, _findWholeWord);
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

            // Aguarda Mermaid/KaTeX renderizarem antes de gerar o PDF — caso contrário
            // o snapshot pega diagramas/fórmulas ainda como source bruto.
            await webView.CoreWebView2.ExecuteScriptAsync(
                "(async () => { if (typeof renderEnhancements === 'function') { await renderEnhancements(); } return 'done'; })()"
            );

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

    private static readonly HashSet<string> _allowedContextMenuItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "copy",             // Copiar (quando há seleção)
        "copyLinkLocation", // Copiar endereço do link (quando direito em link)
        "copyImage",        // Copiar imagem (quando direito em imagem)
        "copyImageLink",    // Copiar link da imagem
    };

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        // Remove items default que não são ações de cópia (Print, Reload, Save As, etc).
        for (int i = e.MenuItems.Count - 1; i >= 0; i--)
        {
            if (!_allowedContextMenuItems.Contains(e.MenuItems[i].Name))
                e.MenuItems.RemoveAt(i);
        }

        var env = webView.CoreWebView2.Environment;

        // Se sobrou alguma ação de cópia, adiciona um separator antes dos exports.
        if (e.MenuItems.Count > 0)
        {
            var sep = env.CreateContextMenuItem(
                string.Empty, null, CoreWebView2ContextMenuItemKind.Separator);
            e.MenuItems.Add(sep);
        }

        // Exports do documento — sempre disponíveis (clique em qualquer ponto do preview).
        var exportHtml = env.CreateContextMenuItem(
            "Exportar para HTML…", null, CoreWebView2ContextMenuItemKind.Command);
        exportHtml.CustomItemSelected += (_, _) => ExportHtmlRequested?.Invoke();
        e.MenuItems.Add(exportHtml);

        var exportPdf = env.CreateContextMenuItem(
            "Exportar para PDF…", null, CoreWebView2ContextMenuItemKind.Command);
        exportPdf.CustomItemSelected += (_, _) => ExportPdfRequested?.Invoke();
        e.MenuItems.Add(exportPdf);
    }

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

        // Virtual host pra Mermaid/KaTeX (scripts servidos pelo virtual host
        // pra contornar o cap de ~2MB do NavigateToString)
        if (uri.StartsWith("https://mm.local/", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("http://mm.local/", StringComparison.OrdinalIgnoreCase))
            return;

        // file:// usado internamente pelo Export PDF (temp HTML)
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && !e.IsUserInitiated)
            return;

        // Custom markdown navigation scheme
        if (uri.StartsWith("mdnav://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            var queryString = new Uri(uri).Query;
            var qs = HttpUtility.ParseQueryString(queryString);
            var path = qs["path"];
            var fragment = qs["fragment"];
            if (!string.IsNullOrEmpty(path))
            {
                var decodedPath = Uri.UnescapeDataString(path);
                var decodedFragment = string.IsNullOrEmpty(fragment)
                    ? null
                    : Uri.UnescapeDataString(fragment);
                LinkClicked?.Invoke(decodedPath, decodedFragment);
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
