using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class FlowchartModel : ObservableObject
{
    [ObservableProperty] private FlowDirection _direction = FlowDirection.TD;

    public ObservableCollection<FlowNode> Nodes { get; } = new();
    public ObservableCollection<FlowEdge> Edges { get; } = new();

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.Append("graph ").Append(Direction.ToString()).AppendLine();

        foreach (var node in Nodes)
            sb.Append("    ").Append(EmitNode(node)).AppendLine();

        foreach (var edge in Edges)
            sb.Append("    ").Append(EmitEdge(edge)).AppendLine();

        return sb.ToString().TrimEnd();
    }

    private static string EmitNode(FlowNode node)
    {
        var (open, close) = ShapeBrackets(node.Shape);
        var label = EscapeLabel(node.Label);
        if (NeedsQuoting(node.Label))
            return $"{node.Id}{open}\"{label}\"{close}";
        return $"{node.Id}{open}{label}{close}";
    }

    private static string EmitEdge(FlowEdge edge)
    {
        return edge.Arrow switch
        {
            FlowArrowType.Solid        => $"{edge.FromId} --> {edge.ToId}",
            FlowArrowType.SolidLabeled => $"{edge.FromId} -->|{EscapeLabel(edge.Label)}| {edge.ToId}",
            FlowArrowType.Dotted       => $"{edge.FromId} -.-> {edge.ToId}",
            FlowArrowType.Thick        => $"{edge.FromId} ==> {edge.ToId}",
            _                          => $"{edge.FromId} --> {edge.ToId}",
        };
    }

    private static string EscapeLabel(string label)
    {
        return label
            .Replace("\"", "'")
            .Replace("\r\n", "<br/>")
            .Replace("\n", "<br/>")
            .Replace("\r", "<br/>");
    }

    private static bool NeedsQuoting(string label)
    {
        // Brackets dentro de label de Flowchart precisam ser envolvidos por aspas duplas.
        return label.IndexOfAny(new[] { '[', ']', '(', ')', '{', '}' }) >= 0;
    }

    private static (string open, string close) ShapeBrackets(FlowNodeShape shape) => shape switch
    {
        FlowNodeShape.Rectangle => ("[", "]"),
        FlowNodeShape.Rounded   => ("(", ")"),
        FlowNodeShape.Diamond   => ("{", "}"),
        FlowNodeShape.Circle    => ("((", "))"),
        FlowNodeShape.Stadium   => ("([", "])"),
        _ => ("[", "]"),
    };
}
