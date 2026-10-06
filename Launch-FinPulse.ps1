# FinPulse Windows 11 Launcher Script
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Launching FinPulse Companion (Windows 11 WinUI 3 Native)" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

dotnet run --project "FinPulse.Windows.csproj" -c Debug
