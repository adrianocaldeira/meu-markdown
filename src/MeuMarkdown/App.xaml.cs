using System.Windows;
using System.Windows.Threading;

namespace MeuMarkdown;

public partial class App : Application
{
    private SplashWindow? _splash;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Show splash screen
        _splash = new SplashWindow();
        _splash.Show();

        // Load main window after a short delay
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            // Fade out and close splash
            _splash.Close();
            _splash = null;
        };
        timer.Start();
    }
}
