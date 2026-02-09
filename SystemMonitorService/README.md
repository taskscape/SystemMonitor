# SystemMonitorService (Agent)

Solution: SystemMonitor

## Intended use

Windows service that samples CPU, RAM, disk, and per-process usage. It stores samples in a local SQLite buffer and pushes them to the `SystemCollectorService` periodically.

## Features

- **Resilience:** Local SQLite storage ensures no data is lost during network outages.
- **Remote Restart:** Capable of executing system restarts received from the central server.

## Installation & Setup

### Windows
Run as an administrator to access full system metrics:
```powershell
sc.exe create SystemMonitorService binPath= "C:\Path\To\SystemMonitorService.exe" start= auto obj= LocalSystem
sc.exe start SystemMonitorService
```

## Configuration

File: `appsettings.json`

- `MonitorSettings:CollectorEndpoint`: URL of the collector (e.g., `https://your-server:5101/api/v1/metrics`).
- `MonitorSettings:PushIntervalSeconds`: Delay between data uploads (default `30`).
- `MonitorSettings:TrustAllCertificates`: Set to `true` if using self-signed HTTPS certificates.