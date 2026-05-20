using Markdig;
using MeuMarkdown.Extensions.WikiLinks;
using Xunit;

namespace MeuMarkdown.Tests.Markdig;

public class WikiLinkHtmlRendererTests
{
    private static string Render(
        string md,
        Func<string, string?, WikiLinkResolution?>? resolver = null,
        string? currentDir = null)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .Use(new WikiLinkExtension(resolver, () => currentDir))
            .Build();
        return global::Markdig.Markdown.ToHtml(md, pipeline);
    }

    [Fact]
    public void Render_BrokenLink_EmitsSpanWithBrokenClass()
    {
        var html = Render("Veja [[Foo]] aqui.", resolver: (_, _) => null);

        Assert.Contains("<span class=\"wikilink-broken\"", html);
        Assert.Contains(">Foo</span>", html);
    }

    [Fact]
    public void Render_ResolvedLink_EmitsAnchorWithMdnavHref()
    {
        var resolver = (string target, string? _) =>
            target == "Foo" ? new WikiLinkResolution("C:\\workspace\\Foo.md", "Foo") : null;
        var html = Render("Veja [[Foo]] aqui.", resolver);

        Assert.Contains("<a href=\"mdnav://open?path=", html);
        Assert.Contains("class=\"wikilink\"", html);
        Assert.Contains(">Foo</a>", html);
    }

    [Fact]
    public void Render_ResolvedWithFragment_HrefIncludesFragmentParam()
    {
        var resolver = (string target, string? _) =>
            target == "Foo" ? new WikiLinkResolution("C:\\workspace\\Foo.md", "Foo") : null;
        var html = Render("Veja [[Foo#secao]] aqui.", resolver);

        Assert.Contains("fragment=secao", html);
    }

    [Fact]
    public void Render_AliasOverridesDisplayText()
    {
        var resolver = (string target, string? _) =>
            target == "Foo" ? new WikiLinkResolution("C:\\workspace\\Foo.md", "Foo") : null;
        var html = Render("Veja [[Foo|texto alternativo]] aqui.", resolver);

        Assert.Contains(">texto alternativo</a>", html);
    }

    [Fact]
    public void Render_NullResolver_AllLinksBroken()
    {
        var html = Render("Veja [[Foo]] aqui.", resolver: null);

        Assert.Contains("wikilink-broken", html);
    }
}
