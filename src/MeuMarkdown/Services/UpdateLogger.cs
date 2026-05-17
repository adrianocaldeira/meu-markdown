using System.IO;

namespace MeuMarkdown.Services;

public sealed class UpdateLogger
{
    private readonly string _path;

    public UpdateLogger() : this(DefaultPath()) { }

    public UpdateLogger(string path) { _path = path; }

    private static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeuMarkdown");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "update.log");
    }

    public void Log(string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            File.AppendAllText(_path, line);
        }
        catch
        {
            // Log não deve quebrar o app
        }
    }
}
