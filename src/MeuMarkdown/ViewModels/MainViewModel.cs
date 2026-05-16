using System.Collections.ObjectModel;
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

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _statusText = "Pronto";

    public NavigationService Navigation => _navigationService;

    public MainViewModel()
    {
        _navigationService = new NavigationService(_fileService);
        _navigationService.NavigationRequested += OnNavigationRequested;
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
        // Check if already open
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

        if (tab.IsDirty)
        {
            var result = MessageBox.Show(
                $"O arquivo '{tab.FileName}' tem alterações não salvas.\nDeseja salvar antes de fechar?",
                "Salvar alterações",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes)
            {
                _fileService.SaveFile(tab.GetDocument());
                tab.MarkSaved();
            }
        }

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (Tabs.Count > 0)
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
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

    private void OnNavigationRequested(string filePath)
    {
        OpenFileByPath(filePath);
    }
}
