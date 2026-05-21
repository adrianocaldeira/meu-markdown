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

    public string CreateNewFile(string directory, string baseName, string extension)
    {
        if (!Directory.Exists(directory))
            throw new FileOperationException($"Diretório não existe: {directory}");

        var name = GenerateUniqueName(directory, baseName, extension);
        var fullPath = Path.Combine(directory, name);
        try
        {
            File.WriteAllText(fullPath, string.Empty);
            return fullPath;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Erro ao criar arquivo '{name}': {ex.Message}", ex);
        }
    }

    public string CreateNewFolder(string directory, string baseName)
    {
        if (!Directory.Exists(directory))
            throw new FileOperationException($"Diretório não existe: {directory}");

        var name = GenerateUniqueName(directory, baseName, null);
        var fullPath = Path.Combine(directory, name);
        try
        {
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Erro ao criar pasta '{name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Copia source para destDirectory. Se nome já existe no destino:
    /// - Se sourceDirectory == destDirectory: gera nome único automaticamente (sem callback).
    /// - Senão: invoca onConflict; true → sobrescreve; false ou null → retorna null.
    /// </summary>
    public string? CopyFile(string sourcePath, string destDirectory, Func<string, bool>? onConflict)
    {
        if (!File.Exists(sourcePath))
            throw new FileOperationException($"Arquivo origem não existe: {sourcePath}");
        if (!Directory.Exists(destDirectory))
            throw new FileOperationException($"Diretório destino não existe: {destDirectory}");

        var sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDirectory, fileName);

        if (string.Equals(sourceDir, destDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var uniqueName = GenerateUniqueName(destDirectory, baseName, ext);
            destPath = Path.Combine(destDirectory, uniqueName);
        }
        else if (File.Exists(destPath) || Directory.Exists(destPath))
        {
            if (onConflict == null || !onConflict(destPath))
                return null;
        }

        try
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Erro ao copiar '{fileName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Move source para destDirectory. Move no mesmo dir é no-op (retorna sourcePath).
    /// </summary>
    public string? MoveFile(string sourcePath, string destDirectory, Func<string, bool>? onConflict)
    {
        if (!File.Exists(sourcePath))
            throw new FileOperationException($"Arquivo origem não existe: {sourcePath}");
        if (!Directory.Exists(destDirectory))
            throw new FileOperationException($"Diretório destino não existe: {destDirectory}");

        var sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
        if (string.Equals(sourceDir, destDirectory, StringComparison.OrdinalIgnoreCase))
            return sourcePath;

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destDirectory, fileName);

        if (File.Exists(destPath) || Directory.Exists(destPath))
        {
            if (onConflict == null || !onConflict(destPath))
                return null;
            if (File.Exists(destPath)) File.Delete(destPath);
            else Directory.Delete(destPath, recursive: true);
        }

        try
        {
            File.Move(sourcePath, destPath);
            return destPath;
        }
        catch (Exception ex)
        {
            throw new FileOperationException($"Erro ao mover '{fileName}': {ex.Message}", ex);
        }
    }
}
