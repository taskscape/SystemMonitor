# SystemMonitorMobile

Solution: SystemMonitor

## Intended use

Cross-platform MAUI client that lets users select a machine, see current CPU/RAM/HDD utilization, and browse 7-day history from SystemCollectorService.

## Integration requirements

- Requires SystemCollectorService reachable via HTTP.
- Uses endpoints:
  - GET /api/v1/machines
  - GET /api/v1/machines/{machineName}/current
  - GET /api/v1/machines/{machineName}/history?days=7

## Main software patterns

- MVVM-style binding (MainViewModel + MainPage XAML).
- Typed API client (CollectorApiClient) with HttpClient.
- Dependency Injection for services and view models.
- Value converter for byte formatting.

## Configuration

File: Resources/Raw/appsettings.json

- CollectorSettings:BaseUrl (string) base URL of the collector.

Notes:
- Android emulator: use http://10.0.2.2:5100
- iOS simulator: use host IP address

## Logging

Debug logging enabled in DEBUG builds via Microsoft.Extensions.Logging.

## Deployment

Build:

```
dotnet build SystemMonitorMobile/SystemMonitorMobile.csproj
```

Run:

- Use Visual Studio or `dotnet build` + platform-specific tooling (Android/iOS/Windows) to deploy.
- Ensure the collector base URL is reachable from the target device.
