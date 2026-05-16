namespace MeuMarkdown.EditorBehaviors;

public static class AutoPair
{
    private static readonly Dictionary<string, string> _pairs = new()
    {
        { "[", "]" },
        { "(", ")" },
        { "`", "`" },
    };

    public static string? GetClosing(string opener)
    {
        return _pairs.TryGetValue(opener, out var closing) ? closing : null;
    }

    /// <summary>
    /// Verifica se o caractere deve disparar auto-pair no contexto.
    /// '_' só é pareado no início de palavra para evitar bagunçar snake_case.
    /// </summary>
    public static bool ShouldAutoPair(string opener, string lineBeforeCaret, int caretOffsetInLine)
    {
        if (opener == "_")
        {
            if (string.IsNullOrEmpty(lineBeforeCaret) || caretOffsetInLine == 0)
                return true;
            var prev = lineBeforeCaret[Math.Min(caretOffsetInLine, lineBeforeCaret.Length) - 1];
            return char.IsWhiteSpace(prev);
        }
        return true;
    }
}
