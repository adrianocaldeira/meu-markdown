using System.IO;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class FileService
{
    public MarkdownDocument OpenFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var content = File.ReadAllText(fullPath);
        return new MarkdownDocument
        {
            FilePath = fullPath,
            Content = content,
            IsDirty = false,
            LastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath)
        };
    }

    public void SaveFile(MarkdownDocument document)
    {
        if (string.IsNullOrEmpty(document.FilePath)) return;
        File.WriteAllText(document.FilePath, document.Content);
        document.IsDirty = false;
        document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(document.FilePath);
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public string ResolvePath(string relativePath, string baseDirectory)
    {
        if (Path.IsPathRooted(relativePath))
            return Path.GetFullPath(relativePath);
        return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    }
}
