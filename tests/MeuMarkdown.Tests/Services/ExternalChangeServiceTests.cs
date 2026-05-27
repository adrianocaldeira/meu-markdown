using System;
using System.IO;
using System.Threading;
using MeuMarkdown.Models;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class ExternalChangeServiceTests
{
    private static MarkdownDocument OpenTemp(out string path, string content = "original")
    {
        path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return new FileService().OpenFile(path);
    }

    [Fact]
    public void Check_ArquivoInalterado_RetornaUnchanged()
    {
        var doc = OpenTemp(out var path);
        try
        {
            var result = new ExternalChangeService().Check(doc, isDirty: false);
            Assert.Equal(ExternalChangeStatus.Unchanged, result.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoModificadoDocLimpo_RetornaChangedCleanComConteudoNovo()
    {
        var doc = OpenTemp(out var path);
        try
        {
            Thread.Sleep(20);
            File.WriteAllText(path, "conteúdo externo");

            var result = new ExternalChangeService().Check(doc, isDirty: false);

            Assert.Equal(ExternalChangeStatus.ChangedClean, result.Status);
            Assert.Equal("conteúdo externo", result.Content);
            Assert.Equal(File.GetLastWriteTimeUtc(path), result.LastWriteTimeUtc);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoModificadoDocSujo_RetornaChangedDirty()
    {
        var doc = OpenTemp(out var path);
        try
        {
            Thread.Sleep(20);
            File.WriteAllText(path, "conteúdo externo");

            var result = new ExternalChangeService().Check(doc, isDirty: true);

            Assert.Equal(ExternalChangeStatus.ChangedDirty, result.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoRemovido_RetornaDeleted()
    {
        var doc = OpenTemp(out var path);
        File.Delete(path);

        var result = new ExternalChangeService().Check(doc, isDirty: false);

        Assert.Equal(ExternalChangeStatus.Deleted, result.Status);
    }

    [Fact]
    public void Check_DocumentoSemCaminho_RetornaUnchanged()
    {
        var doc = new MarkdownDocument { FilePath = string.Empty };
        var result = new ExternalChangeService().Check(doc, isDirty: false);
        Assert.Equal(ExternalChangeStatus.Unchanged, result.Status);
    }
}
