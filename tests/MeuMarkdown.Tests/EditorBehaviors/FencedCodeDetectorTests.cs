using MeuMarkdown.EditorBehaviors;
using Xunit;

namespace MeuMarkdown.Tests.EditorBehaviors;

public class FencedCodeDetectorTests
{
    [Fact]
    public void IsInsideFencedCode_NoFences_ReturnsFalse()
    {
        var text = "linha 1\nlinha 2\nlinha 3";
        Assert.False(FencedCodeDetector.IsInsideFencedCode(text, offset: 20));
    }

    [Fact]
    public void IsInsideFencedCode_AfterOpeningFence_ReturnsTrue()
    {
        var text = "texto\n```csharp\nvar x = 1;\n";
        var offset = text.IndexOf("var x") + 5;
        Assert.True(FencedCodeDetector.IsInsideFencedCode(text, offset));
    }

    [Fact]
    public void IsInsideFencedCode_AfterClosingFence_ReturnsFalse()
    {
        var text = "texto\n```\ncode\n```\ndepois";
        var offset = text.IndexOf("depois");
        Assert.False(FencedCodeDetector.IsInsideFencedCode(text, offset));
    }

    [Fact]
    public void IsInsideFencedCode_InsideTildeFence_ReturnsTrue()
    {
        var text = "~~~\ncode here\n";
        var offset = text.IndexOf("code") + 4;
        Assert.True(FencedCodeDetector.IsInsideFencedCode(text, offset));
    }

    [Fact]
    public void IsInsideFencedCode_OffsetZero_ReturnsFalse()
    {
        Assert.False(FencedCodeDetector.IsInsideFencedCode("```\ncode", offset: 0));
    }

    [Fact]
    public void IsInsideFencedCode_TwoOpenFences_LastOneCounts()
    {
        var text = "```\nfirst\n```\n\n```\nsecond\n";
        var offset = text.IndexOf("second") + 6;
        Assert.True(FencedCodeDetector.IsInsideFencedCode(text, offset));
    }
}
