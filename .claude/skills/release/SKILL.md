---
name: release
description: Publica um novo release do Meu Markdown — bump de versão (Directory.Build.props), build, gerar instalador, commit, tag git, push, e criar GitHub Release com asset anexado. Use SEMPRE que o usuário pedir "release", "publicar release", "subir release", "lançar versão", "shipa", "publish", "bump + publish", "vamos pra X.Y.Z", "sobe pro GitHub", ou qualquer variação que envolva ato de publicar uma nova versão pública. Não use só pra build local sem publicar.
---

# Skill: release

Automatiza todo o pipeline de release pro Meu Markdown. Usuário invoca, escolhe a versão alvo, confirma os release notes gerados, e a skill executa o resto.

## Pré-requisitos do ambiente

Antes de tudo, valide. Se algo falhar, **pare e reporte** — não tente contornar.

- Working tree limpo (`git status --porcelain` retorna vazio)
- Branch atual é `main` (`git branch --show-current` = `main`)
- `Directory.Build.props` existe na raiz com tag `<Version>`
- `gh` CLI disponível e autenticado (tente `gh auth status`; se `gh` não estiver no PATH, busque em `C:\Program Files\GitHub CLI\gh.exe`)
- Inno Setup ISCC em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`

## Fluxo de decisão da versão alvo

A versão alvo vem do argumento do usuário ou é decidida interativamente:

| Input do usuário | Comportamento |
|---|---|
| `/release 1.3.0` (versão exata) | usa `1.3.0` |
| `/release patch` | incrementa o patch do último tag |
| `/release minor` | incrementa o minor (zera patch) |
| `/release major` | incrementa o major (zera minor e patch) |
| `/release` (sem argumento) | pergunte via AskUserQuestion: patch / minor / major / versão específica |

Última versão = `git describe --tags --abbrev=0` (strip do `v` inicial). Se não houver tag nenhum, leia de `Directory.Build.props`.

Valide que a versão alvo é **maior** que a atual via comparação semver simples (split por `.`, compara como ints). Se for menor ou igual, pare e avise.

## Gerar release notes a partir dos commits

Esta é a parte mais importante — release notes vazios ou genéricos são piores que ausência. Faça assim:

1. **Pegue os commits desde o último tag:**
   ```bash
   git log $(git describe --tags --abbrev=0)..HEAD --pretty=format:"%H|%s|%b---END---" --no-merges
   ```
   Se for o primeiro release (sem tag prévia), use `git log --pretty=...`.

2. **Filtre ruído:**
   - Commits com subject começando `chore: bump` (commits de versão anteriores)
   - Commits com subject só `Merge ...`
   - Commits de docs internos do plano (`docs(plano):...`, `docs(spec):...`) — esses são meta, não user-facing

3. **Agrupe por tipo de Conventional Commit:**

   | Prefixo | Seção em PT-BR |
   |---|---|
   | `feat`, `feat(...)` | **Novidades** |
   | `fix`, `fix(...)` | **Correções** |
   | `refactor`, `refactor(...)` | **Refatoração** |
   | `perf`, `perf(...)` | **Performance** |
   | `style`, `style(...)` | **Estilo** |
   | `docs`, `docs(...)` (não-internos) | **Documentação** |
   | `chore`, `chore(...)` (exceto bump) | **Manutenção** |
   | qualquer outro / sem prefixo | **Outros** |

   Seções sem itens não aparecem no output.

4. **Formate cada item** removendo o prefixo Conventional Commit e capitalizando a primeira letra. Se o subject já é descritivo o suficiente, use-o. Se tiver scope (`feat(MainWindow):`), pode manter o scope como prefixo amigável (`MainWindow: ...`) — bom senso.

5. **Sempre inclua o footer de instalação:**
   ```markdown
   ## Instalação

   Baixe `MeuMarkdown-Setup-vX.Y.Z.exe` e execute. Instala por usuário (sem precisar de admin) por padrão.
   ```

6. **Exemplo de output:**

   ```markdown
   ## Novidades

   - **Menu Ajuda com modal Sobre**
   - **Verificar atualizações** consulta o GitHub Releases

   ## Correções

   - Title bar não fica mais estreita ao maximizar

   ## Refatoração

   - Versionamento single-source via Directory.Build.props

   ## Instalação

   Baixe `MeuMarkdown-Setup-v1.2.0.exe` e execute. Instala por usuário (sem precisar de admin) por padrão.
   ```

## Confirmação antes de agir

Após gerar a versão alvo e o markdown dos notes, **mostre tudo ao usuário** e pergunte se pode prosseguir. Layout:

```
═══════════════════════════════════════════════════
Release v1.3.0  (atual: v1.2.0)

Vou executar:
  1. Atualizar Directory.Build.props → <Version>1.3.0</Version>
  2. dotnet build + dotnet test
  3. dotnet publish -c Release
  4. ISCC com /DAppVersion=1.3.0 → installer/dist/MeuMarkdown-Setup-v1.3.0.exe
  5. git commit -am "chore: bump 1.3.0"
  6. git tag v1.3.0
  7. git push origin main v1.3.0
  8. gh release create v1.3.0 ... com os notes abaixo

Release notes:
─── (markdown gerado) ───
═══════════════════════════════════════════════════
```

Use `AskUserQuestion` com opções: **Pode seguir** / **Editar notes** / **Cancelar**.

Se "Editar notes": ofereça o markdown num arquivo temporário pra usuário editar e cole o resultado. Ou aceite o markdown atualizado em texto livre.

Só prossiga após confirmação explícita.

## Execução

Faça os passos em ordem. **Pare e reporte na primeira falha** — não pule pra "passo seguinte" silenciosamente.

```bash
# 1. Bump em Directory.Build.props (substitui só o conteúdo da tag Version)
#    Use Edit tool — não regex no bat.

# 2. Build + tests
dotnet build src/MeuMarkdown/MeuMarkdown.csproj
dotnet test

# 3. Publish (single-file, self-contained)
dotnet publish src/MeuMarkdown/MeuMarkdown.csproj -c Release

# 4. Gerar instalador (PowerShell, NÃO bash — /D conflita com path no Git Bash)
#    via PowerShell tool:
#    & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=X.Y.Z installer\MeuMarkdown.iss

# 5. Verificar artefato
ls installer/dist/MeuMarkdown-Setup-vX.Y.Z.exe

# 6. Commit, tag, push (usar rtk)
rtk git add Directory.Build.props
rtk git commit -m "chore: bump X.Y.Z"
rtk git tag vX.Y.Z
rtk git push origin main vX.Y.Z

# 7. Release notes em arquivo temp
#    Write tool: %TEMP%\release-notes-vX.Y.Z.md

# 8. Criar release (PowerShell, gh pode não estar no PATH)
#    & "C:\Program Files\GitHub CLI\gh.exe" release create vX.Y.Z installer\dist\MeuMarkdown-Setup-vX.Y.Z.exe --title "vX.Y.Z" --notes-file "$env:TEMP\release-notes-vX.Y.Z.md"
```

## Falhas e rollback

| Onde falhou | O que reverter |
|---|---|
| Build ou test | Reverter `Directory.Build.props` (`git checkout -- Directory.Build.props`) |
| Inno Setup falha | Reverter `Directory.Build.props` |
| Push falha | Deletar tag local (`git tag -d vX.Y.Z`) — commits podem ficar localmente |
| `gh release create` falha | Tag remoto pode existir mas sem release; usuário pode reusar via `gh release create` retry, OU `gh release delete vX.Y.Z && git push --delete origin vX.Y.Z` se quiser refazer do zero |

Em qualquer falha, **diga claramente** o que aconteceu, o que reverteu, e o que o usuário pode fazer pra retomar.

## Resumo final

Quando der certo, mostre:

```
✅ Release v1.3.0 publicado

  Tag:       v1.3.0
  Asset:     MeuMarkdown-Setup-v1.3.0.exe (XX MB)
  URL:       https://github.com/<owner>/<repo>/releases/tag/v1.3.0

  Próximo bump: edite Directory.Build.props ou invoque /release de novo.
```

URL do release vem do output do `gh release create` (ele imprime a URL).

## Notas de implementação para o agente

- **Não tente fazer tudo numa única chamada gigante.** Cada passo é uma chamada de tool clara, com verificação de output. É melhor demorar 30s a mais e parar limpo no erro do que tentar continuar e deixar repositório inconsistente.

- **`rtk` prefix** nos comandos `git`/`dotnet`/`gh` quando rodando via Bash. Por que: padrão global do usuário (vide CLAUDE.md global). Funciona com qualquer comando, sempre seguro.

- **PowerShell vs Bash:** use PowerShell pra invocar `ISCC.exe` e `gh.exe` quando o argumento começa com `/` (Git Bash interpreta `/D` como path POSIX e quebra). Pra `git` e `dotnet`, Bash com `rtk` é mais limpo.

- **Não bumpe se já há mudanças não commitadas que NÃO são o bump.** Pre-flight cobre isso, mas se algo escapou (ex: rebuild gera arquivos no `obj/`), só comite `Directory.Build.props` explicitamente — não `git add -A`.

- **Não toque em outros arquivos de versão** (SplashWindow, AboutWindow). Eles leem do assembly em runtime via `VersionInfo.cs` — `Directory.Build.props` é a fonte única. Se descobrir referência hardcoded, sinalize ao usuário ao invés de "consertar" silenciosamente.
