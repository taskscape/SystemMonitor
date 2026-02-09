# SystemMonitorService

Solution: SystemMonitor

## Intended use

Windows service that samples CPU, RAM, disk, and per-process usage, stores a short local history (SQLite), and pushes batched metrics to the collector.

## Integration requirements

- **Collector Endpoint:** Must point to the `SystemCollectorService` (default port `5101` with HTTPS).
- **Permissions:** Runs best under `LocalSystem` to access full process metrics.

## Configuration

File: `appsettings.json`

- `MonitorSettings:CollectorEndpoint`: REST endpoint (e.g., `https://your-server:5101/api/v1/metrics`).
- `MonitorSettings:DatabasePath`: Path to local SQLite DB.
- `MonitorSettings:RetentionDays`: Local retention window (default 7 days).

## Deployment

Install as Windows service (elevated prompt):

```powershell
sc.exe create SystemMonitorService binPath= "C:\Path\To\SystemMonitorService.exe" start= auto obj= LocalSystem
sc.exe start SystemMonitorService
```