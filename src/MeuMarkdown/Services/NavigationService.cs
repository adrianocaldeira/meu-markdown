using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class NavigationService
{
    private readonly FileService _fileService;
    private readonly NavigationHistory _history = new();

    public NavigationHistory History => _history;

    /// <summary>
    /// Disparado quando uma navegação é solicitada. O segundo parâmetro é o
    /// fragment (heading sem `#`) opcional — null quando o link não tem âncora.
    /// </summary>
    public event Action<string, string?>? NavigationRequested;

    public NavigationService(FileService fileService)
    {
        _fileService = fileService;
    }

    public void NavigateTo(string filePath, string currentDirectory, string? fragment = null)
    {
        var resolvedPath = _fileService.ResolvePath(filePath, currentDirectory);
        if (!_fileService.FileExists(resolvedPath)) return;

        _history.Navigate(resolvedPath);
        NavigationRequested?.Invoke(resolvedPath, fragment);
    }

    public void GoBack()
    {
        var path = _history.GoBack();
        if (path != null)
            NavigationRequested?.Invoke(path, null);
    }

    public void GoForward()
    {
        var path = _history.GoForward();
        if (path != null)
            NavigationRequested?.Invoke(path, null);
    }
}
