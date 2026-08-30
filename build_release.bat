@echo off
echo =========================================
echo Publicando SyncLib em modo Release Auto-contido na pasta Executavel...
echo =========================================

dotnet publish SyncLib.App\SyncLib.App.csproj -c Release -r win-x64 --self-contained true -o Executavel

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] A publicacao em Release falhou!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo =========================================
echo Gerando Instalador MSI com WiX Toolset v4...
echo =========================================

dotnet build SyncLib.Installer\SyncLib.Installer.wixproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] A geracao do instalador MSI falhou!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Copiando instalador para a pasta Compilado...
if not exist "Compilado" mkdir Compilado
copy /Y SyncLib.Installer\bin\x64\Release\pt-BR\SyncLibSetup.msi .\Compilado\SyncLibSetup.msi > nul

echo.
echo [SUCESSO] Publicacao e Instalador MSI gerados com sucesso na pasta Compilado!
pause
