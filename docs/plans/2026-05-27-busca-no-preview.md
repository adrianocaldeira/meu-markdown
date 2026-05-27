# Busca no preview (Ctrl+F) — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Ctrl+F` passa a buscar no painel ativo — no preview (WebView2) quando o preview está em foco ou no modo Visualização, no editor caso contrário (comportamento atual preservado).

**Architecture:** A busca no preview usa a API de Find nativa do WebView2 (`CoreWebView2.Find`), dirigida por uma segunda instância da `FindReplaceBar` (find-only, sem regex) no topo do painel de preview. Um método puro `FindRouting.Resolve` decide o alvo (Editor/Preview). A `MarkdownPreview` encapsula a busca e re-aplica o termo ativo no `NavigationCompleted` para o highlight sobreviver ao re-render.

**Tech Stack:** .NET 10 WPF, Microsoft.Web.WebView2 1.0.3800.47 (Find API), CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/specs/2026-05-27-busca-no-preview-design.md`

---

## Estrutura de arquivos

| Arquivo | Responsabilidade | Mudança |
|---|---|---|
| `src/MeuMarkdown/Controls/FindRouting.cs` | Enum `FindTarget` + método puro `Resolve` (testável) | Criar |
| `src/MeuMarkdown/Controls/FindReplaceBar.xaml.cs` | `Open(allowRegex)` + `CurrentFindText` | Modificar |
| `src/MeuMarkdown/Controls/MarkdownPreview.xaml.cs` | API de busca (Find nativo) + foco + re-aplicar no NavigationCompleted | Modificar |
| `src/MeuMarkdown/MainWindow.xaml` | Barra de busca no painel do preview | Modificar |
| `src/MeuMarkdown/MainWindow.xaml.cs` | Roteamento Ctrl+F / Next / Prev / Close + wiring | Modificar |
| `tests/MeuMarkdown.Tests/Controls/FindRoutingTests.cs` | Cobrir as 3 decisões de roteamento | Criar |

---

## Task 1: `FindRouting` — decisão de alvo (puro, testável)

**Files:**
- Create: `src/MeuMarkdown/Controls/FindRouting.cs`
- Test: `tests/MeuMarkdown.Tests/Controls/FindRoutingTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Crie `tests/MeuMarkdown.Tests/Controls/FindRoutingTests.cs`:

```csharp
using MeuMarkdown.Controls;
using Xunit;

namespace MeuMarkdown.Tests.Controls;

public class FindRoutingTests
{
    [Fact]
    public void Resolve_ModoVisualizacao_RetornaPreview()
    {
        Assert.Equal(FindTarget.Preview, FindRouting.Resolve(isViewMode: true, previewFocused: false));
    }

    [Fact]
    public void Resolve_Split_PreviewFocado_RetornaPreview()
    {
        Assert.Equal(FindTarget.Preview, FindRouting.Resolve(isViewMode: false, previewFocused: true));
    }

    [Fact]
    public void Resolve_Split_EditorFocado_RetornaEditor()
    {
        Assert.Equal(FindTarget.Editor, FindRouting.Resolve(isViewMode: false, previewFocused: false));
    }
}
```

- [ ] **Step 2: Rodar e confirmar falha**

Run: `rtk dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~FindRoutingTests`
Expected: FAIL — `FindRouting`/`FindTarget` não existem.

- [ ] **Step 3: Implementar**

Crie `src/MeuMarkdown/Controls/FindRouting.cs`:

```csharp
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
```

- [ ] **Step 4: Rodar e confirmar que passa**

Run: `rtk dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~FindRoutingTests`
Expected: PASS (3 testes).

- [ ] **Step 5: Commit**

```bash
rtk git add src/MeuMarkdown/Controls/FindRouting.cs tests/MeuMarkdown.Tests/Controls/FindRoutingTests.cs
rtk git commit -m "feat: FindRouting decide alvo da busca (editor vs preview)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: `FindReplaceBar` — ocultar regex e expor texto atual

**Files:**
- Modify: `src/MeuMarkdown/Controls/FindReplaceBar.xaml.cs:21-36`

- [ ] **Step 1: Atualizar `Open` para aceitar `allowRegex` e expor `CurrentFindText`**

Em `src/MeuMarkdown/Controls/FindReplaceBar.xaml.cs`, substitua o método `Open` (linhas 21-29) por:

```csharp
    public void Open(string? initialQuery, bool showReplace, bool allowRegex = true)
    {
        if (!string.IsNullOrEmpty(initialQuery))
            FindBox.Text = initialQuery;
        ReplaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
        RegexToggle.Visibility = allowRegex ? Visibility.Visible : Visibility.Collapsed;
        if (!allowRegex)
            RegexToggle.IsChecked = false;
        Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }
```

Logo após a propriedade `CurrentReplaceText` (linha 36), adicione:

```csharp
    public string CurrentFindText => FindBox.Text;
```

- [ ] **Step 2: Compilar**

Run: `rtk dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors. (`RegexToggle` já existe no XAML, linha 81.)

> Nota: a chamada existente `findBar.Open(..., showReplace)` continua válida porque
> `allowRegex` tem default `true`. Nenhum chamador atual precisa mudar nesta task.

- [ ] **Step 3: Commit**

```bash
rtk git add src/MeuMarkdown/Controls/FindReplaceBar.xaml.cs
rtk git commit -m "feat: FindReplaceBar pode ocultar regex e expõe CurrentFindText

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `MarkdownPreview` — API de busca via Find nativo

**Files:**
- Modify: `src/MeuMarkdown/Controls/MarkdownPreview.xaml.cs` (campos 11-19, construtor 22-26, `OnLoaded` ~57)

- [ ] **Step 1: Adicionar campos de estado da busca e foco**

Em `src/MeuMarkdown/Controls/MarkdownPreview.xaml.cs`, junto aos campos privados (linhas 11-14), adicione:

```csharp
    private string? _findTerm;
    private bool _findCaseSensitive;
    private bool _findWholeWord;
```

Junto aos eventos públicos (após a linha 19), adicione:

```csharp
    /// <summary>Dispara quando o contador de matches da busca no preview muda: (índice ativo, total).</summary>
    public event EventHandler<(int activeIndex, int total)>? FindMatchesChanged;

    /// <summary>True quando o conteúdo web do preview está com foco de teclado.</summary>
    public bool IsPreviewFocused { get; private set; }
```

- [ ] **Step 2: Rastrear foco no construtor**

No construtor (linhas 22-26), após `Loaded += OnLoaded;`, adicione:

```csharp
        webView.GotFocus += (_, _) => IsPreviewFocused = true;
        webView.LostFocus += (_, _) => IsPreviewFocused = false;
```

- [ ] **Step 3: Assinar eventos do Find e re-aplicar no NavigationCompleted**

Em `OnLoaded`, logo após `_isInitialized = true;` (linha 57), adicione:

```csharp
            webView.CoreWebView2.Find.MatchCountChanged += OnFindStatusChanged;
            webView.CoreWebView2.Find.ActiveMatchIndexChanged += OnFindStatusChanged;
            // Re-aplica a busca ativa após cada navegação (re-render via NavigateToString),
            // para o highlight sobreviver à digitação/reload enquanto a barra está aberta.
            webView.CoreWebView2.NavigationCompleted += OnNavCompletedReapplyFind;
```

- [ ] **Step 4: Implementar os métodos de busca**

Adicione estes membros à classe `MarkdownPreview` (perto de `ScrollToLine`):

```csharp
    /// <summary>
    /// Inicia (ou atualiza) a busca no preview via API nativa do WebView2. Term vazio para a busca.
    /// </summary>
    public async void StartFind(string term, bool caseSensitive, bool wholeWord)
    {
        if (!_isInitialized) return;
        if (string.IsNullOrEmpty(term))
        {
            StopFind();
            return;
        }
        _findTerm = term;
        _findCaseSensitive = caseSensitive;
        _findWholeWord = wholeWord;

        var options = webView.CoreWebView2.Environment.CreateFindOptions();
        options.FindTerm = term;
        options.IsCaseSensitive = caseSensitive;
        options.ShouldMatchWord = wholeWord;
        options.ShouldHighlightAllMatches = true;
        options.SuppressDefaultFindDialog = true;
        try
        {
            await webView.CoreWebView2.Find.StartAsync(options);
        }
        catch
        {
            // Find indisponível (runtime Edge antigo): ignora silenciosamente.
        }
    }

    /// <summary>Vai para o próximo match.</summary>
    public void FindNext()
    {
        if (_isInitialized && _findTerm != null) webView.CoreWebView2.Find.FindNext();
    }

    /// <summary>Vai para o match anterior.</summary>
    public void FindPrevious()
    {
        if (_isInitialized && _findTerm != null) webView.CoreWebView2.Find.FindPrevious();
    }

    /// <summary>Encerra a busca e limpa os highlights.</summary>
    public void StopFind()
    {
        _findTerm = null;
        if (_isInitialized) webView.CoreWebView2.Find.Stop();
    }

    private void OnFindStatusChanged(object? sender, object e)
    {
        if (!_isInitialized) return;
        var f = webView.CoreWebView2.Find;
        FindMatchesChanged?.Invoke(this, (f.ActiveMatchIndex, f.MatchCount));
    }

    private void OnNavCompletedReapplyFind(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_findTerm != null)
            StartFind(_findTerm, _findCaseSensitive, _findWholeWord);
    }
```

- [ ] **Step 5: Compilar e validar a API contra o SDK**

Run: `rtk dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors.

Se o compilador acusar membro inexistente (ex.: assinatura de `MatchCountChanged` diferente de `EventHandler<object>`, ou `Find` não encontrado), confira a API confirmada no spec (seção "Risco e verificação") e ajuste o tipo do handler. A API alvo (SDK 1.0.3800): `CoreWebView2.Find` → `CoreWebView2Find` com `StartAsync(CoreWebView2FindOptions)`, `FindNext()`, `FindPrevious()`, `Stop()`, `MatchCount`, `ActiveMatchIndex`, `MatchCountChanged`, `ActiveMatchIndexChanged`; opções via `webView.CoreWebView2.Environment.CreateFindOptions()`.

- [ ] **Step 6: Commit**

```bash
rtk git add src/MeuMarkdown/Controls/MarkdownPreview.xaml.cs
rtk git commit -m "feat: API de busca no MarkdownPreview via Find nativo do WebView2

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `MainWindow` — barra do preview e roteamento do Ctrl+F

**Files:**
- Modify: `src/MeuMarkdown/MainWindow.xaml` (Border do preview, ~553-557)
- Modify: `src/MeuMarkdown/MainWindow.xaml.cs` (campos ~55-57, wiring ~250-260, `OpenFindBar` ~1616, comandos Next/Prev ~259-260, `OnTabSelectionChanged` ~608)

- [ ] **Step 1: Adicionar a barra de busca no painel do preview (XAML)**

Em `src/MeuMarkdown/MainWindow.xaml`, o painel do preview hoje é:

```xml
                <!-- Preview panel with subtle border -->
                <Border Grid.Column="2" Margin="0,4,4,4"
                        Background="{DynamicResource Brush.Bg.Surface}" BorderBrush="{DynamicResource Brush.Border.Subtle}" BorderThickness="1" CornerRadius="6">
                    <controls:MarkdownPreview x:Name="preview" />
                </Border>
```

Substitua por (envolve o preview num Grid com uma linha `Auto` pra barra):

```xml
                <!-- Preview panel with subtle border -->
                <Border Grid.Column="2" Margin="0,4,4,4"
                        Background="{DynamicResource Brush.Bg.Surface}" BorderBrush="{DynamicResource Brush.Border.Subtle}" BorderThickness="1" CornerRadius="6">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="*" />
                        </Grid.RowDefinitions>
                        <controls:FindReplaceBar Grid.Row="0" x:Name="previewFindBar" Visibility="Collapsed" />
                        <controls:MarkdownPreview Grid.Row="1" x:Name="preview" />
                    </Grid>
                </Border>
```

- [ ] **Step 2: Adicionar campo de estado do alvo da busca**

Em `src/MeuMarkdown/MainWindow.xaml.cs`, junto aos campos de find (linhas 55-57, perto de `_findRenderer`/`_activeFindIndex`/`_lastFindRequest`), adicione:

```csharp
    private FindTarget _findTarget = FindTarget.Editor;
```

(`FindTarget` está em `MeuMarkdown.Controls`, já importado — o arquivo usa `FindReplaceBar`/`FindRequest`. Se faltar, adicione `using MeuMarkdown.Controls;`.)

- [ ] **Step 3: Wiring dos eventos da barra do preview**

Em `src/MeuMarkdown/MainWindow.xaml.cs`, junto ao wiring da `findBar` (linhas 250-255), adicione:

```csharp
        previewFindBar.FindRequested += (_, req) => preview.StartFind(req.Query, req.CaseSensitive, req.WholeWord);
        previewFindBar.NextRequested += (_, _) => preview.FindNext();
        previewFindBar.PrevRequested += (_, _) => preview.FindPrevious();
        previewFindBar.CloseRequested += (_, _) => ClosePreviewFindBar();
        preview.FindMatchesChanged += (_, m) =>
            Dispatcher.Invoke(() => previewFindBar.SetMatchCount(m.activeIndex, m.total));
```

- [ ] **Step 4: Rotear `OpenFindBar` conforme o painel ativo**

Substitua o método `OpenFindBar` (linhas 1616-1620) por:

```csharp
    private void OpenFindBar(bool showReplace)
    {
        // Replace (Ctrl+H) só faz sentido no editor; busca segue o painel ativo.
        var target = showReplace
            ? FindTarget.Editor
            : FindRouting.Resolve(_isViewMode, preview.IsPreviewFocused);

        if (target == FindTarget.Preview)
        {
            _findTarget = FindTarget.Preview;
            previewFindBar.Open(_lastFindRequest?.Query, showReplace: false, allowRegex: false);
            return;
        }

        _findTarget = FindTarget.Editor;
        var selected = textEditor.SelectedText;
        findBar.Open(string.IsNullOrEmpty(selected) ? _lastFindRequest?.Query : selected, showReplace);
    }
```

- [ ] **Step 5: Adicionar `ClosePreviewFindBar` e roteamento de Next/Prev**

Adicione o método (perto de `CloseFindBar`, ~1622):

```csharp
    private void ClosePreviewFindBar()
    {
        previewFindBar.Visibility = Visibility.Collapsed;
        preview.StopFind();
        preview.Focus();
        _findTarget = FindTarget.Editor;
    }

    private void FindNextRouted()
    {
        if (_findTarget == FindTarget.Preview) preview.FindNext();
        else MoveToFindMatch(+1);
    }

    private void FindPrevRouted()
    {
        if (_findTarget == FindTarget.Preview) preview.FindPrevious();
        else MoveToFindMatch(-1);
    }
```

Troque os dois `CommandBinding` de Next/Prev (linhas 259-260) de:

```csharp
        CommandBindings.Add(new CommandBinding(FindNextCommand, (_, _) => MoveToFindMatch(+1)));
        CommandBindings.Add(new CommandBinding(FindPrevCommand, (_, _) => MoveToFindMatch(-1)));
```

para:

```csharp
        CommandBindings.Add(new CommandBinding(FindNextCommand, (_, _) => FindNextRouted()));
        CommandBindings.Add(new CommandBinding(FindPrevCommand, (_, _) => FindPrevRouted()));
```

- [ ] **Step 6: Fechar a barra do preview ao trocar de aba**

Em `OnTabSelectionChanged` (linha 608), no início do método (logo após a abertura `{`, antes de `sidebarHost.OutlinePanel.BindToTab(...)`), adicione:

```csharp
        if (previewFindBar.Visibility == Visibility.Visible)
            ClosePreviewFindBar();
```

- [ ] **Step 7: Compilar**

Run: `rtk dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Rodar toda a suíte de testes**

Run: `rtk dotnet test tests/MeuMarkdown.Tests`
Expected: PASS (todos, incluindo os 3 novos de `FindRoutingTests`).

- [ ] **Step 9: Verificação manual (UI/WebView2 — sem teste automatizado)**

1. `rtk dotnet run --project src/MeuMarkdown/MeuMarkdown.csproj -- "test-files/exemplo.md"`
2. **Modo Visualização** (F5, editor oculto): `Ctrl+F` → barra aparece no topo do preview, **sem** o toggle `.*` (regex). Digite um termo → highlights aparecem, contador `X/Y` atualiza. Enter / setas navegam (scroll até o match). Esc fecha e limpa os highlights.
3. **Split**: clique no editor, `Ctrl+F` → abre a barra do **editor** (com regex), como hoje. Clique no preview, `Ctrl+F` → abre a barra do **preview**.
4. **Ctrl+H** (substituir): sempre abre no editor, mesmo com foco no preview.
5. Com a barra do preview aberta no split, edite o texto no editor → o preview re-renderiza e os highlights se mantêm/atualizam (re-aplicação no NavigationCompleted).
6. Troque de aba com a barra do preview aberta → ela fecha.

Confirme cada caso visualmente. Se algo divergir, **não** declare sucesso — registre o que aconteceu.

- [ ] **Step 10: Commit**

```bash
rtk git add src/MeuMarkdown/MainWindow.xaml src/MeuMarkdown/MainWindow.xaml.cs
rtk git commit -m "feat: Ctrl+F busca no preview conforme painel ativo

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Notas de verificação

- Task 1 é coberta por testes automatizados (xUnit) — a lógica de roteamento.
- Tasks 2, 3 e 4 envolvem WPF/WebView2, sem cobertura automatizada — verificação é build limpo + o roteiro manual do Step 9 da Task 4.
- A API de Find foi confirmada na doc oficial (via context7); se algum membro divergir no SDK em runtime, o Step 5 da Task 3 orienta o ajuste, e o fallback documentado é a CSS Custom Highlight API.
