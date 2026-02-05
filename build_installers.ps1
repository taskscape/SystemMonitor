# Build script for System Monitor Installers

$ErrorActionPreference = "Stop"

Write-Host "--- 1. Cleaning old publish folders ---" -ForegroundColor Cyan
if (Test-Path "publish_collector") { Remove-Item -Recurse -Force "publish_collector" }
if (Test-Path "publish_monitor") { Remove-Item -Recurse -Force "publish_monitor" }

Write-Host "--- 2. Publishing SystemCollectorService (Server) ---" -ForegroundColor Cyan
dotnet publish SystemCollectorService/SystemCollectorService.csproj -c Release -o publish_collector /p:PublishSingleFile=false /p:SelfContained=false

Write-Host "--- 3. Publishing SystemMonitorService (Client) ---" -ForegroundColor Cyan
dotnet publish SystemMonitorService/SystemMonitorService.csproj -c Release -o publish_monitor /p:PublishSingleFile=false /p:SelfContained=false

Write-Host "--- 4. Locating Inno Setup Compiler (ISCC.exe) ---" -ForegroundColor Cyan
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    # Try to find in PATH if not in default location
    $iscc = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
}

if (-not $iscc) {
    Write-Error "Inno Setup Compiler (ISCC.exe) not found! Please install it from https://jrsoftware.org/isdl.php"
    exit
}

Write-Host "Using ISCC from: $iscc" -ForegroundColor Gray

Write-Host "--- 5. Compiling Combined Installer ---" -ForegroundColor Green
& $iscc "CombinedInstaller.iss"

Write-Host "--- 6. Compiling Server Installer ---" -ForegroundColor Green
& $iscc "ServerInstaller.iss"

Write-Host "--- 7. Compiling Client Installer ---" -ForegroundColor Green
& $iscc "ClientInstaller.iss"

Write-Host "`nSuccessfully generated installers:" -ForegroundColor Cyan
Get-ChildItem *.exe | Where-Object { $_.Name -like "SystemMonitor*Setup.exe" } | Select-Object Name, @{Name="Size(MB)";Expression={"{0:N2}" -f ($_.Length / 1MB)}}

Write-Host "`nDone!" -ForegroundColor Cyan
