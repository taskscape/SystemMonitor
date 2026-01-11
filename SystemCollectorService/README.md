# SystemCollectorService

Solution: SystemMonitor

## Intended use

Windows service that receives metrics from SystemMonitorService, stores them in PostgreSQL with 1-minute averaged cache, and serves a web UI plus API for browsing machine history.

## Integration requirements

- SystemMonitorService posts to POST /api/v1/metrics.
- PostgreSQL is required; the service creates the database and schema if missing.
- Mobile and web clients call the REST endpoints exposed by this service.

## Main software patterns

- Minimal API (ASP.NET Core) for REST endpoints and static file hosting.
- Background startup task (IHostedService) for database initialization.
- Repository pattern for persistence and queries.
- Options pattern (CollectorSettings) for configuration binding.

## Configuration

File: appsettings.json

- CollectorSettings:ConnectionString (string) PostgreSQL connection string.
- CollectorSettings:ListenUrl (string) HTTP listen URL (default http://0.0.0.0:5100).

## Logging

Uses Microsoft.Extensions.Logging with appsettings.json levels. Logs database creation events and ingestion errors.

## Deployment

Build:

```
dotnet build SystemCollectorService/SystemCollectorService.csproj
```

Install as Windows service (elevated prompt):

```
sc.exe create SystemCollectorService binPath= "C:\Projects\SystemMonitor\SystemCollectorService\bin\Release\net10.0-windows\SystemCollectorService.exe" start= auto obj= LocalSystem
sc.exe start SystemCollectorService
```

Open UI:

- http://<collector-host>:5100/
