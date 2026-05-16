using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class EditorSearchServiceTests
{
    [Fact]
    public void FindMatches_CaseInsensitive_FindsAllOccurrences()
    {
        var service = new EditorSearchService();
        var text = "Hello world. HELLO Markdown. hello again.";

        var matches = service.FindMatches(text, "hello", caseSensitive: false, useRegex: false, wholeWord: false);

        Assert.Equal(3, matches.Count);
        Assert.Equal(0, matches[0].Start);
        Assert.Equal(5, matches[0].Length);
    }

    [Fact]
    public void FindMatches_CaseSensitive_FindsOnlyMatchingCase()
    {
        var service = new EditorSearchService();
        var text = "Hello world. HELLO Markdown. hello again.";

        var matches = service.FindMatches(text, "hello", caseSensitive: true, useRegex: false, wholeWord: false);

        Assert.Single(matches);
        Assert.Equal(29, matches[0].Start);
    }

    [Fact]
    public void FindMatches_Regex_AppliesPattern()
    {
        var service = new EditorSearchService();
        var text = "foo123 bar456 baz789";

        var matches = service.FindMatches(text, @"\w+\d+", caseSensitive: false, useRegex: true, wholeWord: false);

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void FindMatches_WholeWord_RespectsBoundaries()
    {
        var service = new EditorSearchService();
        var text = "test testing tested";

        var matches = service.FindMatches(text, "test", caseSensitive: false, useRegex: false, wholeWord: true);

        Assert.Single(matches);
        Assert.Equal(0, matches[0].Start);
    }

    [Fact]
    public void FindMatches_EmptyQuery_ReturnsEmpty()
    {
        var service = new EditorSearchService();
        var matches = service.FindMatches("anything", "", caseSensitive: false, useRegex: false, wholeWord: false);
        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_InvalidRegex_ReturnsEmptyDoesNotThrow()
    {
        var service = new EditorSearchService();
        var matches = service.FindMatches("anything", "[invalid", caseSensitive: false, useRegex: true, wholeWord: false);
        Assert.Empty(matches);
    }
}
