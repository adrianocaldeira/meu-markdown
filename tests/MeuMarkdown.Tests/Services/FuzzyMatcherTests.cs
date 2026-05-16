using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class FuzzyMatcherTests
{
    [Fact]
    public void Score_ExactMatch_GetsHighScore()
    {
        var s1 = FuzzyMatcher.Score("readme", "readme");
        var s2 = FuzzyMatcher.Score("readme", "readme.md");
        Assert.True(s1 > 500);
        Assert.True(s2 >= s1 - 200);
    }

    [Fact]
    public void Score_SubsequenceMatch_ReturnsPositive()
    {
        var score = FuzzyMatcher.Score("rdm", "readme.md");
        Assert.True(score > 0);
    }

    [Fact]
    public void Score_NoMatch_ReturnsZero()
    {
        var score = FuzzyMatcher.Score("xyz", "readme.md");
        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_EmptyQuery_ReturnsZero()
    {
        var score = FuzzyMatcher.Score("", "readme.md");
        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_ConsecutiveMatches_ScoresBetterThanScattered()
    {
        var consecutive = FuzzyMatcher.Score("read", "readme");
        var scattered = FuzzyMatcher.Score("read", "rXeXaXdX");
        Assert.True(consecutive > scattered);
    }

    [Fact]
    public void Score_CaseInsensitive()
    {
        var lower = FuzzyMatcher.Score("readme", "README.MD");
        var upper = FuzzyMatcher.Score("README", "readme.md");
        Assert.True(lower > 0);
        Assert.True(upper > 0);
    }

    [Fact]
    public void Score_ExactSubstring_BeatsSubsequence()
    {
        var substring = FuzzyMatcher.Score("setup", "setup.md");
        var subsequence = FuzzyMatcher.Score("stp", "setup.md");
        Assert.True(substring > subsequence);
    }
}
