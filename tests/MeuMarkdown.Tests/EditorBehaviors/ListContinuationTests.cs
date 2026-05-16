using MeuMarkdown.EditorBehaviors;
using Xunit;

namespace MeuMarkdown.Tests.EditorBehaviors;

public class ListContinuationTests
{
    [Theory]
    [InlineData("- item", "- ")]
    [InlineData("* item", "* ")]
    [InlineData("+ item", "+ ")]
    [InlineData("  - aninhado", "  - ")]
    [InlineData("    * mais aninhado", "    * ")]
    public void Compute_UnorderedList_ReturnsSameMarker(string current, string expected)
    {
        var result = ListContinuation.Compute(current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1. primeiro", "2. ")]
    [InlineData("9. nono", "10. ")]
    [InlineData("99. ", null)]
    [InlineData("  3. aninhado", "  4. ")]
    public void Compute_OrderedList_IncrementsNumber(string current, string? expected)
    {
        var result = ListContinuation.Compute(current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("- [ ] task", "- [ ] ")]
    [InlineData("- [x] done", "- [ ] ")]
    [InlineData("* [X] CONCLUIDO", "* [ ] ")]
    public void Compute_Checkbox_ResetsState(string current, string expected)
    {
        var result = ListContinuation.Compute(current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("> citação", "> ")]
    [InlineData(">> aninhada", ">> ")]
    public void Compute_Blockquote_ContinuesPrefix(string current, string expected)
    {
        var result = ListContinuation.Compute(current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("* ")]
    [InlineData("1. ")]
    [InlineData("- [ ] ")]
    [InlineData("> ")]
    public void Compute_EmptyMarker_ReturnsNullToExit(string current)
    {
        var result = ListContinuation.Compute(current);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("texto normal")]
    [InlineData("")]
    [InlineData("    indentado mas não lista")]
    public void Compute_NotAList_ReturnsEmpty(string current)
    {
        var result = ListContinuation.Compute(current);
        Assert.Equal("", result);
    }
}
