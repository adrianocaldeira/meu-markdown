using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MeuMarkdown.Models;

namespace MeuMarkdown.ViewModels;

public partial class DocumentTabViewModel : ObservableObject
{
    private readonly MarkdownDocument _document;
    private bool _suppressDirty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _htmlPreview = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private int _charCount;

    [ObservableProperty]
    private int _readingTimeMinutes;

    public void UpdateMetrics(int words, int chars)
    {
        WordCount = words;
        CharCount = chars;
        ReadingTimeMinutes = Math.Max(1, words / 200);
    }

    public ObservableCollection<Heading> Headings { get; } = new();

    [ObservableProperty]
    private Heading? _currentHeading;

    public void UpdateHeadings(IReadOnlyList<Heading> headings)
    {
        Headings.Clear();
        foreach (var h in headings)
            Headings.Add(h);
    }

    public string FilePath => _document.FilePath;
    public string FileName => _document.FileName;
    public string Directory => _document.Directory;

    public string TabTitle => IsDirty ? $"{FileName} *" : FileName;

    public DocumentTabViewModel(MarkdownDocument document)
    {
        _document = document;
        _content = document.Content;
    }

    partial void OnContentChanged(string value)
    {
        _document.Content = value;
        if (!_suppressDirty) IsDirty = true;
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(TabTitle));
    }

    public void MarkSaved()
    {
        _document.IsDirty = false;
        IsDirty = false;
    }

    /// <summary>
    /// Substitui o conteúdo do documento com a versão lida do disco, sem marcar como sujo,
    /// e atualiza o timestamp de referência. Usado no recarregamento por mudança externa.
    /// </summary>
    public void ReloadFromDisk(string content, DateTime lastWriteTimeUtc)
    {
        _suppressDirty = true;
        Content = content;
        _suppressDirty = false;
        _document.LastWriteTimeUtc = lastWriteTimeUtc;
        IsDirty = false;
    }

    public MarkdownDocument GetDocument() => _document;
}
