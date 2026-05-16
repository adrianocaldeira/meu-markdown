using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Services;

public class ExportService
{
    private readonly MarkdownService _markdownService;

    public ExportService(MarkdownService markdownService)
    {
        _markdownService = markdownService;
    }

    public void ExportHtml(DocumentTabViewModel tab, string destPath, bool darkTheme, bool convertMdLinksToHtml)
    {
        var content = tab.GetDocument().Content;
        var baseDir = tab.Directory;
        var html = _markdownService.ConvertToHtmlFragment(content, baseDir);

        html = Regex.Replace(html, @"href=""mdnav://open\?path=([^""]+)""", m =>
        {
            var decoded = Uri.UnescapeDataString(m.Groups[1].Value);
            if (convertMdLinksToHtml)
                decoded = Path.ChangeExtension(decoded, ".html");
            return $@"href=""{decoded}""";
        });

        var css = LoadEmbeddedCss();
        var bodyClass = darkTheme ? "dark" : "";

        var fullHtml = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
<meta charset=""UTF-8"">
<title>{System.Web.HttpUtility.HtmlEncode(tab.FileName)}</title>
<style>
{css}
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
