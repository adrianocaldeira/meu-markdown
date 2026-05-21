using MeuMarkdown.Models.Mermaid;
using MeuMarkdown.Services;

namespace MeuMarkdown.Tests.Services;

public class MermaidTemplateServiceTests
{
    [Theory]
    [InlineData(MermaidDiagramType.Flowchart, "graph TD")]
    [InlineData(MermaidDiagramType.Sequence, "sequenceDiagram")]
    [InlineData(MermaidDiagramType.Class, "classDiagram")]
    [InlineData(MermaidDiagramType.Er, "erDiagram")]
    [InlineData(MermaidDiagramType.State, "stateDiagram-v2")]
    [InlineData(MermaidDiagramType.Gantt, "gantt")]
    [InlineData(MermaidDiagramType.Mindmap, "mindmap")]
    [InlineData(MermaidDiagramType.Pie, "pie")]
    public void GetTemplate_StartsWithExpectedKeyword(MermaidDiagramType type, string keyword)
    {
        var svc = new MermaidTemplateService();
        var text = svc.GetTemplate(type);
        Assert.StartsWith(keyword, text.TrimStart());
    }

    [Theory]
    [InlineData(MermaidDiagramType.Flowchart)]
    [InlineData(MermaidDiagramType.Sequence)]
    [InlineData(MermaidDiagramType.Class)]
    [InlineData(MermaidDiagramType.Er)]
    [InlineData(MermaidDiagramType.State)]
    [InlineData(MermaidDiagramType.Gantt)]
    [InlineData(MermaidDiagramType.Mindmap)]
    [InlineData(MermaidDiagramType.Pie)]
    public void GetTemplate_ContainsPortugueseComment(MermaidDiagramType type)
    {
        var svc = new MermaidTemplateService();
        var text = svc.GetTemplate(type);
        Assert.Contains("%%", text);
    }

    [Fact]
    public void GetTemplate_SecondCall_ReturnsSameInstance()
    {
        var svc = new MermaidTemplateService();
        var a = svc.GetTemplate(MermaidDiagramType.Flowchart);
        var b = svc.GetTemplate(MermaidDiagramType.Flowchart);
        Assert.Same(a, b);
    }
}
