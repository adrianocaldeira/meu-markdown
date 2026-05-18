using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models;

public partial class FileNode : ObservableObject
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }

    public ObservableCollection<FileNode> Children { get; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isSelected;

    public FileNode(string name, string fullPath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }
}
