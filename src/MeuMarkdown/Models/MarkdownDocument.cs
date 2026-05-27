using System.IO;

namespace MeuMarkdown.Models;

public class MarkdownDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => string.IsNullOrEmpty(FilePath) ? "Sem título" : Path.GetFileName(FilePath);
    public string Directory => string.IsNullOrEmpty(FilePath) ? string.Empty : Path.GetDirectoryName(FilePath) ?? string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsDirty { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
}
