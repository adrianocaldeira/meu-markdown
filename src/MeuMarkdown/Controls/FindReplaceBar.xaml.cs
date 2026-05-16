using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeuMarkdown.Controls;

public partial class FindReplaceBar : UserControl
{
    public event EventHandler<FindRequest>? FindRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PrevRequested;
    public event EventHandler<string>? ReplaceOneRequested;
    public event EventHandler<string>? ReplaceAllRequested;

    public FindReplaceBar()
    {
        InitializeComponent();
    }

    public void Open(string? initialQuery, bool showReplace)
    {
        if (!string.IsNullOrEmpty(initialQuery))
            FindBox.Text = initialQuery;
        ReplaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
        Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    public void SetMatchCount(int currentIndex, int total)
    {
        MatchCountText.Text = total == 0 ? "0 resultados" : $"{currentIndex + 1}/{total}";
    }

    public string CurrentReplaceText => ReplaceBox.Text;

    private void EmitFind()
    {
        FindRequested?.Invoke(this, new FindRequest(
            FindBox.Text,
            CaseSensitiveToggle.IsChecked == true,
            RegexToggle.IsChecked == true,
            WholeWordToggle.IsChecked == true));
    }

    private void OnFindTextChanged(object sender, TextChangedEventArgs e) => EmitFind();
    private void OnOptionChanged(object sender, RoutedEventArgs e) => EmitFind();

    private void OnFindKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NextRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnNext(object sender, RoutedEventArgs e) => NextRequested?.Invoke(this, EventArgs.Empty);
    private void OnPrev(object sender, RoutedEventArgs e) => PrevRequested?.Invoke(this, EventArgs.Empty);
    private void OnReplaceOne(object sender, RoutedEventArgs e) => ReplaceOneRequested?.Invoke(this, ReplaceBox.Text);
    private void OnReplaceAll(object sender, RoutedEventArgs e) => ReplaceAllRequested?.Invoke(this, ReplaceBox.Text);
}

public record FindRequest(string Query, bool CaseSensitive, bool UseRegex, bool WholeWord);
