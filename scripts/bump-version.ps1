<#
.SYNOPSIS
    Atualiza a versão do Meu Markdown em todos os arquivos onde ela é declarada.

.DESCRIPTION
    Mantém .csproj e installer/.iss em sincronia. Sem este script, é fácil
    publicar uma release com o instalador apontando uma versão antiga.

.PARAMETER Version
    Versão alvo no formato MAJOR.MINOR.PATCH (ex: 1.2.0).

.PARAMETER NoCommit
    Apenas edita os arquivos. Sem este flag, cria commit e tag locais.

.EXAMPLE
    .\scripts\bump-version.ps1 1.2.0
    .\scripts\bump-version.ps1 1.2.0 -NoCommit
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$NoCommit
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot\.."

$csproj = Join-Path $repoRoot 'src/MeuMarkdown/MeuMarkdown.csproj'
$iss    = Join-Path $repoRoot 'installer/MeuMarkdown.iss'

if (-not (Test-Path $csproj)) { throw "Não encontrei: $csproj" }
if (-not (Test-Path $iss))    { throw "Não encontrei: $iss" }

Write-Host "Atualizando para versão: $Version" -ForegroundColor Cyan

# .csproj: <Version>, <AssemblyVersion>, <FileVersion>
$csprojContent = Get-Content $csproj -Raw
$csprojContent = $csprojContent -replace '<Version>[^<]+</Version>',               "<Version>$Version</Version>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>[^<]+</FileVersion>',         "<FileVersion>$Version.0</FileVersion>"
Set-Content -Path $csproj -Value $csprojContent -NoNewline -Encoding utf8

# installer.iss: #define AppVersion "X.Y.Z"
$issContent = Get-Content $iss -Raw
$issContent = $issContent -replace '#define AppVersion "[^"]+"', "#define AppVersion `"$Version`""
Set-Content -Path $iss -Value $issContent -NoNewline -Encoding utf8

Write-Host "Arquivos atualizados:" -ForegroundColor Green
Write-Host "  - $csproj"
Write-Host "  - $iss"

if ($NoCommit) {
    Write-Host "`n-NoCommit: sem commit/tag. Revise e commite manualmente." -ForegroundColor Yellow
    exit 0
}

Write-Host "`nCriando commit e tag locais..." -ForegroundColor Cyan
git -C $repoRoot add src/MeuMarkdown/MeuMarkdown.csproj installer/MeuMarkdown.iss
git -C $repoRoot commit -m "chore: bump version → $Version"
git -C $repoRoot tag "v$Version"

Write-Host "`nPronto. Para publicar a release:" -ForegroundColor Green
Write-Host "  git push origin main"
Write-Host "  git push origin v$Version"
Write-Host "`nO workflow .github/workflows/release.yml roda automaticamente ao receber a tag."
