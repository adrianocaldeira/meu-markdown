using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeuMarkdown.Services;

namespace MeuMarkdown.Controls;

public partial class QuickSwitcherPopup : UserControl
{
    private List<string> _allFiles = new();
    private List<string> _recentFiles = new();
    private List<string> _openTabPaths = new();
    private string? _workspacePath;

    public event EventHandler<string>? FileSelected;

    public QuickSwitcherPopup()
    {
        InitializeComponent();
    }

    public void Open(IEnumerable<string> allFiles, IEnumerable<string> recentFiles, IEnumerable<string> openTabPaths, string? workspacePath)
    {
        _allFiles = allFiles.ToList();
        _recentFiles = recentFiles.ToList();
        _openTabPaths = openTabPaths.ToList();
        _workspacePath = workspacePath;
        QueryBox.Text = "";
        Visibility = Visibility.Visible;
        UpdateResults();
        QueryBox.Focus();
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
    }

    private void UpdateResults()
    {
        var query = QueryBox.Text?.Trim() ?? "";
        var candidates = _allFiles.Count > 0 ? _allFiles : _recentFiles;

        var scored = candidates
            .Select(p => new
            {
                Path = p,
                Score = ComputeScore(p, query)
            })
            .Where(x => string.IsNullOrEmpty(query) || x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(50)
            .Select(x => new QuickSwitcherItem
            {
                Name = Path.GetFileName(x.Path),
                RelativeDir = ComputeRelativeDir(x.Path),
                FullPath = x.Path
            })
            .ToList();

        Results.ItemsSource = scored;
        if (scored.Count > 0)
            Results.SelectedIndex = 0;
    }

    private int ComputeScore(string path, string query)
    {
        var fileName = Path.GetFileName(path);
        var score = FuzzyMatcher.Score(query, fileName);

        if (_recentFiles.Take(10).Any(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase)))
            score += 200;
        if (_openTabPaths.Any(t => string.Equals(t, path, StringComparison.OrdinalIgnoreCase)))
            score += 30;

        return score;
    }

    private string ComputeRelativeDir(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        if (!string.IsNullOrEmpty(_workspacePath))
        {
            var wsWithSep = _workspacePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (dir.Equals(_workspacePath, StringComparison.OrdinalIgnoreCase))
                return ".";
            if (dir.StartsWith(wsWithSep, StringComparison.OrdinalIgnoreCase))
                return dir.Substring(wsWithSep.Length);
        }
        return dir;
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e) => UpdateResults();

    private void OnQueryKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (Results.Items.Count > 0)
                {
                    Results.SelectedIndex = Math.Min(Results.SelectedIndex + 1, Results.Items.Count - 1);
                    Results.ScrollIntoView(Results.SelectedItem);
                }
                e.Handled = true;
                break;
            case Key.Up:
                if (Results.Items.Count > 0)
                {
                    Results.SelectedIndex = Math.Max(Results.SelectedIndex - 1, 0);
                    Results.ScrollIntoView(Results.SelectedItem);
                }
                e.Handled = true;
                break;
            case Key.Enter:
                ActivateSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnResultKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ActivateSelected();
    }

    private void ActivateSelected()
    {
        if (Results.SelectedItem is QuickSwitcherItem item)
        {
            FileSelected?.Invoke(this, item.FullPath);
            Close();
        }
    }
}

public class QuickSwitcherItem
{
    public string Name { get; set; } = "";
    public string RelativeDir { get; set; } = "";
    public string FullPath { get; set; } = "";
}
