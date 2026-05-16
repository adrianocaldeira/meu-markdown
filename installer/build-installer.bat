@echo off
echo ============================================
echo  MSATEC - Meu Markdown - Build Instalador
echo ============================================
echo.

:: Build release
echo [1/3] Publicando aplicacao...
cd /d "%~dp0.."
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
    echo   src\MeuMarkdown\bin\Release\net9.0-windows\win-x64\publish\MeuMarkdown.exe
    pause
    exit /b 0
)

%INNO% installer\MeuMarkdown.iss
if errorlevel 1 (
    echo ERRO: Falha ao gerar instalador!
    pause
    exit /b 1
)

echo.
echo [3/3] Concluido!
echo Instalador gerado em: installer\dist\
dir installer\dist\*.exe
echo.
pause
