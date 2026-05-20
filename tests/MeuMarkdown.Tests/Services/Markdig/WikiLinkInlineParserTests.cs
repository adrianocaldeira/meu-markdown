using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MeuMarkdown.Extensions.WikiLinks;
using Xunit;

namespace MeuMarkdown.Tests.Markdig;

public class WikiLinkInlineParserTests
{
    private static MarkdownPipeline BuildPipeline()
    {
        return new MarkdownPipelineBuilder()
            .Use(new WikiLinkExtension())
            .Build();
    }

    private static WikiLinkInline? FirstWikiLink(string markdown)
    {
        var doc = global::Markdig.Markdown.Parse(markdown, BuildPipeline());
        foreach (var block in doc.Descendants<ParagraphBlock>())
        {
            if (block.Inline == null) continue;
            foreach (var inline in block.Inline)
            {
                if (inline is WikiLinkInline wl) return wl;
            }
        }
        return null;
    }

    [Fact]
    public void Parse_SimpleName_TargetIsName()
    {
        var wl = FirstWikiLink("Veja [[Foo]] aqui.");
        Assert.NotNull(wl);
        Assert.Equal("Foo", wl!.Target);
        Assert.Equal("Foo", wl.DisplayText);
        Assert.Null(wl.Fragment);
    }

    [Fact]
    public void Parse_WithAlias_DisplayTextIsAlias()
    {
        var wl = FirstWikiLink("Veja [[Foo|texto alternativo]] aqui.");
        Assert.NotNull(wl);
        Assert.Equal("Foo", wl!.Target);
        Assert.Equal("texto alternativo", wl.DisplayText);
    }

    [Fact]
    public void Parse_WithFragment_FragmentExtracted()
    {
        var wl = FirstWikiLink("Veja [[Foo#secao]] aqui.");
        Assert.NotNull(wl);
        Assert.Equal("Foo", wl!.Target);
        Assert.Equal("secao", wl.Fragment);
    }

    [Fact]
    public void Parse_WithFragmentAndAlias_BothExtracted()
    {
        var wl = FirstWikiLink("Veja [[Foo#secao|alias]] aqui.");
        Assert.NotNull(wl);
        Assert.Equal("Foo", wl!.Target);
        Assert.Equal("secao", wl.Fragment);
        Assert.Equal("alias", wl.DisplayText);
    }

    [Fact]
    public void Parse_WithSlashPath_TargetIncludesPath()
    {
        var wl = FirstWikiLink("Veja [[Sub/Foo]] aqui.");
        Assert.NotNull(wl);
        Assert.Equal("Sub/Foo", wl!.Target);
    }

    [Fact]
    public void Parse_NotAWikiLink_SingleBracket_ReturnsNull()
    {
        Assert.Null(FirstWikiLink("Veja [Foo] aqui."));
    }

    [Fact]
    public void Parse_EmptyBrackets_ReturnsNull()
    {
        Assert.Null(FirstWikiLink("Veja [[]] aqui."));
    }

    [Fact]
    public void Parse_UnclosedWikiLink_ReturnsNull()
    {
        Assert.Null(FirstWikiLink("Veja [[Foo aqui."));
    }
}
