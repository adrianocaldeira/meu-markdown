---
name: release
description: Publica um novo release do Meu Markdown — bump em Directory.Build.props, atualiza CHANGELOG.md (move [Não lançado] → [X.Y.Z]) e README.md (seção Já entregue, se MINOR/MAJOR), commit + tag + push. O GitHub Actions release.yml dispara automaticamente ao receber a tag e cuida do build, instalador, anexo de assets e criação do GitHub Release. Use SEMPRE que o usuário pedir "release", "publicar release", "subir release", "lançar versão", "shipa", "publish", "vamos pra X.Y.Z", "sobe pro GitHub", ou qualquer variação que envolva ato de publicar uma nova versão pública. Não use só pra build local sem publicar.
---

# Skill: release

Automatiza a publicação de novas versões do Meu Markdown. A skill **prepara** o commit (bump + docs) e **dispara** o pipeline via tag push — o workflow `.github/workflows/release.yml` faz o trabalho pesado (build, instalador, GitHub Release).

## Arquitetura — quem faz o quê

| Responsabilidade | Onde |
|---|---|
| Bump da versão | `scripts/bump-version.ps1` (chamado pela skill com `-NoCommit`) |
| Atualizar CHANGELOG.md | **Skill** |
| Atualizar README.md (seção "Já entregue") | **Skill** (apenas MINOR/MAJOR) |
| Commit único + tag + push | **Skill** |
| Build + publish + instalador | `.github/workflows/release.yml` (GitHub Actions) |
| Criar GitHub Release + anexar assets | `.github/workflows/release.yml` |
| Release notes da publicação | Workflow extrai a seção `[X.Y.Z]` do CHANGELOG.md (fonte única) |

Princípio: **CHANGELOG.md é a fonte canônica das release notes**. A skill garante que a seção `[X.Y.Z]` exista e o workflow a publica.

## Pré-requisitos

Antes de tudo, valide. Se algo falhar, **pare e reporte** — não tente contornar.

- Working tree limpo (`git status --porcelain` vazio)
- Branch atual é `main` (`git branch --show-current`)
- Arquivos esperados existem na raiz: `Directory.Build.props`, `CHANGELOG.md`, `README.md`, `scripts/bump-version.ps1`
- `gh` CLI disponível + autenticado. Se `gh` não estiver no PATH, use `C:\Program Files\GitHub CLI\gh.exe`. Verifique auth com `gh auth status`
- Existe o workflow `.github/workflows/release.yml` (a tag vai disparar ele)

Avisos não-bloqueantes (mostre ao usuário mas não pare):

**Wiki** — o repositório em `D:\src\meu-markdown.wiki\` (remoto `<repo>.wiki.git`) é **evergreen por design** — sem versão hardcoded, URLs usam `vX.Y.Z` genérico ou `releases/latest`. Na maioria dos releases (patch, minor sem feature destacável), **não há nada pra tocar no wiki**.

Sinalize ao usuário pra **considerar revisar** uma destas páginas só quando se aplicar:

| Mudança no release | Página a revisar |
|---|---|
| Nova feature destacável (`feat:` significativo) | `Funcionalidades.md` |
| Novo/removido atalho de teclado | `Atalhos-de-teclado.md` |
| Mudança em fluxo de onboarding | `Primeiros-passos.md` |
| Mudança arquitetural (MVVM, fluxos, schemes) | `Arquitetura.md` |
| Mudança no processo de release/versionamento | `Versao-e-atualizacoes.md` |
| Nova dependência runtime (.NET nova versão, Windows version mínimo) | `Instalacao.md` |
| Adicionou página nova ao wiki | `_Sidebar.md`, `_Footer.md` |

Como a skill detecta:
- Se tem `feat(` nos commits desde último tag → flag `Funcionalidades.md`
- Se tem mudança em `MainWindow.xaml` KeyBindings ou `InputBindings` → flag `Atalhos-de-teclado.md`
- Se é MAJOR → flag genérico "revise wiki em geral"

Se `D:\src\meu-markdown.wiki` existir e tiver `uncommitted changes` ou commits locais não pushados, mostre antes:
> "⚠ Wiki local tem mudanças não pushadas. Considere `cd D:\src\meu-markdown.wiki && git push` antes ou depois deste release."

A skill **não modifica o wiki automaticamente** — apenas avisa. Wiki edits são deliberadas e caso-a-caso.

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

**Classificação do bump** (importante depois):
- Se mudou major → "MAJOR"
- Se mudou minor (e major igual) → "MINOR"
- Se mudou patch (e minor/major iguais) → "PATCH"

## 1. Preparar conteúdo da seção do CHANGELOG

`CHANGELOG.md` segue [Keep a Changelog](https://keepachangelog.com/). Tem sempre uma seção `## [Não lançado]` no topo e versões antigas abaixo.

### 1a. Ler a seção `[Não lançado]` atual

Extraia o conteúdo entre `## [Não lançado]` e a próxima `## [` (ou EOF). Esse é o **rascunho** que o usuário foi construindo conforme commitava.

### 1b. Se a seção [Não lançado] está vazia ou só com placeholders

Sugira conteúdo a partir dos commits desde o último tag:

```bash
git log $(git describe --tags --abbrev=0)..HEAD --pretty=format:"%s" --no-merges
```

**Agrupe** os subjects em categorias Keep a Changelog (não Conventional Commits — Keep a Changelog usa nomes diferentes):

| Prefixo Conventional | Categoria Keep a Changelog |
|---|---|
| `feat`, `feat(...)` | **Adicionado** |
| `fix`, `fix(...)` | **Corrigido** |
| `refactor`, `perf`, `style` | **Mudado** |
| `revert` | **Removido** |
| `chore`, `docs`, `build`, `ci`, `test` | omitir (interno, não user-facing) |
| Sem prefixo conhecido | **Mudado** (default) |

**Filtre ruído explícito:**
- `chore: bump version → ...` (commits de versão anteriores)
- `Merge ...`
- `docs(plano):...`, `docs(spec):...` (meta de processo interno)
- Commits em `.claude/`, `docs/superpowers/`, `.github/workflows/`, scripts internos — heurística: olhe `git show --stat` se houver dúvida

Formate cada item removendo o prefixo do Conventional Commit e capitalizando.

### 1c. Mostre ao usuário e peça confirmação

Apresente o conteúdo proposto formatado:

```markdown
### Adicionado
- ...

### Corrigido
- ...
```

Use AskUserQuestion: **Pode seguir** / **Editar** / **Cancelar**. Se "Editar", aceite o markdown corrigido em texto livre OU abra arquivo temp pro user editar.

## 2. Aplicar o CHANGELOG

Após confirmação:

1. **Renomeie** o heading `## [Não lançado]` para `## [X.Y.Z] — YYYY` (YYYY = ano corrente)
2. **Insira** nova seção vazia no topo:
   ```markdown
   ## [Não lançado]
   ```
   (em branco — o usuário começa a popular novamente após o release)
3. **Substitua** o corpo de `[X.Y.Z]` pelo conteúdo confirmado (se foi auto-gerado ou editado)
4. **Atualize os links no rodapé do arquivo**. Padrão atual:
   ```markdown
   [Não lançado]: https://github.com/<owner>/<repo>/compare/v<PREV>...HEAD
   [<PREV>]: https://github.com/<owner>/<repo>/releases/tag/v<PREV>
   ```
   Após release de X.Y.Z, deve virar:
   ```markdown
   [Não lançado]: https://github.com/<owner>/<repo>/compare/vX.Y.Z...HEAD
   [X.Y.Z]: https://github.com/<owner>/<repo>/releases/tag/vX.Y.Z
   [<PREV>]: https://github.com/<owner>/<repo>/releases/tag/v<PREV>
   ```

## 3. Atualizar README.md (apenas se MINOR ou MAJOR)

Para PATCH (`1.2.1`, `1.2.2`...), pule este passo — patches não mudam o roadmap.

Para MINOR/MAJOR:

1. Localize a seção `### ✅ Já entregue (vX.Y.Z)` no Roadmap
2. Atualize o label para a nova versão: `### ✅ Já entregue (vX.Y.Z)` → `### ✅ Já entregue (vNEW.Y.Z)`
3. Sugira ao usuário **acrescentar bullets** com features novas baseadas nos `### Adicionado` do CHANGELOG (resumindo — README é mais conciso que CHANGELOG)
4. Use AskUserQuestion pra confirmar a versão proposta — usuário pode ajustar livre.

## 4. Bump da versão

Chame o script existente sem deixar ele commitar:

```powershell
.\scripts\bump-version.ps1 X.Y.Z -NoCommit
```

Isso só edita `Directory.Build.props`. Os arquivos já editados (CHANGELOG, README) ficam staged junto.

## 5. Commit único + tag + push

Tudo num commit só pra que o tag aponte para o estado completo do release:

```bash
rtk git add Directory.Build.props CHANGELOG.md
# README.md também, se editado
rtk git status -s          # confirmar que SÓ esses arquivos estão staged
rtk git commit -m "chore: release vX.Y.Z"
rtk git tag vX.Y.Z
rtk git push origin main vX.Y.Z
```

A última linha (push da tag) **dispara o workflow `release.yml`** automaticamente.

## 6. Acompanhar o workflow

Após o push, o workflow começa. Mostre o link e ofereça acompanhar:

```
✅ Tag vX.Y.Z pushada.

Workflow rodando — vai levar ~3-5 min.
  Status: https://github.com/<owner>/<repo>/actions

Opções:
  - Acompanhar aqui:  gh run watch (interativo, segura até terminar)
  - Voltar depois:    gh release view vX.Y.Z
```

Use AskUserQuestion: **Acompanhar agora (`gh run watch`)** / **Continuar e checar depois**.

Se acompanhar: rode `gh run watch --exit-status` (PowerShell se gh não estiver no PATH). Mostre o resultado.

## 7. Resumo final

Se sucesso:

```
✅ Release v1.3.0 publicado

  Tag:        v1.3.0
  Release:    https://github.com/<owner>/<repo>/releases/tag/v1.3.0
  Assets:     MeuMarkdown-Setup-v1.3.0.exe + MeuMarkdown.exe (portable)
  Notes:      extraídos do CHANGELOG.md seção [1.3.0]

  Lembretes pós-release:
    - "Verificar atualizações" no app vai detectar a v1.3.0
    - CHANGELOG agora tem nova [Não lançado] vazia pra próxima ronda
    - Wiki (se aplicável): D:\src\meu-markdown.wiki
```

Se o workflow falhar, mostre o erro do log (`gh run view <id> --log-failed | head -50`) e oriente sobre rollback.

## Falhas e rollback

| Onde falhou | O que reverter |
|---|---|
| Edição de CHANGELOG/README/props (antes do commit) | `git checkout -- <arquivos>` |
| Após commit, antes do tag | `git reset --hard HEAD~1` |
| Após tag local, antes do push | `git tag -d vX.Y.Z && git reset --hard HEAD~1` |
| Push do main OK mas push da tag falhou | `git push origin vX.Y.Z` retry. Se persistir: `git tag -d vX.Y.Z`, fix, refazer |
| Workflow falhou no GitHub Actions | Investigar log. Pode-se: corrigir + repush tag (`git tag -d vX.Y.Z && git push --delete origin vX.Y.Z` + retry) OU corrigir + bumpar patch (X.Y.Z+1). Pergunte ao usuário. |
| Release criada mas com erro nos assets | `gh release edit vX.Y.Z` ou `gh release delete vX.Y.Z --cleanup-tag` + retry |

Em qualquer falha, **diga claramente** o que aconteceu e o que reverteu.

## Notas de implementação

- **Use `rtk` prefix** com `git`/`dotnet`/`gh` quando rodando via Bash (padrão global do usuário — vide CLAUDE.md).

- **PowerShell pra ISCC/gh quando precisa passar `/D` ou caminhos com espaço.** Git Bash interpreta `/D` como path POSIX e quebra.

- **Não bumpe se há mudanças não relacionadas no working tree.** Pre-flight bloqueia isso, mas se algo passou (ex: rebuild gera arquivos no `obj/`), só comite explicitamente os 3 arquivos esperados (`Directory.Build.props`, `CHANGELOG.md`, `README.md`) — nunca `git add -A`.

- **Não toque em outros arquivos de versão** (SplashWindow, AboutWindow, csproj). `VersionInfo.cs` lê do assembly em runtime; csproj herda de `Directory.Build.props`. Se descobrir referência hardcoded em outro lugar, **sinalize ao usuário** ao invés de "consertar" silenciosamente — pode ser intencional.

- **Wiki é separada.** O repositório `meu-markdown.wiki` em `D:\src\meu-markdown.wiki\` é um clone do `<repo>.wiki.git`. A skill **não modifica** o wiki — apenas avisa o usuário caso (a) tenha mudanças não pushadas, ou (b) seja MAJOR release que provavelmente exige revisão manual.

- **GitHub Actions é o source of truth do build de release.** Se o usuário quiser fazer um build local de teste, ele usa `installer\build-installer.bat` separadamente — não é responsabilidade da skill.
