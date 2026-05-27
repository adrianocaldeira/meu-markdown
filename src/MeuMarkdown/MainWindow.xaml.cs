using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MeuMarkdown.ViewModels;
using MeuMarkdown.Services;
using MeuMarkdown.EditorBehaviors;
using MeuMarkdown.Controls;
using MeuMarkdown.Themes;
using Microsoft.Win32;

namespace MeuMarkdown;

public partial class MainWindow : Window
{
    // RoutedCommands for keyboard shortcuts
    public static readonly RoutedUICommand FormatBoldCommand = new("Bold", "FormatBold", typeof(MainWindow));
    public static readonly RoutedUICommand FormatItalicCommand = new("Italic", "FormatItalic", typeof(MainWindow));
    public static readonly RoutedUICommand FormatLinkCommand = new("Link", "FormatLink", typeof(MainWindow));
    public static readonly RoutedUICommand FormatInlineCodeCommand = new("InlineCode", "FormatInlineCode", typeof(MainWindow));
    public static readonly RoutedUICommand FormatH1Command = new("H1", "FormatH1", typeof(MainWindow));
    public static readonly RoutedUICommand FormatH2Command = new("H2", "FormatH2", typeof(MainWindow));
    public static readonly RoutedUICommand FormatH3Command = new("H3", "FormatH3", typeof(MainWindow));
    public static readonly RoutedUICommand FormatStrikethroughCommand = new("Strikethrough", "FormatStrikethrough", typeof(MainWindow));
    public static readonly RoutedUICommand ToggleViewModeCommand = new("ViewMode", "ToggleViewMode", typeof(MainWindow));
    public static readonly RoutedUICommand ToggleDarkThemeCommand = new("DarkTheme", "ToggleDarkTheme", typeof(MainWindow));
    public static readonly RoutedUICommand ToggleSidebarCommand = new("ToggleSidebar", "ToggleSidebar", typeof(MainWindow));
    public static readonly RoutedUICommand OpenOutlineCommand = new("OpenOutline", "OpenOutline", typeof(MainWindow));
    public static readonly RoutedUICommand FindCommand = new("Find", "Find", typeof(MainWindow));
    public static readonly RoutedUICommand ReplaceCommand = new("Replace", "Replace", typeof(MainWindow));
    public static readonly RoutedUICommand FindNextCommand = new("FindNext", "FindNext", typeof(MainWindow));
    public static readonly RoutedUICommand FindPrevCommand = new("FindPrev", "FindPrev", typeof(MainWindow));
    public static readonly RoutedUICommand QuickSwitcherCommand = new("QuickSwitcher", "QuickSwitcher", typeof(MainWindow));
    public static readonly RoutedUICommand ZenModeCommand = new("ZenMode", "ZenMode", typeof(MainWindow));
    public static readonly RoutedUICommand ZenSoloCommand = new("ZenSolo", "ZenSolo", typeof(MainWindow));
    public static readonly RoutedUICommand TypewriterCommand = new("Typewriter", "Typewriter", typeof(MainWindow));

    private readonly MainViewModel _viewModel;
    private readonly ExportService _exportService;
    private readonly DispatcherTimer _debounceTimer;
    private bool _isDarkTheme;
    private bool _isViewMode;
    private bool _suppressEditorUpdate;
    private bool _suppressSyncFromPreview;
    private bool _suppressSyncFromEditor;
    private GridLength _savedEditorWidth = new(1, GridUnitType.Star);
    private readonly EditorSearchService _searchService = new();
    private readonly FindResultsRenderer _findRenderer = new();
    private int _activeFindIndex = -1;
    private FindRequest? _lastFindRequest;
    private enum ZenSoloState { None, EditorOnly, PreviewOnly }
    private bool _isZenMode;
    private ZenSoloState _zenSolo = ZenSoloState.None;
    private WindowState _savedWindowState;
    private WindowStyle _savedWindowStyle;
    private TypewriterScrollManager? _typewriterManager;
    private readonly Services.MermaidTemplateService _mermaidTemplateService = new();
    private string _assetsDir = "";

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _exportService = new ExportService(_viewModel.MarkdownService);

        // Aplicar estado persistido
        var state = App.State;
        _viewModel.SidebarActivePanel = state.Sidebar.ActivePanel;
        _viewModel.SidebarWidth = state.Sidebar.Width;
        _viewModel.IsSidebarCollapsed = state.Sidebar.Collapsed;
        _viewModel.IsActivityBarVisible = state.Sidebar.ActivityBarVisible;
        _viewModel.SyncScrollEnabled = state.Preferences.SyncScrollEnabled;
        syncScrollMenuItem.IsChecked = _viewModel.SyncScrollEnabled;

        // Aplicar window state — com clamping contra área de tela visível
        Width = state.Window.Width;
        Height = state.Window.Height;
        if (!double.IsNaN(state.Window.X) && !double.IsNaN(state.Window.Y))
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            // Janela precisa ter ao menos 100px visíveis no virtual screen pra ser usável
            var titleBarVisible =
                state.Window.X + state.Window.Width > virtualLeft + 100 &&
                state.Window.X < virtualRight - 100 &&
                state.Window.Y + 30 > virtualTop &&
                state.Window.Y < virtualBottom - 100;

            if (titleBarVisible)
            {
                Left = state.Window.X;
                Top = state.Window.Y;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
            // Senão: deixa o XAML CenterScreen tomar conta
        }
        if (state.Window.Maximized)
            WindowState = WindowState.Maximized;

        // Set window icon from embedded resource
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/app-icon.ico", UriKind.Absolute);
            Icon = new System.Windows.Media.Imaging.BitmapImage(iconUri);
        }
        catch { }

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += OnDebounceTimerTick;

        textEditor.TextChanged += OnEditorTextChanged;
        textEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        preview.LinkClicked += OnPreviewLinkClicked;
        preview.ExternalLinkClicked += OnExternalLinkClicked;
        preview.PreviewScrolled += OnPreviewScrolled;
        preview.ExportHtmlRequested += () => OnExportHtml(this, new RoutedEventArgs());
        preview.ExportPdfRequested += () => OnExportPdf(this, new RoutedEventArgs());
        textEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorScrollChanged;

        // Extrai Mermaid/KaTeX pra um dir local e registra como virtual host "mm.local"
        // no WebView2. Necessário porque NavigateToString tem cap de ~2MB e os scripts
        // inline ultrapassam o limite.
        _assetsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeuMarkdown", "webview-assets");
        _viewModel.MarkdownService.ExtractEnhancementAssetsTo(_assetsDir);
        preview.RegisterEnhancementAssetsHost(_assetsDir);

        // Register format command bindings
        CommandBindings.Add(new CommandBinding(FormatBoldCommand, (_, _) => WrapSelection("**", "**")));
        CommandBindings.Add(new CommandBinding(FormatItalicCommand, (_, _) => WrapSelection("*", "*")));
        CommandBindings.Add(new CommandBinding(FormatLinkCommand, (_, _) => InsertLink()));
        CommandBindings.Add(new CommandBinding(FormatInlineCodeCommand, (_, _) => WrapSelection("`", "`")));
        CommandBindings.Add(new CommandBinding(FormatH1Command, (_, _) => InsertLinePrefix("# ")));
        CommandBindings.Add(new CommandBinding(FormatH2Command, (_, _) => InsertLinePrefix("## ")));
        CommandBindings.Add(new CommandBinding(FormatH3Command, (_, _) => InsertLinePrefix("### ")));
        CommandBindings.Add(new CommandBinding(FormatStrikethroughCommand, (_, _) => WrapSelection("~~", "~~")));
        CommandBindings.Add(new CommandBinding(ToggleViewModeCommand, (_, _) => ToggleViewMode()));
        CommandBindings.Add(new CommandBinding(ToggleDarkThemeCommand, (_, _) => ToggleDarkTheme()));
        CommandBindings.Add(new CommandBinding(ToggleSidebarCommand, (_, _) => ToggleSidebar()));
        CommandBindings.Add(new CommandBinding(OpenOutlineCommand, (_, _) =>
        {
            _viewModel.SidebarActivePanel = "Outline";
            _viewModel.IsSidebarCollapsed = false;
            sidebarCol.Width = new GridLength(_viewModel.SidebarWidth);
            sidebarSplitterCol.Width = new GridLength(4);
            sidebarHost.ShowPanel("Outline");
            activityBar.SetActivePanel("Outline");
            sidebarHost.OutlinePanel.HeadingsList.Focus();
        }));

        // F5 / F6 key bindings
        InputBindings.Add(new KeyBinding(ToggleViewModeCommand, Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(ToggleDarkThemeCommand, Key.F6, ModifierKeys.None));

        // Configurar colunas da sidebar conforme estado
        activityBarCol.Width = _viewModel.IsActivityBarVisible ? GridLength.Auto : new GridLength(0);
        if (_viewModel.IsActivityBarVisible && !_viewModel.IsSidebarCollapsed)
        {
            sidebarCol.Width = new GridLength(_viewModel.SidebarWidth);
            sidebarSplitterCol.Width = new GridLength(4);
        }
        else
        {
            sidebarCol.Width = new GridLength(0);
            sidebarSplitterCol.Width = new GridLength(0);
        }
        activityBar.SetActivePanel(_viewModel.SidebarActivePanel);
        sidebarHost.ShowPanel(_viewModel.SidebarActivePanel);
        sidebarHost.OutlinePanel.HeadingSelected += OnOutlineHeadingSelected;
        activityBar.PanelSelected += OnActivityPanelSelected;

        // Restaurar sort mode do Explorer antes de abrir o workspace (afeta o BuildNode).
        _viewModel.WorkspaceService.SortMode = ParseSortMode(App.State.Preferences.ExplorerSort);

        // Restaurar workspace + recents persistidos
        var lastWs = App.State.LastWorkspace;
        if (!string.IsNullOrEmpty(lastWs) && System.IO.Directory.Exists(lastWs))
        {
            _viewModel.WorkspaceService.Open(lastWs, App.State.Preferences.ExplorerShowAllFiles);
        }
        _viewModel.RecentFilesService.LoadFrom(App.State.RecentFiles);

        sidebarHost.ExplorerPanel.Bind(
            _viewModel.WorkspaceService,
            _viewModel.RecentFilesService,
            App.State.Preferences.ExplorerShowAllFiles);
        sidebarHost.ExplorerPanel.FileActivated += OnExplorerFileActivated;
        sidebarHost.ExplorerPanel.FilePreview += OnExplorerFilePreview;
        sidebarHost.ExplorerPanel.OpenFolderRequested += OnOpenFolderRequested;
        sidebarHost.ExplorerPanel.CloseWorkspaceRequested += OnCloseWorkspaceRequested;
        sidebarHost.ExplorerPanel.ShowAllFilesChanged += OnShowAllFilesChanged;
        sidebarHost.ExplorerPanel.SortModeChanged += OnExplorerSortChanged;

        sidebarHost.SearchPanel.Bind(_viewModel.WorkspaceService, _viewModel.WorkspaceSearchService);
        sidebarHost.SearchPanel.MatchActivated += OnSearchMatchActivated;

        sidebarHost.SettingsPanel.Bind(_viewModel, App.State.Preferences.ExplorerShowAllFiles);
        sidebarHost.SettingsPanel.SyncScrollChanged += (_, val) =>
        {
            _viewModel.SyncScrollEnabled = val;
            syncScrollMenuItem.IsChecked = val;
        };
        sidebarHost.SettingsPanel.TypewriterChanged += (_, val) =>
        {
            _viewModel.TypewriterMode = val;
            _typewriterManager!.Enabled = val;
            typewriterMenuItem.IsChecked = val;
        };
        sidebarHost.SettingsPanel.ShowAllFilesChanged += (_, val) => OnShowAllFilesChanged(this, val);
        sidebarHost.SettingsPanel.ChangeWorkspaceRequested += (_, _) => OnOpenFolderRequested(this, EventArgs.Empty);
        sidebarHost.SettingsPanel.ClearRecentsRequested += (_, _) =>
        {
            _viewModel.RecentFilesService.Clear();
            sidebarHost.SettingsPanel.RefreshWorkspaceAndRecents();
        };

        LoadMarkdownSyntaxHighlighting();
        SmartListBehavior.Attach(textEditor);
        AutoPairBehavior.Attach(textEditor);
        ImagePasteHandler.Attach(textEditor, () => _viewModel.SelectedTab);
        WikiLinkCompletion.Attach(textEditor, () => _viewModel.WorkspaceService);
        _typewriterManager = new TypewriterScrollManager(textEditor);
        _viewModel.TypewriterMode = App.State.Preferences.TypewriterMode;
        _typewriterManager.Enabled = _viewModel.TypewriterMode;
        typewriterMenuItem.IsChecked = _viewModel.TypewriterMode;

        textEditor.TextArea.TextView.BackgroundRenderers.Add(_findRenderer);
        findBar.FindRequested += OnFindRequested;
        findBar.NextRequested += (_, _) => MoveToFindMatch(+1);
        findBar.PrevRequested += (_, _) => MoveToFindMatch(-1);
        findBar.CloseRequested += (_, _) => CloseFindBar();
        findBar.ReplaceOneRequested += OnReplaceOne;
        findBar.ReplaceAllRequested += OnReplaceAll;

        CommandBindings.Add(new CommandBinding(FindCommand, (_, _) => OpenFindBar(showReplace: false)));
        CommandBindings.Add(new CommandBinding(ReplaceCommand, (_, _) => OpenFindBar(showReplace: true)));
        CommandBindings.Add(new CommandBinding(FindNextCommand, (_, _) => MoveToFindMatch(+1)));
        CommandBindings.Add(new CommandBinding(FindPrevCommand, (_, _) => MoveToFindMatch(-1)));
        CommandBindings.Add(new CommandBinding(QuickSwitcherCommand, (_, _) => OpenQuickSwitcher()));
        quickSwitcher.FileSelected += (_, path) => _viewModel.OpenFileByPath(path);
        CommandBindings.Add(new CommandBinding(ZenModeCommand, (_, _) => ToggleZenMode()));
        CommandBindings.Add(new CommandBinding(ZenSoloCommand, (_, _) => ToggleZenSolo()));
        CommandBindings.Add(new CommandBinding(TypewriterCommand, (_, _) =>
        {
            _viewModel.TypewriterMode = !_viewModel.TypewriterMode;
            _typewriterManager!.Enabled = _viewModel.TypewriterMode;
            typewriterMenuItem.IsChecked = _viewModel.TypewriterMode;
        }));

        _isDarkTheme = IsOsDarkMode();
        ApplyTheme();
        darkThemeMenuItem.IsChecked = _isDarkTheme;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        Closing += OnWindowClosing;

        // Restaurar abas abertas da sessão anterior
        foreach (var path in App.State.OpenTabs ?? new List<string>())
        {
            if (File.Exists(path))
                _viewModel.OpenFileByPath(path);
        }
        if (!string.IsNullOrEmpty(App.State.ActiveTab))
        {
            var active = _viewModel.Tabs.FirstOrDefault(t => t.FilePath == App.State.ActiveTab);
            if (active != null) _viewModel.SelectedTab = active;
        }
        // Restaurar quais abas estavam fixadas (pinned)
        var pinnedSet = new HashSet<string>(
            App.State.PinnedTabs ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var tab in _viewModel.Tabs)
        {
            if (pinnedSet.Contains(tab.FilePath)) tab.IsPinned = true;
        }

        // Restaurar modo de visualização (F5)
        _isViewMode = App.State.Preferences.ViewMode;
        ApplyViewMode();

        // Open files passed as command-line arguments (after session restore — viram aba ativa)
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]) &&
                (args[i].EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                 args[i].EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
                _viewModel.OpenFileByPath(args[i]);
        }

        // Verificação silenciosa de atualização (toast aparece ~10s depois se houver versão nova).
        CheckForUpdatesInBackgroundAsync();
        StartPeriodicUpdateCheck();
    }

    private void LoadMarkdownSyntaxHighlighting()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MeuMarkdown.Resources.markdown-syntax.xshd");
            if (stream == null) return;
            using var reader = new XmlTextReader(stream);
            var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            textEditor.SyntaxHighlighting = highlighting;
        }
        catch
        {
            // Silently ignore syntax highlighting errors
        }
    }

    private static bool IsOsDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        Dispatcher.Invoke(() =>
        {
            _isDarkTheme = IsOsDarkMode();
            darkThemeMenuItem.IsChecked = _isDarkTheme;
            ApplyTheme();
        });
    }

    // === View Mode (read-only preview) ===

    private void ToggleViewMode()
    {
        _isViewMode = !_isViewMode;
        ApplyViewMode();
    }

    /// <summary>
    /// Força o modo de visualização para o valor especificado.
    /// Usado pelos handlers do Explorer (single click = visualização, double = edit+preview).
    /// </summary>
    public void SetViewMode(bool viewMode)
    {
        if (_isViewMode == viewMode) return;
        _isViewMode = viewMode;
        ApplyViewMode();
    }

    private void ApplyViewMode()
    {
        viewModeMenuItem.IsChecked = _isViewMode;
        viewModeToggle.IsChecked = _isViewMode;

        if (_isViewMode)
        {
            // Save current editor width and hide editor
            _savedEditorWidth = editorColumn.Width;
            editorColumn.Width = new GridLength(0);
            editorColumn.MinWidth = 0;
            splitterColumn.Width = new GridLength(0);
            editorBorder.Visibility = Visibility.Collapsed;
            gridSplitter.Visibility = Visibility.Collapsed;
            toolbarBorder.Visibility = Visibility.Collapsed;
            viewModeText.Text = "Visualização";
        }
        else
        {
            // Restore editor
            editorColumn.Width = _savedEditorWidth;
            editorColumn.MinWidth = 100;
            splitterColumn.Width = new GridLength(5);
            editorBorder.Visibility = Visibility.Visible;
            gridSplitter.Visibility = Visibility.Visible;
            toolbarBorder.Visibility = Visibility.Visible;
            viewModeText.Text = "";
        }
    }

    private void OnToggleViewMode(object sender, RoutedEventArgs e)
    {
        _isViewMode = viewModeMenuItem.IsChecked;
        ApplyViewMode();
    }

    private void OnToggleSyncScroll(object sender, RoutedEventArgs e)
    {
        _viewModel.SyncScrollEnabled = syncScrollMenuItem.IsChecked;
    }

    private void OnToggleViewModeButton(object sender, RoutedEventArgs e)
    {
        _isViewMode = viewModeToggle.IsChecked == true;
        ApplyViewMode();
    }

    // === Editor events ===

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();

        if (!_suppressEditorUpdate && _viewModel.SelectedTab != null)
        {
            _viewModel.SelectedTab.Content = textEditor.Text;
        }
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        UpdatePreview();
        if (_viewModel.SelectedTab != null)
        {
            var headings = _viewModel.MarkdownService.ExtractHeadings(textEditor.Text);
            _viewModel.SelectedTab.UpdateHeadings(headings);
        }
        if (_viewModel.SelectedTab != null)
        {
            var text = textEditor.Text;
            var words = System.Text.RegularExpressions.Regex.Matches(text, @"\b\w+\b").Count;
            _viewModel.SelectedTab.UpdateMetrics(words, text.Length);
        }
        TryConsumePendingScrollFragment();
    }

    private void UpdatePreview()
    {
        if (_viewModel.SelectedTab == null) return;

        var tab = _viewModel.SelectedTab;
        var html = _viewModel.RenderMarkdown(textEditor.Text, tab.Directory);
        preview.SetFullHtml(html);
        preview.SetDarkTheme(_isDarkTheme);

        if (!string.IsNullOrEmpty(tab.Directory))
            preview.SetVirtualHostMapping(tab.Directory);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        var line = textEditor.TextArea.Caret.Line;
        var col = textEditor.TextArea.Caret.Column;
        lineColText.Text = $"Ln {line}, Col {col}";
        // Atualiza heading atual no Outline
        if (_viewModel.SelectedTab != null && _viewModel.SelectedTab.Headings.Count > 0)
        {
            var currentLine = textEditor.TextArea.Caret.Line;
            MeuMarkdown.Models.Heading? currentHeading = null;
            foreach (var h in _viewModel.SelectedTab.Headings)
            {
                if (h.StartLine <= currentLine)
                    currentHeading = h;
                else
                    break;
            }
            _viewModel.SelectedTab.CurrentHeading = currentHeading;
            sidebarHost.OutlinePanel.HighlightCurrentHeading(currentHeading);
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        sidebarHost.OutlinePanel.BindToTab(_viewModel.SelectedTab);
        if (_viewModel.SelectedTab == null)
        {
            textEditor.Text = string.Empty;
            textEditor.IsEnabled = false;
            // Sem aba ativa: limpa o preview também (antes ficava mostrando o HTML da última aba).
            preview.SetFullHtml("");
            return;
        }

        textEditor.IsEnabled = true;
        _suppressEditorUpdate = true;
        textEditor.Text = _viewModel.SelectedTab.Content;
        _suppressEditorUpdate = false;
        UpdatePreview();

        // Reveal o arquivo ativo na árvore do Explorer (no-op se fora do workspace).
        sidebarHost.ExplorerPanel.RevealFile(_viewModel.SelectedTab.FilePath);

        // Se há um fragment pendente e os Headings já estão populados (aba já aberta antes),
        // consomemos aqui. Caso contrário (aba nova), o OnDebounceTimerTick vai consumir
        // após extrair os headings do conteúdo carregado.
        TryConsumePendingScrollFragment();
    }

    private void OnPreviewLinkClicked(string relativePath, string? fragment)
    {
        if (_viewModel.SelectedTab == null) return;
        _viewModel.Navigation.NavigateTo(relativePath, _viewModel.SelectedTab.Directory, fragment);
    }

    private void OnExternalLinkClicked(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void OnEditorScrollChanged(object? sender, EventArgs e)
    {
        if (!_viewModel.SyncScrollEnabled || _suppressSyncFromPreview) return;
        var textView = textEditor.TextArea.TextView;
        if (textView.VisualLines.Count == 0) return;
        var firstVisible = textView.VisualLines[0].FirstDocumentLine.LineNumber;
        _suppressSyncFromEditor = true;
        preview.ScrollToLine(firstVisible);
        Dispatcher.BeginInvoke(new Action(() => _suppressSyncFromEditor = false),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnPreviewScrolled(int line)
    {
        if (!_viewModel.SyncScrollEnabled || _suppressSyncFromEditor) return;
        _suppressSyncFromPreview = true;
        try
        {
            textEditor.ScrollToLine(line);
        }
        finally
        {
            Dispatcher.BeginInvoke(new Action(() => _suppressSyncFromPreview = false),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void OnCloseTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DocumentTabViewModel tab)
            _viewModel.CloseTabCommand.Execute(tab);
    }

    // === Theme ===

    private void ToggleDarkTheme()
    {
        _isDarkTheme = !_isDarkTheme;
        darkThemeMenuItem.IsChecked = _isDarkTheme;
        ApplyTheme();
    }

    private void OnToggleDarkTheme(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = darkThemeMenuItem.IsChecked;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        ThemeManager.Apply(_isDarkTheme ? AppTheme.Dark : AppTheme.Light);
        preview.SetDarkTheme(_isDarkTheme);
    }

    // === Drag & Drop ===

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var file in files)
        {
            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.OpenFileByPath(file);
            }
        }
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnShowAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    // Cacheado em memória após o check em background no startup. Usado pelo toast e
    // pelo diálogo do fechar pra evitar refazer a chamada HTTP.
    private UpdateInfo? _backgroundUpdateInfo;
    // Quando o user clica "Atualizar agora" no toast/diálogo, marca que o flow do
    // auto-update está em curso pra evitar que o diálogo do fechar dispare de novo.
    private bool _updateFlowInProgress;
    // Timer pra reverificar a cada 30min enquanto o app fica aberto (sem precisar fechar/abrir).
    private DispatcherTimer? _periodicUpdateCheckTimer;

    private void StartPeriodicUpdateCheck()
    {
        _periodicUpdateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _periodicUpdateCheckTimer.Tick += (_, _) =>
        {
            // Pula se o toast já está visível (user ainda não interagiu) ou se um
            // auto-update já está em curso — evita ruído.
            if (UpdateToastPopup.IsOpen || _updateFlowInProgress) return;
            CheckForUpdatesInBackgroundAsync(initialDelay: false);
        };
        _periodicUpdateCheckTimer.Start();
    }

    private async void CheckForUpdatesInBackgroundAsync(bool initialDelay = true)
    {
        var logger = new UpdateLogger();
        try
        {
            if (initialDelay)
            {
                logger.Log("BG_CHECK scheduled (delay 10s)");
                // Pequeno delay pra não atrapalhar o startup.
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            else
            {
                logger.Log("BG_CHECK scheduled (periodic, no delay)");
            }

            // Tenta até 3x: imediato, depois 30s e 90s se NetworkError (rede instável recupera).
            // Total ~2min de tentativas antes de desistir até o próximo startup.
            var retryDelays = new[] { TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) };
            UpdateCheckResult? result = null;
            for (int attempt = 0; attempt < retryDelays.Length; attempt++)
            {
                if (retryDelays[attempt] > TimeSpan.Zero)
                {
                    logger.Log($"BG_CHECK retry #{attempt} after {retryDelays[attempt].TotalSeconds}s");
                    await Task.Delay(retryDelays[attempt]);
                }
                logger.Log($"BG_CHECK starting (current=v{VersionInfo.Current})");
                var service = new UpdateService();
                result = await service.CheckForUpdatesAsync();
                logger.Log($"BG_CHECK result status={result.Status} latest={result.Info?.LatestVersion ?? "?"} err={result.ErrorMessage ?? ""}");

                if (result.Status != UpdateCheckStatus.NetworkError) break;
            }

            if (result == null || result.Status != UpdateCheckStatus.UpdateAvailable || result.Info == null)
                return;

            _backgroundUpdateInfo = result.Info;

            UpdateToastVersionText.Text = $"v{result.Info.CurrentVersion}  →  v{result.Info.LatestVersion}";

            var notesSummary = ExtractNotesSummary(result.Info.ReleaseNotes);
            if (!string.IsNullOrEmpty(notesSummary))
            {
                UpdateToastNotesText.Text = notesSummary;
                UpdateToastNotesText.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateToastNotesText.Visibility = Visibility.Collapsed;
            }

            ShowUpdateToastAnimated();
            logger.Log("BG_CHECK toast shown");
        }
        catch (Exception ex)
        {
            // Check silencioso — não atrapalha o usuário se falhar.
            logger.Log($"BG_CHECK exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Extrai os primeiros bullets da seção de release notes (markdown-style)
    /// pra mostrar no toast. Pega até 3 itens, máx ~180 chars, com elipse no fim.
    /// </summary>
    private static string ExtractNotesSummary(string? notes, int maxItems = 5, int maxChars = 350)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var bullets = notes
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r').TrimStart())
            .Where(l => l.StartsWith("- ") || l.StartsWith("* "))
            .Select(l => "• " + l[2..].Trim())
            .Take(maxItems)
            .ToList();
        if (bullets.Count == 0) return string.Empty;

        var text = string.Join("\n", bullets);
        if (text.Length > maxChars)
            text = text[..maxChars].TrimEnd() + "…";
        return text;
    }

    private void ShowUpdateToastAnimated()
    {
        // Posiciona o popup no canto inferior direito da janela. Custom placement callback
        // recebe os tamanhos atualizados a cada IsOpen=true ou move/resize da Window.
        UpdateToastPopup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
        {
            var x = targetSize.Width - popupSize.Width - 20;
            var y = targetSize.Height - popupSize.Height - 40;
            return new[] { new System.Windows.Controls.Primitives.CustomPopupPlacement(
                new Point(x, y),
                System.Windows.Controls.Primitives.PopupPrimaryAxis.None) };
        };
        UpdateToastPopup.IsOpen = true;

        // Slide-up (sobe da base 80px) + fade-in. Easing "EaseOut" desacelera no final
        // dando uma sensação de "pousando" no lugar.
        UpdateToast.Opacity = 0;
        UpdateToastTransform.X = 0;
        UpdateToastTransform.Y = 80;

        var dur = TimeSpan.FromMilliseconds(360);
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };

        var slide = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 80, To = 0, Duration = dur, EasingFunction = ease
        };
        var fade = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0, To = 1, Duration = dur, EasingFunction = ease
        };
        UpdateToastTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        UpdateToast.BeginAnimation(OpacityProperty, fade);
    }

    private void HideUpdateToast()
    {
        UpdateToastPopup.IsOpen = false;
    }

    private void OnUpdateToastInstall(object sender, RoutedEventArgs e)
    {
        HideUpdateToast();
        if (_backgroundUpdateInfo == null) return;
        _updateFlowInProgress = true;
        var update = new UpdateWindow(_backgroundUpdateInfo, autoStart: true) { Owner = this };
        if (update.ShowDialog() == true)
        {
            Close();
        }
        else
        {
            _updateFlowInProgress = false;
        }
    }

    private void OnUpdateToastLater(object sender, RoutedEventArgs e)
    {
        // Esconde nesta sessão; o diálogo do fechar ainda vai oferecer.
        HideUpdateToast();
    }

    private void OnUpdateToastDismiss(object sender, RoutedEventArgs e)
    {
        // X = mesmo comportamento do "Mais tarde": só esconde, não persiste dismissal
        // (que fica reservado pro botão explícito do diálogo do fechar).
        HideUpdateToast();
    }

    private void OnUpdateToastReleaseNotes(object sender, RoutedEventArgs e)
    {
        // Abre a página do release no browser sem fechar o toast.
        if (_backgroundUpdateInfo == null || string.IsNullOrEmpty(_backgroundUpdateInfo.ReleaseUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_backgroundUpdateInfo.ReleaseUrl)
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    // === Tab drag-drop reorder ===
    private System.Windows.Point _tabDragStart;
    private DocumentTabViewModel? _tabDragSource;

    private void OnTabHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Não inicia drag se o clique foi no botão de fechar (X) da aba.
        if (e.OriginalSource is DependencyObject d && FindAncestor<Button>(d) != null) return;

        _tabDragStart = e.GetPosition(null);
        _tabDragSource = (sender as FrameworkElement)?.DataContext as DocumentTabViewModel;
    }

    private void OnTabHeaderMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Click puro (sem arrastar) — limpa o estado pra próxima interação.
        _tabDragSource = null;
    }

    private void OnTabHeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _tabDragSource == null) return;
        var diff = e.GetPosition(null) - _tabDragStart;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (sender is DependencyObject element)
        {
            var source = _tabDragSource;
            _tabDragSource = null; // limpa antes do DoDragDrop (que é bloqueante)
            DragDrop.DoDragDrop(element, source, DragDropEffects.Move);
        }
    }

    private void OnTabHeaderDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(DocumentTabViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTabHeaderDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(DocumentTabViewModel)) is not DocumentTabViewModel source) return;
        var target = (sender as FrameworkElement)?.DataContext as DocumentTabViewModel;
        if (target == null || ReferenceEquals(target, source)) return;

        var oldIdx = _viewModel.Tabs.IndexOf(source);
        var newIdx = _viewModel.Tabs.IndexOf(target);
        if (oldIdx < 0 || newIdx < 0) return;

        _viewModel.Tabs.Move(oldIdx, newIdx);
        _viewModel.SelectedTab = source;
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    // === Tab context menu handlers ===
    // O DataContext do MenuItem dentro do ContextMenu é o DocumentTabViewModel da aba clicada
    // (vem do StackPanel.PlacementTarget). Comandos são acessados via Execute() pq RelativeSource
    // pra Window não funciona dentro de Popup (ContextMenu vive fora do visual tree).

    private DocumentTabViewModel? TabFromMenu(object sender)
        => (sender as FrameworkElement)?.DataContext as DocumentTabViewModel;

    private void OnTabCtxCloseThis(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        if (tab != null) _viewModel.CloseTabCommand.Execute(tab);
    }

    private void OnTabCtxTogglePin(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        if (tab != null) _viewModel.TogglePinTabCommand.Execute(tab);
    }

    private void OnTabCtxCloseOthers(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        if (tab != null) _viewModel.CloseOtherTabsCommand.Execute(tab);
    }

    private void OnTabCtxCloseUnpinned(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        _viewModel.CloseUnpinnedTabsCommand.Execute(tab);
    }

    private void OnTabCtxCloseLeft(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        if (tab != null) _viewModel.CloseLeftTabsCommand.Execute(tab);
    }

    private void OnTabCtxCloseRight(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        if (tab != null) _viewModel.CloseRightTabsCommand.Execute(tab);
    }

    private void OnTabCtxCloseUnchanged(object sender, RoutedEventArgs e)
    {
        var tab = TabFromMenu(sender);
        _viewModel.CloseUnchangedTabsCommand.Execute(tab);
    }

    private void OnCheckForUpdates(object sender, RoutedEventArgs e)
    {
        var update = new UpdateWindow { Owner = this };
        if (update.ShowDialog() == true)
        {
            // Auto-update disparou o setup. Fechar a MainWindow pra rodar o OnClosing,
            // que persiste state.json antes do app encerrar.
            Close();
        }
    }

    // === Window chrome (custom title bar) ===

    private void OnMinimizeWindow(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreWindow(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        var maximized = WindowState == WindowState.Maximized;

        if (FindName("MaxIconNormal") is System.Windows.Shapes.Path normalIcon &&
            FindName("MaxIconRestore") is System.Windows.Shapes.Path restoreIcon)
        {
            normalIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
            restoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // === Workaround do WindowChrome para maximize ===
    // Com WindowStyle=None + WindowChrome, ao maximizar o Windows estende a janela
    // ~7px além de cada borda da tela E ignora o taskbar (vai pra tela cheia).
    // Resultado: title bar perde altura no topo, conteúdo no fundo é coberto pelo taskbar.
    // Fix: interceptar WM_GETMINMAXINFO e informar o tamanho/posição corretos baseados
    // no WorkArea do monitor (que exclui taskbar).
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref mi);
            var work = mi.rcWork;
            var monitorArea = mi.rcMonitor;
            mmi.ptMaxPosition.x = Math.Abs(work.left - monitorArea.left);
            mmi.ptMaxPosition.y = Math.Abs(work.top - monitorArea.top);
            mmi.ptMaxSize.x = Math.Abs(work.right - work.left);
            mmi.ptMaxSize.y = Math.Abs(work.bottom - work.top);
        }
        System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINTAPI { public int x; public int y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINTAPI ptReserved;
        public POINTAPI ptMaxSize;
        public POINTAPI ptMaxPosition;
        public POINTAPI ptMinTrackSize;
        public POINTAPI ptMaxTrackSize;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private void OnSetAsDefault(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = new FileAssociationService().Register();
            if (result == FileAssociationResult.Success)
            {
                MessageBox.Show(
                    "MeuMarkdown definido como padrão para arquivos .md.\n\nAo dar duplo clique em qualquer arquivo .md no Explorer, ele abrirá aqui.",
                    "Associação registrada",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "A associação foi registrada, mas o Windows mantém outro aplicativo como padrão para .md " +
                    "(uma escolha protegida do sistema).\n\n" +
                    "Para concluir, clique com o botão direito em um arquivo .md no Explorer → " +
                    "\"Abrir com\" → \"Escolher outro aplicativo\" → selecione MeuMarkdown e marque " +
                    "\"Sempre usar este aplicativo\".",
                    "Ação necessária no Windows",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erro ao registrar associação:\n{ex.Message}",
                "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // === Toolbar format button handlers ===

    private void OnFormatH1(object sender, RoutedEventArgs e) => InsertLinePrefix("# ");
    private void OnFormatH2(object sender, RoutedEventArgs e) => InsertLinePrefix("## ");
    private void OnFormatH3(object sender, RoutedEventArgs e) => InsertLinePrefix("### ");
    private void OnFormatBold(object sender, RoutedEventArgs e) => WrapSelection("**", "**");
    private void OnFormatItalic(object sender, RoutedEventArgs e) => WrapSelection("*", "*");
    private void OnFormatStrikethrough(object sender, RoutedEventArgs e) => WrapSelection("~~", "~~");
    private void OnFormatLink(object sender, RoutedEventArgs e) => InsertLink();
    private void OnFormatImage(object sender, RoutedEventArgs e) => InsertImage();
    private void OnFormatInlineCode(object sender, RoutedEventArgs e) => WrapSelection("`", "`");
    private void OnFormatCodeBlock(object sender, RoutedEventArgs e) => WrapSelection("\n```\n", "\n```\n");
    private void OnFormatBulletList(object sender, RoutedEventArgs e) => InsertLinePrefix("- ");
    private void OnFormatNumberedList(object sender, RoutedEventArgs e) => InsertLinePrefix("1. ");
    private void OnFormatCheckbox(object sender, RoutedEventArgs e) => InsertLinePrefix("- [ ] ");
    private void OnFormatQuote(object sender, RoutedEventArgs e) => InsertLinePrefix("> ");
    private void OnFormatHorizontalRule(object sender, RoutedEventArgs e) => InsertAtCursor("\n---\n");
    private void OnFormatTable(object sender, RoutedEventArgs e) =>
        InsertAtCursor("\n| Coluna 1 | Coluna 2 | Coluna 3 |\n|----------|----------|----------|\n| Valor    | Valor    | Valor    |\n");

    // === Format helper methods ===

    private void WrapSelection(string before, string after)
    {
        var selection = textEditor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            var offset = textEditor.CaretOffset;
            var placeholder = "texto";
            textEditor.Document.Insert(offset, before + placeholder + after);
            textEditor.Select(offset + before.Length, placeholder.Length);
        }
        else
        {
            var selectedText = selection.GetText();
            var startOffset = textEditor.SelectionStart;
            var length = textEditor.SelectionLength;
            textEditor.Document.Replace(startOffset, length, before + selectedText + after);
            textEditor.Select(startOffset + before.Length, selectedText.Length);
        }
        textEditor.Focus();
    }

    private void InsertLinePrefix(string prefix)
    {
        var line = textEditor.Document.GetLineByOffset(textEditor.CaretOffset);
        var lineText = textEditor.Document.GetText(line.Offset, line.Length);

        if (lineText.StartsWith(prefix))
        {
            textEditor.Document.Replace(line.Offset, prefix.Length, "");
        }
        else
        {
            if (prefix.TrimEnd().StartsWith("#"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(lineText, @"^#{1,6}\s");
                if (match.Success)
                    textEditor.Document.Replace(line.Offset, match.Length, prefix);
                else
                    textEditor.Document.Insert(line.Offset, prefix);
            }
            else
            {
                textEditor.Document.Insert(line.Offset, prefix);
            }
        }
        textEditor.Focus();
    }

    private void InsertAtCursor(string text)
    {
        textEditor.Document.Insert(textEditor.CaretOffset, text);
        textEditor.CaretOffset += text.Length;
        textEditor.Focus();
    }

    private void OnMermaidToolbarClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.ContextMenu == null) return;
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        btn.ContextMenu.IsOpen = true;
    }

    private void OnInsertMermaidTemplate(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.Tag is not string typeName) return;
        if (!Enum.TryParse<Models.Mermaid.MermaidDiagramType>(typeName, out var type)) return;

        var template = _mermaidTemplateService.GetTemplate(type);
        InsertMermaidBlock(template);
    }

    private void OnOpenMermaidBuilder(object sender, RoutedEventArgs e)
    {
        var win = new Views.MermaidBuilderWindow(_assetsDir, _isDarkTheme)
        {
            Owner = this,
        };
        var ok = win.ShowDialog();
        if (ok == true && !string.IsNullOrWhiteSpace(win.ResultMermaidCode))
        {
            InsertMermaidBlock(win.ResultMermaidCode);
        }
    }

    private void InsertMermaidBlock(string mermaidCode)
    {
        if (textEditor == null) return;
        var content = textEditor.Document.Text ?? "";
        var caret = textEditor.CaretOffset;

        var (text, newOffset) = Services.MarkdownInsertionService
            .BuildMermaidInsertion(content, caret, mermaidCode);

        var selection = textEditor.TextArea.Selection;
        if (!selection.IsEmpty)
        {
            var startOffset = textEditor.SelectionStart;
            var length = textEditor.SelectionLength;
            textEditor.Document.Replace(startOffset, length, text);
            textEditor.CaretOffset = startOffset + text.Length;
        }
        else
        {
            textEditor.Document.Insert(caret, text);
            textEditor.CaretOffset = newOffset;
        }
        textEditor.Focus();
    }

    private void InsertLink()
    {
        var selection = textEditor.TextArea.Selection;
        var selectedText = selection.IsEmpty ? "texto do link" : selection.GetText();
        var startOffset = selection.IsEmpty ? textEditor.CaretOffset : textEditor.SelectionStart;
        var length = selection.IsEmpty ? 0 : textEditor.SelectionLength;

        var linkMarkdown = $"[{selectedText}](url)";
        textEditor.Document.Replace(startOffset, length, linkMarkdown);
        var urlStart = startOffset + selectedText.Length + 3;
        textEditor.Select(urlStart, 3);
        textEditor.Focus();
    }

    private void InsertImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.gif;*.svg)|*.png;*.jpg;*.jpeg;*.gif;*.svg|Todos (*.*)|*.*",
            Title = "Selecionar imagem"
        };

        if (dialog.ShowDialog() == true)
        {
            var imagePath = dialog.FileName;
            if (_viewModel.SelectedTab != null && !string.IsNullOrEmpty(_viewModel.SelectedTab.Directory))
            {
                try
                {
                    var baseUri = new Uri(_viewModel.SelectedTab.Directory + Path.DirectorySeparatorChar);
                    var imageUri = new Uri(imagePath);
                    imagePath = Uri.UnescapeDataString(baseUri.MakeRelativeUri(imageUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
                }
                catch { }
            }
            InsertAtCursor($"![imagem]({imagePath})");
        }
    }

    private void OnActivityPanelSelected(object? sender, string panelName)
    {
        if (_isZenMode) return;
        if (_viewModel.SidebarActivePanel == panelName && !_viewModel.IsSidebarCollapsed)
        {
            // Clicou no mesmo painel ativo: colapsa
            _viewModel.IsSidebarCollapsed = true;
            sidebarCol.Width = new GridLength(0);
            sidebarSplitterCol.Width = new GridLength(0);
        }
        else
        {
            _viewModel.SidebarActivePanel = panelName;
            _viewModel.IsSidebarCollapsed = false;
            sidebarCol.Width = new GridLength(_viewModel.SidebarWidth);
            sidebarSplitterCol.Width = new GridLength(4);
            sidebarHost.ShowPanel(panelName);
        }
    }

    private void OnSidebarSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _viewModel.SidebarWidth = sidebarCol.ActualWidth;
    }

    private void OnToggleSidebar(object sender, RoutedEventArgs e) => ToggleSidebar();

    private void ToggleSidebar()
    {
        if (_isZenMode) return;
        _viewModel.IsActivityBarVisible = !_viewModel.IsActivityBarVisible;
        activityBarCol.Width = _viewModel.IsActivityBarVisible ? GridLength.Auto : new GridLength(0);

        if (!_viewModel.IsActivityBarVisible)
        {
            sidebarCol.Width = new GridLength(0);
            sidebarSplitterCol.Width = new GridLength(0);
        }
        else if (!_viewModel.IsSidebarCollapsed)
        {
            sidebarCol.Width = new GridLength(_viewModel.SidebarWidth);
            sidebarSplitterCol.Width = new GridLength(4);
        }
    }

    private void OnOutlineHeadingSelected(object? sender, MeuMarkdown.Models.Heading heading)
    {
        var line = Math.Max(1, Math.Min(heading.StartLine, textEditor.Document.LineCount));
        var offset = textEditor.Document.GetOffset(line, 1);
        textEditor.CaretOffset = offset;
        textEditor.ScrollToLine(line);
        // Em modo Visualização o editor fica oculto, então o sync via OnEditorScrollChanged
        // não chega no preview. Disparamos o scroll do preview explicitamente também.
        preview.ScrollToLine(line);
        if (!_isViewMode) textEditor.Focus();
    }

    private void TryConsumePendingScrollFragment()
    {
        var fragment = _viewModel.PendingScrollFragment;
        if (string.IsNullOrEmpty(fragment)) return;

        var tab = _viewModel.SelectedTab;
        if (tab == null) return;

        var heading = tab.Headings.FirstOrDefault(h =>
            string.Equals(h.AnchorId, fragment, StringComparison.OrdinalIgnoreCase));
        if (heading == null) return;

        // Consome o fragment antes de despachar para evitar chamadas duplicadas.
        _viewModel.PendingScrollFragment = null;

        // Defer ao próximo idle para garantir que editor e preview já estão renderizados.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            OnOutlineHeadingSelected(this, heading);
        });
    }

    private void OnExplorerFileActivated(object? sender, string filePath)
    {
        // Double-click no Explorer → modo edit+preview (split).
        _viewModel.OpenFileByPath(filePath);
        SetViewMode(false);
    }

    private void OnExplorerFilePreview(object? sender, string filePath)
    {
        // Single-click no Explorer → modo visualização (apenas preview).
        _viewModel.OpenFileByPath(filePath);
        SetViewMode(true);
    }

    private void OnOpenFolderRequested(object? sender, EventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Abrir pasta como workspace"
        };
        if (dlg.ShowDialog() == true)
        {
            _viewModel.CloseTabsOutsideWorkspace(dlg.FolderName);
            _viewModel.WorkspaceService.Open(dlg.FolderName, App.State.Preferences.ExplorerShowAllFiles);
            sidebarHost.ExplorerPanel.Bind(_viewModel.WorkspaceService, _viewModel.RecentFilesService,
                App.State.Preferences.ExplorerShowAllFiles);
        }
    }

    private void OnCloseWorkspaceRequested(object? sender, EventArgs e)
    {
        _viewModel.WorkspaceService.Close();
        sidebarHost.ExplorerPanel.Bind(_viewModel.WorkspaceService, _viewModel.RecentFilesService,
            App.State.Preferences.ExplorerShowAllFiles);
    }

    private void OnShowAllFilesChanged(object? sender, bool showAll)
    {
        App.State.Preferences.ExplorerShowAllFiles = showAll;
        var path = _viewModel.WorkspaceService.RootPath;
        if (!string.IsNullOrEmpty(path))
        {
            _viewModel.WorkspaceService.Open(path, showAll);
            sidebarHost.ExplorerPanel.Bind(_viewModel.WorkspaceService, _viewModel.RecentFilesService, showAll);
        }
    }

    private void OnExplorerSortChanged(object? sender, FileTreeSortMode mode)
    {
        _viewModel.WorkspaceService.SortMode = mode;
        App.State.Preferences.ExplorerSort = mode == FileTreeSortMode.DateModifiedDesc ? "date" : "name";
        // Re-aplica a ordenação reconstruindo a árvore.
        var path = _viewModel.WorkspaceService.RootPath;
        if (!string.IsNullOrEmpty(path))
        {
            _viewModel.WorkspaceService.Open(path, App.State.Preferences.ExplorerShowAllFiles);
            sidebarHost.ExplorerPanel.Bind(_viewModel.WorkspaceService, _viewModel.RecentFilesService,
                App.State.Preferences.ExplorerShowAllFiles);
        }
    }

    private static FileTreeSortMode ParseSortMode(string? value)
        => string.Equals(value, "date", StringComparison.OrdinalIgnoreCase)
            ? FileTreeSortMode.DateModifiedDesc
            : FileTreeSortMode.NameNatural;

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var state = App.State;
        if (WindowState == WindowState.Maximized)
        {
            state.Window.X = RestoreBounds.Left;
            state.Window.Y = RestoreBounds.Top;
            state.Window.Width = RestoreBounds.Width;
            state.Window.Height = RestoreBounds.Height;
        }
        else
        {
            state.Window.X = Left;
            state.Window.Y = Top;
            state.Window.Width = Width;
            state.Window.Height = Height;
        }
        state.Window.Maximized = WindowState == WindowState.Maximized;
        state.Sidebar.ActivePanel = _viewModel.SidebarActivePanel;
        state.Sidebar.Width = _viewModel.SidebarWidth;
        state.Sidebar.Collapsed = _viewModel.IsSidebarCollapsed;
        state.Sidebar.ActivityBarVisible = _viewModel.IsActivityBarVisible;
        state.Preferences.SyncScrollEnabled = _viewModel.SyncScrollEnabled;
        state.Preferences.TypewriterMode = _viewModel.TypewriterMode;
        state.Preferences.ViewMode = _isViewMode;
        state.LastWorkspace = _viewModel.WorkspaceService.RootPath;
        state.RecentFiles = _viewModel.RecentFilesService.Snapshot().ToList();
        state.OpenTabs = _viewModel.Tabs
            .Select(t => t.FilePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
        state.ActiveTab = _viewModel.SelectedTab?.FilePath;
        state.PinnedTabs = _viewModel.Tabs
            .Where(t => t.IsPinned && !string.IsNullOrEmpty(t.FilePath))
            .Select(t => t.FilePath)
            .ToList();

        try
        {
            App.StateService.Save(state);
        }
        catch
        {
            // Salvar estado não deve impedir o fechamento
        }
    }

    private bool _bypassDirtyCheckOnClose = false;

    /// <summary>
    /// Verifica se há abas com mudanças não salvas e pergunta ao usuário se quer prosseguir.
    /// Retorna true se OK pra fechar (sem dirty ou usuário aceitou descartar).
    /// </summary>
    private bool ConfirmCloseAllowingDirtyDiscard()
    {
        var dirtyTabs = _viewModel.Tabs.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Count == 0) return true;

        var result = MessageBox.Show(
            $"Existem {dirtyTabs.Count} arquivo(s) com alterações não salvas.\nDeseja sair mesmo assim?",
            "Sair",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Pergunta ao usuário se OK pra fechar (respeitando dirty tabs).
    /// Se retornar true, próxima chamada de Close() NÃO vai mostrar prompt de novo.
    /// Usado pelo AutoUpdateService antes de lançar o setup.
    /// </summary>
    public bool TryShutdownForUpdate()
    {
        if (!ConfirmCloseAllowingDirtyDiscard()) return false;
        _bypassDirtyCheckOnClose = true;
        return true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_bypassDirtyCheckOnClose && !ConfirmCloseAllowingDirtyDiscard())
        {
            e.Cancel = true;
        }

        if (!e.Cancel)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
        base.OnClosing(e);
    }

    // === Find / Replace ===

    private void OpenFindBar(bool showReplace)
    {
        var selected = textEditor.SelectedText;
        findBar.Open(string.IsNullOrEmpty(selected) ? _lastFindRequest?.Query : selected, showReplace);
    }

    private void CloseFindBar()
    {
        findBar.Visibility = Visibility.Collapsed;
        _findRenderer.Matches = Array.Empty<SearchMatch>();
        _findRenderer.ActiveMatchIndex = -1;
        textEditor.TextArea.TextView.InvalidateLayer(_findRenderer.Layer);
        textEditor.Focus();
    }

    private void OnFindRequested(object? sender, FindRequest req)
    {
        _lastFindRequest = req;
        var matches = _searchService.FindMatches(textEditor.Text, req.Query, req.CaseSensitive, req.UseRegex, req.WholeWord);
        _findRenderer.Matches = matches;
        _activeFindIndex = matches.Count > 0 ? 0 : -1;
        _findRenderer.ActiveMatchIndex = _activeFindIndex;
        findBar.SetMatchCount(_activeFindIndex, matches.Count);
        textEditor.TextArea.TextView.InvalidateLayer(_findRenderer.Layer);
        if (_activeFindIndex >= 0) ScrollToFindMatch();
    }

    private void MoveToFindMatch(int delta)
    {
        if (_findRenderer.Matches.Count == 0) return;
        _activeFindIndex = (_activeFindIndex + delta + _findRenderer.Matches.Count) % _findRenderer.Matches.Count;
        _findRenderer.ActiveMatchIndex = _activeFindIndex;
        findBar.SetMatchCount(_activeFindIndex, _findRenderer.Matches.Count);
        textEditor.TextArea.TextView.InvalidateLayer(_findRenderer.Layer);
        ScrollToFindMatch();
    }

    private void ScrollToFindMatch()
    {
        var match = _findRenderer.Matches[_activeFindIndex];
        var line = textEditor.Document.GetLineByOffset(match.Start);
        textEditor.ScrollToLine(line.LineNumber);
        textEditor.Select(match.Start, match.Length);
    }

    private void OnReplaceOne(object? sender, string replacement)
    {
        if (_activeFindIndex < 0 || _findRenderer.Matches.Count == 0) return;
        var match = _findRenderer.Matches[_activeFindIndex];
        textEditor.Document.Replace(match.Start, match.Length, replacement);
        if (_lastFindRequest != null) OnFindRequested(this, _lastFindRequest);
    }

    private void OpenQuickSwitcher()
    {
        var files = _viewModel.WorkspaceService.EnumerateMarkdownFiles().ToList();
        var recents = _viewModel.RecentFilesService.Snapshot();
        var openTabs = _viewModel.Tabs.Select(t => t.FilePath).ToList();
        var wsPath = _viewModel.WorkspaceService.RootPath;
        quickSwitcher.Open(files, recents, openTabs, wsPath);
    }

    private void OnReplaceAll(object? sender, string replacement)
    {
        if (_lastFindRequest == null || _findRenderer.Matches.Count == 0) return;
        if (_findRenderer.Matches.Count > 20)
        {
            var confirm = MessageBox.Show(
                $"Substituir {_findRenderer.Matches.Count} ocorrências?",
                "Confirmar substituição em massa",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }
        var matches = _findRenderer.Matches.ToList();
        textEditor.Document.BeginUpdate();
        try
        {
            for (int i = matches.Count - 1; i >= 0; i--)
                textEditor.Document.Replace(matches[i].Start, matches[i].Length, replacement);
        }
        finally
        {
            textEditor.Document.EndUpdate();
        }
        OnFindRequested(this, _lastFindRequest);
    }

    private void OnToggleZen(object sender, RoutedEventArgs e) => ToggleZenMode();

    private void OnToggleTypewriter(object sender, RoutedEventArgs e)
    {
        _viewModel.TypewriterMode = typewriterMenuItem.IsChecked;
        _typewriterManager!.Enabled = _viewModel.TypewriterMode;
    }

    private void ToggleZenMode()
    {
        if (!_isZenMode)
        {
            _savedWindowState = WindowState;
            _savedWindowStyle = WindowStyle;
            menuBar.Visibility = Visibility.Collapsed;
            toolbarBorder.Visibility = Visibility.Collapsed;
            tabStripBorder.Visibility = Visibility.Collapsed;
            statusBorder.Visibility = Visibility.Collapsed;
            activityBarCol.Width = new GridLength(0);
            sidebarCol.Width = new GridLength(0);
            sidebarSplitterCol.Width = new GridLength(0);
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isZenMode = true;
        }
        else
        {
            menuBar.Visibility = Visibility.Visible;
            toolbarBorder.Visibility = Visibility.Visible;
            tabStripBorder.Visibility = Visibility.Visible;
            statusBorder.Visibility = Visibility.Visible;
            activityBarCol.Width = _viewModel.IsActivityBarVisible ? GridLength.Auto : new GridLength(0);
            if (_viewModel.IsActivityBarVisible && !_viewModel.IsSidebarCollapsed)
            {
                sidebarCol.Width = new GridLength(_viewModel.SidebarWidth);
                sidebarSplitterCol.Width = new GridLength(4);
            }
            WindowStyle = _savedWindowStyle;
            WindowState = _savedWindowState;
            ApplyZenSolo(ZenSoloState.None);
            _zenSolo = ZenSoloState.None;
            _isZenMode = false;
        }
    }

    private void ToggleZenSolo()
    {
        if (!_isZenMode) return;
        _zenSolo = _zenSolo switch
        {
            ZenSoloState.None => ZenSoloState.EditorOnly,
            ZenSoloState.EditorOnly => ZenSoloState.PreviewOnly,
            ZenSoloState.PreviewOnly => ZenSoloState.None,
            _ => ZenSoloState.None
        };
        ApplyZenSolo(_zenSolo);
    }

    private void ApplyZenSolo(ZenSoloState state)
    {
        switch (state)
        {
            case ZenSoloState.None:
                editorColumn.Width = new GridLength(1, GridUnitType.Star);
                splitterColumn.Width = new GridLength(5);
                previewColumn.Width = new GridLength(1, GridUnitType.Star);
                editorBorder.Visibility = Visibility.Visible;
                break;
            case ZenSoloState.EditorOnly:
                editorColumn.Width = new GridLength(1, GridUnitType.Star);
                splitterColumn.Width = new GridLength(0);
                previewColumn.Width = new GridLength(0);
                editorBorder.Visibility = Visibility.Visible;
                break;
            case ZenSoloState.PreviewOnly:
                editorColumn.Width = new GridLength(0);
                splitterColumn.Width = new GridLength(0);
                previewColumn.Width = new GridLength(1, GridUnitType.Star);
                editorBorder.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private async void OnExportHtml(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTab == null)
        {
            MessageBox.Show("Nenhum documento aberto.", "Exportar HTML", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var defaultName = System.IO.Path.GetFileNameWithoutExtension(_viewModel.SelectedTab.FileName) + ".html";
        var dlg = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html",
            FileName = defaultName,
            Title = "Exportar como HTML"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            // HTML export sempre usa tema claro — destinado a compartilhar/imprimir
            await Task.Run(() => _exportService.ExportHtml(_viewModel.SelectedTab, dlg.FileName,
                darkTheme: false, convertMdLinksToHtml: false));
            _viewModel.StatusText = $"HTML exportado: {dlg.FileName}";
            ShowExportSuccess("HTML exportado com sucesso", dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao exportar HTML:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnExportPdf(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTab == null)
        {
            MessageBox.Show("Nenhum documento aberto.", "Exportar PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var defaultName = System.IO.Path.GetFileNameWithoutExtension(_viewModel.SelectedTab.FileName) + ".pdf";
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = defaultName,
            Title = "Exportar como PDF"
        };
        if (dlg.ShowDialog() != true) return;

        var tempHtml = _exportService.CreateTempHtmlForPrint(_viewModel.SelectedTab, darkTheme: false);
        try
        {
            _viewModel.StatusText = "Gerando PDF...";
            var success = await preview.PrintToPdfAsync(tempHtml, dlg.FileName);
            if (success)
            {
                _viewModel.StatusText = $"PDF exportado: {dlg.FileName}";
                ShowExportSuccess("PDF exportado com sucesso", dlg.FileName);
            }
            else
            {
                _viewModel.StatusText = "Falha ao gerar PDF";
                MessageBox.Show("Falha ao gerar PDF. Verifique o caminho de destino e tente novamente.",
                    "Exportar PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao exportar PDF:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            try { System.IO.File.Delete(tempHtml); } catch { }
            if (_viewModel.SelectedTab != null)
            {
                var html = _viewModel.RenderMarkdown(textEditor.Text, _viewModel.SelectedTab.Directory);
                preview.SetFullHtml(html);
                preview.SetDarkTheme(_isDarkTheme);
            }
        }
    }

    private void ShowExportSuccess(string title, string filePath)
    {
        var result = MessageBox.Show(
            $"{title}!\n\n{filePath}\n\nAbrir o arquivo agora?",
            "Exportar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível abrir o arquivo:\n{ex.Message}",
                    "Abrir arquivo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OnSearchMatchActivated(object? sender, MeuMarkdown.Controls.Panels.FileSearchMatchActivated e)
    {
        _viewModel.OpenFileByPath(e.FilePath);
        // Move caret to the line
        var line = Math.Max(1, Math.Min(e.LineNumber, textEditor.Document.LineCount));
        var offset = textEditor.Document.GetOffset(line, 1);
        textEditor.CaretOffset = offset;
        textEditor.ScrollToLine(line);
        textEditor.Focus();
    }
}

public class NullToCollapsedConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
