using MeuMarkdown.Models;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class MarkdownServiceTests
{
    [Fact]
    public void ExtractHeadings_ReturnsAllHeadingsWithCorrectLevels()
    {
        var service = new MarkdownService();
        var md = "# Título 1\n\nTexto.\n\n## Subseção A\n\nTexto.\n\n### Detalhe\n\n## Subseção B\n";

        var headings = service.ExtractHeadings(md);

        Assert.Equal(4, headings.Count);
        Assert.Equal(1, headings[0].Level);
        Assert.Equal("Título 1", headings[0].Text);
        Assert.Equal(2, headings[1].Level);
        Assert.Equal("Subseção A", headings[1].Text);
        Assert.Equal(3, headings[2].Level);
        Assert.Equal("Detalhe", headings[2].Text);
        Assert.Equal(2, headings[3].Level);
        Assert.Equal("Subseção B", headings[3].Text);
    }

    [Fact]
    public void ExtractHeadings_EmptyMarkdown_ReturnsEmptyList()
    {
        var service = new MarkdownService();

        var headings = service.ExtractHeadings("");

        Assert.Empty(headings);
    }

    [Fact]
    public void ExtractHeadings_StartLineMatchesSourcePosition()
    {
        var service = new MarkdownService();
        var md = "Parágrafo.\n\n# Heading na linha 3\n\nMais texto.";

        var headings = service.ExtractHeadings(md);

        Assert.Single(headings);
        Assert.Equal(3, headings[0].StartLine);
    }

    [Fact]
    public void ExtractHeadings_GeneratesGfmCompatibleSlugs()
    {
        var service = new MarkdownService();
        var md = "# Olá Mundo!\n## Subtítulo com Acentuação\n";

        var headings = service.ExtractHeadings(md);

        Assert.Equal("olá-mundo", headings[0].AnchorId);
        Assert.Equal("subtítulo-com-acentuação", headings[1].AnchorId);
    }

    [Fact]
    public void ConvertToHtml_LinkWithoutFragment_EmitsPathOnly()
    {
        var service = new MarkdownService();
        var md = "[ver](arquivo.md)";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("mdnav://open?path=arquivo.md", html);
        Assert.DoesNotContain("fragment=", html);
    }

    [Fact]
    public void ConvertToHtml_LinkWithFragment_EmitsPathAndFragmentSeparately()
    {
        var service = new MarkdownService();
        var md = "[ver](arquivo.md#secao)";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("path=arquivo.md", html);
        Assert.Contains("fragment=secao", html);
        Assert.DoesNotContain("path=arquivo.md%23", html);
        Assert.DoesNotContain("path=arquivo.md#", html);
    }

    [Fact]
    public void ConvertToHtml_LinkWithFragmentContainingSpecialChars_EncodesCorrectly()
    {
        var service = new MarkdownService();
        var md = "[ver](arquivo.md#seção-com-acento)";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("path=arquivo.md", html);
        // Markdig pré-encoda os bytes UTF-8 (%C3%A7 para ç, %C3%A3 para ã) antes do
        // nosso RewriteRelativeLinks, que então chama Uri.EscapeDataString sobre o valor
        // já encoded — resultando em double-encoding dos % como %25.
        Assert.Contains("fragment=se%25C3%25A7%25C3%25A3o-com-acento", html);
    }

    [Fact]
    public void ConvertToHtml_LinkWithSubdirPathAndFragment_SplitsCorrectly()
    {
        var service = new MarkdownService();
        var md = "[ver](docs/sub/arquivo.md#secao)";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("path=docs%2Fsub%2Farquivo.md", html);
        Assert.Contains("fragment=secao", html);
    }

    [Fact]
    public void ConvertToHtml_LinkWithEmptyFragment_OmitsFragmentParam()
    {
        var service = new MarkdownService();
        var md = "[ver](arquivo.md#)";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("path=arquivo.md", html);
        Assert.DoesNotContain("fragment=", html);
    }

    [Fact]
    public void ConvertToHtml_LinkWithSpacesInPath_EncodesPath()
    {
        var service = new MarkdownService();
        var md = "[ver](<meu arquivo.md>)";  // sintaxe do Markdig pra path com espaço

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        // Markdig pré-encoda o espaço como %20 antes do nosso RewriteRelativeLinks,
        // que então chama Uri.EscapeDataString — resultando em double-encoding (%2520).
        Assert.Contains("path=meu%2520arquivo.md", html);
    }

    [Fact]
    public void ConvertToHtml_InlineMath_GeneratesMathSpan()
    {
        var service = new MarkdownService();
        var md = "A fórmula $E=mc^2$ é famosa.";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("math", html);
    }

    [Fact]
    public void ConvertToHtml_BlockMath_GeneratesMathDiv()
    {
        var service = new MarkdownService();
        var md = "$$\n\\int_0^\\infty e^{-x} dx\n$$";

        var html = service.ConvertToHtmlFragment(md, baseDirectory: "C:\\docs");

        Assert.Contains("math", html);
    }
}
