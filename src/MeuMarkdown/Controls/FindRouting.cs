namespace MeuMarkdown.Controls;

/// <summary>Para onde o Ctrl+F deve direcionar a busca.</summary>
public enum FindTarget
{
    Editor,
    Preview
}

/// <summary>Decide o alvo da busca conforme o painel ativo. Unidade pura, sem dependência de UI.</summary>
public static class FindRouting
{
    /// <summary>
    /// Preview quando o editor está oculto (modo Visualização) ou o preview está em foco;
    /// caso contrário, Editor.
    /// </summary>
    public static FindTarget Resolve(bool isViewMode, bool previewFocused)
        => isViewMode || previewFocused ? FindTarget.Preview : FindTarget.Editor;
}
