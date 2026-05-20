using System.IO;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class FileOperationsServiceTests : IDisposable
{
    private readonly string _tmpDir;

    public FileOperationsServiceTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(),
            "mm-fileops-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    [Fact]
    public void GenerateUniqueName_NoConflict_ReturnsBaseName()
    {
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "foo", ".md");
        Assert.Equal("foo.md", name);
    }

    [Fact]
    public void GenerateUniqueName_OneConflict_ReturnsWithSuffix2()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "foo.md"), "");
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "foo", ".md");
        Assert.Equal("foo (2).md", name);
    }

    [Fact]
    public void GenerateUniqueName_TwoConflicts_ReturnsWithSuffix3()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "foo.md"), "");
        File.WriteAllText(Path.Combine(_tmpDir, "foo (2).md"), "");
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "foo", ".md");
        Assert.Equal("foo (3).md", name);
    }

    [Fact]
    public void GenerateUniqueName_NoExtension_TreatsAsFolder()
    {
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "nova-pasta", null);
        Assert.Equal("nova-pasta", name);
    }

    [Fact]
    public void GenerateUniqueName_FolderConflict_ReturnsWithSuffix()
    {
        Directory.CreateDirectory(Path.Combine(_tmpDir, "nova-pasta"));
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "nova-pasta", null);
        Assert.Equal("nova-pasta (2)", name);
    }

    [Fact]
    public void GenerateUniqueName_ExtensionWithoutDot_NormalizesIt()
    {
        var name = FileOperationsService.GenerateUniqueName(_tmpDir, "foo", "md");
        Assert.Equal("foo.md", name);
    }

    [Fact]
    public void CreateNewFile_NewFile_CreatesEmptyFile()
    {
        var svc = new FileOperationsService();
        var path = svc.CreateNewFile(_tmpDir, "novo", ".md");

        Assert.True(File.Exists(path));
        Assert.EndsWith("novo.md", path);
        Assert.Equal(string.Empty, File.ReadAllText(path));
    }

    [Fact]
    public void CreateNewFile_AlreadyExists_AppendsSuffix()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "novo.md"), "");
        var svc = new FileOperationsService();
        var path = svc.CreateNewFile(_tmpDir, "novo", ".md");

        Assert.EndsWith("novo (2).md", path);
    }

    [Fact]
    public void CreateNewFolder_NewFolder_CreatesDirectory()
    {
        var svc = new FileOperationsService();
        var path = svc.CreateNewFolder(_tmpDir, "minha-pasta");

        Assert.True(Directory.Exists(path));
        Assert.EndsWith("minha-pasta", path);
    }

    [Fact]
    public void CreateNewFolder_AlreadyExists_AppendsSuffix()
    {
        Directory.CreateDirectory(Path.Combine(_tmpDir, "minha-pasta"));
        var svc = new FileOperationsService();
        var path = svc.CreateNewFolder(_tmpDir, "minha-pasta");

        Assert.EndsWith("minha-pasta (2)", path);
    }
}
