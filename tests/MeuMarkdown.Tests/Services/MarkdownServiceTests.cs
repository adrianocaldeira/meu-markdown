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
}
