# SystemMonitorService

Solution: SystemMonitor

## Intended use

Windows service that samples CPU, RAM, disk, and per-process usage on a host machine, stores a short local history, and pushes batched metrics to the collector.

## Integration requirements

- SystemCollectorService must be reachable at the configured endpoint.
- Uses the collector contract: POST /api/v1/metrics with a JSON array of samples.
- Runs best under an elevated account (e.g., LocalSystem) to access full process metrics.

## Main software patterns

- Worker Service (BackgroundService) for scheduled loops (collect, push, cleanup).
- Repository-like storage class (SqliteStorage) to persist and query samples.
- Dependency Injection for collectors, storage, and HTTP client.
- Options pattern (MonitorSettings) for configuration binding.

## Configuration

File: appsettings.json

- MonitorSettings:CollectorEndpoint (string) REST endpoint for the collector.
- MonitorSettings:DatabasePath (string) path to SQLite DB. Empty uses ProgramData.
- MonitorSettings:PushBatchSize (int) number of samples per push batch.
- MonitorSettings:PushIntervalSeconds (int) push attempt interval.
- MonitorSettings:RetryDelaySeconds (int) minimum retry delay on failure.
- MonitorSettings:RetentionDays (int) local retention window.

## Logging

Uses Microsoft.Extensions.Logging with appsettings.json levels. Logs warnings for failed counters, push retries, and cleanup issues.

## Deployment

Build:

```
dotnet build SystemMonitorService/SystemMonitorService.csproj
```

Install as Windows service (elevated prompt):

```
sc.exe create SystemMonitorService binPath= "C:\Projects\SystemMonitor\SystemMonitorService\bin\Release\net10.0-windows\SystemMonitorService.exe" start= auto obj= LocalSystem
sc.exe start SystemMonitorService
```

Adjust the binPath to the actual release output path.
