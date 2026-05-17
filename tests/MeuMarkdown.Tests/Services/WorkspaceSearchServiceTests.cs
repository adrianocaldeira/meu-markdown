using System.IO;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class WorkspaceSearchServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceSearchServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeuMarkdownSearch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var result = svc.Search(new[] { WriteFile("a.md", "hello world") }, "", false, false, false);
        Assert.Empty(result);
    }

    [Fact]
    public void Search_MatchInSingleFile_ReturnsOneFileResult()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var path = WriteFile("a.md", "hello world\nfoo bar\nhello again\n");

        var result = svc.Search(new[] { path }, "hello", false, false, false);

        Assert.Single(result);
        Assert.Equal(path, result[0].FilePath);
        Assert.Equal(2, result[0].Matches.Count);
        Assert.Equal(1, result[0].Matches[0].LineNumber);
        Assert.Equal("hello world", result[0].Matches[0].LineText);
        Assert.Equal(3, result[0].Matches[1].LineNumber);
    }

    [Fact]
    public void Search_MultipleFiles_ReturnsOneResultPerMatchingFile()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var a = WriteFile("a.md", "alpha\nbravo\n");
        var b = WriteFile("b.md", "charlie\nbravo\n");
        var c = WriteFile("c.md", "delta\necho\n");

        var result = svc.Search(new[] { a, b, c }, "bravo", false, false, false);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.FilePath == a);
        Assert.Contains(result, r => r.FilePath == b);
        Assert.DoesNotContain(result, r => r.FilePath == c);
    }

    [Fact]
    public void Search_RespectsCaseSensitive()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var path = WriteFile("a.md", "Hello\nhello\nHELLO\n");

        var insensitive = svc.Search(new[] { path }, "hello", false, false, false);
        var sensitive = svc.Search(new[] { path }, "hello", true, false, false);

        Assert.Equal(3, insensitive[0].Matches.Count);
        Assert.Single(sensitive[0].Matches);
        Assert.Equal(2, sensitive[0].Matches[0].LineNumber);
    }

    [Fact]
    public void Search_RegexMode()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var path = WriteFile("a.md", "foo123\nbar\nfoo456\n");

        var result = svc.Search(new[] { path }, @"foo\d+", false, true, false);

        Assert.Single(result);
        Assert.Equal(2, result[0].Matches.Count);
    }

    [Fact]
    public void Search_MissingFile_SkipsGracefully()
    {
        var svc = new WorkspaceSearchService(new EditorSearchService());
        var existing = WriteFile("a.md", "hello\n");
        var missing = Path.Combine(_tempDir, "does-not-exist.md");

        var result = svc.Search(new[] { existing, missing }, "hello", false, false, false);

        Assert.Single(result);
        Assert.Equal(existing, result[0].FilePath);
    }
}
