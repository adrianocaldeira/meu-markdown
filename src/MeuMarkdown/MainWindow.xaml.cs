using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MeuMarkdown.ViewModels;
using MeuMarkdown.Services;
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

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _debounceTimer;
    private bool _isDarkTheme;
    private bool _isViewMode;
    private bool _suppressEditorUpdate;
    private GridLength _savedEditorWidth = new(1, GridUnitType.Star);

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

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

        // F5 / F6 key bindings
        InputBindings.Add(new KeyBinding(ToggleViewModeCommand, Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(ToggleDarkThemeCommand, Key.F6, ModifierKeys.None));

        LoadMarkdownSyntaxHighlighting();

        _isDarkTheme = IsOsDarkMode();
        ApplyTheme();
        darkThemeMenuItem.IsChecked = _isDarkTheme;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Open files passed as command-line arguments
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]) &&
                (args[i].EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                 args[i].EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
                _viewModel.OpenFileByPath(args[i]);
        }
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
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.SelectedTab == null)
        {
            textEditor.Text = string.Empty;
            textEditor.IsEnabled = false;
            return;
        }

        textEditor.IsEnabled = true;
        _suppressEditorUpdate = true;
        textEditor.Text = _viewModel.SelectedTab.Content;
        _suppressEditorUpdate = false;
        UpdatePreview();
    }

    private void OnPreviewLinkClicked(string relativePath)
    {
        if (_viewModel.SelectedTab == null) return;
        _viewModel.Navigation.NavigateTo(relativePath, _viewModel.SelectedTab.Directory);
    }

    private void OnExternalLinkClicked(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
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
        var bgMain = _isDarkTheme ? Color.FromRgb(17, 24, 39) : Color.FromRgb(248, 249, 250);
        var bgSurface = _isDarkTheme ? Color.FromRgb(31, 41, 55) : Colors.White;
        var bgToolbar = _isDarkTheme ? Color.FromRgb(31, 41, 55) : Colors.White;
        var fgMain = _isDarkTheme ? Color.FromRgb(229, 231, 235) : Color.FromRgb(17, 24, 39);
        var borderColor = _isDarkTheme ? Color.FromRgb(55, 65, 81) : Color.FromRgb(229, 231, 235);
        var statusBg = _isDarkTheme ? Color.FromRgb(31, 41, 55) : Color.FromRgb(241, 245, 249);
        var menuPopupBg = _isDarkTheme ? Color.FromRgb(40, 50, 65) : Color.FromRgb(249, 250, 251);

        rootGrid.Background = new SolidColorBrush(bgMain);
        menuBar.Background = new SolidColorBrush(bgToolbar);
        menuBar.Foreground = new SolidColorBrush(fgMain);
        toolbarBorder.Background = new SolidColorBrush(bgToolbar);
        toolbarBorder.BorderBrush = new SolidColorBrush(borderColor);
        editorBorder.Background = new SolidColorBrush(bgSurface);
        editorBorder.BorderBrush = new SolidColorBrush(borderColor);
        textEditor.Background = new SolidColorBrush(bgSurface);
        textEditor.Foreground = new SolidColorBrush(fgMain);
        statusBorder.Background = new SolidColorBrush(statusBg);
        statusBorder.BorderBrush = new SolidColorBrush(borderColor);

        // Apply theme to menu items (dropdown popups)
        var menuItemStyle = new Style(typeof(MenuItem));
        menuItemStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(menuPopupBg)));
        menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(fgMain)));
        menuItemStyle.Setters.Add(new Setter(MenuItem.BorderBrushProperty, new SolidColorBrush(borderColor)));

        // Highlight on hover
        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        var hoverBg = _isDarkTheme ? Color.FromRgb(55, 65, 81) : Color.FromRgb(229, 231, 235);
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(hoverBg)));
        menuItemStyle.Triggers.Add(hoverTrigger);

        // Apply to all menu items
        menuBar.Resources[typeof(MenuItem)] = menuItemStyle;

        // Separator: template explícito para garantir visibilidade em qualquer tema
        var sepFactory = new FrameworkElementFactory(typeof(Border));
        sepFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0, 1, 0, 0));
        sepFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(borderColor));
        sepFactory.SetValue(Border.MarginProperty, new Thickness(8, 3, 8, 3));
        var sepTemplate = new ControlTemplate(typeof(Separator));
        sepTemplate.VisualTree = sepFactory;
        var sepStyle = new Style(typeof(Separator));
        sepStyle.Setters.Add(new Setter(Separator.TemplateProperty, sepTemplate));
        menuBar.Resources[typeof(Separator)] = sepStyle;

        // Toolbar buttons and separators
        var fgToolbar = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(209, 213, 219) : Color.FromRgb(55, 65, 81));
        var sepBrush = new SolidColorBrush(borderColor);
        foreach (UIElement child in toolbarPanel.Children)
        {
            if (child is System.Windows.Controls.Button btn) btn.Foreground = fgToolbar;
            else if (child is System.Windows.Controls.Primitives.ToggleButton tb) tb.Foreground = fgToolbar;
            else if (child is Border sep) sep.Background = sepBrush;
        }

        // Tab strip
        tabStripBorder.Background = new SolidColorBrush(bgSurface);
        tabStripBorder.BorderBrush = new SolidColorBrush(borderColor);
        tabControl.Foreground = new SolidColorBrush(fgMain);

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

    private void OnSetAsDefault(object sender, RoutedEventArgs e)
    {
        try
        {
            new FileAssociationService().Register();
            MessageBox.Show(
                "MeuMarkdown definido como padrão para arquivos .md.\n\nAo dar duplo clique em qualquer arquivo .md no Explorer, ele abrirá aqui.",
                "Associação registrada",
                MessageBoxButton.OK, MessageBoxImage.Information);
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

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var dirtyTabs = _viewModel.Tabs.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Count > 0)
        {
            var result = MessageBox.Show(
                $"Existem {dirtyTabs.Count} arquivo(s) com alterações não salvas.\nDeseja sair mesmo assim?",
                "Sair",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
                e.Cancel = true;
        }
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnClosing(e);
    }
}
