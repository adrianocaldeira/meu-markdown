using Microsoft.Win32;
using MeuMarkdown.Services;
using MeuMarkdown.Tests.TestHelpers;

namespace MeuMarkdown.Tests.Services;

public class InstallContextTests
{
    private const string SubKey = @"Software\MeuMarkdown";

    [Fact]
    public void Detect_PerUserRegistry_ReturnsPerUser()
    {
        var reg = new FakeRegistryReader()
            .Set(RegistryHive.CurrentUser, SubKey, "InstallScope", "user")
            .Set(RegistryHive.CurrentUser, SubKey, "InstallPath", @"C:\Users\u\AppData\Local\Programs\Meu Markdown");

        var ctx = InstallContext.Detect(reg, basePath: @"C:\Users\u\AppData\Local\Programs\Meu Markdown");

        Assert.Equal(InstallType.PerUser, ctx.Type);
        Assert.False(ctx.RequiresElevation);
        Assert.True(ctx.SupportsAutoUpdate);
    }

    [Fact]
    public void Detect_PerMachineRegistry_ReturnsPerMachine()
    {
        var reg = new FakeRegistryReader()
            .Set(RegistryHive.LocalMachine, SubKey, "InstallScope", "machine")
            .Set(RegistryHive.LocalMachine, SubKey, "InstallPath", @"C:\Program Files\Meu Markdown");

        var ctx = InstallContext.Detect(reg, basePath: @"C:\Program Files\Meu Markdown");

        Assert.Equal(InstallType.PerMachine, ctx.Type);
        Assert.True(ctx.RequiresElevation);
        Assert.True(ctx.SupportsAutoUpdate);
    }

    [Fact]
    public void Detect_NoRegistryButPathInProgramFiles_ReturnsPerMachine()
    {
        // Fallback v1.2.x: registry não tem InstallScope, mas path indica Program Files
        var reg = new FakeRegistryReader();

        var ctx = InstallContext.Detect(reg, basePath: @"C:\Program Files\Meu Markdown");

        Assert.Equal(InstallType.PerMachine, ctx.Type);
    }

    [Fact]
    public void Detect_NoRegistryButPathInLocalAppDataPrograms_ReturnsPerUser()
    {
        var reg = new FakeRegistryReader();
        var basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Meu Markdown");

        var ctx = InstallContext.Detect(reg, basePath);

        Assert.Equal(InstallType.PerUser, ctx.Type);
    }

    [Fact]
    public void Detect_NoRegistryAndArbitraryPath_ReturnsPortable()
    {
        var reg = new FakeRegistryReader();

        var ctx = InstallContext.Detect(reg, basePath: @"D:\Tools\MeuMarkdown-portable");

        Assert.Equal(InstallType.Portable, ctx.Type);
        Assert.False(ctx.SupportsAutoUpdate);
    }
}
