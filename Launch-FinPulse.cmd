@echo off
title FinPulse Companion - Windows 11
echo ========================================================
echo Launching FinPulse Companion (Windows 11 WinUI 3 Native)
echo ========================================================
echo.
cd /d "%~dp0"
dotnet run --project "FinPulse.Windows.csproj" -c Debug
