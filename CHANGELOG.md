# Changelog

Todas as mudanças relevantes deste projeto são documentadas neste arquivo.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e o projeto adota [Semantic Versioning](https://semver.org/lang/pt-BR/).

## [Não lançado]

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

[Não lançado]: https://github.com/adrianocaldeira/meu-markdown/compare/v1.3.1...HEAD
[1.3.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.1
[1.3.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.3.0
[1.2.1]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.2.1
[1.2.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.2.0
[1.1.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.1.0
[1.0.0]: https://github.com/adrianocaldeira/meu-markdown/releases/tag/v1.0.0
