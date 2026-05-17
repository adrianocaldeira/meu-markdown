@echo off
echo ============================================
echo  Meu Markdown - Build Instalador
echo ============================================
echo.

cd /d "%~dp0.."

:: [0/3] Extract version from Directory.Build.props (single source of truth)
set VERSION=
for /f "tokens=2 delims=<>" %%V in ('findstr "<Version>" Directory.Build.props') do (
    if not defined VERSION set VERSION=%%V
)
if not defined VERSION (
    echo ERRO: Nao consegui ler ^<Version^> de Directory.Build.props
    pause
    exit /b 1
)
echo Versao alvo: %VERSION%
echo.

:: Build release
echo [1/3] Publicando aplicacao...
dotnet publish src\MeuMarkdown\MeuMarkdown.csproj -c Release
if errorlevel 1 (
    echo ERRO: Falha no publish!
    pause
    exit /b 1
)
echo OK.

:: Create dist folder
if not exist installer\dist mkdir installer\dist

:: Run Inno Setup
echo.
echo [2/3] Compilando instalador...
set INNO="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO% set INNO="C:\Program Files\Inno Setup 6\ISCC.exe"
if not exist %INNO% (
    echo AVISO: Inno Setup nao encontrado em %INNO%
    echo Baixe em: https://jrsoftware.org/isinfo.php
    echo.
    echo O .exe publicado esta em:
    echo   src\MeuMarkdown\bin\Release\net10.0-windows\win-x64\publish\MeuMarkdown.exe
    pause
    exit /b 0
)

%INNO% /DAppVersion=%VERSION% installer\MeuMarkdown.iss
if errorlevel 1 (
    echo ERRO: Falha ao gerar instalador!
    pause
    exit /b 1
)

echo.
echo [3/3] Concluido!
echo Instalador gerado em: installer\dist\MeuMarkdown-Setup-v%VERSION%.exe
echo.
echo Proximos passos para publicar release:
echo   git tag v%VERSION%
echo   git push origin v%VERSION%
echo   gh release create v%VERSION% installer\dist\MeuMarkdown-Setup-v%VERSION%.exe
echo.
pause
