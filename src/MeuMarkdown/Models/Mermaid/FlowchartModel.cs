using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class FlowchartModel : ObservableObject
{
    [ObservableProperty] private FlowDirection _direction = FlowDirection.TD;

    public ObservableCollection<FlowNode> Nodes { get; } = new();
    public ObservableCollection<FlowEdge> Edges { get; } = new();

    private static readonly Regex ValidIdRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.Append("graph ").Append(Direction.ToString()).AppendLine();

        var emittedIds = new HashSet<string>(StringComparer.Ordinal);
        var idMap = new Dictionary<FlowNode, string>();

        foreach (var node in Nodes)
        {
            var id = ResolveId(node, emittedIds);
            if (id == null) continue; // duplicado
            idMap[node] = id;
            emittedIds.Add(id);
            sb.Append("    ").Append(EmitNode(id, node)).AppendLine();
        }

        // IDs de nós válidos pra checar arestas órfãs.
        var validIds = new HashSet<string>(idMap.Values, StringComparer.Ordinal);

        foreach (var edge in Edges)
        {
            if (!validIds.Contains(edge.FromId) || !validIds.Contains(edge.ToId)) continue;
            sb.Append("    ").Append(EmitEdge(edge)).AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string? ResolveId(FlowNode node, HashSet<string> alreadyEmitted)
    {
        var raw = node.Id ?? "";
        var id = ValidIdRegex.IsMatch(raw) ? raw : GenerateTempId(node);

        if (alreadyEmitted.Contains(id))
        {
            // duplicado — pulamos o nó (primeiro vence)
            return null;
        }
        return id;
    }

    private static string GenerateTempId(FlowNode node)
    {
        var hash = (node.GetHashCode() & 0x7fffffff).ToString("x8");
        return "n_" + hash;
    }

    private static string EmitNode(string id, FlowNode node)
    {
        var (open, close) = ShapeBrackets(node.Shape);
        var label = EscapeLabel(node.Label);
        if (NeedsQuoting(node.Label))
            return $"{id}{open}\"{label}\"{close}";
        return $"{id}{open}{label}{close}";
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
