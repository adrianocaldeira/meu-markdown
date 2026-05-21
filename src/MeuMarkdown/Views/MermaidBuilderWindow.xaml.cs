using System.Windows;
using System.Windows.Controls;
using MeuMarkdown.Controls;
using MeuMarkdown.Controls.MermaidBuilderPanels;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Views;

public partial class MermaidBuilderWindow : Window
{
    private readonly MermaidBuilderViewModel _vm = new();
    public string? ResultMermaidCode { get; private set; }

    public MermaidBuilderWindow(string assetsDir, bool darkTheme)
    {
        InitializeComponent();
        DataContext = _vm;

        Preview.SetAssetsDirectory(assetsDir);
        Preview.SetDarkTheme(darkTheme);
        Preview.MermaidError += msg => Dispatcher.Invoke(() => _vm.SetRenderError(msg));

        _vm.RenderRequested += code => Dispatcher.Invoke(() => _ = Preview.RenderAsync(code));

        TypeBox.SelectedIndex = 0;
        SwapPanel(BuilderDiagramKind.Flowchart);
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeBox.SelectedItem is not ComboBoxItem cbi) return;
        var tag = cbi.Tag as string;
        if (!Enum.TryParse<BuilderDiagramKind>(tag, out var kind)) return;
        if (kind == _vm.DiagramKind) return;

        if (_vm.IsModified)
        {
            var ok = MessageDialog.Confirm(this, "Trocar tipo",
                "Trocar de tipo descarta o que você fez. Continuar?",
                MessageDialogKind.Warning);
            if (!ok)
            {
                TypeBox.SelectionChanged -= OnTypeChanged;
                TypeBox.SelectedIndex = _vm.DiagramKind == BuilderDiagramKind.Flowchart ? 0 : 1;
                TypeBox.SelectionChanged += OnTypeChanged;
                return;
            }
        }

        _vm.DiagramKind = kind;
        SwapPanel(kind);
    }

    private void SwapPanel(BuilderDiagramKind kind)
    {
        FrameworkElement panel = kind switch
        {
            BuilderDiagramKind.Flowchart => new FlowchartBuilderPanel(),
            BuilderDiagramKind.Sequence => new SequenceBuilderPanel(),
            _ => new FlowchartBuilderPanel(),
        };
        panel.DataContext = _vm;
        PanelHost.Content = panel;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        ResultMermaidCode = _vm.GeneratedCode;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (_vm.IsModified)
        {
            var ok = MessageDialog.Confirm(this, "Descartar diagrama",
                "Descartar o diagrama em edição?", MessageDialogKind.Question);
            if (!ok) return;
        }
        DialogResult = false;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult == true) return;
        if (_vm.IsModified && ResultMermaidCode == null)
        {
            var ok = MessageDialog.Confirm(this, "Descartar diagrama",
                "Descartar o diagrama em edição?", MessageDialogKind.Question);
            if (!ok) e.Cancel = true;
        }
    }

    private void OnCopyCode(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_vm.GeneratedCode); }
        catch { }
    }
}
