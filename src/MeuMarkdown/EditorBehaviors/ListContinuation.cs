using System.Text.RegularExpressions;

namespace MeuMarkdown.EditorBehaviors;

public static class ListContinuation
{
    private static readonly Regex _checkbox = new(@"^(\s*)([-*+])\s\[[ xX]\]\s(.*)$", RegexOptions.Compiled);
    private static readonly Regex _bullet = new(@"^(\s*)([-*+])\s(.*)$", RegexOptions.Compiled);
    private static readonly Regex _numbered = new(@"^(\s*)(\d+)\.\s(.*)$", RegexOptions.Compiled);
    private static readonly Regex _quote = new(@"^(\s*)(>+)\s?(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Calcula o prefixo a ser inserido após Enter para continuar uma lista.
    /// </summary>
    public static string? Compute(string currentLine)
    {
        var checkboxMatch = _checkbox.Match(currentLine);
        if (checkboxMatch.Success)
        {
            var content = checkboxMatch.Groups[3].Value;
            if (string.IsNullOrEmpty(content))
                return null;
            return $"{checkboxMatch.Groups[1].Value}{checkboxMatch.Groups[2].Value} [ ] ";
        }

        var bulletMatch = _bullet.Match(currentLine);
        if (bulletMatch.Success)
        {
            var content = bulletMatch.Groups[3].Value;
            if (string.IsNullOrEmpty(content))
                return null;
            return $"{bulletMatch.Groups[1].Value}{bulletMatch.Groups[2].Value} ";
        }

        var numberedMatch = _numbered.Match(currentLine);
        if (numberedMatch.Success)
        {
            var content = numberedMatch.Groups[3].Value;
            if (string.IsNullOrEmpty(content))
                return null;
            if (int.TryParse(numberedMatch.Groups[2].Value, out var n))
                return $"{numberedMatch.Groups[1].Value}{n + 1}. ";
            return "";
        }

        var quoteMatch = _quote.Match(currentLine);
        if (quoteMatch.Success)
        {
            var content = quoteMatch.Groups[3].Value;
            if (string.IsNullOrEmpty(content))
                return null;
            return $"{quoteMatch.Groups[1].Value}{quoteMatch.Groups[2].Value} ";
        }

        return "";
    }
}
