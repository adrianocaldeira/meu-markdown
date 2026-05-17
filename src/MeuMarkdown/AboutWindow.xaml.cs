using System.Windows;

namespace MeuMarkdown;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        versionText.Text = "v" + VersionInfo.Current;
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
