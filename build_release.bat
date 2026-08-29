@echo off
echo =========================================
echo Compilando SyncLib em modo Release...
echo =========================================

dotnet build SyncLib.App\SyncLib.App.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] A compilacao em Release falhou!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCESSO] Compilacao em Release concluida com sucesso!
pause
