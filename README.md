# SystemMonitorService

Windows service that samples CPU, RAM, and fixed drive usage every second, captures per-process CPU/RAM, keeps one week of local history, and pushes batches to a REST collector.

## Build

```
dotnet build SystemMonitorService/SystemMonitorService.csproj
```

## Run as a Windows service

Example using `sc.exe` (run from an elevated prompt):

```
sc.exe create SystemMonitorService binPath= "C:\Projects\SystemMonitor\SystemMonitorService\bin\Release\net10.0-windows\SystemMonitorService.exe" start= auto obj= LocalSystem
sc.exe start SystemMonitorService
```

The service is expected to run under an elevated account (for example, `LocalSystem`) to access full process metrics.

## Configuration

Edit `SystemMonitorService/appsettings.json`:

- `MonitorSettings:CollectorEndpoint` - REST endpoint used for pushing metrics.
- `MonitorSettings:DatabasePath` - SQLite path. Leave empty to use `C:\ProgramData\SystemMonitorService\monitor.db`.
- `MonitorSettings:PushBatchSize` - batch size sent per request.
- `MonitorSettings:PushIntervalSeconds` - interval between push attempts.
- `MonitorSettings:RetryDelaySeconds` - minimum delay before retrying failed pushes.
- `MonitorSettings:RetentionDays` - days to keep local records.

## Collector contract (proposed)

Endpoint:

```
POST /api/v1/metrics
Content-Type: application/json
```

Request body is an array of samples:

```json
[
  {
    "machineName": "HOST01",
    "machine": {
      "timestampUtc": "2026-01-10T22:10:00Z",
      "cpuPercent": 23.4,
      "ramUsedBytes": 7340032000,
      "ramTotalBytes": 17179869184
    },
    "drives": [
      {
        "name": "C:\\",
        "totalBytes": 511705088000,
        "usedBytes": 348966092800
      }
    ],
    "processes": [
      {
        "processId": 1234,
        "processName": "chrome",
        "cpuPercent": 5.2,
        "ramBytes": 524288000
      }
    ]
  }
]
```

Success response: any `2xx` status. Non-`2xx` response or network failure causes the batch to be retried no sooner than one minute later.

# SystemCollectorService

Windows service that receives `SystemMonitorService` payloads, stores them in PostgreSQL, and hosts a web UI for fleet history.

## Build

```
dotnet build SystemCollectorService/SystemCollectorService.csproj
```

## Run as a Windows service

Example using `sc.exe` (run from an elevated prompt):

```
sc.exe create SystemCollectorService binPath= "C:\Projects\SystemMonitor\SystemCollectorService\bin\Release\net10.0-windows\SystemCollectorService.exe" start= auto obj= LocalSystem
sc.exe start SystemCollectorService
```

## Configuration

Edit `SystemCollectorService/appsettings.json`:

- `CollectorSettings:ConnectionString` - PostgreSQL connection string. The service creates the database and schema if missing.
- `CollectorSettings:ListenUrl` - HTTP listen address (default `http://0.0.0.0:5100`).

## API

- `POST /api/v1/metrics` accepts the JSON array produced by `SystemMonitorService`.
- `GET /api/v1/machines` lists registered computers.
- `GET /api/v1/machines/{machineName}/current` returns the latest sample.
- `GET /api/v1/machines/{machineName}/history?days=7` returns seven days of history.

## UI

Browse to `http://<collector-host>:5100/` and choose a machine to view current CPU/RAM/HDD usage and charts for the last 7 days.
