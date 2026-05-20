using System.IO;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;


public class WorkspaceServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MeuMarkdownWS-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Open_ListsOnlyMarkdownFiles_ByDefault()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.md"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.markdown"), "");
        File.WriteAllText(Path.Combine(_tempDir, "c.txt"), "");

        var svc = new WorkspaceService();
        svc.Open(_tempDir, showAllFiles: false);

        Assert.NotNull(svc.Root);
        var fileNames = svc.Root!.Children.Where(c => !c.IsDirectory).Select(c => c.Name).ToList();
        Assert.Contains("a.md", fileNames);
        Assert.Contains("b.markdown", fileNames);
        Assert.DoesNotContain("c.txt", fileNames);
    }

    [Fact]
    public void Open_ShowAllFiles_IncludesNonMarkdown()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.md"), "");
        File.WriteAllText(Path.Combine(_tempDir, "c.txt"), "");

        var svc = new WorkspaceService();
        svc.Open(_tempDir, showAllFiles: true);

        var fileNames = svc.Root!.Children.Where(c => !c.IsDirectory).Select(c => c.Name).ToList();
        Assert.Contains("a.md", fileNames);
        Assert.Contains("c.txt", fileNames);
    }

    [Fact]
    public void Open_HiddenDirectories_AlwaysExcluded()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "docs"));
        File.WriteAllText(Path.Combine(_tempDir, "docs", "x.md"), "");

        var svc = new WorkspaceService();
        svc.Open(_tempDir, showAllFiles: true);

        var dirNames = svc.Root!.Children.Where(c => c.IsDirectory).Select(c => c.Name).ToList();
        Assert.DoesNotContain(".git", dirNames);
        Assert.DoesNotContain("node_modules", dirNames);
        Assert.Contains("docs", dirNames);
    }

    [Fact]
    public void EnumerateMarkdownFiles_RecursesAll()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "guides"));
        File.WriteAllText(Path.Combine(_tempDir, "root.md"), "");
        File.WriteAllText(Path.Combine(_tempDir, "guides", "nested.md"), "");

        var svc = new WorkspaceService();
        svc.Open(_tempDir, showAllFiles: false);

        var all = svc.EnumerateMarkdownFiles().ToList();
        Assert.Contains(all, p => p.EndsWith("root.md"));
        Assert.Contains(all, p => p.EndsWith("nested.md"));
    }

    [Fact]
    public void Open_EmptyPath_ResetsRootToNull()
    {
        var svc = new WorkspaceService();
        svc.Open(_tempDir, showAllFiles: false);
        Assert.NotNull(svc.Root);

        svc.Close();
        Assert.Null(svc.Root);
    }

    // ── ResolveWikiLink tests ──────────────────────────────────────────────

    [Fact]
    public void ResolveWikiLink_SimpleName_FindsByFilename()
    {
        var tmp = CreateSubWorkspace(("Foo.md", ""), ("Bar.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("Foo", currentFileDir: tmp);

        Assert.NotNull(result);
        Assert.EndsWith("Foo.md", result!.AbsolutePath);
    }

    [Fact]
    public void ResolveWikiLink_CaseInsensitive()
    {
        var tmp = CreateSubWorkspace(("Foo.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("foo", currentFileDir: tmp);

        Assert.NotNull(result);
    }

    [Fact]
    public void ResolveWikiLink_NotFound_ReturnsNull()
    {
        var tmp = CreateSubWorkspace(("Foo.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("Inexistente", currentFileDir: tmp);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveWikiLink_PathWithSlash_ResolvesRelativeToCurrent()
    {
        var tmp = CreateSubWorkspace(("Sub/Bar.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("Sub/Bar", currentFileDir: tmp);

        Assert.NotNull(result);
        Assert.EndsWith("Bar.md", result!.AbsolutePath);
    }

    [Fact]
    public void ResolveWikiLink_PathWithSlashAndMdExtension_Works()
    {
        var tmp = CreateSubWorkspace(("Sub/Bar.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("Sub/Bar.md", currentFileDir: tmp);

        Assert.NotNull(result);
    }

    [Fact]
    public void ResolveWikiLink_MultipleCandidates_PrefersClosestByLca()
    {
        var tmp = CreateSubWorkspace(
            ("area-a/sub/current.md", ""),
            ("area-a/Foo.md", ""),
            ("area-b/Foo.md", ""));
        var ws = new WorkspaceService();
        ws.Open(tmp, showAllFiles: false);

        var result = ws.ResolveWikiLink("Foo",
            currentFileDir: Path.Combine(tmp, "area-a", "sub"));

        Assert.NotNull(result);
        Assert.Contains("area-a", result!.AbsolutePath);
        Assert.DoesNotContain("area-b", result.AbsolutePath);
    }

    [Fact]
    public void ResolveWikiLink_NoWorkspaceOpen_ReturnsNull()
    {
        var ws = new WorkspaceService();

        var result = ws.ResolveWikiLink("Foo", currentFileDir: null);

        Assert.Null(result);
    }

    /// <summary>
    /// Cria um sub-diretório dentro do _tempDir com os arquivos especificados.
    /// </summary>
    private string CreateSubWorkspace(params (string relPath, string content)[] files)
    {
        var dir = Path.Combine(_tempDir, "ws-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        return dir;
    }
}
