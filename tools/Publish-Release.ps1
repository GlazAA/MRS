# Build MRS release packages (local SQLite, no PostgreSQL required).
# Run from repo root: .\tools\Publish-Release.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$maui = Join-Path $root "src\MRS.Maui"
$dist = Join-Path $root "dist"
$winOut = Join-Path $dist "windows-win10-x64"
$androidOut = Join-Path $dist "android"

Write-Host "=== MRS Release publish ===" -ForegroundColor Cyan

Write-Host "`n[1/2] Windows x64..." -ForegroundColor Yellow
if (Test-Path $winOut) { Remove-Item $winOut -Recurse -Force }
New-Item -ItemType Directory -Path $winOut -Force | Out-Null

Push-Location $maui
dotnet publish -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifier=win10-x64 -o $winOut
Pop-Location

$zipWin = Join-Path $dist "MRS-Windows-win10-x64.zip"
if (Test-Path $zipWin) { Remove-Item $zipWin -Force }
Compress-Archive -Path (Join-Path $winOut "*") -DestinationPath $zipWin -Force
Write-Host "  OK: $zipWin" -ForegroundColor Green

$androidJar = @(
    (Join-Path $env:USERPROFILE "AppData\Local\Android\Sdk\platforms\android-36\android.jar"),
    (Join-Path $env:USERPROFILE "AppData\Local\Android\Sdk\platforms\android-35\android.jar"),
    (Join-Path $env:USERPROFILE "AppData\Local\Android\Sdk\platforms\android-34\android.jar")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
Write-Host "`n[2/2] Android APK..." -ForegroundColor Yellow

if (-not $androidJar) {
    Write-Host "  Skipped: Android SDK Platform 35 not installed." -ForegroundColor DarkYellow
    Write-Host "  Run: dotnet build -t:InstallAndroidDependencies -f net9.0-android -p:AcceptAndroidSDKLicenses=true" -ForegroundColor Gray
    Write-Host "  Install via Android Studio SDK Manager, then re-run." -ForegroundColor Gray
    Write-Host "`nWindows package is ready in dist\" -ForegroundColor Cyan
    exit 0
}

if (Test-Path $androidOut) { Remove-Item $androidOut -Recurse -Force }
New-Item -ItemType Directory -Path $androidOut -Force | Out-Null

Push-Location $maui
dotnet publish -f net9.0-android -c Release -p:AndroidPackageFormat=apk -o $androidOut
Pop-Location

$apk = Get-ChildItem -Path $androidOut -Filter "*.apk" -Recurse | Select-Object -First 1
if ($apk) {
    $apkDest = Join-Path $dist "MRS-Android.apk"
    Copy-Item $apk.FullName $apkDest -Force
    Write-Host "  OK: $apkDest" -ForegroundColor Green
} else {
    Write-Host "  APK not found in $androidOut" -ForegroundColor Red
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan
