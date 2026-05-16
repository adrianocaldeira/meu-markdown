using System.IO;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class FileService
{
    public MarkdownDocument OpenFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return new MarkdownDocument
        {
            FilePath = Path.GetFullPath(filePath),
            Content = content,
            IsDirty = false
        };
    }

    public void SaveFile(MarkdownDocument document)
    {
        if (string.IsNullOrEmpty(document.FilePath)) return;
        File.WriteAllText(document.FilePath, document.Content);
        document.IsDirty = false;
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public string ResolvePath(string relativePath, string baseDirectory)
    {
        if (Path.IsPathRooted(relativePath))
            return Path.GetFullPath(relativePath);
        return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    }
}
