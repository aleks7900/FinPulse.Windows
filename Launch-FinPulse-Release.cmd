@echo off
title FinPulse Companion (Release) - Windows 11
echo ========================================================
echo Launching FinPulse Companion [Release Mode]
echo ========================================================
echo.
cd /d "%~dp0"
dotnet run --project "FinPulse.Windows.csproj" -c Release --no-build
pause
