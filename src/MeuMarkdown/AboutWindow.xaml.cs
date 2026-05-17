using System.Windows;

namespace MeuMarkdown;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
