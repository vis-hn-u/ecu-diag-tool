@echo off
setlocal enabledelayedexpansion
title Automotive CAN / ECU Engineering Platform
echo ========================================================
echo   Automotive CAN / ECU Engineering Platform
echo   Phases 1-5: Diagnostics, Engine, Dyno and Flash Analyzer
echo ========================================================
echo.

:: Ensure user .NET SDK path is prioritized if installed
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "PATH=%USERPROFILE%\.dotnet;!PATH!"
)

:: 1. Check if frontend is built, if not, build it
if not exist "%~dp0frontend\dist\index.html" (
    echo [1/3] Building frontend UI...
    cd /d "%~dp0frontend"
    call npm install
    call npm run build
    cd /d "%~dp0"
) else (
    echo [1/3] Frontend bundle verified (frontend\dist)
)

:: 2. Launch browser in the background
start "" http://localhost:5000

:: 3. Run the Backend Platform Server
echo [2/3] Starting ASP.NET Core Backend on http://localhost:5000 ...
echo [3/3] Platform is online! Access via http://localhost:5000
echo       Press Ctrl+C in this window to stop.
echo.

dotnet run --project "%~dp0backend\CanEngineeringPlatform.Api\CanEngineeringPlatform.Api.csproj" --urls "http://localhost:5000"
pause
