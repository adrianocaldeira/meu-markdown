using System.Windows;
using System.Windows.Threading;
using MeuMarkdown.Models;
using MeuMarkdown.Services;

namespace MeuMarkdown;

public partial class App : Application
{
    private SplashWindow? _splash;

    public static AppStateService StateService { get; private set; } = null!;
    public static AppState State { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        StateService = AppStateService.CreateDefault();
        State = StateService.Load();

        _splash = new SplashWindow();
        _splash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            _splash.Close();
            _splash = null;
        };
        timer.Start();
    }
}
