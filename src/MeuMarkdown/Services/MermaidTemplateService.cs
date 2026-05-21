using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using MeuMarkdown.Models.Mermaid;

namespace MeuMarkdown.Services;

public class MermaidTemplateService
{
    private readonly ConcurrentDictionary<MermaidDiagramType, string> _cache = new();

    public string GetTemplate(MermaidDiagramType type)
    {
        return _cache.GetOrAdd(type, Load);
    }

    private static string Load(MermaidDiagramType type)
    {
        var fileName = type switch
        {
            MermaidDiagramType.Flowchart => "flowchart-td.txt",
            MermaidDiagramType.Sequence => "sequence.txt",
            MermaidDiagramType.Class => "class.txt",
            MermaidDiagramType.Er => "er.txt",
            MermaidDiagramType.State => "state.txt",
            MermaidDiagramType.Gantt => "gantt.txt",
            MermaidDiagramType.Mindmap => "mindmap.txt",
            MermaidDiagramType.Pie => "pie.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        var resourceName = $"MeuMarkdown.Resources.MermaidTemplates.{fileName}";
        var asm = typeof(MermaidTemplateService).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Template não encontrado: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
