# SystemMonitor

A distributed system monitoring solution consisting of a Windows Service (Collector), a Client Service (Monitor), and a Mobile App.

## Infrastructure (Server-side)

The system now uses **RabbitMQ** for metrics queuing and **PostgreSQL** for storage. The easiest way to start the infrastructure is using Docker:

```bash
docker-compose up -d
```

This will start:
- **PostgreSQL** on port `5432`
- **RabbitMQ** on ports `5672` (AMQP) and `15672` (Management UI)

## SystemCollectorService (Server)

Windows service that receives `SystemMonitorService` payloads via RabbitMQ or REST, stores them in PostgreSQL, and hosts a web UI for fleet history.

### Build
```bash
dotnet build SystemCollectorService/SystemCollectorService.csproj
```

### Configuration
Edit `SystemCollectorService/appsettings.json`:
- `CollectorSettings:ConnectionString` - PostgreSQL connection string.
- `CollectorSettings:ListenUrl` - HTTPS listen address (default `https://0.0.0.0:5101`).
- `CollectorSettings:RabbitMqHostName` - Hostname for RabbitMQ (default `localhost`).

### UI & API
- **Web UI:** `https://<collector-host>:5101/`
- `POST /api/v1/metrics` - Ingests metrics (now pushes to RabbitMQ queue).
- `GET /api/v1/machines` - Lists registered computers.

---

## SystemMonitorService (Client)

Windows service that samples CPU, RAM, and fixed drive usage every second and pushes batches to the collector.

### Build
```bash
dotnet build SystemMonitorService/SystemMonitorService.csproj
```

### Configuration
Edit `SystemMonitorService/appsettings.json`:
- `MonitorSettings:CollectorEndpoint` - REST endpoint (e.g., `https://<collector-ip>:5101/api/v1/metrics`).

---

## SystemMonitorMobile (App)

MAUI application to monitor your fleet from anywhere.

### Configuration
Edit `SystemMonitorMobile/Resources/Raw/appsettings.json`:
- `CollectorSettings:BaseUrl` - Base URL of the collector (e.g., `https://<collector-ip>:5101`).