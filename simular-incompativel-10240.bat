@echo off
taskkill /F /IM DeskQuadra* 2>nul
dotnet run --project "%~dp0src\DeskQuadra.UI.Wpf\DeskQuadra.UI.Wpf.csproj" -- --simulate-build 10240 --open-settings
