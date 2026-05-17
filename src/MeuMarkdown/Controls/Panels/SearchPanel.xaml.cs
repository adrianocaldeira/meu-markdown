using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeuMarkdown.Services;

namespace MeuMarkdown.Controls.Panels;

public partial class SearchPanel : UserControl
{
    private WorkspaceService? _workspace;
    private WorkspaceSearchService? _searchService;

    public event EventHandler<FileSearchMatchActivated>? MatchActivated;

    public SearchPanel()
    {
        InitializeComponent();
    }

    public void Bind(WorkspaceService workspace, WorkspaceSearchService searchService)
    {
        _workspace = workspace;
        _searchService = searchService;
    }

    public void FocusQueryBox() => QueryBox.Focus();

    private void OnOptionChanged(object sender, RoutedEventArgs e) => RunSearch();

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            QueryBox.Text = "";
            ResultsTree.ItemsSource = null;
            StatusText.Text = "";
            e.Handled = true;
        }
    }

    private void RunSearch()
    {
        if (_workspace == null || _searchService == null) return;
        var query = QueryBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            ResultsTree.ItemsSource = null;
            StatusText.Text = "";
            return;
        }
        if (_workspace.RootPath == null)
        {
            StatusText.Text = "Abra um workspace pra buscar";
            ResultsTree.ItemsSource = null;
            return;
        }

        var files = _workspace.EnumerateMarkdownFiles();
        var results = _searchService.Search(
            files, query,
            CaseToggle.IsChecked == true,
            RegexToggle.IsChecked == true,
            WholeWordToggle.IsChecked == true);

        ResultsTree.ItemsSource = results;
        var totalMatches = results.Sum(r => r.Matches.Count);
        StatusText.Text = results.Count == 0
            ? "Nenhum resultado"
            : $"{totalMatches} resultados em {results.Count} arquivo{(results.Count == 1 ? "" : "s")}";
    }

    private void OnResultClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsTree.SelectedItem is FileSearchMatch match)
        {
            // Encontra o FilePath do file-group pai
            foreach (var resObj in ResultsTree.Items)
            {
                if (resObj is FileSearchResult res && res.Matches.Contains(match))
                {
                    MatchActivated?.Invoke(this, new FileSearchMatchActivated(res.FilePath, match.LineNumber));
                    return;
                }
            }
        }
    }
}

public record FileSearchMatchActivated(string FilePath, int LineNumber);
