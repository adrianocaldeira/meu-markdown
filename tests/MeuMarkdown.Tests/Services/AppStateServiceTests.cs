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

    [Fact]
    public void Save_ThenLoad_PreservesAllFields()
    {
        var service = new AppStateService(_stateFile);
        var original = new AppState
        {
            Window = { X = 50, Y = 80, Width = 1400, Height = 900, Maximized = true },
            Sidebar = { ActivePanel = "Outline", Width = 320, Collapsed = true, ActivityBarVisible = false },
            LastWorkspace = @"C:\notes",
            OpenTabs = new List<string> { @"C:\notes\a.md", @"C:\notes\b.md" },
            ActiveTab = @"C:\notes\a.md",
            RecentFiles = new List<string> { @"C:\notes\a.md" },
            Preferences = { SyncScrollEnabled = true, TypewriterMode = true, ExplorerShowAllFiles = true }
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.Equal(50, loaded.Window.X);
        Assert.Equal(1400, loaded.Window.Width);
        Assert.True(loaded.Window.Maximized);
        Assert.Equal("Outline", loaded.Sidebar.ActivePanel);
        Assert.Equal(320, loaded.Sidebar.Width);
        Assert.True(loaded.Sidebar.Collapsed);
        Assert.False(loaded.Sidebar.ActivityBarVisible);
        Assert.Equal(@"C:\notes", loaded.LastWorkspace);
        Assert.Equal(2, loaded.OpenTabs.Count);
        Assert.Equal(@"C:\notes\a.md", loaded.ActiveTab);
        Assert.True(loaded.Preferences.SyncScrollEnabled);
        Assert.True(loaded.Preferences.TypewriterMode);
    }

    [Fact]
    public void Load_WhenJsonCorrupt_ReturnsDefaultStateWithoutThrowing()
    {
        File.WriteAllText(_stateFile, "{ this is not valid json :::");
        var service = new AppStateService(_stateFile);

        var state = service.Load();

        Assert.NotNull(state);
        Assert.Equal(1200, state.Window.Width);
        Assert.Equal("Explorer", state.Sidebar.ActivePanel);
    }
}
