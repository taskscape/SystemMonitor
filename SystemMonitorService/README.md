# SystemMonitorService

Solution: SystemMonitor (Agent)

## Intended use

Windows service that samples CPU, RAM, disk, and per-process usage, stores a short local history (SQLite), and pushes batched metrics to the collector.

## Standalone Features

- **Local Buffering:** Stores metrics in a local SQLite database if the collector is unreachable.
- **Auto-Cleanup:** Automatically manages its local database size (default 7-day retention).
- **Zero Dependencies:** Distributed as a self-contained executable with bundled .NET runtime.

## Configuration

File: `appsettings.json`

- `MonitorSettings:CollectorEndpoint`: REST endpoint (e.g., `https://your-server:5101/api/v1/metrics`).
- `MonitorSettings:TrustAllCertificates`: Set to `true` for development/self-signed certificates.
- `MonitorSettings:RetentionDays`: Local retention window (default 7 days).

## Deployment

The recommended way to install is using the **SystemMonitorClientSetup.exe** installer.

Manual installation:
```powershell
sc.exe create SystemMonitorService binPath= "C:\Path\To\SystemMonitorService.exe" start= auto obj= LocalSystem
sc.exe start SystemMonitorService
```
