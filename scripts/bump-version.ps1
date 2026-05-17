<#
.SYNOPSIS
    Atualiza a versão do Meu Markdown.

.DESCRIPTION
    A versão é centralizada em Directory.Build.props na raiz do repo:
      - csproj herda Version/AssemblyVersion/FileVersion
      - build-installer.bat extrai e passa /DAppVersion=X.Y.Z ao ISCC
      - SplashWindow/AboutWindow leem em runtime via Assembly.GetName().Version
    Este script edita apenas Directory.Build.props — sem este atomizador é fácil
    publicar release com instalador apontando versão desatualizada.

.PARAMETER Version
    Versão alvo no formato MAJOR.MINOR.PATCH (ex: 1.2.0).

.PARAMETER NoCommit
    Apenas edita o arquivo. Sem este flag, cria commit e tag locais.

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
$propsPath = Join-Path $repoRoot 'Directory.Build.props'

if (-not (Test-Path $propsPath)) { throw "Não encontrei: $propsPath" }

Write-Host "Atualizando para versão: $Version" -ForegroundColor Cyan

$content = Get-Content $propsPath -Raw
$updated = $content -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"

if ($content -eq $updated) {
    Write-Host "Versão já estava em $Version — nada a fazer." -ForegroundColor Yellow
    exit 0
}

Set-Content -Path $propsPath -Value $updated -NoNewline -Encoding utf8
Write-Host "Atualizado: $propsPath" -ForegroundColor Green

if ($NoCommit) {
    Write-Host "`n-NoCommit: sem commit/tag. Revise e commite manualmente." -ForegroundColor Yellow
    exit 0
}

Write-Host "`nCriando commit e tag locais..." -ForegroundColor Cyan
git -C $repoRoot add Directory.Build.props
git -C $repoRoot commit -m "chore: bump version → $Version"
git -C $repoRoot tag "v$Version"

Write-Host "`nPronto. Para publicar a release:" -ForegroundColor Green
Write-Host "  git push origin main"
Write-Host "  git push origin v$Version"
Write-Host "`nO workflow .github/workflows/release.yml roda automaticamente ao receber a tag."
