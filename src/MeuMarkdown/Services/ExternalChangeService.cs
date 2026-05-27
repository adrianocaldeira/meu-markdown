using System.IO;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

/// <summary>Classificação de uma checagem de mudança externa do arquivo.</summary>
public enum ExternalChangeStatus
{
    /// <summary>Sem mudança relevante (ou documento sem caminho em disco).</summary>
    Unchanged,
    /// <summary>Arquivo mudou no disco e o documento não tem edições locais.</summary>
    ChangedClean,
    /// <summary>Arquivo mudou no disco mas o documento tem edições não salvas.</summary>
    ChangedDirty,
    /// <summary>Arquivo não existe mais no caminho.</summary>
    Deleted
}

/// <summary>Resultado de <see cref="ExternalChangeService.Check"/>.</summary>
/// <param name="Status">Classificação da mudança.</param>
/// <param name="Content">Conteúdo lido do disco (só em <see cref="ExternalChangeStatus.ChangedClean"/>).</param>
/// <param name="LastWriteTimeUtc">Timestamp do disco (só em <see cref="ExternalChangeStatus.ChangedClean"/>).</param>
public readonly record struct ExternalChangeResult(
    ExternalChangeStatus Status,
    string Content,
    System.DateTime LastWriteTimeUtc);

/// <summary>
/// Verifica, sob demanda, se o arquivo de um documento aberto mudou no disco por outra
/// ferramenta. Não usa watcher: é chamado ao ativar a aba e ao app recuperar o foco.
/// </summary>
public class ExternalChangeService
{
    /// <summary>
    /// Compara o <see cref="MarkdownDocument.LastWriteTimeUtc"/> guardado com o disco e classifica.
    /// Nunca lança: falhas de I/O são tratadas como <see cref="ExternalChangeStatus.Unchanged"/>.
    /// </summary>
    public ExternalChangeResult Check(MarkdownDocument document, bool isDirty)
    {
        if (string.IsNullOrEmpty(document.FilePath))
            return new ExternalChangeResult(ExternalChangeStatus.Unchanged, string.Empty, default);

        try
        {
            if (!File.Exists(document.FilePath))
                return new ExternalChangeResult(ExternalChangeStatus.Deleted, string.Empty, default);

            var diskTime = File.GetLastWriteTimeUtc(document.FilePath);
            if (diskTime == document.LastWriteTimeUtc)
                return new ExternalChangeResult(ExternalChangeStatus.Unchanged, string.Empty, default);

            if (isDirty)
                return new ExternalChangeResult(ExternalChangeStatus.ChangedDirty, string.Empty, default);

            var content = File.ReadAllText(document.FilePath);
            return new ExternalChangeResult(ExternalChangeStatus.ChangedClean, content, diskTime);
        }
        catch (Exception)
        {
            // Qualquer falha de acesso a disco (bloqueio, caminho inválido, permissão):
            // trata como sem mudança; a próxima ativação tenta de novo.
            return new ExternalChangeResult(ExternalChangeStatus.Unchanged, string.Empty, default);
        }
    }
}
