# Contribuindo

Obrigado por considerar contribuir! Bug, ideia ou PR é bem-vindo.

## Como reportar bug ou pedir feature

- **Bugs** → [Issue templates](https://github.com/adrianocaldeira/meu-markdown/issues/new/choose) → "Bug report"
- **Ideias** → [Discussions](https://github.com/adrianocaldeira/meu-markdown/discussions) primeiro (alinha antes de codar)
- **Vulnerabilidade de segurança** → não abra issue pública. Veja [SECURITY.md](SECURITY.md).

## Setup local

```powershell
git clone https://github.com/adrianocaldeira/meu-markdown.git
cd meu-markdown
dotnet build src/MeuMarkdown/MeuMarkdown.csproj
dotnet run --project src/MeuMarkdown/MeuMarkdown.csproj
```

**Requisitos:**
- Windows 10 (1809+) ou Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 17.12+ ou Rider 2024.3+ (qualquer IDE com suporte a .NET 10)

### Ativar o pre-commit hook (recomendado)

O repo já está configurado pra usar `.githooks/`. Garante que o hook ativa:

```powershell
git config core.hooksPath .githooks
```

Para o hook efetivamente varrer segredos antes do commit, instale `gitleaks`:

```powershell
winget install gitleaks
```

## Rodar testes

```powershell
dotnet test
```

Os testes ficam em `tests/MeuMarkdown.Tests/` e usam **xUnit**.

## Padrão de commits

Seguimos [Conventional Commits](https://www.conventionalcommits.org/pt-br/) em português:

```
<tipo>(<escopo opcional>): descrição curta no imperativo

[corpo opcional explicando o porquê]
```

**Tipos:** `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `style`, `build`, `ci`.

Exemplos reais do histórico:

```
feat(Outline): painel mostra headings clicáveis em árvore
fix(MainWindow): maximize respeita work area do monitor (taskbar visível)
refactor(Theme.Common): MenuItem template usa cor do tema
chore: bump 1.1.0 → 1.2.0
```

## Estilo de código

- Siga o estilo existente nos arquivos que está alterando
- C# 12+ features são bem-vindas (.NET 10 suporta)
- Padrão **MVVM** com `CommunityToolkit.Mvvm` (use `[ObservableProperty]`, `[RelayCommand]`)
- Nullable habilitado — trate `null` explicitamente

## Fluxo de PR

1. Fork → branch a partir de `main`
2. Commits pequenos e focados, seguindo o padrão acima
3. Adicione/atualize testes quando aplicável
4. `dotnet build` e `dotnet test` devem passar
5. Abra o PR — descreva **o porquê** da mudança, não só o quê
6. Mencione issue relacionada se houver (`Closes #123`)

## Versionamento

Usamos [SemVer](https://semver.org/lang/pt-BR/):

- **MAJOR** — quebra de compatibilidade real (ex: mudança no formato do `state.json` sem migração)
- **MINOR** — feature compatível
- **PATCH** — bugfix

Para subir versão antes de release:

```powershell
.\scripts\bump-version.ps1 1.2.0
```

O script atualiza `.csproj` e `installer/MeuMarkdown.iss` atomicamente.

## Dúvidas?

Abra uma [discussion](https://github.com/adrianocaldeira/meu-markdown/discussions/new/choose).
