using Markdig.Syntax.Inlines;

namespace MeuMarkdown.Extensions.WikiLinks;

/// <summary>
/// AST node para wiki-links [[Arquivo]], [[Arquivo|Alias]], [[Arquivo#heading]].
/// Resolução do path real é feita pelo renderer.
/// </summary>
public class WikiLinkInline : Inline
{
    public required string Target { get; init; }
    public string? Fragment { get; init; }
    public required string DisplayText { get; init; }
}
