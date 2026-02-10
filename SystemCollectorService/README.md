# SystemCollectorService

Solution: SystemMonitor

## Intended use

Central server that receives metrics from SystemMonitorService agents, queues them via **RabbitMQ**, and processes them into **PostgreSQL**. It also serves a web UI and provides an API for the mobile application.

## Key Features

- **Scalable Ingestion:** Producers push metrics to a queue, allowing the consumer to process data at an optimal pace.
- **Data Retention:** Automatically prunes data older than a configured number of days using the `DatabaseCleanupService`.
- **Remote Control:** Provides endpoints to queue system commands (like `restart`) for agents to pick up.

## Integration requirements

- **RabbitMQ:** Required for asynchronous metrics processing.
- **PostgreSQL:** Required for persistent storage.
- Use `docker-compose up -d` in the root directory to start both.

## Configuration

File: `appsettings.json`

- `CollectorSettings:ConnectionString`: PostgreSQL connection string.
- `CollectorSettings:ListenUrl`: HTTPS listen URL (default `https://0.0.0.0:5101`).
- `CollectorSettings:RetentionDays`: Number of days to keep historical data (default `7`).

## Deployment

### Windows
Install as a Windows service (elevated prompt):
```powershell
sc.exe create SystemCollectorService binPath= "C:\Path\To\SystemCollectorService.exe" start= auto obj= LocalSystem
sc.exe start SystemCollectorService
```

### Web Dashboard
Open: `https://<collector-host>:5101/`
The dashboard shows real-time status and allows sending restart commands to active machines.
