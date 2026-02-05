# SystemCollectorService

Solution: SystemMonitor

## Intended use

Windows service that receives metrics from SystemMonitorService, queues them via **RabbitMQ**, and processes them into **PostgreSQL**. It also serves a web UI and API.

## Integration requirements

- **RabbitMQ:** Required for asynchronous metrics processing.
- **PostgreSQL:** Required for persistent storage.
- Use `docker-compose up -d` in the root directory to start both.

## Main software patterns

- **RabbitMQ Integration:** Uses a Producer (REST API) and Consumer (Background Service) pattern for scalable ingestion.
- **Minimal API:** For REST endpoints and static file hosting.
- **Background Services:** One for database initialization and another for consuming RabbitMQ messages.

## Configuration

File: `appsettings.json`

- `CollectorSettings:ConnectionString`: PostgreSQL connection string.
- `CollectorSettings:ListenUrl`: HTTPS listen URL (default `https://0.0.0.0:5101`).
- `CollectorSettings:RabbitMqHostName`: RabbitMQ host (default `localhost`).

## Deployment

Install as Windows service (elevated prompt):

```powershell
sc.exe create SystemCollectorService binPath= "C:\Path\To\SystemCollectorService.exe" start= auto obj= LocalSystem
sc.exe start SystemCollectorService
```

Open UI: `https://<collector-host>:5101/`