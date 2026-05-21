using System.Windows;
using System.Windows.Input;

namespace MeuMarkdown.Controls;

public partial class InputDialog : Window
{
    public string Result { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Result = InputBox.Text;
            DialogResult = true;
        }
    }

    /// <summary>
    /// Mostra o dialog modal e retorna o texto digitado, ou null se cancelado.
    /// </summary>
    public static string? Show(Window owner, string title, string prompt, string defaultValue)
    {
        var dlg = new InputDialog(title, prompt, defaultValue) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }
}
