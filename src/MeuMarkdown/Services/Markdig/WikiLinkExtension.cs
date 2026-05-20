using Markdig;
using Markdig.Renderers;

namespace MeuMarkdown.Extensions.WikiLinks;

public record WikiLinkResolution(string AbsolutePath, string DisplayName);

public class WikiLinkExtension : IMarkdownExtension
{
    private readonly Func<string, string?, WikiLinkResolution?>? _resolver;
    private readonly Func<string?> _currentFileDir;

    public WikiLinkExtension(
        Func<string, string?, WikiLinkResolution?>? resolver = null,
        Func<string?>? currentFileDir = null)
    {
        _resolver = resolver;
        _currentFileDir = currentFileDir ?? (() => null);
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<WikiLinkInlineParser>())
            pipeline.InlineParsers.Insert(0, new WikiLinkInlineParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer
            && !htmlRenderer.ObjectRenderers.Contains<WikiLinkHtmlRenderer>())
        {
            htmlRenderer.ObjectRenderers.Add(new WikiLinkHtmlRenderer(_resolver, _currentFileDir));
        }
    }
}
