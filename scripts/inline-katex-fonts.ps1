# Inline KaTeX fonts as data: URLs in katex.min.css.
# Reads the original katex.min.css and fonts/ from a source directory,
# emits a single CSS file with fonts embedded.
#
# Usage:
#   .\scripts\inline-katex-fonts.ps1 -SourceDir <katex-folder> -Out <output.css>

param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$Out
)

$cssPath = Join-Path $SourceDir 'katex.min.css'
$fontsDir = Join-Path $SourceDir 'fonts'

if (-not (Test-Path $cssPath)) { throw "katex.min.css not found at $cssPath" }
if (-not (Test-Path $fontsDir)) { throw "fonts/ not found at $fontsDir" }

$css = Get-Content $cssPath -Raw

# Inline url(fonts/*.woff2) as data: URL.
$pattern = 'url\(fonts/([^)]+\.woff2)\)'
$css = [regex]::Replace($css, $pattern, {
    param($m)
    $fontFile = Join-Path $fontsDir $m.Groups[1].Value
    if (-not (Test-Path $fontFile)) { return $m.Value }
    $bytes = [IO.File]::ReadAllBytes($fontFile)
    $b64 = [Convert]::ToBase64String($bytes)
    "url(data:font/woff2;base64,$b64)"
})

# Drop url(fonts/*.woff) and url(fonts/*.ttf) (older formats — WOFF2 suffices).
$css = [regex]::Replace($css, 'url\(fonts/[^)]+\.(woff|ttf)\)\s*format\([^)]+\),?\s*', '')

Set-Content -Path $Out -Value $css -Encoding UTF8
Write-Host "Generated $Out ($($css.Length) bytes)"
