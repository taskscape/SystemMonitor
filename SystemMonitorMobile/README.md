# SystemMonitorMobile

Solution: SystemMonitor (App)

## Intended use

Cross-platform MAUI client to browse machine history and current utilization from `SystemCollectorService`.

## Configuration

File: `Resources/Raw/appsettings.json`

- `CollectorSettings:BaseUrl`: Base URL of the collector (e.g., `https://your-server:5101`).

### Platform Notes
- **Android Emulator:** Use `https://10.0.2.2:5101` if running the collector on the host machine.
- **Certificate Handling:** Ensure the device trusts the server's certificate or set up appropriate bypasses in `MauiProgram.cs` for development.

## Deployment

Build and run using Visual Studio:
1. Open `SystemMonitor.sln`.
2. Set `SystemMonitorMobile` as the startup project.
3. Select your target device (Android, iOS, or Windows).
