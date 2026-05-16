using System.Collections.ObjectModel;

namespace MeuMarkdown.Services;

/// <summary>
/// Mantém a lista de arquivos recentemente abertos com deduplicação case-insensitive
/// e capacidade configurável (FIFO ao atingir o limite).
/// </summary>
public class RecentFilesService
{
    private readonly int _capacity;

    /// <summary>Coleção observável dos caminhos recentes, do mais novo ao mais antigo.</summary>
    public ObservableCollection<string> Items { get; } = new();

    /// <summary>
    /// Inicializa o serviço.
    /// </summary>
    /// <param name="capacity">Número máximo de entradas mantidas. Padrão: 50.</param>
    public RecentFilesService(int capacity = 50)
    {
        _capacity = capacity;
    }

    /// <summary>
    /// Adiciona <paramref name="path"/> ao topo da lista. Se já existir (comparação
    /// case-insensitive), move para o topo sem duplicar. Remove a entrada mais antiga
    /// quando a capacidade é excedida.
    /// </summary>
    public void Add(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var existing = -1;
        for (int i = 0; i < Items.Count; i++)
        {
            if (string.Equals(Items[i], path, StringComparison.OrdinalIgnoreCase))
            {
                existing = i;
                break;
            }
        }

        if (existing >= 0)
            Items.RemoveAt(existing);

        Items.Insert(0, path);

        while (Items.Count > _capacity)
            Items.RemoveAt(Items.Count - 1);
    }

    /// <summary>
    /// Popula a lista a partir de uma coleção persistida (ex.: settings.json).
    /// Substitui o conteúdo atual.
    /// </summary>
    public void LoadFrom(IReadOnlyList<string> paths)
    {
        Items.Clear();
        foreach (var p in paths)
        {
            if (Items.Count >= _capacity) break;
            Items.Add(p);
        }
    }

    /// <summary>Remove todos os itens da lista.</summary>
    public void Clear() => Items.Clear();

    /// <summary>Retorna um snapshot imutável da lista atual.</summary>
    public IReadOnlyList<string> Snapshot() => Items.ToList();
}
