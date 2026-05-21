namespace MeuMarkdown.Services;

/// <summary>
/// Helper estático para calcular como inserir um bloco mermaid no documento,
/// dado o conteúdo atual e a posição do cursor. Pure — fácil de testar sem WPF.
/// </summary>
public static class MarkdownInsertionService
{
    /// <summary>
    /// Constrói o texto a ser inserido na posição <paramref name="caretOffset"/>
    /// para representar um bloco mermaid, adicionando quebras de linha antes
    /// quando o cursor não estiver no início de uma linha vazia.
    /// </summary>
    /// <returns>O texto pronto para inserir e o novo offset do cursor após a inserção.</returns>
    public static (string text, int newCaretOffset) BuildMermaidInsertion(
        string content, int caretOffset, string mermaidCode)
    {
        var needsPrefix = NeedsLeadingBlankLine(content, caretOffset);
        var prefix = needsPrefix ? "\n\n" : "";
        var body = "```mermaid\n" + mermaidCode + "\n```\n\n";
        var text = prefix + body;
        return (text, caretOffset + text.Length);
    }

    private static bool NeedsLeadingBlankLine(string content, int caretOffset)
    {
        if (caretOffset <= 0) return false;
        var prev = content[caretOffset - 1];
        if (prev == '\n') return false;
        return true;
    }
}
