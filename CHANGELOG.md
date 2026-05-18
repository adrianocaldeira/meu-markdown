# Changelog

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adota [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Não lançado]

## [1.5.1] — 2026

### Mudado
- Verificação silenciosa de atualização agora grava eventos em `%LocalAppData%\MeuMarkdown\update.log` (scheduled / starting / result / toast / exception). Ajuda a diagnosticar quando o toast não aparece (timing, rede, dispensa, etc.).

## [1.5.0] — 2026

### Adicionado
- Menu de contexto nas abas (clique direito) com: Fechar esta aba · Fixar/Desafixar · Fechar todas exceto esta · Fechar todas exceto as fixadas · Fechar à esquerda · Fechar à direita · Fechar inalteradas. Todos os itens têm ícone.
- Abas fixadas (pinned): ícone de pin antes do nome no tab strip, estado persistido em `state.json` entre sessões. Fixar protege a aba do "fechar todas exceto fixadas".

## [1.4.0] — 2026

### Adicionado
- Verificação silenciosa de atualização no startup: ~10s após abrir o app, ele consulta o GitHub em background e mostra um toast discreto no canto inferior se houver versão nova ("Atualizar agora" / "Mais tarde").
- Diálogo "Atualizar antes de sair?" no fechamento do app quando há versão nova pendente que você ainda não atualizou nem dispensou. Três opções: atualizar, sair sem atualizar (dispensa essa versão pra não perguntar de novo), ou cancelar o fechamento.
- Nova preferência `dismissedUpdateVersion` em `state.json` pra lembrar qual versão você dispensou explicitamente (o app só volta a sugerir quando sair uma versão maior).

## [1.3.6] — 2026

### Corrigido
- Item selecionado no Explorer (TreeView) agora usa as cores do tema em vez do azul/cinza padrão do Windows que ficava ilegível no dark mode.
- Ao trocar de aba, o Explorer rola automaticamente até o arquivo da aba ativa ficar visível na árvore (antes só expandia as pastas, podia ficar fora do viewport).

## [1.3.5] — 2026

### Corrigido
- Preview vinha em branco (com tema light) quando o app restaurava abas no startup em tema escuro. Causa: `SetDarkTheme()` era chamado antes do WebView2 inicializar e o tema acumulado nunca era reaplicado pós-init.
- Modo de visualização (F5) não era preservado entre sessões — agora persiste em `state.json` junto das outras preferências.
- Explorer agora expande automaticamente as pastas até revelar o arquivo da aba ativa, em vez de manter a árvore toda colapsada.

## [1.3.4] — 2026

### Corrigido
- Sessão é preservada corretamente após auto-update: abas abertas, aba ativa, workspace, layout da sidebar e preferências eram perdidos quando o app reabria. Causa: `Application.Shutdown()` no fim do auto-update não disparava `Window.Closing`, então o `state.json` não era salvo.
- Lista de abas abertas e aba ativa agora são realmente persistidas entre sessões (bug pré-existente — os campos `openTabs` e `activeTab` existiam em `state.json` mas nunca eram populados nem restaurados).

## [1.3.3] — 2026

### Corrigido
- Auto-update agora relança o app automaticamente depois de instalar a nova versão. Antes, o setup terminava silenciosamente e o app ficava fechado — agora ele abre sozinho na versão nova.

## [1.3.2] — 2026

### Corrigido
- Menus de contexto (clique direito em arquivos do Explorer, botão "more" no header da panel, etc.) agora respeitam o tema do app — antes mostravam o chrome cinza-claro padrão do Windows, ignorando dark/light mode.

## [1.3.1] — 2026

### Adicionado
- Links para o repositório no GitHub e a wiki na janela "Sobre" (Ajuda → Sobre).

## [1.3.0] — 2026

### Adicionado
- Auto-update "1-click" na janela "Verificar atualizações": baixa o instalador do GitHub, valida SHA-256 e executa o upgrade in-place silenciosamente. Funciona pra instalações per-user e per-machine (com UAC). Versões portable continuam abrindo o browser.
- Detecção automática do tipo de instalação (per-user / per-machine / portable) via nova chave de registro `Software\MeuMarkdown\InstallScope`.
- Log local em `%LocalAppData%\MeuMarkdown\update.log` pra debug de atualizações.

### Mudado
- Botão "Baixar instalador" virou "Atualizar agora" pras instalações com suporte a auto-update.

## [1.2.1] — 2026

### Corrigido
- `UpdateService`: desabilita autodetecção WPAD do `WebProxy` pra evitar timeout de ~20s na verificação de atualizações quando rodando a partir do `.exe` instalado

## [1.2.0] — 2026

### Adicionado
- Workflows de CI (build/test) e Release (build + instalador + GitHub Release)
- Pre-commit hook via gitleaks
- Templates de issue e pull request
- `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`
- README e wiki

## [1.1.0] — 2026

### Adicionado
- **Workspace estilo VS Code** com Activity Bar e 4 painéis (Explorer, Outline, Search, Settings)
- **Quick Switcher** (`Ctrl+P`) com fuzzy match entre arquivos do workspace
- **Busca no workspace inteiro** com regex e case sensitive
- **Outline** automático dos headings do documento ativo
- **Tema claro/escuro** com paleta cuidada (warm Claude-like no dark)
- **Custom window chrome** (janela sem borda nativa)
- **Zen mode** (`F11`) e **Typewriter mode** (`Ctrl+Shift+T`)
- Atalhos de formatação Markdown (negrito, itálico, link, headings, código)
- **Find & Replace** na aba ativa com `Ctrl+F`/`Ctrl+H`/`F3`
- Auto-pair de delimitadores e continuação automática de listas
- Detecção de bloco de código (não-formatação dentro de fenced code)
- Persistência de estado da janela e abas abertas em `%AppData%/MeuMarkdown/`
- Associação de arquivos `.md` e `.markdown` (via instalador)
- Splash screen ao iniciar
- AboutWindow

### Mudado
- Portado para **.NET 10**
- Refatoração visual completa para o sistema de tema baseado em `DynamicResource`
- Ícones emoji substituídos por outline (Lucide)
- Sistema de syntax highlighting com cores warm para heading/code/quote

## [1.0.0] — 2026

### Adicionado
- Editor Markdown com syntax highlighting (AvalonEdit)
- Preview ao vivo com debounce de 300 ms (WebView2 + Markdig)
- Múltiplas abas
- Navegação entre arquivos `.md` linkados com histórico back/forward
- Exportação para HTML
- Recent files

[Não lançado]: https://github.com/adrianocaldeira/meu-markdown/compare/v1.5.1...HEAD
[1.5.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.5.1
[1.5.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.5.0
[1.4.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.4.0
[1.3.6]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.6
[1.3.5]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.5
[1.3.4]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.4
[1.3.3]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.3
[1.3.2]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.2
[1.3.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.1
[1.3.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.0
[1.2.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.2.1
[1.2.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.2.0
[1.1.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.1.0
[1.0.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.0.0
