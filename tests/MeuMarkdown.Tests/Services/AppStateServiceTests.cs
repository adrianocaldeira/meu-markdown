using System.IO;
using MeuMarkdown.Models;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class AppStateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stateFile;

    public AppStateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeuMarkdownTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _stateFile = Path.Combine(_tempDir, "state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultState()
    {
        var service = new AppStateService(_stateFile);

        var state = service.Load();

        Assert.NotNull(state);
        Assert.Equal(1200, state.Window.Width);
        Assert.Equal(700, state.Window.Height);
        Assert.Equal("Explorer", state.Sidebar.ActivePanel);
        Assert.Equal(280, state.Sidebar.Width);
        Assert.False(state.Sidebar.Collapsed);
    }
}
