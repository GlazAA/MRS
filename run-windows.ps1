# Запуск MAUI на Windows (не путать с dotnet build — сборка без окна).
Set-Location $PSScriptRoot
dotnet run --project "src\MRS.Maui\MRS.Maui.csproj" -f net9.0-windows10.0.19041.0 @args
