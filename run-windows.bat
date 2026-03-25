@echo off
cd /d "%~dp0"
echo Запуск MRS (Windows). Сборка только dotnet build окно не открывает.
dotnet run --project "src\MRS.Maui\MRS.Maui.csproj" -f net9.0-windows10.0.19041.0
if errorlevel 1 pause
