namespace MeuMarkdown.EditorBehaviors;

public static class FencedCodeDetector
{
    /// <summary>
    /// Determina se o offset cai dentro de um bloco de código cercado por ``` ou ~~~.
    /// Conta as fences acima do offset; ímpar = dentro, par = fora.
    /// </summary>
    public static bool IsInsideFencedCode(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset <= 0) return false;
        if (offset > text.Length) offset = text.Length;

        var fenceCount = 0;
        var lineStart = 0;
        for (int i = 0; i < offset; i++)
        {
            if (i == lineStart)
            {
                if (IsFenceMarker(text, i))
                    fenceCount++;
            }
            if (text[i] == '\n')
                lineStart = i + 1;
        }
        return fenceCount % 2 == 1;
    }

    private static bool IsFenceMarker(string text, int lineStart)
    {
        if (lineStart + 2 >= text.Length) return false;
        var c0 = text[lineStart];
        if (c0 != '`' && c0 != '~') return false;
        return text[lineStart + 1] == c0 && text[lineStart + 2] == c0;
    }
}
