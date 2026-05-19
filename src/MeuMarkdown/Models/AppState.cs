namespace MeuMarkdown.Models;

public class AppState
{
    public WindowStateInfo Window { get; set; } = new();
    public SidebarStateInfo Sidebar { get; set; } = new();
    public string? LastWorkspace { get; set; }
    public List<string> OpenTabs { get; set; } = new();
    public string? ActiveTab { get; set; }
    public List<string> PinnedTabs { get; set; } = new();
    public List<string> RecentFiles { get; set; } = new();
    public PreferencesInfo Preferences { get; set; } = new();
}

public class WindowStateInfo
{
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 700;
    public bool Maximized { get; set; }
}

public class SidebarStateInfo
{
    public string ActivePanel { get; set; } = "Explorer";
    public double Width { get; set; } = 280;
    public bool Collapsed { get; set; }
    public bool ActivityBarVisible { get; set; } = true;
}

public class PreferencesInfo
{
    public bool SyncScrollEnabled { get; set; }
    public bool TypewriterMode { get; set; }
    public string? DarkThemeOverride { get; set; }
    public bool ExplorerShowAllFiles { get; set; }
    public bool ViewMode { get; set; }
    public string? DismissedUpdateVersion { get; set; }
    /// <summary>"name" (natural sort) | "date" (data modificação descendente). Default: "name".</summary>
    public string? ExplorerSort { get; set; }
}
