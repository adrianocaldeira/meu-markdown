using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuMarkdown.Models;
using MeuMarkdown.Services;
using Microsoft.Win32;
using System.Windows;

namespace MeuMarkdown.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FileService _fileService = new();
    private readonly MarkdownService _markdownService = new();
    private readonly NavigationService _navigationService;
    private readonly WorkspaceService _workspaceService = new();
    private readonly RecentFilesService _recentFilesService = new();
    private readonly WorkspaceSearchService _workspaceSearchService;

    public WorkspaceService WorkspaceService => _workspaceService;
    public RecentFilesService RecentFilesService => _recentFilesService;
    public WorkspaceSearchService WorkspaceSearchService => _workspaceSearchService;

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _statusText = "Pronto";

    [ObservableProperty]
    private string _sidebarActivePanel = "Explorer";

    [ObservableProperty]
    private double _sidebarWidth = 280;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private bool _isActivityBarVisible = true;

    [ObservableProperty]
    private bool _syncScrollEnabled;

    [ObservableProperty]
    private bool _typewriterMode;

    [ObservableProperty]
    private string? _pendingScrollFragment;

    public NavigationService Navigation => _navigationService;

    public MarkdownService MarkdownService => _markdownService;

    public MainViewModel()
    {
        _navigationService = new NavigationService(_fileService);
        _navigationService.NavigationRequested += OnNavigationRequested;
        _workspaceSearchService = new WorkspaceSearchService(new EditorSearchService());

        _markdownService.ConfigureWikiLinkResolver(
            resolver: (target, currentDir) => _workspaceService.ResolveWikiLink(target, currentDir),
            currentFileDir: () => SelectedTab?.Directory);
    }

    [RelayCommand]
    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Arquivos Markdown (*.md;*.markdown)|*.md;*.markdown|Todos os arquivos (*.*)|*.*",
            Multiselect = true,
            Title = "Abrir arquivo Markdown"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
                OpenFileByPath(filePath);
        }
    }

    public void OpenFileByPath(string filePath)
    {
        var existing = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedTab = existing;
            return;
        }

        try
        {
            var document = _fileService.OpenFile(filePath);
            var tab = new DocumentTabViewModel(document);
            Tabs.Add(tab);
            SelectedTab = tab;
            StatusText = filePath;

            _recentFilesService.Add(filePath);

            if (_workspaceService.Root == null)
            {
                var parentDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(parentDir))
                    _workspaceService.Open(parentDir, showAllFiles: false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao abrir arquivo:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedTab == null) return;
        try
        {
            _fileService.SaveFile(SelectedTab.GetDocument());
            SelectedTab.MarkSaved();
            StatusText = $"Salvo: {SelectedTab.FilePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel? tab)
    {
        tab ??= SelectedTab;
        if (tab == null) return;
        TryCloseTab(tab);
    }

    /// <summary>
    /// Fecha uma aba lidando com o prompt de save de dirty. Retorna true se a aba foi
    /// fechada, false se o usuário cancelou. Usado pelos comandos de "fechar várias".
    /// </summary>
    private bool TryCloseTab(DocumentTabViewModel tab)
    {
        if (tab.IsDirty)
        {
            var result = MessageBox.Show(
                $"O arquivo '{tab.FileName}' tem alterações não salvas.\nDeseja salvar antes de fechar?",
                "Salvar alterações",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.Yes)
            {
                _fileService.SaveFile(tab.GetDocument());
                tab.MarkSaved();
            }
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (Tabs.Count > 0 && SelectedTab == null)
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        return true;
    }

    [RelayCommand]
    private void TogglePinTab(DocumentTabViewModel? tab)
    {
        if (tab == null) return;
        tab.IsPinned = !tab.IsPinned;
    }

    [RelayCommand]
    private void CloseOtherTabs(DocumentTabViewModel? tab)
    {
        if (tab == null) return;
        CloseBulk(t => t != tab);
    }

    [RelayCommand]
    private void CloseUnpinnedTabs(DocumentTabViewModel? tab)
    {
        CloseBulk(t => !t.IsPinned);
    }

    [RelayCommand]
    private void CloseLeftTabs(DocumentTabViewModel? tab)
    {
        if (tab == null) return;
        var pivot = Tabs.IndexOf(tab);
        if (pivot < 0) return;
        var toClose = Tabs.Take(pivot).ToList();
        CloseList(toClose);
    }

    [RelayCommand]
    private void CloseRightTabs(DocumentTabViewModel? tab)
    {
        if (tab == null) return;
        var pivot = Tabs.IndexOf(tab);
        if (pivot < 0) return;
        var toClose = Tabs.Skip(pivot + 1).ToList();
        CloseList(toClose);
    }

    [RelayCommand]
    private void CloseUnchangedTabs(DocumentTabViewModel? tab)
    {
        CloseBulk(t => !t.IsDirty);
    }

    private void CloseBulk(Func<DocumentTabViewModel, bool> predicate)
    {
        CloseList(Tabs.Where(predicate).ToList());
    }

    private void CloseList(List<DocumentTabViewModel> targets)
    {
        // Snapshot do SelectedTab pra restaurar se ainda existir.
        var keepSelected = SelectedTab;
        foreach (var t in targets)
        {
            // Se o usuário cancelar o save de uma aba dirty, paramos o lote ali.
            if (!TryCloseTab(t)) break;
        }
        if (keepSelected != null && Tabs.Contains(keepSelected))
            SelectedTab = keepSelected;
        else if (Tabs.Count > 0 && SelectedTab == null)
            SelectedTab = Tabs[0];
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    [RelayCommand]
    private void GoForward() => _navigationService.GoForward();

    public string RenderMarkdown(string content, string baseDirectory)
    {
        return _markdownService.ConvertToHtml(content, baseDirectory);
    }

    public string RenderMarkdownFragment(string content, string baseDirectory)
    {
        return _markdownService.ConvertToHtmlFragment(content, baseDirectory);
    }

    private void OnNavigationRequested(string filePath, string? fragment)
    {
        OpenFileByPath(filePath);
        PendingScrollFragment = fragment;
    }

    /// <summary>
    /// Fecha todas as abas cujo arquivo não está dentro do newWorkspaceRoot.
    /// Abas dirty pedem confirmação via TryCloseTab; se user cancela aquele save,
    /// o lote para, mas as outras abas já processadas continuam fechadas.
    /// </summary>
    public void CloseTabsOutsideWorkspace(string? newWorkspaceRoot)
    {
        if (string.IsNullOrEmpty(newWorkspaceRoot)) return;

        var tabsToClose = Tabs
            .Where(t => !IsUnderRoot(t.FilePath, newWorkspaceRoot))
            .ToList();

        foreach (var tab in tabsToClose)
        {
            if (!TryCloseTab(tab))
            {
                // User cancelou na prompt de save — para o lote.
                break;
            }
        }
    }

    private static bool IsUnderRoot(string filePath, string root)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(root)) return false;
        try
        {
            var normalizedFile = Path.GetFullPath(filePath);
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
