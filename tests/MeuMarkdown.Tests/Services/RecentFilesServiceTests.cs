using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class RecentFilesServiceTests
{
    [Fact]
    public void Add_NewFile_AppearsAtTop()
    {
        var svc = new RecentFilesService();

        svc.Add(@"C:\notes\a.md");
        svc.Add(@"C:\notes\b.md");

        Assert.Equal(2, svc.Items.Count);
        Assert.Equal(@"C:\notes\b.md", svc.Items[0]);
        Assert.Equal(@"C:\notes\a.md", svc.Items[1]);
    }

    [Fact]
    public void Add_ExistingFile_MovesToTopWithoutDuplicating()
    {
        var svc = new RecentFilesService();
        svc.Add(@"C:\notes\a.md");
        svc.Add(@"C:\notes\b.md");

        svc.Add(@"C:\notes\a.md");

        Assert.Equal(2, svc.Items.Count);
        Assert.Equal(@"C:\notes\a.md", svc.Items[0]);
        Assert.Equal(@"C:\notes\b.md", svc.Items[1]);
    }

    [Fact]
    public void Add_BeyondCapacity_DropsOldest()
    {
        var svc = new RecentFilesService(capacity: 3);
        svc.Add("a.md");
        svc.Add("b.md");
        svc.Add("c.md");
        svc.Add("d.md");

        Assert.Equal(3, svc.Items.Count);
        Assert.Equal("d.md", svc.Items[0]);
        Assert.Equal("c.md", svc.Items[1]);
        Assert.Equal("b.md", svc.Items[2]);
        Assert.DoesNotContain("a.md", svc.Items);
    }

    [Fact]
    public void Add_CaseInsensitive_TreatsAsSameFile()
    {
        var svc = new RecentFilesService();
        svc.Add(@"C:\notes\Foo.md");
        svc.Add(@"c:\notes\foo.md");

        Assert.Single(svc.Items);
    }

    [Fact]
    public void LoadFrom_PopulatesItems()
    {
        var svc = new RecentFilesService();

        svc.LoadFrom(new List<string> { "x.md", "y.md", "z.md" });

        Assert.Equal(3, svc.Items.Count);
        Assert.Equal("x.md", svc.Items[0]);
    }

    [Fact]
    public void Clear_EmptiesItems()
    {
        var svc = new RecentFilesService();
        svc.Add("a.md");

        svc.Clear();

        Assert.Empty(svc.Items);
    }
}
