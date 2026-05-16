using System.Windows;
using System.Windows.Controls;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Controls.Panels;

public partial class SettingsPanel : UserControl
{
    private MainViewModel? _viewModel;

    public event EventHandler<bool>? SyncScrollChanged;
    public event EventHandler<bool>? TypewriterChanged;
    public event EventHandler<bool>? ShowAllFilesChanged;
    public event EventHandler? ChangeWorkspaceRequested;
    public event EventHandler? ClearRecentsRequested;

    public SettingsPanel()
    {
        InitializeComponent();
    }

    public void Bind(MainViewModel viewModel, bool showAllFiles)
    {
        _viewModel = viewModel;
        SyncScrollCheck.IsChecked = viewModel.SyncScrollEnabled;
        TypewriterCheck.IsChecked = viewModel.TypewriterMode;
        ShowAllFilesCheck.IsChecked = showAllFiles;
        UpdateWorkspaceText();
        UpdateRecentCount();

        viewModel.RecentFilesService.Items.CollectionChanged += (_, _) => Dispatcher.Invoke(UpdateRecentCount);
    }

    public void RefreshWorkspaceAndRecents()
    {
        UpdateWorkspaceText();
        UpdateRecentCount();
    }

    private void UpdateWorkspaceText()
    {
        WorkspaceText.Text = _viewModel?.WorkspaceService.RootPath ?? "Nenhum workspace";
    }

    private void UpdateRecentCount()
    {
        var count = _viewModel?.RecentFilesService.Items.Count ?? 0;
        RecentCountText.Text = count == 1 ? "1 arquivo" : $"{count} arquivos";
    }

    private void OnSyncScrollChanged(object sender, RoutedEventArgs e)
        => SyncScrollChanged?.Invoke(this, SyncScrollCheck.IsChecked == true);

    private void OnTypewriterChanged(object sender, RoutedEventArgs e)
        => TypewriterChanged?.Invoke(this, TypewriterCheck.IsChecked == true);

    private void OnShowAllFilesChanged(object sender, RoutedEventArgs e)
        => ShowAllFilesChanged?.Invoke(this, ShowAllFilesCheck.IsChecked == true);

    private void OnChangeWorkspace(object sender, RoutedEventArgs e)
        => ChangeWorkspaceRequested?.Invoke(this, EventArgs.Empty);

    private void OnClearRecents(object sender, RoutedEventArgs e)
        => ClearRecentsRequested?.Invoke(this, EventArgs.Empty);
}
