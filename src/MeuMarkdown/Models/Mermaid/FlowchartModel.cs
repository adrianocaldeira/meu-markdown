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
        {
            var (open, close) = ShapeBrackets(node.Shape);
            sb.Append("    ").Append(node.Id).Append(open).Append(node.Label).Append(close).AppendLine();
        }

        return sb.ToString().TrimEnd();
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
