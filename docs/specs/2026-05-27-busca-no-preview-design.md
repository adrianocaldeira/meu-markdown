# Busca no preview (Ctrl+F)

**Data:** 2026-05-27
**Status:** Aprovado (design)

## Problema

No editor, `Ctrl+F` abre a `FindReplaceBar` e busca no texto do AvalonEdit com
highlight próprio. No **preview** (WebView2 com HTML renderizado) não há busca — o
`Ctrl+F` nativo do Chromium está desabilitado. Quem está lendo no modo
Visualização (ou olhando o painel de preview no split) não consegue pesquisar.

## Objetivo

`Ctrl+F` passa a buscar no **painel ativo**: no preview quando o preview está em
foco ou o app está em modo Visualização; no editor caso contrário (comportamento
atual preservado). A busca no preview tem a mesma aparência da barra do editor.

## Decisões

- **Mecanismo**: API de Find nativa do WebView2 (`CoreWebView2.Find`), disponível
  no SDK `1.0.3800.47`. Highlight de todas as ocorrências, destaque do match
  ativo, contador e navegação são nativos do Chromium.
- **Recursos da barra no preview**: campo de busca, contador `X/Y`,
  anterior/próximo, toggles **Aa (case-sensitive)** e **palavra inteira**. **Sem
  Substituir** (não se edita HTML renderizado) e **sem regex** (a API nativa não
  suporta; o toggle fica oculto/desabilitado quando o alvo é o preview — o editor
  mantém regex sobre o markdown cru).
- **Gatilho**: painel ativo (foco no preview ou modo Visualização → preview;
  senão editor).

## Componentes

### `MarkdownPreview` — API de busca

Encapsula `CoreWebView2.Find`:

- `StartFind(string term, bool caseSensitive, bool wholeWord)` — se o term é
  vazio, chama `StopFind`. Cria as opções via
  `webView.CoreWebView2.Environment.CreateFindOptions()` com
  `FindTerm = term`, `IsCaseSensitive = caseSensitive`,
  `ShouldMatchWord = wholeWord`, `SuppressDefaultFindDialog = true`,
  `ShouldHighlightAllMatches = true`, e chama `Find.StartAsync(options)`.
- `FindNext()` → `Find.FindNext()`.
- `FindPrevious()` → `Find.FindPrevious()`.
- `StopFind()` → `Find.Stop()` (limpa highlights).

> API confirmada na doc oficial (Microsoft Learn, via context7): em .NET os métodos
> de navegação são síncronos (`FindNext()`, `FindPrevious()`, `Stop()`); só
> `StartAsync(CoreWebView2FindOptions)` é assíncrono. `CoreWebView2.Find` →
> `CoreWebView2Find`; opções via `Environment.CreateFindOptions()`.
- Evento `event EventHandler<(int activeIndex, int total)> FindMatchesChanged` —
  assina `Find.MatchCountChanged` e `Find.ActiveMatchIndexChanged` e repassa
  `(Find.ActiveMatchIndex, Find.MatchCount)`.
- `bool IsPreviewFocused` — rastreado via `GotFocus`/`LostFocus` do controle
  WebView2.
- Guarda de inicialização: todos os métodos viram no-op enquanto
  `!_isInitialized`.

### `FindReplaceBar` — opção de ocultar regex

A barra hoje tem toggles Aa / palavra inteira / regex e a linha de Substituir
(controlada por `showReplace` no `Open`). Adicionar a capacidade de **ocultar o
toggle de regex** para a instância do preview — via um parâmetro novo em
`Open(...)` (ex.: `bool allowRegex = true`) ou uma propriedade. Quando regex está
oculto, o `FindRequest` emitido tem `UseRegex = false`.

### `MainWindow` — segunda barra e roteamento

- Nova instância `previewFindBar` (`FindReplaceBar`) no topo do `Border` do
  preview (mesma técnica da `externalChangeBar`/`findBar`: uma `RowDefinition`
  `Auto` acima do `MarkdownPreview`). Aberta com `showReplace: false` e
  `allowRegex: false`.
- Estado: `_findTarget` (`Editor` | `Preview`) define para onde Next/Prev e Close
  roteiam.
- Método puro `ResolveFindTarget(bool isViewMode, bool previewFocused)` →
  retorna `Editor`/`Preview`. Testável isoladamente.
- `OpenFindBar(showReplace)`:
  - alvo = `ResolveFindTarget(_isViewMode, preview.IsPreviewFocused)`.
  - se `Preview`: abre `previewFindBar` (find-only, sem regex), seed do último
    termo; `_findTarget = Preview`.
  - se `Editor`: comportamento atual intacto; `_findTarget = Editor`.
- Eventos da `previewFindBar`:
  - `FindRequested` → `preview.StartFind(req.Query, req.CaseSensitive, req.WholeWord)`.
  - `NextRequested`/`PrevRequested` → `preview.FindNext()`/`FindPrevious()`.
  - `CloseRequested` → `preview.StopFind()`, esconde a barra, devolve foco ao
    preview.
- `preview.FindMatchesChanged` → `previewFindBar.SetMatchCount(activeIndex, total)`
  (no dispatcher da UI).
- `FindNextCommand`/`FindPrevCommand` (e Enter) roteiam conforme `_findTarget`.

## Fluxo

1. Usuário em modo Visualização (ou com foco no preview) aperta `Ctrl+F`.
2. `previewFindBar` abre e foca o campo.
3. Ao digitar, `StartFind` dispara; Chromium destaca todas as ocorrências e
   posiciona na primeira. `FindMatchesChanged` atualiza `X/Y` na barra.
4. Enter / setas → próximo/anterior (scroll nativo até o match ativo).
5. Esc → `StopFind` limpa highlights, barra some, foco volta ao preview.

## Casos de borda

- **Re-render do preview** (digitação no split que dispara `SetFullHtml`, ou
  reload externo) com a `previewFindBar` aberta: re-executa `StartFind` com o
  termo atual; se vazio, no-op.
- **WebView2 não inicializado**: API de find é no-op até `_isInitialized`.
- **Sem aba ativa**: `Ctrl+F` no preview vazio — `StartFind` com preview vazio
  não acha nada (`0 resultados`).
- **Troca de aba** com a barra aberta: a barra do preview fecha (consistente com
  como o editor lida com a troca de contexto).

## Risco e verificação

- API **confirmada** na doc oficial (Microsoft Learn, via context7): `CoreWebView2.Find`,
  `Environment.CreateFindOptions()`, `CoreWebView2FindOptions`
  (`FindTerm`/`IsCaseSensitive`/`ShouldMatchWord`/`ShouldHighlightAllMatches`/`SuppressDefaultFindDialog`),
  `CoreWebView2Find.StartAsync(options)`, `FindNext()`, `FindPrevious()`, `Stop()`,
  `MatchCount`, `ActiveMatchIndex`, `MatchCountChanged`, `ActiveMatchIndexChanged`.
  Disponível no SDK `1.0.3800.47`. Se em runtime a propriedade `Find` não existir
  (runtime Edge muito antigo — improvável), o fallback é a CSS Custom Highlight API
  via JS (plano B do brainstorm).

## Testes

- **Automatizável**: `ResolveFindTarget(isViewMode, previewFocused)` — método puro
  cobrindo: view mode → Preview; split + preview focado → Preview; split + editor
  focado → Editor.
- **Manual (UI/WebView2)**: abrir Ctrl+F no preview (view mode e split com foco no
  preview), digitar termo, ver highlight + contador, navegar com Enter/setas,
  fechar com Esc (highlights somem), confirmar que no editor focado o Ctrl+F ainda
  abre a barra do editor com regex.

## Fora de escopo

- Substituir no preview (não se edita HTML renderizado).
- Regex na busca do preview (limitação da API nativa; editor mantém).
- Busca simultânea nos dois painéis com índices sincronizados.
