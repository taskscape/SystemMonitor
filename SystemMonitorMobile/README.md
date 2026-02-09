# SystemMonitorMobile

Solution: SystemMonitor

## Intended use

Cross-platform MAUI client to browse machine history and current utilization from `SystemCollectorService`.

## Configuration

File: `Resources/Raw/appsettings.json`

- `CollectorSettings:BaseUrl`: Base URL of the collector (e.g., `https://192.168.1.100:5101`).

### Platform Notes
- **Android Emulator:** Use `https://10.0.2.2:5101` (if running collector on host).
- **Certificate Handling:** Since we use HTTPS with a development certificate, ensure the device trusts the certificate or bypass validation in `MauiProgram.cs` for development.

## Deployment

Build and run using Visual Studio or:

```bash
dotnet build SystemMonitorMobile/SystemMonitorMobile.csproj
```