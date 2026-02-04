# Simple Build Script

if (Test-Path "publish_monitor") { Remove-Item "publish_monitor" -Recurse -Force }

echo "Building..."
dotnet publish SystemMonitorService\SystemMonitorService.csproj -c Release -r win-x64 --self-contained true -o publish_monitor /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    echo "Compiling Installer..."
    & $iscc "ClientInstaller.iss"
    echo "Done."
} else {
    echo "Inno Setup not found."
}
