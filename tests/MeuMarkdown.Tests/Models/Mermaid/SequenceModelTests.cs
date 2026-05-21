using MeuMarkdown.Models.Mermaid;

namespace MeuMarkdown.Tests.Models.Mermaid;

public class SequenceModelTests
{
    [Fact]
    public void ToMermaid_EmptyModel_StartsWithSequenceDiagram()
    {
        var model = new SequenceModel();
        Assert.StartsWith("sequenceDiagram", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_ParticipantWithoutAlias_EmitsParticipantLine()
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Bob", IsActor = false });
        Assert.Contains("participant Bob", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_ActorWithAlias_EmitsAsClause()
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Cliente", Alias = "C", IsActor = true });
        Assert.Contains("actor Cliente as C", model.ToMermaid());
    }

    [Theory]
    [InlineData(SequenceArrowType.Sync, "Alice->>Bob: olá")]
    [InlineData(SequenceArrowType.Reply, "Alice-->>Bob: olá")]
    [InlineData(SequenceArrowType.Async, "Alice-)Bob: olá")]
    public void ToMermaid_Message_EmitsCorrectArrow(SequenceArrowType arrow, string expected)
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Alice", IsActor = true });
        model.Actors.Add(new Actor { Name = "Bob", IsActor = false });
        model.Messages.Add(new Message { FromActor = "Alice", ToActor = "Bob", Arrow = arrow, Label = "olá" });
        Assert.Contains(expected, model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_MessageWithUnknownActor_IsOmitted()
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Alice", IsActor = true });
        model.Messages.Add(new Message { FromActor = "Alice", ToActor = "Ghost", Arrow = SequenceArrowType.Sync, Label = "olá" });
        Assert.DoesNotContain("Ghost", model.ToMermaid());
    }

    [Fact]
    public void ToMermaid_LabelWithQuotes_EscapesToSingleQuotes()
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Alice", IsActor = true });
        model.Actors.Add(new Actor { Name = "Bob", IsActor = false });
        model.Messages.Add(new Message { FromActor = "Alice", ToActor = "Bob", Arrow = SequenceArrowType.Sync, Label = "tem \"aspas\"" });
        var output = model.ToMermaid();
        Assert.Contains("tem 'aspas'", output);
        Assert.DoesNotContain("\"aspas\"", output);
    }

    [Fact]
    public void ToMermaid_LabelWithNewline_EmitsBrTag()
    {
        var model = new SequenceModel();
        model.Actors.Add(new Actor { Name = "Alice", IsActor = true });
        model.Actors.Add(new Actor { Name = "Bob", IsActor = false });
        model.Messages.Add(new Message { FromActor = "Alice", ToActor = "Bob", Arrow = SequenceArrowType.Sync, Label = "linha1\nlinha2" });
        Assert.Contains("linha1<br/>linha2", model.ToMermaid());
    }
}
