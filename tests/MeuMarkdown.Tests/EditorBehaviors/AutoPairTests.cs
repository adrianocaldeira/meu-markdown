using MeuMarkdown.EditorBehaviors;
using Xunit;

namespace MeuMarkdown.Tests.EditorBehaviors;

public class AutoPairTests
{
    [Theory]
    [InlineData("[", "]")]
    [InlineData("(", ")")]
    [InlineData("`", "`")]
    public void GetClosing_KnownOpeners_ReturnsClosing(string opener, string expected)
    {
        Assert.Equal(expected, AutoPair.GetClosing(opener));
    }

    [Fact]
    public void GetClosing_UnknownOpener_ReturnsNull()
    {
        Assert.Null(AutoPair.GetClosing("x"));
    }

    [Fact]
    public void ShouldAutoPair_UnderscoreAfterLetter_ReturnsFalse()
    {
        Assert.False(AutoPair.ShouldAutoPair("_", "snake", caretOffsetInLine: 5));
    }

    [Fact]
    public void ShouldAutoPair_UnderscoreAtWordStart_ReturnsTrue()
    {
        Assert.True(AutoPair.ShouldAutoPair("_", "uma frase ", caretOffsetInLine: 10));
        Assert.True(AutoPair.ShouldAutoPair("_", "", caretOffsetInLine: 0));
    }

    [Fact]
    public void ShouldAutoPair_OtherChars_AlwaysTrue()
    {
        Assert.True(AutoPair.ShouldAutoPair("[", "any context", caretOffsetInLine: 5));
        Assert.True(AutoPair.ShouldAutoPair("`", "letter`", caretOffsetInLine: 7));
    }
}
