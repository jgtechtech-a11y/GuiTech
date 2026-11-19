@echo off
rem Gera o Gui_Tech pronto para levar a outro PC (nao precisa instalar o .NET la).
rem
rem Saida em ..\GuiTech-dist:
rem   GuiTech-Setup-<versao>.exe  instalador (RECOMENDADO para distribuir: um arquivo so)
rem   GuiTech\                    versao em pasta (nao extrai nada em %%TEMP%%)
rem   GuiTech.zip                 a pasta acima compactada
rem   GuiTech.exe                 versao em arquivo unico (autoextraivel; so para uso local)
rem   LEIA-ME.txt
rem
rem Requer o Inno Setup 6 instalado para gerar o instalador (passo 3 e pulado se nao achar).
rem Sempre recompila a partir do codigo atual desta pasta.
setlocal enabledelayedexpansion
cd /d "%~dp0"

rem Acha o dotnet que TEM SDK: o de Program Files pode ter so o runtime (erro confuso de
rem "No .NET SDKs were found" com codigo de saida negativo). A instalacao por usuario tem prioridade.
set "DOTNET_CMD=dotnet"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "DOTNET_CMD=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
    set "DOTNET_MULTILEVEL_LOOKUP=0"
)
"%DOTNET_CMD%" --list-sdks 2>nul | findstr /r "." >nul
if errorlevel 1 (
    echo ERRO: nenhum SDK do .NET foi encontrado - sem ele o programa nao compila.
    echo O dotnet de "%DOTNET_CMD%" so tem o runtime. Instale o SDK com:
    echo     winget install Microsoft.DotNet.SDK.10
    pause
    exit /b 1
)

echo Fechando o GuiTech se estiver aberto ...
taskkill /f /im GuiTech.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo.
echo [1/4] Gerando a versao em pasta ...
rmdir /s /q obj\Release >nul 2>&1
"%DOTNET_CMD%" publish GuiTech.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -o "..\GuiTech-dist\GuiTech" -nologo -v q
if !errorlevel! neq 0 goto falhou

copy /y "LEIA-ME.txt" "..\GuiTech-dist\GuiTech\LEIA-ME.txt" >nul
copy /y "LICENSE" "..\GuiTech-dist\GuiTech\LICENSE" >nul

echo [2/4] Compactando GuiTech.zip ...
if exist "..\GuiTech-dist\GuiTech.zip" del /q "..\GuiTech-dist\GuiTech.zip"
powershell -NoProfile -Command "Compress-Archive -Path '..\GuiTech-dist\GuiTech' -DestinationPath '..\GuiTech-dist\GuiTech.zip' -CompressionLevel Optimal"
if errorlevel 1 goto falhou

echo [3/4] Gerando o instalador ...
set "ISCC="
for %%P in (
    "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
    "%ProgramFiles%\Inno Setup 6\ISCC.exe"
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
) do if exist "%%~P" set "ISCC=%%~P"

if not defined ISCC (
    echo    Inno Setup 6 nao encontrado - instalador NAO gerado.
    echo    Instale com: winget install JRSoftware.InnoSetup
) else (
    "%ISCC%" /Qp instalador.iss
    if errorlevel 1 goto falhou
    for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "Get-ChildItem '..\GuiTech-dist\GuiTech\GuiTech.exe' | Select-Object -ExpandProperty VersionInfo | Select-Object -ExpandProperty ProductVersion"`) do set "VER=%%V"
)

echo [4/4] Gerando a versao em arquivo unico ...
"%DOTNET_CMD%" publish GuiTech.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "..\GuiTech-dist" -nologo -v q
if !errorlevel! neq 0 goto falhou

copy /y "LEIA-ME.txt" "..\GuiTech-dist\LEIA-ME.txt" >nul

echo.
echo Pronto: %~dp0..\GuiTech-dist
for %%F in ("..\GuiTech-dist\GuiTech.exe") do echo   GuiTech.exe    %%~tF  %%~zF bytes
for %%F in ("..\GuiTech-dist\GuiTech.zip") do echo   GuiTech.zip    %%~tF  %%~zF bytes
if defined ISCC (
    for %%F in ("..\GuiTech-dist\GuiTech-Setup-!VER!.exe") do echo   Instalador     %%~tF  %%~zF bytes
)
echo.
echo Para outros PCs: envie o GuiTech-Setup (um arquivo so, ja cria atalhos e desinstalador).
echo SHA-256 para conferencia (tambem gravado em ..\GuiTech-dist\SHA256SUMS.txt):
powershell -NoProfile -Command "$items = Get-ChildItem '..\GuiTech-dist\*.exe','..\GuiTech-dist\*.zip' | Where-Object { $_.Name -notmatch 'GuiTech 2' }; $lines = $items | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Name }; $lines | Set-Content '..\GuiTech-dist\SHA256SUMS.txt' -Encoding ASCII; $lines | ForEach-Object { '  ' + $_ }"
pause
exit /b 0

:falhou
echo.
echo FALHOU. Veja os erros acima. O que estava pronto NAO foi atualizado.
pause
exit /b 1
