using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Services;

public class ExportService
{
    private readonly MarkdownService _markdownService;

    // CSS injetado em export light pra blindar contra Auto Dark Mode do Chromium / WebView2
    // que pode aplicar dark scheme automaticamente baseado no tema do Windows mesmo quando
    // o HTML não tem class="dark". Usa !important pra vencer qualquer cascade subsequente.
    private const string LightModeForceCss = @"
:root { color-scheme: light only !important; }
html, body { background: #ffffff !important; color: #1f2328 !important; }
body.markdown-body, .markdown-body { background: #ffffff !important; color: #1f2328 !important; }
.markdown-body code { background: #f3f4f6 !important; color: #1f2328 !important; }
.markdown-body pre { background: #f6f8fa !important; color: #1f2328 !important; border-color: #e5e7eb !important; }
.markdown-body th { background: #f6f8fa !important; color: #1f2328 !important; }
.markdown-body tr:nth-child(even) td { background: #fafbfc !important; }
.markdown-body blockquote { color: #6b7280 !important; }
.markdown-body a { color: #0969da !important; }
";

    public ExportService(MarkdownService markdownService)
    {
        _markdownService = markdownService;
    }

    public void ExportHtml(DocumentTabViewModel tab, string destPath, bool darkTheme, bool convertMdLinksToHtml)
    {
        var content = tab.GetDocument().Content;
        var baseDir = tab.Directory;
        var html = _markdownService.ConvertToHtmlFragment(content, baseDir);

        html = Regex.Replace(html, @"href=""mdnav://open\?path=([^""&]+)(?:&fragment=([^""]*))?""", m =>
        {
            var path = Uri.UnescapeDataString(m.Groups[1].Value);
            var fragment = m.Groups[2].Success ? Uri.UnescapeDataString(m.Groups[2].Value) : null;
            if (convertMdLinksToHtml)
                path = Path.ChangeExtension(path, ".html");
            return string.IsNullOrEmpty(fragment)
                ? $@"href=""{path}"""
                : $@"href=""{path}#{fragment}""";
        });

        var css = LoadEmbeddedCss();
        var bodyClass = darkTheme ? "dark" : "";

        var fullHtml = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
<meta charset=""UTF-8"">
<meta name=""color-scheme"" content=""{(darkTheme ? "dark" : "light")} only"">
<title>{System.Web.HttpUtility.HtmlEncode(tab.FileName)}</title>
<style>
{css}
{(darkTheme ? "" : LightModeForceCss)}
</style>
</head>
<body class=""{bodyClass}"">
<div class=""markdown-body"">
{html}
</div>
</body>
</html>";

        File.WriteAllText(destPath, fullHtml, Encoding.UTF8);
    }

    private static string LoadEmbeddedCss()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("MeuMarkdown.Resources.preview-template.html");
        if (stream == null) return string.Empty;
        using var reader = new StreamReader(stream);
        var template = reader.ReadToEnd();

        var firstStyleEnd = template.IndexOf("</style>", StringComparison.Ordinal);
        if (firstStyleEnd < 0) return string.Empty;
        var secondStyleStart = template.IndexOf("<style>", firstStyleEnd, StringComparison.Ordinal);
        if (secondStyleStart < 0) return string.Empty;
        var secondStyleEnd = template.IndexOf("</style>", secondStyleStart, StringComparison.Ordinal);
        if (secondStyleEnd < 0) return string.Empty;

        return template.Substring(secondStyleStart + "<style>".Length,
            secondStyleEnd - (secondStyleStart + "<style>".Length));
    }

    public string CreateTempHtmlForPrint(DocumentTabViewModel tab, bool darkTheme)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"meumarkdown-export-{Guid.NewGuid():N}.html");
        ExportHtml(tab, tempPath, darkTheme, convertMdLinksToHtml: false);
        return tempPath;
    }
}
