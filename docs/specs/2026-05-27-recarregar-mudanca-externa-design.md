# Recarregar documento ao detectar mudança externa

**Data:** 2026-05-27
**Status:** Aprovado (design)

## Problema

Quando um arquivo `.md` aberto no Meu Markdown é alterado por outra ferramenta
(ex.: VS Code, git checkout, script), o documento na tela continua mostrando a
versão antiga. O usuário precisa fechar e reabrir a aba, perdendo o estado de
visualização (scroll do editor, scroll e zoom do preview).

## Objetivo

Detectar que o arquivo mudou no disco e, quando seguro, recarregar o conteúdo
preservando o estado de visualização atual. Quando não for seguro (edições
locais não salvas), avisar sem sobrescrever.

## Escopo e gatilhos

A verificação **não usa watcher ao vivo**. Ela ocorre sob demanda, em dois
momentos:

1. **Troca de aba** — quando uma aba se torna a ativa (`SelectedTab` muda).
2. **Foco da janela** — quando a janela do app recupera o foco
   (`Window.Activated`), re-checa a aba atualmente ativa. Cobre o caso de editar
   em outra ferramenta e voltar pro Meu Markdown sem trocar de aba.

Abas sem caminho em disco (arquivo novo "Sem título") são ignoradas.

## Classificação da mudança

Ao disparar, compara o `LastWriteTimeUtc` guardado da aba com o do disco e
classifica em um de quatro estados:

| Estado         | Condição                                                        |
|----------------|-----------------------------------------------------------------|
| `Unchanged`    | timestamp do disco == timestamp guardado                        |
| `ChangedClean` | timestamp do disco != guardado **e** documento não está sujo    |
| `ChangedDirty` | timestamp do disco != guardado **e** documento está sujo        |
| `Deleted`      | arquivo não existe mais no caminho                              |

A comparação por `LastWriteTimeUtc` evita falso-positivo do próprio `Save` do
app, porque o `Save` atualiza o timestamp guardado. Opcionalmente compara também
o tamanho do arquivo como reforço.

## Reações

- **`ChangedClean`** → recarrega o texto do disco. Antes de substituir o texto,
  captura `editor.TextArea.TextView.ScrollOffset`; reaplica após o reload. O
  preview se atualiza pelo fluxo normal (`updateContent` via script, que faz
  update de DOM e **não** recarrega a página), preservando zoom e scroll do
  preview naturalmente. Atualiza `LastWriteTimeUtc` guardado.
- **`ChangedDirty`** → exibe barra/aviso discreto não-bloqueante no topo do
  editor: *"Este arquivo mudou no disco."* com ações **[Recarregar]** (descarta
  edições locais e recarrega) e **[Manter o meu]** (dispensa o aviso). Não
  sobrescreve nada até ação explícita.
- **`Deleted`** → mesma barra: *"Removido do disco."* com ação **[Salvar
  novamente]**. A aba continua aberta com o conteúdo em memória e `IsDirty=true`.

## Componentes

- **`MarkdownDocument`** ganha `DateTime LastWriteTimeUtc`. É a referência de
  comparação. Preenchido no load, atualizado no save.
- **`FileService`** preenche `LastWriteTimeUtc` em `Load` (a partir de
  `File.GetLastWriteTimeUtc`) e atualiza em `Save`.
- **Novo `ExternalChangeService`** — sem estado de watcher. Método
  `CheckResult Check(MarkdownDocument doc, bool isDirty)` que lê o disco e
  retorna o enum de classificação. Unidade pura, fácil de testar.
- **`MainWindow`** — liga os gatilhos (`SelectedTab` changed, `Window.Activated`)
  ao serviço e às reações de UI. A barra de aviso é um controle reaproveitável no
  topo da área de edição.

## Tratamento de erros

- Falha de I/O ao reler (arquivo bloqueado por outro processo) → trata como
  `Unchanged` silenciosamente nessa checagem; a próxima ativação tenta de novo.
- `Check` nunca lança para o chamador; encapsula exceções de acesso a disco.

## Testes

`ExternalChangeService.Check` é testável de forma isolada com arquivos
temporários, cobrindo as quatro classificações:

- arquivo inalterado → `Unchanged`
- arquivo modificado, doc limpo → `ChangedClean`
- arquivo modificado, doc sujo → `ChangedDirty`
- arquivo removido → `Deleted`

Segue o padrão de `FileServiceTests` / `WorkspaceServiceTests`.

## Fora de escopo

- Watcher ao vivo / atualização de abas em segundo plano.
- Preservar cursor/seleção do editor no reload (apenas scroll do editor).
- Merge de conteúdo em conflito (decisão é recarregar-ou-manter, não merge).
