using MeuMarkdown.Services;

namespace MeuMarkdown.Tests.Services;

public class MarkdownInsertionServiceTests
{
    [Fact]
    public void Build_OnEmptyDocument_NoPrefixNoExtraSuffix()
    {
        var (text, newOffset) = MarkdownInsertionService.BuildMermaidInsertion(
            content: "",
            caretOffset: 0,
            mermaidCode: "graph TD\n    A --> B");

        Assert.Equal("```mermaid\ngraph TD\n    A --> B\n```\n\n", text);
        Assert.Equal(text.Length, newOffset);
    }

    [Fact]
    public void Build_CursorOnEmptyLine_NoPrefix()
    {
        var doc = "Texto antes\n\n";
        var (text, _) = MarkdownInsertionService.BuildMermaidInsertion(
            content: doc,
            caretOffset: doc.Length,
            mermaidCode: "graph TD\n    A --> B");

        Assert.StartsWith("```mermaid", text);
    }

    [Fact]
    public void Build_CursorOnNonEmptyLine_PrefixesDoubleNewline()
    {
        var doc = "Texto sem quebra";
        var (text, _) = MarkdownInsertionService.BuildMermaidInsertion(
            content: doc,
            caretOffset: doc.Length,
            mermaidCode: "graph TD\n    A --> B");

        Assert.StartsWith("\n\n```mermaid", text);
    }

    [Fact]
    public void Build_AtStartOfDocument_NoPrefix()
    {
        var doc = "Texto qualquer";
        var (text, _) = MarkdownInsertionService.BuildMermaidInsertion(
            content: doc,
            caretOffset: 0,
            mermaidCode: "graph TD");

        Assert.StartsWith("```mermaid", text);
    }

    [Fact]
    public void Build_CursorMidLine_PrefixesDoubleNewline()
    {
        var doc = "abc";
        var (text, _) = MarkdownInsertionService.BuildMermaidInsertion(
            content: doc,
            caretOffset: 1,
            mermaidCode: "graph TD");

        Assert.StartsWith("\n\n```mermaid", text);
    }

    [Fact]
    public void Build_AlwaysSuffixesDoubleNewline()
    {
        var (text, _) = MarkdownInsertionService.BuildMermaidInsertion(
            content: "",
            caretOffset: 0,
            mermaidCode: "graph TD");

        Assert.EndsWith("```\n\n", text);
    }

    [Fact]
    public void Build_NewOffset_PointsToEndOfInsertion()
    {
        var doc = "Algo antes";
        var (text, newOffset) = MarkdownInsertionService.BuildMermaidInsertion(
            content: doc,
            caretOffset: doc.Length,
            mermaidCode: "graph TD");

        Assert.Equal(doc.Length + text.Length, newOffset);
    }
}
