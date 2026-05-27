using MeuMarkdown.Controls;
using Xunit;

namespace MeuMarkdown.Tests.Controls;

public class FindRoutingTests
{
    [Fact]
    public void Resolve_ModoVisualizacao_RetornaPreview()
    {
        Assert.Equal(FindTarget.Preview, FindRouting.Resolve(isViewMode: true, previewFocused: false));
    }

    [Fact]
    public void Resolve_Split_PreviewFocado_RetornaPreview()
    {
        Assert.Equal(FindTarget.Preview, FindRouting.Resolve(isViewMode: false, previewFocused: true));
    }

    [Fact]
    public void Resolve_Split_EditorFocado_RetornaEditor()
    {
        Assert.Equal(FindTarget.Editor, FindRouting.Resolve(isViewMode: false, previewFocused: false));
    }
}
