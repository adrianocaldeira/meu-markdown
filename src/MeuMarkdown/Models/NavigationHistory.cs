namespace MeuMarkdown.Models;

public class NavigationHistory
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private string? _current;

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public void Navigate(string filePath)
    {
        if (_current != null)
            _backStack.Push(_current);
        _current = filePath;
        _forwardStack.Clear();
    }

    public string? GoBack()
    {
        if (!CanGoBack) return null;
        if (_current != null)
            _forwardStack.Push(_current);
        _current = _backStack.Pop();
        return _current;
    }

    public string? GoForward()
    {
        if (!CanGoForward) return null;
        if (_current != null)
            _backStack.Push(_current);
        _current = _forwardStack.Pop();
        return _current;
    }
}
