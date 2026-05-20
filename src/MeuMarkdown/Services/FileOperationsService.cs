using System.IO;

namespace MeuMarkdown.Services;

public class FileOperationException : Exception
{
    public FileOperationException(string message) : base(message) { }
    public FileOperationException(string message, Exception inner) : base(message, inner) { }
}

public class FileOperationsService
{
    /// <summary>
    /// Gera um nome único no diretório: se "baseName.ext" não existe, retorna ele;
    /// se existe, tenta "baseName (2).ext", "baseName (3).ext", etc.
    /// Para pasta (extension null/vazio), gera "baseName", "baseName (2)", etc.
    /// </summary>
    public static string GenerateUniqueName(string directory, string baseName, string? extension)
    {
        var ext = string.IsNullOrEmpty(extension) ? string.Empty : extension;
        if (!ext.StartsWith('.') && ext.Length > 0) ext = "." + ext;

        var candidate = Path.Combine(directory, baseName + ext);
        if (!Exists(candidate)) return baseName + ext;

        for (int i = 2; i < 1000; i++)
        {
            var name = $"{baseName} ({i}){ext}";
            candidate = Path.Combine(directory, name);
            if (!Exists(candidate)) return name;
        }
        throw new FileOperationException($"Não foi possível gerar nome único para {baseName}{ext} (mais de 1000 tentativas).");
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
