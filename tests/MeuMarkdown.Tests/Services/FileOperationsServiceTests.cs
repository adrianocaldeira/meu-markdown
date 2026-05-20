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

    [Fact]
    public void CopyFile_NoConflict_CopiesToDest()
    {
        var src = Path.Combine(_tmpDir, "src.md");
        File.WriteAllText(src, "conteúdo");
        var destDir = Path.Combine(_tmpDir, "sub");
        Directory.CreateDirectory(destDir);

        var svc = new FileOperationsService();
        var result = svc.CopyFile(src, destDir, onConflict: null);

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(destDir, "src.md"), result);
        Assert.True(File.Exists(src), "Original deve continuar existindo");
        Assert.Equal("conteúdo", File.ReadAllText(result));
    }

    [Fact]
    public void CopyFile_SameDir_AutoIncrementsName()
    {
        var src = Path.Combine(_tmpDir, "foo.md");
        File.WriteAllText(src, "x");

        var svc = new FileOperationsService();
        var result = svc.CopyFile(src, _tmpDir, onConflict: null);

        Assert.EndsWith("foo (2).md", result);
        Assert.True(File.Exists(src));
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void CopyFile_DestExistsAndCallbackReturnsFalse_ReturnsNull()
    {
        var src = Path.Combine(_tmpDir, "foo.md");
        File.WriteAllText(src, "src");
        var destDir = Path.Combine(_tmpDir, "sub");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "foo.md"), "dest-existing");

        var svc = new FileOperationsService();
        var result = svc.CopyFile(src, destDir, onConflict: _ => false);

        Assert.Null(result);
        Assert.Equal("dest-existing", File.ReadAllText(Path.Combine(destDir, "foo.md")));
    }

    [Fact]
    public void CopyFile_DestExistsAndCallbackReturnsTrue_Overwrites()
    {
        var src = Path.Combine(_tmpDir, "foo.md");
        File.WriteAllText(src, "new-content");
        var destDir = Path.Combine(_tmpDir, "sub");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "foo.md"), "old-content");

        var svc = new FileOperationsService();
        var result = svc.CopyFile(src, destDir, onConflict: _ => true);

        Assert.NotNull(result);
        Assert.Equal("new-content", File.ReadAllText(result));
    }

    [Fact]
    public void MoveFile_NoConflict_MovesToDest()
    {
        var src = Path.Combine(_tmpDir, "src.md");
        File.WriteAllText(src, "x");
        var destDir = Path.Combine(_tmpDir, "sub");
        Directory.CreateDirectory(destDir);

        var svc = new FileOperationsService();
        var result = svc.MoveFile(src, destDir, onConflict: null);

        Assert.NotNull(result);
        Assert.False(File.Exists(src), "Original NÃO deve existir após move");
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void MoveFile_DestExistsAndCallbackReturnsTrue_Overwrites()
    {
        var src = Path.Combine(_tmpDir, "foo.md");
        File.WriteAllText(src, "new");
        var destDir = Path.Combine(_tmpDir, "sub");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "foo.md"), "old");

        var svc = new FileOperationsService();
        var result = svc.MoveFile(src, destDir, onConflict: _ => true);

        Assert.NotNull(result);
        Assert.False(File.Exists(src));
        Assert.Equal("new", File.ReadAllText(result));
    }
}
