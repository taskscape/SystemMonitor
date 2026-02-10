# SystemMonitor

A distributed system monitoring solution consisting of a Windows Service (Collector), a Client Service (Monitor), and a Mobile App.

## Features

- **Real-time Monitoring:** Tracks CPU, RAM, Disk usage, and top processes.
- **Remote Commands:** Trigger system restarts directly from the Web UI or Mobile App.
- **Asynchronous Ingestion:** Uses RabbitMQ to handle high-volume metrics data.
- **History & Analytics:** 7-day retention with aggregated minute-by-minute cache.
- **Auto-Cleanup:** Built-in background service to prune old data automatically.

## Infrastructure (Server-side)

The system uses **RabbitMQ** for metrics queuing and **PostgreSQL** for storage. The easiest way to start the infrastructure is using Docker:

```bash
docker-compose up -d
```

This will start:
- **PostgreSQL** on port `5433` (mapped from `5432`)
- **RabbitMQ** on ports `5672` (AMQP) and `15672` (Management UI)

## SystemCollectorService (Server)

A central hub that receives payloads via RabbitMQ or REST, stores them in PostgreSQL, and hosts a web UI for fleet management.

### Configuration
Edit `SystemCollectorService/appsettings.json`:
- `CollectorSettings:ConnectionString` - PostgreSQL connection string.
- `CollectorSettings:ListenUrl` - HTTPS listen address (default `https://0.0.0.0:5101`).
- `CollectorSettings:RetentionDays` - How many days of data to keep (default `7`).

### UI & API
- **Web UI:** `https://<collector-host>:5101/`
- `POST /api/v1/metrics` - Ingests metrics (queued via RabbitMQ).
- `POST /api/v1/machines/{name}/commands` - Sends remote commands (e.g., `restart`).

---

## SystemMonitorService (Client)

Windows service that samples system metrics and pushes them to the collector.

### Configuration
Edit `SystemMonitorService/appsettings.json`:
- `MonitorSettings:CollectorEndpoint` - REST endpoint (e.g., `https://<collector-ip>:5101/api/v1/metrics`).
- `MonitorSettings:PushIntervalSeconds` - Frequency of data uploads (default `30`).

---

## SystemMonitorMobile (App)

MAUI application to monitor your fleet from anywhere. Supports Android, iOS, and Windows.

### Configuration
- Enter the Server URL on the first launch (e.g., `https://<collector-ip>:5101`).
- Supports SSL bypass for development environments.