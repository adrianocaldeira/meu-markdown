using System.Text.RegularExpressions;

namespace MeuMarkdown.Services;

public readonly record struct SearchMatch(int Start, int Length)
{
    public int End => Start + Length;
}

public class EditorSearchService
{
    public List<SearchMatch> FindMatches(string text, string query, bool caseSensitive, bool useRegex, bool wholeWord)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return new List<SearchMatch>();

        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            options |= RegexOptions.Multiline;

            string pattern;
            if (useRegex)
                pattern = query;
            else
                pattern = Regex.Escape(query);

            if (wholeWord)
                pattern = $@"\b{pattern}\b";

            var matches = Regex.Matches(text, pattern, options, TimeSpan.FromMilliseconds(500));
            var result = new List<SearchMatch>(matches.Count);
            foreach (Match m in matches)
            {
                if (m.Success && m.Length > 0)
                    result.Add(new SearchMatch(m.Index, m.Length));
            }
            return result;
        }
        catch (Exception ex) when (ex is RegexParseException or RegexMatchTimeoutException or ArgumentException)
        {
            return new List<SearchMatch>();
        }
    }
}
