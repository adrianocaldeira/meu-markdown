using System.IO;
using System.Reflection;
using Markdig;

namespace MeuMarkdown.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly string _htmlTemplate;
    private readonly string _css;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers()
            .UseTaskLists()
            .UseAutoLinks()
            .Build();

        _htmlTemplate = LoadEmbeddedResource("MeuMarkdown.Resources.preview-template.html");
        _css = LoadEmbeddedResource("MeuMarkdown.Resources.github-markdown.css");
    }

    public string ConvertToHtml(string markdown, string baseDirectory)
    {
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        html = RewriteRelativeLinks(html, baseDirectory);
        return _htmlTemplate
            .Replace("{{CSS}}", _css)
            .Replace("{{CONTENT}}", html);
    }

    public string ConvertToHtmlFragment(string markdown, string baseDirectory)
    {
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return RewriteRelativeLinks(html, baseDirectory);
    }

    private static string RewriteRelativeLinks(string html, string baseDirectory)
    {
        // Rewrite relative .md links to use custom scheme
        // Pattern: href="something.md" or href="path/to/file.md"
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            @"href=""(?!https?://)(?!mailto:)(?!#)([^""]*\.md(?:#[^""]*)?)""",
            match =>
            {
                var relativePath = match.Groups[1].Value;
                var encodedPath = Uri.EscapeDataString(relativePath);
                return $@"href=""mdnav://open?path={encodedPath}""";
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
