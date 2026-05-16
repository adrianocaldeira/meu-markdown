using System.Windows.Controls;

namespace MeuMarkdown.Controls;

/// <summary>
/// ActivityBar UserControl - Barra de atividades vertical com RadioButtons para seleção de painéis (Explorer, Outline, Buscar, Configurações).
/// </summary>
public partial class ActivityBar : UserControl
{
    public event EventHandler<string>? PanelSelected;

    public ActivityBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Define qual painel está ativo alterando a seleção de RadioButton.
    /// </summary>
    public void SetActivePanel(string panelName)
    {
        var btn = panelName switch
        {
            "Explorer" => ExplorerBtn,
            "Outline" => OutlineBtn,
            "Search" => SearchBtn,
            "Settings" => SettingsBtn,
            _ => ExplorerBtn
        };
        btn.IsChecked = true;
    }

    private void OnPanelChecked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string panelName)
            PanelSelected?.Invoke(this, panelName);
    }
}
