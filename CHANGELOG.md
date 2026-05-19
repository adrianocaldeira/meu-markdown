# Changelog

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adota [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Não lançado]

## [1.7.5] — 2026

### Removido
- Diálogo "Atualizar antes de sair?" no fechamento do app. A UX ficava confusa (clicar Não silenciava a versão pra sempre) e com o check periódico de 30min em vigor o caso ficou menos necessário. Agora a verificação de atualização acontece apenas via toast (no startup + a cada 30min) ou manualmente via Ajuda → Verificar atualizações.
- Checagem do campo `dismissedUpdateVersion` no toast — quem foi pego pelo diálogo C antigo agora volta a receber notificações normalmente.

## [1.7.4] — 2026

### Adicionado
- Clique direito numa área vazia do preview (sem seleção/link/imagem) agora mostra **Exportar para HTML…** e **Exportar para PDF…** como atalho — antes o menu ficava vazio nesse cenário.
- Verificação periódica de atualização: a cada 30 minutos, enquanto o app está aberto, o BG_CHECK roda de novo e mostra o toast se sair versão nova. Antes só verificava no startup — quem deixava o app aberto o dia todo nunca via novas versões até reabrir.

## [1.7.3] — 2026

### Mudado
- Toast de atualização agora é persistente — fica visível até você clicar em "Atualizar agora", "Mais tarde" ou "×". Antes ele sumia quando você ia pra outra janela do Windows e voltava só ao recuperar foco, dando a impressão de "sumiu sem fazer nada". Tradeoff conhecido: como o toast é um `Popup` WPF, ele aparece sobre outras apps quando o Meu Markdown está em background.
- Diálogo "Atualizar antes de sair?" (no fechamento do app) agora tem **Cancelar** como botão padrão em vez de "Sair sem atualizar". Apertar Enter sem ler antes dispensava a versão silenciosamente; agora apertar Enter cancela o fechamento.

### Adicionado
- Verificação silenciosa de atualização agora tenta de novo se falhar por erro de rede: até 3 tentativas com delays de 0s, 30s e 90s. Pega cenários onde a rede está instável no momento exato do startup.

## [1.7.2] — 2026

### Corrigido
- Drag-and-drop pra reordenar abas não funcionava: o `TabItem` consumia os eventos de mouse antes do nosso handler. Trocado por `PreviewMouseLeftButtonDown`/`PreviewMouseMove` (tunnel events) — agora o drag inicia corretamente.

## [1.7.1] — 2026

### Adicionado
- Link "Saiba mais" no toast de atualização — abre a página completa do release no GitHub pra você ver as notas inteiras antes de atualizar.

### Mudado
- Toast de atualização agora some quando o app perde foco e reaparece quando você volta. Antes ficava topmost sobre TODAS as janelas do Windows (mesmo quando você ia usar outra aplicação).
- Toast ficou mais largo (480px) e mostra mais bullets das release notes (até 5 itens, até 350 caracteres) — antes o texto era truncado cedo demais.

### Corrigido
- Janela de progresso do auto-update não aparecia visualmente quando o user clicava "Atualizar agora" no toast — a janela era criada mas o download começava antes dela ter tempo de pintar, dando a impressão de "tela travada com o instalador na frente". Agora damos tempo da janela renderizar antes de iniciar o download.
- Instalador mostrava o erro "Incapaz de fechar automaticamente todos os aplicativos" porque o setup era disparado mais rápido do que o app conseguia fechar. Agora o setup é lançado via `cmd /c timeout 2 & start setup.exe` — dá 2 segundos pro app fechar antes do Inno tentar copiar os arquivos. Também trocado `/SILENT` por `/VERYSILENT /SUPPRESSMSGBOXES` pra esconder UI residual do Inno e suprimir prompts de erro.

## [1.7.0] — 2026

### Adicionado
- Menu de contexto no preview: clique direito sobre texto selecionado mostra **Copiar**; sobre link mostra **Copiar endereço do link**; sobre imagem mostra **Copiar imagem**. Itens nativos não-úteis do Edge (Print, Reload, Save As, etc.) ficam ocultos.

## [1.6.3] — 2026

### Corrigido
- Clicar num item do Outline não scrollava o preview até o heading quando o app estava em modo Visualização (F5). Causa: o handler só movia o editor (que está oculto nesse modo). Agora dispara o scroll do preview também.

## [1.6.2] — 2026

### Mudado
- Toast de atualização redesenhado pra ficar mais visível: layout maior com ícone destacado à esquerda, botão "Atualizar agora" em cor de destaque sólida, animação slide-up subindo da base (em vez do slide horizontal anterior). Inspirado no estilo do Claude Desktop.
- Toast agora mostra um resumo das release notes da nova versão (primeiros 3 bullets extraídos do CHANGELOG) — assim você vê o que mudou antes de decidir atualizar.

## [1.6.1] — 2026

### Corrigido
- Hover do Explorer (TreeView) destacava vários itens ao mesmo tempo ao passar o mouse — pai, avô e item ficavam todos coloridos. Causa: `IsMouseOver` em WPF propaga pra ancestrais. Agora o hover observa só o `IsMouseOver` do header do próprio item, isolando o efeito.

## [1.6.0] — 2026

### Adicionado
- Drag-and-drop pra reordenar abas. Clique e arraste uma aba pra trocar de posição com outra. Funciona com abas fixadas também.
- Toast de "nova versão disponível" agora tem animação de entrada (slide-in da direita + fade-in) e borda em cor de destaque, pra chamar mais atenção.

### Corrigido
- Toast de atualização não aparecia na prática mesmo com o log dizendo "toast shown". Causa: o `WebView2` (preview) é uma janela nativa do Windows dentro do WPF — devido ao "airspace problem", qualquer elemento WPF tentando renderizar sobre o WebView2 fica invisível. O toast estava posicionado exatamente em cima da área do preview. Resolvido envolvendo o toast num `Popup` WPF, que roda em hwnd próprio e fica naturalmente acima.
- Preview ficava mostrando o HTML da última aba quando você fechava a única aba aberta. Agora o preview também é limpo quando não há aba ativa.

## [1.5.3] — 2026

### Corrigido
- Explorer não fecha mais as pastas abertas sozinho quando há mudança nos arquivos (save, rename, criar). Causa: o rebuild da árvore após eventos do `FileSystemWatcher` criava `FileNode`s novos com `IsExpanded=false`, perdendo todo o estado. Agora a árvore é reconstruída preservando quais pastas estavam abertas e o arquivo selecionado.
- Aumentado o debounce do rebuild da árvore de 300ms para 800ms — agrupa melhor rajadas de eventos (build, git operations, save em massa) e reduz o pisca visual.

## [1.5.2] — 2026

### Corrigido
- Menu de contexto das abas (clique direito) — nenhum dos itens funcionava. Causa: o `Command` binding com `RelativeSource={AncestorType=Window}` não resolve dentro de `ContextMenu` (que vive em popup separado, fora do visual tree). Trocado por `Click` handlers no code-behind.

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

[Não lançado]: https://github.com/adrianocaldeira/meu-markdown/compare/v1.7.5...HEAD
[1.7.5]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.5
[1.7.4]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.4
[1.7.3]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.3
[1.7.2]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.2
[1.7.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.1
[1.7.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.7.0
[1.6.3]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.6.3
[1.6.2]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.6.2
[1.6.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.6.1
[1.6.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.6.0
[1.5.3]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.5.3
[1.5.2]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.5.2
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
