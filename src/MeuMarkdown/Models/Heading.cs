namespace MeuMarkdown.Models;

public record Heading(int Level, string Text, int StartLine, string AnchorId)
{
    public int Indent => (Level - 1) * 12;
}
