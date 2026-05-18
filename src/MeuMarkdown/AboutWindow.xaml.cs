using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

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

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // ignora — usuário pode copiar a URL se quiser
        }
        e.Handled = true;
    }
}
