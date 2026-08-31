@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo   Discord Proxy Launcher - Build NET48
echo ========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERRO: .NET SDK nao encontrado.
    echo Instale o .NET SDK ou abra o projeto no Visual Studio 2022.
    echo.
    pause
    exit /b 1
)

if exist dist rmdir /s /q dist
mkdir dist

echo Restaurando dependencias de compilacao...
dotnet restore "DiscordProxyLauncher.csproj"
if errorlevel 1 goto :error

echo.
echo Compilando Release...
dotnet build "DiscordProxyLauncher.csproj" -c Release --no-restore
if errorlevel 1 goto :error

copy /y "bin\Release\net48\DiscordProxyLauncher.exe" "dist\DiscordProxyLauncher.exe" >nul

if not exist "dist\DiscordProxyLauncher.exe" goto :error

echo.
echo ========================================
echo PRONTO!
echo Arquivo para enviar:
echo %CD%\dist\DiscordProxyLauncher.exe
for %%A in ("dist\DiscordProxyLauncher.exe") do echo Tamanho: %%~zA bytes
 echo ========================================
echo.
pause
exit /b 0

:error
echo.
echo Falha na compilacao.
echo Se necessario, abra DiscordProxyLauncher.csproj no Visual Studio 2022 e use Build ^> Build Solution.
echo.
pause
exit /b 1
