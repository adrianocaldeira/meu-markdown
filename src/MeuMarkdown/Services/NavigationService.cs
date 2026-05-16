using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class NavigationService
{
    private readonly FileService _fileService;
    private readonly NavigationHistory _history = new();

    public NavigationHistory History => _history;

    public event Action<string>? NavigationRequested;

    public NavigationService(FileService fileService)
    {
        _fileService = fileService;
    }

    public void NavigateTo(string filePath, string currentDirectory)
    {
        var resolvedPath = _fileService.ResolvePath(filePath, currentDirectory);
        if (!_fileService.FileExists(resolvedPath)) return;

        _history.Navigate(resolvedPath);
        NavigationRequested?.Invoke(resolvedPath);
    }

    public void GoBack()
    {
        var path = _history.GoBack();
        if (path != null)
            NavigationRequested?.Invoke(path);
    }

    public void GoForward()
    {
        var path = _history.GoForward();
        if (path != null)
            NavigationRequested?.Invoke(path);
    }
}
