using System.Windows;
using System.Windows.Controls;

namespace MeuMarkdown.Controls;

/// <summary>
/// SidebarHost UserControl - Container que faz swap de visibilidade entre os 4 painéis da sidebar (Explorer, Outline, Search, Settings).
/// </summary>
public partial class SidebarHost : UserControl
{
    public SidebarHost()
    {
        InitializeComponent();
    }

    public Controls.Panels.OutlinePanel OutlinePanel => OutlinePanelInstance;
    public Controls.Panels.ExplorerPanel ExplorerPanel => ExplorerPanelInstance;
    public Controls.Panels.SettingsPanel SettingsPanel => SettingsPanelInstance;

    /// <summary>
    /// Exibe o painel especificado escondendo os demais.
    /// </summary>
    public void ShowPanel(string panelName)
    {
        ExplorerPanelInstance.Visibility = panelName == "Explorer" ? Visibility.Visible : Visibility.Collapsed;
        OutlinePanelInstance.Visibility = panelName == "Outline" ? Visibility.Visible : Visibility.Collapsed;
        SearchPanelInstance.Visibility = panelName == "Search" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanelInstance.Visibility = panelName == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }
}
