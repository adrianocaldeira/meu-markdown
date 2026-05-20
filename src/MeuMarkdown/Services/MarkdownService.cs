using System.IO;
using System.Reflection;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using MeuMarkdown.Extensions.WikiLinks;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class MarkdownService
{
    private MarkdownPipeline _pipeline;
    private readonly string _htmlTemplate;
    private readonly string _css;
    private readonly string _katexJs;
    private readonly string _katexCss;
    private readonly string _katexAutoRenderJs;
    private readonly string _mermaidJs;

    private Func<string, string?, WikiLinkResolution?>? _wikiResolver;
    private Func<string?>? _currentFileDir;

    public MarkdownService()
    {
        _htmlTemplate      = LoadEmbeddedResource("MeuMarkdown.Resources.preview-template.html");
        _css               = LoadEmbeddedResource("MeuMarkdown.Resources.github-markdown.css");
        _katexJs           = LoadEmbeddedResource("MeuMarkdown.Resources.katex.min.js");
        _katexCss          = LoadEmbeddedResource("MeuMarkdown.Resources.katex.min.css");
        _katexAutoRenderJs = LoadEmbeddedResource("MeuMarkdown.Resources.katex-auto-render.min.js");
        _mermaidJs         = LoadEmbeddedResource("MeuMarkdown.Resources.mermaid.min.js");
        _pipeline = BuildPipeline();
    }

    /// <summary>
    /// Recursos KaTeX + Mermaid embutidos. Usado pelo ExportService pra incluir
    /// os mesmos scripts no HTML exportado, garantindo que fórmulas e diagramas
    /// renderizem no arquivo standalone.
    /// </summary>
    public (string KatexJs, string KatexCss, string KatexAutoRenderJs, string MermaidJs) GetEnhancementResources()
        => (_katexJs, _katexCss, _katexAutoRenderJs, _mermaidJs);

    /// <summary>
    /// Extrai Mermaid e KaTeX (JS + auto-render) pra um diretório, pra serem servidos
    /// via virtual host mapping pelo WebView2 (contornando o cap de ~2MB do NavigateToString).
    /// Idempotente: só reescreve se o conteúdo diferir do já em disco.
    /// </summary>
    public void ExtractEnhancementAssetsTo(string directory)
    {
        Directory.CreateDirectory(directory);
        WriteIfDifferent(Path.Combine(directory, "mermaid.min.js"), _mermaidJs);
        WriteIfDifferent(Path.Combine(directory, "katex.min.js"), _katexJs);
        WriteIfDifferent(Path.Combine(directory, "katex-auto-render.min.js"), _katexAutoRenderJs);
    }

    private static void WriteIfDifferent(string path, string content)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (existing == content) return;
        }
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Configura o resolver de wiki-links. Reconstrói o pipeline com a extensão ativa.
    /// Sem chamar este método, wiki-links são renderizados como broken.
    /// </summary>
    public void ConfigureWikiLinkResolver(
        Func<string, string?, WikiLinkResolution?> resolver,
        Func<string?> currentFileDir)
    {
        _wikiResolver = resolver;
        _currentFileDir = currentFileDir;
        _pipeline = BuildPipeline();
    }

    private MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers()
            .UseTaskLists()
            .UseAutoLinks()
            .UseMathematics()
            .DisableHtml();

        builder.Extensions.Add(new WikiLinkExtension(
            _wikiResolver,
            _currentFileDir ?? (() => null)));

        return builder.Build();
    }

    public string ConvertToHtml(string markdown, string baseDirectory)
    {
        var html = ConvertWithDataLine(markdown);
        html = RewriteRelativeLinks(html, baseDirectory);
        // Mermaid + KaTeX são carregados via virtual host mapping "mm.local"
        // (registrado no MarkdownPreview) porque NavigateToString tem cap de ~2MB,
        // e os scripts inline ultrapassam esse limite. CSS continua inline porque cabe.
        return _htmlTemplate
            .Replace("{{CSS}}", _css)
            .Replace("{{KATEX_CSS}}", _katexCss)
            .Replace("{{CONTENT}}", html);
    }

    public string ConvertToHtmlFragment(string markdown, string baseDirectory)
    {
        var html = ConvertWithDataLine(markdown);
        return RewriteRelativeLinks(html, baseDirectory);
    }

    private string ConvertWithDataLine(string markdown)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);

        var document = Markdig.Markdown.Parse(markdown, _pipeline);

        foreach (var block in document)
        {
            if (block is HeadingBlock or ParagraphBlock or ListBlock or QuoteBlock
                or CodeBlock or ThematicBreakBlock or Markdig.Extensions.Tables.Table)
            {
                block.GetAttributes().AddProperty("data-line", (block.Line + 1).ToString());
            }
        }

        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    public List<Heading> ExtractHeadings(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return new List<Heading>();

        var document = Markdig.Markdown.Parse(markdown, _pipeline);
        var results = new List<Heading>();

        foreach (var block in document.Descendants<HeadingBlock>())
        {
            var text = block.Inline?.FirstChild is null
                ? string.Empty
                : ExtractInlineText(block.Inline);
            var anchorId = SlugifyGfm(text);
            results.Add(new Heading(block.Level, text, block.Line + 1, anchorId));
        }

        return results;
    }

    private static string ExtractInlineText(Markdig.Syntax.Inlines.ContainerInline container)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in container)
        {
            if (inline is Markdig.Syntax.Inlines.LiteralInline literal)
                sb.Append(literal.Content.ToString());
            else if (inline is Markdig.Syntax.Inlines.ContainerInline childContainer)
                sb.Append(ExtractInlineText(childContainer));
        }
        return sb.ToString().Trim();
    }

    private static string SlugifyGfm(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                sb.Append(c);
            else if (char.IsWhiteSpace(c))
                sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private static string RewriteRelativeLinks(string html, string baseDirectory)
    {
        // Reescreve hrefs relativos terminando em .md (com fragment opcional) para
        // o scheme mdnav://. Path e fragment ficam em query params distintos para
        // que o handler de clique consiga separá-los sem ambiguidade.
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            @"href=""(?!https?://)(?!mailto:)(?!#)([^""#]*\.md)(?:#([^""]*))?""",
            match =>
            {
                var path = match.Groups[1].Value;
                var fragment = match.Groups[2].Success ? match.Groups[2].Value : null;
                var encodedPath = Uri.EscapeDataString(path);
                if (string.IsNullOrEmpty(fragment))
                    return $@"href=""mdnav://open?path={encodedPath}""";
                var encodedFragment = Uri.EscapeDataString(fragment);
                return $@"href=""mdnav://open?path={encodedPath}&fragment={encodedFragment}""";
            });
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return string.Empty;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
