using MeuMarkdown.Models.Mermaid;

namespace MeuMarkdown.Tests.Models.Mermaid;

public class FlowchartModelTests
{
    [Fact]
    public void ToMermaid_EmptyModel_StartsWithGraphTD()
    {
        var model = new FlowchartModel();
        var output = model.ToMermaid();
        Assert.StartsWith("graph TD", output);
    }

    [Theory]
    [InlineData(FlowDirection.TD, "graph TD")]
    [InlineData(FlowDirection.LR, "graph LR")]
    [InlineData(FlowDirection.BT, "graph BT")]
    [InlineData(FlowDirection.RL, "graph RL")]
    public void ToMermaid_Direction_EmitsCorrectHeader(FlowDirection dir, string expected)
    {
        var model = new FlowchartModel { Direction = dir };
        Assert.StartsWith(expected, model.ToMermaid());
    }

    [Theory]
    [InlineData(FlowNodeShape.Rectangle, "n1[Olá]")]
    [InlineData(FlowNodeShape.Rounded, "n1(Olá)")]
    [InlineData(FlowNodeShape.Diamond, "n1{Olá}")]
    [InlineData(FlowNodeShape.Circle, "n1((Olá))")]
    [InlineData(FlowNodeShape.Stadium, "n1([Olá])")]
    public void ToMermaid_Shape_EmitsCorrectSyntax(FlowNodeShape shape, string expectedFragment)
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Olá", Shape = shape });
        Assert.Contains(expectedFragment, model.ToMermaid());
    }

    [Theory]
    [InlineData(FlowArrowType.Solid, "n1 --> n2")]
    [InlineData(FlowArrowType.Dotted, "n1 -.-> n2")]
    [InlineData(FlowArrowType.Thick, "n1 ==> n2")]
    public void ToMermaid_Arrow_EmitsCorrectSyntax(FlowArrowType arrow, string expected)
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "A" });
        model.Nodes.Add(new FlowNode { Id = "n2", Label = "B" });
        model.Edges.Add(new FlowEdge { FromId = "n1", ToId = "n2", Arrow = arrow });
        Assert.Contains(expected, model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_SolidLabeledArrow_EmitsLabelBetweenPipes()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "A" });
        model.Nodes.Add(new FlowNode { Id = "n2", Label = "B" });
        model.Edges.Add(new FlowEdge { FromId = "n1", ToId = "n2", Arrow = FlowArrowType.SolidLabeled, Label = "sim" });
        Assert.Contains("n1 -->|sim| n2", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_SolidArrow_IgnoresLabelEvenIfPresent()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "A" });
        model.Nodes.Add(new FlowNode { Id = "n2", Label = "B" });
        model.Edges.Add(new FlowEdge { FromId = "n1", ToId = "n2", Arrow = FlowArrowType.Solid, Label = "ignorado" });
        Assert.Contains("n1 --> n2", model.ToMermaid());
        Assert.DoesNotContain("|ignorado|", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_LabelWithQuotes_EscapesToSingleQuotes()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Tem \"aspas\"" });
        var output = model.ToMermaid();
        Assert.Contains("Tem 'aspas'", output);
        Assert.DoesNotContain("\"aspas\"", output);
    }

    [Fact]
    public void ToMermaid_LabelWithNewline_EmitsBrTag()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Linha 1\nLinha 2" });
        Assert.Contains("Linha 1<br/>Linha 2", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_LabelWithBrackets_WrapsInDoubleQuotes()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Texto (a) [b]", Shape = FlowNodeShape.Rectangle });
        Assert.Contains("n1[\"Texto (a) [b]\"]", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_EdgeWithOrphanFromId_IsOmitted()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "A" });
        model.Edges.Add(new FlowEdge { FromId = "ghost", ToId = "n1", Arrow = FlowArrowType.Solid });
        var output = model.ToMermaid();
        Assert.DoesNotContain("ghost", output);
    }

    [Fact]
    public void ToMermaid_EdgeWithOrphanToId_IsOmitted()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "A" });
        model.Edges.Add(new FlowEdge { FromId = "n1", ToId = "ghost", Arrow = FlowArrowType.Solid });
        Assert.DoesNotContain("ghost", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_DuplicateNodeId_EmitsOnlyFirst()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Primeiro" });
        model.Nodes.Add(new FlowNode { Id = "n1", Label = "Segundo" });
        var output = model.ToMermaid();
        Assert.Contains("Primeiro", output);
        Assert.DoesNotContain("Segundo", output);
    }

    [Fact]
    public void ToMermaid_EmptyOrInvalidId_UsesTempId()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "", Label = "Sem ID" });
        var output = model.ToMermaid();
        Assert.Contains("Sem ID", output);
        Assert.Matches(@"n_[a-z0-9]+\[Sem ID\]", output);
    }

    [Fact]
    public void ToMermaid_InvalidIdWithSpaces_UsesTempId()
    {
        var model = new FlowchartModel();
        model.Nodes.Add(new FlowNode { Id = "id com espaço", Label = "X" });
        var output = model.ToMermaid();
        Assert.DoesNotContain("id com espaço", output);
        Assert.Contains("[X]", output);
    }
}
