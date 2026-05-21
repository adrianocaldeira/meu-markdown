using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class SequenceModel : ObservableObject
{
    public ObservableCollection<Actor> Actors { get; } = new();
    public ObservableCollection<Message> Messages { get; } = new();

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.AppendLine("sequenceDiagram");

        var knownNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in Actors)
        {
            if (string.IsNullOrWhiteSpace(actor.Name)) continue;
            if (!knownNames.Add(actor.Name)) continue;

            var kw = actor.IsActor ? "actor" : "participant";
            if (!string.IsNullOrWhiteSpace(actor.Alias))
                sb.Append("    ").Append(kw).Append(' ').Append(actor.Name).Append(" as ").Append(actor.Alias).AppendLine();
            else
                sb.Append("    ").Append(kw).Append(' ').Append(actor.Name).AppendLine();
        }

        foreach (var msg in Messages)
        {
            if (!knownNames.Contains(msg.FromActor) || !knownNames.Contains(msg.ToActor)) continue;
            var arrow = msg.Arrow switch
            {
                SequenceArrowType.Sync  => "->>",
                SequenceArrowType.Reply => "-->>",
                SequenceArrowType.Async => "-)",
                _                       => "->>",
            };
            sb.Append("    ")
              .Append(msg.FromActor).Append(arrow).Append(msg.ToActor)
              .Append(": ").Append(EscapeLabel(msg.Label))
              .AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string EscapeLabel(string label)
    {
        return label
            .Replace("\"", "'")
            .Replace("\r\n", "<br/>")
            .Replace("\n", "<br/>")
            .Replace("\r", "<br/>");
    }
}
