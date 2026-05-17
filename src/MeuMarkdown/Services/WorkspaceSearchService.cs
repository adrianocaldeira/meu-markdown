using System.IO;

namespace MeuMarkdown.Services;

public record FileSearchMatch(int LineNumber, string LineText, int MatchStart, int MatchLength);
public record FileSearchResult(string FilePath, IReadOnlyList<FileSearchMatch> Matches);

public class WorkspaceSearchService
{
    private readonly EditorSearchService _editorSearch;

    public WorkspaceSearchService(EditorSearchService editorSearch)
    {
        _editorSearch = editorSearch;
    }

    public List<FileSearchResult> Search(IEnumerable<string> files, string query, bool caseSensitive, bool useRegex, bool wholeWord)
    {
        var results = new List<FileSearchResult>();
        if (string.IsNullOrEmpty(query)) return results;

        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var matches = _editorSearch.FindMatches(text, query, caseSensitive, useRegex, wholeWord);
            if (matches.Count == 0) continue;

            var fileMatches = new List<FileSearchMatch>(matches.Count);
            foreach (var m in matches)
            {
                var (lineNumber, lineStart) = FindLine(text, m.Start);
                var lineEnd = text.IndexOf('\n', m.Start);
                if (lineEnd < 0) lineEnd = text.Length;
                var lineText = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
                fileMatches.Add(new FileSearchMatch(lineNumber, lineText, m.Start - lineStart, m.Length));
            }

            results.Add(new FileSearchResult(file, fileMatches));
        }

        return results;
    }

    private static (int lineNumber, int lineStart) FindLine(string text, int offset)
    {
        int line = 1, lineStart = 0;
        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, lineStart);
    }
}
