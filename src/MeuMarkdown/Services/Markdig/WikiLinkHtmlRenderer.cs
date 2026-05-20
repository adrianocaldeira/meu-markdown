using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace MeuMarkdown.Extensions.WikiLinks;

public class WikiLinkHtmlRenderer : HtmlObjectRenderer<WikiLinkInline>
{
    private readonly Func<string, string?, WikiLinkResolution?>? _resolver;
    private readonly Func<string?> _currentFileDir;

    public WikiLinkHtmlRenderer(
        Func<string, string?, WikiLinkResolution?>? resolver,
        Func<string?> currentFileDir)
    {
        _resolver = resolver;
        _currentFileDir = currentFileDir;
    }

    protected override void Write(HtmlRenderer renderer, WikiLinkInline obj)
    {
        var resolved = _resolver?.Invoke(obj.Target, _currentFileDir());
        if (resolved == null)
        {
            renderer.Write("<span class=\"wikilink-broken\" title=\"Arquivo não encontrado\">");
            renderer.WriteEscape(obj.DisplayText);
            renderer.Write("</span>");
            return;
        }

        var encodedPath = Uri.EscapeDataString(resolved.AbsolutePath);
        var href = $"mdnav://open?path={encodedPath}";
        if (!string.IsNullOrEmpty(obj.Fragment))
        {
            href += "&fragment=" + Uri.EscapeDataString(obj.Fragment);
        }
        renderer.Write("<a href=\"");
        renderer.Write(href);
        renderer.Write("\" class=\"wikilink\">");
        renderer.WriteEscape(obj.DisplayText);
        renderer.Write("</a>");
    }
}
