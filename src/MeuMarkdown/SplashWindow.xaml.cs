using System.Windows;
using System.Windows.Threading;

namespace MeuMarkdown;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _timer;
    private int _step;

    private readonly string[] _messages =
    [
        "Iniciando...",
        "Carregando componentes...",
        "Configurando editor...",
        "Preparando preview...",
        "Pronto!"
    ];

    public SplashWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_step < _messages.Length)
        {
            statusText.Text = _messages[_step];
            _step++;
        }
        else
        {
            _timer.Stop();
        }
    }
}
