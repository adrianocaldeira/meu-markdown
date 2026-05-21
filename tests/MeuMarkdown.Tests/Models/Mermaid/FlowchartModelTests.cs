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
}
