using System;
using System.IO;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class FileServiceTests
{
    [Fact]
    public void OpenFile_PreencheLastWriteTimeUtc_IgualAoDisco()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# olá");
        try
        {
            var service = new FileService();
            var doc = service.OpenFile(path);

            Assert.Equal(File.GetLastWriteTimeUtc(path), doc.LastWriteTimeUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveFile_AtualizaLastWriteTimeUtc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "antigo");
        try
        {
            var service = new FileService();
            var doc = service.OpenFile(path);
            var original = doc.LastWriteTimeUtc;

            System.Threading.Thread.Sleep(20);
            doc.Content = "novo conteúdo";
            service.SaveFile(doc);

            Assert.Equal(File.GetLastWriteTimeUtc(path), doc.LastWriteTimeUtc);
            Assert.True(doc.LastWriteTimeUtc >= original);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
