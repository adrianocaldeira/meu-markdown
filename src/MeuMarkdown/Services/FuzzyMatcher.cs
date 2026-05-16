namespace MeuMarkdown.Services;

/// <summary>
/// Algoritmo de correspondência fuzzy para o Quick Switcher e busca rápida de arquivos.
/// Pontuação mais alta = melhor correspondência.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// Calcula a pontuação de correspondência entre <paramref name="query"/> e
    /// <paramref name="target"/>. Retorna 0 se não houver correspondência de subsequência.
    /// </summary>
    /// <param name="query">Texto digitado pelo usuário.</param>
    /// <param name="target">Candidato a ser avaliado (ex.: nome do arquivo).</param>
    /// <returns>
    /// Pontuação positiva quanto melhor a correspondência:
    /// <list type="bullet">
    ///   <item>Substring exata: base 1000 + bônus por posição inicial.</item>
    ///   <item>Subsequência: 100 por caractere + bônus de consecutividade.</item>
    ///   <item>Sem correspondência ou query vazia: 0.</item>
    /// </list>
    /// </returns>
    public static int Score(string query, string target)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
            return 0;

        var q = query.ToLowerInvariant();
        var t = target.ToLowerInvariant();

        // Correspondência de substring exata — pontuação mais alta
        var substringIdx = t.IndexOf(q, StringComparison.Ordinal);
        if (substringIdx >= 0)
            return 1000 + (substringIdx == 0 ? 200 : 0) - substringIdx;

        // Correspondência de subsequência com bônus de consecutividade
        int qi = 0, ti = 0;
        int score = 0;
        int consecutive = 0;

        while (qi < q.Length && ti < t.Length)
        {
            if (q[qi] == t[ti])
            {
                score += 100;
                consecutive++;
                score += consecutive * 50;
                qi++;
            }
            else
            {
                consecutive = 0;
            }
            ti++;
        }

        // Todos os caracteres da query precisam ter sido encontrados
        if (qi < q.Length) return 0;

        // Bônus leve por iniciar pelo primeiro caractere do target
        if (t.StartsWith(q[0])) score += 30;

        return score;
    }
}
