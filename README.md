<div align="center">

# Meu Markdown

**Editor e visualizador Markdown para Windows. Rápido, focado, sem nuvem.**

[![versão](https://img.shields.io/github/v/release/adrianocaldeira/meu-markdown?label=vers%C3%A3o&color=2ea44f)](https://github.com/adrianocaldeira/meu-markdown/releases)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6?logo=windows&logoColor=white)](#instalação)
[![license](https://img.shields.io/github/license/adrianocaldeira/meu-markdown?color=blue)](LICENSE)
[![CI](https://img.shields.io/github/actions/workflow/status/adrianocaldeira/meu-markdown/ci.yml?branch=main&label=build)](https://github.com/adrianocaldeira/meu-markdown/actions/workflows/ci.yml)
[![downloads](https://img.shields.io/github/downloads/adrianocaldeira/meu-markdown/total?color=blueviolet)](https://github.com/adrianocaldeira/meu-markdown/releases)

[**⬇️ Baixar**](https://github.com/adrianocaldeira/meu-markdown/releases/latest) ·
[**📖 Wiki**](https://github.com/adrianocaldeira/meu-markdown/wiki) ·
[**🗺️ Roadmap**](#-roadmap) ·
[**🐛 Reportar bug**](https://github.com/adrianocaldeira/meu-markdown/issues/new/choose)

<br>

<img src="docs/assets/hero.png" alt="Meu Markdown — interface principal" width="820">

</div>

---

## ✨ Por que existe

Existem ótimos editores Markdown. A maioria é Electron pesado, depende de nuvem, exige login, ou força um workflow específico.

**Meu Markdown** é um app Windows nativo de **um único `.exe`**, que abre seus arquivos locais, mostra o preview ao lado, e segue o caminho — sem telemetria, sem conta, sem download de extensão.

## 🎬 Em ação

<div align="center">
<img src="docs/assets/demo.gif" alt="Demo: editor + preview live + navegação entre arquivos" width="720">
</div>

## 🚀 Funcionalidades

| | | |
|---|---|---|
| ✍️ **Editor potente** com syntax highlighting [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | 👁️ **Preview ao vivo** via [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) + [Markdig](https://github.com/xoofx/markdig) (GFM) | 🔗 **Navegação entre `.md`** com histórico back/forward |
| 🗂️ **Workspace** estilo VS Code (Explorer + Outline) | 🔍 **Busca no workspace inteiro** com regex e case sensitive | ⚡ **Quick Switcher** (`Ctrl+P`) com fuzzy match |
| 🌗 **Tema claro/escuro** com paleta cuidada | 📦 **Single-file `.exe`** (~165 MB self-contained, sem instalação obrigatória) | 🇧🇷 **100% em português** |
| ⌨️ **Atalhos de formatação** (negrito, link, headings, etc.) | 🧘 **Zen mode** (F11) e Typewriter mode | 📤 **Exportação para HTML** |

## 📥 Instalação

### Opção 1 — Instalador (recomendado)

Baixe `MeuMarkdown-Setup-vX.Y.Z.exe` na [página de releases](https://github.com/adrianocaldeira/meu-markdown/releases/latest) e execute. Pede privilégios mínimos (não precisa de admin) e associa `.md`/`.markdown` opcionalmente.

### Opção 2 — Portable (sem instalar)

Baixe `MeuMarkdown.exe` na mesma página. Coloque onde quiser e execute. Self-contained — não precisa de .NET runtime na máquina.

### Opção 3 — Compilar do código

```powershell
# Requisitos: .NET 10 SDK
git clone https://github.com/adrianocaldeira/meu-markdown.git
cd meu-markdown
dotnet run --project src/MeuMarkdown/MeuMarkdown.csproj
```

> [!NOTE]
> Windows 10 (1809+) ou Windows 11. O runtime **WebView2** já vem pré-instalado no Windows 11; no Windows 10 mais antigo, [baixe aqui](https://developer.microsoft.com/microsoft-edge/webview2/) se necessário.

## 🎯 Uso rápido

1. **Abra um arquivo** com `Ctrl+O` ou arraste um `.md` pra dentro da janela.
2. **Abra uma pasta inteira** pelo menu Arquivo → Abrir pasta — vira workspace navegável no painel Explorer.
3. **Edite à esquerda, veja o preview à direita** — atualiza em ~300 ms.
4. **Pule entre arquivos** com `Ctrl+P` (Quick Switcher fuzzy).
5. **Clique em links `.md`** no preview — abre em nova aba automaticamente.
6. **Exporte como HTML** pelo menu Arquivo → Exportar.

## ⌨️ Atalhos

<details>
<summary><b>Arquivos e navegação</b></summary>

| Atalho | Ação |
|---|---|
| `Ctrl+O` | Abrir arquivo |
| `Ctrl+S` | Salvar |
| `Ctrl+W` | Fechar aba |
| `Ctrl+P` | Quick Switcher (busca fuzzy entre arquivos do workspace) |
| `Alt+←` / `Alt+→` | Voltar / Avançar no histórico de navegação |

</details>

<details>
<summary><b>Formatação Markdown</b></summary>

| Atalho | Ação |
|---|---|
| `Ctrl+B` | **Negrito** |
| `Ctrl+I` | *Itálico* |
| `Ctrl+Shift+S` | ~~Strikethrough~~ |
| `Ctrl+K` | Link |
| `Ctrl+E` | `Código inline` |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Heading H1 / H2 / H3 |

</details>

<details>
<summary><b>Busca</b></summary>

| Atalho | Ação |
|---|---|
| `Ctrl+F` | Buscar na aba ativa |
| `Ctrl+H` | Substituir |
| `F3` / `Shift+F3` | Próximo / anterior |

</details>

<details>
<summary><b>Interface e leitura</b></summary>

| Atalho | Ação |
|---|---|
| `F5` | Alternar modo de visualização (editor / preview / split) |
| `F6` | Alternar tema claro/escuro |
| `F10` | Zen Solo (foco no editor, sem sidebar) |
| `F11` | Zen Mode (tela cheia minimalista) |
| `Ctrl+\` | Mostrar/ocultar sidebar |
| `Ctrl+Shift+O` | Abrir painel Outline (headings do documento) |
| `Ctrl+Shift+T` | Typewriter mode (linha ativa sempre no centro) |

</details>

## 🧱 Stack técnica

- **[.NET 10](https://dotnet.microsoft.com/)** + **WPF** — app nativo Windows
- **[AvalonEdit](https://github.com/icsharpcode/AvalonEdit)** — editor com syntax highlighting customizada
- **[Markdig](https://github.com/xoofx/markdig)** — conversão Markdown → HTML (GitHub Flavored)
- **[WebView2](https://learn.microsoft.com/microsoft-edge/webview2/)** — renderização HTML do preview
- **[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)** — source generators MVVM
- **[Inno Setup](https://jrsoftware.org/isinfo.php)** — empacotamento do instalador

Arquitetura **MVVM** simples e enxuta — detalhes na [Wiki/Arquitetura](https://github.com/adrianocaldeira/meu-markdown/wiki/Arquitetura).

## 🗺️ Roadmap

### ✅ Já entregue (v1.1.0)
- Editor + preview live com syntax highlighting
- Múltiplas abas, navegação entre `.md` linkados com histórico
- Workspace (Explorer + Outline + Busca no workspace)
- Quick Switcher fuzzy
- Tema claro/escuro
- Associação de arquivos `.md`/`.markdown`
- Custom window chrome
- Zen mode / Typewriter mode

### 🚧 Em estudo
- Snippets / templates de documento
- Export para PDF nativo
- Suporte a Mermaid e diagramas
- Plugin/extension model

### 💭 Ideias
- Sync opcional via Git (push pra repo do usuário)
- Tema customizável pelo usuário

Sugestões? [Abra uma discussion](https://github.com/adrianocaldeira/meu-markdown/discussions) ou [crie uma issue](https://github.com/adrianocaldeira/meu-markdown/issues/new/choose).

## 🤝 Contribuindo

Bug, ideia ou PR é bem-vindo. Leia [CONTRIBUTING.md](CONTRIBUTING.md) pra setup local e padrão de commits, e [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## 🔒 Segurança

Reportar vulnerabilidade: ver [SECURITY.md](SECURITY.md).

## 📄 Licença

[MIT](LICENSE) © Adriano Caldeira

## 🙏 Agradecimentos

Ombros de gigantes: [AvalonEdit](https://github.com/icsharpcode/AvalonEdit), [Markdig](https://github.com/xoofx/markdig), [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), [Lucide Icons](https://lucide.dev/), [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/), [Inno Setup](https://jrsoftware.org/isinfo.php).
