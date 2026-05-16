using System.IO;
using System.Text.Json;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class AppStateService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public AppStateService(string filePath)
    {
        _filePath = filePath;
    }

    public static AppStateService CreateDefault()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeuMarkdown");
        Directory.CreateDirectory(dir);
        return new AppStateService(Path.Combine(dir, "state.json"));
    }

    public AppState Load()
    {
        if (!File.Exists(_filePath))
            return new AppState();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new AppState();
        }
    }

    public void Save(AppState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
