# Recarregar documento ao detectar mudança externa — Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ao ativar uma aba ou o app recuperar o foco, detectar se o arquivo mudou no disco e recarregar (quando limpo) preservando o scroll do editor, avisando sem sobrescrever quando há edições não salvas ou o arquivo foi removido.

**Architecture:** Verificação sob demanda (sem watcher) disparada por troca de aba e `Window.Activated`. Um `ExternalChangeService` puro lê o disco e classifica em `Unchanged`/`ChangedClean`/`ChangedDirty`/`Deleted`, comparando `LastWriteTimeUtc` guardado no `MarkdownDocument` com o do disco. A `MainWindow` reage: recarrega o texto preservando o scroll do editor (zoom do preview sobrevive por ser propriedade do WebView2), ou exibe uma barra de aviso não-bloqueante no topo do editor.

**Tech Stack:** .NET 10 WPF, AvalonEdit, CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/specs/2026-05-27-recarregar-mudanca-externa-design.md`

---

## Estrutura de arquivos

| Arquivo | Responsabilidade | Mudança |
|---|---|---|
| `src/MeuMarkdown/Models/MarkdownDocument.cs` | Guardar `LastWriteTimeUtc` como referência de comparação | Modificar |
| `src/MeuMarkdown/Services/FileService.cs` | Preencher `LastWriteTimeUtc` no open/save | Modificar |
| `src/MeuMarkdown/Services/ExternalChangeService.cs` | Ler disco e classificar a mudança (unidade pura) | Criar |
| `src/MeuMarkdown/ViewModels/DocumentTabViewModel.cs` | Recarregar conteúdo sem marcar dirty | Modificar |
| `src/MeuMarkdown/MainWindow.xaml` | Barra de aviso no topo do editor | Modificar |
| `src/MeuMarkdown/MainWindow.xaml.cs` | Gatilhos (troca de aba, foco) + reações + preservar scroll | Modificar |
| `tests/MeuMarkdown.Tests/Services/ExternalChangeServiceTests.cs` | Cobrir as 4 classificações | Criar |
| `tests/MeuMarkdown.Tests/Services/FileServiceTests.cs` | Cobrir `LastWriteTimeUtc` no open | Criar (ou adicionar) |

---

## Task 1: `LastWriteTimeUtc` no modelo e no FileService

**Files:**
- Modify: `src/MeuMarkdown/Models/MarkdownDocument.cs`
- Modify: `src/MeuMarkdown/Services/FileService.cs:8-24`
- Test: `tests/MeuMarkdown.Tests/Services/FileServiceTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

Crie `tests/MeuMarkdown.Tests/Services/FileServiceTests.cs`:

```csharp
using System;
using System.IO;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class FileServiceTests
{
    [Fact]
    public void OpenFile_PreencheLastWriteTimeUtc_IgualAoDisco()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# olá");
        try
        {
            var service = new FileService();
            var doc = service.OpenFile(path);

            Assert.Equal(File.GetLastWriteTimeUtc(path), doc.LastWriteTimeUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveFile_AtualizaLastWriteTimeUtc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "antigo");
        try
        {
            var service = new FileService();
            var doc = service.OpenFile(path);
            var original = doc.LastWriteTimeUtc;

            System.Threading.Thread.Sleep(20);
            doc.Content = "novo conteúdo";
            service.SaveFile(doc);

            Assert.Equal(File.GetLastWriteTimeUtc(path), doc.LastWriteTimeUtc);
            Assert.True(doc.LastWriteTimeUtc >= original);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Rodar o teste e confirmar que falha**

Run: `dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~FileServiceTests`
Expected: FAIL — `MarkdownDocument` não tem `LastWriteTimeUtc`.

- [ ] **Step 3: Adicionar a propriedade ao modelo**

Em `src/MeuMarkdown/Models/MarkdownDocument.cs`, dentro da classe `MarkdownDocument`, adicione:

```csharp
public DateTime LastWriteTimeUtc { get; set; }
```

(O arquivo já tem `using System.IO;`. Não precisa de `using System;` — `DateTime` está em `System`, que é implicitamente incluído via `ImplicitUsings`.)

- [ ] **Step 4: Preencher no FileService**

Em `src/MeuMarkdown/Services/FileService.cs`, substitua os métodos `OpenFile` e `SaveFile`:

```csharp
public MarkdownDocument OpenFile(string filePath)
{
    var fullPath = Path.GetFullPath(filePath);
    var content = File.ReadAllText(fullPath);
    return new MarkdownDocument
    {
        FilePath = fullPath,
        Content = content,
        IsDirty = false,
        LastWriteTimeUtc = File.GetLastWriteTimeUtc(fullPath)
    };
}

public void SaveFile(MarkdownDocument document)
{
    if (string.IsNullOrEmpty(document.FilePath)) return;
    File.WriteAllText(document.FilePath, document.Content);
    document.IsDirty = false;
    document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(document.FilePath);
}
```

- [ ] **Step 5: Rodar o teste e confirmar que passa**

Run: `dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~FileServiceTests`
Expected: PASS (2 testes).

- [ ] **Step 6: Commit**

```bash
git add src/MeuMarkdown/Models/MarkdownDocument.cs src/MeuMarkdown/Services/FileService.cs tests/MeuMarkdown.Tests/Services/FileServiceTests.cs
git commit -m "feat: registra LastWriteTimeUtc do documento no open/save"
```

---

## Task 2: `ExternalChangeService` (classificação da mudança)

**Files:**
- Create: `src/MeuMarkdown/Services/ExternalChangeService.cs`
- Test: `tests/MeuMarkdown.Tests/Services/ExternalChangeServiceTests.cs`

- [ ] **Step 1: Escrever os testes que falham**

Crie `tests/MeuMarkdown.Tests/Services/ExternalChangeServiceTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using MeuMarkdown.Models;
using MeuMarkdown.Services;
using Xunit;

namespace MeuMarkdown.Tests.Services;

public class ExternalChangeServiceTests
{
    private static MarkdownDocument OpenTemp(out string path, string content = "original")
    {
        path = Path.Combine(Path.GetTempPath(), $"mm-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return new FileService().OpenFile(path);
    }

    [Fact]
    public void Check_ArquivoInalterado_RetornaUnchanged()
    {
        var doc = OpenTemp(out var path);
        try
        {
            var result = new ExternalChangeService().Check(doc, isDirty: false);
            Assert.Equal(ExternalChangeStatus.Unchanged, result.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoModificadoDocLimpo_RetornaChangedCleanComConteudoNovo()
    {
        var doc = OpenTemp(out var path);
        try
        {
            Thread.Sleep(20);
            File.WriteAllText(path, "conteúdo externo");

            var result = new ExternalChangeService().Check(doc, isDirty: false);

            Assert.Equal(ExternalChangeStatus.ChangedClean, result.Status);
            Assert.Equal("conteúdo externo", result.Content);
            Assert.Equal(File.GetLastWriteTimeUtc(path), result.LastWriteTimeUtc);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoModificadoDocSujo_RetornaChangedDirty()
    {
        var doc = OpenTemp(out var path);
        try
        {
            Thread.Sleep(20);
            File.WriteAllText(path, "conteúdo externo");

            var result = new ExternalChangeService().Check(doc, isDirty: true);

            Assert.Equal(ExternalChangeStatus.ChangedDirty, result.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Check_ArquivoRemovido_RetornaDeleted()
    {
        var doc = OpenTemp(out var path);
        File.Delete(path);

        var result = new ExternalChangeService().Check(doc, isDirty: false);

        Assert.Equal(ExternalChangeStatus.Deleted, result.Status);
    }

    [Fact]
    public void Check_DocumentoSemCaminho_RetornaUnchanged()
    {
        var doc = new MarkdownDocument { FilePath = string.Empty };
        var result = new ExternalChangeService().Check(doc, isDirty: false);
        Assert.Equal(ExternalChangeStatus.Unchanged, result.Status);
    }
}
```

- [ ] **Step 2: Rodar os testes e confirmar que falham**

Run: `dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~ExternalChangeServiceTests`
Expected: FAIL — `ExternalChangeService`/`ExternalChangeStatus` não existem.

- [ ] **Step 3: Implementar o serviço**

Crie `src/MeuMarkdown/Services/ExternalChangeService.cs`:

```csharp
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
        catch (IOException)
        {
            // Arquivo bloqueado por outro processo: a próxima ativação tenta de novo.
            return new ExternalChangeResult(ExternalChangeStatus.Unchanged, string.Empty, default);
        }
        catch (UnauthorizedAccessException)
        {
            return new ExternalChangeResult(ExternalChangeStatus.Unchanged, string.Empty, default);
        }
    }
}
```

- [ ] **Step 4: Rodar os testes e confirmar que passam**

Run: `dotnet test tests/MeuMarkdown.Tests --filter FullyQualifiedName~ExternalChangeServiceTests`
Expected: PASS (5 testes).

- [ ] **Step 5: Commit**

```bash
git add src/MeuMarkdown/Services/ExternalChangeService.cs tests/MeuMarkdown.Tests/Services/ExternalChangeServiceTests.cs
git commit -m "feat: ExternalChangeService classifica mudança externa de arquivo"
```

---

## Task 3: Recarregar conteúdo no DocumentTabViewModel sem marcar dirty

**Files:**
- Modify: `src/MeuMarkdown/ViewModels/DocumentTabViewModel.cs:63-67`

- [ ] **Step 1: Adicionar guarda de dirty e método de reload**

Em `src/MeuMarkdown/ViewModels/DocumentTabViewModel.cs`, adicione o campo logo após a declaração de `_document` (linha 9):

```csharp
    private bool _suppressDirty;
```

Substitua o `OnContentChanged` existente (linhas 63-67) por:

```csharp
    partial void OnContentChanged(string value)
    {
        _document.Content = value;
        if (!_suppressDirty) IsDirty = true;
    }
```

Adicione este método público logo após `MarkSaved()` (depois da linha 78):

```csharp
    /// <summary>
    /// Substitui o conteúdo do documento com a versão lida do disco, sem marcar como sujo,
    /// e atualiza o timestamp de referência. Usado no recarregamento por mudança externa.
    /// </summary>
    public void ReloadFromDisk(string content, DateTime lastWriteTimeUtc)
    {
        _suppressDirty = true;
        Content = content;
        _suppressDirty = false;
        _document.Content = content;
        _document.LastWriteTimeUtc = lastWriteTimeUtc;
        IsDirty = false;
    }
```

(O arquivo não tem `using System;` explícito, mas `DateTime` resolve via `ImplicitUsings`. Se o build reclamar, adicione `using System;` no topo.)

- [ ] **Step 2: Compilar para garantir que não quebrou**

Run: `dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/MeuMarkdown/ViewModels/DocumentTabViewModel.cs
git commit -m "feat: DocumentTabViewModel recarrega do disco sem marcar dirty"
```

---

## Task 4: Barra de aviso no XAML

**Files:**
- Modify: `src/MeuMarkdown/MainWindow.xaml:527-544`

- [ ] **Step 1: Adicionar a barra de aviso acima da FindReplaceBar**

Em `src/MeuMarkdown/MainWindow.xaml`, dentro do `Border x:Name="editorBorder"` (linha 525), o `<Grid>` interno (linha 527) tem duas linhas. Substitua o bloco das linhas 527-544 por:

```xml
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="*" />
                        </Grid.RowDefinitions>

                        <!-- Aviso de mudança externa (oculto por padrão) -->
                        <Border Grid.Row="0" x:Name="externalChangeBar" Visibility="Collapsed"
                                Background="{DynamicResource Brush.Bg.SurfaceAlt}"
                                BorderBrush="{DynamicResource Brush.Border.Subtle}"
                                BorderThickness="0,0,0,1" Padding="10,6">
                            <DockPanel>
                                <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                                    <Button x:Name="externalChangePrimaryButton"
                                            Content="Recarregar"
                                            Click="OnExternalChangePrimary"
                                            Padding="10,3" Margin="0,0,6,0" />
                                    <Button x:Name="externalChangeDismissButton"
                                            Content="Manter o meu"
                                            Click="OnExternalChangeDismiss"
                                            Padding="10,3" />
                                </StackPanel>
                                <TextBlock x:Name="externalChangeText"
                                           VerticalAlignment="Center"
                                           Foreground="{DynamicResource Brush.Fg.Primary}"
                                           Text="Este arquivo mudou no disco." />
                            </DockPanel>
                        </Border>

                        <controls:FindReplaceBar Grid.Row="1" x:Name="findBar" Visibility="Collapsed" />
                        <ae:TextEditor Grid.Row="2" x:Name="textEditor"
                                       FontFamily="Cascadia Code, Consolas, Courier New"
                                       FontSize="14"
                                       ShowLineNumbers="True"
                                       WordWrap="True"
                                       VerticalScrollBarVisibility="Auto"
                                       HorizontalScrollBarVisibility="Auto"
                                       Padding="8,8"
                                       Background="{DynamicResource Brush.Bg.Surface}"
                                       Foreground="{DynamicResource Brush.Fg.Primary}"
                                       BorderThickness="0" />
                    </Grid>
```

(Mudanças: o `findBar` passou de `Grid.Row="0"` para `Grid.Row="1"`, o `textEditor` de `Grid.Row="1"` para `Grid.Row="2"`, e foi adicionada uma terceira `RowDefinition` e a barra de aviso.)

- [ ] **Step 2: Compilar para validar o XAML**

Run: `dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors. (Vai falhar com "OnExternalChangePrimary não encontrado" — esses handlers vêm na Task 5. Se falhar só por isso, prossiga para a Task 5 e compile junto.)

> Nota: como os handlers `OnExternalChangePrimary`/`OnExternalChangeDismiss` ainda não existem no code-behind, o build do XAML só fecha após a Task 5. Faça o commit desta task junto com a Task 5.

---

## Task 5: Gatilhos, reações e preservação de scroll na MainWindow

**Files:**
- Modify: `src/MeuMarkdown/MainWindow.xaml.cs` (campos da classe, construtor ~115-140, `OnTabSelectionChanged:481-506`)

- [ ] **Step 1: Adicionar campos de serviço e estado**

Em `src/MeuMarkdown/MainWindow.xaml.cs`, junto aos outros campos privados da classe (perto de `_debounceTimer`/`_suppressEditorUpdate`), adicione:

```csharp
    private readonly ExternalChangeService _externalChangeService = new();
    private ExternalChangeStatus _pendingExternalChange = ExternalChangeStatus.Unchanged;
```

(Garanta que `using MeuMarkdown.Services;` está presente no topo do arquivo; o `FileAssociationService` já é usado, então provavelmente já está.)

- [ ] **Step 2: Assinar o evento de foco da janela no construtor**

No construtor da `MainWindow`, após `InitializeComponent()` (ou junto às outras assinaturas de evento, perto da linha 127), adicione:

```csharp
        Activated += OnWindowActivated;
```

- [ ] **Step 3: Implementar checagem e reações**

Adicione estes métodos à classe `MainWindow` (perto de `OnTabSelectionChanged`):

```csharp
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        CheckActiveTabForExternalChange();
    }

    /// <summary>
    /// Verifica se o arquivo da aba ativa mudou no disco e reage: recarrega se limpo,
    /// avisa se sujo ou removido. Chamado ao trocar de aba e ao app recuperar o foco.
    /// </summary>
    private void CheckActiveTabForExternalChange()
    {
        var tab = _viewModel.SelectedTab;
        if (tab == null)
        {
            HideExternalChangeBar();
            return;
        }

        var result = _externalChangeService.Check(tab.GetDocument(), tab.IsDirty);
        switch (result.Status)
        {
            case ExternalChangeStatus.Unchanged:
                HideExternalChangeBar();
                break;
            case ExternalChangeStatus.ChangedClean:
                ReloadActiveTabFromDisk(tab, result.Content, result.LastWriteTimeUtc);
                HideExternalChangeBar();
                break;
            case ExternalChangeStatus.ChangedDirty:
                ShowExternalChangeBar(
                    "Este arquivo mudou no disco.", "Recarregar", ExternalChangeStatus.ChangedDirty);
                break;
            case ExternalChangeStatus.Deleted:
                ShowExternalChangeBar(
                    "Este arquivo foi removido do disco.", "Salvar novamente", ExternalChangeStatus.Deleted);
                break;
        }
    }

    private void ReloadActiveTabFromDisk(DocumentTabViewModel tab, string content, DateTime lastWriteTimeUtc)
    {
        var offset = textEditor.TextArea.TextView.ScrollOffset;

        tab.ReloadFromDisk(content, lastWriteTimeUtc);

        _suppressEditorUpdate = true;
        textEditor.Text = content;
        _suppressEditorUpdate = false;

        UpdatePreview();

        textEditor.ScrollToHorizontalOffset(offset.X);
        textEditor.ScrollToVerticalOffset(offset.Y);
    }

    private void ShowExternalChangeBar(string message, string primaryLabel, ExternalChangeStatus status)
    {
        externalChangeText.Text = message;
        externalChangePrimaryButton.Content = primaryLabel;
        externalChangeDismissButton.Visibility =
            status == ExternalChangeStatus.Deleted ? Visibility.Collapsed : Visibility.Visible;
        _pendingExternalChange = status;
        externalChangeBar.Visibility = Visibility.Visible;
    }

    private void HideExternalChangeBar()
    {
        externalChangeBar.Visibility = Visibility.Collapsed;
        _pendingExternalChange = ExternalChangeStatus.Unchanged;
    }

    private void OnExternalChangePrimary(object sender, RoutedEventArgs e)
    {
        var tab = _viewModel.SelectedTab;
        if (tab == null) { HideExternalChangeBar(); return; }

        if (_pendingExternalChange == ExternalChangeStatus.ChangedDirty)
        {
            // Recarrega do disco, descartando as edições locais.
            var fresh = _externalChangeService.Check(tab.GetDocument(), isDirty: false);
            if (fresh.Status == ExternalChangeStatus.ChangedClean)
                ReloadActiveTabFromDisk(tab, fresh.Content, fresh.LastWriteTimeUtc);
        }
        else if (_pendingExternalChange == ExternalChangeStatus.Deleted)
        {
            // "Salvar novamente": reescreve o arquivo a partir do conteúdo em memória.
            _viewModel.SaveCommand.Execute(null);
        }

        HideExternalChangeBar();
    }

    private void OnExternalChangeDismiss(object sender, RoutedEventArgs e)
    {
        // "Manter o meu": reconhece a versão atual do disco (atualiza só o timestamp de
        // referência, sem tocar no conteúdo em memória) para não reavisar da MESMA mudança
        // a cada foco. Uma nova escrita externa volta a disparar o aviso.
        var tab = _viewModel.SelectedTab;
        if (tab != null)
        {
            var doc = tab.GetDocument();
            try
            {
                if (System.IO.File.Exists(doc.FilePath))
                    doc.LastWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(doc.FilePath);
            }
            catch (System.IO.IOException) { /* tenta de novo no próximo foco */ }
        }
        HideExternalChangeBar();
    }
```

- [ ] **Step 4: Disparar a checagem na troca de aba**

Em `OnTabSelectionChanged` (linha 481), adicione a chamada ao final do método (após `TryConsumePendingScrollFragment();` na linha 505):

```csharp
        CheckActiveTabForExternalChange();
```

E no ramo de aba nula (após `preview.SetFullHtml("");`, antes do `return;` na linha 490) adicione:

```csharp
            HideExternalChangeBar();
```

- [ ] **Step 5: Confirmar que `SaveCommand` existe**

O `OnExternalChangePrimary` usa `_viewModel.SaveCommand`. Confirme que existe (gerado pelo `[RelayCommand] private void Save()` em `MainViewModel.cs:120-121`).

Run: `grep -n "private void Save\b" src/MeuMarkdown/ViewModels/MainViewModel.cs`
Expected: encontra `private void Save()` — o source generator cria `SaveCommand`.

- [ ] **Step 6: Compilar tudo (XAML da Task 4 + code-behind)**

Run: `dotnet build src/MeuMarkdown/MeuMarkdown.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Rodar toda a suíte de testes**

Run: `dotnet test tests/MeuMarkdown.Tests`
Expected: PASS (todos, incluindo os novos de Task 1 e 2).

- [ ] **Step 8: Verificação manual (UI — não há teste automatizado de WPF)**

1. `dotnet run --project src/MeuMarkdown/MeuMarkdown.csproj -- "test-files/exemplo.md"`
2. Role o editor até o meio. Em outra ferramenta (ex.: bloco de notas), edite e salve `test-files/exemplo.md`.
3. Clique fora do app e volte (foco) — ou troque de aba e volte: o editor deve recarregar com o conteúdo novo **mantendo a posição de scroll**, sem barra de aviso.
4. Repita digitando algo no editor (deixando sujo) antes de editar o arquivo por fora: ao voltar, deve aparecer a barra "Este arquivo mudou no disco." com **[Recarregar]** e **[Manter o meu]**, sem sobrescrever.
5. Delete o arquivo por fora e volte ao app: barra "Este arquivo foi removido do disco." com **[Salvar novamente]**; clicar recria o arquivo.

Confirme cada caso visualmente. Se algo não bater, **não** declare sucesso — registre o que divergiu.

- [ ] **Step 9: Commit (inclui a Task 4)**

```bash
git add src/MeuMarkdown/MainWindow.xaml src/MeuMarkdown/MainWindow.xaml.cs
git commit -m "feat: recarrega documento ao detectar mudança externa ao ativar aba/foco"
```

---

## Notas de verificação

- Tasks 1 e 2 são cobertas por testes automatizados (xUnit).
- Tasks 3, 4 e 5 envolvem WPF e WebView2, que não têm cobertura de teste automatizada neste projeto — a verificação é o build limpo + o roteiro manual do Step 8 da Task 5.
- O zoom do preview é preservado naturalmente (`WebView2.ZoomFactor` sobrevive ao `NavigateToString`); o scroll do preview segue o comportamento atual (volta ao topo), conforme decidido no spec.
