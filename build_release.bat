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
echo [SUCESSO] Publicacao em Release concluida com sucesso na pasta Executavel!
pause
