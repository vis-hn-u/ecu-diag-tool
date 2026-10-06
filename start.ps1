# Automotive CAN / ECU Engineering Platform - One-Command Launcher

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Automotive CAN / ECU Engineering Platform" -ForegroundColor Cyan
Write-Host "  Phases 1-5: Diagnostics, Engine, Dyno & Flash Analyzer" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$rootDir = $PSScriptRoot
$distIndex = Join-Path $rootDir "frontend\dist\index.html"

if (-not (Test-Path $distIndex)) {
    Write-Host "[1/3] Building frontend bundle..." -ForegroundColor Yellow
    Push-Location (Join-Path $rootDir "frontend")
    npm install
    npm run build
    Pop-Location
} else {
    Write-Host "[1/3] Frontend bundle verified." -ForegroundColor Green
}

Write-Host "[2/3] Launching default browser at http://localhost:5000 ..." -ForegroundColor Green
Start-Process "http://localhost:5000"

Write-Host "[3/3] Starting ASP.NET Core Backend on http://localhost:5000 ..." -ForegroundColor Green
Write-Host "Platform is online! Press Ctrl+C in this console to stop.`n" -ForegroundColor Cyan

$dotnetExe = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet\dotnet.exe" } else { "dotnet" }
$projectPath = Join-Path $rootDir "backend\CanEngineeringPlatform.Api\CanEngineeringPlatform.Api.csproj"

& $dotnetExe run --project $projectPath --urls "http://localhost:5000"
